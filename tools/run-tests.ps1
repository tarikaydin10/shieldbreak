# SHIELDBREAK — EditMode-Tests im Unity-Batchmode ausführen (Pflicht vor jedem Commit, siehe CLAUDE.md §8)
# Unity-Editor mit diesem Projekt muss geschlossen sein; bei offenem Editor stattdessen Unity MCP run_tests nutzen.
# Aufruf aus dem Repo-Root:  .\tools\run-tests.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Project = Join-Path $RepoRoot "game"
$Version = ((Get-Content (Join-Path $Project "ProjectSettings\ProjectVersion.txt") | Select-String "m_EditorVersion:") -split ":\s*")[1].Trim()
$Unity = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
if (-not (Test-Path $Unity)) { throw "Unity $Version nicht gefunden: $Unity" }

$OutDir = Join-Path $Project "Logs"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$Results = Join-Path $OutDir "editmode-results.xml"
$Log = Join-Path $OutDir "editmode-tests.log"
if (Test-Path $Results) { Remove-Item $Results }

Write-Host "Unity $Version, EditMode-Tests laufen ..." -ForegroundColor Cyan
# WaitForExit instead of Start-Process -Wait: -Wait also waits for Unity's child processes
# (shader compiler, licensing client), which outlive the editor and hang the script
$p = Start-Process -FilePath $Unity -PassThru -ArgumentList @(
    "-batchmode", "-projectPath", "`"$Project`"",
    "-runTests", "-testPlatform", "EditMode",
    "-testResults", "`"$Results`"", "-logFile", "`"$Log`"")
$p.WaitForExit()

if (-not (Test-Path $Results)) {
    Select-String -Path $Log -Pattern "error CS|Exception" | Select-Object -First 20 | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "Keine Testergebnisse (Exit $($p.ExitCode)). Log: $Log"
}

[xml]$xml = Get-Content $Results
$run = $xml.'test-run'
Write-Host ("Ergebnis: {0}  total={1} passed={2} failed={3} skipped={4}" -f $run.result, $run.total, $run.passed, $run.failed, $run.skipped)
$xml.SelectNodes("//test-case") | ForEach-Object {
    $color = if ($_.result -eq "Passed") { "Green" } else { "Red" }
    Write-Host ("  [{0}] {1} ({2:N2}s)" -f $_.result, $_.fullname, [double]$_.duration) -ForegroundColor $color
    if ($_.result -ne "Passed") { Write-Host ("      " + $_.failure.message.InnerText) -ForegroundColor Red }
}
exit $p.ExitCode
