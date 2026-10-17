# Combat

Die Kampf-Grammatik aus CLAUDE.md 3.5. Spieler und Bots benutzen dieselben Regeln (`CombatSystem.TryAttack`, `SetBlock`, `TryFeint`, `TryStep`, `TrySpecial`).

## Zustände pro Soldat (`CombatData`)

```
Idle ──Angriff──► Windup ──► Release ──► Recovery ──► Idle
  │                 │ Finte (frühe 60 %)    ▲
  └─Block halten    └─► Idle                │ geblockt: springt in Recovery
Idle ──Schritt──► Stepping (300 ms, 2 m, nicht unterbrechbar) ──► Idle
Idle ──Special──► Special (Schildstoß: ausholen, stoßen, erholen) ──► Idle
Stunned (Treffer, Guard Break, Perfect Block, Schildstoß des Gegners) ──► Idle
Dead
```

## Ein Treffer

1. **SwingTrace:** 4 Punkte entlang der Klinge ziehen pro Tick eine Strecke; getestet gegen Kapseln für Beine, Torso, Kopf der Gegner in der Nähe.
2. **Block?** Hält das Ziel den Block und steht der Angreifer in seinem Frontbogen (Schild 120°, Waffe 90°):
   - Block innerhalb 250 ms vor Kontakt gedrückt → **Perfect Block**: kostet nichts, Angreifer wird betäubt.
   - sonst kostet der Block Stamina; leer → **Guard Break**.
3. **Schaden:** Energie ½·m·v² des schnellsten treffenden Klingenpunkts × Durchdringung (Schneide quer, Spitze längs) minus Rüstungs-Schwelle der Zone × Faktor.

## Weitere Verben

- **Sprint:** schneller, kostet Ausdauer pro Sekunde. Die Körpergeschwindigkeit geht in die Trefferenergie ein: ein Stich aus dem Sprint trägt etwa doppelt so viel Energie wie im Stand.
- **Schritt:** Ausweichen um 2 m relativ zur Blickrichtung, kostet Ausdauer.
- **Schildstoß (Special Einhand+Schild):** trifft den nächsten Gegner im schmalen Kegel vorn. Erhobener Block → Guard Break. Sonst leichter Schaden und 800 ms Taumeln; ein sofortiger Stich trifft, bevor der Gegner den Schild wieder hoch hat.
- **Angriffs-Tokens:** höchstens zwei Bots holen gleichzeitig gegen dasselbe Ziel aus, die übrigen warten knapp außer Reichweite (CLAUDE.md 3.5 D, 1vX).

![L5-Held gegen drei Bots](../../../../../../docs/screenshots/m1_duel_1v3.jpg)

## Ordner

| Ordner | Inhalt |
|---|---|
| `Grammar/` | Zustände, Ereignisse, Regeln (`CombatTuning`), Bot-Profile, Phasen-Timing |
| `Archetypes/` | `WeaponData`, `AttackData`, `CombatSetup` (alle Tabellen) |
| `Resolvers/` | `WeaponPath` (Waffenbahn aus Keyframes), `SwingTrace` (Geometrie, Durchdringung) |
| `CombatSystem.cs` | der Kampf-Job, ein Tick in Agenten-Reihenfolge |

Werte (Schaden, Fenster, Stamina, Bot-Stufen) kommen aus ScriptableObjects in `Scripts/Data`. Tests: `CombatTests`, `CombatVerbTests`, `AttackTests`.
