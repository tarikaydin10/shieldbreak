# Stand: was es gibt, wie man es spielt und testet

> Wird nach jeder Iteration im selben Commit aktualisiert (CLAUDE.md, Arbeitsregeln). Stand: **2026-10-20**, Meilenstein **M2 Kompanie**, Task 5 von 8 fertig: Formationen haben Verhalten (Halten, Vorrücken, Angriff, Folgen, Rückzug), kämpfen aus der Formation, und Soldaten brechen (Panik-Feld, Moral, Rout und Flucht); die Sandbox ist ein Gefecht 20 gegen 20 (Task 4 und 5 in Unity noch nicht angesehen). **M1 Duell ist abgeschlossen**: Fun-Gate am 2026-10-19 bestanden (Tarik: „es macht Spaß“).

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

Hauptmenü → Sandbox. Vier Formationen zu je 10 Soldaten: A Linie, A Schildwall (rot) gegen B Keil, B Linie (blau), alle Bots Stufe 3 mit Schwert und Schild, gezeigt als Kampf-Figuren. Sie stehen, bis sie einen Befehl bekommen; wer an den Feind gerät, kämpft. Im Norden eine Mauer mit 6 m breitem Tor, im Süden ein Fels, hinter Team B Gestrüpp (begehbar, aber teuer). Die Zeit läuft; Esc pausiert.

| Taste | Aktion |
|---|---|
| Linksklick auf einen Soldaten / Rahmen ziehen | Formation(en) auswählen (ein Soldat wählt seine ganze Formation); Shift fügt hinzu; 1 / 2 / 3 = Team A / Team B / alle |
| Rechtsklick | Auswahl marschiert dorthin, Blick in Laufrichtung, Breite bleibt |
| Rechts ziehen | Frontlinie zeichnen: Position, Breite und Blickrichtung (weg von dort, wo die Truppe steht); mehrere Formationen teilen sich die Linie nebeneinander |
| 4 / 5 / 6 / 7 / 8 | Form: Linie / Schildwall / Keil / Kreis / locker |
| H | **Halten:** bleibt stehen, wo sie gerade ist; kämpft nur, wenn ein Feind vor die Front kommt |
| R | **Vorrücken:** marschiert geschlossen zur nächsten feindlichen Formation, hält 1,5 m davor und kämpft aus der Formation; folgt dem Feind, wenn er ausweicht |
| C | **Angriff:** stürmt auf den nächsten Feind, die Soldaten verlassen ihre Plätze und kämpfen frei |
| X | **Rückzug:** zieht sich 20 m zurück, ohne zu kämpfen, dann Halten mit Front zum Feind |
| F / G / T / V / M | Overlay: Formationen / Grid / Ziele / Geschwindigkeit / Moral |
| WASD, Q/E, Mausrad, mittlere Maustaste | Kamera |

Im Overlay: Kreuz = Slot (hell bei Auswahl), Pfeil = Anker und Blickrichtung, weiße Linie = geplanter Weg mit Wegpunkten, am Ende die Front, die dort entsteht; Beschriftung = Typ · Soldaten · Verhalten (hält, marschiert, rückt vor, greift an, folgt, zieht sich zurück). Rote Kästchen mit Kreuz = gesperrt, gelbe Schraffur = Gestrüpp.

Kampf in Formation: Beim Halten, Vorrücken und Folgen bleiben die Soldaten an einer 2-m-Leine um ihren Platz, kämpfen nach vorn (120°-Bogen), zur Seite und nach hinten nur gegen Feinde, die näher als 1,6 m kommen; das zweite Glied rückt in Lücken nach. Beim Angriff kämpfen sie frei, beim Rückzug gar nicht. Ein Schildwall blockt, statt auszuweichen. Folgen geht schon über die Befehls-API, aber noch ohne Taste: es braucht den Helden (Task 6 und 8).

**Moral:** Wer stirbt, erschreckt die Seinen in der Nähe; Panik breitet sich aus und klingt ab. Treffer von der Seite oder von hinten erschrecken, Fliehende stecken an. Kills und ein brechender Feind ermutigen. Unter 25 % Moral bricht ein Soldat: Er verlässt seine Formation, wehrt sich nicht mehr, rennt vom Feind weg und verlässt am Kartenrand das Feld. In einer haltenden, vorrückenden oder folgenden Formation sinkt die Moral langsamer als im Sturmangriff oder allein. Overlay (M): Panik pro Team als rote bzw. lila Fläche, Zuversicht grün, ein Balken unter jedem Soldaten, der nicht mehr voll bei Mut ist, Fliehende mit orangem Ring; die Beschriftung zeigt die Moral der Formation. Der Held beruhigt seine Seite im Umkreis von 8 m (kommt mit Task 7 und 8, die Sandbox hat keinen Helden).

Gemessen ohne Unity mit Moral (20 gegen 20, Stufe 3, je 10 Gefechte): Die Verlierer brechen nach 7–9 Toten (35–45 %), die übrigen fliehen; die Sieger behalten meist 13–16 Kämpfer. Ein Gefecht ist nach ~30 s entschieden (ohne Moral nach ~45 s, fast bis zum letzten Mann). Halten gegen Angriff 8:2 (ohne Moral 5:5), Vorrücken gegen Vorrücken ohne festen Sieger.

Gemessen ohne Unity, noch ohne Moral (je 10 Gefechte, 20 gegen 20, Stufe 3; Siege A:B):

| Gefecht | A:B |
|---|---|
| Halten gegen Vorrücken, Halten gegen Angriff, Angriff gegen Vorrücken | je 5:5 |
| Schildwall 8 m breit (hält) gegen Linie 11 m (rückt vor) | 5:5 |
| Schildwall 11 m breit gegen Linie 11 m | 9:1 |
| Linie 8 m breit gegen Linie 11 m | 1:9 |
| Stufe 5 gegen Stufe 3 | 10:0 |

Marschieren: Auf Wegen ab 8 m blickt die Formation in Marschrichtung (der Körper folgt dem Anker um Ecken) und dreht am Ziel in die befohlene Richtung. Kleine Drehungen schwenkt sie (die Flügel laufen höchstens 1 m/s), Drehungen über 60° formt sie um (hinteres Glied wird Front).

## Was existiert

| Bereich | Inhalt | Wo |
|---|---|---|
| Sim-Kern | 60-Hz-Fixed-Step, `InputQueue` (auch im Singleplayer), `SimRandom`, Hash; deterministisch, Lockstep-tauglich | `Scripts/Sim/Core` |
| Grid, Agenten | Spatial Hash per Counting-Sort, Steering mit Trennung und Ausweichen an Wänden (nie in gesperrte Zellen); Hindernisse und Gelände-Kosten pro 1-m-Zelle; 500 Agenten 0,12 ms pro Tick (vor dem Wand-Ausweichen gemessen) | `Scripts/Sim/Grid`, `Agents`, `Nav` |
| Kampf-Grammatik | Ausholen → Treffen → Erholen, Finte, Morph, Eingabepuffer, ein Block im Frontbogen, Perfect Block, Guard Break, Ausweichschritt, Sprung, Sprint, Waffenwechsel, Rückstoß, Energie-Schaden über Klingenbahn (SwingTrace) | [Sim/Combat](../game/Assets/_Project/Scripts/Sim/Combat/README.md) |
| Waffen | **Schwert + Schild** (Schild 120°, Schildstoß) und **Zweihänder** (90° Block, Sprung-Hieb, trifft härter, drückt Blocker stark) | `Data/Weapons`, `Data/Attacks` |
| Formationen (M2) | Linie, Schildwall, Keil, Kreis, lockere Ordnung; Slots mit optimaler Zuordnung (kurze Wege, stabil), Marsch mit Zusammenhalt auf einem A*-Weg um Hindernisse (breite Fronten meiden enge Lücken), Schwenken und Umdrehen, Lücken schließen nach Verlusten; `FormationOrder` (Bewegen/Ausrichten, Form, Breite, Verhalten) über die `InputQueue`; Verhalten Halten, Vorrücken, Angriff, Folgen, Rückzug; Frontlinien-Planer für mehrere Formationen; in der Sandbox sichtbar und per Maus und Tasten befehligbar | `Scripts/Sim/Formations`, `Sim/Orders`, `Data/FormationDefinition`, `Debug/` |
| Kampf in Formation (M2) | Leine um den Slot, Kampf nach vorn, Selbstverteidigung, Schildwall blockt statt auszuweichen, Angriff löst die Leine, Rückzug kämpft nicht; Bots spielen dabei unverändert ihre Stufe | [Sim/Combat/Context](../game/Assets/_Project/Scripts/Sim/Combat/README.md) |
| Moral (M2) | Panik-Feld pro Team mit Diffusion (Zuversicht als negativer Wert), Quellen und Senken, Moral pro Soldat, Rout, Flucht bis zum Kartenrand; im Duell aus | `Scripts/Sim/Morale`, `Data/MoraleDefinition`, `Data/Morale/Morale_Default` |
| Bots | Stufen L1–L5 spielen dieselben Regeln, nur Reaktionszeit, Fehlerquote und Techniken unterscheiden sich; höchstens 2 greifen gleichzeitig an | `Scripts/Sim/AI` |
| Kampfgefühl | Hitstop, Kamera-Kick, Rückstoß, Blut/Funken/Splitter, Platzhalter-Sounds (synthetisch), Leben als roter Rand und Ausdauer als dunkler Puls statt HUD, Zeitlupe bei entscheidenden Kills | `Scripts/Presentation/Feedback` |
| Feel-Presets | Schwer, Mittel, Tödlich (alle Zahlen in ScriptableObjects) | `Data/Combat` |
| Rahmen | Hauptmenü, Pausemenü, Steuerung mit Tastenänderung, Optionen, Tutorial, Ergebnisbildschirm mit Statistik | `Scripts/Presentation/Menu`, `Player/TutorialDirector` |
| Weitere Szenen | `M0_Sandbox` (4 Formationen, 20 gegen 20 aus der Draufsicht, mit Kampf), `Proto_Animation` (Oben-Hieb), `Proto_Style` (Look) | `Scenes/` |
| Asset-Pipeline | Low-Poly-Modelle aus JSON-Specs per Headless-Blender, Palette, Validator, Kontaktbogen | [art](../art/README.md), [tools/assetgen](../tools/assetgen/README.md) |

Schadenswerte (Preset Mittel, stehendes Ziel): Schwert oben 63, Seite 33, Stich 22. Zweihänder oben 69, Seite 47, Stich 39, Sprung-Hieb aus 2,6 m 92. Leben 100.

## Testen

| Was | Befehl / Vorgehen |
|---|---|
| Alle EditMode-Tests (157, inkl. Determinismus) | `.\sb.ps1 test` (Unity darf offen bleiben); in Unity: Test Runner. [Was welche Klasse prüft](../game/Assets/Tests/EditMode/README.md) |
| Build-Smoke-Test mit Screenshot | Hauptmenü: `SHIELDBREAK.exe -shot out.png -shotDelay 2`. Duell: `-shotScene M1_Duel -duelSpectator -duelOpponents 2 -duelBotWeapon -1 -duelHeroWeapon 1 -shot out.png -shotDelay 4`. Tutorial: `-shotScene M1_Duel -duelTutorial` |
| Assets reproduzierbar | `.\sb.ps1 assets` (baut und prüft) |
| Sim- und Kampf-Tests ohne Unity (Cloud, Linux) | `tools/simcheck/run.sh`: Sim, Definitionen und 149 Tests (alle Kampf-, Formations- und Moral-Tests) mit .NET 8, [Details](../tools/simcheck/README.md) |

**Von Hand prüfen (Rahmen):** Hauptmenü → Tutorial durchspielen (F12 überspringt), Esc-Menü öffnen, eine Taste umlegen und prüfen, dass sie nach Neustart gilt, Optionen ändern und im Duell merken.

**Fun-Gate M1:** bestanden am 2026-10-19.

**Von Hand prüfen (Formationen, Sandbox):** eine Formation anklicken und rechts klicken (marschiert als Block, Linie bleibt Linie), eine Linie hinter die Truppe ziehen (dreht um, hinteres Glied wird Front), beide A-Formationen wählen und eine lange Linie ziehen (teilen sich die Linie, kein Kreuzen), 4–8 durchschalten (formt auf der Stelle um), eine Formation durch das Tor in der Mauer schicken (Weg im Overlay, alle kommen durch), eine breite Linie hinter den Fels ziehen (läuft außen herum).

**Von Hand prüfen (Kampf, Sandbox):** 1 und R (Team A rückt vor, Team B hält): die Linien treffen sich, kämpfen, das zweite Glied rückt nach, die Formationen bleiben erkennbar. Dann 2 und C (Team B greift an): die blauen Soldaten lösen sich und stürmen. Eine Formation mitten im Kampf mit X zurückziehen: sie dreht ab, kämpft nicht mehr und steht 20 m weiter mit Front zum Feind. Läuft das flüssig (F1 zeigt FPS und Sim-Zeit)?

**Von Hand prüfen (Moral, Sandbox):** 1 und R, dann 2 und R (beide rücken vor), M für das Moral-Overlay. Rote und lila Flecken entstehen, wo Soldaten fallen, grüne, wo sie siegen. Bricht eine Seite, bevor sie ausgelöscht ist? Laufen die Fliehenden sichtbar weg (oranger Ring) und verschwinden am Rand? Fühlt sich der Bruch verdient an, oder kommt er zu früh oder zu spät? Die Werte stehen in `Data/Morale/Morale_Default`.

## Offen

- Feinschliff an Sprint-Angriff und Sprung (Tarik, nach erstem Anspielen)
- **Perfect Block zu stark** (Tarik, 2026-10-18): damit gewinnt man gegen L5. **Plan beschlossen (2026-10-19), noch nicht umgesetzt:**
  1. **Bots antworten mit Finten:** Bots merken sich, wie oft ihr Gegner perfekt blockt, und finten dann häufiger (Säule 4: Antwort über dieselbe Grammatik, kein Stat-Cheat). Die Stärke der Reaktion wird ein Feld pro Bot-Stufe in `BotRosterDefinition` (neben `feintChance`)
  2. **Fenster kleiner:** `perfectBlockMs` in `CombatTuningDefinition` von 250 auf 150 ms, pro Feel-Preset tunebar
  3. Nachweis per EditMode-Test: L5-Bot gewinnt gegen einen Bot, der nur Perfect Blocks versucht
  - Verworfen für jetzt: Ausdauerkosten für Perfect Block, kürzerer Stun danach (bleiben Reserve, falls 1 und 2 nicht reichen)

- Welches Feel-Preset (Schwer, Mittel, Tödlich) Standard wird, und die Bewertung des prozeduralen Hiebs aus M0 (`Proto_Animation`) (Tarik)
- Formationen: Umformen einer Formation mit 150 Soldaten kostet einen Tick lang ~14 ms (ohne Burst gemessen; bei 25–40 pro Formation < 0,3 ms). Falls in M4 große Formationen hängen: Lücken nach Verlusten lokal füllen statt komplett neu zuordnen
- **Schildwall (Tarik):** Er wird nur durch Dichte stark (gleich breit wie die Linie gewinnt er 9:1). Dauerhaft erhobene Schilde verlieren, weil der Schildstoß jeden Block bricht. Soll der Schildwall eine eigene Regel bekommen (z. B. verschränkte Schilde, die der Schildstoß nicht bricht)? Das wäre eine Design-Entscheidung, siehe `DECISIONS.md` 2026-10-20
- Kampf in der Sandbox: noch ohne Treffer-Effekte und Sounds (die gibt es im Duell), die Kamera bleibt die Draufsicht. Kommt mit Task 7 (Kommandoansicht) und 8 (`M2_Company`)
- Rückzug ist ein einfacher Rückzug (Rücken zum Feind); kämpfender Rückzug steht in `IDEAS.md`
- **Moral-Tuning (Tarik):** Wann bricht eine Seite? Startwerte und Messungen in `DECISIONS.md` 2026-10-20. Fliehende kommen in v1 nicht zurück (Sammeln steht in `IDEAS.md`). Flankentreffer erschrecken nur wenig zusätzlich (0,3); wer Flanken stärker spüren will, dreht dort
- Moral wirkt nur in der Sandbox; Duell und Tutorial bleiben ohne
- Fliehende suchen keinen Weg um Mauern, sie weichen nur lokal aus und gleiten an der Wand entlang (wie alle Soldaten, CLAUDE.md 3.6)
- Vorrücken und Angriff wählen die nächste feindliche Formation selbst; eine bestimmte Formation als Ziel kommt mit der Kommandoansicht
- Formationen und Hindernisse: Beim Umformen laufen Soldaten geradeaus zum neuen Slot, ein Slot hinter einer Wand wird erst erreicht, wenn die Formation weiterzieht (nur lokales Steering, CLAUDE.md 3.6). Hindernisse sind nur im Overlay sichtbar (keine Meshes vor M4)
- Sounds sind Platzhalter (M5), Figuren sind Gliederpuppen aus der Pipeline (Synty-Vergleich offen, `DECISIONS.md`)
- Bogen und Lanze: zwei Grammatik-Erweiterungen geplant, siehe [Papier-Test](combat/papier-test-bogen-lanze.md)

## Aktueller Meilenstein

**M2 Kompanie** (Taskliste in CLAUDE.md, Abschnitt 6): ✅ 1 Formationen in der Sim · ✅ 2 Sichtbar machen (Sandbox, Overlay, Befehle per Maus) · ✅ 3 Formations-Pfad (A*) · ✅ 4 Verhalten + Kampf in Formation, 20 gegen 20 · ✅ 5 Moral und Rout · 6 Radialmenü · 7 Kommandoansicht · 8 Szene `M2_Company`, Build, Fun-Gate.
