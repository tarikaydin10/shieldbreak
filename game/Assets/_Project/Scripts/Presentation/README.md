# Presentation

Zeigt die Sim, ändert sie aber nie direkt: alles, was den Spielzustand beeinflusst, geht als Befehl in die `InputQueue`.

| Datei / Ordner | Inhalt |
|---|---|
| `SimRunner.cs` | treibt die Sim mit festen 60 Hz, interpoliert zwischen zwei Ticks, Pause und Zeitlupe |
| `AgentRenderer.cs` | zeichnet alle Soldaten per Instancing aus den Pipeline-Meshes |
| `AgentSelection.cs` | ausgewählte Soldaten (reiner UI-Zustand, nicht in der Sim) |
| `Admin/` | F1-Menü, Pause, Eingabe-Zugriff (`GameInput`), Build-Smoke-Test (`AutoScreenshot`) |
| `Cameras/` | `DebugTopDownCamera` |
| `Proto/` | Prototyp-Werkzeuge: `StyleCameraRig`, `AttackPreview` |

Neue Komponenten mit sichtbarem Zustand implementieren `IAdminStatus`, dann erscheinen sie im F1-Menü.
