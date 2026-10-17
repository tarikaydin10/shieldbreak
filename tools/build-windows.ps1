# SHIELDBREAK — Windows-Build (Arbeitsregel: jeden Freitag ein spielbarer Build)
# Aufruf aus dem Repo-Root:  .\tools\build-windows.ps1 [-Shadow] [-Output builds\<name>]
# -Shadow baut auf der Kopie .ci/game (wie run-tests.ps1 -Shadow), damit ein offener Editor nicht stört.
param(
    [switch]$Shadow,
    [string]$Output = ("builds\" + (Get-Date -Format "yyyy-MM-dd"))
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Project = Join-Path $RepoRoot "game"
if ($Shadow) {
    $Copy = Join-Path $RepoRoot ".ci\game"
    Write-Host "Spiegle game/ nach .ci/game ..." -ForegroundColor Cyan
    robocopy $Project $Copy /MIR /XD Library Temp Logs obj UserSettings .vs /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy fehlgeschlagen ($LASTEXITCODE)" }
    $Project = $Copy
}
$Version = ((Get-Content (Join-Path $Project "ProjectSettings\ProjectVersion.txt") | Select-String "m_EditorVersion:") -split ":\s*")[1].Trim()
$Unity = "C:\Program Files\Unity\Hub\Editor\$Version\Editor\Unity.exe"
if (-not (Test-Path $Unity)) { throw "Unity $Version nicht gefunden: $Unity" }

$Target = Join-Path $RepoRoot $Output
$Log = Join-Path $Project "Logs\build-windows.log"
New-Item -ItemType Directory -Force -Path (Split-Path $Log) | Out-Null
Write-Host "Unity $Version, baue nach $Target ..." -ForegroundColor Cyan
$p = Start-Process -FilePath $Unity -PassThru -ArgumentList @(
    "-batchmode", "-quit", "-projectPath", "`"$Project`"",
    "-executeMethod", "Shieldbreak.EditorTools.BuildTools.BuildWindowsBatch",
    "-buildOutput", "`"$Target`"", "-logFile", "`"$Log`"")
$p.WaitForExit()

Select-String -Path $Log -Pattern "\[build\]|error CS" | Select-Object -First 10 | ForEach-Object { Write-Host $_.Line }
if ($p.ExitCode -ne 0) { throw "Build fehlgeschlagen (Exit $($p.ExitCode)). Log: $Log" }
Write-Host "Fertig: $Target\SHIELDBREAK.exe" -ForegroundColor Green
# Explicit: otherwise the script inherits robocopy's exit code (1 = files copied)
exit 0
