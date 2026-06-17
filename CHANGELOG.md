# Changelog

All notable changes to Thermalith are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) once it reaches 1.0.

## [Unreleased]

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

[Unreleased]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v0.5.0-beta...v1.0.0
[0.5.0-beta]: https://github.com/EvilGeniusLabs-ca/Thermalith/releases/tag/v0.5.0-beta
