using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Thermalith.Core.Fonts;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;

namespace Thermalith.App.ViewModels;

/// <summary>
/// The clip-art palette (GitHub #2). Loads the embedded glyph index + font catalog and exposes a tab per
/// bundled font (plus a Search tab). The grid's column count is derived from the (user-resizable) width so
/// glyphs fill the panel. Gauge build: browse only — search and canvas-insert are not wired yet.
/// </summary>
public sealed partial class ClipPaletteViewModel : ViewModelBase, IDisposable
{
    /// <summary>Smallest usable palette size (drag handle can't shrink below this).</summary>
    public const double MinWidth = 420;
    public const double MinHeight = 300;

    // Cell footprint (border 50 + 1px margin/side + 2px row spacing) and the panel's fixed chrome width,
    // used to derive how many columns fit the current width.
    private const double CellSize = 54;
    private const double ChromeWidth = 30;

    private readonly ClipFontCatalog _catalog;
    private readonly Action<double, double>? _onSizeChanged;
    private readonly Action? _onRequestClose;

    public IReadOnlyList<ClipFontTabViewModel> Tabs { get; }

    // Flyout size — seeded from settings, updated live by the drag handle, persisted on drag end.
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;

    /// <summary>Columns that fit the current width (drives the responsive grid).</summary>
    public int Columns { get; private set; }

    public ClipPaletteViewModel() : this(920, 520, null, null, null) { }

    public ClipPaletteViewModel(double width, double height, Action<double, double>? onSizeChanged,
        Action? onRequestClose, Action<ClipGlyph>? onInsert)
    {
        _width = Math.Max(MinWidth, width);
        _height = Math.Max(MinHeight, height);
        _onSizeChanged = onSizeChanged;
        _onRequestClose = onRequestClose;
        Columns = ColumnsFor(_width);

        var index = ClipIndex.LoadEmbedded();
        _catalog = ClipFontCatalog.CreateEmbedded();

        var tabs = new List<ClipFontTabViewModel> { ClipFontTabViewModel.SearchTab(index, _catalog, Columns, onInsert) };
        tabs.AddRange(index.Fonts.Select(f => new ClipFontTabViewModel(f, index, _catalog, Columns, onInsert)));
        Tabs = tabs;
    }

    /// <summary>Bake a clicked glyph, centered at half the canvas's shortest side. Returns the element + the
    /// <c>(assetId, png)</c> to add to the document, or <c>null</c> if it can't be rendered.</summary>
    public (ImageElement Element, string AssetId, byte[] Png)? BakeCentered(ClipGlyph glyph, double canvasWidthMm, double canvasHeightMm, int dpi) =>
        ClipBaker.BakeCentered(_catalog, glyph.Font, glyph.Codepoint,
            "clip_" + Guid.NewGuid().ToString("N")[..8], canvasWidthMm, canvasHeightMm, dpi);

    private static int ColumnsFor(double width) => Math.Max(1, (int)((width - ChromeWidth) / CellSize));

    partial void OnWidthChanged(double value)
    {
        var cols = ColumnsFor(value);
        if (cols == Columns) return;   // only when the column count actually crosses a boundary
        Columns = cols;
        foreach (var tab in Tabs) tab.OnColumnsChanged(cols);
    }

    /// <summary>Persist the current size (called when the user finishes dragging the resize handle).</summary>
    public void SaveSize() => _onSizeChanged?.Invoke(Width, Height);

    [RelayCommand]
    private void Close() => _onRequestClose?.Invoke();

    public void Dispose() => _catalog.Dispose();
}

/// <summary>One palette tab — a bundled font's glyph grid, or the (placeholder) Search tab. Glyphs are built
/// lazily on first show; rows re-chunk to the current column count when the panel is resized.</summary>
public sealed partial class ClipFontTabViewModel : ViewModelBase
{
    private const int SearchLimit = 600;

    private readonly ClipIndex _index;
    private readonly ClipFontCatalog _catalog;
    private readonly string? _fontKey;   // null on the Search tab (searches across all fonts)
    private readonly Action<ClipGlyph>? _onInsert;
    private readonly Dictionary<(string Font, int Cp), ClipGlyphItemViewModel> _itemCache = new();
    private int _columns = 1;

    public string Header { get; }
    public bool IsSearch { get; }
    public string Watermark { get; }

    // Live filter — per-font tabs scope to their font, the Search tab spans all fonts.
    [ObservableProperty] private string _filter = "";

    private ClipFontTabViewModel(string header, bool isSearch, string? fontKey, ClipIndex index,
        ClipFontCatalog catalog, int columns, Action<ClipGlyph>? onInsert)
    {
        Header = header;
        IsSearch = isSearch;
        _fontKey = fontKey;
        _index = index;
        _catalog = catalog;
        _onInsert = onInsert;
        _columns = Math.Max(1, columns);
        Watermark = isSearch ? "Search all fonts…" : $"Search {header}…";
    }

    public static ClipFontTabViewModel SearchTab(ClipIndex index, ClipFontCatalog catalog, int columns, Action<ClipGlyph>? onInsert) =>
        new("Search", isSearch: true, fontKey: null, index, catalog, columns, onInsert);

    public ClipFontTabViewModel(ClipFontInfo font, ClipIndex index, ClipFontCatalog catalog, int columns, Action<ClipGlyph>? onInsert)
        : this(font.Label, isSearch: false, font.Key, index, catalog, columns, onInsert) { }

    /// <summary>Shown when the Search tab has no query yet — a hint to start typing.</summary>
    public bool ShowEmptyHint => IsSearch && string.IsNullOrWhiteSpace(Filter);

    public void OnColumnsChanged(int columns)
    {
        _columns = Math.Max(1, columns);
        OnPropertyChanged(nameof(Rows));
    }

    partial void OnFilterChanged(string value)
    {
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(ShowEmptyHint));
    }

    public IReadOnlyList<ClipGlyphRow> Rows =>
        CurrentGlyphs().Select(Item).Chunk(Math.Max(1, _columns)).Select(c => new ClipGlyphRow(c)).ToList();

    // Empty query: browse the font (Search tab shows nothing until you type). Non-empty: scored search,
    // scoped to this font or across all.
    private IReadOnlyList<ClipGlyph> CurrentGlyphs()
    {
        if (string.IsNullOrWhiteSpace(Filter))
            return _fontKey is null ? Array.Empty<ClipGlyph>() : _index.ForFont(_fontKey);
        return _index.Search(Filter, _fontKey, SearchLimit);
    }

    private ClipGlyphItemViewModel Item(ClipGlyph g)
    {
        var key = (g.Font, g.Codepoint);
        if (!_itemCache.TryGetValue(key, out var vm))
            _itemCache[key] = vm = new ClipGlyphItemViewModel(g, _catalog, _onInsert);
        return vm;
    }
}

/// <summary>A fixed-width row of glyph cells — the ListBox virtualizes these.</summary>
public sealed class ClipGlyphRow
{
    public IReadOnlyList<ClipGlyphItemViewModel> Cells { get; }
    public ClipGlyphRow(IReadOnlyList<ClipGlyphItemViewModel> cells) => Cells = cells;
}

/// <summary>One glyph cell. Its preview bitmap is rasterized once, on first access (only realized cells in a
/// virtualized grid ask for it).</summary>
public sealed partial class ClipGlyphItemViewModel
{
    private const int PreviewPx = 48;

    private readonly ClipGlyph _glyph;
    private readonly ClipFontCatalog _catalog;
    private readonly Action<ClipGlyph>? _onInsert;
    private Bitmap? _preview;
    private bool _rendered;

    public ClipGlyphItemViewModel(ClipGlyph glyph, ClipFontCatalog catalog, Action<ClipGlyph>? onInsert)
    {
        _glyph = glyph;
        _catalog = catalog;
        _onInsert = onInsert;
    }

    public string Name => _glyph.Name;

    [RelayCommand]
    private void Insert() => _onInsert?.Invoke(_glyph);

    public Bitmap? Preview
    {
        get
        {
            if (_rendered) return _preview;
            _rendered = true;
            var png = ClipPreview.RenderPng(_catalog, _glyph.Font, _glyph.Codepoint, PreviewPx);
            if (png is not null)
            {
                using var ms = new MemoryStream(png);
                _preview = new Bitmap(ms);
            }
            return _preview;
        }
    }
}
