# Kriegszug (M4b): Plan

Stand 2026-11-15. Vorarbeit für M4b, gebaut wird erst nach dem Fun-Gate M4 (CLAUDE.md 6: ein Meilenstein gilt erst mit bestandenem Fun-Gate). Grundlagen: `DECISIONS.md` 2026-11-01 „Leichte Meta: Kriegszug“ und 2026-11-15 „Setting und Reihenfolge“.

**Ziel (Fun-Gate M4b):** Ein neuer Kriegszug wird freiwillig bis zur dritten Stadt gespielt, und man will danach weiter.

**Tariks Wunsch:** Inszenierung, Immersion, ein roter Faden. Der Kriegszug ist der Ort dafür: Jede Schlacht hat einen Grund, jeder Gefallene fehlt danach, und die Geschichte geht weiter, wenn man gewinnt oder verliert.

## Der Ablauf

```
Neuer Kriegszug → Einführung (Tafeln) → Stadt ─┬→ Markt, Werber, Schmied, Stall
                                               └→ Aufträge (2–3) → Aufstellung → Schlacht → Nachschlacht ─┐
                        ↑                                                                                  │
                        └──────────────── nächste Stadt oder dieselbe, Story-Tafel bei Wendepunkten ──────┘
```

| Station | Was passiert | Säule |
|---|---|---|
| Einführung | 4 bis 6 Tafeln mit Bild und kurzem Text: wer du bist, warum du ein Heer brauchst, wer der Feind ist | Faden |
| Stadt | Beute verkaufen; Waffen, Rüstung, Pferde, Rekruten kaufen; Trupp ansehen (wer lebt, wer verwundet ist, wer was trägt) | 1, Faden |
| Aufträge | 2 bis 3 zur Wahl, jeder mit Gegner, Karte, Lohn und einem Satz Story. Banditen sind leicht und arm, Rivalen schwer und reich | 2, Faden |
| Schlacht | Das eigene Heer aus dem Trupp, in Formationen gruppiert, auf einer Karte aus `Data/Maps`, mit Aufstellung | 1, 2, 3 |
| Nachschlacht | Wer gefallen ist (mit Namen), wer verwundet ist, Beute, Lohn, Ruf. Bei einem Wendepunkt eine Story-Tafel | Faden |

**Verlieren:** Eine verlorene Schlacht beendet den Kriegszug nicht. Man verliert Leute, Beute und Ruf und landet in der letzten Stadt. Erst wenn der Held fällt, endet der Zug (wie die Schlacht selbst, `DECISIONS.md` 2026-11-01).

**Bogen bis zum Fun-Gate:** drei Städte, je zwei bis drei Aufträge, ein Antagonist, der in der dritten Stadt selbst ins Feld zieht. Danach geht es weiter (Endlos-Aufträge), bis ein zweiter Akt geschrieben ist.

## Der Trupp

Jeder Soldat ist eine Person mit Namen, Herkunft (aus der Stadt, in der er angeworben wurde), Erfahrung und Ausrüstung. Daraus entsteht die Bindung, die Shieldwall fehlt.

| Wert | Wirkung in der Schlacht | Wie er sich ändert |
|---|---|---|
| Bot-Stufe (L1 bis L5) | Reaktionszeit, Fehlerrate, Techniken (wie heute) | Steigt mit überlebten Schlachten |
| Waffe, Zweitwaffe | Archetyp und Formation (Schild → Linie oder Schildwall, Speer, Bogen, Lanze) | Kaufen |
| Rüstung | Zonen-Schwellwerte für Beine, Torso, Kopf pro Soldat | Kaufen |
| Pferd | beritten oder nicht | Kaufen |
| Verwundung | Fehlt in der nächsten Schlacht oder kämpft mit weniger Leben | Heilt mit der Zeit in der Stadt |

Formationen bildet das Spiel aus der Ausrüstung (Schützen zusammen, Reiter zusammen, Speere zusammen); vor der Schlacht kann man sie in der Aufstellung umstellen.

## Daten (ScriptableObjects, `Scripts/Data` und `Data/Kriegszug`)

| Definition | Inhalt |
|---|---|
| `CampaignDefinition` | Titel, Einführungstafeln, Folge der Städte, Wendepunkte (Bedingung → Tafel), Startgold, Start-Trupp |
| `TownDefinition` | Name, Bild, Sortiment (Waffen, Rüstungen, Pferde mit Preisen), Rekruten (Typ, Anzahl, Preis), Aufträge |
| `ContractDefinition` | Titel, Story-Satz, Gegner-Heer (Formationen mit Waffen, Stufen, Rüstung), Karte, Lohn, Beute, Ruf |
| `ArmorDefinition` | Name, Preis, Schwellwerte pro Zone, Gewicht (Tempo, Ausdauer) |
| `StoryPanel` | Bild, Text (Deutsch), optional Sprecher |
| `TroopTemplate` | Rekruten-Typ: Startstufe, Startausrüstung, Namenspool |

Der **Spielstand** ist JSON in `Application.persistentDataPath` (`DECISIONS.md` 2026-11-01): Gold, Ruf, Trupp (Personen mit Werten), aktuelle Stadt, erledigte Aufträge, Seed. Er enthält nur Verweise auf Definitionen (Namen), keine Zahlen aus ihnen.

## Technik

| Teil | Ort | Regel |
|---|---|---|
| Meta-Logik | `Scripts/Meta/` (eigene asmdef) | Kennt Data und das Szenario, nie `SimWorld` direkt; reines C#, testbar ohne Szene |
| Brücke zur Schlacht | `Meta/BattleBuilder` | Baut aus Trupp und Auftrag ein Laufzeit-`CompanyScenarioDefinition` (Formationen, Waffen, Stufen, Rüstung, Karte, Seed) |
| Ergebnis zurück | `Meta/BattleReport` | Liest nach der Schlacht, welcher Agent welche Person war: gefallen, verwundet (Leben unter einer Schwelle), überlebt; Beute aus besiegten Gegnern |
| Rüstung pro Soldat | `Sim/Combat` | Die Zonen-Schwellwerte wandern aus `CombatTuning` in `CombatData` (pro Agent), Standard aus dem Tuning. Eine Architekturänderung: vorher `DECISIONS.md` |
| Bildschirme | Szene `Campaign` | Hauptmenü → Kriegszug; Stadt, Aufträge, Trupp, Tafeln, Nachschlacht; Rückweg aus `M2_Company` nach der Schlacht |
| Tests | EditMode | Spielstand hin und zurück, Preise und Gold, Formationen aus dem Trupp, Szenario aus Auftrag, Ergebnis zurück auf Personen, Determinismus einer Schlacht aus dem Kriegszug |

Die Sim bleibt unberührt bis auf die Rüstung pro Soldat. Der Multiplayer bleibt Skirmish.

## Inszenierung

| Was | Wie | Wann |
|---|---|---|
| Story-Tafeln | Bild und Text, langsamer Zoom auf das Bild, Text erscheint zeilenweise | M4b |
| Bilder | Aus der Pipeline: Dioramen aus den eigenen Modellen (Heer, Stadt, Banner), gerendert mit fester Kamera und Licht wie der Kontaktbogen. So bleibt der Stil eins (CLAUDE.md 8: Assets nur über die Pipeline) | M4b, braucht Stadt- und Banner-Modelle |
| Namen in der Schlacht | Der Ergebnisbildschirm nennt Gefallene mit Namen; F1 zeigt Personen statt Nummern | M4b |
| Schlachtbeginn | Kameraflug über beide Heere vor der Aufstellung, Hornsignal, Banner über den Formationen | M5 (oder vorziehen, wenn Zeit bleibt) |
| Ton | Musik pro Ort (Stadt, Aufstellung, Schlacht), Schlachtruf | M5 |

## Taskliste M4b (~80 h)

1. **Rüstung pro Soldat** (Sim) — Schwellwerte in `CombatData`, `ArmorDefinition`, Tests. Vorher `DECISIONS.md`.
2. **Meta-Gerüst** — `Scripts/Meta`, Spielstand als JSON, `CampaignDefinition`, Tests für Speichern und Laden.
3. **Trupp und Schlacht** — Personen, Formationen aus Ausrüstung, `BattleBuilder`, `BattleReport`, Rückweg aus der Schlacht.
4. **Stadt** — Markt, Werber, Schmied, Stall; Preise und Gold; Trupp ansehen.
5. **Aufträge** — Auswahl, Gegner-Heere, Lohn und Beute, Banditen und Rivalen.
6. **Story** — Einführung, Wendepunkte, Nachschlacht mit Namen, Verlieren ohne Ende.
7. **Bilder aus der Pipeline** — Stadt- und Banner-Modelle, Dioramen für die Tafeln.
8. **Erster Akt** — drei Städte, Antagonist, Texte. Inhalt von Tarik (Story), Struktur von Claude.
9. **Fun-Gate M4b** (Tarik).

## Welt: drei Entwürfe

Low-Fantasy in einer eigenen Welt (`DECISIONS.md` 2026-11-15): erfundene Reiche, mittelalterliche Technik, keine Magie. Die Namen sind Vorschläge.

### A — Die Grauen Marken

Das Kaiserreich Aldur ist vor zwanzig Jahren zerfallen. In den Grenzmarken herrscht, wer ein Heer hat. Du bist Hauptmann einer kleinen Söldnerschar, angeheuert von der Stadt Brennfurt gegen Banditen. Bald zeigt sich, dass hinter den Banditen der Markgraf Orwin steht, der die Marken für sich will.
Gegner: Banditen, Orwins Landsknechte, zuletzt seine Panzerreiter. Ton: rau, Söldnerleben, Gold zählt.

### B — Das Erbe von Valdmark

Du bist der verstoßene Sohn des Fürsten von Valdmark. Dein Onkel Keorn hat den Thron genommen und dich für tot erklärt. Mit einem alten Waffenmeister und zwei Dutzend Getreuen ziehst du von Stadt zu Stadt, um ein Heer zu sammeln und zurückzuholen, was dir gehört.
Gegner: Wegelagerer, Keorns Vasallen, zuletzt Keorn selbst mit seiner Garde. Ton: persönlich, Rache und Treue, der stärkste rote Faden.

### C — Salz und Eisen

An der Salzküste kämpfen drei Handelsstädte um die Minen im Hinterland. Du führst eine freie Kompanie und verkaufst dein Schwert. Jeder Auftrag stärkt eine Stadt und schwächt eine andere, bis eine von ihnen dich verrät.
Gegner: Bergbanditen, die Söldner der anderen Städte, zuletzt die Stadtwache des Verräters. Ton: Intrige, Wahl mit Folgen.

## Offene Fragen an Tarik

- Welcher Weltentwurf (A, B, C oder eine Mischung)?
- Hat der Held einen Namen und eine Geschichte (B), oder ist er ein unbeschriebenes Blatt (A, C)?
- Sollen Gefallene endgültig weg sein (Bindung, Härte) oder in der Stadt ersetzbar mit gleichem Namen (milder)?
- Bilder als Dioramen aus den eigenen Modellen: einverstanden, oder lieber gezeichnet (dann wäre das eine Ausnahme von „Assets nur über die Pipeline“)?
- Sprache der Story: nur Deutsch, oder von Anfang an zweisprachig angelegt?
