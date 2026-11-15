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

## Welt: Das Erbe von Valdmark

Entschieden am 2026-11-15 (Tarik, `DECISIONS.md`): Entwurf B. Low-Fantasy in einer eigenen Welt, erfundene Reiche, mittelalterliche Technik, keine Magie. Verworfen: A „Die Grauen Marken“ (Söldner im zerfallenen Kaiserreich) und C „Salz und Eisen“ (Handelsstädte und Verrat).

Du bist der verstoßene Sohn des Fürsten von Valdmark. Dein Onkel Keorn hat den Thron genommen und dich für tot erklärt. Mit einem alten Waffenmeister und zwei Dutzend Getreuen ziehst du von Stadt zu Stadt, um ein Heer zu sammeln und zurückzuholen, was dir gehört.
Gegner: Wegelagerer, Keorns Vasallen, zuletzt Keorn selbst mit seiner Garde. Ton: persönlich, Rache und Treue.

### Figuren (Namen sind Vorschläge)

| Figur | Rolle | Im Spiel |
|---|---|---|
| Der Erbe (Name offen) | Held, Sohn des toten Fürsten Aldric, galt seit fünf Jahren als tot | Der Spieler-Held, kämpft in der Linie |
| Brom, der Waffenmeister | Hat den Erben ausgebildet und versteckt; spricht die meisten Tafeln | Fester Teil des Start-Trupps, Stufe L5. Fällt er, kommentiert die nächste Tafel das |
| Keorn | Onkel, Usurpator, kalt und fähig | Gegner des Akts, führt in der dritten Stadt selbst |
| Ser Madok | Keorns Vasall und Hauptmann, Schildwall-Mann | Gegner in der zweiten Stadt |

### Erster Akt: Gerüst

Drei Städte auf dem Weg zurück nach Valdmark. Jede Stadt hat zwei bis drei Aufträge, ein Pflichtauftrag führt weiter. Die Karten sind die aus M4.

| Stadt | Lage | Aufträge | Gegner | Karte | Wendepunkt |
|---|---|---|---|---|---|
| **Erlach** | Grenzstadt im Exil, arm | Wegelagerer vertreiben (leicht), Viehdiebe stellen (leicht), Pflicht: Keorns Steuereintreiber abfangen | Banditen mit Speeren und Bögen, 15 bis 25 Mann; die Eintreiber mit Schild und ein paar Reitern | Waldrand | Nach dem Pflichtauftrag: Keorn erfährt, dass der Erbe lebt |
| **Morwick** | Handelsstadt, der Rat zögert | Karawane schützen, Söldner verjagen, Pflicht: Ser Madok an der Furt schlagen | Söldner gemischt; Madok mit Schildwall und Schützen, 40 bis 60 Mann | Sumpfpass | Der Rat von Morwick schließt sich an (Rekruten werden besser und billiger) |
| **Tannfeld** | Stadt eines Keorn-Vasallen, an der Grenze zu Valdmark | Vorräte erbeuten, Pflicht: Keorns Heer auf den Hügeln | Keorn mit Garde und Panzerreitern, 80 bis 100 Mann | Hügelland | Sieg: Keorn flieht nach Valdhall, Ende des ersten Akts. Niederlage: zurück nach Morwick, Keorn hält Tannfeld, der Auftrag bleibt |

Einführung (5 Tafeln): die Nacht des Verrats, fünf Jahre Versteck bei Brom, die Nachricht vom Tod des Vaters, der Aufbruch, Erlach. Danach geht es mit Endlos-Aufträgen weiter, bis der zweite Akt (der Weg nach Valdhall) geschrieben ist.

Die Größen der Schlachten wachsen mit dem Trupp: 15 gegen 20 am Anfang, um 100 gegen 100 am Ende des Akts. Das ist der Rahmen, den die Sim seit M4 trägt.

## Offene Fragen an Tarik

- Wie heißt der Erbe? Fester Name oder vom Spieler gewählt?
- Sollen Gefallene endgültig weg sein (Bindung, Härte) oder in der Stadt ersetzbar mit gleichem Namen (milder)?
- Bilder als Dioramen aus den eigenen Modellen: einverstanden, oder lieber gezeichnet (dann wäre das eine Ausnahme von „Assets nur über die Pipeline“)?
- Sprache der Story: nur Deutsch, oder von Anfang an zweisprachig angelegt?
