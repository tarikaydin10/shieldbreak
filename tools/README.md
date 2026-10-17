# tools

Skripte für Setup, Tests, Builds und die Asset-Pipeline. Aufruf immer aus dem Repo-Root.

| Skript | Zweck |
|---|---|
| `setup-windows.ps1` | Einmalig: uv, Blender-MCP-Addon, Git LFS |
| `add-packages.ps1` | Einmalig: Unity-Pakete (Burst, Collections, Input System, Cinemachine …) |
| `run-tests.ps1` | EditMode-Tests im Batchmode; Pflicht vor jedem Commit |
| `run-tests.ps1 -Shadow` | dasselbe bei offenem Unity, auf einer Kopie unter `.ci/game` |
| `build-windows.ps1 -Shadow` | spielbarer Build nach `builds/<datum>/SHIELDBREAK.exe` |
| `assetgen/` | Asset-Pipeline, siehe [assetgen/README.md](assetgen/README.md) |

`-Shadow` spiegelt `game/` ohne `Library` nach `.ci/game`. Der erste Lauf importiert alles neu (einige Minuten), danach geht es schnell. Neue Dateien sollten vorher eine `.meta` haben: Sonst erzeugt die Kopie eine eigene GUID, und Verweise darauf passen später nicht zum Hauptprojekt.
