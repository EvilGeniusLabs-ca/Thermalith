# Thermalith — Open-Source NIIMBOT System (Build Spec)

> **Status:** Planning document. Hand to Claude Code to scaffold and flesh out.
> **Name:** **Thermalith** — *"Open-Source NIIMBOT System"*. Brand-neutral/trademark-safe product name; "NIIMBOT" used nominatively in the tagline/description for discoverability. The protocol lib stays `Niimbot.Net` (also nominative).
> **Last updated:** 2026-06-06

---

## 1. Purpose

A cross-platform (Windows / macOS / Linux) desktop label design-and-print application for NIIMBOT thermal label printers, built to replace the vendor's cloud-nagging desktop app with a local-first, FOSS-friendly alternative. The solution is factored so the printer protocol is an independent, reusable NuGet package, the label engine is UI-agnostic, and the system is accessible programmatically through an API / MCP server.

Three primary consumers of one shared engine:
- **Desktop editor** (Avalonia) — interactive label design and printing.
- **API / MCP server** — headless access for automation and LLM-driven workflows.
- **The NuGet package itself** — usable standalone by any third party.

---

## 2. Goals & Non-Goals

**Goals**
- Local-first. No account, no cloud, no telemetry. All data stays on the machine.
- Clean separation: protocol | engine | UI | server.
- The protocol library is independently valuable — the first solid C# NIIMBOT implementation.
- Native feel and platform-correct ergonomics (menus, accelerators) on all three OSes.
- Data-driven batch printing from external DBs and files.

**Non-Goals (initial)**
- Cross-platform Bluetooth (BLE). USB serial first; BLE behind the same transport interface later.
- Mobile clients.
- A general graphics editor — this is a *label* tool with a fixed-DPI raster target, not Inkscape.

---

## 3. Tech Stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | **.NET 10** | TFM `net10.0`; app/server may use `net10.0` + OS RIDs |
| UI | **Avalonia** (latest stable at project start) | XAML, MVVM, Skia renderer |
| MVVM | **CommunityToolkit.Mvvm** | Lighter than ReactiveUI; `ObservableObject`, `RelayCommand`, source generators |
| Rendering | **SkiaSharp** | Avalonia's own backend; render label → `SKBitmap` → 1bpp |
| Barcodes / QR | **ZXing.Net** (+ `ZXing.Net.Bindings.SkiaSharp`) | Code128/39/EAN/etc. + QR in one lib. `QRCoder` as QR-only fallback |
| Data access | **Dapper** + per-provider ADO.NET drivers | See §6.5 — explicitly *not* ODBC |
| File data | **CsvHelper**, **ClosedXML** (xlsx), `System.Text.Json` | ClosedXML is MIT; avoid EPPlus (non-commercial license) |
| Packaging (storage) | `System.IO.Compression` (built-in) | Zip container, no dependency |
| MCP server | **ModelContextProtocol** C# SDK | *Confirm exact package name/version at project start — SDK is evolving* |
| Server host | **ASP.NET Core** (Minimal API) | HTTP + SSE transport for MCP; stdio transport also supported |
| Tests | **xUnit** + **Verify** (snapshot) | Snapshot the rendered bitmaps + serialized packages |

All packages above are FOSS-licensed and cross-platform. Pin versions at scaffold time.

---

## 4. Solution Architecture

```
Thermalith.slnx                    # modern .slnx solution format
Directory.Build.props                 # shared: net10.0, Nullable, ImplicitUsings, version vars
Directory.Packages.props              # central package management (versions live here, not in csproj)
global.json                           # pin the .NET SDK
├── src/
│   ├── Niimbot.Net/                 # (1) NuGet: protocol + transport. PURE. No UI, no render, no data.
│   ├── Thermalith.Core/         # (2) Label engine: document model, render, serialize, data-bind.
│   │                                #     Depends on Niimbot.Net. No UI.
│   ├── Thermalith.App/           # (3) Avalonia desktop editor. Depends on Core.
│   └── Thermalith.Server/        # (4) API / MCP server. Depends on Core.
│                                    # (Donations: external EvilGeniusLabs.DonationWare NuGet — own repo.)
├── tests/
│   ├── Niimbot.Net.Tests/
│   ├── Thermalith.Core.Tests/
│   └── Thermalith.Server.Tests/
├── tools/
│   └── print-harness/               # Minimal console app: open serial, print one bitmap. Transport proof.
└── docs/
    ├── manual/                      # PDF manual source
    └── capabilities/                # LLM-facing JSON capability doc (see §9)
```

**Dependency direction (strict):**

```
Niimbot.Net  ←  Thermalith.Core  ←  Thermalith.App
                          ↑
                          └──────────────  Thermalith.Server
```

Nothing depends "up". `Niimbot.Net` knows nothing about labels, rendering, or data. `Core` knows nothing about Avalonia. The App and Server are siblings that share `Core`. Donation support is the **external `EvilGeniusLabs.DonationWare` MIT NuGet** (its own repo, §4.2) consumed by the App at Phase 6 — it sits entirely outside this solution and the Niimbot domain graph.

> **Note on the "implementation project":** the label document model, rendering, serialization, and data binding are shared between the editor and the server, so they belong in `Thermalith.Core`, not inside the app. The app is the editor *UI* on top of Core; the server is headless access *to* Core. This keeps the three-project intent intact while avoiding duplicated engine logic.

### 4.1 Solution conventions & house style

Conventions for the whole solution, set once at scaffold.

- **Solution format:** `.slnx` (modern XML solution), flat `src/` + `tests/`, projects named on the root namespace (`Niimbot.*` / `Thermalith.*`).
- **Shared build config:** root `Directory.Build.props` sets `net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, and shared version variables (e.g. an `$(AvaloniaVersion)`).
- **Central package management:** `Directory.Packages.props` with `<ManagePackageVersionsCentrally>true` + `CentralPackageTransitivePinningEnabled` — csproj files reference packages *without* versions; every version lives centrally. `global.json` pins the SDK.
- **MVVM:** `ViewModelBase : ObservableObject`; `[ObservableProperty]` on `_underscore` backing fields, `[RelayCommand]` methods, `partial` classes throughout; `[NotifyCanExecuteChangedFor(...)]` to gate command enablement. Marshal background callbacks onto the UI thread with `Dispatcher.UIThread.Post`. View code-behind is just `InitializeComponent()`.
- **DI — deliberate split:** the **App** uses **no container** — manual wiring in `Program.cs`, app-wide settings exposed statically, constructor injection into VMs. But keep **`Thermalith.Core` container-agnostic** (plain constructors, no container reference) so both consumers wire it their own way, and let **`Thermalith.Server`** use **ASP.NET Core's built-in DI** — idiomatic for the host.
- **Logging:** Serilog with category context (`Log.ForContext("Category", …)`). File sink to the per-platform log dir (§7).
- **Core stack pins (bump to latest stable at scaffold):** Avalonia `11.3.x`, CommunityToolkit.Mvvm `8.x`, SkiaSharp via Avalonia's backend, `Avalonia.Fonts.Inter` (the bundled Latin default, §6.3). Pin exact versions in `Directory.Packages.props`.

### 4.2 Donation support — external `EvilGeniusLabs.DonationWare` NuGet

Voluntary donation options (Ko-fi, PayPal, Stripe Payment Link, custom) are provided by the
**`EvilGeniusLabs.DonationWare`** package — a small, **UI-agnostic, MIT-licensed** NuGet that lives in
**its own repo** (`d:\Projects\EvilGeniusLabs.DonationWare`, extracted 2026-06-08; reusable across all
EvilGeniusLabs software, open or commercial). Thermalith simply *consumes* the published NuGet and wires
it into Help/About at Phase 6 (§12) — off the critical path, not part of this solution. Full design +
worklist live in that repo's `Documentation/`. URL-only, no money handled in-process.

**License: MIT** — a generic reusable widget wants maximum reuse and carries no protocol IP. A permissive package aggregates cleanly into the otherwise-GPL-v3 solution (§10).

---

## 5. Project 1 — `Niimbot.Net` (Protocol NuGet)

Already scoped in prior planning. Recap of boundaries:

**In:** packet framing (header/opcode/length/payload/checksum/footer), full command set, request/response handling, 1bpp row encoding @ 8px/mm (incl. RLE + print-task-version variants), client/session API (connect, status, density/label-type, print, progress poll, disconnect), `INiimbotTransport` abstraction with `SerialTransport` (`System.IO.Ports`) as first impl, per-model profiles (B1 first). **Profiles carry resolution (DPI / px-per-mm) and max print width** — Core reads these to seed `canvas.dpi` (§6.1), since this varies across models (203 vs 300 DPI).

**Capability & loaded-label query (drive setup from the hardware).** On connect, query the device for **model / firmware**, resolve it to a profile, and expose a `PrinterCapabilities` record (resolution, max print width, density range, supported label types). Where the model and the loaded roll support it (the **B1 reads RFID-tagged label rolls**), also query the **loaded label's dimensions/type** and surface it. Core uses this to auto-seed `canvas.dpi` *and* `canvas.widthMm/heightMm` (§6.1) — the user only sets up manually when the hardware can't report it. This is a read; the user can always override.

**Out:** rendering, label model, data, UI, inventory.

**Source material — synthesis, not a port.** We study **two** existing implementations as co-equal references and produce a **wholly new, idiomatic C# implementation** from them — not a line-by-line translation of either:
- `niimbluelib` (TypeScript) — the most complete open-source treatment of the command set, packet framing, and print-task-version variants.
- `niimprint` (Python) — independent cross-check on raw-serial framing and real-world device behaviour.

Where the two disagree, **byte-level capture from a real B1 is the tiebreaker** (the unit tests in §10 are built from those captures). The output is our own work: idiomatic async C#, no interop, no JS/Python runtime, no copied source. This synthesis approach — plus the GPL-v3 license (§10) — keeps the lib clear of derivative-work entanglement with either upstream project.

**API shape decisions:**
- **Async-first** throughout — `Task`/`ValueTask` + `CancellationToken` on all I/O.
- **Transport** — the default *is* injection; full design in §5.1.

### 5.1 Transport & discovery (resolved, Open Decision #4)

**Layered — not "inject vs own", but both.** One minimal interface, a shipped default impl, swappable by advanced consumers. The default path is injection with zero friction.

`INiimbotTransport` is a **dumb async byte duplex** — it knows nothing about Niimbot packets:

```csharp
public interface INiimbotTransport : IAsyncDisposable
{
    bool IsConnected { get; }
    ValueTask ConnectAsync(CancellationToken ct = default);
    ValueTask DisconnectAsync(CancellationToken ct = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);
    event EventHandler<TransportState> StateChanged;   // surprise unplug, reconnect, etc.
}
```

- **Byte-level, not frame-aware.** All framing (header/opcode/length/checksum/footer), request/response correlation, and RLE live in the client *above* the transport. This keeps serial, a future BLE transport, and a test/replay transport all trivial to implement. (BLE is GATT characteristics rather than a stream, but models cleanly as a byte duplex — write-to-characteristic / notify-gives-bytes — so promoting the BLE non-goal later won't break this interface.)
- **`SerialTransport : INiimbotTransport`** is the shipped default (`System.IO.Ports.SerialPort`). The client accepts an `INiimbotTransport`; a convenience factory builds a `SerialTransport` from a port name for the common case.
- **Designed clean** from the protocol's own needs.

**Discovery — enumerate + probe.** The lib ships two pieces:
- a `SerialPortEnumerator` (`SerialPort.GetPortNames()` + OS friendly-names / VID·PID where available); and
- a **probe** — open a candidate port, send a lightweight status/info command, confirm a real Niimbot answers and identify the model.

This powers the App's printer panel *and* the future MCP `list_printers` / `printer_status` (§8): "find my printer" works without the user guessing a COM port, and a probe hitting the wrong device fails fast instead of silently.

**Concurrency — single in-flight request + internal command queue.** Serial is sequential request/response; the **client** (not the transport) owns a queue, serializes commands, and awaits each response with a per-command timeout + cancellation. The transport stays a dumb duplex. Use `System.IO.Pipelines` (`PipeReader`) over the read side for efficient frame accumulation — implementation detail.

**Lifecycle / disposal — ownership follows creation.** Inject a transport → you dispose it. Use the port-name convenience → the client owns and disposes the `SerialTransport` it created (tracked by an ownership flag). `IAsyncDisposable` end to end.

**Testing seam.** A `FakeTransport` / replay transport implementing the same interface drives the captured-byte unit tests (§10) — the entire protocol layer is testable with no hardware attached.

**License:** GPL-v3, free-for-all, no commercial track (see §10). As a wholly-new rebuild (above) we inherit no license from `niimbluelib`/`niimprint` and chose GPL-v3 outright; the LGPL carve-out was considered and dropped.

---

## 6. Project 2 — `Thermalith.Core` (Label Engine)

UI-agnostic engine: a label *is* a document; the editor and server both manipulate that document.

### 6.1 Label Document Model

A `LabelDocument` is the root, serialized to the storage package (§6.6). **The full field-level schema, every element type's property table, and a complete 50×30 mm worked example live in the companion [`label-json-spec.md`](label-json-spec.md);** this section is the design summary.

**Coordinate & unit conventions (authoritative — every consumer obeys these):**
- Origin **top-left**; `+x` is right, `+y` is down.
- **Geometry is in mm** (x, y, w, h, stroke widths, corner radii, barcode module widths, quiet zones). **Typography is in pt.** The renderer converts both to pixels via `canvas.dpi`. One unit per field — never mix.
- `rotation` is **degrees clockwise**, applied about the element's **center**.
- **Z-order is array order**: `elements[0]` is the backmost, the last entry is frontmost. There is no separate z field — reordering is a list move.
- Every element has a **stable `id`** that survives undo/redo, copy/paste, and data binding.

**Root structure:**

```jsonc
{
  "schemaVersion": "1.0",
  "metadata": {
    "name": "shelf-label",
    "createdUtc": "2026-06-06T12:00:00Z",
    "modifiedUtc": "2026-06-06T12:00:00Z",
    "appVersion": "0.1.0"
  },
  "canvas": {
    "widthMm": 40, "heightMm": 30,       // auto-seeded from the loaded RFID roll when available (§5); else manual.
    "dpi": 203,                          // model-dependent; seeded from the printer profile (§5).
                                         // 203 DPI ≈ 8 px/mm; 300-DPI models ≈ 11.8 px/mm.
    "shape": "rectangle",                // rectangle | rounded | circle | dieCut
    "cornerRadiusMm": 2,                 // when shape = rounded
    "tail": { "position": "none", "lengthMm": 0 },   // cable labels: none | left | right
    "background": "white"
  },
  "elements": [
    {
      "id": "el_a1b2c3",
      "type": "text",
      "name": "Product name",            // shown in the layers list
      "x": 2, "y": 2, "w": 36, "h": 8,
      "rotation": 0,
      "locked": false, "visible": true,
      "justify": { "h": "left", "v": "top" },   // content alignment WITHIN the element (see §6.2)
      "props": { "content": "{name}", "fontFamily": "Inter", "fontSizePt": 9, "wrap": "word" }
    }
  ],
  "dataSource": {                        // connection details only — NEVER credentials (§6.5, §7)
    "kind": "none",                      // none | database | csv | xlsx | json
    "credentialRef": null                // opaque key into the OS secret store, not the secret
  },
  "bindings": {                          // explicit token→column remap; auto-mapped by name otherwise
    "sku": "product_sku"
  }
}
```

The four element fields above the `props` object (`id`, `type`, `name`, geometry, `rotation`, `locked`, `visible`, `justify`) form the **element base**, shared by every control. The `props` object is type-specific — field names are listed in §6.1.1.

#### 6.1.1 Per-type `props` fields

These are the concrete JSON field names backing the prose controls catalog in §6.2.

| `type` | `props` fields |
|---|---|
| `text` | `content` (token-aware), `fontFamily`, `fontSizePt`, `bold`, `italic`, `underline`, `lineSpacing`, `letterSpacing`, `wrap` (`none`/`word`), `fontSizing` (`fixed`/`shrink`/`fill` — auto-scale to the box), `minFontSizePt`, `maxFontSizePt` |
| `barcode` | `symbology`, `value` (token-aware), `showText`, `textPosition` (`above`/`below`/`none`), `moduleWidthMm`, `quietZoneMm` |
| `qr` | `value` (token-aware), `encoding` (`text`/`hex`), `ecLevel` (`L`/`M`/`Q`/`H`), `moduleSizeMm` (or `"auto"`), `quietZoneMm` |
| `serial` | `start`, `step`, `padLength`, `padChar`, `prefix`, `suffix` (the live counter is runtime state, not stored in the doc) |
| `datetime` | `kind` (`date`/`time`/`datetime`), `format` (.NET format string), `source` (`printNow`/`fixed`), `fixedValueUtc` |
| `shape` | `shapeType` (`rect`/`roundedRect`/`ellipse`), `strokeWidthMm`, `fill` (`none`/`solid`), `cornerRadiusMm` |
| `line` | `x1Mm`/`y1Mm`, `x2Mm`/`y2Mm` (endpoints relative to the element origin), `weightMm`. Base `x`/`y`/`w`/`h` is the derived bbox (kept in sync). Legacy `shape`/`line` migrates to this on load. |
| `image` | `assetId` (→ `assets/`), `fit` (`fill`/`fit`/`stretch`/`center`), `dither` (`threshold`/`floydSteinberg`/`atkinson`/`ordered`/`none`), `threshold` (0–255), `invert` |
| `table` | `cols`, `rows`, `columnWidthsMm[]`, `rowHeightsMm[]`, `cells[][]` (each `{ content, justify }`), `borderWidthMm`, `headerRow` |

#### 6.1.2 Scope of the model

The model is a root with canvas + a flat, ordered control collection (optionally nested in containers); each control shares a geometry/identity base plus a type-specific block and a freeform `properties` escape hatch. This deliberately simple shape is what lets the editor GUI (§7), snapshot undo (§6.4), and validation (§6.7) stay straightforward.

- **Include:** an optional **style cascade** (a label-level default style overridable per element — cuts repetition on multi-text labels); freeform `properties` + `[JsonExtensionData]` forward-compat (§6.6).
- **Exclude:** any interactive machinery — animation, event wiring, input/HID. A print label is **static output**, not an interactive screen; its only dynamism is token/data substitution (§6.5).
- **Units are physical, not pixels:** a label is a physical artifact printed at 203/300 DPI, so geometry is **mm**, type is **pt**, and pixels exist only at render time (§6.3).

**Label presets (label-stock catalog).** Ship a catalog of standard Niimbot label stock — per-SKU `widthMm × heightMm` + `shape` (rectangle / rounded / circle / die-cut / cable) — so the user picks "40×30 rect" from a list instead of typing dimensions. Complements RFID auto-detect (§5) for non-RFID rolls and seeds new-label setup. **The catalog is a data-gathering task: mine Niimbot's own desktop software and website for the official label SKUs/sizes/shapes** (§11). Ships as a static JSON resource in Core, user-extensible with custom sizes.

#### 6.1.3 Safe print area (hardware skew / registration tolerance)

Hardware finding (B1, validated 2026-06-07): real prints carry a small **constant** mechanical skew — the label tracks through the head at a slight fixed angle, shearing straight lines — plus a registration offset. The **vendor app skews the identical square the same way**, confirming this is hardware, not the raster we send. Design intent: **slight skew is acceptable; content printing off the label is not.** The engine therefore optimizes for *staying on-label*, not for perfect straightness.

- **Safe print area.** The canvas carries an inset **safe area** — a margin smaller than the full label — sized so worst-case skew + registration cannot push content past the label edge. Authoring defaults inside it; content may extend toward the bleed edge, but validation (§6.7) flags anything at risk of clipping. The inset is per-profile and calibratable.
- **Editor guides (§7).** The editor draws the safe-area rectangle (vertical + horizontal guides) on the canvas so the user lays out within it; the bleed/edge zone is visually distinct.
- **Transparency.** When output is skewed, the product says plainly it is printer registration/skew (a hardware limit): we send a clean raster, the head prints it straight, the paper path shears it. No implication of a design defect.
- **Optional counter-shear (secondary).** Because the skew is constant, the calibration system (§6.3.6) *may* pre-shear the raster to cancel it — exceeding vendor fidelity (the app does not). A nice-to-have, not required to ship; the safe area is the primary mitigation.

### 6.2 Controls Catalog

| Control | Type-specific properties |
|---|---|
| **Text** | content (supports `{tokens}`), font family/size/weight/style, line spacing, wrap, justification, alignment. Static captions are just Text with no `{token}` in `content` — there is no separate Label control (resolved, Open Decision #3). |
| **Table** | rows/cols, cell content (token-aware), borders, per-cell alignment, column widths |
| **Barcode** | symbology (Code128/39/EAN/UPC/ITF…), value (token-aware), show-text toggle, module width, quiet zone — via ZXing.Net |
| **QR Code** | value (token-aware, incl. `hex:` binary), error-correction level, module size — via ZXing.Net/QRCoder |
| **Serial Number** | start value, increment step, padding/format, per-print advance (auto-increments across a batch) |
| **Time** | format string, source = print-time-now or fixed |
| **Date** | format string, source = print-time-now or fixed |
| **Standard Shapes** | rectangle, rounded rectangle, ellipse/circle, line; stroke width, fill, corner radius |
| **Image** | embedded raster (stored in package), fit/scale mode, dithering/threshold algorithm |

**Justification & Alignment — two distinct concepts, both required:**
- **Justification** = content alignment *within* an element (text left/center/right/justify; vertical top/middle/bottom).
- **Alignment / Arrange** = positioning *across selected elements* (align edges/centers, distribute, send-to-front/back, group). Mirrors the toolbar in the vendor app.

**Auto-sizing (text).** A text control's `fontSizing` makes the font follow the box: `fill` scales the type to the largest size that fits `w`×`h` (grows *and* shrinks as the control resizes), `shrink` only reduces to avoid overflow, `fixed` leaves it alone. `minFontSizePt` / `maxFontSizePt` bound the result. The renderer finds the fitted size by binary-searching measure in the layout pass (§6.3); the editor recomputes live on resize and shows the computed size read-only in the inspector.

### 6.3 Rendering Pipeline

The renderer is the heart of Core: it turns a `LabelDocument` into the exact 1bpp raster `Niimbot.Net` prints, and the editor preview is the *same* code path (WYSIWYG). It lives in Core (SkiaSharp only, **no Avalonia dependency**) so the headless server renders identically.

```
LabelDocument (+ optional data row)
  │
  ├─ 1. RESOLVE      bind data, substitute {tokens}, expand serial/date/time
  │                  → ResolvedLabel (no unresolved tokens; pure, deterministic)
  ├─ 2. MEASURE      text wrap + auto-sizing (shrink/fill), table cell rects, barcode/QR grids
  │                  → laid-out element geometry in device px
  ├─ 3. RASTER       draw elements in z-order onto an 8-bit grayscale SKSurface
  │                  at  ceil(mm × dpi/25.4)  px
  ├─ 4. MONOCHROME   → 1bpp: hard threshold for text/vector, dither for images (§6.3.2)
  │                  → MonochromeBitmap (model-agnostic: W×H px, packed bits)
  └─ 5. ENCODE       Niimbot.Net → wire format per the profile's print-task
                     version + row RLE  (this is NOT the renderer's job)
```

**Boundary correction (the old sketch was wrong here):** the renderer emits a plain, model-agnostic `MonochromeBitmap`; the *encoder* in `Niimbot.Net` applies the model/firmware-specific **print-task version** and row RLE, seeded from the printer profile (§5). Keep that knob out of Core's renderer — Core must not know wire formats.

#### 6.3.1 Units & the device transform
`pxPerMm = canvas.dpi / 25.4` (203 dpi → 7.992 px/mm — **not** a hard-coded 8, §6.1). Surface size = `ceil(widthMm × pxPerMm) × ceil(heightMm × pxPerMm)`. All mm geometry multiplies by `pxPerMm`; pt typography converts via `px = pt × dpi / 72`. A single `RenderContext` carries `pxPerMm` + the surface; every element draws through it. Element `rotation` is an `SKCanvas` rotation about the element-rect centre; whole-label orientation (print-head feed direction) is applied at this stage too, before monochrome.

#### 6.3.2 Crisp vs dithered — the one rule that keeps prints scannable
The pipeline's critical decision. **Never dither the whole composite.** Dithering text edges and barcodes turns them into fuzzy, unscannable noise on a 1-bit head. Split by content kind:
- **Text, barcodes, QR, shapes, lines → hard threshold** (pure black/white). They are inherently bi-level and must stay crisp.
- **Images → dithered** per the element's own `props.dither` (`floydSteinberg`/`atkinson`/`ordered`/`threshold`/`none`) + `threshold`, converted to 1-bit *before* compositing so an image's dithering never bleeds into neighbouring text.

Implementation: pre-dither each image to a 1-bit blit; draw text/vector with the AA rule below; OR the planes together. Stage 4's global pass is then only a threshold safety-net over whatever grayscale remains (mostly anti-aliased text).

#### 6.3.3 Anti-aliasing & pixel-snapping
- **Text:** AA **on** → grayscale → threshold (~50%). At ~8 px/mm, small type is unreadable without AA+threshold, and pure aliased text is too jagged. Threshold is tunable; expose if needed.
- **Barcodes / QR / thin rules:** AA **off**, and **snap module width to whole device pixels** — round `moduleWidthMm × pxPerMm` to ≥1 px and draw bars as exact integer-px rectangles. Sub-pixel bars are the #1 cause of codes that won't scan. If a requested module rounds to <1 px at this DPI, surface a warning.

#### 6.3.4 Fonts (WYSIWYG depends on this)

SkiaSharp resolves fonts against whatever the host OS provides, so a label authored on Windows can render differently — or tofu-out entirely for CJK — on Linux/macOS or on the headless server. Strategy (resolved, see §11):
- **Any system TTF is selectable.** The font picker enumerates all installed fonts via SkiaSharp's `SKFontManager` — no allow-list. The user designs with whatever they have.
- **Bundle a minimal default set** with the app/Core purely as the guaranteed fallback floor: one Latin family + one CJK-capable family, both **OFL/Apache-licensed** (e.g. Inter + Noto Sans CJK) so there is no redistribution risk.
- **Missing-font fallback chain**, applied at render time: `[requested family] → [bundled default] → [OS default]`. When a loaded `.nlbl` names a font that isn't installed locally, Core falls back, the editor **flags the substitution**, and the user can reassign to any available font.
- The `.nlbl` package stores the **font family name only** — never the font file. (No embedding; keeps the format lean.)
- `canvas.dpi` drives the pt→px conversion, so font sizing is correct on 300-DPI models, not just 203.

#### 6.3.5 The 1bpp output contract
`MonochromeBitmap { int WidthPx; int HeightPx; byte[] Packed; }` — row-major, **MSB-first** within each byte, `1 = burn (black)`, rows padded to a byte boundary. The exact bit order + padding are **verified against a real B1 byte capture** (§5, §10) before trusting niimbluelib's docs. This struct is the clean hand-off seam into `Niimbot.Net`: model-agnostic, trivially snapshot-testable, and the encoder's sole raster input alongside the profile.

#### 6.3.6 Preview modes (honest WYSIWYG)
The editor reuses this exact path and offers two previews: **(a) smooth** — the grayscale stage-3 surface, easy on the eyes for layout; **(b) exact** — the stage-4 1-bit result, i.e. what actually burns. **Default to exact**, because thermal output is harsh and a smooth preview lies about how thin strokes, light grays, and dithered photos really come out. Same renderer, just stop before or after stage 4; a side-by-side toggle is ideal.

**(c) thermal-accurate — "can't tell it's a preview" (later-phase polish).** Simulate the *physical* print on top of the exact 1-bit raster so the on-screen label is near-indistinguishable from the real thing:
- render burnt dots onto real **thermal-paper tone** (off-white, not `#fff`) using a **burn colour** that is a dark warm near-black (not pure `#000`);
- apply slight **dot gain / bleed** so edges read like heat diffusion, not crisp pixels;
- make darkness **density-aware** — tied to the selected print-density setting;
- offer a **life-size view** (on-screen mm = physical mm via monitor DPI) so the label renders actual size.

Highest fidelity is an opt-in **calibration**: print a test pattern, scan/photograph it, derive paper tone + burn colour + dot gain per printer + paper, and profile the simulation to that hardware. Hard physical ceiling: an emissive screen can't perfectly match reflective paper under all lighting, but paper-tone + accurate burn + dot-gain + life-size gets convincingly close. Strictly after the core prints — not before Phase 0–3.

#### 6.3.7 Determinism, tests & batch
The whole path is **pure** given `(ResolvedLabel, profile, renderOptions)` — datetime/serial are frozen in stage 1, no other clock or RNG — so Verify snapshot tests over the stage-4 bytes guard both WYSIWYG and the encoder boundary (§10). For **batch** print (N rows): render the static, non-bound elements once into a base layer, then per row re-render only the bound elements over a copy — large speedups on big runs, and per-row work stays proportional to what actually changes.

### 6.4 Undo / Redo

**Snapshot-based undo.** A `LabelDocument` is a small, fully JSON-serializable model (§6.1), so cloning the whole document on each committed edit is cheap and dead simple. Two stacks (undo/redo); push a snapshot **at gesture boundaries** (before a drag/resize begins, once on release), *not* per-frame, so cost stays one clone per user action and the "can't coalesce a drag" concern never arises. Lives in Core so the server could expose it too, but it's primarily driven by the App.

> Trade-off accepted: snapshots hold N document copies vs a command-pattern's deltas. For label-sized documents that's negligible, and it's far simpler to build. (A command-pattern with `Apply`/`Revert` was the earlier draft; dropped — the document is small enough that whole-document snapshots win on simplicity.)

### 6.5 Data Binding & Tokens (the "not ODBC" requirement)

**Template + data are separate concerns (resolved — supersedes the original "connection details live in `label.json`" model).** A label is a **template** that *declares a token contract*; **data** fills that contract from one of several sources. This is the architectural shift that makes the API/MCP verb clean — *"take this template + this data, print it"* — and it lets a single template back many print runs.

**The token contract (declared by the template).** The template lists the tokens it expects — `{name}` plus optional `type`, `description`, `sample`, `default`, `required`. The editor auto-populates this list by scanning the `{tokens}` used in element content; the author can annotate it. This declared contract is exactly what MCP `load_label` returns (§8), so an LLM or API caller knows what to supply without parsing the layout.

**Where the data comes from (precedence — first present wins):**
1. **Supplied directly** by the API/MCP caller or a passed-in row — the headless "here's the data, print it" path.
2. **Bundled `data.json`** in the package (§6.6) — sample or default rows that ship with the template.
3. **A live data source** the template references (DB query / file) — resolved at print time.

Unbound tokens render as visible placeholders in preview. A binding panel maps source columns → tokens (explicit, or auto-by-name).

**Live data sources — databases via Dapper over native ADO.NET providers (no ODBC):**

| DB | Provider package |
|---|---|
| PostgreSQL | `Npgsql` |
| MySQL / MariaDB | `MySqlConnector` (MIT) |
| SQL Server | `Microsoft.Data.SqlClient` |
| SQLite | `Microsoft.Data.Sqlite` |
| (Oracle, optional) | `Oracle.ManagedDataAccess.Core` |

Dapper runs the user's query against the chosen `DbConnection` and returns rows as `IDictionary<string,object>` — columns become tokens directly. A small `IDataSourceProvider` factory maps provider-enum → connection. (`System.Data.Common.DbProviderFactories` is an alternative provider-agnostic route; the explicit-driver approach is cleaner and more predictable — recommend it.)

**File connectors:**
- **CSV** → `CsvHelper` (headers become tokens).
- **XLSX** → `ClosedXML` (sheet + header row → tokens).
- **JSON** → `System.Text.Json` (flatten to rows).

**Token syntax & batch.** `{column_name}` inside text/barcode/qr/table content. **Batch print** = one rendered label per data row; serial/time/date controls advance per row (the §6.3.7 base-layer optimization applies). A referenced live source stores **connection details only, never credentials** (those live in the OS secret store, §7); resolved row *values* may be bundled in `data.json`, but credentials never are.

### 6.6 Storage Format — `.nlbl` package

A **Zip container** (`System.IO.Compression`). The package is a **template** that *may* also carry data:

```
mylabel.nlbl  (zip)
├── manifest.json     # package metadata + manifestVersion + fingerprint (fields below)
├── label.json        # the TEMPLATE: canvas, elements (with {tokens}), declared token contract — no data values
├── data.json         # OPTIONAL: data rows that fill the tokens (a sample, or an actual batch)
└── assets/
    ├── image_0001.png # embedded images, referenced by id from label.json
    └── ...
```

- **Template vs. label-with-data is just whether `data.json` is present.** Omit it → a reusable template (a "label type"). Include it → a self-contained label carrying its own data. Either way the API/MCP can override with supplied data (§6.5). This is the structural payoff of the template/data split.
- **`manifest.json`:** `manifestVersion` (int — refuse to open a *newer* one), `id` (stable UUID / reverse-DNS), `name`, `version` (SemVer, author-controlled), `created` / `updated` (ISO-8601 UTC), `author`, `description`, `license`, `fingerprint` (SHA-256 of content, integrity check).
- **JSON conventions:** `System.Text.Json`, camelCase, **forgiving read** (comments + trailing commas allowed), null-omit on write, and `[JsonExtensionData]` on every model so unknown fields from a newer version round-trip instead of being silently dropped.
- **Export = Save**: the `.nlbl` package *is* the interchange format. Import is just opening it; no separate export pipeline.
- **Credentials are never stored** (§7): a referenced live source keeps connection details in `label.json`, resolved values may sit in `data.json`, secrets stay in the OS secret store.
- JSON is human-diffable (git-friendly); images live beside it.

### 6.7 Validation

`ILabelValidator.Validate(LabelDocument) → ValidationResult` of `ValidationDiagnostic { Code, Severity (Error/Warning/Info), Message, JsonPath, Line? }`. Representative rules:

- undeclared/unresolved tokens; duplicate element `id`s;
- **barcode/QR module rounds to <1 px at `canvas.dpi`** → won't scan (§6.3.3);
- text overflow / `fontSizing` floor (`minFontSizePt`) hit; element outside canvas (or bleed) bounds;
- missing asset reference; named font not installed (warning — §6.3.4).

Surfaced live in the editor's **output panel** (§7) and returnable via the API/MCP. **Errors block print; warnings don't.** The `Code` / `Severity` / `JsonPath` diagnostic shape keeps messages and tooling consistent.

---

## 7. Project 3 — `Thermalith.App` (Avalonia Editor)

The interactive editor. The interface is the whole point of building this — design for **fast, repetitive, fixed-template labeling**, not freeform graphic design.

**Core surfaces:**
- **Canvas/editor** with live WYSIWYG preview (reuses Core's renderer).
- **Element inspector** (selected element's properties + justification).
- **Insert toolbar** (the controls catalog, §6.2).
- **Arrange toolbar** (align/distribute/z-order, §6.2).
- **Data panel** — provider + connection + query + token mapping; row navigator for preview; batch print.
- **Printer panel** — discover serial ports, connect, status (paper/cover/battery), density, offset calibration, quantity.

**Window shape — a five-region docked grid** (the proven layout for this kind of editor). Panels resizable via `GridSplitter`, widths persisted to settings:

```
┌───────────────────────────────────────────────┐
│  Menu (File · Edit · View · Insert · Arrange…) │
├───────────────────────────────────────────────┤
│  Toolbar  (insert · arrange · zoom · grid/snap)│
├──────────┬──────────────────────────┬──────────┤
│ LEFT     │   CENTER CANVAS          │ RIGHT    │
│ Layers / │   (live WYSIWYG)         │ Inspector│
│ element  │                          │ (props + │
│ tree /   │                          │  justify)│
│ insert   │                          │          │
│ palette  │                          │          │
├──────────┴──────────────────────────┴──────────┤
│  Status bar (cursor mm · zoom · dirty flag)     │
└───────────────────────────────────────────────┘
```

Concrete patterns:
- **Canvas = three stacked layers** under one `RenderTransform` (scale + translate): `_renderedLayer` (the WYSIWYG render from Core), `_gridLayer` (grid dots), `_selectionLayer` (selection adorners + marquee). Pointer events on the host drive an `InteractionMode` enum (`None/Panning/DragSelect/Moving/Resizing/DragPlacing`).
- **Selection adorners:** dashed rect + 8 resize handles, `IsHitTestVisible=false`, rebuilt on selection change; handle radius scales by `1/zoom`.
- **Inspector = `ItemsControl` + per-type `DataTemplate`s** over an `ObservableCollection<object>` of property-item VMs (header / text / number / dropdown / checkbox / color / dimension-pair…). The VM rebuilds this list for the current selection. This is exactly how our type-specific `props` (§6.1.1) should surface in the UI — one template per property kind, resizable label column.
- **Debounced render** (~100 ms) so property-spinner drags don't thrash the renderer; debounced validation similarly.
- **Panel sizes persisted** on close, restored on load (see settings, below).

**File handling:**
- **Load / Save / Save As** (`.nlbl`).
- **Last-edited list (MRU)** — persisted in a JSON settings file in the platform app-data dir (`%APPDATA%` / `~/.config` / `~/Library/Application Support`). Use a small settings abstraction; no registry.
- **Export** = Save As `.nlbl` (see §6.6).

> **Settings + MRU store.** Use a per-platform path resolver (`OperatingSystem.IsWindows/IsMacOS/IsLinux` + `Environment.SpecialFolder`) to locate the app-data dir, and a small JSON load/save (`System.Text.Json`, `Directory.CreateDirectory` + try/catch on read) for both the settings file and the MRU list. This is a *user-writable runtime* store — distinct from any deploy-time `appsettings.json`.

**Secrets:** DB credentials stored via OS secret stores where available, else an encrypted local store — never in the `.nlbl` package.

> **Build a real per-OS secret store** for DB credentials: Windows DPAPI / Credential Manager, macOS Keychain, Linux Secret Service (libsecret), with an encrypted-file fallback. Never store credentials as plaintext. (Open Decision #6.)

### 7.1 Standard Menus

`File` · `Edit` · `View` · `Insert` · `Arrange` · `Printer` · `Help`

- Use Avalonia **`NativeMenuBar`** so macOS gets a real top-of-screen menu bar (and the standard app menu), while Windows/Linux get an in-window menu. App-menu items (About, Preferences, Quit) route to the macOS app menu on macOS.

> **Menus:** use `NativeMenuBar` + a central keymap service (§7.2) for correct macOS behaviour, with `[RelayCommand]`-per-action wiring — rather than per-`MenuItem` `InputGesture` hotkeys, which don't yield a real macOS menu bar.

### 7.2 Accelerator Keys (platform-correct, matching)

Avalonia exposes the platform command modifier via `TopLevel.PlatformSettings.HotkeyConfiguration` / `PlatformHotkeyConfiguration` — bind gestures to the **platform command key** so the *same* logical accelerator resolves to **⌘ on macOS** and **Ctrl on Windows/Linux** automatically. Define gestures centrally (a keymap service), not per-control.

| Action | macOS | Windows / Linux |
|---|---|---|
| New | ⌘N | Ctrl+N |
| Open | ⌘O | Ctrl+O |
| Save | ⌘S | Ctrl+S |
| Save As | ⇧⌘S | Ctrl+Shift+S |
| Print | ⌘P | Ctrl+P |
| Undo | ⌘Z | Ctrl+Z |
| Redo | ⇧⌘Z | Ctrl+Y |
| Cut / Copy / Paste | ⌘X / ⌘C / ⌘V | Ctrl+X / C / V |
| Duplicate | ⌘D | Ctrl+D |
| Delete | ⌫ / ⌦ | Delete |
| Select All | ⌘A | Ctrl+A |
| Group / Ungroup | ⌘G / ⇧⌘G | Ctrl+G / Ctrl+Shift+G |
| Bring Front / Send Back | ⇧⌘] / ⇧⌘[ | Ctrl+Shift+] / Ctrl+Shift+[ |
| Zoom In / Out / Fit | ⌘+ / ⌘− / ⌘0 | Ctrl++ / Ctrl+− / Ctrl+0 |
| Preferences | ⌘, | Ctrl+, |
| Quit | ⌘Q | (Alt+F4 / window close) |

---

## 8. Project 4 — `Thermalith.Server` (API / MCP)

Headless access to the engine for automation and LLM workflows. Built on the **ModelContextProtocol** C# SDK, hosted in **ASP.NET Core** (Minimal API). Supports both **HTTP+SSE** (remote) and **stdio** (local LLM client) transports. Uses `Thermalith.Core` for everything and `Niimbot.Net` for the actual print.

**Proposed MCP tool surface:**

| Tool | Purpose |
|---|---|
| `list_printers` | Enumerate available serial ports / printers |
| `printer_status` | Paper / cover / battery / readiness for a printer |
| `list_labels` | List saved `.nlbl` packages in a configured library dir |
| `load_label` | Load a `.nlbl` → return its schema + token list |
| `render_label` | Render a label (+ optional data row) → preview PNG (base64) |
| `bind_and_preview` | Run a query / accept a row, map to tokens, render preview |
| `print_label` | Print a label to a named printer (single or batch over a query) |
| `create_label` | Author a label from a JSON `LabelDocument` (lets an LLM build labels) |
| `get_capabilities` | Return the capability JSON (§9) — controls, schema, shortcuts |

Also expose the **`LabelDocument` JSON schema** as an MCP resource so an LLM can author valid labels directly.

> This is the natural home for the "Claude MCP agent for the label software" idea — it falls out of the architecture for free, because Core is already UI-agnostic.

---

## 9. Help System

Two artifacts from **one source** (author the capability data once; generate both):

1. **PDF manual** — user-facing documentation in `docs/manual/`. Human reference.
2. **LLM capability JSON** — `docs/capabilities/` — a structured document describing controls, their properties, the `.nlbl` schema, the token system, and keyboard shortcuts. Consumed by:
   - the in-app help/assistant,
   - the MCP server's `get_capabilities` tool and tool descriptions.

Single source of truth keeps the manual, the in-app help, and the MCP tool descriptions from drifting.

---

## 10. Cross-Cutting Decisions

**Licensing (resolved — GPL-v3, free-for-all, no commercial track):**
This project is not being commercialized. The four Niimbot projects (`Niimbot.Net`, `Thermalith.Core`, `Thermalith.App`, `Thermalith.Server`) ship under **GPL-v3**: free for anyone to use, modify, and redistribute; copyleft keeps derivatives open, so there is no proprietary/closed commercial implementation. (Donations come from the external **MIT** `EvilGeniusLabs.DonationWare` NuGet — §4.2, its own repo — which aggregates cleanly into a GPL solution.)
- **No license inherited from the source projects.** `Niimbot.Net` is a *wholly new rebuild* synthesized from studying `niimbluelib` + `niimprint`, not a copy (§5), so we inherit neither upstream's terms and are free to license as we choose. GPL-v3 is a deliberate pick, not a requirement. (A one-time check of each upstream's actual license is good hygiene, but it does not constrain us.)
- **LGPL carve-out — considered and dropped.** LGPL-v3 on the lib would let *closed-source* third-party apps depend on `Niimbot.Net` (GPL-v3 only lets GPL apps use it). That trade only matters if maximizing third-party adoption of the lib outranks pure copyleft. Given the free-for-all / non-commercial priority and the value of simplicity: **GPL-v3 throughout, no carve-out.** Revisit only if a concrete need to let closed apps build on the lib appears.

**Funding model — donationware (a first for an EGL project):**
The product is GPL-v3 / free-for-all (above) and asks for *voluntary* support only, surfaced in-app via `EvilGeniusLabs.DonationWare` (§4.2): a single, opt-in, dismissible donate affordance (Help/About) — **no paywall, no nag loop, no telemetry**, consistent with the local-first goals in §2. GPL + donationware is a coherent pairing: the software is free, support is voluntary. Providers: Ko-fi, PayPal, Stripe Payment Link, and arbitrary custom URLs — whatever the user wants to give through.

**Distribution (later, not core):**
- **Self-contained single-file per-RID** publish via `Properties/PublishProfiles/{rid}.pubxml` (`SelfContained=true`, `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`). For the **Avalonia app keep `PublishTrimmed=false`** — trimming breaks reflection-heavy UI; `PublishReadyToRun=true` on x64, `false` on ARM; **no AOT** (consistent with the global rule). The headless server/CLI can trim more aggressively if desired.
- **RIDs:** `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Orchestrate with `Build.ps1` / `Build.sh` looping profiles into an `artifacts/` tree.
- **macOS `.app` bundle** assembled post-publish in CI (PNG→icns, `Info.plist` template).
- **Versioning:** stamp `<Version>` manually in csproj; trigger releases on `v*` tags. Add **MinVer** only if you want the tag to flow into assembly version automatically.
- **Updater/installer:** out of initial scope (Velopack / MSI / `.deb` / `.dmg` later).

**Testing:**
- Unit-test the protocol encoder against known-good byte sequences captured from `niimbluelib`/`niimprint`.
- Snapshot-test rendering (Verify) — guard WYSIWYG and 1bpp output.
- Round-trip-test `.nlbl` save/load.

**CI:** build + test on all three OSes (the whole point is cross-platform). A single Linux runner can cross-publish every RID (`win`/`linux`/`osx`).

> **CI platform (resolved).** Build and run CI on the self-hosted GitLab (`gitlab.glyphdeck.org`, `eg-projects/` group); **mirror to GitHub and publish public releases there at go-live**. GitLab is the dev/CI home; GitHub is the public release face. The publish-profile + `Build.{ps1,sh}` approach above is CI-agnostic; write the GitLab pipeline against it.

---

## 11. Open Decisions (resolve at scaffold)

1. ~~Solution/product **name**~~ — **RESOLVED:** **Thermalith** ("Open-Source NIIMBOT System"). Product/solution + `Thermalith.*` projects; `Niimbot.Net` keeps the brand nominatively. Trademark-safe; discoverability via the nominative tagline/description.
2. ~~`Niimbot.Net` **license**~~ — **RESOLVED:** GPL-v3 across the four Niimbot projects (DonationWare leaf is MIT), free-for-all, no commercial track; LGPL carve-out considered and dropped — and as a wholly-new rebuild we inherit no upstream license (§5, §10).
3. ~~**Text vs Label** controls~~ — **RESOLVED:** one `text` type, no separate Label; a static caption is just Text with no `{token}` (§6.1.1, §6.2).
4. ~~Transport **ownership** in `Niimbot.Net`~~ — **RESOLVED:** layered — injectable `INiimbotTransport` (dumb byte duplex) + shipped `SerialTransport` default; enumerate-and-probe discovery; client owns framing/queue; designed clean (§5.1).
5. Default **1bpp dithering** algorithm + which "print task versions" to ship for B1.
6. **Secret storage** strategy per OS for DB credentials.
7. ~~**Font portability**~~ — **RESOLVED:** any system TTF selectable, bundle minimal OFL default + Core fallback chain; `.nlbl` stores font name only, no embedding (§6.3).
8. ~~**DPI assumption**~~ — **RESOLVED:** `canvas.dpi` is a real field seeded from the printer profile, not a hardcoded 8 px/mm; renderer must read it (§6.1, §6.3).
9. ~~**Funding model**~~ — **RESOLVED:** donationware; voluntary Ko-fi / PayPal / Stripe-Payment-Link / custom via the UI-agnostic, MIT-licensed `EvilGeniusLabs.DonationWare` NuGet, named for future break-out (§4.2, §10).
10. ~~**Undo model**~~ — **RESOLVED:** snapshot-based (the label doc is small and JSON-serializable) (§6.4).
11. ~~**CI platform / hosting**~~ — **RESOLVED:** build/CI on GitLab (`gitlab.glyphdeck.org`); mirror + public releases to GitHub at go-live (§10).
12. **Donation provider targets** — supply the actual Ko-fi / PayPal.me / Stripe Payment Link URLs at integration time (Phase 6).
13. **Niimbot label-stock catalog** — data-gathering: mine Niimbot's desktop software / website for official label SKUs (size mm + shape) to seed the preset catalog (§6.1.2). Not blocking; can start with a handful of common sizes (e.g. 40×30, 50×30, circular) and grow.
14. **Auto-size consistency across a batch** — with data binding (Phase 4), should `shrink`/`fill` text size each label independently (per-row) or fit once to the longest value so a run looks uniform? Default per-row; add a uniform option if wanted. (§6.2 auto-sizing, §6.5.)
15. **Thermal-accurate preview** (enhancement, later-phase) — paper-tone + burn-colour + dot-gain + density-aware + life-size simulation, optionally hardware-calibrated, to make the preview near-indistinguishable from a real print (§6.3.6). After core prints.

---

## 12. Phased Build Plan (for Claude Code)

**Phase 0 — Spike (prove the transport):**
- `Niimbot.Net` skeleton + `SerialTransport` + minimal print command.
- `tools/print-harness`: open the serial port, send one hardcoded 1bpp bitmap, **get a physical label out of the B1**. Do this before anything else.

**Phase 1 — Protocol library:**
- Full command set, status polling, 1bpp encoder (RLE + print-task versions), B1 profile, unit tests vs captured byte sequences. Publish to a local NuGet feed.

**Phase 2 — Core engine:**
- `LabelDocument` model + controls + SkiaSharp renderer + `.nlbl` serialization + snapshot undo/redo + `ILabelValidator`. Snapshot tests. (No data binding yet.)

**Phase 3 — Avalonia editor:**
- Canvas, inspector, insert/arrange toolbars, printer panel, menus, accelerators (keymap service), MRU, load/save. WYSIWYG via Core renderer.

**Phase 4 — Data binding:**
- Provider factory + Dapper + file connectors, token mapping panel, batch print, serial/time/date advance.

**Phase 5 — API / MCP server:**
- MCP SDK host, tool surface (§8), capability JSON, schema resource.

**Phase 6 — Help + distribution + donationware:**
- Capability JSON → PDF manual + in-app help; per-OS packaging.
- Integrate the external `EvilGeniusLabs.DonationWare` NuGet (§4.2, its own repo) into the App's Help/About. The package is standalone and off the critical path — built/published from its own repo anytime — but wire it in here, late, so it never distracts from getting a label out of the printer.

Each phase is independently testable; Phase 0 de-risks the whole project the same way proving the serial port did before.

## 13. Future Work

Ideas captured for later, deliberately out of the near-term phases above.

### 13.1 Community-shared label database (opt-in)

**Context — the label catalogue is learned locally, not scraped.** Investigation (2026-06) found
NIIMBOT's per-printer *size capabilities* live in a public static file
(`oss-print.niimbot.com/.../devices.json` — printers, default/min/max widths, density, paper types,
margins; mined into our own factual `printers.json`), but the **roll/SKU catalogue** (named stock
with specific sizes, the "Label library" gallery) is a separate, auth/region-gated cloud API bundled
with copyrighted template artwork — no clean public endpoint, and neither `niimbluelib` nor the
`niimblue` editor mirrors it. So the label catalogue is **learned from the user's own rolls**: it
starts empty; when a new RFID roll is detected (or the printer is off), the user fills out the
**whole roll definition** — paper type (gap/black/continuous/transparent/…), width, height, shape,
name, density — which is stored keyed by the roll's RFID barcode/SKU, auto-applied on re-load, and
remembered as the last-used default. No scraping, no images, no IP exposure.

**The future enhancement:** let users **opt in to share** their learned roll definitions to a hosted
API, building an **ongoing, growing public, crowdsourced label database** — turning every user's
cold-start empty list into a warm one over time.

- **Shape (clean-room):** a record of product facts only — `{ barcode, name, paperType, widthMm,
  heightMm, shape, density }`. Our own crowdsourced database, not derived from NIIMBOT's catalogue or
  assets.
- **Push:** when the user defines a roll, offer to share it (anonymous; no PII).
- **Pull:** when a new barcode is detected, optionally query the database to pre-fill the roll form;
  the user still confirms.
- **Local-first, opt-in, off by default.** Never a dependency — the local learned store works fully
  offline; the community DB is purely an enhancement.
- **Home:** `Thermalith.Server` (the §8 API/MCP project), with submit + fetch verbs; hosted on EGL
  infrastructure. Same fetch/cache plumbing as the printer-catalogue update.
- **Quality:** dedup/consensus keyed by barcode (the same SKU converges to one size), plus light
  moderation against junk submissions.
- **License:** the shared dataset under an open data license (e.g. CC0 or ODbL) so it stays free and
  reusable, consistent with the project's GPL/free-for-all stance (§10).
