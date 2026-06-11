# Thermalith

**Open-Source NIIMBOT System** — a local-first, cross-platform label designer and printer driver for NIIMBOT thermal label printers.

Thermalith is a free, FOSS alternative to the vendor's cloud-tethered desktop app: design labels, bind data, and print — entirely on your machine. No account, no cloud, no telemetry.

> **Not affiliated with NIIMBOT.** "NIIMBOT" is a trademark of its respective owner. Thermalith is an independent community project that is *compatible with* NIIMBOT printers; it is not produced, endorsed, or supported by the trademark owner.

## Status

Past the scaffold and in active development; not yet at a tagged release.

- **Printing works on real hardware.** The full design → render → print path is verified end-to-end on a NIIMBOT **B1** (203 dpi). Other models resolve their geometry from a 77-printer capability catalogue (built from the vendor's device data); broadening verified driver coverage across the printer family is in progress, with a B4 and a D11 joining the test matrix.
- **The editor is usable.** Text, barcode, QR, serial, date/time, shape, line, image, and table elements; on-canvas drag / resize / align / distribute / group, layers, undo/redo, rulers with grid snap, a printable-area guide, click-to-type editing, and light / dark / system themes.
- Currently in a usability/cleanup pass ahead of Release 1 — scope and the launch / CI / signing plan live in [`Documentation/release-plan.md`](Documentation/release-plan.md).

## What's here

One solution, factored so each piece is independently useful:

| Project | What it is |
|---|---|
| `Niimbot.Net` | Standalone protocol + transport library for NIIMBOT printers (its own NuGet). |
| `Thermalith.Core` | UI-agnostic label engine: document model, 5-stage SkiaSharp rendering, `.nlbl` storage, validation, data binding, printer catalogue. |
| `Thermalith.App` | Avalonia cross-platform desktop editor — the main application. |
| `Thermalith.Server` | Headless API / MCP server over the same engine (scaffold; post-Release-1). |

Also in the tree: `tools/print-harness` (a console probe/print tool for hardware bring-up) and `tests/` (78 tests across Niimbot.Net, Thermalith.Core, and Thermalith.Server).

Design docs live in [`Documentation/`](Documentation/): start with the [build spec](Documentation/thermalith-build-spec.md), the [`.nlbl` format spec](Documentation/label-json-spec.md), and the [roadmap](Documentation/roadmap.md).

## Building

Requires the **.NET 10 SDK**.

```bash
dotnet restore
dotnet build
dotnet test
```

Run the desktop editor:

```bash
dotnet run --project src/Thermalith.App
```

## Platforms

Windows, macOS, and Linux. USB-serial printers first; Bluetooth (BLE) later, behind the same transport interface.

## License

The Thermalith application and libraries are **GPL-3.0-or-later** — free to use, modify, and redistribute.

Thermalith is **donationware**: free for everyone, with a voluntary in-app tip jar (planned, via the separately-published MIT-licensed `EvilGeniusLabs.DonationWare` helper) for anyone who'd like to support development.
