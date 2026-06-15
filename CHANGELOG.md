# Changelog

All notable changes to Thermalith are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html) once it reaches 1.0.

## [Unreleased]

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

[Unreleased]: https://github.com/EvilGeniusLabs-ca/Thermalith/compare/v0.5.0-beta...HEAD
[0.5.0-beta]: https://github.com/EvilGeniusLabs-ca/Thermalith/releases/tag/v0.5.0-beta
