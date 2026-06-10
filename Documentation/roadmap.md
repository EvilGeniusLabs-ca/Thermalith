# Thermalith — Roadmap / Wishlist

Future / wishlist — things we want to do but aren't active. Move items here when deferred; promote back to worklist.md when they become active.

## E. Deferred / later

16. **Insertable / drag-drop clip-art system — CONFIRMED PRE-LAUNCH goal** (Richard, 2026-06-08; not
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
17a. **Icon-set migration to MDI (usability pass).** `Material.Icons.Avalonia` is now wired in
    (`<mi:MaterialIcon Kind="…" />`, styles registered in App.axaml); the rotate buttons already use
    it. Migrate the remaining hand-drawn `StreamGeometry` icons (insert palette, align/distribute,
    eye/lock, B-I-U, mirror/flip, conn-dot, etc.) to MDI `Kind`s in one coordinated pass so the whole
    UI shares one style (MDI is *filled* glyphs vs the current *stroked* line-art — do it all at once,
    not piecemeal, to avoid a mixed look). Flip/mirror → `FlipHorizontal`/`FlipVertical`. Richard may
    also source a custom PNG for flip/mirror. Note: the `Material.Icons` assembly bundles all ~7400
    icon paths (~couple MB, untrimmable) — acceptable for desktop, but revisit if size bites.
18. Distribution / packaging (Phase 6) — single-file + `EnableCompressionInSingleFile`, per-RID,
    ReadyToRun choice, `.icns` into the macOS `.app`. No trimming (Avalonia is reflection-heavy).
19. 3 skipped Niimbot.Net tests — flip via a real print.txt capture (optional).
21. **Implement EGL Donation from the NuGet** (Phase 6) — consume the external
    `EvilGeniusLabs.DonationWare` MIT NuGet (its own repo at `d:\Projects\EvilGeniusLabs.DonationWare`)
    in Help/About: render the provider affordance(s) + inject the launcher. The package's own build-out
    and publish are tracked in that repo, not here. Off the critical path; wire in late.
20. **App UI localization / multi-language (i18n)** — future (Richard, 2026-06-08). Localize Thermalith's
    own UI (menus, dialogs, inspector labels, messages) into multiple languages — relevant to the global
    NIIMBOT audience. Distinct layer from the §6.3.4 **CJK font** gap (that's rendering CJK text *on the
    label*; this is translating the *app chrome*). Means extracting hardcoded XAML/VM strings into resource
    files + a culture/locale switch (Avalonia resx or a localization lib). Big-ish refactor; do once the
    UI has settled so strings aren't churning. Pairs with the CJK font for a real international release.

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

NEEDS DISCUSSION before building (with §I drag-handle item): data model + UX flow.

## Future phases (expected scaffolds — untracked but not forgotten)

- **§8 — MCP tool surface.** `Thermalith.Server` is a `/`+`/health` scaffold; none of the 9 tools
  (`list_printers`, `printer_status`, `list_labels`, `load_label`, `render_label`, `bind_and_preview`,
  `print_label`, `create_label`, `get_capabilities`) nor the `LabelDocument` schema resource exist; no
  `ModelContextProtocol` package. **Phase 5 — not R1.**
- **§9 — help system.** No PDF manual, no LLM-capability JSON (+ generator), no in-app help/assistant.
  Only `docs/captures/` exists. **Phase 6.**
  *(DonationWare §4.2 build-out is no longer here — extracted to its own repo 2026-06-08; Thermalith just
  consumes the NuGet, §E.21.)*

## Niimbot.Net broad-model support — hardware test matrix (when v1 driver work starts)

The Niimbot.Net v1 goal is "drive every catalogue printer" (release-plan.md §23; only B1 is
hardware-verified today). When that work is scheduled, a 3-point **form-factor + print-path** matrix
gives near-full coverage with no redundancy — buy hardware *then*, not ahead of the code (idle units):

- **Narrow end — D-series (D11/D110, ~12 mm).** Highest value, ~$15–30. Covers the narrow form factor
  *and* the different print engine (`PrintTaskVersion.D110` + `PrintDirection.Left` feed) — the path the
  current generic-B1 fallback definitely gets wrong. Low-regret; fine to buy early.
- **Middle — B1 (48 mm).** Already owned + the verified reference. 48 sits ~dead-centre of 12–104, so
  it's the anchor, not a unit to replace.
- **Wide end — B4 (4" / 104 mm shipping).** The expensive corner *and* a market bet (shipping labels
  may not be the r/niimbot crowd). **Hold** — let launch demand decide, not the wallet.

Note: the 25×78 mm "cable" labels already on order are B-series stock — the B1 prints them. A D-series is
a *different* (narrow wire-marker) cable form factor, not a printer for those labels.

## A. Community-shared label DB (future consideration)

15. (FUTURE CONSIDERATION — Phase 5 / server, not near-term) Community-shared label DB — OPT-IN.
    Users may share a learned roll definition
    `{ barcode, name, paperType, widthMm, heightMm, shape, density }` to a hosted API; the app can
    optionally pull it to pre-fill a newly-detected barcode (user still confirms). Clean — our own
    crowdsourced factual DB, no NIIMBOT IP. Lives in `Thermalith.Server` (currently a stub) on EGL
    infra. Needs: submit + fetch verbs, dedup/consensus by barcode (same SKU converges), moderation,
    an open DB license (CC0/ODbL). Off by default, never a dependency (local store works offline).
    Design the local record shape now so this drops in later without rework (same record).
