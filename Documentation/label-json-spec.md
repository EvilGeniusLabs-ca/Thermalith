# Thermalith Label Definition (`label.json`) — Format Specification

> **Status:** Format spec, v1.0 (schema). Companion to the build spec (`thermalith-build-spec.md`).
> **Scope:** the `label.json` *template* document only. Its container (`.nlbl`), the optional `data.json`, `manifest.json`, and `assets/` are defined in build-spec §6.6.
> **Last updated:** 2026-06-06

## 1. Overview

`label.json` is the **template** for a single label: a canvas plus an ordered list of elements, where any text/value field may contain `{tokens}` filled later from data (build-spec §6.5). It is UI-agnostic — the editor, the headless renderer, and the API/MCP server all read and write the same document.

A template carries **no data values** of its own; it *declares* the tokens it expects (§8). Data arrives separately — supplied by the API/MCP caller, bundled as `data.json`, or pulled from a live source.

The model is a static-output format: it has **no** interactive machinery (no signals, animation, or input handling), and it works in **physical units (mm/pt)**, not screen pixels.

## 2. Conventions

**Units & coordinates (authoritative):**
- Origin is **top-left**; `+x` is right, `+y` is down.
- **Geometry is in millimetres** (`x`, `y`, `w`, `h`, stroke widths, corner radii, barcode module widths, quiet zones). **Typography is in points** (`fontSizePt`). The renderer converts both to pixels via `canvas.dpi` (`pxPerMm = dpi / 25.4`; `px = pt × dpi / 72`). Never mix units within a field.
- Every element is positioned by its **upper-left corner** (`x`, `y`) and sized by **`w` × `h`**.
- `rotation` is in **degrees clockwise**, applied about the element's **centre**.
- **Z-order is array order**: `elements[0]` is backmost, the last entry is frontmost. There is no `z` field.

**JSON:**
- `System.Text.Json`, **camelCase** property names.
- **Forgiving read:** comments (`//`) and trailing commas are tolerated on load.
- **Null-omit on write:** optional fields at their default are omitted.
- Every object carries an implicit **extension bag** (`[JsonExtensionData]`): unknown fields from a newer `schemaVersion` round-trip instead of being dropped.

**Identity:** every element has a **stable `id`** that survives undo/redo, copy/paste, and binding. Ids are unique within a document.

## 3. Root object

```jsonc
{
  "schemaVersion": "1.0",        // string; consumer refuses a newer major it doesn't understand
  "metadata":   { ... },         // §4
  "canvas":     { ... },         // §5
  "defaultStyle": { ... },       // §6  (optional — style cascade)
  "tokens":     [ ... ],         // §8  (optional — declared data contract)
  "dataSource": { ... },         // §9  (optional — live source binding)
  "bindings":   { ... },         // §10 (optional — token→column remap)
  "elements":   [ ... ]          // §11 (required — ordered, back→front)
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `schemaVersion` | string | yes | Semantic; consumers gate on the major. |
| `metadata` | object | yes | §4 |
| `canvas` | object | yes | §5 |
| `defaultStyle` | object | no | §6 — inherited typography/visual defaults. |
| `tokens` | array | no | §8 — the data contract `load_label` returns. |
| `dataSource` | object | no | §9 — connection details only, never credentials. |
| `bindings` | object | no | §10 — `{ "token": "column" }`. |
| `elements` | array | yes | §11 — may be empty. |

## 4. `metadata`

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | string | yes | Human label name. |
| `createdUtc` | string (ISO-8601) | yes | Creation timestamp, UTC. |
| `modifiedUtc` | string (ISO-8601) | yes | Last-modified, UTC. |
| `appVersion` | string | no | Authoring app version. |

## 5. `canvas`

| Field | Type | Default | Notes |
|---|---|---|---|
| `widthMm` | number | — (req) | Physical width. Auto-seeded from a loaded RFID roll where available (build-spec §5). |
| `heightMm` | number | — (req) | Physical height. |
| `dpi` | integer | from profile (203) | Model-dependent (203 ≈ 8 px/mm; 300-DPI models ≈ 11.8). Renderer reads this — never assume 8. |
| `shape` | enum | `rectangle` | `rectangle` \| `rounded` \| `circle` \| `dieCut`. |
| `cornerRadiusMm` | number | 0 | Used when `shape = rounded`. |
| `bleedMm` | number | 0 | Optional bleed margin for die-cut/cutter stock; validator warns on elements outside it. |
| `orientationDeg` | enum int | 0 | Whole-label rotation relative to print-head feed: `0` \| `90` \| `180` \| `270`. Applied at render before monochrome. |
| `tail` | object | `{position:"none"}` | Cable/double labels: `{ "position": "none"\|"left"\|"right", "lengthMm": number }`. |
| `background` | string | `white` | Thermal stock is white; reserved for future. |

## 6. `defaultStyle` (style cascade — optional)

A label-level set of visual defaults that elements inherit when their own `props`/`style` omit a value. Resolution order (last wins): `defaultStyle` → element `style` → element `props`. Cuts repetition on multi-text labels. All fields optional.

| Field | Type | Notes |
|---|---|---|
| `fontFamily` | string | Default text family (falls back per build-spec §6.3.4). |
| `fontSizePt` | number | Default point size. |
| `bold` / `italic` / `underline` | bool | Default text styling. |
| `lineSpacing` | number | Multiplier (1.0 = single). |
| `strokeWidthMm` | number | Default shape/table stroke. |
| `fill` | enum | `none` \| `solid` — default shape fill. |

> An element may carry an optional `style` object with the same fields to override `defaultStyle` for that element only.

## 7. (reserved)

## 8. `tokens` — the declared data contract

The list of tokens the template expects. The editor auto-populates it by scanning `{tokens}` in element content; the author may annotate. This is precisely what MCP `load_label` returns, so a caller knows what data to supply without parsing the layout.

```jsonc
"tokens": [
  { "name": "name",  "type": "string", "description": "Product name", "sample": "Widget Pro", "required": true },
  { "name": "sku",   "type": "string", "sample": "WP-001", "required": true },
  { "name": "price", "type": "string", "sample": "$9.99" },
  { "name": "url",   "type": "string", "sample": "https://example.com/p/wp-001" }
]
```

| Field | Type | Default | Notes |
|---|---|---|---|
| `name` | string | — (req) | The `{name}` referenced in content. Unique. |
| `type` | enum | `string` | `string` \| `number` \| `date` \| `bool` — hint for editors/validators. |
| `description` | string | — | Human description (shown to API/LLM callers). |
| `sample` | string | — | Preview value when no data is bound. |
| `default` | string | — | Value used if data omits the token. |
| `required` | bool | `false` | Validator errors if a required token is unresolved at print. |

## 9. `dataSource` (optional — live source binding)

Connection details only. **Credentials are never stored here** (OS secret store, build-spec §7).

```jsonc
"dataSource": {
  "kind": "csv",              // none | database | csv | xlsx | json
  "credentialRef": null,      // opaque key into the OS secret store, or null
  // database:
  "provider": null,           // postgres | mysql | sqlserver | sqlite | oracle
  "connectionString": null,   // no password; placeholder only
  "query": null,              // SQL
  // file:
  "path": "stock.csv",        // relative (in package) or absolute
  "hasHeaderRow": true,
  "delimiter": ",",           // csv
  "sheet": null, "range": null // xlsx
}
```

## 10. `bindings` (optional)

Explicit token→column remap; tokens auto-map by matching name when omitted.

```jsonc
"bindings": { "sku": "product_sku", "name": "title" }
```

## 11. Elements

### 11.1 Element base (every element)

| Field | Type | Default | Notes |
|---|---|---|---|
| `id` | string | — (req) | Stable, unique. |
| `type` | enum | — (req) | `text` \| `barcode` \| `qr` \| `serial` \| `datetime` \| `shape` \| `line` \| `image` \| `table`. |
| `name` | string | `type` | Label shown in the layers list. |
| `x`, `y` | number (mm) | — (req) | **Upper-left** corner. |
| `w`, `h` | number (mm) | — (req) | Width, height. |
| `rotation` | number (deg) | 0 | Clockwise about centre. |
| `locked` | bool | false | Editor: not selectable/movable. |
| `visible` | bool | true | Hidden elements are skipped at render. |
| `justify` | object | `{h:"left",v:"top"}` | Content alignment **within** the element. `h`: `left`\|`center`\|`right`\|`justify`; `v`: `top`\|`middle`\|`bottom`. |
| `style` | object | — | Optional per-element style override (§6). |
| `props` | object | — (req) | Type-specific, §11.2–11.9. |
| `properties` | object | — | Freeform extension bag (round-trips). |

### 11.2 `text` (`props`)

Static captions are `text` with no `{token}` in `content` — there is no separate Label control.

| Field | Type | Default | Notes |
|---|---|---|---|
| `content` | string | "" | Token-aware (`{name}`). Supports newlines. |
| `fontFamily` | string | inherit | Any installed TTF; falls back per §6.3.4. |
| `fontSizePt` | number | inherit | Point size. Used as-is when `fontSizing`=`fixed`; the starting hint for `shrink`; ignored for `fill`. |
| `bold` / `italic` / `underline` | bool | false | |
| `lineSpacing` | number | 1.0 | Multiplier. |
| `letterSpacing` | number (pt) | 0 | Extra tracking. |
| `wrap` | enum | `word` | `none` \| `word`. |
| `fontSizing` | enum | `fixed` | `fixed` (use `fontSizePt`) · `shrink` (start at `fontSizePt`, reduce to fit `w`×`h` — never grows) · `fill` (font scales with the box: largest size that fits `w`×`h`). |
| `minFontSizePt` | number | — | Floor for `shrink`/`fill`; below it the renderer stops and the validator flags overflow. |
| `maxFontSizePt` | number | — | Cap for `fill`, so an oversized box doesn't produce absurd type. |

### 11.3 `barcode` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `symbology` | enum | `code128` | `code128`\|`code39`\|`ean13`\|`ean8`\|`upca`\|`upce`\|`itf`\|`codabar` (via ZXing.Net). |
| `value` | string | "" | Token-aware. Must be valid for the symbology. |
| `showText` | bool | true | Render the human-readable value. |
| `textPosition` | enum | `below` | `above`\|`below`\|`none`. |
| `moduleWidthMm` | number | 0.33 | Narrowest bar. Snapped to ≥1 device px at render (§6.3.3); validator warns if <1 px. |
| `quietZoneMm` | number | 2.0 | Left/right margin. |

### 11.4 `qr` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `value` | string | "" | Token-aware. |
| `encoding` | enum | `text` | `text` \| `hex` (binary payload as hex). |
| `ecLevel` | enum | `M` | `L`\|`M`\|`Q`\|`H`. |
| `moduleSizeMm` | number \| `"auto"` | `auto` | `auto` fits the code to `w`×`h`; explicit value snaps to whole px. |
| `quietZoneMm` | number | 1.0 | ~4 modules recommended. |

### 11.5 `serial` (`props`)

Runtime counter is **not** stored in the template; it advances per row at batch print.

| Field | Type | Default | Notes |
|---|---|---|---|
| `start` | integer | 1 | First value. |
| `step` | integer | 1 | Increment per label. |
| `padLength` | integer | 0 | Zero/`padChar`-pad to width. |
| `padChar` | string | "0" | Single char. |
| `prefix` | string | "" | Prepended literal. |
| `suffix` | string | "" | Appended literal. |

### 11.6 `datetime` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `kind` | enum | `date` | `date` \| `time` \| `datetime`. |
| `format` | string | `yyyy-MM-dd` | .NET format string. |
| `source` | enum | `printNow` | `printNow` (resolved at print) \| `fixed`. |
| `fixedValueUtc` | string (ISO-8601) | — | Required when `source = fixed`. |

### 11.7 `shape` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `shapeType` | enum | `rect` | `rect`\|`roundedRect`\|`ellipse`. (`line` is its own `line` element — §11.7b.) |
| `strokeWidthMm` | number | 0.3 | 0 = no stroke. |
| `fill` | enum | `none` | `none` \| `solid`. |
| `cornerRadiusMm` | number | 0 | `roundedRect` only. |

### 11.7b `line` (`props`)

A straight segment between two endpoints — independent of other controls (divider / underline / accent).
The endpoints are the authored source of truth, stored **relative to the element origin** (`x`,`y`); the
base `x`/`y`/`w`/`h` is the derived bounding box (kept in sync, used for selection/align/marquee/group).
Because `w`/`h` are derived (never authored), an axis-aligned line is just `y1==y2` (or `x1==x2`) and never
hits the `w`/`h` min-size clamp.

| Field | Type | Default | Notes |
|---|---|---|---|
| `x1Mm` / `y1Mm` | number | 0 | Endpoint 1, relative to `(x, y)`. |
| `x2Mm` / `y2Mm` | number | 0 | Endpoint 2, relative to `(x, y)`. |
| `weightMm` | number | 0.3 | Stroke weight. |

> **Back-compat:** a legacy `shape` with `shapeType = "line"` (a box-diagonal line) is migrated to a `line`
> element on load (P1 = top-left, P2 = bottom-right). The renderer still draws legacy `shape`/`line` so
> un-migrated files render headless-side.

### 11.8 `image` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `assetId` | string | — (req) | References `assets/…` in the package. |
| `fit` | enum | `fit` | `fill`\|`fit`\|`stretch`\|`center`. |
| `dither` | enum | `floydSteinberg` | `threshold`\|`floydSteinberg`\|`atkinson`\|`ordered`\|`none`. Images are dithered; text/vector are not (§6.3.2). |
| `threshold` | integer (0–255) | 128 | Used when `dither = threshold`. |
| `invert` | bool | false | Invert black/white. |

### 11.9 `table` (`props`)

| Field | Type | Default | Notes |
|---|---|---|---|
| `cols` | integer | — (req) | Column count. |
| `rows` | integer | — (req) | Row count. |
| `columnWidthsMm` | number[] \| `"auto"` | `auto` | Length = `cols`. |
| `rowHeightsMm` | number[] \| `"auto"` | `auto` | Length = `rows`. |
| `cells` | object[][] | — | `[row][col]` of `{ "content": string (token-aware), "justify"?: {h,v} }`. |
| `borderWidthMm` | number | 0.2 | 0 = no borders. |
| `headerRow` | bool | false | Row 0 rendered bold. |

## 12. Complete example — 50 × 30 mm, every control type

A deliberately dense **kitchen-sink** label that exercises all eight element types. It is a schema demonstration, not a tidy production layout; some elements sit close together.

```jsonc
{
  "schemaVersion": "1.0",
  "metadata": {
    "name": "kitchen-sink-50x30",
    "createdUtc": "2026-06-06T12:00:00Z",
    "modifiedUtc": "2026-06-06T12:00:00Z",
    "appVersion": "0.1.0"
  },
  "canvas": {
    "widthMm": 50, "heightMm": 30,
    "dpi": 203,
    "shape": "rectangle",
    "orientationDeg": 0,
    "background": "white"
  },
  "defaultStyle": {
    "fontFamily": "Inter",
    "fontSizePt": 8,
    "lineSpacing": 1.0,
    "strokeWidthMm": 0.3
  },
  "tokens": [
    { "name": "name",  "type": "string", "description": "Product name", "sample": "Widget Pro", "required": true },
    { "name": "sku",   "type": "string", "sample": "WP-001", "required": true },
    { "name": "price", "type": "string", "sample": "$9.99" },
    { "name": "url",   "type": "string", "sample": "https://ex.com/p/wp-001" },
    { "name": "lot",   "type": "string", "sample": "L240606" }
  ],
  "dataSource": {
    "kind": "csv",
    "path": "data.json",
    "hasHeaderRow": true
  },
  "bindings": { "sku": "product_sku" },

  "elements": [

    { "id": "el_title", "type": "text", "name": "Product name",
      "x": 2, "y": 1.5, "w": 30, "h": 6, "rotation": 0,
      "justify": { "h": "left", "v": "middle" },
      "props": { "content": "{name}", "fontSizePt": 11, "bold": true, "wrap": "word",
                 "fontSizing": "fill", "minFontSizePt": 6, "maxFontSizePt": 14 } },

    { "id": "el_logo", "type": "image", "name": "Logo",
      "x": 41, "y": 1.5, "w": 7, "h": 7,
      "props": { "assetId": "image_0001", "fit": "fit", "dither": "atkinson", "invert": false } },

    { "id": "el_rule", "type": "line", "name": "Divider",
      "x": 2, "y": 9, "w": 46, "h": 0,
      "props": { "x1Mm": 0, "y1Mm": 0, "x2Mm": 46, "y2Mm": 0, "weightMm": 0.3 } },

    { "id": "el_barcode", "type": "barcode", "name": "SKU barcode",
      "x": 2, "y": 10.5, "w": 28, "h": 9,
      "justify": { "h": "center", "v": "top" },
      "props": { "symbology": "code128", "value": "{sku}", "showText": true,
                 "textPosition": "below", "moduleWidthMm": 0.33, "quietZoneMm": 2 } },

    { "id": "el_qr", "type": "qr", "name": "URL QR",
      "x": 33, "y": 10.5, "w": 14, "h": 14,
      "props": { "value": "{url}", "encoding": "text", "ecLevel": "M",
                 "moduleSizeMm": "auto", "quietZoneMm": 1 } },

    { "id": "el_price", "type": "text", "name": "Price",
      "x": 2, "y": 20, "w": 16, "h": 5,
      "justify": { "h": "left", "v": "middle" },
      "props": { "content": "{price}", "fontSizePt": 12, "bold": true } },

    { "id": "el_serial", "type": "serial", "name": "Serial",
      "x": 2, "y": 25.5, "w": 14, "h": 3.5,
      "justify": { "h": "left", "v": "middle" },
      "props": { "start": 1, "step": 1, "padLength": 5, "padChar": "0", "prefix": "SN", "suffix": "" } },

    { "id": "el_date", "type": "datetime", "name": "Print date",
      "x": 17, "y": 25.5, "w": 14, "h": 3.5,
      "justify": { "h": "center", "v": "middle" },
      "props": { "kind": "date", "format": "yyyy-MM-dd", "source": "printNow" } },

    { "id": "el_time", "type": "datetime", "name": "Print time",
      "x": 17, "y": 20, "w": 14, "h": 4,
      "justify": { "h": "center", "v": "middle" },
      "props": { "kind": "time", "format": "HH:mm", "source": "printNow" } },

    { "id": "el_lotbox", "type": "shape", "name": "Lot box",
      "x": 33, "y": 25, "w": 15, "h": 4,
      "props": { "shapeType": "roundedRect", "strokeWidthMm": 0.25, "fill": "none", "cornerRadiusMm": 0.8 } },

    { "id": "el_spec", "type": "table", "name": "Spec table",
      "x": 33, "y": 20, "w": 15, "h": 4,
      "props": {
        "cols": 2, "rows": 2,
        "columnWidthsMm": [7, 8],
        "rowHeightsMm": "auto",
        "borderWidthMm": 0.2,
        "headerRow": true,
        "cells": [
          [ { "content": "Lot" },   { "content": "Qty" } ],
          [ { "content": "{lot}" }, { "content": "1", "justify": { "h": "right", "v": "middle" } } ]
        ]
      } }

  ]
}
```

### Element inventory in the example

| `id` | type | x,y (mm) | w×h (mm) | exercises |
|---|---|---|---|---|
| `el_title` | text | 2, 1.5 | 30×6 | token, bold, auto-size (`fill`) |
| `el_logo` | image | 41, 1.5 | 7×7 | asset, Atkinson dither |
| `el_rule` | line | 2, 9 | 46×0 | horizontal rule |
| `el_barcode` | barcode | 2, 10.5 | 28×9 | Code128, human-readable |
| `el_qr` | qr | 33, 10.5 | 14×14 | auto module sizing |
| `el_price` | text | 2, 20 | 16×5 | static-ish token |
| `el_serial` | serial | 2, 25.5 | 14×3.5 | prefix + zero-pad |
| `el_date` | datetime | 17, 25.5 | 14×3.5 | date, printNow |
| `el_time` | datetime | 17, 20 | 14×4 | time, printNow |
| `el_lotbox` | shape | 33, 25 | 15×4 | rounded rect outline |
| `el_spec` | table | 33, 20 | 15×4 | 2×2, header row, per-cell justify |

## 13. Validation

A `label.json` is checked by `ILabelValidator` (build-spec §6.7), which returns coded diagnostics (`Code`, `Severity`, `Message`, `JsonPath`). Key rules: unique `id`s; every `{token}` either declared in `tokens` or resolvable from data; barcode/QR `moduleWidthMm`/`moduleSizeMm` ≥ 1 device px at `canvas.dpi`; elements within `canvas` (± `bleedMm`); `assetId` references exist; required tokens resolvable at print. Errors block printing; warnings do not.

## 14. Versioning & forward-compatibility

`schemaVersion` is semantic. A consumer opens any document with the same major; unknown fields from a newer minor are preserved via the extension bag (§2) rather than discarded, so round-tripping through an older app is non-destructive. A newer **major** than the consumer understands is refused.
