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
$Projects = @(
    @{ Name = "Thermalith.App"; Path = "src/Thermalith.App/Thermalith.App.csproj" }
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
