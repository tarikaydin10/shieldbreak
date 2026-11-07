# EditMode-Tests

Vor jedem Commit grün: `.\sb.ps1 test` (Unity darf offen bleiben) oder Test Runner in Unity. Ohne Unity laufen die Sim-, Kampf- und Formations-Tests mit `tools/simcheck/run.sh`.

| Testklasse | Prüft |
|---|---|
| `DeterminismTests` | gleicher Seed + gleiche Eingaben = gleicher Hash über 10 000 Ticks mit 200 Agenten |
| `SimCoreTests` | `SimRandom`, `InputQueue`, `SimHash`, Befehls-Stempel |
| `SimPurityTests` | keine verbotenen APIs in `Sim/` |
| `GridTests` | Grid-Koordinaten, Spatial Hash, Nachbarsuche |
| `AgentTests` | Ankommen, Tempo- und Beschleunigungsgrenzen, Trennung, Wände (nie hinein, entlanggleiten), ungültige Befehle |
| `FormationTests` | Slot-Layouts aller Typen, Zuordnung optimal (gegen Brute Force), Marsch, Umdrehen, Umformen, Befehls-Rechte, Determinismus mit Befehlen und Wänden, Frontlinien-Planer, Wege um Mauern, Neuplanung bei Kartenänderung, Ziel in der Wand |
| `FormationBehaviorTests` | Verhalten ohne Kampf: Vorrücken bis zum Kontakt (auch einem ausweichenden Gegner nach, gegen eine gewählte statt die nächste Formation), Angriff ohne Abstand, Folgen hinter einem Soldaten, Rückzug und danach Front zum Feind, Halten stoppt, Bewegen beendet ein Verhalten, ungültige Befehle, Determinismus; Haltung: Reagieren greift nahe Feinde an, Stellung halten bleibt stehen, ein Marsch hinter den Feind wird unterwegs nicht abgelenkt und greift am Ziel seinen Rücken an, „Halten“ aus dem Radialmenü beendet die Reaktion; Bedrohungen: Front dreht zu Flanke und Rücken, nicht bei mehr Feinden vorn, zu wenigen Feinden oder im Kreis, ein Befehl geht für die Abklingzeit vor, ein von selbst begonnener Angriff wechselt das Ziel, ein befohlener nicht |
| `FormationCombatTests` | Kampf in Formation: Regeln (Leine, Frontbogen, Selbstverteidigung, Angriff, Rückzug, Schildwall), haltende Linie bleibt an den Slots und stürmende zerfällt, Rückzug kämpft nicht, Schildwall weicht nicht aus, 20 gegen 20 endet und ist deterministisch, breitere Front gewinnt |
| `CompanyTests` | Schnellbefehle (Radialmenü) ergeben die passenden `FormationOrder`s, Folgen braucht einen Helden; Kompanie-Szenario: Held ist Spieler 0, befehligt nur seine Formationen, Feind rückt vor, Befehle laufen über die `InputQueue`, Truppe folgt dem Helden, Determinismus, das Gefecht findet statt; `BattleOutcome`: Sieg, wenn kein Feind mehr kämpft, Niederlage, wenn der Held fällt oder die eigene Truppe bricht, die Kompanie-Schlacht ist ohne Zutun nach höchstens drei Minuten entschieden und bleibt es |
| `CommanderTests` | Kommandant: Scores (Stärkere greifen an, Schwächere halten, Flanke zur offenen Seite und nur mit Platz und zweiter Formation, Rückzug bei Schwäche und Panik), Hysterese, Mindestdauer, Ungeduld; in der Kompanie: liest das Feld, befiehlt nur eigene Formationen, flankiert und bindet, Determinismus, keine Schlacht bleibt stehen |
| `HeroUnattendedTests` | Kommandoansicht in der Sim: Betreten über die `InputQueue`, der Held zieht sich hinter seine Linie zurück und ignoriert Spieler-Befehle, wehrt sich selbst (ein Held ohne Spieler tut nichts), folgt ihm die Formation, hält er; zurückgenommen bleibt nichts vom Bot; ohne Einstellung oder für fremde Helden wirkungslos; Determinismus |
| `MoraleTests` | Panik-Feld (verteilt gleichmäßig, verliert am Rand nichts, klingt ab, instabile Werte abgelehnt), Tote erschrecken die eigene Seite und ermutigen die andere, Treffer von hinten erschrecken, Formation dämpft, Erholung, Zuversicht, Rout (verlässt Formation, kämpft nicht, rennt, verlässt das Feld), ein brechender Feind ermutigt, Ansteckung, Held als Senke und bricht nie, Moral aus, 20 gegen 20 endet im Rout |
| `NavTests` | A*: freies Feld, Mauer mit Lücke, keine Eckenschnitte, unerreichbares Ziel, breite Front nimmt die breite Lücke, Abstandsfeld, gleiche Planung zweimal gleich |
| `CombatTests` | jede Kampfregel tick-genau, Bot-Duelle, Bot-Stufen gegeneinander |
| `CombatVerbTests` | Sprint, Ausweichschritt, Sprung (Beine in der Luft unverwundbar), Schildstoß mit Nachstich, Rückstoß, Reaktionszeit ≤ 50 ms, Angriffs-Tokens (1vX) |
| `TwoHandTests` | Zweihänder gegen Einhand, Blockbogen, Sprung-Hieb, Waffenwechsel, Zweihand-Bots |
| `AttackTests` | Phasen-Timing, Waffenbahn, schnellster Moment vor dem Kontakt |
| `PerformanceTests` | 500 Agenten und 500 kämpfende Bots unter 2 ms pro Tick |
| `SandboxScenarioTests` | Spawn-Gruppen als Formationen oder lose, mit Waffen als Bots ihrer Stufe und mit Moral, Szenario-Determinismus mit Befehlen |
| `ArtPipelineTests` | Modelle unverändert aus der Pipeline, Palette-Material |
| `ControlsDocumentationTests` | jede Eingabe beschrieben, kein direkter Gerätezugriff |
| `SceneCatalogTests` | jede Build-Szene und jedes Szenario über den Szenen-Katalog erreichbar, jeder Eintrag mit Ziel, Steuerung und Prüfliste, Build Settings = `BuildTools.Scenes`, Auswahl des laufenden Eintrags |

`TestScenarios`, `TestCombat` (Waffe 0 = Einhand + Schild, 1 = Zweihänder; `Weapons` liefert die Definitionen dahinter), `InputDriver`: gemeinsame Aufbauten für die Tests.
