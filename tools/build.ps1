# SHIELDBREAK — spielbarer Build (Arbeitsregel: jeden Freitag ein spielbarer Build)
# Aufruf aus dem Repo-Root:  .\tools\build.ps1 [-Shadow] [-Output builds\<name>]
# Baut fuer das System, auf dem es laeuft: Windows -> <Output>\SHIELDBREAK.exe, macOS -> <Output>/SHIELDBREAK.app.
# -Shadow baut auf der Kopie .ci/game (wie run-tests.ps1 -Shadow), damit ein offener Editor nicht stört.
param(
    [switch]$Shadow,
    [string]$Output = ("builds/" + (Get-Date -Format "yyyy-MM-dd"))
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $RepoRoot "game"
if ($Shadow) {
    $Copy = Join-Path $RepoRoot ".ci/game"
    Sync-ShadowProject $Project $Copy
    $Project = $Copy
}
$Unity = Get-UnityEditor $Project
if ($OnMac) { $Method = "BuildMacBatch"; $Player = "SHIELDBREAK.app" }
else { $Method = "BuildWindowsBatch"; $Player = "SHIELDBREAK.exe" }

$Target = Join-Path $RepoRoot $Output
$Log = Join-Path $Project "Logs/build.log"
New-Item -ItemType Directory -Force -Path (Split-Path $Log) | Out-Null
Write-Host "Unity $($Unity.Version), baue $Player nach $Target ..." -ForegroundColor Cyan
$ExitCode = Invoke-Unity $Unity.Path @(
    "-batchmode", "-quit", "-projectPath", "`"$Project`"",
    "-executeMethod", "Shieldbreak.EditorTools.BuildTools.$Method",
    "-buildOutput", "`"$Target`"", "-logFile", "`"$Log`"")

Select-String -Path $Log -Pattern "\[build\]|error CS" | Select-Object -First 10 | ForEach-Object { Write-Host $_.Line }
if ($ExitCode -ne 0) {
    if ($OnMac) { Write-Host "Mac: Burst braucht die Xcode Command Line Tools (xcode-select --install)." -ForegroundColor Yellow }
    throw "Build fehlgeschlagen (Exit $ExitCode). Log: $Log"
}
Write-Host "Fertig: $(Join-Path $Target $Player)" -ForegroundColor Green
exit 0
