# Presentation

Zeigt die Sim, ändert sie aber nie direkt: alles, was den Spielzustand beeinflusst, geht als Befehl in die `InputQueue`.

| Datei / Ordner | Inhalt |
|---|---|
| `SimRunner.cs` | treibt die Sim mit festen 60 Hz aus einem `ISimScenario`, interpoliert, Pause, Zeitlupe, Hitstop, Neustart |
| `AgentRenderer.cs` | zeichnet viele Soldaten per Instancing (Sandbox, ohne Kampf-Posen) |
| `CombatFigureRenderer.cs` | Kämpfer als Figuren: Waffe folgt der Sim-Waffenbahn, Arme per IK an Schwert und Schild, Neigung, Stun, Tod |
| `AgentSelection.cs` | ausgewählte Soldaten (reiner UI-Zustand, nicht in der Sim) |
| `Admin/` | F1-Menü, Pause, Eingabe-Zugriff (`GameInput`), Build-Smoke-Test (`AutoScreenshot`) |
| `Cameras/` | `DebugTopDownCamera`, `ThirdPersonCamera` (Schulter, Kick, Zuschauer-Orbit) |
| `Player/` | `PlayerCombatInput` (Eingaben → Befehle), `DuelDirector` (Presets, Bot-Stufe, Hitstop, Balken) |
| `Proto/` | Prototyp-Werkzeuge: `StyleCameraRig`, `AttackPreview` |

Neue Komponenten mit sichtbarem Zustand implementieren `IAdminStatus`, dann erscheinen sie im F1-Menü.
