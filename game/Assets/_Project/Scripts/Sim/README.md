# Sim

Die deterministische Simulation. Gleicher Seed und gleiche Eingaben ergeben auf jedem Rechner bit-gleich denselben Zustand; darauf beruht der Lockstep-Multiplayer.

## Regeln (Test `SimPurityTests` prüft sie)

- kein `UnityEngine`, `Mathf`, `Time`, `System.Random`, `DateTime`; nur `Unity.Mathematics`
- Zufall nur über `SimRandom`, Zeit nur über `SimWorld.Tick` und `SimConstants.DT` (60 Hz)
- jede Eingabe als `InputCommand` über die `InputQueue`, auch im Singleplayer
- Zustand in Structs ohne Padding (wird roh gehasht), Burst-Jobs mit `FloatMode.Deterministic`

## Ein Tick (`SimWorld.Step`)

1. Befehle des Ticks anwenden (sortiert nach Spieler und Sequenz)
2. Spieler-Bewegungsabsichten anwenden
3. `AgentSystem`: Spatial Hash, Steering (Ziel, Abstand, Drehrate), Integration
4. `CombatSystem`: Bots denken, Angriffe laufen, SwingTrace löst Treffer auf

## Ordner

| Ordner | Inhalt |
|---|---|
| `Core/` | `SimWorld`, `SimConstants`, `SimRandom`, `InputQueue`, `InputCommand`, `SimHash` |
| `Grid/` | `WorldGrid` (Kosten pro 1-m-Zelle), `SpatialHash` (Nachbarsuche per Counting-Sort) |
| `Agents/` | `AgentData`, `AgentSystem` (Bewegung als Burst-Jobs) |
| [Combat/](Combat/README.md) | Kampf-Grammatik, Waffen, Treffererkennung |
| `AI/` | `BotBrain`: Nahkampf-Bots auf derselben Grammatik wie der Spieler |
| `Formations/`, `Nav/`, `Morale/`, `Orders/` | noch leer (M2/M3) |
