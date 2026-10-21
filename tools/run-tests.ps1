# SHIELDBREAK — EditMode-Tests im Unity-Batchmode ausführen (Pflicht vor jedem Commit, siehe CLAUDE.md §8)
# Unity-Editor mit diesem Projekt muss geschlossen sein, ausser mit -Shadow: dann laufen die Tests auf einer Kopie
# unter .ci/game (ohne Library; der erste Lauf importiert alles neu und dauert einige Minuten).
# Aufruf aus dem Repo-Root:  .\tools\run-tests.ps1  [-Shadow]   (Windows und macOS, siehe common.ps1)
param([switch]$Shadow)

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

$OutDir = Join-Path $Project "Logs"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Results = Join-Path $OutDir "editmode-results.xml"
$Log = Join-Path $OutDir "editmode-tests.log"
if (Test-Path $Results) { Remove-Item $Results }

Write-Host "Unity $($Unity.Version), EditMode-Tests laufen ..." -ForegroundColor Cyan
$ExitCode = Invoke-Unity $Unity.Path @(
    "-batchmode", "-projectPath", "`"$Project`"",
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", "`"$Results`"", "-logFile", "`"$Log`"")

if (-not (Test-Path $Results)) {
    Select-String -Path $Log -Pattern "error CS|Exception" | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "Keine Testergebnisse (Exit $ExitCode). Log: $Log"
}

[xml]$xml = Get-Content $Results
$run = $xml.'test-run'
Write-Host ("Ergebnis: {0}  total={1} passed={2} failed={3} skipped={4}" -f $run.result, $run.total, $run.passed, $run.failed, $run.skipped)
$xml.SelectNodes("//test-case") | ForEach-Object {
    $color = if ($_.result -eq "Passed") { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1} ({2:N2}s)" -f $_.result, $_.fullname, [double]$_.duration) -ForegroundColor $color
    if ($_.result -ne "Passed") { Write-Host ("      " + $_.failure.message.InnerText) -ForegroundColor Red }
    if ($_.output -and $_.output.InnerText.Trim()) { Write-Host ("      " + $_.output.InnerText.Trim()) -ForegroundColor Gray }
}
exit $ExitCode
