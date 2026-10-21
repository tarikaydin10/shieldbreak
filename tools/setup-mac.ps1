# SHIELDBREAK — Einmaliges Setup (macOS, Apple Silicon oder Intel)
# Aufruf aus dem Repo-Root im Terminal:  ./sb.ps1 setup   (braucht PowerShell: brew install powershell)
# Installiert uv und Git LFS über Homebrew, das Blender-MCP-Addon, registriert Blender-MCP in Claude Code und prüft
# Blender, Unity Hub, die passende Unity-Version und die Xcode Command Line Tools (Burst braucht sie für Mac-Builds).

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "common.ps1")
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot
Write-Host "Repo-Root: $RepoRoot" -ForegroundColor Cyan

function Has($cmd) { return [bool](Get-Command $cmd -ErrorAction SilentlyContinue) }

# 1. Homebrew, uv, Git LFS
if (-not (Has "brew")) { throw "Homebrew fehlt: https://brew.sh installieren, Terminal neu öffnen, dann erneut ./sb.ps1 setup" }
foreach ($tool in @(@{ Cmd = "uv"; Formula = "uv" }, @{ Cmd = "git-lfs"; Formula = "git-lfs" })) {
    if (Has $tool.Cmd) { Write-Host "[1/6] $($tool.Cmd) vorhanden" -ForegroundColor Green }
    else {
        Write-Host "[1/6] Installiere $($tool.Formula) ..." -ForegroundColor Yellow
        brew install $tool.Formula
    }
}
if (Has "git-lfs") { git lfs install | Out-Null; git lfs pull }

# 2. Blender-MCP-Addon installieren
Write-Host "[2/6] Installiere Blender-MCP-Addon ..." -ForegroundColor Yellow
uvx blender-mcp install-addon
Write-Host "      Danach in Blender: Settings > Add-ons > 'MCP for Blender' aktivieren." -ForegroundColor Gray

# 3. Claude Code: Blender-MCP im Projekt-Scope (steht schon in .mcp.json, wenn das Repo es mitbringt)
if (Has "claude") {
    if (-not (Test-Path ".mcp.json")) {
        Write-Host "[3/6] Registriere Blender-MCP in Claude Code (Projekt-Scope) ..." -ForegroundColor Yellow
        claude mcp add --scope project blender uvx blender-mcp
    } else { Write-Host "[3/6] .mcp.json vorhanden, Blender-MCP ist eingetragen." -ForegroundColor Green }
} else {
    Write-Host "[3/6] Claude Code CLI nicht gefunden (optional): https://claude.com/claude-code" -ForegroundColor Gray
}

# 4. Xcode Command Line Tools (Git und Burst brauchen sie)
if ((& xcode-select -p 2>$null) -and $LASTEXITCODE -eq 0) { Write-Host "[4/6] Xcode Command Line Tools vorhanden" -ForegroundColor Green }
else { Write-Host "[4/6] Xcode Command Line Tools fehlen: xcode-select --install ausführen" -ForegroundColor Red }

# 5. Blender, Unity Hub, Unity-Version prüfen (nur Hinweise)
Write-Host "[5/6] Tool-Check ..." -ForegroundColor Yellow
if (Test-Path "/Applications/Blender.app") { Write-Host "      Blender gefunden." -ForegroundColor Green }
else { Write-Host "      Blender fehlt (>= 3.0, für ./sb.ps1 assets): brew install --cask blender" -ForegroundColor Red }
if (Test-Path "/Applications/Unity Hub.app") { Write-Host "      Unity Hub gefunden." -ForegroundColor Green }
else { Write-Host "      Unity Hub fehlt: brew install --cask unity-hub" -ForegroundColor Red }
try {
    $unity = Get-UnityEditor (Join-Path $RepoRoot "game")
    Write-Host "      Unity $($unity.Version) gefunden." -ForegroundColor Green
} catch {
    Write-Host "      $($_.Exception.Message)" -ForegroundColor Red
}

# 6. Nächste Schritte
Write-Host ""
Write-Host "[6/6] Fertig. Nächste Schritte:" -ForegroundColor Cyan
Write-Host "  a) Unity Hub > Installs: die Version aus game/ProjectSettings/ProjectVersion.txt installieren" -ForegroundColor White
Write-Host "     (Apple-Silicon-Editor; 'Mac Build Support' gehört zum Mac-Editor, Windows-Builds weiter auf dem PC)" -ForegroundColor White
Write-Host "  b) Unity Hub > Projects > Add > Ordner game, einmal öffnen (erster Import dauert)" -ForegroundColor White
Write-Host "  c) Unity > Window > MCP for Unity > Configure All Detected Clients" -ForegroundColor White
Write-Host "  d) Blender öffnen > N-Panel > MCP for Blender > Connect to Claude" -ForegroundColor White
Write-Host "  e) ./sb.ps1 test, dann ./sb.ps1 play" -ForegroundColor White
