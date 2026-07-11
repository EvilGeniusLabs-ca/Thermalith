# Clip-art fonts

Bundled glyph fonts that source the clip-art palette (GitHub issue #2). Each placed clip is a glyph
rasterized to a bitmap via the image pipeline; these fonts are the *palette source*. All licenses below
are GPL-3.0-compatible (Thermalith is GPL-3.0). Per-font license text is in `LICENSES/`.

**Not yet embedded.** These are sourced/staged only — they are deliberately *not* registered as
`<EmbeddedResource>` in `Thermalith.Core.csproj` yet, so they don't bloat the binary before the palette
feature is wired. Embedding + `FontService` loading happens at implementation time.

## Fonts

| Family | File | License | Role | ~Glyphs |
|---|---|---|---|---|
| Material Design Icons | `materialdesignicons-webfont.ttf` | Apache-2.0 (Pictogrammers Free) | General icons | ~7,400 |
| Font Awesome 6 Free | `fa-solid-900.ttf`, `fa-regular-400.ttf` | SIL OFL-1.1 (fonts) | General icons | ~2,000 |
| Lucide | `lucide.ttf` | ISC | General icons | ~1,600 |
| Tabler Icons | `tabler-icons.ttf` | MIT | General icons (broad set) | ~5,900 |
| Phosphor | `Phosphor.ttf` | MIT | General icons | ~1,500 |
| Noto Emoji (monochrome) | `NotoEmoji-VF.ttf` | SIL OFL-1.1 | Emoji — outline, thresholds clean | ~890 |
| Noto Color Emoji | `NotoColorEmoji.ttf` | SIL OFL-1.1 | Emoji — color (CBDT), "try it and see" | ~3,600 |

## Notes

- This is a **test set** — bundle all 7, exercise them through insert → rasterize, keep the ones that
  print well. Color emoji especially is judged by eye.
- **Dropped during sourcing:** Bootstrap Icons (ships woff2 only — not SkiaSharp-loadable without
  conversion; replaced by Tabler); IBM Carbon (SVG-only, no icon font); Remix Icon (custom license v1.0,
  §9 self-declares incompatibility with strong-copyleft licenses — not GPL-compatible; replaced by
  Phosphor); HazChem (license forbids commercial use / bundling / derivatives).
- Font Awesome Free: the **fonts** are OFL-1.1 (attribution appreciated); the SVG/JS icon files are
  CC-BY-4.0, but we bundle only the fonts.

## Search metadata (`metadata/`)

Glyphs inside the TTFs sit at Private Use Area codepoints with, at best, terse `post`-table names and
**no synonyms** — not enough to search on. Each set's companion metadata file (in `metadata/`) is the real
search index: it maps **name → tags / aliases / keywords**. The name→glyph(codepoint) binding comes from
the font's own `cmap` at index-build time; these files add the searchable vocabulary. Emoji are the
exception — they sit at real Unicode codepoints, so their keywords come from Unicode/CLDR data.

At build we compile these into one slim unified index (`{font, name, codepoint, tags}`); the raw files
here are the *source*, not shipped as-is.

| Font | Metadata file | Provides | Entries |
|---|---|---|---|
| Material Design Icons | `mdi.meta.json` | name, codepoint, aliases, tags | 7,447 |
| Font Awesome Free | `fontawesome.icons.json` | name, unicode, search terms, categories | 1,895 |
| Lucide | `lucide.tags.json` | name → tags | 1,746 |
| Tabler | `tabler.icons.json` | name, category, tags, styles | 5,093 |
| Phosphor | `phosphor.tags.json` | name → tags (extracted from core `icons.ts`) | 1,530 |
| Noto Emoji (both) | `emoji.emojibase-en.json` | label + keyword tags (search) | 1,949 |
| Noto Emoji (both) | `emoji.unicode-emoji-json.json` | name + group/subgroup (category browse) | 1,914 |

Metadata files inherit their upstream project licenses (already captured in `LICENSES/`); they are
build-time source, not embedded in the binary.

## Compiled index (`clip-index.json`)

The shipped search index — the unified `{ fonts:[{key,label,count}], glyphs:[{f,c,n,t}] }` view the
palette browses/searches (f=font key, c=codepoint, n=name, t=extra keyword tags). Built by
`tools/build-clip-index/build_clip_index.py` (fontTools), which reads each font's `cmap` for the present
codepoints and joins name+tags from the metadata above. ~21k glyphs, ~2.1 MB minified. This is the ONLY
clip metadata that ships; the raw `metadata/` files are build-time source. Regenerate after changing the
font set: `python tools/build-clip-index/build_clip_index.py`.

## Sources

- Material Design Icons — https://github.com/Templarian/MaterialDesign-Webfont
- Font Awesome Free — https://github.com/FortAwesome/Font-Awesome
- Lucide — https://github.com/lucide-icons/lucide
- Tabler Icons — https://github.com/tabler/tabler-icons
- Phosphor — https://github.com/phosphor-icons/web
- Noto Emoji / Noto Color Emoji — https://github.com/googlefonts/noto-emoji
