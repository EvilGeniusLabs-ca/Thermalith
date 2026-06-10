# Thermalith — project guide

Open-source NIIMBOT thermal-label designer + driver. Cross-platform Avalonia / .NET 10, GPL-3.0. The
B1 print path is verified on real hardware. Currently mid **usability/cleanup pass** on the editor.

**Continuing the cleanup work: start at `Documentation/worklist.md` §I** (the active punch-list).

## Where things are

- `src/Niimbot.Net` — printer protocol + transport (its own NuGet, GPL). Pure: no UI/render/data.
- `src/Thermalith.Core` — label engine: model, 5-stage SkiaSharp render, `.nlbl` serialize, validation,
  catalog. No UI.
- `src/Thermalith.App` — the Avalonia desktop editor.
- `src/Thermalith.Server` — API/MCP scaffold (Phase 5, stub — not R1).
- `tests/` — Niimbot.Net / Thermalith.Core / Thermalith.Server (**78 tests**).
- `tools/print-harness` — console probe/print tool for hardware.
- `EvilGeniusLabs.DonationWare` is NOT here — it was extracted to its own repo at
  `d:\Projects\EvilGeniusLabs.DonationWare` (MIT NuGet). Thermalith only consumes it (roadmap).

## Docs (read on demand)

- `Documentation/worklist.md` — **active backlog; cleanup queue is §I.**
- `Documentation/roadmap.md` — future / wishlist (clip-art, i18n, data-merge, MCP, help, community DB).
- `Documentation/release-plan.md` — release / launch / CI / signing pipeline + Release-1 scope.
- `Documentation/worklist-done.md` — completed record.
- `Documentation/reference-data.md` — endpoints / RFID / devices.json facts.
- `Documentation/thermalith-build-spec.md` — canonical §-numbered design. `label-json-spec.md` — `.nlbl` schema.

## Build / test / verify

- Build the app: `dotnet build src/Thermalith.App/Thermalith.App.csproj`.
- **The app is often running** (Richard tests live) → file-copy lock errors `MSB3021`/`MSB3027`
  ("being used by another process") are EXPECTED and harmless. Real failures are compile errors:
  `dotnet build … 2>&1 | grep -iE "error (CS|AVLN|XAMLIL|XFC)"` must print nothing.
- Tests: `dotnet test` — 78 pass, 0 skip. Render tests are SkiaSharp snapshots (Verify); baselines were
  generated on Windows.
- No GUI from the agent side → "launch smoke" = `timeout 7 dotnet run … --no-build` and grep stderr for
  exceptions. **Visual correctness is Richard's hands-on check**, not the agent's.

## Conventions established (match these)

- **Numeric inspector fields are `NumericUpDown`** (global style in `App.axaml`: spinners + Minimum). The
  `NumericTextConverter` (`Num`) is now used ONLY by `RollDialog`.
- **Positioning is decimal-mm** (reverted from whole-mm 2026-06-10): `EditorViewModel.Snap` rounds to
  0.1 mm (or the grid pitch when snap is on); X/Y/W/H are `0.#`-format NumericUpDowns with 0.5 steps,
  Rotation stays integer. Model stays mm (portable); the printer renders at ~0.125 mm/dot (203 dpi) —
  **B1 ≈ 8 dots per mm** (203/25.4 = 7.99), so 1 mm of placement ≈ 8 printer dots.
- **Edit shortcuts guarded**: `TextAwareCommand` makes Delete/Cut/Copy/Paste/Dup/Undo/Redo edit the
  focused text field instead of the document when a text box has focus.
- **Icons**: Material Design Icons via `Material.Icons.Avalonia` (`<mi:MaterialIcon Kind="…"/>`) going
  forward; some hand-drawn `StreamGeometry` glyphs remain (full migration tracked in roadmap).
- Inspector section headers use the `TextBlock.section` style.
- **No `---` horizontal rules in markdown.** Commit messages: **Richard Barnes only, no `Co-Authored-By`
  trailer**. Commit AND push together.

## Working rhythm

- Richard does the hands-on testing, collects a batch of findings, and hands over a list → work it
  top-to-bottom, commit+push per coherent chunk. **Interrupt-worthy:** crashes / data-loss /
  build-breaks. Everything else batches.
- Delegating mechanical, isolatable, or read-only work to background subagents is fine (keeps context
  lean).
- Three items need **discussion before building**: drag-handle min-size, the table cluster, data-merge.
