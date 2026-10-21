# SHIELDBREAK asset pipeline: builds all specs in art/specs with headless Blender (siehe art/style/style-bible.md)
# Aufruf aus dem Repo-Root:  .\tools\assetgen\run.ps1          (bauen, validieren, exportieren, Kontaktbogen)
#                            .\tools\assetgen\run.ps1 -Check   (neu bauen, gegen manifest.json vergleichen, nichts schreiben)
param(
    [switch]$Check,
    [switch]$NoRender,
    [string]$Blender = $env:BLENDER_EXE
)

$ErrorActionPreference = "Stop"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $Blender -and $IsMacOS) {
    $Blender = "/Applications/Blender.app/Contents/MacOS/Blender"
} elseif (-not $Blender) {
    $Blender = Get-ChildItem "C:\Program Files\Blender Foundation\Blender *\blender.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $Blender -or -not (Test-Path $Blender)) { throw "Blender nicht gefunden. BLENDER_EXE setzen oder -Blender angeben." }

$ArgsList = @()
if ($Check) { $ArgsList += "--check" }
if ($NoRender) { $ArgsList += "--no-render" }

# --factory-startup: no user add-ons or preferences may influence the result.
# Continue: Blender writes deprecation warnings to stderr, which PowerShell 5.1 would turn into a terminating error.
$ErrorActionPreference = "Continue"
& $Blender -b --factory-startup --python-exit-code 1 -P (Join-Path $Here "build.py") -- @ArgsList
exit $LASTEXITCODE
