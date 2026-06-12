#!/usr/bin/env bash
# Package-Platform.sh - build a distributable archive for one published RID.
#
# Produces a platform-native package INSIDE the publish dir (so each platform's
# deliverable sits in its own folder):
#   win-*   -> Thermalith-<ver>-<rid>.zip      (zip of the published payload)
#   linux-* -> Thermalith-<ver>-<rid>.tar.gz   (tarball; preserves the exec bit)
#   osx-*   -> Thermalith-<ver>-<rid>.dmg       (disk image of Thermalith.app; macOS only)
#
# .pdb debug symbols are excluded from the package. The archive is built in a
# temp dir and moved in last, so it never includes itself.
#
# Usage:
#   ./Package-Platform.sh <rid> <publish-dir>
#   ./Package-Platform.sh osx-arm64 artifacts/thermalith/osx-arm64
#
# Called automatically by Build.sh after each successful publish.

set -euo pipefail

RID="${1:?usage: Package-Platform.sh <rid> <publish-dir>}"
PUBLISH_DIR="${2:?usage: Package-Platform.sh <rid> <publish-dir>}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Version: single source of truth is <Version> in the app csproj.
CSPROJ="$REPO_ROOT/src/Thermalith.App/Thermalith.App.csproj"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ" | head -1)"
VERSION="${VERSION:-0.1.0}"

BASENAME="Thermalith-$VERSION-$RID"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

case "$RID" in
    win-*)
        archive="$BASENAME.zip"
        ( cd "$PUBLISH_DIR" && zip -qr "$TMP/$archive" . -x '*.pdb' )
        mv "$TMP/$archive" "$PUBLISH_DIR/"
        echo "    packaged $PUBLISH_DIR/$archive"
        ;;
    linux-*)
        archive="$BASENAME.tar.gz"
        tar --exclude='*.pdb' -czf "$TMP/$archive" -C "$PUBLISH_DIR" .
        mv "$TMP/$archive" "$PUBLISH_DIR/"
        echo "    packaged $PUBLISH_DIR/$archive"
        ;;
    osx-*)
        # The .dmg wraps the Thermalith.app bundle (not the bare binary). hdiutil is
        # macOS-only, so skip the dmg when cross-building — the .app is still produced.
        if [ "$(uname -s)" != "Darwin" ]; then
            echo "    Skipping .dmg (not running on macOS)"
            exit 0
        fi
        if [ ! -d "$PUBLISH_DIR/Thermalith.app" ]; then
            echo "    Skipping .dmg (no Thermalith.app — run Pack-MacApp.sh first)"
            exit 0
        fi
        archive="$BASENAME.dmg"
        # Stage the .app beside an /Applications symlink for drag-to-install UX.
        stage="$TMP/stage"
        mkdir -p "$stage"
        cp -R "$PUBLISH_DIR/Thermalith.app" "$stage/Thermalith.app"
        ln -s /Applications "$stage/Applications"
        hdiutil create -quiet -volname "Thermalith $VERSION" \
            -srcfolder "$stage" -ov -format UDZO "$TMP/$archive"
        mv "$TMP/$archive" "$PUBLISH_DIR/"
        echo "    packaged $PUBLISH_DIR/$archive"
        ;;
    *)
        echo "    No packaging rule for RID '$RID' — skipped"
        ;;
esac
