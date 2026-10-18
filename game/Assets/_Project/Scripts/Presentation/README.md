# Presentation

Zeigt die Sim, ändert sie aber nie direkt: alles, was den Spielzustand beeinflusst, geht als Befehl in die `InputQueue`.

| Datei / Ordner | Inhalt |
|---|---|
| `SimRunner.cs` | treibt die Sim mit festen 60 Hz aus einem `ISimScenario`, interpoliert, Pause, Zeitlupe (auch kurz bei Kills), Hitstop, Neustart |
| `AgentRenderer.cs` | zeichnet viele Soldaten per Instancing (Sandbox, ohne Kampf-Posen) |
| `CombatFigureRenderer.cs` | Kämpfer als Figuren: Waffe folgt der Sim-Waffenbahn, Arme per IK an Schwert und Schild bzw. beide Hände am Zweihänder, Neigung, Haltung nach Leben, Stun, Tod weg vom Angreifer |
| `AgentSelection.cs` | ausgewählte Soldaten (reiner UI-Zustand, nicht in der Sim) |
| `Menu/` | `MainMenu` (erste Szene: Modi, Duell-Setup), `PauseMenu` (Esc), `ControlsPage` (Tasten ändern), `OptionsPage`, `GameSettings` (PlayerPrefs), `DuelLaunch` (Menüwahl → Duell), `DuelSetupPage` (Setup-Seite, im Hauptmenü und nach dem Kampf) |
| `Admin/` | F1-Menü, Pause, Eingabe-Zugriff (`GameInput`), Build-Smoke-Test (`AutoScreenshot`) |
| `Feedback/` | `ImpactEffects` (Blut, Funken, Holzsplitter), `CombatAudio` + `ProceduralSfx` (Platzhalter-Sounds, synthetisch), `StaminaVignette` (Leben als roter Rand, Ausdauer als dunkler Puls, statt HUD) |
| `Cameras/` | `DebugTopDownCamera`, `ThirdPersonCamera` (Schulter, Kick, Zuschauer-Orbit) |
| `Player/` | `PlayerCombatInput` (Eingaben → Befehle), `DuelDirector` (Start-Einblendung, Hitstop, Statistik, Ergebnisbildschirm), `TutorialDirector` (geführtes Duell in 13 Schritten) |
| `Proto/` | Prototyp-Werkzeuge: `StyleCameraRig`, `AttackPreview` |

Neue Komponenten mit sichtbarem Zustand implementieren `IAdminStatus`, dann erscheinen sie im F1-Menü.
