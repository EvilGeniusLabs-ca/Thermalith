#!/usr/bin/env bash
# Build.sh - pack the Niimbot.Net NuGet and publish Thermalith.App for one or more target platforms.
# Run from the repo root (where Thermalith.slnx lives). Output goes to artifacts/.
#
# Usage:
#   ./Build.sh                                # all platforms
#   ./Build.sh win-x64                        # single platform
#   ./Build.sh win-x64 linux-x64              # multiple platforms

set -euo pipefail

declare -A PROJECTS=(
    ["Thermalith.App"]="src/Thermalith.App/Thermalith.App.csproj"
)

# Ordered project names (bash associative arrays don't preserve order)
PROJECT_ORDER=("Thermalith.App")

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
for project_name in "${PROJECT_ORDER[@]}"; do
    project_path="${PROJECTS[$project_name]}"

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
            esac
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
