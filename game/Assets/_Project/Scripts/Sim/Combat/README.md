# Combat

Die Kampf-Grammatik aus CLAUDE.md 3.5. Spieler und Bots benutzen dieselben Regeln (`CombatSystem.TryAttack`, `SetBlock`, `TryFeint`, `TryStep`, `TrySpecial`, `TrySwitch`).

## Zustände pro Soldat (`CombatData`)

```
Idle ──Angriff──► Windup ──► Release ──► Recovery ──► Idle
  │                 │ Finte (frühe 60 %)    ▲
  └─Block halten    └─► Idle                │ geblockt: springt in Recovery
Idle ──Schritt──► Stepping (300 ms, 2 m, nicht unterbrechbar) ──► Idle
Idle ──Sprung──► Jumping (450 ms Luft, Beine unverwundbar, 120 ms Landung) ──► Idle
Idle ──Special──► Special (Schildstoß: ausholen, stoßen, erholen) ──► Idle
Idle ──Special mit Zweihänder──► Windup (Sprung-Hieb aus dem Spezial-Slot) …
Idle ──Waffe wechseln──► Switching (450 ms) ──► Idle
Stunned (Treffer, Guard Break, Perfect Block, Schildstoß des Gegners) ──► Idle
Dead
```

## Ein Treffer

1. **SwingTrace:** 4 Punkte entlang der Klinge ziehen pro Tick eine Strecke; getestet gegen Kapseln für Beine, Torso, Kopf der Gegner in der Nähe.
2. **Block?** Hält das Ziel den Block und steht der Angreifer in seinem Frontbogen (Schild 120°, Waffe 90°):
   - Block innerhalb 250 ms vor Kontakt gedrückt → **Perfect Block**: kostet nichts, Angreifer wird betäubt.
   - sonst kostet der Block Stamina; leer → **Guard Break**.
3. **Schaden:** Energie ½·m·v² des schnellsten treffenden Klingenpunkts × Durchdringung (Schneide quer, Spitze längs) minus Rüstungs-Schwelle der Zone × Faktor.
4. **Rückstoß:** Das Ziel wird aus der Trefferrichtung geschoben (Oben-Hieb ≈ 30 cm), ein Blocker halb so weit, ein Angreifer am Perfect Block prallt zurück.

## Weitere Verben

- **Sprint:** schneller, kostet Ausdauer pro Sekunde. Die Körpergeschwindigkeit geht in die Trefferenergie ein: ein Stich aus dem Sprint trägt etwa doppelt so viel Energie wie im Stand.
- **Schritt:** Ausweichen um 2 m relativ zur Blickrichtung, kostet Ausdauer.
- **Sprung:** 450 ms in der Luft, laufen geht weiter, nichts anderes; tiefe Hiebe treffen die Beine nicht. Landung 120 ms.
- **Schildstoß (Special Einhand+Schild):** trifft den nächsten Gegner im schmalen Kegel vorn. Erhobener Block → Guard Break. Sonst leichter Schaden und 800 ms Taumeln; ein sofortiger Stich trifft, bevor der Gegner den Schild wieder hoch hat.
- **Angriffs-Tokens:** höchstens zwei Bots holen gleichzeitig gegen dasselbe Ziel aus, die übrigen warten knapp außer Reichweite (CLAUDE.md 3.5 D, 1vX).

![L5-Held gegen drei Bots](../../../../../../docs/screenshots/m1_duel_1v3.jpg)

## Archetypen

| Archetyp | Block | Special | Besonderheit |
|---|---|---|---|
| Einhand + Schild | Schild, 120° | Schildstoß: bricht jeden Block, sonst Taumeln | schnell, gut geschützt |
| Zweihand | Waffe, 90° | Sprung-Hieb: Ausfallschritt 1,4 m, Körpertempo geht in die Energie | trifft härter, Block kostet den Gegner 2,5× Stamina, lange Erholung |
| Stangenwaffe | Waffe, 90°, kostet beim Blocken 1,6-mal so viel Ausdauer | Rundumschlag: bis 4 Ziele im Bogen, lange Erholung | Reichweite: Bots kämpfen 0,7 m weiter weg; Stich und Hieb, kein Schild. Der Block gegen einen Reiter ist das **Brace** |
| Lanze (beritten) | keiner | – | jede Richtung ist das Einlegen (Couch): 300 ms senken, 2 s halten; Treffer per ContactTrace mit der Annäherungsgeschwindigkeit |

Der Held trägt ein Loadout (bis 4 Slots, Tasten 1–4). Ein gezogener Zweihänder hängt den Schild auf den Rücken.

![Held mit Zweihänder gegen Schildträger](../../../../../../docs/screenshots/m1_twohand.jpg)

## Reiterei (M4)

Reiter und Pferd sind **ein Agent** (`AgentFlags.Mounted`). Das Pferd hat Trägheit (`AgentSystem.Ride`): 9 m/s im Galopp, 3 s bis dahin, Wendekreis v²/8 m (im Galopp 10 m), es läuft nur dorthin, wohin es schaut. Im Wald läuft es mit 45 %, im Sumpf mit 30 %. Die Höhe des Reiters spielt in der Sim keine Rolle: Hiebe zielen auf den Gegner, wie am Hang.

| Was | Regel |
|---|---|
| **Lanze (ContactTrace)** | Die eingelegte Lanze liegt still entlang der Pose. Der erste feindliche Körper auf ihr wird mit ½·m·v² der Annäherung entlang der Lanze getroffen (Reiter minus Ziel), mal Spitzenfaktor. Unter 3 m/s nur ein Anstoßen. Galopp gegen einen Stehenden: 118 Schaden, Trab etwa ein Viertel. Ein Treffer, dann wird die Lanze gehoben |
| **Niederreiten** | Erreicht ein Pferd mit mindestens 3 m/s einen Mann zu Fuß (Brust 0,9 m vor dem Reiter, Bogen ±60°), trifft ihn ein Anteil von ½·m·v² (500 kg): im Galopp etwa 40 Schaden und ein Wurf nach hinten. Das Pferd behält 75 % seines Tempos pro Mann. Ein erhobener Schild oder eine Waffe fängt den Stoß für Ausdauer ab |
| **Brace** | Ein Speerträger, der den Block hält und den Reiter im Frontbogen hat, stemmt den Speer: Das Pferd steht, der Reiter bekommt die Spitze mit dem Gewicht des Pferdes (im Galopp tödlich) |
| **Schildwall** | Ein blockender Soldat im Schildwall: Das Pferd scheut, behält 15 % seines Tempos, der Reiter ist 600 ms benommen, der Soldat zahlt Ausdauer. Eine Linie mit erhobenen Schilden wird dagegen überritten |
| **Reiter-Bot** | Nimmt den nächsten Feind zu Fuß auf dem ganzen Feld, zielt 12 m hinter ihn und reitet durch, senkt die Lanze ab 14 m im Galopp. Nach dem Durchgang (Ziel liegt zwischen Start und Reiter, Lanze traf, oder das Pferd steht am Feind) reitet er 25 m weiter und wendet. In einer haltenden Formation bleibt er am Slot; der Angriffsbefehl schickt ihn los |
| **Soldat gegen Reiter** | Ein Reiter, der im Galopp auf ihn zukommt (vorn, Bahn höchstens 2,5 m neben ihm, bis 20 m), löst seine Reaktionszeit aus. Dann antwortet er einmal mit seiner Stufe: Speer stemmt, Schildwall steht (der Schritt-Anteil geht an den Block wie gegen ein Schwert), sonst Block oder ein Schritt zur Seite im letzten Moment. Wer den Reiter im Rücken hat, sieht ihn nicht |

Ereignisse: `Trample`, `Brace`, `Refuse`; danach folgt das übliche `Hit`, `Blocked` oder `GuardBreak`. Alle Werte stehen in `CombatTuningDefinition` (Kopf „Cavalry“) und `AgentMovementDefinition` (Kopf „Horse“). Tests: `CavalryTests`.

## Kampf in Formation (`Context/FormationCombatRules`, Schicht D)

Regeln über der Grammatik, nie in ihr: Bots in einer Formation reagieren, blocken und schlagen genau wie allein (gleiche Reaktionszeit, gleiche Fehlerquote), die Formation legt nur fest, **wen** sie bekämpfen und **wie weit** sie dafür ihren Platz verlassen.

| Verhalten der Formation | Soldaten |
|---|---|
| Halten, Vorrücken, Folgen | bleiben an der Leine: höchstens 2 m vom Slot weg; Gegner nur im 120°-Bogen vor der Front, von hinten und der Seite nur, wer näher als 1,6 m kommt; kein Sprint |
| Angriff | frei wie im Duell (keine Leine, sprinten) |
| Rückzug | kämpfen nicht, suchen kein Ziel |
| Typ Schildwall | antwortet auf einen gesehenen Angriff mit dem Schild statt mit einem Ausweichschritt aus der Wand |
| Held, unbeaufsichtigt (Spieler in der Kommandoansicht) | Bot-Stufe aus `FormationDefinition` (L2); gebunden wie an einen Slot an seinen Posten 1,5 m hinter dem hintersten Glied der nächsten eigenen Formation (im Kreis: in der Mitte), Front wie diese Formation; Block oben, solange ein Feind in Reichweite ist, sonst Schild unten, damit er schnell zurückkommt. Folgt ihm die Formation, hält er, wo er ist |

Ein dauerhaft erhobener Schild im Schildwall wurde ausprobiert und verworfen: Der Schildstoß bricht jeden erhobenen Block, und unter erhobenem Schild lädt die Ausdauer nicht nach (Ergebnisse in `DECISIONS.md`, 2026-10-20). Alle Werte stehen im `FormationDefinition`.

## Ordner

| Ordner | Inhalt |
|---|---|
| `Grammar/` | Zustände, Ereignisse, Regeln (`CombatTuning`), Bot-Profile, Phasen-Timing |
| `Archetypes/` | `WeaponData`, `AttackData`, `CombatSetup` (alle Tabellen) |
| `Resolvers/` | `WeaponPath` (Waffenbahn aus Keyframes), `SwingTrace` (Geometrie, Durchdringung; auch für die Lanze: Strecke Griff bis Spitze gegen die Kapseln) |
| `Context/` | `FormationCombatRules`: Leine, Frontbogen, Selbstverteidigung, Schildwall, Angriff, Rückzug, unbeaufsichtigter Held |
| `Resolvers/Projectile.cs` | Pfeile: `ProjectileData`, Ballistik ohne Trigonometrie; Flug und Treffer laufen im Kampf-Job |
| `CombatSystem.cs` | der Kampf-Job, ein Tick; die Richtung wechselt jeden Tick, Bots sehen einander im Zustand vom Tick-Anfang, damit keine Agenten-ID einen Vorteil hat. `Couch` (Lanze) und `Charges` (Pferde gegen Fußvolk, nach allen Agenten in ID-Reihenfolge) |

Werte (Schaden, Fenster, Stamina, Bot-Stufen) kommen aus ScriptableObjects in `Scripts/Data`. Tests: `CombatTests`, `CombatVerbTests`, `TwoHandTests`, `AttackTests`, `FormationCombatTests`, `PolearmTests`, `CavalryTests`. Papier-Test gegen Bogen und Lanze: [docs/combat](../../../../../../docs/combat/papier-test-bogen-lanze.md).
