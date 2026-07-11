using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Fonts;

namespace Thermalith.App.ViewModels;

/// <summary>
/// The clip-art palette (GitHub #2). Loads the embedded glyph index + font catalog and exposes a tab per
/// bundled font (plus a Search tab). Gauge build: browse only — search and canvas-insert are not wired yet.
/// </summary>
public sealed class ClipPaletteViewModel : ViewModelBase, IDisposable
{
    private readonly ClipFontCatalog _catalog;

    public IReadOnlyList<ClipFontTabViewModel> Tabs { get; }

    public ClipPaletteViewModel()
    {
        var index = ClipIndex.LoadEmbedded();
        _catalog = ClipFontCatalog.CreateEmbedded();

        var tabs = new List<ClipFontTabViewModel> { ClipFontTabViewModel.SearchTab() };
        tabs.AddRange(index.Fonts.Select(f => new ClipFontTabViewModel(f, index, _catalog)));
        Tabs = tabs;
    }

    public void Dispose() => _catalog.Dispose();
}

/// <summary>One palette tab — a bundled font's glyph grid, or the (placeholder) Search tab. The glyph list
/// is built lazily the first time the tab is shown.</summary>
public sealed partial class ClipFontTabViewModel : ViewModelBase
{
    private readonly ClipIndex? _index;
    private readonly ClipFontCatalog? _catalog;
    private readonly string? _fontKey;
    private IReadOnlyList<ClipGlyphItemViewModel>? _glyphs;

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

    public ClipFontTabViewModel(ClipFontInfo font, ClipIndex index, ClipFontCatalog catalog)
    {
        Header = font.Label;
        _index = index;
        _catalog = catalog;
        _fontKey = font.Key;
    }

    // Fixed column count for the fixed-width flyout. Glyphs are chunked into rows so a stock ListBox
    // (which virtualizes its rows) gives a virtualized grid without ItemsRepeater.
    private const int Columns = 8;
    private IReadOnlyList<ClipGlyphRow>? _rows;

    public IReadOnlyList<ClipGlyphRow> Rows =>
        _rows ??= BuildGlyphs().Chunk(Columns).Select(c => new ClipGlyphRow(c)).ToList();

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
