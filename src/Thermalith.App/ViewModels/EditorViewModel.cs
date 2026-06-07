using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.App.Rendering;
using Thermalith.App.ViewModels.Editors;
using Thermalith.Core.Fonts;
using Thermalith.Core.History;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;

namespace Thermalith.App.ViewModels;

/// <summary>
/// The editor's document state and live WYSIWYG (build spec §7): holds the open <see cref="LabelDocument"/>,
/// snapshot history (§6.4), selection, and the Core-rendered preview. Edits flow from the inspector
/// editor VMs, are coalesced into one undo checkpoint per gesture, and re-render through a debounce so
/// rapid changes don't thrash the renderer.
/// </summary>
public sealed partial class EditorViewModel : ObservableObject
{
    private readonly FontService _fonts = new();
    private readonly LabelRenderer _renderer;
    private readonly DispatcherTimer _renderDebounce;
    private readonly DispatcherTimer _gestureSettle;

    private LabelDocument _live = DocumentFactory.New();
    private SnapshotHistory _history;
    private Manifest _manifest = new() { Id = DocumentFactory.NewId(), Name = "Untitled" };
    private IReadOnlyDictionary<string, byte[]> _assets = new Dictionary<string, byte[]>();
    private bool _gestureActive;

    public EditorViewModel()
    {
        _renderer = new LabelRenderer(_fonts);
        _history = new SnapshotHistory(_live);
        _renderDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _renderDebounce.Tick += (_, _) => { _renderDebounce.Stop(); RenderNow(); };
        _gestureSettle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _gestureSettle.Tick += (_, _) => { _gestureSettle.Stop(); FlushGesture(); };

        Layers = [];
        LoadInternal(_live, null, null, new Dictionary<string, byte[]>());
    }

    public ObservableCollection<LayerItemViewModel> Layers { get; }

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private LayerItemViewModel? _selectedLayer;
    [ObservableProperty] private ElementEditorViewModel? _selectedEditor;
    [ObservableProperty] private double _zoom = 2.0;
    [ObservableProperty] private double _displayWidth;
    [ObservableProperty] private double _displayHeight;
    [ObservableProperty] private Rect _selectionBounds;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _dirty;

    public int PreviewWidthPx { get; private set; }
    public int PreviewHeightPx { get; private set; }
    public string? FilePath { get; private set; }
    public string DocumentName => _live.Metadata.Name;
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;

    /// <summary>Raised whenever undo/redo/dirty/file state changes so the shell can refresh command enablement + title.</summary>
    public event EventHandler? StateChanged;

    // ── Document lifecycle ────────────────────────────────────────────────────────────────────

    public void NewDocument() => LoadInternal(DocumentFactory.New(), new Manifest { Id = DocumentFactory.NewId(), Name = "Untitled" }, null, new Dictionary<string, byte[]>());

    public void LoadPackage(LabelPackage package, string path) =>
        LoadInternal(package.Document, package.Manifest, path, package.Assets);

    public void SaveTo(string path)
    {
        FlushGesture();
        _manifest = _manifest with
        {
            Name = _live.Metadata.Name,
            Updated = DateTimeOffset.UtcNow.ToString("o"),
        };
        var package = new LabelPackage { Manifest = _manifest, Document = _live, Assets = _assets };
        LabelPackageIo.Save(package, path);
        FilePath = path;
        Dirty = false;
        RaiseState();
    }

    private void LoadInternal(LabelDocument doc, Manifest? manifest, string? path, IReadOnlyDictionary<string, byte[]> assets)
    {
        _live = doc;
        _manifest = manifest ?? new Manifest { Id = DocumentFactory.NewId(), Name = doc.Metadata.Name };
        _assets = assets;
        _history = new SnapshotHistory(doc);
        _gestureActive = false;
        FilePath = path;
        Dirty = false;
        RebuildLayers();
        SelectedLayer = null;
        FitToWidth();
        RenderNow();
        RaiseState();
    }

    // ── Editing ───────────────────────────────────────────────────────────────────────────────

    private void OnElementEdited()
    {
        if (SelectedEditor is null) return;
        var updated = SelectedEditor.ToElement();
        ReplaceElement(updated);

        var layer = Layers.FirstOrDefault(l => l.Id == updated.Id);
        if (layer is not null) { layer.Name = updated.Name ?? updated.Type; layer.Visible = updated.Visible; }

        BeginGesture();
        MarkDirty();
        UpdateSelectionBounds();
        RequestRender();
    }

    private void ReplaceElement(LabelElement el)
    {
        var idx = _live.Elements.FindIndex(e => e.Id == el.Id);
        if (idx < 0) return;
        var list = new List<LabelElement>(_live.Elements) { [idx] = el };
        _live = _live with { Elements = list };
    }

    public void DeleteSelected()
    {
        if (SelectedEditor is not { } editor) return;
        FlushGesture();
        _live = _live with { Elements = _live.Elements.Where(e => e.Id != editor.Id).ToList() };
        _history.Commit(_live);
        RebuildLayers();
        SelectedLayer = null;
        MarkDirty();
        RenderNow();
        RaiseState();
    }

    public void Undo()
    {
        FlushGesture();
        if (!_history.CanUndo) return;
        _live = _history.Undo();
        AfterHistoryChange();
    }

    public void Redo()
    {
        FlushGesture();
        if (!_history.CanRedo) return;
        _live = _history.Redo();
        AfterHistoryChange();
    }

    private void AfterHistoryChange()
    {
        var keepId = SelectedEditor?.Id;
        RebuildLayers();
        SelectedLayer = keepId is null ? null : Layers.FirstOrDefault(l => l.Id == keepId);
        Dirty = true;
        RenderNow();
        RaiseState();
    }

    // ── Gestures (history coalescing, §6.4) ─────────────────────────────────────────────────────

    private void BeginGesture()
    {
        _gestureActive = true;
        _gestureSettle.Stop();
        _gestureSettle.Start();
    }

    private void FlushGesture()
    {
        _gestureSettle.Stop();
        if (!_gestureActive) return;
        _gestureActive = false;
        _history.Commit(_live);
        RaiseState();
    }

    // ── Selection + hit testing ─────────────────────────────────────────────────────────────────

    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        var el = value is null ? null : _live.Elements.FirstOrDefault(e => e.Id == value.Id);
        SelectedEditor = el is null ? null : ElementEditorViewModel.Create(el, OnElementEdited);
        UpdateSelectionBounds();
    }

    /// <summary>Select the topmost (frontmost) element under a point in display coordinates, or clear selection.</summary>
    public void HitTestSelect(double displayX, double displayY)
    {
        var pxPerMm = _live.Canvas.Dpi / 25.4;
        var mmX = displayX / Zoom / pxPerMm;
        var mmY = displayY / Zoom / pxPerMm;
        for (var i = _live.Elements.Count - 1; i >= 0; i--)
        {
            var e = _live.Elements[i];
            if (mmX >= e.X && mmX <= e.X + e.W && mmY >= e.Y && mmY <= e.Y + e.H)
            {
                SelectedLayer = Layers.FirstOrDefault(l => l.Id == e.Id);
                return;
            }
        }
        SelectedLayer = null;
    }

    private void UpdateSelectionBounds()
    {
        if (SelectedEditor is not { } ed)
        {
            HasSelection = false;
            return;
        }
        var s = _live.Canvas.Dpi / 25.4 * Zoom;
        SelectionBounds = new Rect(ed.X * s, ed.Y * s, ed.W * s, ed.H * s);
        HasSelection = true;
    }

    // ── Rendering ───────────────────────────────────────────────────────────────────────────────

    private void RequestRender()
    {
        _renderDebounce.Stop();
        _renderDebounce.Start();
    }

    private void RenderNow()
    {
        try
        {
            var ctx = new ResolveContext { Now = DateTimeOffset.Now, Assets = _assets, RowIndex = 0 };
            var mono = _renderer.Render(_live, ctx);
            Preview = PreviewImage.FromMonochrome(mono);
            PreviewWidthPx = mono.WidthPx;
            PreviewHeightPx = mono.HeightPx;
            UpdateDisplaySize();
            UpdateSelectionBounds();
            StatusText = $"{_live.Canvas.WidthMm:0.#} × {_live.Canvas.HeightMm:0.#} mm · {_live.Canvas.Dpi} dpi · {mono.WidthPx}×{mono.HeightPx} px";
        }
        catch (Exception ex)
        {
            StatusText = "Render error: " + ex.Message;
        }
    }

    // ── Zoom ────────────────────────────────────────────────────────────────────────────────────

    public void ZoomIn() => Zoom = Math.Min(12.0, Zoom * 1.25);
    public void ZoomOut() => Zoom = Math.Max(0.25, Zoom / 1.25);
    public void FitToWidth() => Zoom = PreviewWidthPx > 0 ? Math.Clamp(720.0 / PreviewWidthPx, 0.25, 12.0) : 2.0;

    partial void OnZoomChanged(double value)
    {
        UpdateDisplaySize();
        UpdateSelectionBounds();
    }

    private void UpdateDisplaySize()
    {
        DisplayWidth = PreviewWidthPx * Zoom;
        DisplayHeight = PreviewHeightPx * Zoom;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private void RebuildLayers()
    {
        Layers.Clear();
        for (var i = _live.Elements.Count - 1; i >= 0; i--) // frontmost first
            Layers.Add(new LayerItemViewModel(_live.Elements[i]));
    }

    private void MarkDirty()
    {
        if (Dirty) return;
        Dirty = true;
        RaiseState();
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(DocumentName));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
