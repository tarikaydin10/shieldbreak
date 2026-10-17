# CLAUDE.md — Low-Poly Tactical Battle Sim (Arbeitstitel: SHIELDBREAK)

Dieses Dokument ist die Verfassung des Projekts. Claude Code liest es bei jeder Session.
Bei Konflikt zwischen diesem Dokument und einer Ad-hoc-Anweisung: nachfragen, nicht raten.

---

## 1. Vision in drei Sätzen

Third-Person-Schlachtsimulator im Low-Poly-Stil, Fokus ausschließlich auf den Battle-Mode (Skirmish, 50v50 bis 150v150).
Der Spieler ist **General und Krieger zugleich**: Er kämpft selbst in der Linie und führt gleichzeitig 4–6 Formationen.
Kämpfen muss sich physisch und wuchtig anfühlen; Kommandieren muss beim Kämpfen bedienbar sein; die Armee darf ohne Spieler nie dumm werden.

Referenz-Vorbild: Mount & Blade II Bannerlord (Schlachten). Direkter Kleinst-Konkurrent: Shieldwall (Steam).
Shieldwall beweist, dass die Fantasie verkauft (80 % positiv), aber nach ~5 h stirbt: zu wenig Tiefe, eine Gruppe, 25 Soldaten, Spieler im Nahkampf nutzlos, AI cheatet, kein brauchbarer Multiplayer. Jede dieser Lücken schließen wir.

## 2. Design-Säulen (jedes Feature muss auf mindestens eine einzahlen)

1. **Kampf mit Gewicht.** Energie-basiertes Schadensmodell, Impulse auf Treffer, Hitstop. Der Spieler ist ein Elite-Soldat (Wert ≈ 3–5 Linien-Soldaten), weder Gott noch Kanonenfutter.
2. **Führen im Gefecht.** Zwei Befehlswege: Radialmenü (einhändig, aus dem Kampf heraus) für schnelle Befehle, Kommandoansicht (Kamera hoch, Befehle aufs Terrain zeichnen) für alles Räumliche. Die Zeit läuft dabei weiter: Hinschauen kostet Aufmerksamkeit und macht den Helden verwundbar. Mehrere Formationen, Formationstypen, Standing Orders.
3. **Lesbare Masse.** Formationen, Moral und Zustand sind aus 40 m Distanz erkennbar. Low-Poly ist dafür ein Vorteil.
4. **Faire AI.** Die AI cheatet nie bei Stats oder Ressourcen. Schwierigkeit = Reaktionszeit, Fehlerrate, erlaubte Techniken. Der Spieler soll nach einer Niederlage sagen „ich hätte links stehen müssen", nie „die AI hat gecheatet".
5. **Multiplayer-fähig ab Tag 1.** Deterministische Sim, Input-Queue, Lockstep-tauglich. Singleplayer ist ein Lockstep-Spiel mit einem Teilnehmer.

### Nicht-Ziele (v1)
- Keine Kampagne, keine Weltkarte, keine Wirtschaft
- Keine Belagerungen (eigenes Projekt, Pathfinding-Hölle)
- Kein Massen-PvP mit vielen menschlichen Fußsoldaten (anderes Spiel, Serverkosten)
- Keine aktiven Ragdolls im Leben (Exanima-Problem), keine Cross-Platform-Determinismus-Garantie (PC only)

### Explizit verschoben, aber architektonisch vorbereitet
- **Doktrinen / vorprogrammierte Verhaltensregeln** (Phase 2): Formation-Behaviors sind bereits das, was AI und Standing Orders nutzen. Später nur UI zum Konfigurieren.
- **Gebäude-Destruction** (Phase 2): Navigation läuft auf eigenem Grid mit dynamischen Kosten, nicht auf gebackenem NavMesh. Destruction wird dann eine Kostenänderung in Zellen.
- **Befehlsreichweite / Sichtlinie / Banner** (Experiment in M3, Parameter, kein Fundament).
- **Ranked / Anti-Cheat Relay** (nach Launch).

## 3. Architektur

### 3.1 Drei Schichten, eine Befehls-API

```
Tactical   → Commander (Spieler ODER Utility-AI): erteilt FormationOrders
Formation  → Formation: Slots/Form, Facing, Tempo, Behavior; gibt Agenten Slot + Verhalten
Agent      → Soldat: Steering zum Slot, Zielwahl, Kampf-Statemachine
```

Spieler-Radialmenü und Commander-AI sind **zwei Clients derselben `FormationOrder`-API**. Niemals Spieler-Sonderpfade in der Formation.

### 3.2 Sim / Presentation strikt getrennt

- `Sim/` ist **pures C#**, deterministisch, kennt kein `UnityEngine` (erlaubt: `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst`).
- Agenten sind **Structs in NativeArrays**, keine GameObjects. Burst-Jobs über Arrays.
- `Presentation/` liest den Sim-State und interpoliert. GameObjects nur für Spieler, Spieler-Pferd und die ~20 nächsten Agenten (volle Animatoren). Rest: `Graphics.RenderMeshInstanced` + gebackene Animationen (VAT) ab M4.
- Sim-Tick: **60 Hz fixed**. Presentation läuft mit Framerate und interpoliert zwischen Tick N-1 und N.

### 3.3 Determinismus-Regeln (nicht verhandelbar)

- Kein `UnityEngine.Random`, kein `System.Random` in `Sim/`. Nur `SimRandom` (seeded xorshift) aus dem `SimWorld`.
- Kein `Time.deltaTime`, kein `DateTime` in `Sim/`. Nur `SimWorld.Tick` und `SimConstants.DT`.
- Keine PhysX-Abfragen in `Sim/`. Eigene Capsule-/Segment-Tests gegen das Spatial Grid.
- Keine `Mathf`, nur `Unity.Mathematics` (`math.*`). Keine `float.ToString`-basierten Vergleiche. Keine Dictionary-Iteration mit undefinierter Reihenfolge in der Sim.
- **Jede** Eingabe in die Sim (auch Spieler-Befehle im Singleplayer) läuft durch `InputQueue` und wird für Tick T+Delay eingereiht.
- `SimWorld.ComputeHash()` existiert ab M0. EditMode-Test: zwei Welten, gleicher Seed, gleiches Input-Log, 10.000 Ticks → gleicher Hash. Dieser Test läuft vor jedem Commit.

### 3.4 Ein Grid für drei Systeme

`WorldGrid` (Zellgröße 1 m, 2D über die Map):
- **Navigation:** Kosten pro Zelle, Formations-A* auf Grid, später Flowfields. Dynamische Kosten → Destruction-ready.
- **Spatial Hash:** Agenten-IDs pro Zelle für Nachbar-Queries, Hit-Detection, Separation.
- **Moral-Feld:** Panik-Wert pro Zelle, Diffusion pro Tick (`P += D·ΔP − k·P + Quellen − Senken`). Verluste = Quellen, Captains/Banner = Senken.

### 3.5 Kampfsystem (Kern-Mechanik, M1): vier Schichten

Das Kampfsystem ist eine **waffenunabhängige Grammatik**, keine Schwertkampf-Implementierung. Jeder Entwurf in Schicht A wird gegen die Frage geprüft: „Funktioniert das auch für Bogen (Spannen/Lösen) und Lanze (Couch)?" Wenn nein, ist er zu schwertspezifisch.

**Schicht A, Grammatik (universal, `Sim/Combat/Grammar/`)**
- Verben: `Attack(direction)`, `Block` (eine Taste, richtungslos), `Cancel` (Feint), `Special`, `Step`, `Jump`.
- Phasen für alles: `Windup → Release → Recovery`. Schaden/Projektil nur in Release. Feint = Cancel in Windup. Stun nach Guard Break/Treffer. Input-Buffer ~150 ms. Kein Lock-on.
- **Block-Regeln (kein Richtungsblock, bewusste Entscheidung):** (1) Halten blockt alles im Frontbogen (Schild ~120°, Waffe ~90°); Seite und Rücken sind offen. (2) Jeder geblockte Treffer kostet Stamina proportional zur Angriffsenergie; Stamina leer = Guard Break + Stun. (3) **Perfect Block:** Block-Druck innerhalb ~250–300 ms vor Kontakt kostet keine Stamina und staggert den Angreifer (Riposte-Fenster). Fenster bewusst lockstep-tauglich. Angreifer-Antworten: Feint, schwerer Angriff, Guard-Break-Special, Flanke.
- **Angriffsrichtungen sind Geometrie, kein Schlüssel:** Oben = Kopfzone, Seitenhieb = mehrere Ziele in Reihe, Stich = Reichweite und am Schildrand vorbei. Richtung aus Maus-Delta, Option „Auto-Richtung" (nächstes Ziel) für Einsteiger.
- Auflösung: **Energie am Kontakt** `E = ½·m·v²` × Penetrationsprofil × Zonen-Schwellwert (Rüstung ist Schwellwert, nicht Prozent). Penetrationsprofile: Schneide (× `|cos θ|` Edge-Alignment), Spitze (hoch), stumpf (niedrig, viel Stagger). Stich/Hieb/Wucht entstehen aus Geometrie, nicht aus Flags. v kommt vom Resolver (Trace-Delta, Ballistik, Relativgeschwindigkeit).
- Verteidigung: Block (Frontbogen, Stamina-Preis), Perfect Block (Timing-Bonus), Step (Ausweichen), Position.
- **Stamina als Energiehaushalt:** Schwung kostet kinetische Energie, Block absorbiert gegnerischen Impuls als Stamina.
- **Impact-Feedback (Presentation):** Hitstop 20–60 ms, Impuls auf Getroffenen aus Trefferwinkel, additive Hit-Reaction, Ragdoll nur beim Tod in Kameranähe. Kamera-Shake minimal.
- Bots spielen **dieselbe Grammatik** mit Reaktionsverzögerung + Fehlerwahrscheinlichkeit + Technik-Freigaben pro Level. Nie Stat-Cheats.

**Schicht B, Archetypen (`Sim/Combat/Archetypes/`, je ein Resolver + Mapping der Grammatik)**

Jeder Archetyp hat ein **eigenes Verb-Set mit Identität** (Elden-Ring-Prinzip), nicht nur andere Zahlen. Special = ein charakterdefinierender Move pro Archetyp.

| Archetyp | Richtungen | Block | Resolver | Verb-Set / Special | Meilenstein |
|---|---|---|---|---|---|
| Einhand + Schild | 4 | Schild, 120°, blockt Projektile | SwingTrace | Schildstoß (Guard Break) + schneller Nachstich | M1 |
| Zweihand | 4 | Waffe, 90°, hoher Stamina-Preis für Gegner | SwingTrace | Sprung-Overhead (Energie-Bonus), Morph im Windup | M1 |
| Stangenwaffe | 2 (Stich, Hieb) | Waffe, 90°, schwach | ThrustTrace / SwingTrace (Bogen) | Rundumschlag (alle Ziele im Bogen), Brace gegen Kavallerie | M2 |
| Bogen / Armbrust | Zielen | keiner | Projectile | Spannung halten / Nachladen | M3 |
| Wurfwaffe | Zielen | Rückfall auf Nahkampf-Slot | Projectile | – | M3 |
| Lanze (beritten) | Couch | keiner | ContactTrace (Relativgeschw.) | – | M4 |

Resolver: `SwingTrace` = 3–4 Sockets entlang der Klinge, pro Tick Segment-Tests gegen Körperzonen-Capsules (Kopf/Torso/Arme/Beine) im Spatial Grid, Hit-Set pro Schwung. **Die Socket-Positionen kommen aus der Posen-Kurve der Angriffs-Definition (Sim-seitig berechnet), nie aus dem Unity-Animator.** Nur so ist Hit-Detection deterministisch. `ThrustTrace` = linearer Sweep. `Projectile` = Fixed-Step-Ballistik mit Raycast pro Schritt. `ContactTrace` = Kontakt während Couch, Energie aus Relativgeschwindigkeit.

**Schicht C, Instanzen (ScriptableObjects, `Data/Weapons/`)**
Masse, Länge, Penetrationsprofil, Phasendauern pro Richtung, Stamina-Kosten, Ballistik. **Hier liegen auch die Feel-Regler:** Gewicht (Hitstop, Recovery, Impuls), Letalität (Energie vs. Schwellwerte), Tempo (Phasendauern). M1 liefert drei Presets (Schwer / Mittel / Tödlich); der Fun-Gate fragt, welches man zum elften Mal spielen will. „Wuchtig" ist eine Einstellung, keine Doktrin.

**Schicht D, Kontext-Modifikatoren (über der Grammatik, nie in ihr)**
- *Beritten:* Richtungsquelle = Kamera relativ zum Pferd, Lanze verfügbar, Pferdegeschwindigkeit geht in E ein.
- *1vX:* Attack-Tokens für Bots gegen den Spieler (max. 2 greifen gleichzeitig an, Rest positioniert und droht). Reines Agent-AI-Verhalten, kein Sonderpfad.
- *In Formation:* Bots halten Slot, greifen frontal an, Schildwall-Bots blocken bevorzugt.
- *Standoff:* Formation-Behavior, Linien warten auf Befehl oder Moralbruch.

**Spieler:** 3–4 Waffen-Slots, Wechsel kostet eine Recovery. Spieler = Elite (3–5 Soldaten wert), nie Gott.

**Game-Feel-Spec (Ziel: „geil und smooth", komplex genug, kein For Honor)**
Alles hier ist Schicht C oder Presentation. Werte sind Startpunkte, als ScriptableObject tunebar.
- *Reaktion:* Input → sichtbarer Windup-Start ≤ 50 ms (1 Tick), gemessen per Frame-Capture; das ist Teil des M1-Fun-Gates. Input-Buffer 150 ms, nie ein Input verschluckt. Feint-Fenster 60 % des Windups, Morph 40 %. Bewegung und Drehen während Windup/Recovery erlaubt (reduziertes Tempo). Kein Root-Motion für Bewegung, nur für Angriffs-Lunges.
- *Lesbarkeit:* Windup-Silhouetten pro Richtung aus 15 m unterscheidbar (oben: Waffe über Kopf; seitlich: weit ausgeholt; Stich: Hüfte). Windup 350–450 ms, Release 120–180 ms. Eigener Windup-Sound pro Richtung. Kein Kampf-HUD; Stamina als Vignette-Puls, Gesundheit über Haltung und Blut. Richtungsindikator optional, abschaltbar.
- *Impact:* Hitstop 40–80 ms skaliert mit Energie. Impuls auf Ziel aus Trefferrichtung (Stagger-Schritt + additive Oberkörper-Reaktion). Kamera-Kick 2–4° in Schlagrichtung, 60 ms, kein Rauschen; FOV-Puls 1–2° bei eigenem Treffer. Partikel flach, groß, kurz. Drei Sound-Layer pro Treffer (Whoosh, Material-Impact, Grunt). Kills: Ragdoll mit Impuls in Kameranähe, 100 ms Slow-Mo bei entscheidenden Kills.
- *Bewegung:* Beschleunigung 100 ms, Bremsen 80 ms. Kamera mit Schulter-Offset und leichtem Lag, Drehen auf Kamerarichtung in 150 ms. `Step` (4 Richtungen, 300 ms, 2 m, Stamina) statt Roll. Sprint-Angriff mit Energie-Bonus, der Charge in die Linie wird mechanisch belohnt.
- *Vokabular, vollständig:* 4 Angriffsrichtungen (oder Auto-Richtung), Block / Perfect Block, Feint, Step, Jump, Special pro Archetyp, Sprint-Angriff, Waffenwechsel. Keine Stances, Combo-Ketten, Unblockables, Richtungsblock oder heldenspezifischen Move-Sets. Tiefe = Timing, Stamina-Haushalt, Position, Täuschung, Waffenwahl.
- *Bot-Stufen über dieselbe Grammatik:* L1 hält Block; L3 blockt mit Reaktionszeit, feintet selten; L5 trifft Perfect Blocks, ripostiert, nutzt Specials.

### 3.6 Formationen (M2)

- `Formation` = Position, Facing, Typ, Breite/Tiefe → erzeugt Slots. Agent steuert immer seinen Slot, nie das Formationsziel.
- Slot-Zuordnung beim Umformen: Sinkhorn/Optimal-Transport auf Distanz² (keine kreuzenden Laufwege). Zuordnung stabil halten.
- Pathfinding **auf Formationsebene** (Grid-A*), Agenten machen nur lokales Steering (Slot, Separation, Terrain).
- Typen v1: Linie, Schildwall, Keil, Kreis, lockere Ordnung (Fernkampf). SDF-Formen als Experiment nach M2.
- Formation-Behaviors (von AI und Standing Orders genutzt): Halten, Vorrücken, Angriff, Folgen, Rückzug, Schildwall-bei-Kavallerie, Fernkampf-Screening, Rückzug-hinter-Infanterie.

### 3.7 AI (M3)

- **Agent-Ebene:** Behavior Tree oder flache Statemachine. Zielwahl, Slot halten, Kampf-Statemachine mit Reaktionszeit.
- **Formation-Ebene:** Behaviors reagieren auf Bedrohungs-Events (Kav im Anmarsch, Fernkampf-Beschuss, Flanke offen).
- **Commander-Ebene:** Utility-Scoring alle 2–3 s über Taktik-Kandidaten (Halten auf Höhe, Frontalangriff, Flanke L/R, Screening, Rückzug) mit Hysterese. Features: Stärkeverhältnis, Höhendifferenz, Kav-vs-Speer-Matchup, Munition, Moral.
- **Evaluator:** Lanchester-Modelle (Nahkampf linear, Fernkampf quadratisch) für Prognosen. Später MCTS auf abstrahiertem Zustand.
- Utility-Scores jederzeit im Debug-Overlay sichtbar.

### 3.8 Multiplayer (M6, Architektur ab M0)

- **Deterministischer Lockstep.** Über das Netz gehen nur Spieler-Inputs. Beide Clients simulieren dieselbe Armee aus demselben Seed.
- Modus: Kommandant gegen Kommandant, 1v1 bis 2v2, jeder mit eigener AI-Armee. Win-Condition: Rout ODER gegnerischer Kommandant tot.
- Input-Delay 100–150 ms. Falls Nahkampf darunter leidet: lokale Prediction nur für den eigenen Agenten (M6-Refinement).
- Transport: Steam Networking (Facepunch.Steamworks). Desync-Erkennung über periodischen Hash-Vergleich.

### 3.9 Kameras und Ansichten

Kameras sind reine Presentation; die Sim ist perspektivenunabhängig. Kamerazustand lebt nie in `Sim/`.

| Ansicht | Zweck | Meilenstein |
|---|---|---|
| **Debug-Draufsicht** | Freie Top-down-Kamera (Pan, Zoom, Rotation), Agenten anklicken, Grid/Slots/Moral/Pfade einblendbar. Hauptwerkzeug zum Testen von Formationen, Moral und AI ohne Nahkampf. | M0 |
| **Kampfkamera** (Standard, ~80 % der Spielzeit) | Third Person, Schulter-Offset, Zielpunkt per Kamera-Raycast, Cinemachine. | M1 |
| **Kommandoansicht** | Taste halten → Kamera fährt in ~0,5 s auf 30–40 m Höhe schräg über den Helden; loslassen → zurück. Formationen farbig hervorgehoben (Typ, Moral, aktueller Befehl ohne Klick lesbar). Befehle per Drag aufs Terrain (Position, Breite, Ausrichtung) über dieselbe `FormationOrder`-API. | M2 |
| First Person | Optionaler Kameraschalter, Kopf/Clipping ausblenden. | Phase 2 |

**Kommandoansicht, Regeln:**
- **Die Zeit läuft weiter.** Betreten/Verlassen der Kommandoansicht ist ein Sim-Input (`InputQueue`), weil es das Heldenverhalten ändert. Der Held wechselt in das Agent-Verhalten `HeroUnattended` (Block halten, in die eigene Linie zurückziehen, Bot-Grammatik Stufe L2), bleibt aber angreifbar.
- *Singleplayer:* optionale Zeitlupe (Default 0,3×) als Einsteigerhilfe. Umsetzung ausschließlich über weniger Sim-Ticks pro Echtzeitsekunde; `SimConstants.DT` ändert sich nie.
- *Multiplayer:* keine Zeitlupe, Echtzeit. Gegner in der Kommandoansicht = Held exponiert; das ist gewolltes Mindgame.
- Radialmenü bleibt parallel für schnelle Befehle ohne Kamerawechsel (Halten, Angriff, Folgen, Rückzug).

## 4. Tech-Stack

| Bereich | Entscheidung |
|---|---|
| Engine | Unity 6 LTS (6000.x), URP |
| Sprache | C# 9+, `Unity.Mathematics`, `Unity.Collections`, `Unity.Burst` (ohne Entities-Package) |
| Input | Unity Input System, Controller-Support von Anfang an |
| Kamera | Cinemachine (Third-Person, Schulter-Offset, Zielpunkt per Kamera-Raycast) |
| Daten | ScriptableObjects für Einheiten, Waffen, Formationen, Taktiken, Maps. Kein Balancing im Code. |
| Assets | Kapseln + Debug-Gizmos bis M2. Ab M4 Synty POLYGON oder eigener Pipeline-Style (offen, siehe `DECISIONS.md`). Alle eigenen Assets über `tools/assetgen` (headless Blender, Palette, Validator). |
| Animation | **Pose-basiert prozedural** (Overgrowth-Methode): Angriffe = 3–4 Key-Posen + Easing-Kurven + Laufzeit-IK (Unity Animation Rigging) + prozedurale Secondary Motion. Angriffs-Definition ist ein ScriptableObject, aus dem Grammatik (Phasendauern) und SwingTrace (Waffenbahn) dieselben Daten lesen. Posen werden in Blender gesetzt (Blender MCP für Batch/Varianten), optional Cascadeur für physikalisch plausible Referenzposen. Mixamo nur für Locomotion/Idles. Gekaufte Clips nur als Fallback, falls der M0-Prototyp scheitert. |
| Tooling | Blender MCP (ahujasid/blender-mcp) und Unity MCP (Open Source, Stand prüfen) als Claude-Anbindung für Posen, Prefabs, ScriptableObjects. |
| Netz | Facepunch.Steamworks (M6) |
| Tests | Unity Test Framework, EditMode. Determinismus-Test ist Pflicht. |
| Versionierung | Git + Git LFS für Binärassets. Alles direkt auf `main`, keine Meilenstein-Branches; jeder Commit nur mit grünen Tests. |

## 5. Repo-Layout und Ordnerstruktur

```
shieldbreak/                 # Repo-Root (dieses Dokument liegt hier)
  CLAUDE.md
  DECISIONS.md               # Architektur-Entscheidungen mit Datum und Grund
  IDEAS.md                   # Ideen außerhalb des aktuellen Meilensteins
  .mcp.json                  # Claude-Code-MCP-Server (Blender; Unity trägt sich selbst ein)
  docs/                      # Design-Notizen, Research, Posen-Vorgaben
  art/
    style/                   # palette.json, style.json, style-bible.md (Quelle der Wahrheit für den Look)
    specs/                   # eine JSON-Spec pro Asset
    contact-sheet.png        # alle Assets nebeneinander, von der Pipeline gerendert
  tools/                     # Setup-Skripte
    assetgen/                # headless-Blender-Pipeline: Generatoren, Validator, run.ps1
  game/                      # Unity-Projekt (von Unity Hub erzeugt, URP-Template)
```

Unity-Projekt (`game/`):

```
Assets/_Project/
  Scripts/
    Sim/                 # pures C#, deterministisch, KEIN UnityEngine
      Core/              # SimWorld, SimConstants, SimRandom, InputQueue, SimHash
      Grid/              # WorldGrid, Spatial Hash, Nav-Kosten, Moral-Feld
      Agents/            # AgentData (struct), AgentSystem (Jobs), Steering
      Combat/
        Grammar/         # CombatAction, PhaseStateMachine, DefenseResolution, EnergyModel, Stamina
        Archetypes/      # OneHandShield, TwoHand, Polearm, Ranged, Thrown, Lance (Mapping + Resolver)
        Resolvers/       # SwingTrace, ThrustTrace, Projectile, ContactTrace
        Context/         # MountedModifier, AttackTokens, FormationCombatRules
      Formations/        # Formation, SlotGenerator, SlotAssignment, FormationBehaviors
      Nav/               # GridAStar, Flowfield (später)
      Morale/            # MoraleDiffusion, RoutLogic
      AI/                # AgentBrain, FormationAI, CommanderUtility, Lanchester
      Orders/            # FormationOrder-API (gemeinsam für Spieler + AI)
    Presentation/        # SimRunner, Interpolation, Animatoren, VAT-Rendering, VFX, Audio
      Cameras/           # DebugTopDownCamera, CombatCamera, CommandViewCamera
    Player/              # Controller, Input-Mapping, Radialmenü, CommandView (Drag-Orders)
    Data/                # ScriptableObject-Definitionen
    Debug/               # Overlay: Slots, Moral-Feld, Utility-Scores, Pfade, Hashes
  Data/                  # SO-Assets (Units, Weapons, Formations, Tactics, Maps)
  Scenes/
  Art/
Assets/Tests/EditMode/   # Determinismus, Slot-Zuordnung, Schadensformel
```

## 6. Meilensteine mit Fun-Gates

Budget: ~10 h/Woche, ~650 h gesamt. Ein Meilenstein gilt erst als erreicht, wenn sein Fun-Gate mit Ja beantwortet ist. Bei Nein: Meilenstein nachbessern, nicht weiterbauen.

| Phase | Stunden | Inhalt | Fun-Gate |
|---|---|---|---|
| **M0 Skeleton** | 50 | Fixed-Step-SimWorld, InputQueue, SimRandom, Hash-Test, WorldGrid, 500 Kapsel-Agenten laufen zu Zielen, Debug-Draufsicht + Overlay, Interpolation. **Animations-Prototyp:** Synty-Rig in Blender, 4 Key-Posen für Oben-Hieb, in Unity per Posen-Interpolation + IK abspielen, neben einem Mixamo/Kubold-Clip vergleichen. | Determinismus-Test grün, 500 Agenten bei 60 Hz unter 2 ms Sim-Zeit, prozeduraler Hieb lesbar und nicht steril (sonst Fallback auf Clips entscheiden) |
| **M1 Duell** | 110 | Kampfgrammatik (Schicht A), Archetypen Einhand+Schild und Zweihand, SwingTrace, Energiemodell, Stamina, Hitstop/Impulse, Third-Person-Controller, Waffen-Slots, Bot mit Reaktionszeit (3 Levels), drei Feel-Presets. Papier-Test der Grammatik gegen Bogen und Lanze. | 10 Duelle gegen Bot pro Preset: welches willst du zum elften Mal spielen? |
| **M2 Kompanie** | 130 | Formationen + Slots, FormationOrder-API, Radialmenü (einhändig), **Kommandoansicht mit Drag-Orders, `HeroUnattended`-Verhalten, SP-Zeitlupe**, 20v20, Moral-Diffusion, Rout | Formation reagiert sichtbar und sofort auf Befehl; Rout fühlt sich verdient an; Wechsel Kampf ↔ Kommandoansicht fühlt sich riskant, aber nicht lähmend an |
| **M3 Feldherr** | 120 | Formation-Behaviors, Standing Orders (3 Presets), Commander-Utility-AI, Lanchester, 50v50, Experiment Befehlsreichweite | (a) Nach Niederlage: „ich hätte woanders stehen müssen". AI-Entscheidungen im Overlay nachvollziehbar. (b) **Feldherr-Test:** Eine 50v50-Schlacht, nur aus der Kommandoansicht geführt (Held passiv), ist spannend. Wenn nein, trägt die taktische Ebene nicht; vor M4 nachbessern. |
| **M4 Schlacht** | 90 | 100v100+, Reiterei (Pferd als Agent, Couching), 3 Maps mit Höhe/Wald/Deploy-Zonen, Synty-Assets, VAT-Rendering | Drei fremde Spieler spielen 30 min freiwillig weiter |
| **M5 Polish** | 50 | Nachschlachtbericht (wer hat wann warum gefeuert), Sound, Kamera-Feel, Steam-Page | Trailer-Material existiert |
| **M6 Multiplayer** | 120 | Lockstep-Scheduler, Steam-Transport, Lobby mit Teams (1v1, 2v2), Desync-Erkennung, Pre-Battle-Phase | Match gegen Freund läuft 20 min ohne Desync; beide wollen Revanche |

### Phase 2 (nach M6, Reihenfolge offen)
Doktrinen-UI · Feldherr-Modus (nur Kommandoansicht, kein Held) · Sandtisch-Aufstellung · First-Person-Kamera · Gebäude-Destruction · SDF-Formationen · PBD-Kohäsion/Druck · GPU-Hit-Detection · MCTS-Commander · Ranked/Relay

### M0 Taskliste (aktueller Meilenstein)

Reihenfolge einhalten; jeder Task endet mit grünem Test oder sichtbarem Ergebnis und einem Commit.

1. **Sim/Core** — `SimConstants` (`TickRate = 60`, `DT = 1f/60f`), `SimRandom` (seeded xorshift, struct, keine Allokation), `InputCommand` (struct: `Tick`, `PlayerId`, `Type`, Payload) + `InputQueue` (deterministisch sortiert nach Tick, PlayerId, Sequenz), `SimWorld` (`Tick`, `Step()`, besitzt alle NativeArrays, `IDisposable`), `SimHash` (FNV-1a 64 über Rohbytes aller Sim-Arrays + Tick).
2. **Determinismus-Test** — `game/Assets/Tests/EditMode/DeterminismTests.cs`: zwei `SimWorld` mit gleichem Seed und gleichem Input-Log, 10.000 Ticks, Hash nach jedem 100. Tick vergleichen; zusätzlich ein Negativtest (anderer Seed → anderer Hash). Assembly-Definitionen anlegen: `Shieldbreak.Sim` (ohne UnityEngine-Referenz, `noEngineReferences: true`), `Shieldbreak.Presentation`, `Shieldbreak.Tests.EditMode`.
3. **Sim/Grid** — `WorldGrid` (Zellgröße 1 m, Breite/Höhe aus Map-Daten, Kosten-Array), Spatial Hash per Counting-Sort jeden Tick (deterministische Reihenfolge, keine HashMaps), Nachbar-Query über 3×3-Zellen.
4. **Sim/Agents** — `AgentData` (struct: Id, Team, Position `float2`, Velocity, Facing, Target, Radius), Burst-Job `AgentMoveJob`: Seek zum Ziel + Separation über Spatial Hash, max. Beschleunigung. Burst-Jobs immer kompiliert ausführen (Burst im Editor nicht deaktivieren), sonst weichen Editor- und Build-Ergebnisse ab.
5. **Presentation** — `SimRunner : MonoBehaviour` (Akkumulator, fixe Ticks, Zeitskala für SP, Interpolations-Alpha zwischen Tick N-1 und N), Kapsel-Rendering per `Graphics.RenderMeshInstanced`, Teamfarbe.
6. **Debug-Draufsicht + Overlay** — `DebugTopDownCamera` (WASD/Maus-Pan, Zoom, Rotation), Klick setzt Ziel für selektierte Agenten (über `InputQueue`!), Overlay: Grid, Ziele, Velocity-Pfeile, Anzeige Sim-ms pro Tick und aktueller Hash.
7. **Performance-Check** — 500 Agenten, Profiler-Marker um `SimWorld.Step()`, Ziel < 2 ms.
8. **Animations-Prototyp** — nach `docs/animation/overhead-hieb.md` über Blender MCP + Unity MCP; separate Szene `Scenes/Proto_Animation.unity`, berührt `Sim/` nicht.

**Startprompt für Claude Code:** „Lies CLAUDE.md. Wir sind in M0. Setze Task 1 und 2 der M0-Taskliste um: Assembly-Definitionen, Sim/Core und den Determinismus-Test. Danach Tests ausführen und mir das Ergebnis zeigen. Noch keine Agenten, kein Rendering."

## 7. Arbeitsregeln

- **Jeden Freitag ein spielbarer Build.** Egal wie hässlich. Eine Woche ohne spielbaren Stand = falsch priorisiert.
- **Keine Assets vor M4.** Kapseln, Pfeile, Gizmos. Wer früh an Optik dreht, poliert ein Spiel, das es noch nicht gibt.
- **Debug-Tools sind Features** mit eigener Zeit im Plan. Ohne Visualisierung debuggt niemand Formationen oder Moral.
- **Alle 4 Wochen drei fremde Tester.** Beobachten, nichts erklären.
- **Data-driven oder gar nicht.** Jede Zahl, die ein Designer tunen will, steht in einem ScriptableObject.
- **Scope wächst nur in Phase 2.** Neue Ideen landen in `IDEAS.md`, nicht im Sprint.

## 8. Regeln für Claude Code in diesem Repo

- Alle Unity-Pfade in diesem Dokument sind relativ zu `game/`. Claude Code wird im Repo-Root gestartet.
- `Sim/` darf **niemals** `using UnityEngine;` enthalten. Bei Verstoß: ablehnen und Alternative über `Unity.Mathematics` vorschlagen.
- Vor jedem Commit: `game/Assets/Tests/EditMode` ausführen (über Unity MCP `run_tests` oder Unity-Batchmode), Determinismus-Test muss grün sein.
- Neue Sim-Systeme als Burst-kompatible Jobs über NativeArrays schreiben. Keine Klassen-Allokationen im Tick.
- Keine neuen Features außerhalb des aktuellen Meilensteins ohne Rückfrage. Ideen nach `IDEAS.md`.
- Balancing-Werte nie hardcoden; ScriptableObject-Feld anlegen.
- Code-Kommentare und Identifier auf Englisch; Dokumentation (dieses Dokument, Design-Notizen) auf Deutsch.
- Bei Architektur-Entscheidungen, die von Abschnitt 3 abweichen: erst `DECISIONS.md` ergänzen (Datum, Grund, Alternative), dann umsetzen.
- Boilerplate, Jobs, Tests, Tooling: Claude Code. Design-Entscheidungen, Tuning, Fun-Gate-Bewertung: Tarik.
- **Drittanbieter-Assets:** Gameplay-Frameworks (Combat-, Formation-, AI-Systeme) kommen nie ins Repo; sie setzen MonoBehaviour/PhysX/NavMesh voraus und brechen Determinismus und Sim/Presentation-Trennung. Erlaubt sind reine Presentation-Tools (Rendering, VFX, Audio, Editor-Tooling) und Daten (Modelle, Posen). Gekaufte Systeme dürfen als Teardown in einem separaten Projekt studiert werden; Erkenntnisse nach `docs/research/`.
- **Jede Eingabe ist dokumentiert** (Admin-Menü, F1): Jede Taste, Maus- oder Gamepad-Eingabe ist eine Action in `game/Assets/_Project/Resources/Input/Shieldbreak.inputactions` mit einer deutschen Beschreibung in `controls.de.json` daneben, eine Action-Map pro Feature, die das Feature beim Aktivieren einschaltet. Kein direktes Abfragen von Geräten (`Keyboard.current`, `Input.GetKey` usw.); `ControlsDocumentationTests` prüft beides. Neue Features mit sichtbarem Zustand implementieren `IAdminStatus`, damit sie im Menü eine Statuszeile bekommen. Esc = Pause (nur Singleplayer, über `GamePause`; die Sim läuft dann null Ticks).
- **Assets nur über die Pipeline** (`art/style/style-bible.md`): Finale Modelle entstehen ausschließlich aus einer Spec in `art/specs/` über `tools/assetgen/run.ps1` und landen in `game/Assets/_Project/Art/Generated/`. Nie FBX von Hand ablegen oder nachbearbeiten, nie Farben als Hex in Specs. Passt ein Asset nicht, Generator oder `style.json` ändern, nicht das Asset. Blender MCP und KI-Text-zu-3D nur zum Erkunden bzw. als Rohmaterial für einen Normalize-Generator. Vor jedem Commit mit Änderungen an `art/` oder `tools/assetgen/`: `run.ps1` (Kontaktbogen ansehen) und `run.ps1 -Check`.
- **MCP-Regeln (Blender MCP, Unity MCP):** Vor jeder Batch-Operation über MCP ein Git-Commit; in Blender vorher speichern. Keine Lösch- oder Überschreib-Operationen in Blender/Unity ohne explizite Bestätigung. Server bleiben auf localhost; nur eine Client-Instanz pro Server. Unity-MCP-Package auf feste Version gepinnt (`#v10.0.0`), Upgrade nur bewusst mit Eintrag in `DECISIONS.md`. Konfiguration liegt in `.mcp.json` im Repo.

## 9. Offene Entscheidungen (bis M3 erlaubt)

- Setting: historisch (Antike/Mittelalter) vs. Low-Fantasy. Beeinflusst Säulen nicht.
- Spielname. Arbeitstitel SHIELDBREAK.
- Progression zwischen Schlachten (Skirmish-only vs. leichte Meta).
- Befehlsreichweite: bleibt oder fliegt, entscheidet M3-Playtest.

## 10. Setup (einmalig, Windows)

1. `tools/setup-windows.ps1` in PowerShell ausführen (installiert uv, Blender-MCP-Addon, registriert Blender-MCP in Claude Code, initialisiert Git + LFS).
2. Unity Hub → New Project → Universal 3D → Location `C:\Users\tarik\projects\shieldbreak`, Project name `game`, Unity 6000.5.
3. Nach dem ersten Öffnen: `tools/add-packages.ps1` ausführen (ergänzt Burst, Collections, Mathematics, Input System, Cinemachine, Animation Rigging, Test Framework, Unity MCP in `game/Packages/manifest.json`), Unity lädt nach.
4. In Unity: Window → MCP for Unity → Configure All Detected Clients.
5. In Blender: Preferences → Add-ons → „MCP for Blender" aktivieren; im Viewport `N` → MCP-Tab → Connect to Claude.
6. Im Repo-Root `claude` starten, `/mcp` prüfen: blender und unity müssen verbunden sein.

## 11. Referenzen

- Bannerlord Dev-Blogs: Combat AI (3 Ebenen), AI Melee Overhaul, Battle Terrain System
- Chivalry/Mordhau: Swing-Tracing-Hit-Detection
- Ashes of the Singularity: Army → Unit → Element Befehlspropagation
- Treuille et al. 2006, Continuum Crowds (Dichtefelder)
- Lanchester, Aircraft in Warfare (1916), Kampfmodelle
- Shieldwall (Steam): Positiv- und Negativ-Review-Analyse, siehe `docs/research/shieldwall.md`
