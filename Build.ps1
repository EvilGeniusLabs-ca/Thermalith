# Build.ps1 - pack the Niimbot.Net NuGet and publish Thermalith.App for one or more target platforms.
# Run from the repo root (where Thermalith.slnx lives). Output goes to artifacts/.
#
# Usage:
#   ./Build.ps1                                # all platforms
#   ./Build.ps1 -Targets win-x64              # single platform
#   ./Build.ps1 -Targets win-x64,linux-x64    # multiple platforms

param(
    [string[]] $Targets = @()
)

$ErrorActionPreference = "Stop"

# -- NuGet packages -----------------------------------------------------------
$NuGetProjects = @(
    @{ Name = "Niimbot.Net"; Path = "src/Niimbot.Net/Niimbot.Net.csproj" }
)
$NupkgDir = "artifacts/nupkgs"

# -- Publishable apps ---------------------------------------------------------
# Name = artifact folder name under artifacts/ (must stay "thermalith", NOT
# "Thermalith.App" — a folder ending in .App is read as a .app bundle by
# case-insensitive macOS and is confusing on Linux). Path = source csproj.
$Projects = @(
    @{ Name = "thermalith"; Path = "src/Thermalith.App/Thermalith.App.csproj" }
)

$DefaultTargets = @(
    "win-x64",
    "linux-x64",
    "linux-arm64",
    "osx-arm64",
    "osx-x64"
)

$TargetProfiles = if ($Targets.Count -gt 0) { $Targets } else { $DefaultTargets }
$Failed = @()

# -- Pack NuGet packages ------------------------------------------------------
if (!(Test-Path $NupkgDir)) { New-Item -ItemType Directory -Path $NupkgDir | Out-Null }

foreach ($nuget in $NuGetProjects) {
    Write-Host ""
    Write-Host "=== Packing $($nuget.Name) ===" -ForegroundColor White
    dotnet pack $nuget.Path -c Release -o $NupkgDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $($nuget.Name) pack" -ForegroundColor Red
        $Failed += "$($nuget.Name)/nupkg"
        continue
    }
    Write-Host "OK: $($nuget.Name) packed" -ForegroundColor Green
}

# -- Publish apps -------------------------------------------------------------
foreach ($project in $Projects) {
    Write-Host ""
    Write-Host "=== Building $($project.Name) ===" -ForegroundColor White

    foreach ($target in $TargetProfiles) {
        # Clean output folder before publishing
        $outDir = "artifacts/$($project.Name)/$target"
        if (Test-Path $outDir) {
            Write-Host "    Cleaning $outDir ..." -ForegroundColor DarkGray
            Get-ChildItem $outDir | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        }

        Write-Host ""
        Write-Host "==> Publishing $($project.Name) - $target ..." -ForegroundColor Cyan
        dotnet publish $project.Path -p:PublishProfile=$target
        if ($LASTEXITCODE -ne 0) {
            Write-Host "FAILED: $($project.Name) - $target" -ForegroundColor Red
            $Failed += "$($project.Name)/$target"
            continue
        }

        Write-Host "OK: $($project.Name) - $target" -ForegroundColor Green

        # Linux desktop integration: ship a .desktop entry + icon alongside the
        # binary so the artifact can be installed (or wrapped in an AppImage).
        # The ELF binary itself can't carry an icon the way a Windows .exe does.
        if ($target -like "linux-*") {
            Write-Host "    Adding Linux desktop entry + icon ..." -ForegroundColor DarkGray
            Copy-Item "Assets/Icons/thermalith.desktop" -Destination (Join-Path $outDir "thermalith.desktop") -Force
            Copy-Item "Assets/Icons/thermalith-256.png" -Destination (Join-Path $outDir "thermalith.png") -Force
        }

        # macOS needs a .app bundle (Info.plist + icon) to launch from Finder and
        # live in /Applications; the bare binary won't do. The bundle can only be
        # assembled + ad-hoc signed on macOS (needs codesign), so skip elsewhere.
        if ($target -like "osx-*") {
            if ($IsMacOS) {
                Write-Host "    Assembling macOS .app bundle ..." -ForegroundColor DarkGray
                & ./Pack-MacApp.sh $outDir
            } else {
                Write-Host "    Skipping .app bundle (not running on macOS)" -ForegroundColor DarkGray
            }
        }

        # Bundle this platform's payload into a distributable package (zip / tar.gz
        # / dmg) inside its own RID folder. The app name stays "Thermalith"; the
        # arch lives only in the archive filename so downloads are distinguishable.
        Write-Host "    Packaging $target ..." -ForegroundColor DarkGray
        $verMatch = Select-String -Path $project.Path -Pattern '<Version>(.*?)</Version>' | Select-Object -First 1
        $ver = if ($verMatch) { $verMatch.Matches[0].Groups[1].Value } else { "0.1.0" }
        $base = "Thermalith-$ver-$target"
        if ($target -like "win-*") {
            $zip = Join-Path $outDir "$base.zip"
            if (Test-Path $zip) { Remove-Item $zip -Force }
            $items = Get-ChildItem -LiteralPath $outDir -Exclude *.pdb, *.zip
            Compress-Archive -Path $items.FullName -DestinationPath $zip -Force
            Write-Host "    packaged $zip" -ForegroundColor DarkGray
        }
        elseif ($target -like "linux-*") {
            # Build in a temp dir then move in, so the tarball never includes itself.
            $tgz = "$base.tar.gz"
            $tmp = New-Item -ItemType Directory -Force -Path (Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString()))
            tar --exclude='*.pdb' -czf (Join-Path $tmp $tgz) -C $outDir .
            Move-Item (Join-Path $tmp $tgz) (Join-Path $outDir $tgz) -Force
            Remove-Item $tmp -Recurse -Force
            Write-Host "    packaged $(Join-Path $outDir $tgz)" -ForegroundColor DarkGray
        }
        elseif ($target -like "osx-*") {
            # .dmg needs macOS (hdiutil). On macOS reuse the bash packer; skip elsewhere.
            if ($IsMacOS) {
                & ./Package-Platform.sh $target $outDir
            } else {
                Write-Host "    Skipping .dmg (build on macOS)" -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host ""
if ($Failed.Count -eq 0) {
    Write-Host "All platforms built successfully." -ForegroundColor Green
} else {
    $failedList = $Failed -join ", "
    Write-Host "Failed: $failedList" -ForegroundColor Red
    exit 1
}
