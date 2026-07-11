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

## Sources

- Material Design Icons — https://github.com/Templarian/MaterialDesign-Webfont
- Font Awesome Free — https://github.com/FortAwesome/Font-Awesome
- Lucide — https://github.com/lucide-icons/lucide
- Tabler Icons — https://github.com/tabler/tabler-icons
- Phosphor — https://github.com/phosphor-icons/web
- Noto Emoji / Noto Color Emoji — https://github.com/googlefonts/noto-emoji
