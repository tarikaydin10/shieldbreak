# Stand: was es gibt, wie man es spielt und testet

> Wird nach jeder Iteration im selben Commit aktualisiert (CLAUDE.md, Arbeitsregeln). Stand: **2026-10-18 (abends)**, Meilenstein **M1 Duell**: Umfang gebaut, Spielrahmen (Menüs, Tutorial) da, **Fun-Gate offen**.

## Sofort spielen

| Weg | Was tun |
|---|---|
| Build | `.\sb.ps1 play` (baut, wenn nötig, und startet `builds\M1-duel\SHIELDBREAK.exe`; `-Fresh` baut immer neu). Startet im **Hauptmenü** |
| Unity | `game/` im Unity Hub öffnen, Szene `Assets/_Project/Scenes/MainMenu` (oder direkt `M1_Duel`) öffnen, Play |

**Hauptmenü:** Duell spielen (Kampfgefühl, Gegner-Stufe, Gegnerzahl, Waffen wählen), Tutorial (13 Schritte, jeder schließt durch Tun ab), Zuschauen, Sandbox, Prototypen, Steuerung (Tasten ändern, bleibt gespeichert), Optionen (Maus, Lautstärke, Zeitlupe, Angriffs-Steuerung).
**Im Spiel:** **Esc** Pausemenü (Fortsetzen, Neu starten, Steuerung, Optionen, Hauptmenü), **F1** Status und Admin (alle Tasten, Szenenwechsel, FPS).

## Steuerung im Duell (`M1_Duel`)

| Taste (Gamepad) | Aktion |
|---|---|
| WASD (linker Stick), Maus (rechter Stick) | laufen relativ zur Kamera, umschauen |
| Linke Maustaste (RT) | **tippen** = leichter Angriff (Kette rechts/links, mit W: Stich), **halten** = schwerer Hieb von oben |
| Rechte Maustaste (LT) | Block halten (Frontbogen); kurz vor dem Treffer gedrückt = **Perfect Block** |
| Q (B) | Finte (früh im Ausholen abbrechen) |
| Shift (L3) | Sprint; ein Angriff aus dem Sprint trifft härter |
| Leertaste (A) | Ausweichschritt 2 m in Laufrichtung (ohne Richtung: zurück) |
| E (Y) | Special: Schildstoß (Schwert + Schild) oder Sprung-Hieb (Zweihänder) |
| 1–4, mittlere Maustaste (Steuerkreuz hoch) | Waffe wechseln: Slot 1 Schwert + Schild, Slot 2 Zweihänder |
| Tab | Steuerung umschalten: Einfach (leicht/schwer) oder Richtungen (Maus) |
| F5 / F6 / F7 | neu starten / Kampfgefühl (Schwer, Mittel, Tödlich) / Gegner-Stufe L1–L5 |
| F8 / F9 / F10 / F11 | Zuschauen (Bot gegen Bot) / Lebensbalken (Debug) / 1–3 Gegner / Gegner-Waffe |
| Z, N (bei Pause) | Zeitlupe, ein Tick weiter |
| F12 (Tutorial) | Schritt überspringen |

![Tutorial: geführtes Duell](screenshots/tutorial.jpg)

## Was existiert

| Bereich | Inhalt | Wo |
|---|---|---|
| Sim-Kern | 60-Hz-Fixed-Step, `InputQueue` (auch im Singleplayer), `SimRandom`, Hash; deterministisch, Lockstep-tauglich | `Scripts/Sim/Core` |
| Grid, Agenten | Spatial Hash per Counting-Sort, Steering mit Trennung; 500 Agenten 0,12 ms pro Tick | `Scripts/Sim/Grid`, `Agents` |
| Kampf-Grammatik | Ausholen → Treffen → Erholen, Finte, Morph, Eingabepuffer, ein Block im Frontbogen, Perfect Block, Guard Break, Ausweichschritt, Sprint, Waffenwechsel, Rückstoß, Energie-Schaden über Klingenbahn (SwingTrace) | [Sim/Combat](../game/Assets/_Project/Scripts/Sim/Combat/README.md) |
| Waffen | **Schwert + Schild** (Schild 120°, Schildstoß) und **Zweihänder** (90° Block, Sprung-Hieb, trifft härter, drückt Blocker stark) | `Data/Weapons`, `Data/Attacks` |
| Bots | Stufen L1–L5 spielen dieselben Regeln, nur Reaktionszeit, Fehlerquote und Techniken unterscheiden sich; höchstens 2 greifen gleichzeitig an | `Scripts/Sim/AI` |
| Kampfgefühl | Hitstop, Kamera-Kick, Rückstoß, Blut/Funken/Splitter, Platzhalter-Sounds (synthetisch), Ausdauer-Vignette statt HUD, Zeitlupe bei entscheidenden Kills | `Scripts/Presentation/Feedback` |
| Feel-Presets | Schwer, Mittel, Tödlich (alle Zahlen in ScriptableObjects) | `Data/Combat` |
| Rahmen | Hauptmenü, Pausemenü, Steuerung mit Tastenänderung, Optionen, Tutorial | `Scripts/Presentation/Menu`, `Player/TutorialDirector` |
| Weitere Szenen | `M0_Sandbox` (20 gegen 20 aus der Draufsicht), `Proto_Animation` (Oben-Hieb), `Proto_Style` (Look) | `Scenes/` |
| Asset-Pipeline | Low-Poly-Modelle aus JSON-Specs per Headless-Blender, Palette, Validator, Kontaktbogen | [art](../art/README.md), [tools/assetgen](../tools/assetgen/README.md) |

Schadenswerte (Preset Mittel, stehendes Ziel): Schwert oben 63, Seite 33, Stich 22. Zweihänder oben 69, Seite 47, Stich 39, Sprung-Hieb aus 2,6 m 92. Leben 100.

## Testen

| Was | Befehl / Vorgehen |
|---|---|
| Alle EditMode-Tests (85, inkl. Determinismus) | `.\sb.ps1 test` (Unity darf offen bleiben); in Unity: Test Runner. [Was welche Klasse prüft](../game/Assets/Tests/EditMode/README.md) |
| Build-Smoke-Test mit Screenshot | Hauptmenü: `SHIELDBREAK.exe -shot out.png -shotDelay 2`. Duell: `-shotScene M1_Duel -duelSpectator -duelOpponents 2 -duelBotWeapon -1 -duelHeroWeapon 1 -shot out.png -shotDelay 4`. Tutorial: `-shotScene M1_Duel -duelTutorial` |
| Assets reproduzierbar | `.\sb.ps1 assets` (baut und prüft) |

**Von Hand prüfen (Rahmen):** Hauptmenü → Tutorial durchspielen (F12 überspringt), Esc-Menü öffnen, eine Taste umlegen und prüfen, dass sie nach Neustart gilt, Optionen ändern und im Duell merken.

**Von Hand prüfen (Fun-Gate M1):** 10 Duelle pro Preset (F6) gegen L3, dann die Frage, welches Preset man zum elften Mal spielen will. Dabei achten auf: Treffer fühlen sich wuchtig an, Ausholen ist lesbar, Perfect Block ist erreichbar, Zweihänder gegen Schild fühlt sich anders an, 1 gegen 3 (F10) fühlt sich mächtig, aber bedroht an, die Sounds sind erträglich.

## Offen

- **Fun-Gate M1** (Tarik) und Bewertung des prozeduralen Hiebs aus M0 (`Proto_Animation`)
- **Sprung (Jump)** aus dem Vokabular fehlt noch
- Sounds sind Platzhalter (M5), Figuren sind Gliederpuppen aus der Pipeline (Synty-Vergleich offen, `DECISIONS.md`)
- Bogen und Lanze: zwei Grammatik-Erweiterungen geplant, siehe [Papier-Test](combat/papier-test-bogen-lanze.md)

## Nächster Meilenstein

**M2 Kompanie:** Formationen mit Slots, `FormationOrder`-API, Radialmenü, Kommandoansicht mit Drag-Befehlen, 20 gegen 20, Moral, Rout (CLAUDE.md Abschnitt 6).
