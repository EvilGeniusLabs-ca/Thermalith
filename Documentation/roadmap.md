# Thermalith — Roadmap / Wishlist

Future / wishlist — things we want to do but aren't active. Move items here when deferred; promote back to worklist.md when they become active.

## E. Deferred / later

16. **Insertable / drag-drop clip-art system — CONFIRMED PRE-LAUNCH goal** (EvilGenius, 2026-06-08; not
    started). NIIMBOT's app ships shipping/package-handling symbols + electronics-label symbols;
    Thermalith wants its own equivalent. IP rule stands: **our own / CC0 / freely-licensed standard
    pictograms only, NEVER NIIMBOT's artwork.**
    - **Content domains:** general symbols, **shipping** (ISO 780 / ASTM D5445 package-handling:
      fragile, this-way-up, keep-dry, … — these are standardized, public-domain forms), and
      **electronics / hazard** (ESD, high-voltage, WEEE crossed-bin, recycling, CE/UKCA — standard
      marks, freely usable as symbols; mind any mark-usage rules).
    - **Source:** the bundled **MDI** set (Apache-2.0, ~7400 vectors, already shipped offline) covers
      general symbols for free; curated CC0/public-domain pictogram sets fill shipping + electronics.
    - **KEY design call:** insert clip-art as **vector** (filled path → solid black), NOT dithered
      raster. Vector prints crisp at any size on 1-bit thermal with no dithering artifacts — ideal.
      Likely a new icon/glyph element type (or extend `ShapeElement`) backed by path data; the raster
      `ImageElement` stays for photos. MDI paths drop straight into this.
    - **Mechanism:** category-browsable palette → drag/drop onto canvas. (Promote out of §E when it
      reaches the active queue — it's pre-launch, not "later".)
    - (Earlier framing was "remote-cache → canvas image layer"; the bundled-vector approach is better
      — offline, crisp, zero IP risk — so prefer it over remote-fetched raster.)
17. NativeMenuBar for a real macOS top-bar (plain `Menu` today; KeymapService ready for the swap).
17a. **Icon-set migration to MDI (usability pass) — PARTIAL.** `Material.Icons.Avalonia` is wired in
    (`<mi:MaterialIcon Kind="…" />`, styles in App.axaml). **Done:** all four image-transform buttons
    (rotate CW/CCW + mirror/flip → `RotateLeft`/`RotateRight`/`FlipHorizontal`/`FlipVertical`).
    **Remaining (still stroked `StreamGeometry`, ~48 uses):** insert palette, type-list glyphs,
    align/distribute, eye/lock, B-I-U. Migrate in one coordinated pass so the UI shares one style (MDI is
    *filled* vs the current *stroked* line-art — do it all at once to avoid a mixed look). On migration
    the glyphs take `Foreground`; the theme `IconStroke` brush already keeps the stroked ones visible in
    light mode, so this is polish, **not** a light-mode blocker. EvilGenius's colored flip/mirror PNGs are
    parked at `Assets/Icons/` (possible clip-art use). Note: `Material.Icons` bundles ~7400 paths
    (~couple MB, untrimmable) — fine for desktop; revisit if size bites.
18. Distribution / packaging (Phase 6) — single-file + `EnableCompressionInSingleFile`, per-RID,
    ReadyToRun choice, `.icns` into the macOS `.app`. No trimming (Avalonia is reflection-heavy).
19. ~~3 skipped Niimbot.Net tests~~ — no longer skipped (suite runs 0-skip). Item closed.
21. **Implement EGL Donation from the NuGet** (Phase 6) — consume the external
    `EvilGeniusLabs.DonationWare` MIT NuGet (its own repo)
    in Help/About: render the provider affordance(s) + inject the launcher. The package's own build-out
    and publish are tracked in that repo, not here. Off the critical path; wire in late.
20. **App UI localization / multi-language (i18n)** — future (EvilGenius, 2026-06-08). Localize Thermalith's
    own UI (menus, dialogs, inspector labels, messages) into multiple languages — relevant to the global
    NIIMBOT audience. This is the *app chrome*, distinct from rendering CJK text *on the label* — and that
    label-text side is already handled by per-glyph OS font fallback (§6.3.4; no bundled CJK font, decided
    2026-06-15). So i18n is purely the chrome-translation work: extracting hardcoded XAML/VM strings into
    resource files + a culture/locale switch (Avalonia resx or a localization lib). Big-ish refactor; do
    once the UI has settled so strings aren't churning.

## J. Data merge / variable data (PLAN — not started, 2026-06-08)

Raised during testing: there is **no end-to-end data-merge UI/flow yet** — needs its own plan + build.
This is the "print many labels from a data source" capability (mail-merge for labels): bind element
fields (text/barcode/QR/serial) to columns from a source, then batch-render/print one label per row.
**Note (spec audit 2026-06-08):** the **Core token/data plumbing already exists** — `TokenResolver`,
`LabelPackage.DataEntry`, bundled `data.json`, and resolver precedence (data → default → sample). So §J
is really the **live provider (CSV/Excel/DB read at print time) + the column-mapping UI + batch print**,
not a from-scratch system. The §6.5 third precedence tier (live source) is the missing piece.

- Sources to support (packages already pinned in Directory.Packages.props under "Planned: data binding"):
  CSV (`CsvHelper`), Excel (`ClosedXML`), and DB (`Dapper` + `Npgsql`/`MySqlConnector`/SqlClient/Sqlite).
- Model: tokens/placeholders in element content (the resolver already substitutes tokens + has
  `RowIndex` — see `ResolveContext`); merge feeds a row's fields as the token data per render.
- UI: pick a source, map columns → fields, preview row N, print all / range.
- Scope it as a dedicated phase; large enough to be its own design doc before code.

NEEDS DISCUSSION before building: data model + UX flow. (Pairs with the deferred drag-handle / min-size
decision.)

## Future phases (expected scaffolds — untracked but not forgotten)

- **§8 — MCP tool surface.** `Thermalith.Server` is a `/`+`/health` scaffold; none of the 9 tools
  (`list_printers`, `printer_status`, `list_labels`, `load_label`, `render_label`, `bind_and_preview`,
  `print_label`, `create_label`, `get_capabilities`) nor the `LabelDocument` schema resource exist; no
  `ModelContextProtocol` package. **Phase 5 — not R1.**
- **§9 — help system.** No PDF manual, no LLM-capability JSON (+ generator), no in-app help/assistant.
  Only `docs/captures/` exists. **Phase 6.**
  *(DonationWare §4.2 build-out is no longer here — extracted to its own repo 2026-06-08; Thermalith just
  consumes the NuGet, §E.21.)*

## Niimbot.Net broad-model support — hardware test matrix

The Niimbot.Net v1 goal is "drive every catalogue printer". **B1 and B4 are hardware-verified** (B4 added
2026-06-15); D11 is the next unit incoming. Profiles are now **catalogue-derived** (worklist §A / §A.8 done),
so any listed model resolves real geometry, dpi, and density from `printers.json`; the open part is
per-engine **print-path** verification on hardware. Broadening coverage means exercising three axes — width,
print-engine, and dpi:

- **Width:** 12 / 48 / 104 mm · **Engine:** D110 + Left feed (D11) vs B1 + Top feed (B1, B4) ·
  **dpi:** 203 (B1, B4) + 229 (D11) — beyond the all-203/8-dots-per-mm baseline.

**Per-unit task:** read each printer's reported **model-id + dpi** and reconcile against the catalogue —
same "confirm against hardware" discipline as the per-SKU roll key. The D11 especially (see below).

- **Narrow end — D11 "upgraded" (~12 mm).** The higher-dpi variant maps to catalogue
  **D11_H / D11_Pro (229 dpi / 108 px, ids 528/531)**. NIIMBOT markets it "300 dpi" but `devices.json`
  says **229** — CONFIRM the real reported dpi against hardware (drives render dot-pitch). Covers the narrow
  form factor *and* the different engine (`PrintTaskVersion.D110` + `PrintDirection.Left`) *and* the
  non-203 dpi path. The catalogue + `KnownPrinterFacts` now resolve D11 to the D110 engine + Left feed
  already (no longer the generic-B1 fallback), but that mapping is best-known and **unverified** — the D11
  is what confirms the D110 print path on real hardware.
- **Middle — B1 (203 dpi / 384 px).** The verified reference unit, mid-width. (B1 Pro at 229 dpi exists,
  skipped — not a hole, the 300-dpi D11 already exercises the non-203 path.)
- **Wide end — B4 (4" / 104 mm) — VERIFIED 2026-06-15.** The driver holds at 104 mm: a full 98×148 mm
  shipping label prints correct and complete (after the tall-label run-length fix). **No app-side
  "shipping" work exists to do:** a 4" label is just a 104 mm canvas the app already accepts (size fields take 3 digits),
  printed one at a time like any other size. The whole requirement is that **Niimbot.Net lets a developer
  drive the printer** (the v1 NuGet goal) — the app comes along for free. The only additive "shipping-ish"
  capability, printing many labels in a run, is **data-merge / variable-data (§J)** — printer-agnostic and
  orthogonal to the B4, not a shipping feature. So the B4 is purely a *library* test target.

Note: 25×78 mm "cable" labels are B-series stock — the B1 prints them. A D-series is
a *different* (narrow wire-marker) cable form factor, not a printer for those labels.

## A. Community-shared label DB (future consideration)

15. (FUTURE CONSIDERATION — Phase 5 / server, not near-term) Community-shared label DB — OPT-IN.
    Users may share a learned roll definition
    `{ barcode, name, paperType, widthMm, heightMm, shape, density }` to a hosted API; the app can
    optionally pull it to pre-fill a newly-detected barcode (user still confirms). Clean — our own
    crowdsourced factual DB, no NIIMBOT IP. Lives in `Thermalith.Server` (currently a stub) as a
    hosted service. Needs: submit + fetch verbs, dedup/consensus by barcode (same SKU converges), moderation,
    an open DB license (CC0/ODbL). Off by default, never a dependency (local store works offline).
    Design the local record shape now so this drops in later without rework (same record).
