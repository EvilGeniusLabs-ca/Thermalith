# Thermalith — Reference Data & Findings

Factual reference for the NIIMBOT protocol and label hardware — read on demand.

## Reference data & findings

### Roll/label sizing — learned, user-curated (decided 2026-06-08)

We do NOT scrape or pre-curate NIIMBOT's SKU catalogue (no public endpoint; the gallery is
auth'd/region cloud + copyrighted images; niimbluelib has no roll data; the niimblue editor only
hardcodes ~4 presets). Instead the label catalogue is **learned from the user's own rolls** — no
network, no IP exposure.

Dropped: scraping NIIMBOT's cloud SKU catalogue / locating that endpoint — not a clean/public source.

### Endpoints

- Printers (USE THIS): `https://oss-print.niimbot.com/public_resources/static_resources/devices.json`
  — public, no auth, ~144 KB, **76 devices**. This is the same source niimbluelib generates from, so
  it's stable. Factual specs only; `thumb` is `null` for every device — no images in this file.
- Also referenced (not used yet): `https://print.niimbot.com/api/hardware/list`.
- The decorative label/template/product catalog (the gallery with named SKUs + artwork) is a
  SEPARATE cloud endpoint (not located). Its images/templates are copyrighted — do NOT scrape/ship.
  If we ever want the discrete stock-size SKU list, take only factual fields (code, W, H, shape,
  compatible models), never images.

### `devices.json` object shape (key fields)

`id`, `seriesId`, `seriesName`, `codes`, `name`, `printDirection`, `defaultWidth`,
`defaultHeigth` (sic — their typo), `maxPrintWidth`, `maxPrintHeight`, `widthSetStart`,
`widthSetEnd`, `solubilitySetStart/End/Default`, `paccuracy`, `paperType`, `rfidType`,
`consumables[]`, `thumb` (always null), `compatibleWithApplications`, `isSupportWifi`,
`isSupportCalibration`.

Derivations (verified against our B1 profile):
- `paccuracy` = pixels per mm. dpi = round(paccuracy × 25.4). 8 → 203 dpi, 11.81 → 300 dpi.
  (B32 reports 9 → ~229 dpi — unusual, verify if we rely on it.)
- **printheadPx = `widthSetEnd` × `paccuracy`** (B1: 48 × 8 = 384 — matches our profile exactly).
- `widthSetEnd` is the **printable** width (mm); `maxPrintWidth`/`defaultWidth` is the **stock**
  width. They differ: B1 stock = 50 mm, printable = 48 mm. This is the crux of the "50 mm canvas
  won't print on B1" issue — content must live within the printable 48 mm (≈ crop 8 px / side of a
  400 px render).
- `solubilitySet*` = density (min/max/default). B1 = 1/5/3 (matches our profile).
- `consumables[].childProperties[].blindZone` = per-edge unprintable margins in mm, pipe-separated
  e.g. `"0.5|0.5|0.0|0.0"` (edge order appears top|bottom|left|right — CONFIRM against a real print
  before trusting). This is the real per-printer/per-material safe-area inset to drive guides + the
  print crop.

### Code mappings (from niimbluelib `tools/gen-printer-models.js`)

- `paperType` CSV codes → type: 1 = WithGaps, 2 = Black, 3 = Continuous, 4 = Perforated,
  5 = Transparent, 6 = PvcTag, 10 = BlackMarkGap, 11 = HeatShrinkTube.
  (Our `Niimbot.Net.Commands.LabelType` currently has WithGaps/Black/Continuous/Transparent/Invalid —
  missing Perforated/PvcTag/BlackMarkGap/HeatShrinkTube; extend when wiring the catalog.)
- `printDirection`: 0 → top, 180 → top, 90 → left, 270 → left.
- `rfidType`: 0 = none, 1/2/3 = RFID variants.

### Worked numbers (sampled)

- B1: id 4096, default 50×30, stock max 50, printable `widthSetEnd` 48, paccuracy 8 → 203 dpi,
  printheadPx 384, density 1–5 (def 3), paperType `1,2,5`, rfidType 1.
  **Dot ratio: ~8 dots/mm** (203 dpi → 203/25.4 = 7.99 dots/mm; 1 dot ≈ 0.125 mm). Printhead 384 dots ÷
  8 ≈ 48 mm printable. So 1 mm of design ≈ 8 printer dots — placement is 8× coarser than the dot grid.
- B21: 50×30, printable 48, density 1–5, paperType `1,2,3,5`.
- D11 / B18: small — default 30×12, printable width 12–15 mm.
- B50: printable 50, density 6–15 (def 10), rfidType 0.
- B32: printable 72 / stock 75, paccuracy 9, density 1–15.
- Widest (4-inch+ class): B4 / B4 Pro maxPrintWidth 108; B2 Pro / EP2M_H / ET10 → 200.

### RFID read — what the tag carries (B1)

What the B1 reports for an RFID-tagged roll: `uuid` (8 bytes, per *physical* roll), `barcode` (a
~9-digit NIIMBOT article/batch code), `serial` (per *physical* roll), paper type, and label counts.

Findings:
- The RFID barcode is **not** the part name and **not** the box id. Store both separately.
- The RFID carries **no dimensions and no part name** — none of uuid/barcode/serial encodes the size
  or SKU. So size + paper type MUST come from the user-entered roll definition; the RFID is an opaque
  match key only. (NIIMBOT shows the size because their cloud resolves the barcode → SKU; we can't.)
- `uuid`/`serial` are per-physical-roll → not viable per-SKU keys. **`barcode` is the only per-SKU key
  candidate.** OPEN: is `barcode` stable per-SKU or per-batch? Needs a 2nd roll of the SAME SKU to
  confirm. Design defensively: key on `barcode`, store `partName`/`boxId` too, parse W×H from
  `partName` (`T40*20` → 40×20) to pre-fill; refine to partName-keyed + observed-barcodes if barcode
  proves per-batch.
- `RfidInfo` gap: our DTO exposes `Barcode`/`ConsumablesType`/serial/counts but NO mm dimensions, so
  RFID alone can't tell us the loaded label's size — "Consumables inside the printer" needs a
  barcode/SKU → size lookup.

### Decisions locked

- Bundle a generated `printers.json` as an **EmbeddedResource** (inside the single-file exe — same
  mechanism as the Roboto font / `label-stock.json`; ~15 KB, negligible vs the runtime). Update is
  user-initiated, writing a cache to app-data that overrides the embedded baseline. (Option A.)
- The importer + fetch live IN the app; the committed baseline is produced by the app's own
  importer via a `--update-catalog` flag. No separate application.
- No image/template scraping — facts only.
- No trimming (Avalonia is reflection-heavy, per global rule + build spec). Size levers at
  packaging: `EnableCompressionInSingleFile`, per-RID, ReadyToRun choice.
