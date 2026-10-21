# SHIELDBREAK: gemeinsame Helfer der Skripte in tools/ fuer Windows und macOS (Linux geht fuer Tests auch).
# Wird per Dot-Sourcing geladen:  . (Join-Path $PSScriptRoot "common.ps1")
# Laeuft in Windows PowerShell 5.1 und in PowerShell 7 (pwsh, auf dem Mac: brew install powershell).

# PowerShell 5.1 kennt $IsMacOS und $IsLinux nicht (dann $null): das ist immer Windows
$OnMac = [bool]$IsMacOS
$OnWindows = -not ($IsMacOS -or $IsLinux)

# Unity-Editor in der Version aus ProjectVersion.txt, wie Unity Hub ihn installiert; UNITY_EXE ueberschreibt den Pfad
function Get-UnityEditor([string]$Project) {
    $line = Get-Content (Join-Path $Project "ProjectSettings/ProjectVersion.txt") | Select-String "m_EditorVersion:"
    $version = ($line -split ":\s*")[1].Trim()
    if ($env:UNITY_EXE) { $path = $env:UNITY_EXE }
    elseif ($OnMac) { $path = "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" }
    elseif ($OnWindows) { $path = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe" }
    else { $path = Join-Path $HOME "Unity/Hub/Editor/$version/Editor/Unity" }
    if (-not (Test-Path $path)) { throw "Unity $version nicht gefunden: $path (in Unity Hub genau diese Version installieren oder UNITY_EXE setzen)" }
    return [pscustomobject]@{ Version = $version; Path = $path }
}

# Spiegelt das Unity-Projekt ohne Library usw. (die Kopie behaelt ihre eigene Library), damit ein offener Editor nicht stoert
function Sync-ShadowProject([string]$Source, [string]$Copy) {
    Write-Host "Spiegle game/ nach .ci/game ..." -ForegroundColor Cyan
    $skip = @("Library", "Temp", "Logs", "obj", "UserSettings", ".vs")
    if ($OnWindows) {
        robocopy $Source $Copy /MIR /XD @skip /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy fehlgeschlagen ($LASTEXITCODE)" }
    } else {
        New-Item -ItemType Directory -Force -Path $Copy | Out-Null
        $exclude = $skip | ForEach-Object { "--exclude=$_" }
        & rsync -a --delete @exclude "$Source/" "$Copy/"
        if ($LASTEXITCODE -ne 0) { throw "rsync fehlgeschlagen ($LASTEXITCODE)" }
    }
    # robocopy meldet 1 = Dateien kopiert; nichts davon soll als Exit-Code des Aufrufers durchsickern
    $global:LASTEXITCODE = 0
}

# Startet Unity im Batchmode und wartet nur auf Unity selbst (Start-Process -Wait wartet auch auf Kindprozesse wie
# Shader-Compiler und Lizenz-Client, die den Editor ueberleben und das Skript haengen lassen)
function Invoke-Unity([string]$Unity, [string[]]$Arguments) {
    $p = Start-Process -FilePath $Unity -PassThru -ArgumentList $Arguments
    $null = $p.Handle # keeps the exit code readable after the process ended (Windows PowerShell)
    $p.WaitForExit()
    return $p.ExitCode
}
