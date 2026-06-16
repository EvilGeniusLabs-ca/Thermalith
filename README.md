# Thermalith

[![License: GPL-3.0-or-later](https://img.shields.io/badge/License-GPLv3-blue.svg)](LICENSE)

**Open-Source NIIMBOT System** — a local-first, cross-platform label designer and printer driver for NIIMBOT thermal label printers.

Thermalith is a free, FOSS alternative to the vendor's cloud-tethered desktop app: design labels, bind data, and print — entirely on your machine. No account, no cloud, no telemetry.

> **Not affiliated with NIIMBOT.** "NIIMBOT" is a trademark of its respective owner. Thermalith is an independent community project that is *compatible with* NIIMBOT printers; it is not produced, endorsed, or supported by the trademark owner.

## Status

First public release (1.0.0).

- **Printing works on real hardware.** The full design → render → print path is verified end-to-end on the NIIMBOT **B1** and **B4** (203 dpi). Other models resolve their geometry from a 79-printer capability catalogue (built from the vendor's device data); broadening verified driver coverage across the printer family is ongoing, with a D11 next on the test matrix.
- **The editor is usable.** Text, barcode, QR, serial, date/time, shape, line, image, and table elements; on-canvas drag / resize / align / distribute / group, layers, undo/redo, rulers with grid snap, a printable-area guide, click-to-type editing, and light / dark / system themes.
- The binaries are not code-signed yet (see the platform notes below); signing and notarization come in a later release.

## Download

Grab the build for your platform from the [latest release](https://github.com/EvilGeniusLabs-ca/Thermalith/releases/latest). The app is a self-contained single-file binary — nothing to install, not even the .NET runtime. Just download and run.

| Platform | Asset |
|---|---|
| **Windows** | `win-x64` `.zip` |
| **Linux** (Intel/AMD) | `linux-x64` `.tar.gz` |
| **Linux** (ARM, e.g. Raspberry Pi) | `linux-arm64` `.tar.gz` |
| **macOS** (Apple Silicon) | `osx-arm64` `.dmg` |
| **macOS** (Intel) | `osx-x64` `.dmg` |

First launch shows an unsigned-app warning on macOS and Windows — that's expected; see [Running the unsigned macOS build](#running-the-unsigned-macos-build) and the Linux note below. The user manual PDF and the `Niimbot.Net` driver package are attached to each release too.

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

## User manual

A full user manual — a tour of the window, every element type, printing, and keyboard shortcuts, with screenshots — lives in [`Documentation/Manual/`](Documentation/Manual/):

- **Read it online:** start at the [user manual](Documentation/Manual/markdown/00-introduction.md) (Markdown, with screenshots).
- **PDF:** the **Thermalith User Manual** PDF is built in CI and attached to each [release](https://github.com/EvilGeniusLabs-ca/Thermalith/releases) — or build it yourself with `Documentation/Manual/build-pdf.sh`.

## Building

> **Just want to run Thermalith?** You don't need any of this. The published app is a self-contained
> single-file binary — **nothing has to be installed to run it**, not the SDK and not even the .NET
> runtime. Grab the binary for your platform and double-click it. The SDK below is **only** required to
> *build the app yourself from source*.

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

## Connecting a printer on Linux (USB-serial permissions)

On Linux a USB NIIMBOT printer enumerates as a USB CDC serial device — typically `/dev/ttyACM0`
(check with `ls -l /dev/serial/by-id/`). That device node is owned by `root:dialout`, so **your user
must be a member of the `dialout` group** to open it. This is a one-time setup step; Windows and macOS
need no equivalent.

If you are not in the group, the symptom is: the app **scans but does not find the printer** (it shows
up as "no printer", and Connect fails) — the port is there, but opening it is denied. Grant access with:

```bash
sudo usermod -aG dialout $USER
```

Then **fully log out and back in** (or reboot). Group membership is only applied to *new* login
sessions, so the change does not take effect in your current session — and an app launched from the old
session still cannot open the port until you re-login.

Verify it worked:

```bash
groups | tr ' ' '\n' | grep -x dialout   # should print: dialout
ls -l /dev/ttyACM0                        # group should be 'dialout'
```

> **Distro note:** the serial group is `dialout` on Debian/Ubuntu (and this project's reference setup).
> Some distributions (e.g. Arch) use `uucp` instead. If `/dev/ttyACM0` shows a different group owner in
> the `ls -l` above, add yourself to *that* group instead.

## Running the unsigned macOS build

Current releases are **not yet code-signed or notarized** (signing is being set up). Until that lands, macOS Gatekeeper
will refuse to open the downloaded build with a message like *"Thermalith" cannot be opened because the
developer cannot be verified.* This is expected for an unsigned binary — nothing is wrong with the
download. macOS 15 (Sequoia) removed the old Control-click → **Open** bypass, so use one of these
instead:

- **System Settings route (no terminal).** Double-click the app once and dismiss the warning. Then open
  **System Settings → Privacy & Security**, scroll to the **Security** section, and click **Open Anyway**
  next to the Thermalith message. Confirm once more — macOS then remembers the choice.
- **Terminal route.** Strip the quarantine flag macOS attaches to downloaded files, then run it:

  ```bash
  xattr -dr com.apple.quarantine /path/to/Thermalith
  chmod +x /path/to/Thermalith        # only needed for the bare single-file binary
  ```

> **Why this is needed:** Apple only suppresses the warning for apps signed with a Developer ID and
> notarized. **Signing/notarization is actively in progress** — once it ships, future macOS releases
> open with a normal double-click and these steps go away. Earlier downloads are unaffected either way.

## Platforms

Windows, macOS, and Linux. USB-serial printers first; Bluetooth (BLE) later, behind the same transport interface.

## License

The Thermalith application and libraries are **GPL-3.0-or-later** — free to use, modify, and redistribute.

Thermalith is **donationware**: free for everyone, with a voluntary in-app tip jar for anyone who'd like to support development.
