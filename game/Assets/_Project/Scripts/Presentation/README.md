# Presentation

Zeigt die Sim, ändert sie aber nie direkt: alles, was den Spielzustand beeinflusst, geht als Befehl in die `InputQueue`.

| Datei / Ordner | Inhalt |
|---|---|
| `SimRunner.cs` | treibt die Sim mit festen 60 Hz aus einem `ISimScenario`, interpoliert, Pause, Zeitlupe (auch kurz bei Kills), Hitstop, Neustart; `PendingScenario` ersetzt beim nächsten Szenenstart das Szenario der Szene (Stresstest in der Sandbox-Szene) |
| `AgentRenderer.cs` | zeichnet viele Soldaten per Instancing (ohne Kampf-Posen, Tote liegen, Geflohene sind weg); wer schon als Kampf-Figur gezeigt wird, bekommt hier nur seinen Auswahlring |
| `GroundLines.cs` | GL-Linien und gefüllte Zellen auf dem Boden (Linie, Kreuz, Pfeil, Viereck), immer obenauf; für Kommandoansicht und Debug-Overlay |
| `CombatFigureRenderer.cs` | Kämpfer als Figuren (die ersten `maxFigures` Agenten; Duell und Sandbox): Waffe folgt der Sim-Waffenbahn, Arme per IK an Schwert und Schild bzw. beide Hände am Zweihänder, Neigung, Haltung nach Leben, Stun, Tod weg vom Angreifer |
| `AgentSelection.cs` | ausgewählte Soldaten (reiner UI-Zustand, nicht in der Sim) |
| `Menu/` | `MainMenu` (erste Szene: Modi, Duell-Setup, Kompanie, Testmodus), `PauseMenu` (Esc, mit Ziel und Prüfliste der Szene), `ControlsPage` (Tasten ändern), `OptionsPage`, `GameSettings` (PlayerPrefs), `DuelLaunch` (Menüwahl → Duell), `DuelSetupPage` (Setup-Seite, im Hauptmenü und nach dem Kampf), `SceneLaunch` (startet jeden Eintrag des Szenen-Katalogs), `TestPage` (Testmodus: alle Einträge mit Ziel, Steuerung, Prüfliste, Starten), `TestChecklist` (abgehakte Prüfpunkte in PlayerPrefs, gemeinsame Darstellung), `SceneBriefing` (in jeder Szene: Ziel beim Start, F2 volle Hilfe) |
| `Admin/` | F1-Menü, Pause, Eingabe-Zugriff (`GameInput`), Build-Smoke-Test (`AutoScreenshot`) |
| `Feedback/` | `ImpactEffects` (Blut, Funken, Holzsplitter), `CombatAudio` + `ProceduralSfx` (Platzhalter-Sounds, synthetisch), `StaminaVignette` (Leben als roter Rand, Ausdauer als dunkler Puls, statt HUD) |
| `Cameras/` | `DebugTopDownCamera`, `ThirdPersonCamera` (Schulter, Kick, Zuschauer-Orbit), `CommandViewCamera` (fährt in 0,5 s schräg über das Feld und zurück, Mausrad: Höhe, Nebel weicht zurück) |
| `Player/` | `PlayerCombatInput` (Eingaben → Befehle; bei offenem Radialmenü nur laufen), `DuelDirector` (Start-Einblendung, Hitstop, Statistik, Ergebnisbildschirm), `TutorialDirector` (geführtes Duell in 13 Schritten), `RadialMenu` (F/RB halten: Schnellbefehle an die eigenen Formationen, Ziel per Mausrad/LB; in der Kommandoansicht an deren Auswahl), `CommandView` (V/LB halten: Held unbeaufsichtigt, Auswahl, Klick/Ziehen = Marsch/Frontlinie, Rechtsklick auf Feind = Vorrücken gegen ihn, Umriss, Befehl und Moral jeder Formation, Zeitlupe), `CompanyDirector` (Kompanie: Hitstop und Kamera-Kick, Zustand beider Seiten im F1-Menü) |
| `Proto/` | Prototyp-Werkzeuge: `StyleCameraRig`, `AttackPreview` |

Neue Komponenten mit sichtbarem Zustand implementieren `IAdminStatus`, dann erscheinen sie im F1-Menü.

Neue Szene oder neuer Modus: Eintrag in `Resources/SceneCatalog.asset` (Ziel, Steuerung, Prüfliste), Szene in `BuildTools.Scenes` und in den Build Settings. `SceneCatalogTests` schlägt sonst fehl. Kein eigener Start-Hinweis pro Szene; das Briefing kommt aus dem Katalog.
