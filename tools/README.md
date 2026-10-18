# tools

Innereien von `..\sb.ps1`. Du rufst nur `.\sb.ps1 play|test|build|assets|setup` auf; diese Tabelle sagt, was dahinter läuft.

| sb.ps1 | Skript dahinter | Macht |
|---|---|---|
| `setup` | `setup-windows.ps1`, `add-packages.ps1` | uv, Blender-MCP-Addon, Git LFS; Unity-Pakete in `game/Packages/manifest.json` |
| `test` | `run-tests.ps1 -Shadow` | EditMode-Tests im Batchmode auf einer Kopie unter `.ci/game`, damit Unity offen bleiben darf |
| `build` | `build-windows.ps1 -Shadow -Output builds\M1-duel` | spielbarer Build `builds/M1-duel/SHIELDBREAK.exe` |
| `play` | `build` (falls nötig) + Start der exe | Spiel starten |
| `assets` | `assetgen/run.ps1` und `run.ps1 -Check` | Asset-Pipeline, siehe [assetgen/README.md](assetgen/README.md) |

`-Shadow` spiegelt `game/` ohne `Library` nach `.ci/game`. Der erste Lauf importiert alles neu (einige Minuten), danach geht es schnell. Neue Dateien sollten vorher eine `.meta` haben: Sonst erzeugt die Kopie eine eigene GUID, und Verweise darauf passen später nicht zum Hauptprojekt.
