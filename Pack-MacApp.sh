#!/usr/bin/env bash
# Pack-MacApp.sh - assemble a macOS .app bundle from a published osx-* build.
#
# dotnet publish produces a bare Unix executable; macOS needs a .app bundle
# (Info.plist + Contents/MacOS + Contents/Resources) before it shows an icon,
# launches from Finder, or sits in /Applications. This wraps that publish output.
#
# Usage:
#   ./Pack-MacApp.sh <publish-dir>            # writes <publish-dir>/../Thermalith.app
#   ./Pack-MacApp.sh artifacts/Thermalith.App/osx-arm64
#
# Called automatically by Build.sh for osx-* targets.

set -euo pipefail

PUBLISH_DIR="${1:?usage: Pack-MacApp.sh <publish-dir>}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APP_NAME="Thermalith"
EXECUTABLE="Thermalith"              # matches <AssemblyName> in the csproj
BUNDLE_ID="ca.evilgeniuslabs.thermalith"
ICON_SRC="$REPO_ROOT/Assets/Icons/thermalith.icns"
MIN_MACOS="11.0"

# Version: single source of truth is <Version> in the app csproj.
CSPROJ="$REPO_ROOT/src/Thermalith.App/Thermalith.App.csproj"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ" | head -1)"
VERSION="${VERSION:-0.1.0}"

if [ ! -x "$PUBLISH_DIR/$EXECUTABLE" ]; then
    echo "ERROR: $PUBLISH_DIR/$EXECUTABLE not found (publish first)." >&2
    exit 1
fi
if [ ! -f "$ICON_SRC" ]; then
    echo "ERROR: icon not found at $ICON_SRC" >&2
    exit 1
fi

APP_DIR="$PUBLISH_DIR/../$APP_NAME.app"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"

# Payload: the single-file self-contained binary (native libs are bundled inside).
cp "$PUBLISH_DIR/$EXECUTABLE" "$APP_DIR/Contents/MacOS/$EXECUTABLE"
chmod +x "$APP_DIR/Contents/MacOS/$EXECUTABLE"

# Icon
cp "$ICON_SRC" "$APP_DIR/Contents/Resources/thermalith.icns"

# Info.plist
cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>                  <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>           <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>            <string>$BUNDLE_ID</string>
    <key>CFBundleExecutable</key>            <string>$EXECUTABLE</string>
    <key>CFBundleIconFile</key>              <string>thermalith</string>
    <key>CFBundleVersion</key>               <string>$VERSION</string>
    <key>CFBundleShortVersionString</key>    <string>$VERSION</string>
    <key>CFBundlePackageType</key>           <string>APPL</string>
    <key>CFBundleInfoDictionaryVersion</key> <string>6.0</string>
    <key>LSMinimumSystemVersion</key>        <string>$MIN_MACOS</string>
    <key>LSApplicationCategoryType</key>     <string>public.app-category.graphics-design</string>
    <key>NSHighResolutionCapable</key>       <true/>
</dict>
</plist>
PLIST

# PkgInfo (legacy but conventional)
printf 'APPL????' > "$APP_DIR/Contents/PkgInfo"

# Ad-hoc sign so the bundle (Info.plist + Resources) is sealed and launches from
# Finder on Apple Silicon. Not a Developer ID signature — users still right-click
# > Open the first time, or `xattr -dr com.apple.quarantine` after download.
if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$APP_DIR" >/dev/null 2>&1 \
        && echo "    ad-hoc signed" \
        || echo "    WARNING: ad-hoc codesign failed (bundle still usable locally)"
fi

# Normalize the path for display
RESOLVED="$(cd "$(dirname "$APP_DIR")" && pwd)/$APP_NAME.app"
echo "OK: assembled $RESOLVED (v$VERSION)"
