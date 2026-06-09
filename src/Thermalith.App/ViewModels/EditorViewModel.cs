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

    /// <summary>The eight resize-handle rectangles for the current selection (display coords); only shown for a single selection.</summary>
    public ObservableCollection<HandleSpec> SelectionHandles { get; } = [];

    /// <summary>A dashed outline rectangle per selected element (display coords) — supports multi-select.</summary>
    public ObservableCollection<SelRect> SelectionRects { get; } = [];

    private const double HandleSize = 9;

    private readonly List<string> _selectedIds = [];
    private bool _syncingSelection;
    private Dictionary<string, GeomMm>? _dragGeoms;
    private DragMode _dragMode = DragMode.None;
    private Handle _dragHandle;
    private bool _dragMoved;

    /// <summary>Number of currently-selected elements (drives align/distribute enablement).</summary>
    public int SelectionCount => _selectedIds.Count;

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
    [ObservableProperty] private bool _showSafeArea = true;
    [ObservableProperty] private bool _smoothPreview;

    // Re-render when the preview mode flips (exact 1-bit ↔ smooth grayscale, §6.3.6).
    partial void OnSmoothPreviewChanged(bool value) => RequestRender();

    /// <summary>Grid pitch in mm (also the snap increment).</summary>
    public double GridMm { get; } = 2.0;

    /// <summary>Grid spacing in display pixels (mm pitch × pxPerMm × zoom).</summary>
    public double GridSpacingDisplay => _live.Canvas.Dpi / 25.4 * Zoom * GridMm;

    /// <summary>On-screen pixels per mm (dpi/25.4 × zoom) — the canvas rulers read this for their scale.</summary>
    public double DisplayPxPerMm => _live.Canvas.Dpi / 25.4 * Zoom;

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

    /// <summary>Embed image bytes as a package asset and point the selected Image element at it. The
    /// asset travels with the saved <c>.nlbl</c> (it goes into <see cref="LabelPackage.Assets"/>).</summary>
    public bool SetImageAssetOnSelection(byte[] bytes, string fileExtension)
    {
        if (SelectedEditor is not ImageEditorViewModel img) return false;
        var ext = string.IsNullOrWhiteSpace(fileExtension) ? ".png" : fileExtension.ToLowerInvariant();
        var id = "img_" + Guid.NewGuid().ToString("N")[..8] + ext;
        _assets = new Dictionary<string, byte[]>(_assets) { [id] = bytes };
        img.AssetId = id; // fires OnElementEdited → rebuild + re-render with the new asset + history checkpoint
        return true;
    }

    /// <summary>Resize every auto-size text element's box to its measured glyphs — applied on load so
    /// the seed label (and any opened file) opens hugged, not just elements added/edited in-session.</summary>
    private LabelDocument ApplyAutoSizeToText(LabelDocument doc)
    {
        var dpi = doc.Canvas.Dpi;
        var changed = false;
        var elements = doc.Elements.Select(e =>
        {
            if (e is TextElement t && t.Props.AutoSize)
            {
                var (wmm, hmm) = _renderer.MeasureTextMm(t.Props, dpi);
                double w = Math.Ceiling(wmm), h = Math.Ceiling(hmm);
                if (Math.Abs(w - t.W) > 1e-6 || Math.Abs(h - t.H) > 1e-6) { changed = true; return t with { W = w, H = h }; }
            }
            return e;
        }).ToList();
        return changed ? doc with { Elements = elements } : doc;
    }

    private void LoadInternal(LabelDocument doc, Manifest? manifest, string? path, IReadOnlyDictionary<string, byte[]> assets)
    {
        _manifest = manifest ?? new Manifest { Id = DocumentFactory.NewId(), Name = doc.Metadata.Name };
        _assets = assets;
        _live = ApplyAutoSizeToText(doc); // hug auto-size text boxes (the seed label + any loaded file)
        _history = new SnapshotHistory(_live);
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
        var existing = _live.Elements.FirstOrDefault(e => e.Id == SelectedEditor.Id);
        var updated = SelectedEditor.ToElement();
        if (existing is not null) updated = updated with { GroupId = existing.GroupId }; // editors don't carry grouping
        if (updated is TextElement t && t.Props.AutoSize) // box hugs the text: keep W/H = measured glyphs
        {
            var (wmm, hmm) = _renderer.MeasureTextMm(t.Props, _live.Canvas.Dpi);
            double w = Math.Ceiling(wmm), h = Math.Ceiling(hmm);
            updated = t with { W = w, H = h };
            SelectedEditor.SetGeometrySilently(t.X, t.Y, w, h); // reflect measured size in the inspector
        }
        ReplaceElement(updated);

        var layer = Layers.FirstOrDefault(l => l.Id == updated.Id);
        layer?.Sync(updated.Name ?? updated.Type, updated.Visible, updated.Locked);

        BeginGesture();
        MarkDirty();
        UpdateSelectionVisuals();
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
        if (_selectedIds.Count == 0) return;
        FlushGesture();
        var ids = new HashSet<string>(_selectedIds);
        _live = _live with { Elements = _live.Elements.Where(e => !ids.Contains(e.Id)).ToList() };
        _history.Commit(_live);
        RebuildLayers();
        SetSelection([], null);
        MarkDirty();
        RenderNow();
        RaiseState();
    }

    // ── Grouping (§6.2) ─────────────────────────────────────────────────────────────────────────

    /// <summary>True when the selection contains at least one grouped element (enables Ungroup).</summary>
    public bool HasGroupInSelection =>
        _selectedIds.Any(id => _live.Elements.FirstOrDefault(e => e.Id == id)?.GroupId is not null);

    public void Group()
    {
        if (_selectedIds.Count < 2) return;
        var gid = "grp_" + Guid.NewGuid().ToString("N")[..8];
        var sel = new HashSet<string>(_selectedIds);
        CommitTransform(e => sel.Contains(e.Id) ? e with { GroupId = gid } : e);
    }

    public void Ungroup()
    {
        var groups = new HashSet<string>(
            _selectedIds.Select(id => _live.Elements.FirstOrDefault(e => e.Id == id)?.GroupId).OfType<string>());
        if (groups.Count == 0) return;
        CommitTransform(e => e.GroupId is { } g && groups.Contains(g) ? e with { GroupId = null } : e);
    }

    // ── Visibility / lock toggles (one toggle for the whole selection) ───────────────────────────

    /// <summary>Lock the selection; if everything in it is already locked, unlock instead.</summary>
    public void ToggleLock()
    {
        if (_selectedIds.Count == 0) return;
        var sel = new HashSet<string>(_selectedIds);
        var target = !_live.Elements.Where(e => sel.Contains(e.Id)).All(e => e.Locked);
        CommitTransform(e => sel.Contains(e.Id) ? e with { Locked = target } : e);
    }

    /// <summary>Hide the selection; if everything in it is already hidden, show instead.</summary>
    public void ToggleVisible()
    {
        if (_selectedIds.Count == 0) return;
        var sel = new HashSet<string>(_selectedIds);
        var target = !_live.Elements.Where(e => sel.Contains(e.Id)).All(e => e.Visible);
        CommitTransform(e => sel.Contains(e.Id) ? e with { Visible = target } : e);
    }

    /// <summary>Set one element's visibility from the Elements-list eye toggle. Commits history and
    /// re-renders but does NOT rebuild the layer list — the toggled row already holds the new value, so
    /// skipping the rebuild avoids selection churn (which would otherwise trip the list-click focus).</summary>
    public void SetElementVisible(string id, bool visible) => SetElementFlag(id, e => e with { Visible = visible });

    /// <summary>Set one element's lock state from the Elements-list lock toggle (see <see cref="SetElementVisible"/>).</summary>
    public void SetElementLocked(string id, bool locked) => SetElementFlag(id, e => e with { Locked = locked });

    private void SetElementFlag(string id, Func<LabelElement, LabelElement> map)
    {
        if (_live.Elements.FirstOrDefault(e => e.Id == id) is null) return;
        FlushGesture();
        _live = _live with { Elements = _live.Elements.Select(e => e.Id == id ? map(e) : e).ToList() };
        _history.Commit(_live);
        MarkDirty();
        RenderNow();
        UpdateSelectionVisuals(); // a locked/hidden change on the selected element refreshes its adorner
        RaiseState();
    }

    // ── Clipboard (Copy/Cut/Paste/Duplicate, §7.2) ──────────────────────────────────────────────

    private readonly List<LabelElement> _clipboard = [];
    public bool HasClipboard => _clipboard.Count > 0;

    public void Copy()
    {
        _clipboard.Clear();
        _clipboard.AddRange(_live.Elements.Where(e => _selectedIds.Contains(e.Id)));
        OnPropertyChanged(nameof(HasClipboard));
    }

    public void Cut()
    {
        if (_selectedIds.Count == 0) return;
        Copy();
        DeleteSelected();
    }

    public void Paste() => AddClones(_clipboard);
    public void Duplicate() => AddClones(_live.Elements.Where(e => _selectedIds.Contains(e.Id)).ToList());

    private void AddClones(IReadOnlyList<LabelElement> source)
    {
        if (source.Count == 0) return;
        FlushGesture();
        var clones = CloneWithNewIds(source, offsetMm: 2);
        _live = _live with { Elements = _live.Elements.Concat(clones).ToList() };
        _history.Commit(_live);
        RebuildLayers();
        SetSelection(clones.Select(c => c.Id).ToList(), clones[^1].Id);
        MarkDirty();
        RenderNow();
        RaiseState();
    }

    private static List<LabelElement> CloneWithNewIds(IReadOnlyList<LabelElement> source, double offsetMm)
    {
        var groupRemap = new Dictionary<string, string>();
        var result = new List<LabelElement>(source.Count);
        foreach (var e in source)
        {
            string? newGroup = null;
            if (e.GroupId is { } g)
            {
                if (!groupRemap.TryGetValue(g, out var ng))
                {
                    ng = "grp_" + Guid.NewGuid().ToString("N")[..8];
                    groupRemap[g] = ng;
                }
                newGroup = ng;
            }
            result.Add(e with { Id = DocumentFactory.NewId(), X = e.X + offsetMm, Y = e.Y + offsetMm, GroupId = newGroup });
        }
        return result;
    }

    public void AddElement(string type)
    {
        FlushGesture();
        var el = ElementFactory.Create(type, _live.Canvas);
        if (el is TextElement t && t.Props.AutoSize) // start an auto-size text already hugging its glyphs
        {
            var (wmm, hmm) = _renderer.MeasureTextMm(t.Props, _live.Canvas.Dpi);
            el = t with { W = Math.Ceiling(wmm), H = Math.Ceiling(hmm) };
        }
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

    public bool IsDragging => _dragMode != DragMode.None;

    /// <summary>Begin a pointer-driven gesture, capturing every selected element's start geometry. Resize requires a single selection.</summary>
    public void BeginDrag(DragMode mode, Handle handle)
    {
        if (_selectedIds.Count == 0) return;
        FlushGesture();
        _dragHandle = handle;
        _dragMoved = false;
        _dragGeoms = new Dictionary<string, GeomMm>();
        // Locked elements are immovable — exclude them so the gesture leaves them in place.
        foreach (var id in _selectedIds)
            if (_live.Elements.FirstOrDefault(e => e.Id == id) is { Locked: false } e)
                _dragGeoms[id] = new GeomMm(e.X, e.Y, e.W, e.H);
        // Nothing draggable (whole selection locked) → no gesture.
        _dragMode = _dragGeoms.Count == 0 ? DragMode.None : mode;
    }

    /// <summary>Apply the running drag delta (display px) against the captured start geometry.</summary>
    public void DragTo(double deltaXDisplay, double deltaYDisplay)
    {
        if (_dragMode == DragMode.None || _dragGeoms is null) return;
        _dragMoved = true;
        var perMm = _live.Canvas.Dpi / 25.4 * Zoom;
        var dmx = deltaXDisplay / perMm;
        var dmy = deltaYDisplay / perMm;

        var list = new List<LabelElement>(_live.Elements);
        if (_dragMode == DragMode.Move)
        {
            foreach (var (id, g) in _dragGeoms)
            {
                var (nx, ny) = SnapMove(g.X + dmx, g.Y + dmy, g.W, g.H);
                ReplaceIn(list, id, e => e with { X = nx, Y = ny });
            }
        }
        else if (_dragGeoms.Count == 1)
        {
            var (id, g) = _dragGeoms.First();
            var (x, y, w, h) = ResizeGeom(_dragHandle, g, dmx, dmy);
            var (sx, sy, sw, sh) = SnapResize(_dragHandle, x, y, w, h);
            ReplaceIn(list, id, e => e with { X = sx, Y = sy, W = sw, H = sh });
        }

        _live = _live with { Elements = list };
        RefreshPrimaryEditorGeometry();
        MarkDirty();
        UpdateSelectionVisuals();
        RequestRender();
    }

    /// <summary>Commit the drag as one undo checkpoint.</summary>
    public void EndDrag()
    {
        if (_dragMoved) { _history.Commit(_live); RaiseState(); }
        _dragMode = DragMode.None;
        _dragGeoms = null;
    }

    /// <summary>Scale every selected element about its own centre (scroll-wheel resize, §7). Coalesced into one undo via the gesture settle.</summary>
    public void ScaleSelection(double factor)
    {
        if (_selectedIds.Count == 0) return;
        var list = new List<LabelElement>(_live.Elements);
        foreach (var id in _selectedIds)
        {
            if (_live.Elements.FirstOrDefault(e => e.Id == id) is { Locked: true }) continue; // locked = no scroll-resize
            ReplaceIn(list, id, e =>
            {
                var nw = Math.Max(1, Math.Round(e.W * factor));
                var nh = Math.Max(1, Math.Round(e.H * factor));
                return e with { W = nw, H = nh, X = Math.Round(e.X + (e.W - nw) / 2), Y = Math.Round(e.Y + (e.H - nh) / 2) };
            });
        }
        _live = _live with { Elements = list };
        RefreshPrimaryEditorGeometry();
        BeginGesture();
        MarkDirty();
        UpdateSelectionVisuals();
        RequestRender();
    }

    private static (double X, double Y, double W, double H) ResizeGeom(Handle handle, GeomMm g, double dmx, double dmy)
    {
        var left = handle is Handle.TopLeft or Handle.Left or Handle.BottomLeft;
        var right = handle is Handle.TopRight or Handle.Right or Handle.BottomRight;
        var top = handle is Handle.TopLeft or Handle.Top or Handle.TopRight;
        var bottom = handle is Handle.BottomLeft or Handle.Bottom or Handle.BottomRight;

        double x = g.X, y = g.Y, w = g.W, h = g.H;
        if (left) { x = g.X + dmx; w = g.W - dmx; }
        if (right) w = g.W + dmx;
        if (top) { y = g.Y + dmy; h = g.H - dmy; }
        if (bottom) h = g.H + dmy;

        const double min = 1.0;
        if (w < min) { if (left) x = g.X + g.W - min; w = min; }
        if (h < min) { if (top) y = g.Y + g.H - min; h = min; }
        return (x, y, w, h);
    }

    private static void ReplaceIn(List<LabelElement> list, string id, Func<LabelElement, LabelElement> map)
    {
        var idx = list.FindIndex(e => e.Id == id);
        if (idx >= 0) list[idx] = map(list[idx]);
    }

    private void RefreshPrimaryEditorGeometry()
    {
        if (SelectedEditor is { } ed && _live.Elements.FirstOrDefault(e => e.Id == ed.Id) is { } el)
            ed.SetGeometrySilently(el.X, el.Y, el.W, el.H);
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

    /// <summary>
    /// Apply a loaded/selected roll's physical size + shape (and printer DPI) to the canvas (worklist §B).
    /// When <paramref name="maxWidthMm"/> is given (the connected printer's printable width), the canvas
    /// width is clamped to it — a roll's stock width (e.g. 50 mm) can exceed what the printhead can image
    /// (e.g. 48 mm / 384 px), so the canvas tracks the printable area, not the media width (worklist §A6).
    /// Returns true if the width was clamped below the roll's stock width.
    /// </summary>
    public bool ApplyRoll(double widthMm, double heightMm, string? shape, int? dpi, double? maxWidthMm = null)
    {
        FlushGesture();
        var w = widthMm > 0 ? widthMm : _live.Canvas.WidthMm;
        var clamped = maxWidthMm is > 0 && w > maxWidthMm.Value;
        if (clamped) w = maxWidthMm!.Value;
        _live = _live with
        {
            Canvas = _live.Canvas with
            {
                WidthMm = w,
                HeightMm = heightMm > 0 ? heightMm : _live.Canvas.HeightMm,
                Shape = string.IsNullOrEmpty(shape) ? _live.Canvas.Shape : shape,
                Dpi = dpi is > 0 ? dpi.Value : _live.Canvas.Dpi,
            },
        };
        _history.Commit(_live);
        _canvasEditor = new CanvasEditorViewModel(_live, OnCanvasEdited);
        if (SelectedEditor is null) OnPropertyChanged(nameof(InspectorTarget));
        MarkDirty();
        RenderNow();
        RaiseState();
        return clamped;
    }

    /// <summary>Canvas width in mm, surfaced for the toolbar quick-edit (worklist §C). Setting it resizes the
    /// canvas as a committed change; the value stays in sync with the inspector via <see cref="RaiseState"/>.</summary>
    public double CanvasWidthMm
    {
        get => _live.Canvas.WidthMm;
        set { if (value > 0 && Math.Abs(value - _live.Canvas.WidthMm) > 1e-6) ResizeCanvas(value, _live.Canvas.HeightMm); }
    }

    /// <summary>Canvas height in mm, surfaced for the toolbar quick-edit (worklist §C).</summary>
    public double CanvasHeightMm
    {
        get => _live.Canvas.HeightMm;
        set { if (value > 0 && Math.Abs(value - _live.Canvas.HeightMm) > 1e-6) ResizeCanvas(_live.Canvas.WidthMm, value); }
    }

    /// <summary>Print-area inset in mm (0 when none).</summary>
    public double SafeAreaInsetMm => _live.Canvas.SafeAreaInsetMm ?? 0;

    /// <summary>Inset used for the drawn guide + edge snapping, rounded UP to a whole mm so it lands on
    /// the same integer grid as element positions — a control at 2mm lines up with the guide instead of
    /// the guide sitting at a fractional 1.5mm the control can't reach. (Interim until the bleed reframe.)</summary>
    public double GuideInsetMm => Math.Ceiling(SafeAreaInsetMm);

    private void ResizeCanvas(double widthMm, double heightMm)
    {
        FlushGesture();
        _live = _live with { Canvas = _live.Canvas with { WidthMm = widthMm, HeightMm = heightMm } };
        _history.Commit(_live);
        _canvasEditor = new CanvasEditorViewModel(_live, OnCanvasEdited);
        if (SelectedEditor is null) OnPropertyChanged(nameof(InspectorTarget));
        MarkDirty();
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

    // The layers list drives a single primary selection; multi-select happens on the canvas.
    partial void OnSelectedLayerChanged(LayerItemViewModel? value)
    {
        if (_syncingSelection) return;
        SetSelection(value is null ? [] : [value.Id], value?.Id);
    }

    partial void OnSelectedEditorChanged(ElementEditorViewModel? value) => OnPropertyChanged(nameof(InspectorTarget));

    /// <summary>The set of selected element ids in z-order.</summary>
    public IReadOnlyList<string> SelectedIds => _selectedIds;

    private void SetSelection(IReadOnlyList<string> ids, string? primary)
    {
        _selectedIds.Clear();
        foreach (var id in ExpandToGroups(ids)) _selectedIds.Add(id);

        var primId = primary is not null && _selectedIds.Contains(primary) ? primary : _selectedIds.LastOrDefault();
        var el = primId is null ? null : _live.Elements.FirstOrDefault(e => e.Id == primId);
        SelectedEditor = el is null ? null : ElementEditorViewModel.Create(el, OnElementEdited);

        _syncingSelection = true;
        SelectedLayer = primId is null ? null : Layers.FirstOrDefault(l => l.Id == primId);
        _syncingSelection = false;

        if (SelectedEditor is null) OnPropertyChanged(nameof(InspectorTarget));
        UpdateSelectionVisuals();
        OnPropertyChanged(nameof(SelectionCount));
        OnPropertyChanged(nameof(HasGroupInSelection));
    }

    /// <summary>Expand a selection so that selecting any group member selects the whole group (atomic groups).</summary>
    private List<string> ExpandToGroups(IReadOnlyList<string> ids)
    {
        var groups = new HashSet<string>();
        foreach (var id in ids)
            if (_live.Elements.FirstOrDefault(e => e.Id == id)?.GroupId is { } g) groups.Add(g);
        if (groups.Count == 0) return ids.Distinct().ToList();

        var set = new HashSet<string>(ids);
        foreach (var e in _live.Elements)
            if (e.GroupId is { } g && groups.Contains(g)) set.Add(e.Id);
        return _live.Elements.Where(e => set.Contains(e.Id)).Select(e => e.Id).ToList(); // element order
    }

    /// <summary>Topmost (frontmost) element id under a display point, or null.</summary>
    public string? HitTest(double displayX, double displayY)
    {
        var pxPerMm = _live.Canvas.Dpi / 25.4;
        var mmX = displayX / Zoom / pxPerMm;
        var mmY = displayY / Zoom / pxPerMm;
        for (var i = _live.Elements.Count - 1; i >= 0; i--)
        {
            var e = _live.Elements[i];
            if (mmX >= e.X && mmX <= e.X + e.W && mmY >= e.Y && mmY <= e.Y + e.H) return e.Id;
        }
        return null;
    }

    /// <summary>Select the element under a point. <paramref name="additive"/> toggles it in the set. Returns true if an element was hit.</summary>
    public bool SelectAt(double displayX, double displayY, bool additive)
    {
        var id = HitTest(displayX, displayY);
        if (id is null)
        {
            if (!additive) SetSelection([], null);
            return false;
        }
        if (additive)
        {
            var ids = new List<string>(_selectedIds);
            if (ids.Remove(id)) SetSelection(ids, ids.LastOrDefault());
            else { ids.Add(id); SetSelection(ids, id); }
        }
        else if (!_selectedIds.Contains(id))
        {
            SetSelection([id], id); // clicking an unselected element selects just it
        }
        return true;
    }

    /// <summary>Select all elements whose display rect intersects the marquee (empty marquee clears selection).</summary>
    public void SelectInRect(Rect marquee)
    {
        var s = _live.Canvas.Dpi / 25.4 * Zoom;
        var ids = new List<string>();
        foreach (var e in _live.Elements)
        {
            var r = new Rect(e.X * s, e.Y * s, e.W * s, e.H * s);
            if (r.Intersects(marquee)) ids.Add(e.Id);
        }
        SetSelection(ids, ids.LastOrDefault());
    }

    private void UpdateSelectionVisuals()
    {
        var s = _live.Canvas.Dpi / 25.4 * Zoom;
        SelectionRects.Clear();
        foreach (var id in _selectedIds)
            if (_live.Elements.FirstOrDefault(e => e.Id == id) is { } e)
                SelectionRects.Add(new SelRect(e.X * s, e.Y * s, e.W * s, e.H * s, e.Locked));

        HasSelection = _selectedIds.Count > 0;

        // Resize handles only make sense for a single, unlocked, manually-sized selection. An auto-size
        // text box is driven by its glyphs, so it shows no handles (toggle Auto-size off to resize).
        var primary = _selectedIds.Count == 1
            ? _live.Elements.FirstOrDefault(e => e.Id == _selectedIds.First())
            : null;
        var noHandles = primary is { Locked: true } or TextElement { Props.AutoSize: true };
        if (_selectedIds.Count == 1 && !noHandles && SelectedEditor is { } ed)
        {
            var r = new Rect(ed.X * s, ed.Y * s, ed.W * s, ed.H * s);
            SelectionBounds = r;
            RebuildHandles(r);
        }
        else
        {
            SelectionHandles.Clear();
        }
    }

    // ── Align / distribute (Arrange across the selection, §6.2) ─────────────────────────────────

    public void AlignLeft() => AlignEdit(s => { var v = s.Min(e => e.X); return e => e with { X = v }; });
    public void AlignRight() => AlignEdit(s => { var v = s.Max(e => e.X + e.W); return e => e with { X = v - e.W }; });
    public void AlignTop() => AlignEdit(s => { var v = s.Min(e => e.Y); return e => e with { Y = v }; });
    public void AlignBottom() => AlignEdit(s => { var v = s.Max(e => e.Y + e.H); return e => e with { Y = v - e.H }; });
    public void AlignCenterH() => AlignEdit(s => { var c = (s.Min(e => e.X) + s.Max(e => e.X + e.W)) / 2; return e => e with { X = c - e.W / 2 }; });
    public void AlignMiddleV() => AlignEdit(s => { var c = (s.Min(e => e.Y) + s.Max(e => e.Y + e.H)) / 2; return e => e with { Y = c - e.H / 2 }; });

    public void DistributeH() => DistributeEdit(horizontal: true);
    public void DistributeV() => DistributeEdit(horizontal: false);

    private void AlignEdit(Func<IReadOnlyList<LabelElement>, Func<LabelElement, LabelElement>> build)
    {
        if (_selectedIds.Count < 2) return;
        var selected = _selectedIds.Select(id => _live.Elements.First(e => e.Id == id)).ToList();
        var map = build(selected);
        var ids = new HashSet<string>(_selectedIds);
        CommitTransform(e => ids.Contains(e.Id) ? map(e) : e);
    }

    private void DistributeEdit(bool horizontal)
    {
        if (_selectedIds.Count < 3) return;
        var sorted = _selectedIds.Select(id => _live.Elements.First(e => e.Id == id))
            .OrderBy(e => horizontal ? e.X + e.W / 2 : e.Y + e.H / 2).ToList();
        double firstC = Center(sorted[0]), lastC = Center(sorted[^1]);
        var step = (lastC - firstC) / (sorted.Count - 1);
        var updated = new Dictionary<string, LabelElement>();
        for (var i = 1; i < sorted.Count - 1; i++)
        {
            var e = sorted[i];
            var c = firstC + i * step;
            updated[e.Id] = horizontal ? e with { X = c - e.W / 2 } : e with { Y = c - e.H / 2 };
        }
        CommitTransform(e => updated.TryGetValue(e.Id, out var u) ? u : e);

        double Center(LabelElement e) => horizontal ? e.X + e.W / 2 : e.Y + e.H / 2;
    }

    private void CommitTransform(Func<LabelElement, LabelElement> map)
    {
        FlushGesture();
        var ids = _selectedIds.ToList();
        var primary = SelectedEditor?.Id;
        _live = _live with { Elements = _live.Elements.Select(map).ToList() };
        _history.Commit(_live);
        RebuildLayers();
        SetSelection(ids, primary);
        MarkDirty();
        RenderNow();
        RaiseState();
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
            int wpx, hpx;
            if (SmoothPreview)
            {
                var gray = _renderer.RenderGray(_live, ctx);
                Preview = PreviewImage.FromGray(gray);
                wpx = gray.WidthPx;
                hpx = gray.HeightPx;
            }
            else
            {
                var mono = _renderer.Render(_live, ctx);
                Preview = PreviewImage.FromMonochrome(mono);
                wpx = mono.WidthPx;
                hpx = mono.HeightPx;
            }
            PreviewWidthPx = wpx;
            PreviewHeightPx = hpx;
            UpdateDisplaySize();
            UpdateSelectionVisuals();
            UpdateSafeArea();
            var mode = SmoothPreview ? "smooth" : "exact";
            StatusText = $"{_live.Canvas.WidthMm:0.#} × {_live.Canvas.HeightMm:0.#} mm · {_live.Canvas.Dpi} dpi · {wpx}×{hpx} px · {mode}";
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
        UpdateSelectionVisuals();
    }

    private void UpdateDisplaySize()
    {
        DisplayWidth = PreviewWidthPx * Zoom;
        DisplayHeight = PreviewHeightPx * Zoom;
        OnPropertyChanged(nameof(GridSpacingDisplay));
        UpdateSafeArea();
    }

    partial void OnShowSafeAreaChanged(bool value) => UpdateSafeArea();

    private void UpdateSafeArea()
    {
        if (ShowSafeArea && _live.Canvas.SafeAreaInsetMm is { } inset && inset > 0)
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

    // Element positioning is whole-mm only (build decision 2026-06-08): snap to the grid when enabled,
    // otherwise round to the nearest mm so drags never leave sub-mm coordinates.
    private double Snap(double mm) => SnapEnabled ? Math.Round(mm / GridMm) * GridMm : Math.Round(mm);

    // ── Snap to the print guide (safe-area edges) ────────────────────────────────────────────────
    // The safe-area inset (e.g. 1.5 mm) isn't a whole-mm value, so plain Snap() can't land an edge on
    // it. When the guide is shown, treat its four edges as magnetic targets so borders line up with it.
    private const double GuideSnapMm = 0.75;

    private IReadOnlyList<double> SafeGuides(double extentMm)
    {
        // Use the whole-mm guide inset so snap targets match the drawn lines (and the integer grid).
        var inset = GuideInsetMm;
        return HasSafeArea && inset > 0 ? [inset, extentMm - inset] : [];
    }

    private static double? NearestGuide(double valueMm, IReadOnlyList<double> guides)
    {
        double? best = null;
        var bestD = GuideSnapMm;
        foreach (var g in guides)
        {
            var d = Math.Abs(valueMm - g);
            if (d <= bestD) { bestD = d; best = g; }
        }
        return best;
    }

    /// <summary>Snap a moved element's position: a guide wins for either the leading or trailing edge,
    /// otherwise fall back to whole-mm.</summary>
    private (double X, double Y) SnapMove(double x, double y, double w, double h)
    {
        var xg = SafeGuides(_live.Canvas.WidthMm);
        var yg = SafeGuides(_live.Canvas.HeightMm);
        var nx = NearestGuide(x, xg) ?? (NearestGuide(x + w, xg) is { } r ? r - w : (double?)null);
        var ny = NearestGuide(y, yg) ?? (NearestGuide(y + h, yg) is { } b ? b - h : (double?)null);
        return (nx ?? Snap(x), ny ?? Snap(y));
    }

    /// <summary>Snap a resized element: the handle's active edges magnetize to the guide; inactive
    /// edges keep whole-mm behaviour.</summary>
    private (double X, double Y, double W, double H) SnapResize(Handle handle, double x, double y, double w, double h)
    {
        var xg = SafeGuides(_live.Canvas.WidthMm);
        var yg = SafeGuides(_live.Canvas.HeightMm);
        bool left = handle is Handle.TopLeft or Handle.Left or Handle.BottomLeft;
        bool right = handle is Handle.TopRight or Handle.Right or Handle.BottomRight;
        bool top = handle is Handle.TopLeft or Handle.Top or Handle.TopRight;
        bool bottom = handle is Handle.BottomLeft or Handle.Bottom or Handle.BottomRight;

        double rx = Snap(x), ry = Snap(y), rw = Snap(w), rh = Snap(h);
        if (left && NearestGuide(x, xg) is { } nl) { rw = x + w - nl; rx = nl; }
        else if (right && NearestGuide(x + w, xg) is { } nr) { rw = nr - x; rx = x; }
        if (top && NearestGuide(y, yg) is { } nt) { rh = y + h - nt; ry = nt; }
        else if (bottom && NearestGuide(y + h, yg) is { } nb) { rh = nb - y; ry = y; }
        return (rx, ry, Math.Max(1, rw), Math.Max(1, rh));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private void RebuildLayers()
    {
        Layers.Clear();
        for (var i = _live.Elements.Count - 1; i >= 0; i--) // frontmost first
            Layers.Add(new LayerItemViewModel(_live.Elements[i], SetElementVisible, SetElementLocked));
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
        OnPropertyChanged(nameof(CanvasWidthMm));
        OnPropertyChanged(nameof(CanvasHeightMm));
        OnPropertyChanged(nameof(SafeAreaInsetMm));
        OnPropertyChanged(nameof(GuideInsetMm));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
