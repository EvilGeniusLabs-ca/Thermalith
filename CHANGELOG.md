# Changelog

All notable changes to Thermalith are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) once it reaches 1.0.

## [Unreleased]

### Fixed
- **Labels printed about a quarter too small on 300 dpi printers.** Thermalith read each printer's
  resolution from the wrong field in NIIMBOT's device list, so every 300 dpi model was recorded as a
  "229 dpi" printer that does not exist, with a print head narrower than the real one. Everything sent
  to those printers came out at roughly 76% of its designed size. 22 of the 79 catalogued models were
  affected, among them the **B21 Pro**, **B1 Pro**, **B2 Pro**, **B32**, **D11 Pro**, **C1**, **P1** and
  the **M2 / M3 / EP** families. The 203 dpi printers - including the B1, B4 and B3S - were never
  affected, and their settings are unchanged. Reported for the B21 Pro (#17).

## [1.1.0] - 2026-07-12

### Added
- **Data merge / variable data** — print a batch of labels from a CSV, one label per row. Bind any
  text, barcode, or QR value to a column with a `{"Column name"}` or `{3}` (column-number) token; the
  new **Data Merge** menu loads the file and its **Columns** entry inserts the right token for the field
  you're editing. Auto-size text grows to fit each row's value. **Preview Merge Data** steps through the
  rows on the canvas so you can see the real labels, and **Print Merge** prints them — it shows how many
  labels the run needs, and on RFID rolls won't start more than the roll holds (it prints what fits and
  lets you reprint the rest after a roll change). A progress bar lets you cancel part-way through.
- **Insertable clip art** — a browsable, searchable palette of pictograms (from the bundled Material
  Design Icons set) on the Insert palette. Click one to drop it on the label. Clip art is vector, so it
  prints crisp at any size on a thermal printer.

### Changed
- The toolbar and inspector now use one consistent Material Design icon set throughout (alignment, the
  Insert palette, eye/lock, and bold/italic/underline), replacing the older hand-drawn glyphs. The
  eye and lock icons now change to show the current state.

### Fixed
- The selection box and alignment now match what's actually drawn. The dashed box hugs serial numbers,
  dates, and overflowing text (instead of a fixed box that the text spilled out of), follows a rotated
  element, and **Align** / **Distribute** line up the visible edges. Auto-size text now shows resize
  handles — grab one to switch it to a fixed, word-wrapping box (no need to hunt for the checkbox).

## [1.0.2] - 2026-07-09

### Added
- Connection logging for printer troubleshooting. **Help ▸ Connection Logging** records the connection
  conversation — ports found, whether each opened, bytes sent, and what came back — to a timestamped log
  file under the app-data `logs/` folder; **Help ▸ Open Log Folder…** reveals it. Can also be armed before
  launch with `--debug` or `THERMALITH_DEBUG=1`. The log holds only local device information, nothing from
  label designs.
- `Niimbot.Net` 1.2.0: `NiimbotTrace`, an opt-in, dependency-free diagnostic sink underlying the above, so
  other integrations can capture the same trace.

## [1.0.1] - 2026-06-17

### Added
- Verified support for the NIIMBOT D11 / D11_H — the compact 300 dpi side-fed label maker. It prints at
  the correct size and position, one label per copy, and is the first verified side-fed D-series model
  (alongside the B1 and B4).
- Side-fed printers now auto-rotate a fresh canvas to match the printer's narrow head on connect, so the
  design fits the printable width instead of being cropped. An existing or already-rotated design is left
  as you set it.

### Fixed
- The D11_H printed at the wrong scale. Its catalogue resolution and print width were wrong — it is
  300 dpi with a 142-pixel head — so content now rasterises at the right size and fills the label.
- The D11_H printed several labels for a single copy. It uses a different print sequence (D110M-v4) than
  the other D-series models; Thermalith now drives that sequence correctly, so a 1-copy job prints exactly
  one label.

## [1.0.0] - 2026-06-16

First public release.

### Added
- Label orientation: rotate-left / rotate-right buttons that turn the label between portrait and
  landscape. The canvas reshapes and the print output is rotated to match; your placed controls keep
  their own angle.
- A user-set soft safe margin (per label, in the canvas properties) that draws an edge guide to keep
  content clear of a printer's skew. It's a guide only, nothing is cropped to it, and it's remembered as
  the default for new labels.
- B4 printer support (the 4-inch, 104 mm shipping-label model), verified on hardware.
- Broader NIIMBOT model support: printer profiles are now derived from the device catalogue, so a
  connected model resolves its real print width, dpi, and density automatically.
- Tooltips on every inspector property field.
- About dialog: "Check for updates" and "Request beta access" links.
- A first user manual (PDF), built in CI.

### Fixed
- Tall, mostly-blank labels printed only the top strip and stopped. Row runs longer than 255 lines were
  truncating; they now split correctly so a full 148 mm label prints end to end.
- Reconnecting or re-detecting a printer no longer snaps a rotated canvas back to un-rotated.
- Several B4 bring-up issues: connection, fit-to-window on large labels, and USB vs Bluetooth labelling in
  the port list.

## [0.5.0-beta] - 2026-06-12

Initial public beta: the cross-platform desktop label designer (Windows, macOS, Linux) with the
WYSIWYG editor, text / barcode / QR / shape / line / image / table / serial / date-time elements, the
`.nlbl` file format, and the verified B1 print path over USB and Bluetooth.

[Unreleased]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.0.2...v1.1.0
[1.0.2]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v0.5.0-beta...v1.0.0
[0.5.0-beta]: https://github.com/EvilGeniusLabs-ca/Thermalith/releases/tag/v0.5.0-beta
