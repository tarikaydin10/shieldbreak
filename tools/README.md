# tools

Innereien von `../sb.ps1`. Du rufst nur `sb.ps1 play|test|build|assets|setup` auf (Windows: `.\sb.ps1 …` in PowerShell, Mac: `./sb.ps1 …` im Terminal, braucht `brew install powershell`); diese Tabelle sagt, was dahinter läuft.

| sb.ps1 | Skript dahinter | Macht |
|---|---|---|
| `setup` | `git submodule update --init`, `setup-windows.ps1` bzw. `setup-mac.ps1`, dann `add-packages.ps1` | Submodul `assetgen` holen; uv, Blender-MCP-Addon, Git LFS (Mac: über Homebrew, prüft auch Xcode Command Line Tools und die Unity-Version); Unity-Pakete in `game/Packages/manifest.json` |
| `test` | `run-tests.ps1 -Shadow` | EditMode-Tests im Batchmode auf einer Kopie unter `.ci/game`, damit Unity offen bleiben darf. `-Filter CavalryTests` (Klasse, Name oder Regex) lässt nur diese laufen, zum schnellen Nachsehen; vor einem Commit laufen alle |
| `build` | `build.ps1 -Shadow -Output builds/M1-duel` | spielbarer Build für das eigene System: Windows `builds/M1-duel/SHIELDBREAK.exe`, Mac `builds/M1-duel/SHIELDBREAK.app` |
| `play` | `build` (falls nötig) + Start | Spiel starten |
| `assets` | `assetgen/forge.ps1 -Project .` und `-Check` | Asset-Pipeline, siehe [assetgen/README.md](assetgen/README.md) |

`common.ps1` hält, was Windows und Mac unterscheidet: wo Unity liegt (Unity-Hub-Standardpfad der Version aus `ProjectVersion.txt`, `UNITY_EXE` überschreibt), wie gespiegelt wird (Windows `robocopy`, Mac `rsync`) und wie Unity gestartet und abgewartet wird. Blender: Windows unter `C:\Program Files\Blender Foundation`, Mac `/Applications/Blender.app`, `BLENDER_EXE` überschreibt.

`-Shadow` spiegelt `game/` ohne `Library` nach `.ci/game`. Der erste Lauf importiert alles neu (einige Minuten), danach geht es schnell. Neue Dateien sollten vorher eine `.meta` haben: Sonst erzeugt die Kopie eine eigene GUID, und Verweise darauf passen später nicht zum Hauptprojekt.

Ein Build entsteht immer auf dem System, für das er ist: die exe auf dem PC, die App auf dem Mac (Burst braucht dort die Xcode Command Line Tools).

**`assetgen/` ist ein Git-Submodul** ([assetforge](https://github.com/tarikaydin10/assetforge), seit 2026-11-01 ein eigenes Repo, das auch andere Spiele nutzen). Der Kern kennt kein Projekt; Palette, Regeln, Specs und Generatoren liegen in [`art/`](../art/README.md). Änderungen am Werkzeug passieren im Repo `assetforge`, hier zieht `git submodule update --remote tools/assetgen` sie nach (danach `.\sb.ps1 assets` und Commit des neuen Submodul-Stands).

**Ohne sb.ps1:** [`simcheck/`](simcheck/README.md) kompiliert die Sim und ihre Unity-freien Tests mit .NET 8 (für Cloud-Sessions ohne Unity, `tools/simcheck/run.sh`). Für dich nicht nötig.
