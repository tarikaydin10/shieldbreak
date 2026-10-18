# EditMode-Tests

Vor jedem Commit grün: `.\tools\run-tests.ps1` (oder `-Shadow` bei offenem Unity, oder Test Runner in Unity).

| Testklasse | Prüft |
|---|---|
| `DeterminismTests` | gleicher Seed + gleiche Eingaben = gleicher Hash über 10 000 Ticks mit 200 Agenten |
| `SimCoreTests` | `SimRandom`, `InputQueue`, `SimHash`, Befehls-Stempel |
| `SimPurityTests` | keine verbotenen APIs in `Sim/` |
| `GridTests` | Grid-Koordinaten, Spatial Hash, Nachbarsuche |
| `AgentTests` | Ankommen, Tempo- und Beschleunigungsgrenzen, Trennung, ungültige Befehle |
| `CombatTests` | jede Kampfregel tick-genau, Bot-Duelle, Bot-Stufen gegeneinander |
| `CombatVerbTests` | Sprint, Ausweichschritt, Schildstoß mit Nachstich, Angriffs-Tokens (1vX) |
| `TwoHandTests` | Zweihänder gegen Einhand, Blockbogen, Sprung-Hieb, Waffenwechsel, Zweihand-Bots |
| `AttackTests` | Phasen-Timing, Waffenbahn, schnellster Moment vor dem Kontakt |
| `PerformanceTests` | 500 Agenten und 500 kämpfende Bots unter 2 ms pro Tick |
| `SandboxScenarioTests` | Spawn-Gruppen, Szenario-Determinismus |
| `ArtPipelineTests` | Modelle unverändert aus der Pipeline, Palette-Material |
| `ControlsDocumentationTests` | jede Eingabe beschrieben, kein direkter Gerätezugriff |

`TestScenarios`, `TestCombat` (Waffe 0 = Einhand + Schild, 1 = Zweihänder), `InputDriver`: gemeinsame Aufbauten für die Tests.
