# SHIELDBREAK — Unity-Packages ergänzen
# Nach dem ersten Öffnen des Unity-Projekts (game/) ausführen, Unity dabei geschlossen:  .\tools\add-packages.ps1
# Ergänzt game/Packages/manifest.json um die im CLAUDE.md festgelegten Packages.
# Versionen sind Startpunkte; Unity schlägt beim nächsten Öffnen ggf. neuere vor. Unity MCP bleibt gepinnt.

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$Manifest = Join-Path $RepoRoot "game\Packages\manifest.json"
if (-not (Test-Path $Manifest)) { throw "Nicht gefunden: $Manifest. Erst das Unity-Projekt mit Unity Hub unter game/ anlegen." }

$json = Get-Content $Manifest -Raw | ConvertFrom-Json

$wanted = [ordered]@{
    "com.unity.burst"                     = "1.8.21"
    "com.unity.collections"               = "2.5.7"
    "com.unity.mathematics"               = "1.3.2"
    "com.unity.inputsystem"               = "1.14.2"
    "com.unity.cinemachine"               = "3.1.4"
    "com.unity.animation.rigging"         = "1.3.0"
    "com.unity.test-framework"            = "1.5.1"
    "com.coplaydev.unity-mcp"             = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.0.0"
}

$added = @()
foreach ($k in $wanted.Keys) {
    if (-not ($json.dependencies.PSObject.Properties.Name -contains $k)) {
        $json.dependencies | Add-Member -NotePropertyName $k -NotePropertyValue $wanted[$k]
        $added += $k
    }
}

# Test-Framework braucht testables, damit Assets/Tests gefunden wird
if (-not ($json.PSObject.Properties.Name -contains "testables")) {
    $json | Add-Member -NotePropertyName "testables" -NotePropertyValue @()
}

$json | ConvertTo-Json -Depth 10 | Set-Content $Manifest -Encoding UTF8

if ($added.Count -gt 0) { Write-Host "Ergänzt: $($added -join ', ')" -ForegroundColor Green }
else { Write-Host "Alle Packages bereits vorhanden." -ForegroundColor Gray }

# Projektordner-Struktur anlegen (Unity erzeugt .meta beim nächsten Öffnen)
$dirs = @(
    "Assets\_Project\Scripts\Sim\Core",
    "Assets\_Project\Scripts\Sim\Grid",
    "Assets\_Project\Scripts\Sim\Agents",
    "Assets\_Project\Scripts\Sim\Combat\Grammar",
    "Assets\_Project\Scripts\Sim\Combat\Archetypes",
    "Assets\_Project\Scripts\Sim\Combat\Resolvers",
    "Assets\_Project\Scripts\Sim\Combat\Context",
    "Assets\_Project\Scripts\Sim\Formations",
    "Assets\_Project\Scripts\Sim\Nav",
    "Assets\_Project\Scripts\Sim\Morale",
    "Assets\_Project\Scripts\Sim\AI",
    "Assets\_Project\Scripts\Sim\Orders",
    "Assets\_Project\Scripts\Presentation",
    "Assets\_Project\Scripts\Player",
    "Assets\_Project\Scripts\Data",
    "Assets\_Project\Scripts\Debug",
    "Assets\_Project\Data",
    "Assets\_Project\Scenes",
    "Assets\_Project\Art\Poses",
    "Assets\Tests\EditMode"
)
foreach ($d in $dirs) {
    $p = Join-Path $RepoRoot ("game\" + $d)
    if (-not (Test-Path $p)) { New-Item -ItemType Directory -Path $p -Force | Out-Null }
}
Write-Host "Ordnerstruktur unter game/Assets angelegt. Unity jetzt öffnen." -ForegroundColor Cyan
