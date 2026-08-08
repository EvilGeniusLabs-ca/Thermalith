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
`widthSetEnd`, `solubilitySetStart/End/Default`, `paccuracy`, `paccuracyName`, `paperType`,
`rfidType`, `consumables[]`, `thumb` (always null), `compatibleWithApplications`, `isSupportWifi`,
`isSupportCalibration`.

Derivations (corrected 2026-08-08 - see the dpi warning below):
- **dpi = `paccuracyName`.** It is a *string* holding the real head resolution, and across all 79
  devices it takes exactly two values: `"203"` (56 models) and `"300"` (23 models). There is no
  third resolution.
- **`paccuracy` is NOT pixels per mm** - it is an internal code, only ever `8` or `9`, paired
  1:1 with `paccuracyName` (8 --> 203, 9 --> 300).
- **Pixels per mm is a lookup off the dpi, not arithmetic:** 203 --> 8, 300 --> **11.81**. Use 11.81
  literally (not 300/25.4 = 11.811); it is the constant NIIMBOT's own data and niimbluelib's
  `tools/gen-printer-models.js` are built on, and it reproduces every published printhead width.
- **printheadPx = ceil(`widthSetEnd` x ppmm)** - B1: 48 x 8 = 384; B21 Pro: ceil(50 x 11.81) = 591.
  `ceil` matters: B32 is 851 not 850, C1 is 178 not 177.

**The 229 dpi trap.** Treating `paccuracy` as px/mm and scaling by 25.4 makes every 300 dpi printer
report a phantom **229 dpi** (9 x 25.4 = 228.6) with a printhead ~76% of its true width. Labels then
print at ~76% of design size. **No NIIMBOT printer is 229 dpi.** This shipped in Thermalith 1.1.0 and
hit 22 of 79 models; it was first seen on the D11_H (2026-06-17, fixed as a one-off) and reported
against the B21 Pro as GitHub #17. Fixed at source in `PrinterCatalogImporter` on 2026-08-08 and
guarded by tests. If a 229 ever reappears in the catalog, the importer has regressed.
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
The **B3S_P reads identically** (confirmed 2026-07-30, starter roll 70×40: barcode `032624001`,
no partName/boxId on the tag — partName presence varies by roll, not by reader).

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

## Niimbot.Net broad-model support — hardware test matrix

The Niimbot.Net v1 goal is "drive every catalogue printer". **Hardware-verified: B1 + B4 (2026-06-15),
D11_H (2026-06-17), B3S_P (2026-07-30).** Profiles are **catalogue-derived**, so any listed model resolves
real geometry, dpi, and density from `printers.json`; the open part is per-engine **print-path** verification
on hardware. Broadening coverage means exercising three axes — width, print-engine, and dpi:

- **Width:** 12 / 48 / 72 / 104 mm · **Engine:** D110MV4 + Left feed (D11_H) vs B1 + Top feed (B1, B3S_P,
  B4) · **dpi:** 203 (B1, B3S_P, B4) + 300 (D11_H) — beyond the all-203/8-dots-per-mm baseline.

**Per-unit task:** read each printer's reported **model-id + dpi** and reconcile against the catalogue —
same "confirm against hardware" discipline as the per-SKU roll key.

- **Narrow end - D11_H (~12 mm) - VERIFIED 2026-06-17.** Catalogue id 528. The old 229 dpi / 108 px spec
  was wrong (229 was back-computed from the bad 108); the real unit is **300 dpi / 142 px** and uses the
  **D110MV4** print task (9-byte PrintStart, 13-byte SetPageSize carrying the copy count) with Left feed.
  Covers the narrow form factor, the side-fed engine, and the non-203 dpi path. The plain D11/D11S (OldD11
  task) and D110 (D110 task) print paths are still inferred, not hardware-checked. This unit turned out to
  be the first sighting of the catalog-wide 229 dpi bug above; the corrected importer now derives its
  300 / 142 from the general rule rather than a hand-patched entry.
- **Middle - B1 (203 dpi / 384 px).** The verified reference unit, mid-width. (The B1 Pro is **300 dpi /
  567 px** - it was listed here as "229 dpi" before the 2026-08-08 fix. Still skipped for hardware
  testing; the D11_H already exercises the 300-dpi path.)
- **3-inch — B3S_P (72 mm printable, 203 dpi / 576 px, id 272) — VERIFIED 2026-07-30.** Current-production
  USB-C revision (native USB-CDC, VID 3513), fw 7.81; reported model-id/dpi/head match the catalogue
  exactly, and the default **B1 print task** prints correctly. Note for #1: the reporter's ~7-year-old
  micro-USB B3S (ids 256/260/262) is silent on both SPP and USB serial — that old-firmware case is NOT
  covered by this unit and still needs his-hardware forensics.
- **Wide end — B4 (4" / 104 mm) — VERIFIED 2026-06-15.** A full 98×148 mm shipping label prints correct and
  complete (after the tall-label run-length fix). A 4" label is just a 104 mm canvas the app already accepts;
  the requirement is that Niimbot.Net lets a developer drive the printer. The only additive "shipping-ish"
  capability — printing many labels in a run — is data-merge / variable-data (GitHub #7), printer-agnostic
  and orthogonal to the B4.

Note: 25×78 mm "cable" labels are B-series stock — the B1 prints them. A D-series is a *different*
(narrow wire-marker) cable form factor, not a printer for those labels.
