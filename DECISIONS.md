# DECISIONS.md — Architektur- und Design-Entscheidungen

Jede Abweichung von CLAUDE.md Abschnitt 3 wird hier zuerst eingetragen (Datum, Entscheidung, Grund, verworfene Alternative), dann umgesetzt.

## 2026-10-04 — Gründungsentscheidungen

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Nur Battle-Mode, keine Kampagne | Shieldwall zeigt: die Fantasie verkauft sich mit Battle allein; Kampagne verdoppelt den Scope | Weltkarte, Wirtschaft |
| Kernfantasie „General und Krieger zugleich" | Bewährt (Bannerlord), Lücke in der Ausführung, bestätigt durch Shieldwall-Reviews | „Deine Anwesenheit entscheidet" (ungetestet, Befehlslatenz abgelehnt) |
| Drei AI-Ebenen (Tactical / Formation / Agent) mit einer gemeinsamen Order-API | Bannerlord-Architektur; Spieler und AI nutzen dieselbe Schnittstelle | Spieler-Sonderpfade |
| Deterministische Sim, Fixed 60 Hz, Lockstep-Multiplayer | Multiplayer gegen Freunde gewünscht; AI-Armeen kosten so null Bandbreite; Befehle vertragen Input-Delay | Server-autoritativ (Serverkosten, 500 Agenten syncen), Rollback (Resim zu teuer) |
| Sim ohne UnityEngine, Agenten als Structs in NativeArrays + Burst | 500+ Agenten, Determinismus per Konstruktion | Volles DOTS/Entities (Lernkurve, Tooling), MonoBehaviour pro Agent |
| Eigenes WorldGrid statt NavMesh | Eine Datenstruktur für Nav, Spatial Hash, Moral-Diffusion; Destruction später als Kostenänderung | Unity NavMesh (nicht deterministisch, Re-Bake bei Destruction) |
| Hit-Detection: Segment-vs-Capsule auf Posen-Kurven, Sim-seitig | Deterministisch, ~100 Zeilen; PhysX-Sweeps sind frame- und maschinenabhängig | PhysX Sweep/BoxCast (Desync-Risiko) |
| Kampfgrammatik in vier Schichten (Grammatik / Archetyp / Instanz / Kontext) | Spieler wechselt frei zwischen Waffenklassen; Bots spielen jede Klasse; neue Waffen = Daten + Resolver | Schwertspezifisches Kampfsystem |
| Block mit einer Taste (Frontbogen, Stamina-Preis, Perfect Block 250–300 ms) | Aufmerksamkeit muss fürs Kommandieren frei bleiben; 1vX ist Normalfall; lockstep-tauglich; Bots fair tunebar | Richtungsblock (Lernhürde, 1vX unmöglich, Bots entweder unfair oder dumm), Chamber |
| Angriffsrichtungen bleiben als Geometrie (Kopfzone, Mehrfachtreffer, Reichweite) | Expressive Kontrolle ohne Block-Schlüssel-Logik; Auto-Richtung als Option | Reduktion auf Light/Heavy |
| Archetypen mit eigenem Verb-Set (Elden-Ring-Prinzip) | Waffenidentität statt nur anderer Zahlen | Zwei Richtungssysteme mit anderen Werten |
| Energie-Schadensmodell (½mv² × Penetrationsprofil × Zonen-Schwellwert) | Hieb/Stich/Wucht aus Geometrie; Poise/Stagger fällt gratis heraus; Stamina-Kosten beim Block ebenso | Lookup-Tabellen Waffe × Zone |
| Animation pose-basiert prozedural (Overgrowth-Methode) | Planbar, einheitlich, justierbar, deterministisch; Posen-Kurve ist Quelle für Grammatik UND Trace | Gekaufte Clips (fremde Timings, Retargeting), Mocap, Text-to-Motion |
| Blender MCP + Unity MCP als Tooling | Posen, Prefabs, ScriptableObjects direkt aus Claude Code | – |
| Keine Drittanbieter-Gameplay-Frameworks im Repo | Architektur (MonoBehaviour/PhysX/NavMesh) widerspricht Determinismus und Sim-Trennung; Umbau teurer als Neubau | Directional Combat System, Large Scale Formation System (Teardown erlaubt) |
| Spieler = Elite (3–5 Soldaten wert) | Shieldwall: Held nutzlos im Nahkampf, Javelin OP im MP. Beides vermeiden | Gott-Modus (Dynasty Warriors) |
| 4–6 Formationen pro Seite, Agentenzahl skaliert (50v50 bis 150v150) | Befehlsinterface, Lesbarkeit und Commander-AI bleiben konstant | Formationszahl skaliert |
| Multiplayer-Modus: Kommandant gegen Kommandant (1v1, 2v2) mit AI-Armeen | Einziger Modus, den kein Konkurrent bietet; Bannerlord-Captain-Modus scheitert an zu kleinen Trupps | Massen-PvP mit menschlichen Fußsoldaten |

## 2026-10-07 — Kameras und Ansichten

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Third Person als Hauptkamera, plus Kommandoansicht (Taste halten, Kamera hoch, Drag-Orders) | Keine Einzelperspektive erfüllt „General und Krieger zugleich"; Third Person trägt den Kampf, die Draufsicht das Räumliche | First Person (Formationen unsichtbar, Low-Poly wirkt aus der Nähe billig), Isometrisch (Kampf wird ARPG), Vogelperspektive (kein Krieger mehr) |
| Zeit läuft in der Kommandoansicht weiter; Held geht in `HeroUnattended` | Hinschauen kostet Aufmerksamkeit statt Befehlslatenz; ehrliche Spannung zwischen Kämpfen und Führen; im MP ein Mindgame | Pause in der Kommandoansicht (bricht Lockstep, entwertet die Spannung) |
| SP-Zeitlupe nur über weniger Ticks pro Echtzeitsekunde | Determinismus bleibt unangetastet | Variables DT |
| Debug-Draufsicht ab M0 als Hauptwerkzeug für Formation/Moral/AI | Die schwierigsten Systeme ohne Nahkampf testen; die isometrische Version entsteht dabei ohne eigenes Projekt | Separates isometrisches Übungsprojekt (verteilt das Zeitbudget, keins wird fertig) |
| M3 bekommt den Feldherr-Test als zweites Fun-Gate | Billigster frühester Beweis, dass die taktische Ebene trägt | – |
| Feldherr-Modus, Sandtisch, First Person → Phase 2 | Fast gratis nach M3, aber kein Kern | Minigames |

## 2026-10-07 — Sim-Reinheit per Test statt `noEngineReferences`

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `Shieldbreak.Sim.asmdef` mit `noEngineReferences: false`; die Regel „kein UnityEngine in `Sim/`" sichert der EditMode-Test `SimPurityTests` (scannt `Sim/**/*.cs` auf `UnityEngine`, `Mathf`, `Time.`, `System.Random`, `DateTime`, `Stopwatch`, `Physics`) | In Unity 6000.5 liegen `NativeArray`, `Allocator`, `UnsafeUtility` und sogar `Unity.Mathematics` (`UnityEngine.MathematicsModule`) in Engine-Assemblies; mit `noEngineReferences: true` kompiliert `Sim/` nicht (CS1069/CS0246, verifiziert) | `noEngineReferences: true` (nicht kompilierbar), eigene Container statt `NativeArray` (bricht Burst-Jobs) |

## 2026-10-17 — Engine bleibt Unity, Asset-Pipeline über headless Blender

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Engine bleibt Unity 6 (URP) | 150v150 + Hit-Traces brauchen Burst/NativeArrays; Determinismus, Input-Latenz, Cinemachine/Animation Rigging und Steam-Release sind dort gelöst; M0 Task 1+2 stehen | Three.js (GC-Ruckler, Browser-Mathe nicht garantiert deterministisch, Input-Latenz, Steam nur über Electron/Tauri), eigene C++-Engine (Hunderte Stunden bei ~650 h Gesamtbudget) |
| Finale Assets entstehen nur über `tools/assetgen` (headless Blender, `--factory-startup`): Spec in `art/specs/` → Generator → Validator gegen `art/style/` → FBX + `manifest.json` + Kontaktbogen | Style-Konstanz entsteht durch harte Regeln im Repo, nicht durch das Gedächtnis eines Menschen oder Modells. Jede Session (Claude, anderes Modell, Mensch) muss durch denselben Filter | Freies Modellieren über Blender MCP (nicht reproduzierbar), KI-Text-zu-3D direkt ins Spiel (Topologie/Texturen inkonsistent) |
| Eine Palette-Textur (`palette.json` → `palette.png`, Point-Filter), ein Material `M_Palette`, eine Palettenfarbe pro Fläche, Flat Shading | Einheitlicher Look ohne Texturarbeit; KI-tauglich, weil Farben nur per Name referenziert werden | Eigene Texturen pro Asset, Vertex-Farben |
| Reproduzierbarkeit über Geometrie-Hash (`run.ps1 -Check`), Unverfälschtheit über FBX-Hash im Manifest (Unity-Import-Check + `ArtPipelineTests`) | FBX enthält Zeitstempel, Byte-Gleichheit wäre unmöglich; der Geometrie-Hash prüft, was zählt | Byte-Vergleich der FBX |
| Pipeline-Proof vorgezogen in M0 (Ausnahme zu „Keine Assets vor M4") | Ausdrücklicher Wunsch, früh zu beweisen, dass der Style trägt. Es ist Tooling, keine Optik-Politur; die fünf Proof-Assets sind Platzhalter | Pipeline erst in M4 |

## 2026-10-17 — Agenten (M0 Task 4)

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Alle Sim-Burst-Jobs mit `FloatMode.Deterministic` | Burst wählt sonst je nach CPU (AVX2 oder nicht) anderen Code, z. B. FMA; zwei Lockstep-Rechner mit demselben Build würden auseinanderlaufen | Standard-FloatMode (schneller, aber CPU-abhängig) |
| Sim-Jobs schreiben nur ihren eigenen Index aus schreibgeschützten Eingaben (Steering → Scratch-Array → Integration) | Parallele Ausführung kann das Ergebnis nicht verändern | In-place-Update der Agenten im Steering-Job (Ergebnis hinge von der Thread-Reihenfolge ab) |
| Ziele nur per `SetTargetRange`-Befehl über die InputQueue; Agenten-Spawns nur beim Szenario-Setup vor Tick 0 | Jede Eingabe in die Sim läuft durch die Queue (3.3); Spawns sind Map-Daten und auf allen Peers gleich | Direktes Setzen von Zielen aus der Presentation |
| Bewegungswerte im ScriptableObject `AgentMovementDefinition` (Assembly `Shieldbreak.Data`), die Sim sieht nur den Struct `AgentMoveSettings` | Data-driven (Arbeitsregel 7), `Sim/` bleibt frei von UnityEngine | Werte als Konstanten im Job |

## 2026-10-17 — Sandbox mit Pipeline-Soldaten statt Kapseln

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `M0_Sandbox` zeichnet Agenten mit den Pipeline-Assets (Dummy, Helm, Schild, Speer) per Instancing, Boden aus Graskacheln; Ausnahme zu „Kapseln bis M2 / Keine Assets vor M4" | Wunsch von Tarik: die spielbare Szene soll wie der Style-Proof aussehen. Kostet kaum Zeit, weil die Assets schon existieren; die Sim bleibt unberührt | Kapseln bis M2, Umschalter Kapsel/Pipeline |
| Aussehen eines Soldaten im ScriptableObject `SoldierVisualDefinition` (Meshes, Befestigungspunkte, Auswahlring) | Data-driven; Waffen und Teams später ohne Code tauschbar | Hartcodierte Offsets im Renderer |
| Schwert, Bogen, Pfeil kommen erst mit ihren Archetypen (M1 bzw. M3) in die Pipeline | Maße und Griffpunkte müssen zur Kampf-Grammatik passen | Jetzt auf Vorrat bauen |

## Offen (Entscheidung bis M3)

- Setting (historisch vs. Low-Fantasy)
- Spielname (Arbeitstitel SHIELDBREAK)
- Befehlsreichweite / Sichtlinie: Experiment in M3, Parameter
- Progression zwischen Schlachten
- Synty POLYGON ab M4 vs. eigener Pipeline-Style (Synty passt nur, wenn Palette und Proportionen übernommen werden)
