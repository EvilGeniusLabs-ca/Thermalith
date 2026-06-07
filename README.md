# Thermalith

**Open-Source NIIMBOT System** — a local-first, cross-platform label designer and printer driver for NIIMBOT thermal label printers.

Thermalith is a free, FOSS alternative to the vendor's cloud-tethered desktop app: design labels, bind data, and print — entirely on your machine. No account, no cloud, no telemetry.

> **Not affiliated with NIIMBOT.** "NIIMBOT" is a trademark of its respective owner. Thermalith is an independent community project that is *compatible with* NIIMBOT printers; it is not produced, endorsed, or supported by the trademark owner.

## What's here

One solution, factored so each piece is independently useful:

| Project | What it is |
|---|---|
| `Niimbot.Net` | Standalone protocol + transport library for NIIMBOT printers (NuGet). |
| `Thermalith.Core` | UI-agnostic label engine: document model, rendering, `.nlbl` storage, data binding. |
| `Thermalith.App` | Avalonia cross-platform desktop editor — the main application. |
| `Thermalith.Server` | Headless API / MCP server over the same engine. |
| `EvilGeniusLabs.DonationWare` | Small, reusable donation / tip-jar helper (NuGet). |

Design docs live in [`Documentation/`](Documentation/): the [build spec](Documentation/thermalith-build-spec.md) and the [`label.json` format spec](Documentation/label-json-spec.md).

## Status

Early scaffold. The solution, projects, and skeletons are in place; implementation follows the phased plan in the build spec (Phase 0 = prove the serial transport against a real printer).

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

The Thermalith application and libraries are **GPL-3.0-or-later** — free to use, modify, and redistribute. The `EvilGeniusLabs.DonationWare` helper is **MIT**.

Thermalith is **donationware**: free for everyone, with a voluntary in-app tip jar if you would like to support development.
