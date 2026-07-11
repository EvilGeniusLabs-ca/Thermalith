using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Thermalith.Core.Fonts;

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

    public ClipPaletteViewModel() : this(920, 520, null, null) { }

    public ClipPaletteViewModel(double width, double height, Action<double, double>? onSizeChanged, Action? onRequestClose)
    {
        _width = Math.Max(MinWidth, width);
        _height = Math.Max(MinHeight, height);
        _onSizeChanged = onSizeChanged;
        _onRequestClose = onRequestClose;
        Columns = ColumnsFor(_width);

        var index = ClipIndex.LoadEmbedded();
        _catalog = ClipFontCatalog.CreateEmbedded();

        var tabs = new List<ClipFontTabViewModel> { ClipFontTabViewModel.SearchTab() };
        tabs.AddRange(index.Fonts.Select(f => new ClipFontTabViewModel(f, index, _catalog, Columns)));
        Tabs = tabs;
    }

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
    private readonly ClipIndex? _index;
    private readonly ClipFontCatalog? _catalog;
    private readonly string? _fontKey;
    private IReadOnlyList<ClipGlyphItemViewModel>? _glyphs;
    private int _columns = 1;

    public string Header { get; }
    public bool IsSearch { get; }

    // Inert in the gauge build — present so the layout can be judged; wired in the search slice.
    [ObservableProperty] private string _filter = "";

    private ClipFontTabViewModel(string header, bool isSearch)
    {
        Header = header;
        IsSearch = isSearch;
    }

    public static ClipFontTabViewModel SearchTab() => new("Search", isSearch: true);

    public ClipFontTabViewModel(ClipFontInfo font, ClipIndex index, ClipFontCatalog catalog, int columns)
    {
        Header = font.Label;
        _index = index;
        _catalog = catalog;
        _fontKey = font.Key;
        _columns = Math.Max(1, columns);
    }

    public void OnColumnsChanged(int columns)
    {
        _columns = Math.Max(1, columns);
        if (_glyphs is not null) OnPropertyChanged(nameof(Rows)); // re-chunk only if this tab was shown
    }

    public IReadOnlyList<ClipGlyphRow> Rows =>
        BuildGlyphs().Chunk(Math.Max(1, _columns)).Select(c => new ClipGlyphRow(c)).ToList();

    private IReadOnlyList<ClipGlyphItemViewModel> BuildGlyphs() =>
        _glyphs ??= _fontKey is null || _index is null || _catalog is null
            ? Array.Empty<ClipGlyphItemViewModel>()
            : _index.ForFont(_fontKey).Select(g => new ClipGlyphItemViewModel(g, _catalog)).ToList();
}

/// <summary>A fixed-width row of glyph cells — the ListBox virtualizes these.</summary>
public sealed class ClipGlyphRow
{
    public IReadOnlyList<ClipGlyphItemViewModel> Cells { get; }
    public ClipGlyphRow(IReadOnlyList<ClipGlyphItemViewModel> cells) => Cells = cells;
}

/// <summary>One glyph cell. Its preview bitmap is rasterized once, on first access (only realized cells in a
/// virtualized grid ask for it).</summary>
public sealed class ClipGlyphItemViewModel
{
    private const int PreviewPx = 48;

    private readonly ClipGlyph _glyph;
    private readonly ClipFontCatalog _catalog;
    private Bitmap? _preview;
    private bool _rendered;

    public ClipGlyphItemViewModel(ClipGlyph glyph, ClipFontCatalog catalog)
    {
        _glyph = glyph;
        _catalog = catalog;
    }

    public string Name => _glyph.Name;

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
