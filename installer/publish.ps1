# Publishes the sample app + updater service + runner into a single merged folder
# (publish/SampleApp) ready for InnoSetup to package. Run from the repo root or anywhere.
#
#   pwsh installer/publish.ps1            # publishes version from the csproj
#   pwsh installer/publish.ps1 -Version 1.2.0
#
# Auto-update only activates for Release builds (spec §2.4), so we always publish Release.

param(
    [string]$Version,
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "publish/SampleApp"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

$versionArg = @()
if ($Version) { $versionArg = @("-p:Version=$Version") }

# The runner is NOT published separately: it is embedded into AutoUpdater.Service.exe (via
# -p:EmbedRunner=true) and extracted to %TEMP% at runtime (spec §2.1). So we publish only the
# host app and the service into the merged folder.
$projects = @(
    @{ Path = "samples/SampleApp/SampleApp.csproj";              Extra = @() },
    @{ Path = "src/AutoUpdater.Service/AutoUpdater.Service.csproj"; Extra = @("-p:EmbedRunner=true") }
)

foreach ($proj in $projects) {
    Write-Host "Publishing $($proj.Path) ..." -ForegroundColor Cyan
    dotnet publish (Join-Path $root $proj.Path) `
        -c Release -r $Runtime --self-contained false `
        -o $out @versionArg @($proj.Extra)
}

Write-Host "`nPublished to: $out" -ForegroundColor Green
Write-Host "Next: set MyAppVersion in installer/SampleApp.iss, then run 'iscc installer/SampleApp.iss'." -ForegroundColor Green
