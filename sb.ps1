# SHIELDBREAK — der eine Befehl. Aus dem Repo-Root in PowerShell:
#   .\sb.ps1            zeigt diese Hilfe
#   .\sb.ps1 play       Spiel starten (baut vorher, falls kein Build da ist)
#   .\sb.ps1 build      spielbaren Build erzeugen (builds\M1-duel\SHIELDBREAK.exe)
#   .\sb.ps1 test       alle Tests (85, inkl. Determinismus); Unity darf offen bleiben
#   .\sb.ps1 assets     Low-Poly-Modelle aus art/specs neu bauen und prüfen
#   .\sb.ps1 setup      einmalig nach dem Klonen: Werkzeuge, Unity-Pakete
# Die Skripte in tools\ sind die Innereien; du brauchst sie nicht direkt.
param(
    [ValidateSet("help", "play", "build", "test", "assets", "setup")]
    [string]$Command = "help",
    [switch]$Fresh
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root
$Exe = Join-Path $Root "builds\M1-duel\SHIELDBREAK.exe"

function Step($text) { Write-Host "`n== $text" -ForegroundColor Cyan }

switch ($Command) {
    "help" {
        Write-Host ""
        Write-Host "SHIELDBREAK" -ForegroundColor Cyan
        Write-Host "  .\sb.ps1 play      Spiel starten (Hauptmenue; baut vorher, wenn noetig; -Fresh baut immer neu)"
        Write-Host "  .\sb.ps1 build     spielbaren Build erzeugen:  builds\M1-duel\SHIELDBREAK.exe"
        Write-Host "  .\sb.ps1 test      alle Tests laufen lassen (Unity darf offen bleiben)"
        Write-Host "  .\sb.ps1 assets    Modelle aus art\specs neu bauen und auf Reproduzierbarkeit pruefen"
        Write-Host "  .\sb.ps1 setup     einmalig nach dem Klonen"
        Write-Host ""
        Write-Host "Im Spiel: Esc Pausemenue (Steuerung, Optionen), F1 Status und Admin." -ForegroundColor DarkGray
        Write-Host "Was es gibt und wie man es spielt: docs\STATUS.md" -ForegroundColor DarkGray
        Write-Host ""
    }
    "play" {
        if ($Fresh -or -not (Test-Path $Exe)) {
            Step "Kein Build vorhanden oder -Fresh: baue zuerst"
            & (Join-Path $Root "tools\build-windows.ps1") -Shadow -Output "builds\M1-duel"
            if ($LASTEXITCODE -ne 0) { throw "Build fehlgeschlagen." }
        }
        Step "Starte $Exe"
        Start-Process -FilePath $Exe
    }
    "build" {
        & (Join-Path $Root "tools\build-windows.ps1") -Shadow -Output "builds\M1-duel"
        exit $LASTEXITCODE
    }
    "test" {
        & (Join-Path $Root "tools\run-tests.ps1") -Shadow
        exit $LASTEXITCODE
    }
    "assets" {
        Step "Bauen (Kontaktbogen: art\contact-sheet.png)"
        & (Join-Path $Root "tools\assetgen\run.ps1")
        if ($LASTEXITCODE -ne 0) { throw "Asset-Build fehlgeschlagen." }
        Step "Pruefen, dass alles reproduzierbar ist"
        & (Join-Path $Root "tools\assetgen\run.ps1") -Check
        exit $LASTEXITCODE
    }
    "setup" {
        Step "1/2 Werkzeuge (uv, Blender-MCP, Git LFS)"
        & (Join-Path $Root "tools\setup-windows.ps1")
        Step "2/2 Unity-Pakete (Unity dabei geschlossen)"
        & (Join-Path $Root "tools\add-packages.ps1")
        Write-Host "`nFertig. Jetzt: Unity Hub -> Add -> Ordner game, einmal oeffnen, dann .\sb.ps1 play" -ForegroundColor Green
    }
}
