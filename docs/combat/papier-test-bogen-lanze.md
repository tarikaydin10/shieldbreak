# Papier-Test: Kampf-Grammatik gegen Bogen und Lanze

CLAUDE.md 3.5 verlangt, jede Grammatik-Regel gegen Bogen (Spannen/Lösen) und Lanze (Couch) zu prüfen. Stand nach M1 (Einhand + Schild, Zweihänder).

**Ergebnis:** Die Grammatik trägt beide Archetypen. Es braucht zwei allgemeine Erweiterungen (Halten, Resolver-Art), keine Sonderregel für eine Waffe.

## Regel für Regel

| Grammatik (heute) | Bogen / Armbrust (M3) | Lanze beritten (M4) | Passt? |
|---|---|---|---|
| Phasen `Windup → Release → Recovery` | Windup = Spannen, Release = Lösen (Pfeil entsteht), Recovery = Nachlegen bzw. Nachladen (Armbrust: lange Recovery) | Windup = Lanze senken, Release = eingelegt (Kontaktfenster), Recovery = Lanze heben | ja |
| Windup läuft automatisch durch | Bogen muss **gespannt gehalten** werden, bis die Taste losgelassen wird | – | **Erweiterung 1** |
| Release hat feste Länge | – | Couch dauert, **solange gehalten** und das Pferd schnell genug ist | **Erweiterung 1** |
| Finte = Abbruch im frühen Windup | Spannen abbrechen, Pfeil bleibt | Lanze wieder heben | ja |
| Morph (Richtung ändern im Windup) | entfällt, Zielen statt Richtung | entfällt | ja (Archetyp nutzt es nicht) |
| Richtung als Geometrie (`AttackDirection`) | Zielrichtung aus der Kamera: Payload im `CombatAttack`-Befehl (`FloatArgs`), deterministisch wie `PlayerMove` | Richtung = Pferd + Kamera (Schicht D, beritten) | ja |
| Treffer = SwingTrace (Klingen-Sockets) | Projektil: Fixed-Step-Ballistik, Strahl pro Tick gegen dieselben Kapseln | ContactTrace: Lanzenspitze gegen Kapseln, solange eingelegt | **Erweiterung 2** |
| Energie `E = ½·m·v²` am Kontakt, Spitze/Schneide, Zonen-Schwelle | v aus der Ballistik, Profil Spitze | v = Relativgeschwindigkeit Reiter–Ziel (die Körpergeschwindigkeit fließt heute schon über Sprint und Ausfallschritt ein) | ja |
| Block im Frontbogen, Stamina-Preis | Schild blockt Pfeile im 120°-Bogen; Bogen selbst blockt nicht, Rückfall per Waffenwechsel auf den Nahkampf-Slot (gibt es seit M1) | kein Block mit der Lanze | ja |
| Perfect Block (Timing) | gegen Pfeile **aus**: unlesbar und im Lockstep unfair. Schalter pro Resolver | gegen Lanze aus | ja (Schalter) |
| Stamina als Energiehaushalt | Halten der Spannung kostet Stamina pro Sekunde (wie Sprint) | Couch kostet nichts, das Pferd ermüdet (M4) | ja |
| Input-Buffer, Hit-Stun, Hitstop | unverändert | unverändert | ja |
| Ausweichschritt | zu Fuß ja | beritten aus (Schicht D) | ja |
| Special pro Archetyp | Spannung halten mit Zielruhe (Präzisionsbonus) | – | ja |
| Angriffs-Tokens (1vX) | zählen nur Nahkampf-Schwünge; Schützen brauchen kein Token | eigenes Token-Budget gegen Kavallerie (M4) | kleine Anpassung |

## Die zwei Erweiterungen

1. **Halten in einer Phase** (`AttackData`): Ein Angriff kann an einer Pose anhalten, solange die Angriffstaste gedrückt ist. Neuer Befehl `CombatAttackRelease` (Taste losgelassen). Bogen: Halt am Ende des Windups mit Stamina-Abzug pro Tick und Höchstdauer. Lanze: Halt im Release, solange die Bedingung (Pferdetempo) gilt. Der Zweihänder könnte damit später einen aufgeladenen Hieb bekommen.
2. **Resolver-Art pro Angriff** (`AttackData.Resolver`: SwingTrace, Projectile, ContactTrace), dazu die Zielrichtung als Payload. Projektile sind eigene Sim-Structs in einem NativeArray mit Fixed-Step und Hash, also keine GameObjects.

Beides ist allgemein und hängt an keiner Waffe. Der Sonder-Angriffs-Slot aus M1 (`AttackDirection.Special`, genutzt vom Sprung-Hieb) zeigt, dass Archetyp-Specials ohne Sonderpfad in die Tabelle passen.

## Was nicht passt (bewusst)

- Richtungsblock: gibt es nicht und kommt auch für Pfeile nicht.
- Aktive Ragdolls beim Lanzentreffer: nur Darstellung, Sim bleibt Kapseln.
