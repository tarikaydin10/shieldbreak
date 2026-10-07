# Posen-Vorgabe: Oben-Hieb (Einhand), M0-Animationsprototyp

Ziel des Prototyps: Beweisen, dass ein pose-basierter prozeduraler Hieb lesbar und nicht steril ist. Vergleich gegen einen Mixamo/Kubold-Clip im selben Rig. Entscheidung im M0-Fun-Gate.

## Rig

Synty-POLYGON-Skelett (Humanoid, Unity-Humanoid-Mapping). Rechte Hand hält die Waffe; Waffen-Socket am `Hand_R`-Bone, Griff-Offset als Transform im Waffen-Prefab. Linke Hand: Schild-Socket am `Forearm_L`.

## Vier Key-Posen (Zeit normiert 0..1 über den gesamten Angriff)

Phasen gemäß Grammatik: Windup 0.00–0.55, Release 0.55–0.75, Recovery 0.75–1.00. Bei 400 ms Windup / 150 ms Release / 200 ms Recovery entspricht das 750 ms Gesamtdauer (Preset „Mittel").

| # | t | Pose | Kriterium |
|---|---|---|---|
| P0 | 0.00 | **Idle-Guard**: Waffe schräg vor dem Körper, Ellbogen 90°, Gewicht mittig, Schild vor Torso | Neutral, aus der alles startet |
| P1 | 0.55 | **Windup-Ende**: Waffe senkrecht über dem Kopf, Arm fast gestreckt, Oberkörper 15° nach hinten, Hüfte 20° gegen Schlagrichtung eingedreht, Gewicht auf hinterem Fuß, Schild leicht geöffnet | Silhouette aus 15 m eindeutig „oben" |
| P2 | 0.70 | **Kontakt**: Waffe waagrecht auf Brusthöhe des Ziels, Arm gestreckt, Oberkörper 20° nach vorn, Hüfte 25° in Schlagrichtung gedreht, Gewicht auf vorderem Fuß, Schild zurück vor Torso | Maximale Klingengeschwindigkeit liegt kurz VOR dieser Pose (t ≈ 0.63) |
| P3 | 0.85 | **Overshoot**: Waffe 30 cm tiefer als Kontakt, Oberkörper 25° vorn, Arm leicht gebeugt | Gewicht sichtbar, Recovery beginnt |
| P0' | 1.00 | zurück zu Idle-Guard | Loop-fähig |

## Kurven

- **Windup (P0 → P1):** Ease-in-out, langsam beginnend (Anticipation). Die letzten 15 % des Windups fast statisch: das ist das Lesefenster für den Gegner und das Feint-Fenster für den Angreifer.
- **Release (P1 → P2):** Ease-in stark (quadratisch bis kubisch), damit die Klinge beschleunigt. Hieraus kommt v für das Energiemodell. Die Sim berechnet die Socket-Positionen entlang der Klinge aus dieser Kurve, nicht aus Unity.
- **Recovery (P2 → P3 → P0'):** Overshoot in P3, dann Ease-out zurück. Overshoot-Größe = Feel-Regler „Gewicht".

## Secondary Motion (prozedural, Presentation)

- Hüfte führt vor Schulter, Schulter vor Hand: Rotationen jeweils um ~30 ms versetzt.
- Kopf bleibt auf Zielpunkt (Look-At-IK), unabhängig vom Oberkörper.
- Füße per IK am Boden; Gewichtsverlagerung über Hüftposition (−8 cm zurück im Windup, +12 cm vor im Kontakt).
- Seeded Micro-Noise auf Hand-Rotation (±1.5°), damit zwei identische Hiebe nicht identisch aussehen. Seed aus Agent-ID + Tick, deterministisch.

## Fun-Gate-Kriterien

1. Aus 15 m Distanz erkennt ein Zuschauer die Richtung im Windup (Test mit allen vier Richtungen, sobald sie existieren; im M0 nur „oben" vs. Idle).
2. Nebeneinander mit dem Clip: Der prozedurale Hieb darf steifer wirken, aber nicht „falsch". Wenn er falsch wirkt, Kurven iterieren (drei Abende), erst dann über Fallback entscheiden.
3. Timing-Werte aus der Grammatik und sichtbare Bewegung stimmen auf den Tick überein (Debug-Overlay zeichnet Phasen-Marker).

## Blender-Workflow

1. Synty-Rig importieren, Rest-Pose prüfen.
2. Posen P0–P3 als Pose-Library-Einträge (oder Keyframes auf Frame 0/55/70/85/100) setzen.
3. Export FBX nach `game/Assets/_Project/Art/Poses/onehand_overhead.fbx`, nur Skelett + Posen, kein Mesh.
4. In Unity: `AttackDefinition`-ScriptableObject referenziert die Posen, Kurven als `AnimationCurve`-Felder, Phasendauern als Floats.
