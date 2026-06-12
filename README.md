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

### Prerequisite: the .NET 10 SDK (not just the runtime)

You **must** have the **.NET 10 SDK** installed. This is non-negotiable — nothing here builds on .NET 8, .NET 9, or the older "Core" SDKs, and the **runtime alone is not enough**: building requires the full **SDK**.

- Download: <https://dotnet.microsoft.com/download/dotnet/10.0> (pick **SDK**, not Runtime, for your OS).
- Verify it's installed and on your PATH:

  ```bash
  dotnet --version
  ```

  The output must start with `10.` (e.g. `10.0.204` or higher). If it shows `8.x`, `9.x`, or `command not found`, stop and install the .NET 10 SDK first — every step below will fail otherwise.

The exact SDK floor is pinned in [`global.json`](global.json) (`10.0.204`, rolling forward to newer 10.0 feature bands). If your installed SDK is older than that, `dotnet` will refuse to build until you update.

### Develop / test (the normal loop)

From the repository root:

```bash
dotnet restore     # pull NuGet dependencies
dotnet build       # compile the whole solution
dotnet test        # run the test suite (78 tests)
```

Run the desktop editor:

```bash
dotnet run --project src/Thermalith.App
```

### Produce distributable binaries (the build scripts)

Two scripts at the repository root publish **self-contained, single-file** binaries — one standalone executable per platform that **needs no .NET installed on the end user's machine to run** (you only need the SDK to *build* it). Output goes to the gitignored `artifacts/` folder.

Run from the **repository root**:

- **Windows / PowerShell** — `Build.ps1`
- **Linux / macOS / Git Bash** — `Build.sh`

| What you want | PowerShell | Bash |
|---|---|---|
| Build **all** platforms | `./Build.ps1` | `./Build.sh` |
| Build **one** platform | `./Build.ps1 -Targets win-x64` | `./Build.sh win-x64` |
| Build **several** platforms | `./Build.ps1 -Targets win-x64,linux-x64` | `./Build.sh win-x64 linux-x64` |

With no target argument, both scripts build **all five** supported runtimes. Any one OS can cross-build for all of them — you do not need a Mac to produce the macOS binaries:

| Target (RID) | Platform |
|---|---|
| `win-x64` | Windows (64-bit Intel/AMD) |
| `linux-x64` | Linux (64-bit Intel/AMD) |
| `linux-arm64` | Linux (64-bit ARM — e.g. Raspberry Pi) |
| `osx-arm64` | macOS (Apple Silicon — M1 and later) |
| `osx-x64` | macOS (Intel) |

Each run:

1. Packs the `Niimbot.Net` library as a NuGet package → `artifacts/nupkgs/Niimbot.Net.<version>.nupkg`.
2. Publishes `Thermalith.App` for every requested target → `artifacts/Thermalith.App/<RID>/`, producing a single executable named `Thermalith` (`Thermalith.exe` on Windows).

The output directory for each target is wiped before publishing, so each build is clean. If any target fails the script prints which one and exits with a non-zero status; otherwise it ends with `All platforms built successfully.`

## Platforms

Windows, macOS, and Linux. USB-serial printers first; Bluetooth (BLE) later, behind the same transport interface.

## License

The Thermalith application and libraries are **GPL-3.0-or-later** — free to use, modify, and redistribute.

Thermalith is **donationware**: free for everyone, with a voluntary in-app tip jar for anyone who'd like to support development.
