# Sim

Die deterministische Simulation. Gleicher Seed und gleiche Eingaben ergeben auf jedem Rechner bit-gleich denselben Zustand; darauf beruht der Lockstep-Multiplayer.

## Regeln (Test `SimPurityTests` prüft sie)

- kein `UnityEngine`, `Mathf`, `Time`, `System.Random`, `DateTime`; nur `Unity.Mathematics`
- Zufall nur über `SimRandom`, Zeit nur über `SimWorld.Tick` und `SimConstants.DT` (60 Hz)
- jede Eingabe als `InputCommand` über die `InputQueue`, auch im Singleplayer
- Zustand in Structs ohne Padding (wird roh gehasht), Burst-Jobs mit `FloatMode.Deterministic`

## Ein Tick (`SimWorld.Step`)

1. Befehle des Ticks anwenden (sortiert nach Spieler und Sequenz)
2. `FormationSystem`: Tote austragen, Verhalten auswerten (Halten, Vorrücken, Angriff, Folgen, Rückzug wählen das Marschziel), umformen (Burst-Job), Weg planen (A*, Burst-Job), Anker marschieren und schwenken, jedem Mitglied seinen Slot als Ziel geben
3. Spieler-Bewegungsabsichten anwenden
4. `AgentSystem`: Spatial Hash, Steering (Ziel, Abstand, Wände, Drehrate), Integration (nie in gesperrte Zellen)
5. `CombatSystem`: Bots denken (in Formation nach `FormationCombatRules`, Fliehende kämpfen nicht), Angriffe laufen, SwingTrace löst Treffer auf
6. `MoraleSystem`: Panik aus den Ereignissen des Ticks, alle 50 ms Feld (Diffusion, Abklingen, Held), Moral pro Soldat, Rout; Fliehende laufen vom Feind weg und verlassen am Kartenrand das Feld

Ohne Unity prüfen: `tools/simcheck/run.sh` (siehe [README](../../../../../tools/simcheck/README.md)).

## Ordner

| Ordner | Inhalt |
|---|---|
| `Core/` | `SimWorld`, `SimConstants`, `SimRandom`, `InputQueue`, `InputCommand`, `SimHash` |
| `Grid/` | `WorldGrid` (Kosten pro 1-m-Zelle, 255 = gesperrt, `Version` bei jeder Änderung; Panik pro Team und Zelle), `SpatialHash` (Nachbarsuche per Counting-Sort) |
| `Agents/` | `AgentData`, `AgentSystem` (Bewegung als Burst-Jobs) |
| [Combat/](Combat/README.md) | Kampf-Grammatik, Waffen, Treffererkennung, Kampf in Formation (`Context/`) |
| `AI/` | `BotBrain`: Nahkampf-Bots auf derselben Grammatik wie der Spieler |
| `Formations/` | `FormationData` (mit Verhalten), `SlotGenerator` (5 Typen), `SlotAssignment` (Ungarische Methode, exakt), `FormationSystem` |
| `Orders/` | `FormationOrder`: die eine Befehls-API für Spieler (über `InputQueue`) und Commander-AI (direkt): Bewegen/Ausrichten, Form, Breite, Verhalten; `FormationLinePlanner`: Frontlinie oder Zielpunkt → Befehle für mehrere Formationen nebeneinander; `QuickOrders`: Schnellbefehle ohne Kartenpunkt (Radialmenü) und welche Formationen ein Spieler befehligt |
| `Nav/` | `GridAStar`: A* für Formationsanker, ganzzahlige Kosten, Abstandsfeld zu Hindernissen (breite Fronten meiden enge Lücken), Glätten zu wenigen Wegpunkten |
| `Morale/` | `MoraleSystem` (ein Burst-Job: Quellen, Feld, Moral, Rout, Flucht), `MoraleDiffusion` (ein Schritt von `P += D·ΔP − k·P`), `MoraleSettings`, `MoraleData` (Moral pro Agent) |
