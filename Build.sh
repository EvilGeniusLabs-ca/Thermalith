#!/usr/bin/env bash
# Build.sh - pack the Niimbot.Net NuGet and publish Thermalith.App for one or more target platforms.
# Run from the repo root (where Thermalith.slnx lives). Output goes to artifacts/.
#
# Usage:
#   ./Build.sh                                # all platforms
#   ./Build.sh win-x64                        # single platform
#   ./Build.sh win-x64 linux-x64              # multiple platforms

set -euo pipefail

# "name:csproj" entries. name = artifact folder under artifacts/ (must stay
# "thermalith", NOT "Thermalith.App" — a folder ending in .App is read as a
# .app bundle by case-insensitive macOS and is confusing on Linux). Indexed
# array, not "declare -A": macOS ships bash 3.2, which has no associative arrays.
PROJECTS=(
    "thermalith:src/Thermalith.App/Thermalith.App.csproj"
)

DEFAULT_TARGETS=(
    "win-x64"
    "linux-x64"
    "linux-arm64"
    "osx-arm64"
    "osx-x64"
)

# -- NuGet packages -----------------------------------------------------------
NUGET_PROJECTS=(
    "Niimbot.Net:src/Niimbot.Net/Niimbot.Net.csproj"
)
NUPKG_DIR="artifacts/nupkgs"

if [ "$#" -gt 0 ]; then
    TARGET_PROFILES=("$@")
else
    TARGET_PROFILES=("${DEFAULT_TARGETS[@]}")
fi

FAILED=()

# -- Pack NuGet packages ------------------------------------------------------
mkdir -p "$NUPKG_DIR"

for entry in "${NUGET_PROJECTS[@]}"; do
    nuget_name="${entry%%:*}"
    nuget_path="${entry##*:}"

    echo ""
    echo "=== Packing $nuget_name ==="
    if dotnet pack "$nuget_path" -c Release -o "$NUPKG_DIR"; then
        echo "OK: $nuget_name packed"
    else
        echo "FAILED: $nuget_name pack"
        FAILED+=("$nuget_name/nupkg")
    fi
done

# -- Publish apps -------------------------------------------------------------
for entry in "${PROJECTS[@]}"; do
    project_name="${entry%%:*}"
    project_path="${entry##*:}"

    echo ""
    echo "=== Building $project_name ==="

    for target in "${TARGET_PROFILES[@]}"; do
        # Clean output folder before publishing
        out_dir="artifacts/$project_name/$target"
        if [ -d "$out_dir" ]; then
            echo "    Cleaning $out_dir ..."
            rm -rf "$out_dir"
        fi

        echo ""
        echo "==> Publishing $project_name - $target ..."
        if dotnet publish "$project_path" -p:PublishProfile="$target"; then
            echo "OK: $project_name - $target"

            # Linux desktop integration: ship a .desktop entry + icon alongside the
            # binary so the artifact can be installed (or wrapped in an AppImage).
            # The ELF binary itself can't carry an icon the way a Windows .exe does.
            case "$target" in
                linux-*)
                    echo "    Adding Linux desktop entry + icon ..."
                    cp Assets/Icons/thermalith.desktop "$out_dir/thermalith.desktop"
                    cp Assets/Icons/thermalith-256.png  "$out_dir/thermalith.png"
                    ;;
                osx-*)
                    # macOS needs a .app bundle (Info.plist + icon) to launch from
                    # Finder and live in /Applications; the bare binary won't do.
                    # Only assemblable on macOS (needs codesign); skip elsewhere.
                    if [ "$(uname -s)" = "Darwin" ]; then
                        echo "    Assembling macOS .app bundle ..."
                        ./Pack-MacApp.sh "$out_dir"
                    else
                        echo "    Skipping .app bundle (not running on macOS)"
                    fi
                    ;;
            esac

            # Bundle this platform's payload into a distributable package
            # (zip / tar.gz / dmg) inside its own RID folder.
            echo "    Packaging $target ..."
            ./Package-Platform.sh "$target" "$out_dir"
        else
            echo "FAILED: $project_name - $target"
            FAILED+=("$project_name/$target")
        fi
    done
done

echo ""
if [ "${#FAILED[@]}" -eq 0 ]; then
    echo "All platforms built successfully."
else
    echo "Failed: ${FAILED[*]}"
    exit 1
fi
