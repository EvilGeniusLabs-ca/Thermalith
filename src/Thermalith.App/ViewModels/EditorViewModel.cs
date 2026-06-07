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

    /// <summary>The eight resize-handle rectangles for the current selection (display coords).</summary>
    public ObservableCollection<HandleSpec> SelectionHandles { get; } = [];

    private const double HandleSize = 9;

    [ObservableProperty] private Bitmap? _preview;
    [ObservableProperty] private LayerItemViewModel? _selectedLayer;
    [ObservableProperty] private ElementEditorViewModel? _selectedEditor;
    [ObservableProperty] private double _zoom = 2.0;
    [ObservableProperty] private double _displayWidth;
    [ObservableProperty] private double _displayHeight;
    [ObservableProperty] private Rect _selectionBounds;
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private Rect _safeAreaBounds;
    [ObservableProperty] private bool _hasSafeArea;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _dirty;
    [ObservableProperty] private bool _showGrid;
    [ObservableProperty] private bool _snapEnabled;

    /// <summary>Grid pitch in mm (also the snap increment).</summary>
    public double GridMm { get; } = 2.0;

    /// <summary>Grid spacing in display pixels (mm pitch × pxPerMm × zoom).</summary>
    public double GridSpacingDisplay => _live.Canvas.Dpi / 25.4 * Zoom * GridMm;

    public int PreviewWidthPx { get; private set; }
    public int PreviewHeightPx { get; private set; }

    private CanvasEditorViewModel _canvasEditor = null!;

    /// <summary>What the inspector shows: the selected element editor, or the canvas editor when nothing is selected (§7).</summary>
    public object InspectorTarget => SelectedEditor ?? (object)_canvasEditor;

    /// <summary>Render the current document to the 1bpp raster for printing (same path as the preview, §6.3).</summary>
    public Niimbot.Net.Encoding.MonochromeBitmap RenderForPrint() =>
        _renderer.Render(_live, new ResolveContext { Now = DateTimeOffset.Now, Assets = _assets, RowIndex = 0 });
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
        _canvasEditor = new CanvasEditorViewModel(_live, OnCanvasEdited);
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

    public void AddElement(string type)
    {
        FlushGesture();
        var el = ElementFactory.Create(type, _live.Canvas);
        _live = _live with { Elements = _live.Elements.Append(el).ToList() };
        _history.Commit(_live);
        RebuildLayers();
        SelectedLayer = Layers.FirstOrDefault(l => l.Id == el.Id);
        MarkDirty();
        RenderNow();
        RaiseState();
    }

    // ── Z-order (Arrange, §6.2) ─────────────────────────────────────────────────────────────────

    public void BringToFront() => Reorder(els => { var e = Take(els, out var rest); rest.Add(e); return rest; });
    public void SendToBack() => Reorder(els => { var e = Take(els, out var rest); rest.Insert(0, e); return rest; });
    public void BringForward() => Shift(+1);
    public void SendBackward() => Shift(-1);

    private void Shift(int delta) => Reorder(els =>
    {
        var id = SelectedEditor!.Id;
        var idx = els.FindIndex(e => e.Id == id);
        var target = Math.Clamp(idx + delta, 0, els.Count - 1);
        if (target == idx) return els;
        var e = els[idx];
        els.RemoveAt(idx);
        els.Insert(target, e);
        return els;
    });

    private void Reorder(Func<List<LabelElement>, List<LabelElement>> op)
    {
        if (SelectedEditor is null) return;
        FlushGesture();
        var keepId = SelectedEditor.Id;
        _live = _live with { Elements = op(new List<LabelElement>(_live.Elements)) };
        _history.Commit(_live);
        RebuildLayers();
        SelectedLayer = Layers.FirstOrDefault(l => l.Id == keepId);
        MarkDirty();
        RenderNow();
        RaiseState();
    }

    private LabelElement Take(List<LabelElement> els, out List<LabelElement> rest)
    {
        var id = SelectedEditor!.Id;
        var e = els.First(x => x.Id == id);
        rest = els.Where(x => x.Id != id).ToList();
        return e;
    }

    // ── Interactive drag (move + resize, §7) ────────────────────────────────────────────────────

    /// <summary>The selected element's geometry (mm), captured at drag start.</summary>
    public GeomMm? SelectedGeometry() => SelectedEditor is { } ed ? new GeomMm(ed.X, ed.Y, ed.W, ed.H) : null;

    /// <summary>Begin a pointer-driven move/resize gesture; the next <see cref="EndInteractive"/> is its single undo checkpoint.</summary>
    public void BeginInteractive() => FlushGesture();

    /// <summary>Commit the drag as one undo checkpoint.</summary>
    public void EndInteractive()
    {
        _history.Commit(_live);
        RaiseState();
    }

    /// <summary>Apply a drag delta (display px) against the geometry captured at drag start.</summary>
    public void DragApply(DragMode mode, Handle handle, GeomMm start, double deltaXDisplay, double deltaYDisplay)
    {
        if (SelectedEditor is null) return;
        var perMm = _live.Canvas.Dpi / 25.4 * Zoom;
        var dmx = deltaXDisplay / perMm;
        var dmy = deltaYDisplay / perMm;

        double x = start.X, y = start.Y, w = start.W, h = start.H;
        if (mode == DragMode.Move)
        {
            x = Snap(start.X + dmx);
            y = Snap(start.Y + dmy);
            ApplyGeometry(x, y, w, h);
            return;
        }
        {
            var left = handle is Handle.TopLeft or Handle.Left or Handle.BottomLeft;
            var right = handle is Handle.TopRight or Handle.Right or Handle.BottomRight;
            var top = handle is Handle.TopLeft or Handle.Top or Handle.TopRight;
            var bottom = handle is Handle.BottomLeft or Handle.Bottom or Handle.BottomRight;

            if (left) { x = start.X + dmx; w = start.W - dmx; }
            if (right) w = start.W + dmx;
            if (top) { y = start.Y + dmy; h = start.H - dmy; }
            if (bottom) h = start.H + dmy;

            const double min = 1.0;
            if (w < min) { if (left) x = start.X + start.W - min; w = min; }
            if (h < min) { if (top) y = start.Y + start.H - min; h = min; }
        }

        ApplyGeometry(Snap(x), Snap(y), Snap(w), Snap(h));
    }

    private void ApplyGeometry(double x, double y, double w, double h)
    {
        if (SelectedEditor is not { } ed) return;
        ed.SetGeometrySilently(x, y, w, h);
        ReplaceElement(ed.ToElement());
        MarkDirty();
        UpdateSelectionBounds();
        RequestRender();
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
        _canvasEditor = new CanvasEditorViewModel(_live, OnCanvasEdited);
        RebuildLayers();
        SelectedLayer = keepId is null ? null : Layers.FirstOrDefault(l => l.Id == keepId);
        if (SelectedLayer is null) OnPropertyChanged(nameof(InspectorTarget));
        Dirty = true;
        RenderNow();
        RaiseState();
    }

    private void OnCanvasEdited()
    {
        _live = _canvasEditor.Apply(_live);
        BeginGesture();
        MarkDirty();
        RequestRender();
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

    partial void OnSelectedEditorChanged(ElementEditorViewModel? value) => OnPropertyChanged(nameof(InspectorTarget));

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
            SelectionHandles.Clear();
            return;
        }
        var s = _live.Canvas.Dpi / 25.4 * Zoom;
        var r = new Rect(ed.X * s, ed.Y * s, ed.W * s, ed.H * s);
        SelectionBounds = r;
        HasSelection = true;
        RebuildHandles(r);
    }

    private void RebuildHandles(Rect r)
    {
        SelectionHandles.Clear();
        const double hs = HandleSize, half = HandleSize / 2;
        double l = r.X, t = r.Y, cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, rt = r.Right, bm = r.Bottom;
        void Add(double x, double y, Handle k) => SelectionHandles.Add(new HandleSpec(x - half, y - half, hs, k));
        Add(l, t, Handle.TopLeft);
        Add(cx, t, Handle.Top);
        Add(rt, t, Handle.TopRight);
        Add(rt, cy, Handle.Right);
        Add(rt, bm, Handle.BottomRight);
        Add(cx, bm, Handle.Bottom);
        Add(l, bm, Handle.BottomLeft);
        Add(l, cy, Handle.Left);
    }

    /// <summary>Hit-test the resize handles (with a small tolerance) for a point in display coordinates.</summary>
    public Handle? HitTestHandle(double dx, double dy)
    {
        const double pad = 3;
        foreach (var h in SelectionHandles)
            if (dx >= h.Left - pad && dx <= h.Left + h.Size + pad && dy >= h.Top - pad && dy <= h.Top + h.Size + pad)
                return h.Kind;
        return null;
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
            UpdateSafeArea();
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
        OnPropertyChanged(nameof(GridSpacingDisplay));
        UpdateSafeArea();
    }

    private void UpdateSafeArea()
    {
        if (_live.Canvas.SafeAreaInsetMm is { } inset && inset > 0)
        {
            var s = _live.Canvas.Dpi / 25.4 * Zoom;
            SafeAreaBounds = new Rect(
                inset * s, inset * s,
                Math.Max(0, _live.Canvas.WidthMm - 2 * inset) * s,
                Math.Max(0, _live.Canvas.HeightMm - 2 * inset) * s);
            HasSafeArea = true;
        }
        else
        {
            HasSafeArea = false;
        }
    }

    private double Snap(double mm) => SnapEnabled ? Math.Round(mm / GridMm) * GridMm : mm;

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
