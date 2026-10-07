# SHIELDBREAK — Einmaliges Setup (Windows)
# Ausführen in PowerShell aus dem Repo-Root:  .\tools\setup-windows.ps1
# Installiert uv, das Blender-MCP-Addon, registriert Blender-MCP in Claude Code, initialisiert Git + LFS.

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $RepoRoot
Write-Host "Repo-Root: $RepoRoot" -ForegroundColor Cyan

function Has($cmd) { return [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }

# 1. uv (offizieller Installer, nicht pip)
if (-not (Has "uv")) {
    Write-Host "[1/6] Installiere uv ..." -ForegroundColor Yellow
    powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
    $env:Path = "$env:USERPROFILE\.local\bin;$env:Path"
} else { Write-Host "[1/6] uv vorhanden: $(uv --version)" -ForegroundColor Green }

if (-not (Has "uv")) { throw "uv nicht im PATH. Terminal neu öffnen und Skript erneut starten." }

# 2. Blender-MCP-Addon installieren
Write-Host "[2/6] Installiere Blender-MCP-Addon ..." -ForegroundColor Yellow
uvx blender-mcp install-addon
Write-Host "      Danach in Blender: Edit > Preferences > Add-ons > 'MCP for Blender' aktivieren." -ForegroundColor Gray

# 3. Claude Code prüfen und Blender-MCP im Projekt-Scope registrieren
if (Has "claude") {
    Write-Host "[3/6] Registriere Blender-MCP in Claude Code (Projekt-Scope) ..." -ForegroundColor Yellow
    if (-not (Test-Path ".mcp.json")) {
        claude mcp add --scope project blender uvx blender-mcp
    } else { Write-Host "      .mcp.json existiert bereits, übersprungen." -ForegroundColor Gray }
} else {
    Write-Host "[3/6] Claude Code CLI nicht gefunden. Installieren, dann im Repo-Root ausführen:" -ForegroundColor Red
    Write-Host "      claude mcp add --scope project blender uvx blender-mcp" -ForegroundColor Red
}

# 4. Git + LFS
Write-Host "[4/6] Git initialisieren ..." -ForegroundColor Yellow
if (-not (Test-Path ".git")) { git init -b main | Out-Null }
if (Has "git-lfs") { git lfs install | Out-Null; Write-Host "      Git LFS aktiv." -ForegroundColor Green }
else { Write-Host "      Git LFS fehlt: https://git-lfs.com — danach 'git lfs install' ausführen." -ForegroundColor Red }

# 5. Blender und Unity Hub prüfen (nur Hinweis)
Write-Host "[5/6] Tool-Check ..." -ForegroundColor Yellow
$blender = Get-ChildItem "C:\Program Files\Blender Foundation" -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
if ($blender) { Write-Host "      Blender gefunden: $($blender.FullName)" -ForegroundColor Green }
else { Write-Host "      Blender nicht unter 'C:\Program Files\Blender Foundation' gefunden (>= 3.0 nötig)." -ForegroundColor Red }
$hub = Test-Path "C:\Program Files\Unity Hub\Unity Hub.exe"
if ($hub) { Write-Host "      Unity Hub gefunden." -ForegroundColor Green } else { Write-Host "      Unity Hub nicht gefunden." -ForegroundColor Red }

# 6. Nächste Schritte
Write-Host ""
Write-Host "[6/6] Fertig. Nächste Schritte:" -ForegroundColor Cyan
Write-Host "  a) Unity Hub > New Project > Universal 3D" -ForegroundColor White
Write-Host "     Location: $RepoRoot   Project name: game   Version: 6000.5.x" -ForegroundColor White
Write-Host "  b) Nach dem ersten Öffnen Unity schließen und ausführen:  .\tools\add-packages.ps1" -ForegroundColor White
Write-Host "  c) Unity öffnen > Window > MCP for Unity > Configure All Detected Clients" -ForegroundColor White
Write-Host "  d) Blender öffnen > N-Panel > MCP for Blender > Connect to Claude" -ForegroundColor White
Write-Host "  e) Im Repo-Root 'claude' starten und /mcp prüfen" -ForegroundColor White
