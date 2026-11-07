# Stand: was es gibt, wie man es spielt und testet

> Wird nach jeder Iteration im selben Commit aktualisiert (CLAUDE.md, Arbeitsregeln). Stand: **2026-11-07**, Meilenstein **M4 Schlacht** begonnen, **M3 Feldherr** ist fertig und das Fun-Gate bestanden: **Haltung** (Stellung halten, Reagieren, Aggressiv), **Bedrohungen**, ein **Kommandant für den Feind**, eine **Prognose** (Lanchester), **Bogenschützen** auf beiden Seiten und die **Schlacht 50 gegen 50**, die sich auch ganz von oben führen lässt. Soldaten fliehen deutlich später, und die zuerst erzeugte Seite hat keinen versteckten Vorteil mehr. Formationen greifen von selbst an, wenn ein Feind nahe kommt, auch eine Formation, die du hinter den Feind stellst, und sie drehen die Front, wenn der Feind in Flanke oder Rücken steht. Der Feind in der Kompanie entscheidet jetzt selbst: angreifen, flankieren, halten oder zurückweichen, sichtbar auf F3. Unter Beschuss bilden haltende Formationen den Schildwall. **M2 Kompanie ist abgeschlossen**: Fun-Gate am 2026-11-01 bestanden (Tarik: „alles in allem cool“). M1 Duell seit 2026-10-19.

## Sofort spielen

| Weg | Was tun |
|---|---|
| Build | `.\sb.ps1 play` (baut, wenn nötig, und startet `builds\M1-duel\SHIELDBREAK.exe`; `-Fresh` baut immer neu). Startet im **Hauptmenü** |
| Unity | `game/` im Unity Hub öffnen, Szene `Assets/_Project/Scenes/MainMenu` (oder direkt `M1_Duel`) öffnen, Play |
| Mac | `./sb.ps1 play` im Terminal (baut und startet `builds/M1-duel/SHIELDBREAK.app`); einmalig vorher Setup nach CLAUDE.md 10 (Homebrew, PowerShell, `./sb.ps1 setup`). Die Windows-exe läuft auf dem Mac nicht, sie wird weiter auf dem PC gebaut |

**Hauptmenü:** Duell spielen (Kampfgefühl, Gegner-Stufe, Gegnerzahl, Waffen wählen), Tutorial (14 Schritte, jeder schließt durch Tun ab), Zuschauen, Kompanie, **Schlacht 50 gegen 50**, **Testmodus** (alles mit Ziel, Steuerung, Prüfliste), Sandbox, Prototypen, Steuerung (Tasten ändern, bleibt gespeichert), Optionen (Maus, Lautstärke, Zeitlupe, Angriffs-Steuerung).
**Im Duell:** Beim Start steht 4 s lang, was läuft (Preset · Gegner-Stufe · Anzahl · Waffe). Nach dem Kampf kommt ein Ergebnisbildschirm mit Statistik und drei Buttons: Nochmal, Anders einstellen, Hauptmenü. Leben = roter Bildrand (wächst mit dem Schaden), Ausdauer = dunkler Puls am Rand; Lebensbalken nur per F9.
**Im Spiel:** **Esc** Pausemenü (Fortsetzen, Neu starten, Ziel und Prüfliste, Steuerung, Optionen, Hauptmenü), **F1** Status und Admin (alle Tasten, Szenenwechsel, FPS), **F2** Hilfe zur Szene (Ziel, Prüfliste, alle Tasten der Szene).

## Alles testen: Testmodus

Hauptmenü → **Testmodus**. Links alles, was es gibt, rechts für den gewählten Eintrag Ziel, Prüfliste (zum Abhaken) und Steuerung, oben „Starten“. In der Szene zeigt **F2** dasselbe, **Esc → Ziel und Prüfliste** hakt ab (bleibt pro Rechner gespeichert, „Geprüft: x von 42“ im Testmodus). Kompanie, Sandbox, Stresstest und Prototypen zeigen ihr Ziel beim Start 12 s lang oben rechts.

| Eintrag | Szene | Prüfpunkte |
|---|---|---|
| Duell, Tutorial, Zuschauen | `M1_Duel` | 6, 3, 3 |
| Kompanie, mit Kommandoansicht, Haltung, Feind-Kommandant, Schützen und Ergebnis | `M2_Company` | 25 |
| Schlacht: 50 gegen 50 | `M2_Company` mit `Battle_50v50` | 5 |
| Sandbox: Formationen und Moral | `M0_Sandbox` | 10 |
| Stresstest: 250 gegen 250 (vorher nicht erreichbar) | `M0_Sandbox` mit `Stress_250v250` | 3 |
| Prototyp: Oben-Hieb, Prototyp: Look und Kameras | `Proto_Animation`, `Proto_Style` | 4, 2 |

Vorschlag für jetzt (Task 4 bis 7 sind ungetestet): Sandbox, dann Kompanie, dann Stresstest; Duell und Tutorial kurz als Gegenprobe. Die Prüfpunkte stehen nur im Spiel (`game/Assets/_Project/Resources/SceneCatalog.asset`), nicht mehr hier; was dabei auffällt, einfach im Chat sagen.

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

## Kompanie mit Radialmenü und Kommandoansicht (`M2_Company`)

Hauptmenü → Kompanie. Der Held (Steuerung wie im Duell) steht zwischen seiner Linie (links) und seinem Schildwall (rechts), je 12 Bots Stufe 3, dazu 8 Bogenschützen hinter ihm. 36 m vor ihm stehen eine feindliche Linie und ein Keil, die sofort vorrücken, mit eigenen Schützen dahinter; nach etwa einer halben Minute treffen sie aufeinander. Moral ist an: Wer bricht, flieht. **Ende:** Sieg, sobald kein Feind mehr kämpft; Niederlage, wenn deine Truppe bricht oder der Held fällt (steht nur noch der Held, geht es weiter). 2,5 s danach kommt der Ergebnisbildschirm: Verluste beider Seiten (stehen, gefallen, geflohen), deine Treffer und Siege, Zeit in der Kommandoansicht, Buttons Nochmal und Hauptmenü. Neustart jederzeit über Esc → Neu starten.

| Taste (Gamepad) | Aktion |
|---|---|
| F halten (RB) | **Radialmenü** öffnen. Die Zeit läuft weiter; der Held kann laufen, aber nicht schlagen, blocken oder sich umsehen |
| Maus bewegen (rechter Stick) | Befehl wählen: oben Vorrücken, rechts oben Angriff, rechts Folgen (dem Helden), rechts unten Keil, unten Rückzug, links unten Schildwall, links Halten, links oben Linie |
| F loslassen (RB) | Befehl geben; in der Mitte loslassen bricht ab |
| Mausrad (LB) bei offenem Menü | Ziel wechseln: alle eigenen Formationen, Formation 1, Formation 2 |

Der gegebene Befehl steht kurz oben im Bild; F1 zeigt Ziel, letzten Befehl und den Zustand beider Seiten.

**Schützen:** Sie stehen in lockerer Ordnung und schießen bis 40 m im Bogen über die eigenen Reihen, ohne die eigenen Leute zu treffen. Das Verhalten **Abschirmen** ist ihr Standard: Sind sie zu weit weg, rücken sie auf; kommt der Feind näher als 14 m, fallen sie hinter deine Infanterie zurück. Steht ein Feind näher als 5 m, ziehen sie das Schwert. Andere Befehle gelten auch für sie, T holt sie ins Abschirmen zurück. Der Schild blockt Pfeile im Frontbogen für Ausdauer, einen Perfect Block gibt es gegen Pfeile nicht. Eine haltende Formation mit Schilden bildet nach 3 Pfeilen den Schildwall zu den Schützen hin und hält die Schilde oben, solange kein Feind nah ist; 5 s nach dem letzten Pfeil geht sie in ihre Form zurück. Marschierende Formationen tun das nicht. Bessere Bot-Stufen zielen genauer.

**Kommandoansicht** (V halten, Gamepad LB): Die Kamera fährt in einer halben Sekunde schräg über das Feld, die Zeit läuft weiter (Standard 0,3×, Optionen → Kommandoansicht, 1,00× = Echtzeit). Solange sie offen ist, gehört der Held dem Bot (Stufe L2): Er zieht sich 1,5 m hinter die eigene Linie zurück und wehrt sich selbst; ein gelber Ring und eine Linie zeigen ihn und seinen Posten. Jede Formation ist umrandet (blau eigene, gelb ausgewählt, rot Feind), mit Pfeil für die Front und beschriftet: Typ · Soldaten · Befehl, darunter ein Moral-Balken.

| Taste (Gamepad) | Aktion |
|---|---|
| V halten (LB) | Kommandoansicht; loslassen = zurück in den Kampf |
| Linksklick (A), Rahmen ziehen | eigene Formation wählen, Shift: dazu oder weg; ins Leere klicken hebt die Auswahl auf |
| Rechtsklick (RT) | Auswahl marschiert hierher (**ohne Auswahl: alle**) |
| Rechts ziehen (RT halten und Stick) | Frontlinie zeichnen: Position, Breite, Blickrichtung |
| Rechtsklick auf eine feindliche Formation | Auswahl rückt gegen genau diese vor (rote Linie zum Ziel) |
| 1–5 | Form: Linie, Schildwall, Keil, Kreis, locker |
| WASD (rechter Stick) | Blick über das Feld verschieben; zurück in den Kampf vergisst das |
| L (rechten Stick drücken) | Ansicht offen lassen, ohne V zu halten; V bringt dich zurück |
| T (X) | **Schützen** in der Auswahl (ohne Auswahl: alle) gehen zurück ins Abschirmen |
| R (Y) | **Haltung** der Auswahl (ohne Auswahl: alle) wechseln: Stellung halten → Reagieren → Aggressiv, steht in der Beschriftung |
| Mauszeiger auf einer feindlichen Formation | sie leuchtet auf, „Rechtsklick: angreifen“ |
| F halten (RB) | Radialmenü, befiehlt jetzt die Auswahl |
| Mausrad | Kamera höher oder tiefer (18–60 m) |
| linker Stick | Gamepad: Zeiger bewegen |

**Der Feind hat einen Kommandanten.** Er schaut alle 2,5 s aufs Feld und bewertet fünf Taktiken: Halten, Frontalangriff, Flanke links, Flanke rechts, Rückzug. Er liest nur, was du auch siehst (Stärke, Moral, Frontbreiten, Abstand, welche Seite offen ist), und befiehlt über dieselbe Befehls-API wie du. Die Lage schätzt er mit dem linearen Gesetz von Lanchester: Stärke mal Soldaten im Kontakt, dein Held zählt als 4, feindliche Soldaten hält er für Durchschnitt. Bei einer Flanke läuft seine äußere Formation ans Ende deiner Linie und greift dort von selbst an, die andere bindet dich. **F3** zeigt seine Taktik, die Scores aller Taktiken, was er liest, die Prognose (über 1 = er rechnet mit dem Sieg) und dieselbe Prognose für jede seiner Formationen gegen die nächste feindliche; die Taktik steht auch im F1-Menü und auf dem Ergebnisbildschirm.

## Schlacht 50 gegen 50 (`M2_Company` mit `Battle_50v50`)

Hauptmenü → Schlacht. Auf einer Karte von 140 m stehen je 50 Soldaten in fünf Formationen: zwei Linien, ein Schildwall, ein Keil in Reserve, Schützen. Es gelten alle Tasten der Kompanie. **Feldherr-Test:** V halten, L drücken, V loslassen: Die Ansicht bleibt offen, WASD verschiebt den Blick, dein Held kämpft solange allein von seinem Posten aus. Mit der Option Zeitlupe (Optionen → Kommandoansicht) lässt sich das Tempo wählen.

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

**Bedrohungen:** Eine Formation, die am Ziel hält, schaut alle 0,5 s, wer bis 8 m an ihren Umriss herankommt. Stehen mindestens 3 Feinde in Flanke oder Rücken und mehr als vor der Front, dreht sie sich dorthin, ohne ihren Platz zu verlassen; bei großen Drehungen wird das hintere Glied zur Front. Das gilt in jeder Haltung, auch bei Stellung halten. Danach schaut sie 3 s lang nicht mehr. Ein Angriff, den die Haltung ausgelöst hat, wechselt auf die neue Bedrohung. Ein befohlener Angriff und ein Marsch bleiben wie befohlen, nach jedem Befehl gilt ebenfalls die Pause von 3 s, und ein Kreis hat keine Flanke. Die Beschriftung nennt kurz die Seite („Feind links!“, „Feind rechts!“, „Feind im Rücken!“), in der Sandbox wie in der Kommandoansicht. Ein Feind vor dem Flügel einer breiten Linie zählt als Front, denn gemessen wird in den Achsen der Formation.

**Moral:** Wer stirbt, erschreckt die Seinen in der Nähe; Panik breitet sich aus und klingt ab. Treffer von der Seite oder von hinten erschrecken, Fliehende stecken an. Kills und ein brechender Feind ermutigen. Wer hoffnungslos unterlegen ist (weniger als 0,4 eigene Kämpfer pro Feind), verliert laufend Mut, auch ohne Panik; so stehen die letzten Reste nicht ewig. Unter 20 % Moral bricht ein Soldat: Er verlässt seine Formation, wehrt sich nicht mehr, rennt vom Feind weg und verlässt am Kartenrand das Feld. In einer haltenden, vorrückenden oder folgenden Formation sinkt die Moral langsamer als im Sturmangriff oder allein. Overlay (M): Panik pro Team als rote bzw. lila Fläche, Zuversicht grün, ein Balken unter jedem Soldaten, der nicht mehr voll bei Mut ist, Fliehende mit orangem Ring; die Beschriftung zeigt die Moral der Formation. Der Held beruhigt seine Seite im Umkreis von 8 m (in der Kompanie; die Sandbox hat keinen Helden).

Gemessen (Kompanie 24 gegen 24 mit Kommandant, 12 Schlachten): Beim Verlierer flieht der erste Soldat nach rund 13 Toten (53 %), rund 18 (74 %) sind am Ende tot; eine Schlacht dauert im Schnitt 59 s. Vor dem 2026-11-07 floh der erste nach 3 Toten.

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
| Radialmenü (M2) | Schnellbefehle aus dem Kampf an alle eigenen Formationen oder eine: Halten, Vorrücken, Angriff, Folgen, Rückzug, Linie, Schildwall, Keil; als `FormationOrder` über die `InputQueue` | `Scripts/Presentation/Player/RadialMenu`, `Sim/Orders/QuickOrders` |
| Kommandoansicht (M2) | V halten: Kamera hoch, Formationen lesbar (Umriss, Front, Befehl, Moral), Auswahl, Marsch und Frontlinie per Maus, Ziel per Rechtsklick auf den Feind, Formen 1–5, Radialmenü auf die Auswahl, Zeitlupe; der Held kämpft solange als Bot hinter der eigenen Linie (`HeroUnattended`, Sim-Input) | `Scripts/Presentation/Player/CommandView`, `Cameras/CommandViewCamera`, `Sim/Core/SimWorld` |
| Bedrohungen (M3) | Eine haltende Formation dreht die Front zu Feinden in Flanke oder Rücken, ein von selbst begonnener Angriff wechselt auf sie; mit Abklingzeit, Befehle gehen vor; Auslöser in Overlay und Kommandoansicht; Werte in `Data/Formations/Formation_Default` | `Sim/Formations/FormationSystem` (`FaceThreat`) |
| Haltung (M3) | Standing Order pro Formation: Stellung halten, Reagieren (Standard: rückt gegen Feinde vor, die bis 10 m herankommen, hält danach wieder), Aggressiv (ab 30 m, danach weiter); nur wenn die Formation hält und angekommen ist; „Halten“ im Radialmenü heißt auch Stellung halten; Werte in `Data/Formations/Formation_Default` | `Sim/Formations` (`FormationStance`, `React`), `Sim/Orders/FormationOrder`, `Player/CommandView` |
| Fernkampf (M3) | Archetyp Bogen auf derselben Grammatik, Pfeile als Sim-Structs mit Ballistik, Schild blockt Pfeile, Schwert als Zweitwaffe, Zielfehler pro Bot-Stufe, Verhalten Abschirmen, Deckung als Schildwall unter Beschuss; Pfeile bis M4 als einfache Quader; Schützen halten das Schwert-Modell, ein Bogen-Modell fehlt (keine Assets vor M4) | `Sim/Combat/Resolvers/Projectile`, `Sim/Combat/CombatSystem`, `Sim/AI/BotBrain`, `Sim/Formations/FormationSystem`, `Data/Weapons/Weapon_Bow` |
| Kommandant (M3) | Utility-AI für den Feind: Taktik alle 2,5 s aus Stärke, Moral, Frontbreiten und offener Seite, mit Hysterese, Mindestdauer und Ungeduld beim Halten; nur über `FormationOrder`; Scores auf F3; Werte in `Data/Commanders/Commander_Default` | `Sim/AI/CommanderUtility`, `CommanderSystem`, `Presentation/CommanderPanel` |
| Schlacht (M3) | 50 gegen 50 als Daten in der Kompanie-Szene; Kommandoansicht verschieben und offen lassen; 0,15 ms pro Tick, 151 FPS im Build | `Data/Scenarios/Battle_50v50`, `Cameras/CommandViewCamera` |
| Kompanie (M2) | Held mit zwei eigenen Formationen gegen zwei Bot-Formationen unter einem Kommandanten, mit Moral; Hitstop und Kamera-Kick wie im Duell; Ende der Schlacht (`Sim/Morale/BattleOutcome`: eine Seite bricht oder der Held fällt) und Ergebnisbildschirm | `Data/CompanyScenarioDefinition`, `Data/Scenarios/Company_M2`, `Scenes/M2_Company`, `Player/CompanyDirector` |
| Bots | Stufen L1–L5 spielen dieselben Regeln, nur Reaktionszeit, Fehlerquote und Techniken unterscheiden sich; höchstens 2 greifen gleichzeitig an | `Scripts/Sim/AI` |
| Kampfgefühl | Hitstop, Kamera-Kick, Rückstoß, Blut/Funken/Splitter, Platzhalter-Sounds (synthetisch), Leben als roter Rand und Ausdauer als dunkler Puls statt HUD, Zeitlupe bei entscheidenden Kills | `Scripts/Presentation/Feedback` |
| Feel-Presets | Schwer, Mittel, Tödlich (alle Zahlen in ScriptableObjects) | `Data/Combat` |
| Rahmen | Hauptmenü, Pausemenü, Steuerung mit Tastenänderung, Optionen, Tutorial, Ergebnisbildschirm mit Statistik | `Scripts/Presentation/Menu`, `Player/TutorialDirector` |
| Testmodus | Szenen-Katalog: jede Szene und jedes Szenario mit Ziel, Steuerung und Prüfliste; Testseite im Hauptmenü, Hilfe in der Szene (F2), Prüfliste im Pausemenü; Test, dass nichts unerreichbar ist | `Resources/SceneCatalog`, `Menu/TestPage`, `SceneBriefing`, `SceneLaunch` |
| Weitere Szenen | `M0_Sandbox` (4 Formationen, 20 gegen 20 aus der Draufsicht, mit Kampf; als Stresstest 500 lose Soldaten), `Proto_Animation` (Oben-Hieb), `Proto_Style` (Look) | `Scenes/` |
| Asset-Pipeline | Low-Poly-Modelle aus JSON-Specs per Headless-Blender, Palette, Validator, Kontaktbogen; seit 2026-11-01 das eigene Repo [assetforge](https://github.com/tarikaydin10/assetforge) als Submodul `tools/assetgen`, Specs und Generatoren in `art/` | [art](../art/README.md), [tools/assetgen](../tools/assetgen/README.md) |

Schadenswerte (Preset Mittel, stehendes Ziel): Schwert oben 63, Seite 33, Stich 22. Zweihänder oben 69, Seite 47, Stich 39, Sprung-Hieb aus 2,6 m 92. Leben 100.

## Testen

| Was | Befehl / Vorgehen |
|---|---|
| Alle EditMode-Tests (235, inkl. Determinismus) | `.\sb.ps1 test` (Mac: `./sb.ps1 test`; Unity darf offen bleiben); in Unity: Test Runner. [Was welche Klasse prüft](../game/Assets/Tests/EditMode/README.md) |
| Build-Smoke-Test mit Screenshot | Hauptmenü: `SHIELDBREAK.exe -shot out.png -shotDelay 2` (Mac: `SHIELDBREAK.app/Contents/MacOS/SHIELDBREAK -shot …`). Duell: `-shotScene M1_Duel -duelSpectator -duelOpponents 2 -duelBotWeapon -1 -duelHeroWeapon 1 -shot out.png -shotDelay 4`. Tutorial: `-shotScene M1_Duel -duelTutorial`. Katalog-Eintrag: `-shotEntry battle -shotDelay 50 -logFile x.log` (das Log nennt die Bildrate) |
| Assets reproduzierbar | `.\sb.ps1 assets` (baut und prüft) |
| Von Hand: alle Szenen | Hauptmenü → Testmodus (Prüflisten pro Szene, siehe oben) |
| Sim- und Kampf-Tests ohne Unity (Cloud, Linux) | `tools/simcheck/run.sh`: Sim, Definitionen und 161 Tests (alle Kampf-, Formations-, Moral-, Kompanie- und Kommandoansicht-Tests) mit .NET 8, [Details](../tools/simcheck/README.md) |

**Von Hand prüfen:** steht pro Szene als Prüfliste im Testmodus (Hauptmenü → Testmodus, in der Szene F2 und Esc → Ziel und Prüfliste). Dazu der Rahmen: eine Taste umlegen und prüfen, dass sie nach Neustart gilt; Optionen ändern und im Duell merken.

**Fun-Gate M1:** bestanden am 2026-10-19. **Fun-Gate M2:** bestanden am 2026-11-01.

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
- Kampf in der Sandbox: ohne Treffer-Effekte und Sounds (die gibt es im Duell und in der Kompanie), die Kamera bleibt die Draufsicht
- Rückzug ist ein einfacher Rückzug (Rücken zum Feind); kämpfender Rückzug steht in `IDEAS.md`
- **Moral-Tuning (Tarik):** Wann bricht eine Seite? Am 2026-11-07 nach deiner Rückmeldung deutlich später gestellt, Messwerte in `DECISIONS.md`. Stellschrauben: `panicDrain`, `routThreshold`, `hopelessRatio` in `Morale_Default`. Fliehende kommen in v1 nicht zurück (Sammeln steht in `IDEAS.md`). Flankentreffer erschrecken nur wenig zusätzlich (0,3); wer Flanken stärker spüren will, dreht dort
- Moral wirkt in Sandbox und Kompanie; Duell und Tutorial bleiben ohne
- **Radialmenü (Tarik):** Taste F und RB, Belegung der acht Richtungen und das Ziel per Mausrad sind ein erster Vorschlag (`DECISIONS.md` 2026-10-21). Kreis und lockere Ordnung fehlen im Ring (in der Kommandoansicht mit 4 und 5)
- Kompanie: Wer als Held fällt, verliert; ob man danach lieber als Zuschauer weiterschauen will, zeigt das Fun-Gate (Tarik). Die Szene ist von Hand aus Duell- und Sandbox-Szene zusammengesetzt; falls sie in Unity nicht lädt: Menü Shieldbreak → Build M2 Company Scene baut sie neu
- Fliehende suchen keinen Weg um Mauern, sie weichen nur lokal aus und gleiten an der Wand entlang (wie alle Soldaten, CLAUDE.md 3.6)
- Vorrücken und Angriff wählen die nächste feindliche Formation selbst, außer man zeigt in der Kommandoansicht per Rechtsklick auf eine. Freier Angriff auf ein bestimmtes Ziel: erst Rechtsklick auf den Feind, dann Radialmenü → Angriff (das Ziel bleibt)
- **Kommandoansicht (Tarik):** Taste V/LB, Höhe 35 m, Blickwinkel, Zeitlupe 0,3× und „ohne Auswahl = alle“ sind ein erster Vorschlag (`DECISIONS.md` 2026-10-21). Die Kamera folgt dem Helden; Verschieben über das Feld steht in `IDEAS.md`
- **Schlacht (Tarik):** Als Spieler untätig zu bleiben, verliert 6 von 6 nach rund 50 s: Der Held steht dann offen im Pfeilhagel. In der Kommandoansicht hält er bis zum Ende durch (72 bis 166 s), ohne Befehle gewinnt deine Seite dort 3 von 6
- **Schützen (Tarik):** Stärke, Reichweite und Deckung sind ein erster Vorschlag (`Weapon_Bow`, `Bow_Shot`, `Formation_Default`). Pfeile treffen einen Helden, der stillsteht: Ohne eigenes Zutun waren drei von 12 Schlachten nach 20 bis 40 s verloren. Dein Held kann noch nicht schießen, das steht wie Wurfwaffen in `IDEAS.md`. Der Kommandant schätzt Schützen eher zu niedrig ein, die Prognose ist ein Nahkampf-Modell. Die Taste T/X ist ein Vorschlag
- **Feind-Kommandant (Tarik):** Ein Held, der nichts tut, gewinnt mit Schützen 8 von 12 Schlachten. Prognose und faire Zugreihenfolge sind drin, das liegt also eher an der Aufstellung: dein dichter Schildwall gegen Linie und Keil (Prognose für den Feind unter 1), geprüft ist das nicht. Zum Nachstellen: Aufstellung in `Company_M2`, Gewichte in `Commander_Default`, F3 zeigt die Prognose
- **Bedrohungen (Tarik):** 8 m, 3 Feinde und 3 s Abklingzeit sind ein erster Vorschlag (`DECISIONS.md` 2026-11-06). Formationen im befohlenen Vorrücken drehen sich nicht; der Kommandant gibt meist genau solche Befehle, dein Flankenangriff trifft ihn also weiter voll
- **Haltung (Tarik):** Reagieren ist Standard. Dann läuft eine haltende Linie einem anrückenden Feind 10 m entgegen und verliert den Vorteil des Haltens; wer ihn will, stellt R auf Stellung halten. Ob Reagieren der richtige Standard ist und ob 10 m und 30 m passen, beim Spielen entscheiden (`DECISIONS.md` 2026-11-01). Die Sandbox hat noch keine Taste für die Haltung (H setzt Stellung halten)
- Formationen und Hindernisse: Beim Umformen laufen Soldaten geradeaus zum neuen Slot, ein Slot hinter einer Wand wird erst erreicht, wenn die Formation weiterzieht (nur lokales Steering, CLAUDE.md 3.6). Hindernisse sind nur im Overlay sichtbar (keine Meshes vor M4)
- Sounds sind Platzhalter (M5), Figuren sind Gliederpuppen aus der Pipeline (echte Modelle kommen in M4 Task 9, ebenfalls aus der Pipeline)
- Bogen und Lanze: zwei Grammatik-Erweiterungen geplant, siehe [Papier-Test](combat/papier-test-bogen-lanze.md)

## Aktueller Meilenstein

**M4 Schlacht** (Taskliste in CLAUDE.md, Abschnitt 6): 1 100 gegen 100 · 2 Massen-Rendering · 3 Höhe · 4 Wald und Gelände · 5 Speer · 6 Reiterei: Sim · 7 Reiterei: Führung · 8 Drei Karten mit Aufstellung · 9 Modelle aus der Pipeline · 10 Fun-Gate M4.

**M3 Feldherr** ist abgeschlossen (Fun-Gate bestanden am 2026-11-07): Haltung, Bedrohungen, Commander-Utility-AI, Lanchester, Fernkampf (Bots), 50 gegen 50. Die Befehlsreichweite ist nach dem Spielen wieder entfernt worden.

**Neu im Plan (2026-11-01):** Nach M4 kommt **M4b Kriegszug**, eine leichte Meta in Richtung Bannerlord: Story-Einführung, Städte zum Kaufen und Verkaufen (Rüstung, Waffen, Pferde, Rekruten), Schlachten gegen Banditen und Rivalen. Es gibt keine Weltkarte. Begründung in `DECISIONS.md` 2026-11-01, Plan in CLAUDE.md 6. Davor bleiben M2 und M3 unverändert.
