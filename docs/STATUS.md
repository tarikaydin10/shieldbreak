# Stand: was es gibt, wie man es spielt und testet

> Wird nach jeder Iteration im selben Commit aktualisiert (CLAUDE.md, Arbeitsregeln). Stand: **2026-10-19**, Meilenstein **M2 Kompanie**, Task 3 von 8 fertig: Formationen finden Wege um Hindernisse (A* auf dem Grid), Soldaten weichen Wänden aus (in Unity noch nicht angesehen). **M1 Duell ist abgeschlossen**: Fun-Gate am 2026-10-19 bestanden (Tarik: „es macht Spaß“).

## Sofort spielen

| Weg | Was tun |
|---|---|
| Build | `.\sb.ps1 play` (baut, wenn nötig, und startet `builds\M1-duel\SHIELDBREAK.exe`; `-Fresh` baut immer neu). Startet im **Hauptmenü** |
| Unity | `game/` im Unity Hub öffnen, Szene `Assets/_Project/Scenes/MainMenu` (oder direkt `M1_Duel`) öffnen, Play |

**Hauptmenü:** Duell spielen (Kampfgefühl, Gegner-Stufe, Gegnerzahl, Waffen wählen), Tutorial (14 Schritte, jeder schließt durch Tun ab), Zuschauen, Sandbox, Prototypen, Steuerung (Tasten ändern, bleibt gespeichert), Optionen (Maus, Lautstärke, Zeitlupe, Angriffs-Steuerung).
**Im Duell:** Beim Start steht 4 s lang, was läuft (Preset · Gegner-Stufe · Anzahl · Waffe). Nach dem Kampf kommt ein Ergebnisbildschirm mit Statistik und drei Buttons: Nochmal, Anders einstellen, Hauptmenü. Leben = roter Bildrand (wächst mit dem Schaden), Ausdauer = dunkler Puls am Rand; Lebensbalken nur per F9.
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
| C (X) | Sprung: in der Luft nichts möglich, tiefe Hiebe verfehlen die Beine |
| E (Y) | Special: Schildstoß (Schwert + Schild) oder Sprung-Hieb (Zweihänder) |
| 1–4, mittlere Maustaste (Steuerkreuz hoch) | Waffe wechseln: Slot 1 Schwert + Schild, Slot 2 Zweihänder |
| Tab | Steuerung umschalten: Einfach (leicht/schwer) oder Richtungen (Maus) |
| F5 / F6 / F7 | neu starten / Kampfgefühl (Schwer, Mittel, Tödlich) / Gegner-Stufe L1–L5 |
| F8 / F9 / F10 / F11 | Zuschauen (Bot gegen Bot) / Lebensbalken (Debug) / 1–3 Gegner / Gegner-Waffe |
| Z, N (bei Pause) | Zeitlupe, ein Tick weiter |
| F12 (Tutorial) | Schritt überspringen |

![Tutorial: geführtes Duell](screenshots/tutorial.jpg)
![Nach dem Kampf: Ergebnis, Statistik, Auswahl](screenshots/duel_result.jpg)

## Formationen in der Sandbox (`M0_Sandbox`)

Hauptmenü → Sandbox. Vier Formationen: A Linie, A Schildwall (rot) gegen B Keil, B Linie (blau). Im Norden eine Mauer mit 6 m breitem Tor, im Süden ein Fels, hinter Team B Gestrüpp (begehbar, aber teuer). Die Zeit läuft; Esc pausiert.

| Taste | Aktion |
|---|---|
| Linksklick auf einen Soldaten / Rahmen ziehen | Formation(en) auswählen (ein Soldat wählt seine ganze Formation); Shift fügt hinzu; 1 / 2 / 3 = Team A / Team B / alle |
| Rechtsklick | Auswahl marschiert dorthin, Blick in Laufrichtung, Breite bleibt |
| Rechts ziehen | Frontlinie zeichnen: Position, Breite und Blickrichtung (weg von dort, wo die Truppe steht); mehrere Formationen teilen sich die Linie nebeneinander |
| 4 / 5 / 6 / 7 / 8 | Form: Linie / Schildwall / Keil / Kreis / locker |
| H | anhalten, wo sie gerade steht |
| F / G / T / V | Overlay: Formationen / Grid / Ziele / Geschwindigkeit |
| WASD, Q/E, Mausrad, mittlere Maustaste | Kamera |

Im Overlay: Kreuz = Slot (hell bei Auswahl), Pfeil = Anker und Blickrichtung, weiße Linie = geplanter Weg mit Wegpunkten, am Ende die Front, die dort entsteht; Beschriftung = Typ · Soldaten · Zustand. Rote Kästchen mit Kreuz = gesperrt, gelbe Schraffur = Gestrüpp.

Marschieren: Auf Wegen ab 8 m blickt die Formation in Marschrichtung (der Körper folgt dem Anker um Ecken) und dreht am Ziel in die befohlene Richtung. Kleine Drehungen schwenkt sie (die Flügel laufen höchstens 1 m/s), Drehungen über 60° formt sie um (hinteres Glied wird Front).

## Was existiert

| Bereich | Inhalt | Wo |
|---|---|---|
| Sim-Kern | 60-Hz-Fixed-Step, `InputQueue` (auch im Singleplayer), `SimRandom`, Hash; deterministisch, Lockstep-tauglich | `Scripts/Sim/Core` |
| Grid, Agenten | Spatial Hash per Counting-Sort, Steering mit Trennung und Ausweichen an Wänden (nie in gesperrte Zellen); Hindernisse und Gelände-Kosten pro 1-m-Zelle; 500 Agenten 0,12 ms pro Tick (vor dem Wand-Ausweichen gemessen) | `Scripts/Sim/Grid`, `Agents`, `Nav` |
| Kampf-Grammatik | Ausholen → Treffen → Erholen, Finte, Morph, Eingabepuffer, ein Block im Frontbogen, Perfect Block, Guard Break, Ausweichschritt, Sprung, Sprint, Waffenwechsel, Rückstoß, Energie-Schaden über Klingenbahn (SwingTrace) | [Sim/Combat](../game/Assets/_Project/Scripts/Sim/Combat/README.md) |
| Waffen | **Schwert + Schild** (Schild 120°, Schildstoß) und **Zweihänder** (90° Block, Sprung-Hieb, trifft härter, drückt Blocker stark) | `Data/Weapons`, `Data/Attacks` |
| Formationen (M2) | Linie, Schildwall, Keil, Kreis, lockere Ordnung; Slots mit optimaler Zuordnung (kurze Wege, stabil), Marsch mit Zusammenhalt auf einem A*-Weg um Hindernisse (breite Fronten meiden enge Lücken), Schwenken und Umdrehen, Lücken schließen nach Verlusten; `FormationOrder` (Bewegen/Ausrichten, Form, Breite) über die `InputQueue`; Frontlinien-Planer für mehrere Formationen; in der Sandbox sichtbar und per Maus befehligbar | `Scripts/Sim/Formations`, `Sim/Orders`, `Data/FormationDefinition`, `Debug/` |
| Bots | Stufen L1–L5 spielen dieselben Regeln, nur Reaktionszeit, Fehlerquote und Techniken unterscheiden sich; höchstens 2 greifen gleichzeitig an | `Scripts/Sim/AI` |
| Kampfgefühl | Hitstop, Kamera-Kick, Rückstoß, Blut/Funken/Splitter, Platzhalter-Sounds (synthetisch), Leben als roter Rand und Ausdauer als dunkler Puls statt HUD, Zeitlupe bei entscheidenden Kills | `Scripts/Presentation/Feedback` |
| Feel-Presets | Schwer, Mittel, Tödlich (alle Zahlen in ScriptableObjects) | `Data/Combat` |
| Rahmen | Hauptmenü, Pausemenü, Steuerung mit Tastenänderung, Optionen, Tutorial, Ergebnisbildschirm mit Statistik | `Scripts/Presentation/Menu`, `Player/TutorialDirector` |
| Weitere Szenen | `M0_Sandbox` (4 Formationen, 20 gegen 20 aus der Draufsicht, ohne Kampf), `Proto_Animation` (Oben-Hieb), `Proto_Style` (Look) | `Scenes/` |
| Asset-Pipeline | Low-Poly-Modelle aus JSON-Specs per Headless-Blender, Palette, Validator, Kontaktbogen | [art](../art/README.md), [tools/assetgen](../tools/assetgen/README.md) |

Schadenswerte (Preset Mittel, stehendes Ziel): Schwert oben 63, Seite 33, Stich 22. Zweihänder oben 69, Seite 47, Stich 39, Sprung-Hieb aus 2,6 m 92. Leben 100.

## Testen

| Was | Befehl / Vorgehen |
|---|---|
| Alle EditMode-Tests (128, inkl. Determinismus) | `.\sb.ps1 test` (Unity darf offen bleiben); in Unity: Test Runner. [Was welche Klasse prüft](../game/Assets/Tests/EditMode/README.md) |
| Build-Smoke-Test mit Screenshot | Hauptmenü: `SHIELDBREAK.exe -shot out.png -shotDelay 2`. Duell: `-shotScene M1_Duel -duelSpectator -duelOpponents 2 -duelBotWeapon -1 -duelHeroWeapon 1 -shot out.png -shotDelay 4`. Tutorial: `-shotScene M1_Duel -duelTutorial` |
| Assets reproduzierbar | `.\sb.ps1 assets` (baut und prüft) |
| Sim-Tests ohne Unity (Cloud, Linux) | `tools/simcheck/run.sh`: Sim + 63 Unity-freie Tests mit .NET 8, [Details](../tools/simcheck/README.md) |

**Von Hand prüfen (Rahmen):** Hauptmenü → Tutorial durchspielen (F12 überspringt), Esc-Menü öffnen, eine Taste umlegen und prüfen, dass sie nach Neustart gilt, Optionen ändern und im Duell merken.

**Fun-Gate M1:** bestanden am 2026-10-19.

**Von Hand prüfen (Formationen, Sandbox):** eine Formation anklicken und rechts klicken (marschiert als Block, Linie bleibt Linie), eine Linie hinter die Truppe ziehen (dreht um, hinteres Glied wird Front), beide A-Formationen wählen und eine lange Linie ziehen (teilen sich die Linie, kein Kreuzen), 4–8 durchschalten (formt auf der Stelle um), eine Formation durch das Tor in der Mauer schicken (Weg im Overlay, alle kommen durch), eine breite Linie hinter den Fels ziehen (läuft außen herum).

## Offen

- Feinschliff an Sprint-Angriff und Sprung (Tarik, nach erstem Anspielen)
- **Perfect Block zu stark** (Tarik, 2026-10-18): damit gewinnt man gegen L5. **Plan beschlossen (2026-10-19), noch nicht umgesetzt:**
  1. **Bots antworten mit Finten:** Bots merken sich, wie oft ihr Gegner perfekt blockt, und finten dann häufiger (Säule 4: Antwort über dieselbe Grammatik, kein Stat-Cheat). Die Stärke der Reaktion wird ein Feld pro Bot-Stufe in `BotRosterDefinition` (neben `feintChance`)
  2. **Fenster kleiner:** `perfectBlockMs` in `CombatTuningDefinition` von 250 auf 150 ms, pro Feel-Preset tunebar
  3. Nachweis per EditMode-Test: L5-Bot gewinnt gegen einen Bot, der nur Perfect Blocks versucht
  - Verworfen für jetzt: Ausdauerkosten für Perfect Block, kürzerer Stun danach (bleiben Reserve, falls 1 und 2 nicht reichen)

- Welches Feel-Preset (Schwer, Mittel, Tödlich) Standard wird, und die Bewertung des prozeduralen Hiebs aus M0 (`Proto_Animation`) (Tarik)
- Formationen: Umformen einer Formation mit 150 Soldaten kostet einen Tick lang ~14 ms (ohne Burst gemessen; bei 25–40 pro Formation < 0,3 ms). Falls in M4 große Formationen hängen: Lücken nach Verlusten lokal füllen statt komplett neu zuordnen
- Formationen im Kampf: Bots verlassen ihren Slot, sobald sie einen Gegner sehen (Task 4: Kampf in Formation)
- Formationen und Hindernisse: Beim Umformen laufen Soldaten geradeaus zum neuen Slot, ein Slot hinter einer Wand wird erst erreicht, wenn die Formation weiterzieht (nur lokales Steering, CLAUDE.md 3.6). Hindernisse sind nur im Overlay sichtbar (keine Meshes vor M4)
- Sounds sind Platzhalter (M5), Figuren sind Gliederpuppen aus der Pipeline (Synty-Vergleich offen, `DECISIONS.md`)
- Bogen und Lanze: zwei Grammatik-Erweiterungen geplant, siehe [Papier-Test](combat/papier-test-bogen-lanze.md)

## Aktueller Meilenstein

**M2 Kompanie** (Taskliste in CLAUDE.md, Abschnitt 6): ✅ 1 Formationen in der Sim · ✅ 2 Sichtbar machen (Sandbox, Overlay, Befehle per Maus) · ✅ 3 Formations-Pfad (A*) · 4 Behaviors + Kampf in Formation, 20 gegen 20 · 5 Moral und Rout · 6 Radialmenü · 7 Kommandoansicht · 8 Szene `M2_Company`, Build, Fun-Gate.
