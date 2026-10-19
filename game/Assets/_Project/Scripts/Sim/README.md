# Sim

Die deterministische Simulation. Gleicher Seed und gleiche Eingaben ergeben auf jedem Rechner bit-gleich denselben Zustand; darauf beruht der Lockstep-Multiplayer.

## Regeln (Test `SimPurityTests` prüft sie)

- kein `UnityEngine`, `Mathf`, `Time`, `System.Random`, `DateTime`; nur `Unity.Mathematics`
- Zufall nur über `SimRandom`, Zeit nur über `SimWorld.Tick` und `SimConstants.DT` (60 Hz)
- jede Eingabe als `InputCommand` über die `InputQueue`, auch im Singleplayer
- Zustand in Structs ohne Padding (wird roh gehasht), Burst-Jobs mit `FloatMode.Deterministic`

## Ein Tick (`SimWorld.Step`)

1. Befehle des Ticks anwenden (sortiert nach Spieler und Sequenz)
2. `FormationSystem`: Tote austragen, umformen (Burst-Job), Weg planen (A*, Burst-Job), Anker marschieren und schwenken, jedem Mitglied seinen Slot als Ziel geben
3. Spieler-Bewegungsabsichten anwenden
4. `AgentSystem`: Spatial Hash, Steering (Ziel, Abstand, Wände, Drehrate), Integration (nie in gesperrte Zellen)
5. `CombatSystem`: Bots denken, Angriffe laufen, SwingTrace löst Treffer auf

Ohne Unity prüfen: `tools/simcheck/run.sh` (siehe [README](../../../../../tools/simcheck/README.md)).

## Ordner

| Ordner | Inhalt |
|---|---|
| `Core/` | `SimWorld`, `SimConstants`, `SimRandom`, `InputQueue`, `InputCommand`, `SimHash` |
| `Grid/` | `WorldGrid` (Kosten pro 1-m-Zelle, 255 = gesperrt, `Version` bei jeder Änderung), `SpatialHash` (Nachbarsuche per Counting-Sort) |
| `Agents/` | `AgentData`, `AgentSystem` (Bewegung als Burst-Jobs) |
| [Combat/](Combat/README.md) | Kampf-Grammatik, Waffen, Treffererkennung |
| `AI/` | `BotBrain`: Nahkampf-Bots auf derselben Grammatik wie der Spieler |
| `Formations/` | `FormationData`, `SlotGenerator` (5 Typen), `SlotAssignment` (Ungarische Methode, exakt), `FormationSystem` |
| `Orders/` | `FormationOrder`: die eine Befehls-API für Spieler (über `InputQueue`) und Commander-AI (direkt); `FormationLinePlanner`: Frontlinie oder Zielpunkt → Befehle für mehrere Formationen nebeneinander |
| `Nav/` | `GridAStar`: A* für Formationsanker, ganzzahlige Kosten, Abstandsfeld zu Hindernissen (breite Fronten meiden enge Lücken), Glätten zu wenigen Wegpunkten |
| `Morale/` | noch leer (M2 Task 5) |
