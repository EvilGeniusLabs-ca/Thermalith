#!/usr/bin/env bash
# Build the Thermalith User Manual PDF from the chapter markdown.
#
# Requires: pandoc + xelatex (Debian/Ubuntu/WSL:
#   sudo apt install texlive-xetex texlive-fonts-recommended texlive-latex-extra)
# Fonts (Lato body + DejaVu Sans glyph fallback) are BUNDLED in ./fonts and loaded
# via OSFONTDIR below — no system font install is needed, on any machine or in CI.
# To use a different body font, drop its .ttf in ./fonts and change MAINFONT.
#
# Run from anywhere: ./build-pdf.sh   →   writes pdf/Thermalith-User-Manual.pdf
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/markdown"   # run pandoc here so assets/... image paths resolve

command -v pandoc  >/dev/null 2>&1 || { echo "ERROR: pandoc not found."; exit 1; }
command -v xelatex >/dev/null 2>&1 || { echo "ERROR: xelatex not found — install texlive-xetex."; exit 1; }

mkdir -p ../pdf

# Load the repo-bundled fonts (no system install). XeTeX also searches OSFONTDIR.
export OSFONTDIR="$SCRIPT_DIR/fonts"
MAINFONT="Lato"                 # modern sans body font, bundled in ./fonts

# Chapters in numeric order (00-, 01-, ...).
mapfile -t CHAPTERS < <(ls -1 [0-9][0-9]-*.md | sort)
[ ${#CHAPTERS[@]} -gt 0 ] || { echo "ERROR: no NN-*.md chapter files found."; exit 1; }
echo "Building from: ${CHAPTERS[*]}"

pandoc \
  --from=gfm \
  --pdf-engine=xelatex \
  --toc --toc-depth=2 \
  --number-sections \
  -V documentclass=report \
  -V geometry:"top=2cm, bottom=3cm, left=2.2cm, right=2.2cm" \
  -V mainfont="$MAINFONT" \
  -V linkcolor=blue -V urlcolor=blue \
  --include-in-header=../header.tex \
  --include-before-body=../cover.tex \
  "${CHAPTERS[@]}" \
  -o ../pdf/Thermalith-User-Manual.pdf

echo "Wrote $SCRIPT_DIR/pdf/Thermalith-User-Manual.pdf"
