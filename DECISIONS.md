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

## 2026-10-17 — Performance (M0 Task 7) und Waffenbahn (M0 Task 8, Sim-Teil)

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Task 7 erfüllt: 500 Agenten, zwei Blöcke im Kontakt, Median 0,11 ms pro `SimWorld.Step` (p95 0,14 ms, Budget 2 ms); `PerformanceTests` prüft das bei jedem Lauf | Messung im Härtefall (volle Nachbarschaften), Median robust gegen Ausreißer | Durchschnitt über zufällig verteilte Agenten (zu harmlos) |
| Waffenbahn als Keyframes (Hand + Klingenrichtung) mit parametrischem Easing (`Ease.In/Out/InOut` + Exponent) statt `AnimationCurve` | `AnimationCurve` gehört zu UnityEngine und ist in `Sim/` verboten; die Bahn ist Quelle der SwingTrace-Sockets und muss in der Sim laufen | `AnimationCurve` (nur Presentation-tauglich) |
| Klingenrichtung per Slerp (konstante Winkelgeschwindigkeit) | Normalisierter Lerp ließ die Winkelgeschwindigkeit großer Schwünge in der Mitte hochschnellen (Spitze 89 m/s statt plausibler Werte); geht direkt in die Kontaktenergie | Normalisierter Lerp |
| Release als Ease-InOut (Exponent 2) statt reines Ease-in | Die Posen-Vorgabe verlangt die höchste Klingengeschwindigkeit kurz vor dem Kontakt (t ≈ 0,63); Ease-in hätte sie genau auf den Kontakt gelegt. Gemessen: 0,628 | Ease-in kubisch |
| Rig für den Posen-Prototyp offen | Synty-Rig (vorhanden: POLYGON Apocalypse) oder Low-Poly-Gliederpuppe aus der Pipeline; Entscheidung Tarik | – |

## 2026-10-17 — Start M1, Rig für den Prototyp

| Entscheidung | Grund | Verworfen |
|---|---|---|
| M1 beginnt auf Tariks Anweisung („nach bestem Wissen"), bevor das M0-Fun-Gate formal bewertet ist; die Bewertung des Hiebs (`Proto_Animation`) bleibt offen und wird nachgeholt | Technische M0-Kriterien erfüllt (Determinismus grün, 500 Agenten 0,11 ms); Tarik will spielbaren Kampf | Warten auf das Fun-Gate |
| Vorerst Low-Poly-Gliederpuppe aus der Pipeline mit prozeduraler Arm-IK statt Synty-Rig | Sofort nutzbar, deterministisch, stilkonsistent; die Sim (Waffenbahn, SwingTrace) hängt nicht am Rig. Synty-Vergleich wird nachgeholt | Synty POLYGON Apocalypse jetzt einbinden (Retargeting-Aufwand vor dem ersten Kampf) |

## 2026-10-17 — Nur noch `main`

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Alle Arbeit direkt auf `main`; Branches `m0` und `m1` gelöscht | Wunsch von Tarik: ein Stand, kein Abgleich zwischen Branches. Absicherung über die Regel „Commit nur mit grünen Tests" (inkl. Determinismus) | Branch pro Meilenstein (CLAUDE.md alt) |
| M1 Schritt 1: Kampf-Grammatik in der Sim, Bots nach derselben Grammatik, SwingTrace mit Energie-Schaden | CLAUDE.md 3.5; Bot-Stufen unterscheiden sich nur in Reaktion, Fehlerquote, Techniken (L5:L1 12:0, L5:L3 11:1) | Stat-Boni für höhere Stufen |
| Seitenhiebe und Oben-Hieb mit Durchschwung: die Kontakt-Pose liegt hinter dem Ziel, das Ziel wird im schnellsten Moment des Release gekreuzt; Werte per Offline-Analyse der Waffenbahn gesetzt | Sonst trifft die Klinge erst am abbremsenden Ende, Schaden je nach Richtung um Faktor 5 verschieden | Kontakt-Pose genau auf dem Ziel |
| Spieler-Bewegung als gespeicherte Absicht pro Spieler (Sim-Zustand, gehasht), jeden Tick neu angewendet | Ein verspäteter Befehl (Jitter) lässt den Helden nicht stehen | Bewegungsziel nur beim Befehl setzen |

## 2026-10-17 — M1 Schritt 2: spielbares Duell

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Hitstop im Singleplayer über angehaltene Sim-Ticks (wie Zeitlupe, `DT` unverändert) | Einfach, echter „Gewicht"-Effekt; im Multiplayer (M6) wird er rein visuell | Hitstop nur in der Darstellung schon jetzt (aufwendiger, M6-Thema) |
| Eigene `ThirdPersonCamera` statt Cinemachine | Wenige Zeilen, voll kontrollierbar (Kick, FOV-Puls, Zuschauer-Orbit), ohne ungetestete Paket-API; Cinemachine bleibt möglich, die Kamera ist reine Darstellung | Cinemachine 3 sofort |
| Kampf-Figuren aus Pipeline-Teilen mit prozeduralen Armen (Zwei-Knochen-IK) an Waffe und Schild; Gliedmaßen als Pipeline-Segmente | Ein Material, Palette, Validator gelten auch für prozedurale Teile; Waffenbahn kommt aus der Sim | Debug-Materialien für Arme |
| Angriffsrichtung aus der Mausbewegung der letzten 120 ms vor dem Klick, ohne Bewegung „Auto" (oben) | CLAUDE.md 3.5: Richtung aus Maus-Delta, Auto-Richtung für Einsteiger | Feste Tasten pro Richtung |
| Singleplayer-Eingabeverzögerung 1 Tick (`DuelScenarioDefinition.inputDelayTicks`) | Game-Feel-Spec: Windup sichtbar ≤ 50 ms; Multiplayer nutzt 6–9 Ticks | 6 Ticks auch im Singleplayer |

## 2026-10-18 — Einfache Kampfsteuerung als Standard

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Standard-Steuerung „Einfach": tippen = leichter Angriff (Kette rechts/links, mit Vorwärts: Stich), halten = schwerer Hieb von oben; Richtungs-Steuerung per Maus bleibt per Tab wählbar | Tarik will Kampf ohne Richtungs-Komplexität; der Kopf bleibt frei fürs Kommandieren. Die Sim-Grammatik (vier Richtungen als Geometrie, ein richtungsloser Block) bleibt unverändert, nur die Eingabe-Übersetzung ändert sich. Fun-Gate entscheidet endgültig | Nur Richtungen per Maus (CLAUDE.md alt), Light/Heavy auch in der Sim (verliert Kopf/Mehrfachtreffer-Geometrie) |
| „Schwer" startet sofort als leichter Hieb und wird per Morph umgewandelt, sobald die Taste gehalten wird | Kein Warten auf Tippen-oder-Halten, Windup bleibt innerhalb eines Ticks sichtbar; Test sichert, dass auch bei 30 FPS das Morph-Fenster reicht | Angriff erst nach Loslassen auslösen (100–200 ms Verzögerung) |

## 2026-10-18 — M1 Schritt 3: Sprint, Schritt, Schildstoß, Angriffs-Tokens

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Ausweichschritt verschiebt die Position direkt (2 m in 300 ms), Steering steht in der Zeit still | Exakt, deterministisch, unabhängig von Beschleunigungs-Grenzen; nicht unterbrechbar, aber Eingaben werden gepuffert | Schritt als Geschwindigkeits-Impuls (Weg hängt vom Steering ab) |
| Körpergeschwindigkeit geht in die Trefferenergie ein (Klingenweg + Körperweg pro Tick) | CLAUDE.md: „Sprint-Angriff mit Energie-Bonus", Ansturm in die Linie wird mechanisch belohnt, ohne Sonderregel. Stich aus dem Sprint ≈ 614 J statt 289 J | Fester Sprint-Multiplikator |
| Schildstoß: 250 ms ausholen, 100 ms aktiv, 250 ms erholen; bricht jeden erhobenen Block (ohne Schaden), sonst 6 Schaden und 800 ms Taumeln | „Schildstoß + schneller Nachstich": Test sichert, dass ein sofortiger Stich trifft, bevor der Gegner wieder blocken kann. Antwort für Schildwall-Patt und Bots auf L1 | Schildstoß mit Energie-Schaden über SwingTrace (unnötig für einen Stoß) |
| Angriffs-Tokens: höchstens 2 Bots holen gleichzeitig gegen dasselbe Ziel aus (Tuning-Wert), der Rest wartet 1,2 m außerhalb der Reichweite | CLAUDE.md 3.5 D (1vX); Spieler soll sich gegen Überzahl mächtig, aber bedroht fühlen. Reines Bot-Verhalten, kein Sonderpfad | Alle Bots greifen an (Spieler chancenlos), Bots greifen einzeln an (langweilig) |
| Duell bekommt F10 für 1–3 Gegner | Fun-Gate-Test der Macht-Fantasie und der Tokens | Eigene Szene |

## 2026-10-18 — M1: Zweihänder und Waffenwechsel

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Archetyp ist ein Feld der Waffe (`WeaponData.Archetype`) und bestimmt das Special: Einhand + Schild → Schildstoß, Zweihand → Sprung-Hieb | CLAUDE.md 3.5 Schicht B: ein Special pro Archetyp. Spieler und Bots rufen dasselbe `TrySpecial` | Special als eigene Taste pro Waffe |
| Sprung-Hieb ist ein normaler Angriff aus einem fünften Tabellen-Slot (`AttackDirection.Special`) mit Ausfallschritt (Lunge, 1,4 m). Der Ausfallschritt bremst vor dem Gegner und geht über die Körperbewegung in die Energie ein | Energie-Bonus aus Geometrie statt Flag; Phasen, Block, Perfect Block, Finte gelten unverändert. Root-Motion nur für Angriffs-Lunges (Game-Feel-Spec) | Sprung als eigener Zustand mit Schadens-Multiplikator |
| Zweihand-Werte per Sweep gegen ein stehendes Ziel: Griff zur Körpermitte, Klinge 1,05 m, Oben-Hieb und Stich 15 cm zurück, Seitenhiebe 30 % langsamer. Ergebnis Mittel: Oben 69 (Einhand 63), Seite 47 (33), Stich 39 (22), Sprung aus 2,6 m 92; Block kostet den Gegner 2,5× Stamina | Ohne Anpassung trifft die längere Klinge zu früh im Schwung (Oben-Hieb schwächer als Einhand) oder zu hart (Seite 80). Sprung tötet nicht mit einem Treffer | Mehr Masse statt Bahn-Korrektur |
| Waffenwechsel: Loadout mit 4 Slots in `CombatData` (jetzt 144 Bytes), Zustand `Switching` 450 ms, Eingabe wird gepuffert. Schild wandert auf den Rücken, wenn eine andere Waffe gezogen ist | CLAUDE.md: 3–4 Slots, Wechsel kostet eine Recovery; Loadout ist Sim-Zustand und gehasht | Loadout nur in der Darstellung |
| Papier-Test Bogen/Lanze: Grammatik trägt beide mit zwei allgemeinen Erweiterungen (Halten in einer Phase, Resolver-Art pro Angriff), siehe `docs/combat/papier-test-bogen-lanze.md` | Pflicht aus CLAUDE.md 3.5; Umsetzung mit dem Bogen in M3 | Bogen schon in M1 |

## 2026-10-18 — M1: Kampfgefühl vor dem Fun-Gate

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Rückstoß (Stagger-Schritt) in der Sim: `CombatData.Knockback` verschiebt den Körper direkt und klingt pro Tick ab (0,85). Treffer 0,006 m/s pro J (Oben-Hieb ≈ 30 cm), Block halb so viel, Perfect Block wirft den Angreifer zurück | Game-Feel-Spec „Impuls auf Ziel aus Trefferrichtung"; Position ändert das Gefecht, muss also deterministisch und gehasht sein | Nur optischer Rückstoß (Figur und Sim laufen auseinander), Impuls über Velocity (Steering frisst ihn in ~60 ms) |
| Schildstoß schiebt nur 1,5 m/s (≈ 17 cm) | Bei 3,5 m/s erreicht der „schnelle Nachstich" (CLAUDE.md Schicht B) das Ziel nicht mehr; Test sichert die Kombination | Größerer Schub |
| Platzhalter-Sounds werden zur Laufzeit synthetisiert (`ProceduralSfx`): Windup-Ton pro Richtung, Whoosh beim Release, Material-Treffer (Fleisch/Holz/Stahl), Ächzen | Keine Assets vor M4, trotzdem hörbares Feedback für das Fun-Gate; drei Layer pro Treffer wie in der Spec. Echte Aufnahmen in M5 | Stille bis M5, gekaufte Sound-Packs |
| Kein Kampf-HUD: Lebensbalken standardmäßig aus (F9 Debug), Ausdauer als pulsierende Vignette, Treffer als roter Rand, Leben als Haltung (vorgebeugt) | Game-Feel-Spec „Kein Kampf-HUD" | Balken als Standard |
| Entscheidende Kills (Held oder letzter Gegner): 0,1 s Sim-Zeit in Zeitlupe 0,25×, über weniger Ticks pro Sekunde | Spec: Slow-Mo bei entscheidenden Kills; `DT` bleibt fest | Unity-`timeScale` |
| Reaktionszeit als Test: Eingabe bis sichtbare Waffenbewegung 2 Ticks = 33 ms bei Eingabeverzögerung 1 | Spec-Kriterium „≤ 50 ms" ist Teil des Fun-Gates; jetzt automatisch geprüft | nur Frame-Capture von Hand |

## 2026-10-18 — Spielrahmen: Hauptmenü, Pausemenü, Steuerung, Tutorial

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Hauptmenü als erste Szene (`MainMenu`), Esc im Spiel ist das Pausemenü (Fortsetzen, Neu starten, Steuerung, Optionen, Hauptmenü), F1 bleibt Status und Admin | Tarik: „es ist überhaupt nicht wie ein Game aufgebaut"; ein Spiel braucht einen Rahmen, in dem Modi, Steuerung und Optionen auffindbar sind. Admin-tauglich statt Endkunden-Polish reicht bis M5 | Alles weiter im F1-Overlay |
| Menüs in IMGUI mit gemeinsamem Stil (`MenuStyle`), keine UI-Toolkit-/Canvas-Assets | Keine Assets vor M4, wenige Dateien, zur Laufzeit skaliert; das echte Front-End kommt in M5 (Polish) | uGUI/UI Toolkit jetzt |
| Tasten im Menü änderbar (Input-System-Rebinding), Überschreibungen in PlayerPrefs; Maus-Achsen (Umschauen, Zoom) nicht änderbar | Jede Eingabe ist eine Action, also kann sie auch umgelegt werden; controls.de.json beschreibt weiterhin jede Action | Feste Belegung |
| Tutorial = geführtes Duell gegen L2: 13 Schritte, jeder schließt über ein Sim-Ereignis ab (Block-, Finte-, Schritt-Events usw.), stirbt der Held, startet das Duell beim selben Schritt neu | Lernen durch Tun gegen echten Gegner; keine Sonderlogik in der Sim, nur Beobachtung | Textseite mit Tastenliste |
| Duell-Setup (Preset, Stufe, Gegnerzahl, Waffen) im Menü, übergeben per `DuelLaunch` an die Szene; F6–F11 im Duell bleiben | Ein Ort für Start-Einstellungen, der Direktstart der Szene im Editor funktioniert weiter | Einstellungen nur in F-Tasten |

## 2026-10-18 — Sprung

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Sprung (C / X) als eigener Zustand: 450 ms in der Luft, 120 ms Landung, kostet Ausdauer; in der Luft nichts möglich, laufen geht weiter, tiefe Hiebe treffen die Beine nicht | Letztes Verb des Vokabulars (CLAUDE.md 3.5); konkreter Nutzen: Ausweichen über tiefe Seitenhiebe, später Gelände (M4). Sprung-Hieb bleibt beim Zweihänder | Sprung mit Angriff in der Luft (zu viel Vokabular), Sprung nur als Darstellung |
| Damit ist der Bau-Umfang von M1 vollständig; offen ist nur das Fun-Gate | – | – |

## 2026-10-18 — Nach dem Kampf: Ergebnisbildschirm statt F-Tasten; Leben als roter Rand

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Nach jedem Duell ein Ergebnisbildschirm: Sieg/Besiegt, die laufenden Einstellungen, Statistik (Dauer, Treffer, Schaden, Blocks, Perfect Blocks, Specials, Finten), dann drei Buttons: Nochmal, Anders einstellen (dieselbe Setup-Seite wie im Hauptmenü), Hauptmenü. Beim Start 4 s lang eine Zeile, was läuft | Tarik: „ich muss genau wissen, was wie abgeht"; die alte Zeile „F5 neu · F6 …" erklärte nichts. F-Tasten bleiben als Abkürzung, stehen in F1 | Automatischer Neustart, Tastenkürzel-Zeile |
| Leben als roter Bildrand, der ab 70 % Leben wächst und bei 0 fast den Rand füllt (CoD-Stil), plus kurzes Aufflackern bei Treffern; Ausdauer bleibt der dunkle Puls; Haltung zeigt Leben zusätzlich | Ohne HUD muss Leben trotzdem spürbar sein; roter Rand ist in Shootern gelernt und lenkt nicht ab | Lebensbalken als Standard |

## 2026-10-19 — M1 abgeschlossen, M2: Formationen in der Sim

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Fun-Gate M1 bestanden (Tarik: „es macht Spaß"). Perfect Block bleibt vorerst wie er ist; der Plan (Bots finten zurück, Fenster 150 ms) steht in `docs/STATUS.md` | Tarik will erst festhalten, nicht anpassen | Vor M2 nachbessern |
| **Slot-Zuordnung exakt mit der Ungarischen Methode (O(n³)) statt Sinkhorn** (Abweichung von CLAUDE.md 3.6, dort angepasst) | Sinkhorn braucht `exp()` und eine Rundung auf eine Permutation; `exp` ist zwischen Mono, IL2CPP und Burst nicht bit-gleich garantiert und die Rundung ist eine Näherung. Die Ungarische Methode nutzt nur +, −, Vergleiche: exakt, deterministisch, Gleichstände nach Index. Läuft nur beim Umformen (Befehl, Lücken nach Verlusten höchstens 1×/s), als Burst-Job. Gemessen ohne Burst: 150 Soldaten ≈ 14 ms, 40 Soldaten ≈ 0,3 ms | Sinkhorn (Näherung, transzendente Mathematik), Auktions-Algorithmus (ε-abhängig), gieriges Zuordnen (kreuzende Wege) |
| Formation = Anker (Mitte des vordersten Glieds, beim Kreis die Mitte) + Ausrichtung + Typ + Breite. Slots in lokalen Koordinaten; jeder Agent steuert seinen Slot, nie das Formationsziel | CLAUDE.md 3.6; Spieler zieht die Frontlinie, also ist die Front der Anker | Anker im Schwerpunkt |
| Bewegungsbefehl dreht sofort auf der Stelle (Umformen um die Soldaten herum), dann marschiert der Anker mit 1,2 m/s zum Ziel und bremst, wenn die Soldaten im Schnitt mehr als 0,6 m vom Slot weg sind (nie unter 20 %) | Sichtbare, sofortige Reaktion (Fun-Gate M2); kehrt eine Linie um, wird das hintere Glied zur Front statt dass alle durchlaufen | Drehen während des Marsches (Flügel laufen weit), Anker teleportiert (Linie zerfällt) |
| Soldaten zielen beim Marsch etwas vor ihren Slot (Geschwindigkeit × SlowRadius / Tempo) | Das Ankommen-Steering hängt einem bewegten Ziel sonst ~1 m hinterher; so bleibt die Linie im Marsch eine Linie (Test: < 0,8 m Abweichung) | Eigener Geschwindigkeits-Abgleich im Steering (mehr Zustand im Agenten) |
| Spieler-Befehle laufen als `InputCommandType.FormationOrder` durch die `InputQueue`; die Commander-AI ruft dieselbe `FormationOrder` direkt in der Sim auf. Beide nur für eigene Formationen (`Owner`), ungültige Befehle werden ignoriert | CLAUDE.md 3.1: zwei Clients derselben API, kein Spieler-Sonderpfad; Lockstep-robust | Eigene Befehlstypen pro Client |
| `CombatData.OrderFacing` (jetzt 160 Bytes): Blickrichtung am Slot für Bots ohne Gegner | Formationen müssen aus 40 m lesbar sein (Säule 3); eine Linie, die in Laufrichtung guckt, liest sich nicht als Linie | Blickrichtung nur in der Darstellung |
| `tools/simcheck`: Sim und Unity-freie Tests laufen mit .NET 8 gegen Stubs für Collections/Jobs/Burst | Cloud-Sessions ohne Unity können Sim-Code kompilieren und testen, statt blind zu committen. Ersetzt `.\sb.ps1 test` nicht | Ohne Tests committen |

## 2026-10-19 — M2: Formationen in der Sandbox sichtbar und befehligbar

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Ein Klick auf einen Soldaten wählt seine ganze Formation | Befehle gehen an Formationen, nicht an Soldaten (CLAUDE.md 3.1); Einzelsoldaten in einer Formation zu wählen hätte keine Wirkung | Soldaten einzeln, Formation per Doppelklick |
| Rechtsklick = hinmarschieren mit Blick in Laufrichtung, Breite bleibt. Rechts ziehen = Frontlinie: Mitte, Breite und Ausrichtung aus der Linie; Blick weg von dort, wo die Truppe steht (eine Linie durch die Truppe behält die Front) | Wie Bannerlord, also bekannt; ein Zug legt alles Räumliche fest, ohne Zusatztaste für die Richtung | Richtung aus der Ziehrichtung (links→rechts), separate Dreh-Taste |
| Mehrere Formationen teilen eine Linie nebeneinander, 2 m Abstand, Anteil nach ihrer jetzigen Frontbreite, Reihenfolge nach jetziger Lage (keine kreuzenden Wege). Kreis: Ring hinter der Linie | Ein Befehl für die ganze Armee ist der Normalfall in der Kommandoansicht (Task 7) | Alle Formationen auf denselben Punkt, feste Reihenfolge nach Id |
| `FormationLinePlanner` liegt in `Sim/Orders`, nicht in Presentation | Pur und deterministisch; Kommandoansicht und Commander-AI planen später dieselben Linien. Ohne Unity testbar | Planung im MonoBehaviour |
| Die Sandbox spawnt Gruppen als Formationen (Typ pro Gruppe); der Stresstest 250 gegen 250 bleibt lose | Umformen von 250 Soldaten kostet einen Tick lang geschätzt ~60 ms (O(n³), 150 Soldaten gemessen 14 ms ohne Burst); der Stresstest misst Steering und Kampf | Stresstest als Formationen |

## 2026-10-19 — M2: Wege für Formationen

| Entscheidung | Grund | Verworfen |
|---|---|---|
| A* pro Formation auf dem `WorldGrid` (8 Nachbarn, keine Eckenschnitte), ganzzahlige Kosten (gerade 10, diagonal 14, mal Zellkosten), Gleichstände nach h und Zellindex; danach zu wenigen Wegpunkten geglättet (Sichtlinie, nie durch teurere Zellen als der Rasterweg) | Deterministisch ohne Float-Vergleiche in der Suche; wenige Wegpunkte = ruhiger Marsch. Läuft nur bei Befehl, Kartenänderung oder langem Weg (max. 32 Wegpunkte, dann Rest neu planen) | Flowfields (lohnen erst bei vielen Gruppen mit gleichem Ziel, M4), NavMesh (nicht deterministisch, nicht zerstörbar) |
| Breite Fronten meiden enge Lücken weich: Abstandsfeld zu Wänden und Kartenrand, Aufschlag 4 pro fehlender Zelle bis halbe Frontbreite (max. 6) | Eine 20 m breite Linie soll den Umweg durchs breite Tor nehmen, kann sich aber notfalls durch eine Lücke quetschen (die Soldaten weichen lokal aus) | Harte Sperre (Formation bleibt stehen), Formation automatisch schmaler machen (eigenes Behavior, später) |
| Ab 8 m Weg blickt die Formation in Marschrichtung und dreht am Ziel in die befohlene Richtung; kürzere Wege behalten die befohlene Richtung. Schwenken: Flügel höchstens 1 m/s; über 60° wird umgeformt statt geschwenkt (vor dem Marsch um die Soldaten herum, am Ziel um den Anker) | Test zeigte: Eine Linie, die seitlich um ein Mauerende marschiert, schiebt ihre hinteren Glieder hinter die Mauer, sie bleiben hängen. In Marschrichtung folgt der Körper dem Anker. Ersetzt „dreht sofort auf der Stelle, dann Marsch“ vom selben Tag | Immer befohlene Richtung (hängt an Ecken), Formation als Kolonne marschieren lassen (eigener Typ, später) |
| Drehung im verwalteten Sim-Code ohne Trigonometrie: Schwelle als Kosinus (`ReformTurnCos`, im ScriptableObject in Grad), Schritt per Taylor-Reihe | `sin`/`acos` sind in Mono, IL2CPP und Burst nicht garantiert bit-gleich; der Formations-Tick läuft außerhalb von Burst | Formations-Tick komplett als Burst-Job (größerer Umbau, später bei Bedarf) |
| Soldaten: Wände drücken wie Nachbarn (`WallWeight` 2), gesperrte Zellen werden nie betreten (an der Wand entlanggleiten) | CLAUDE.md 3.6: Agenten nur lokales Steering; ohne harte Grenze drücken Massen Soldaten durch Mauern | Pfad pro Agent (zu teuer, widerspricht 3.6) |
| Hindernisse in der Sandbox nur im Overlay sichtbar | Keine Assets vor M4 | Kisten-Meshes jetzt |

## 2026-10-20 — M2: Verhalten und Kampf in Formation

Grundlage der Werte: je 10 Seeds, 20 gegen 20, Bots L3, Schwert und Schild, ohne Unity mit `tools/simcheck` gemessen.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Verhalten (Halten, Vorrücken, Angriff, Folgen, Rückzug) ist ein Feld der Formation und wird über `FormationOrder` mit dem Flag `Behavior` gesetzt. Ein reiner Bewegungsbefehl heißt „hingehen und halten“; Halten ohne Ziel bleibt stehen | CLAUDE.md 3.1 und 3.6: Spieler, Radialmenü, Kommandoansicht und Commander-AI nutzen dieselbe API; Standing Orders und Doktrinen wählen später nur unter diesen Verhalten | Eigene Befehlstypen pro Verhalten |
| Vorrücken und Angriff gehen auf die feindliche Formation mit dem nächsten Anker, zielen auf ihre Mitte und halten mit der eigenen Front 1,5 m (Angriff: 0 m) vor ihrem nächsten Soldaten. Das Ziel wird alle 0,5 s geprüft, neu marschiert wird erst, wenn es sich um mehr als 2 m verschoben hat | Ein ausweichender Gegner wird verfolgt, ohne dass jeder Schritt einen neuen Weg kostet. Mitte statt nächster Soldat: Die Front zielt nicht auf einen Ausreißer an der Flanke | Ziel-Formation im Befehl (kommt mit der Kommandoansicht, wenn der Spieler auf eine Formation zeigen kann) |
| Folgen bleibt 4 m hinter dem Soldaten, auf der Linie von der Formation zu ihm; kommt er zurück, bleibt die Formation stehen | Der Held schaut ständig herum; hinter seiner Blickrichtung zu stehen, würde die Formation bei jedem Umschauen um ihn herum laufen lassen | Platz hinter der Blickrichtung des Helden |
| Rückzug: 20 m weg von der nächsten feindlichen Formation (oder zu einem befohlenen Punkt), ohne zu kämpfen; danach Halten mit Front zum Feind (über 60° Drehung wird umgeformt, das hintere Glied wird Front) | Einfachster lesbarer Rückzug; die Soldaten drehen dem Feind beim Marsch den Rücken zu, das ist der Preis | Kämpfender Rückzug rückwärts mit Front zum Feind (später, wenn Moral und Rout stehen) |
| Kampf in Formation als Schicht D (`Combat/Context/FormationCombatRules`): Leine 2 m um den Slot, Gegner nur im 120°-Bogen vor der Front, ringsum nur, wer näher als 1,6 m kommt; kein Sprint. Angriff löst die Leine, Rückzug kämpft nicht | CLAUDE.md 3.5 D: „Bots halten Slot, greifen frontal an“. Gemessen mit Leine 1 m: Halten gegen Angriff 2:8, das zweite Glied stand untätig. Mit 2 m rückt es in Lücken nach: Halten gegen Vorrücken 5:5, Halten gegen Angriff 5:5, Angriff gegen Vorrücken 5:5 | Leine 1 m, Frontbogen 360° (2:8, ändert nichts), nur größere Selbstverteidigung 2,5 m (4:6) |
| Schildwall „blockt bevorzugt“ heißt: Auf einen gesehenen Angriff antwortet er mit dem Schild statt mit einem Ausweichschritt aus der Wand (der Anteil fürs Ausweichen geht zum Block). Reaktionszeit und Fehlerquote bleiben die der Bot-Stufe | Ausprobiert und gemessen: dauerhaft erhobener Schild und seltener zuschlagen verliert 0:10. Der Schildstoß bricht jeden erhobenen Block (Absicht der Grammatik), unter erhobenem Schild lädt die Ausdauer nicht nach, und wer selten schlägt, lässt den Gegner ungestört angreifen. Was den Schildwall stark macht, ist die Dichte: gleich breit wie die Linie gewinnt er 9:1, schmaler (8 m gegen 11 m) 5:5; eine schmale Linie verliert 1:9 | Schild immer oben, Angriffschance ×0,4 (0:10); Schild oben bei voller Angriffschance (0:10); ein Bonus für Schildwälle in der Grammatik (z. B. Schildstoß bricht verschränkte Schilde nicht) bleibt Tariks Entscheidung |
| `tools/simcheck` kompiliert jetzt auch die Kampf- und Formations-Definitionen aus `Data/` gegen schlanke UnityEngine-Stubs und lässt alle Kampf-Tests laufen (137 statt 63 Tests) | Diese Iteration ändert den Bot-Kopf, den auch das Duell nutzt; ohne die Duell-Tests wäre jede Änderung blind | Kampf-Tests nur in Unity |

## 2026-10-20 — M2: Moral und Rout

Grundlage der Werte: 20 gegen 20, Linien, Bots L3, je 6–10 Seeds, ohne Unity mit `tools/simcheck` gemessen.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Ein Panik-Feld **pro Team** auf dem `WorldGrid`, explizite Diffusion alle 50 ms (`P += dt·(D·ΔP − k·P)`, 4 Nachbarn, am Kartenrand geht nichts verloren, Wände halten nichts auf), als Burst-Job in Agenten-Reihenfolge | CLAUDE.md 3.4. Ein Feld für beide Seiten kann nicht trennen, wessen Tote wen erschrecken. Explizit ist billig und deterministisch; die Stabilitätsgrenze (4·D·dt + k·dt ≤ 1) prüft `MoraleSettings.Validate` | Implizite Diffusion (Gleichungssystem), Moral ohne Feld nur aus Nachbarzählung (keine Ausbreitung, keine Ansteckung) |
| **Das Feld hat ein Vorzeichen:** unter null ist Zuversicht. Kills und brechende Feinde setzen Zuversicht frei, sie breitet sich aus wie Panik und hebt die Moral | Gemessen: Ein auf 0 begrenztes Feld macht „Kills ermutigen“ wirkungslos (der Mut löscht nur Panik in einer Zelle und verschwindet). Folge: Sieger brachen fast so oft wie Verlierer (Sieger mit 7 Kämpfern und 7 Fliehenden). Mit Vorzeichen (und den Endwerten unten) brechen die Verlierer nach 35–45 % Toten, die Sieger behalten 13–16 Kämpfer und kaum Fliehende, Ende nach ~30 s statt ~45 s fast bis zum letzten Mann | Feld ab 0 begrenzt |
| Quellen: eigener Toter 1, Treffer von Seite oder hinten 0,3, Fliehender 0,3 pro Sekunde (Ansteckung). Senken: Abklingen 0,3/s, Feind stirbt oder bricht −0,5, Held (Spieler-Agent) nimmt im Umkreis von 8 m 50 % der Panik pro Sekunde weg | Rout soll verdient sein: Verluste, Flanke, fliehende Kameraden; Erfolg und ein Anführer halten. „Captains/Banner = Senken“ aus CLAUDE.md 3.4: bis es Banner gibt, ist der Held der Anführer | Captains und Banner jetzt (M3-Experiment, `IDEAS.md`) |
| Moral pro Soldat 1 → Rout bei 0,25: Verlust 5 pro Panik-Einheit und Sekunde, in einer haltenden, vorrückenden oder folgenden Formation ×0,7; Erholung 0,02/s, wo es ruhig ist. Nur Bots brechen, nie der Held | Gemessen mit Formations-Dämpfung 0,5: Halten gegen Angriff 9:1 (Angriff fast nutzlos), 0,7: 8:2, 0,8: 7:3. 0,7 behält die Lehre „nicht frontal in eine geschlossene Linie stürmen“, ohne den Angriff wertlos zu machen. Vorrücken gegen Vorrücken bleibt ausgeglichen | Dämpfung 0,5 |
| Rout: Der Soldat verlässt seine Formation (sie schließt die Lücke), kämpft nicht mehr, wirft keinen Block, sprintet vom Feind weg (Fluchtrichtung von den Feinden in 8 m weg, geglättet) und verlässt am Kartenrand das Feld (`Fled` + `Dead`, die Sim ignoriert ihn, die Darstellung blendet ihn aus). Kein Sammeln in v1 | Lesbar und endgültig: Eine Schlacht endet durch Bruch, nicht durch Auslöschung (Säule 3, Fun-Gate M2). Sammeln braucht Banner oder Held als Ziel (`IDEAS.md`) | Fliehende kehren zurück, wenn die Moral steigt; Fliehende werden sofort entfernt (Flucht unsichtbar) |
| Flankentreffer erschrecken nur wenig zusätzlich: 10 Mann von der Flanke statt von vorn kosten A mehr (B behält 14,7 statt 13,0 Kämpfer), das kommt aber vor allem aus dem Kampf selbst | Wert bleibt 0,3; mehr wäre eine Tuning-Entscheidung | – |
| Moral ist pro Szenario an- oder abschaltbar (`MoraleDefinition` oder leer); das Duell bleibt ohne | Das Duell hat sein Fun-Gate bestanden; ein Bot, der im 1 gegen 3 nach dem ersten Toten flieht, ändert es | Moral überall |

## 2026-10-21 — M2: Radialmenü und Kompanie-Szene

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Grundgerüst der Szene `M2_Company` (Held, zwei eigene Formationen, zwei vorrückende Bot-Formationen, Moral) aus Task 8 vor Task 6 gezogen (Tarik) | Das Radialmenü braucht einen Helden mit eigener Truppe; weder Duell noch Sandbox haben beides. Ergebnisbildschirm und Fun-Gate bleiben in Task 8 | Radialmenü zuerst nur in der Sandbox (Befehlslogik testbar, Gefühl im Kampf nicht) |
| Radialmenü: Taste halten (F, Gamepad RB), mit Maus oder rechtem Stick zeigen, loslassen gibt den Befehl, Mitte bricht ab. Ziel per Mausrad (LB): alle eigenen Formationen oder eine. Acht Felder im Uhrzeigersinn ab oben: Vorrücken, Angriff, Folgen, Keil, Rückzug, Schildwall, Halten, Linie | Vorwärts oben, zurück unten, Formen links: merkbar und ohne Untermenü. F und RB liegen frei neben WASD bzw. am Zeigefinger; keine Kampftaste wird doppelt belegt. Kreis und lockere Ordnung (Kavallerie, Fernkampf) braucht es erst ab M3/M4, sie kommen über die Kommandoansicht | Untermenü „Form“ (zweiter Schritt kostet Zeit im Kampf), Zifferntasten (1–4 sind Waffen), Ziel über eigene Tasten |
| Solange das Menü offen ist, läuft der Held weiter, schaut aber nicht herum und beginnt keine Aktion; ein gehaltener Block bleibt, Loslassen wirkt | CLAUDE.md Säule 2: „Hinschauen kostet Aufmerksamkeit und macht den Helden verwundbar“; die Maus wird fürs Zeigen gebraucht | Zeitlupe beim Radialmenü (nur für die Kommandoansicht vorgesehen) |
| Feindliche Formationen der Kompanie bekommen ein Startverhalten (Vorrücken) als Szenario-Einstellung, nicht als Befehl | Ohne Commander-AI (M3) braucht der Feind einen Grund zu kommen; ein Startverhalten ist Szenario-Aufbau wie das Spawnen, auf allen Peers gleich | Feind steht still (kein Kampf ohne Spieler-Initiative) |
| Der Held läuft wie im Duell mit 1,6-fachem Tempo der Liniensoldaten | Er ist ein Elite-Soldat (Säule 1) und muss seine Linie erreichen und verlassen können; das ist eine Held-Eigenschaft, kein AI-Cheat | Held so langsam wie die Linie |

## 2026-10-21 — M2: Kommandoansicht

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Taste halten: **V** (Gamepad **LB**); loslassen kehrt zurück. Die Kamera fährt in 0,5 s auf 35 m Höhe (Mausrad 18–60 m), 58° schräg, Blick in die Richtung der Kampfkamera, 12 m vor den Helden; der Nebel weicht mit der Höhe zurück | CLAUDE.md 3.9 (halten, ~0,5 s, 30–40 m). V liegt neben WASD und ist im Kampf frei; LB ist frei, solange das Radialmenü zu ist. Gleiche Blickrichtung wie vorher, damit oben links immer noch links ist | Umschalten statt Halten (vergessenes Umschalten lässt den Helden stehen), Draufsicht senkrecht (Formationen schwer zu lesen) |
| `HeroUnattended` ist ein Sim-Input; der Held wird so lange vom Bot-Gehirn gesteuert (Stufe 2 aus `FormationDefinition`) und ignoriert Bewegungs- und Kampfbefehle des Spielers. Posten: 1,5 m hinter dem hintersten Glied der nächsten eigenen Formation (beim Kreis die Mitte), dort gebunden wie ein Soldat an seinen Slot (Leine, Frontbogen, Selbstverteidigung) | CLAUDE.md 3.9 („Block halten, in die eigene Linie zurückziehen, L2“), deterministisch auf allen Peers. Gemessen: Der Rückzug ist schneller, als ein verfolgender L3-Gegner zuschlägt; der Verfolger läuft in die eigene Linie | Held bleibt stehen (unbeaufsichtigt mitten im Feind), Held kämpft frei (läuft Gegnern hinterher) |
| Schild oben nur, solange ein Feind in Reichweite ist; auf dem Weg zurück unten | Blocken verlangsamt; mit dem Schild oben holte ihn der Verfolger ein | Schild immer oben (wörtlich „Block halten“) |
| Folgt die nächste Formation dem Helden, hält er, wo er ist | Sonst schieben sich Held und Formation gegenseitig nach hinten | – |
| Rechtsklick ohne Auswahl befiehlt **alle** eigenen Formationen; ein Klick auf eine feindliche Formation befiehlt Vorrücken gegen genau diese (neues Feld `TargetFormation` in `FormationOrder` und `FormationData`; ein Ziel ohne Soldaten fällt auf das nächste zurück; Vorrücken oder Angriff ohne eigenes Ziel behalten es, also „vorrücken gegen X, dann Angriff“ stürmt X; jedes andere Verhalten und ein Marschbefehl löschen es) | Schnell im Gefecht; das Ziel war in STATUS als „kommt mit der Kommandoansicht“ zugesagt. Das Radialmenü befiehlt in der Kommandoansicht die Auswahl | Nichts tun ohne Auswahl; Angriff (Charge) statt Vorrücken auf Rechtsklick (löst die Formation sofort auf) |
| Zeitlupe in der Kommandoansicht als Option, Standard 0,3× (1,0× = aus); nur weniger Ticks pro Sekunde, DT bleibt | CLAUDE.md 3.9 (Einsteigerhilfe, Singleplayer). Multiplayer (M6) ignoriert sie | Pause in der Kommandoansicht (widerspricht „Zeit läuft weiter“) |
| `GroundLines` (vorher `DebugLines`) liegt jetzt in `Presentation/` | Die Kommandoansicht ist Spiel, nicht Debug; das Debug-Assembly darf die Darstellung nutzen, nicht umgekehrt | Linien doppelt |

## 2026-10-21 — Testmodus: jeder Inhalt erreichbar und erklärt

Anlass (Tarik): seit Task 4 nichts angespielt; am Ende soll sich alles vollständig testen lassen, ohne zu wissen, wo was steckt.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Ein **Szenen-Katalog** (`Resources/SceneCatalog.asset`) ist die eine Liste alles Spielbaren: Szene, Startart, optional Ersatz-Szenario, Ziel, Eingabe-Maps, Prüfliste. Hauptmenü → Testmodus listet ihn und startet jeden Eintrag; jede Szene zeigt ihren Eintrag (Ziel beim Start, F2 volle Hilfe), Esc → Prüfliste hakt ab | Eine Quelle für Menü, Hilfe und Prüfliste; die Steuerung kommt live aus den Action-Maps (umgelegte Tasten stimmen). `SceneCatalogTests` schlägt fehl, wenn eine Szene oder ein Szenario keinen Eintrag hat, so bleibt nichts unerreichbar (vorher: `Stress_250v250` war es) | Eigener Start-Text pro Szene (driftet, fehlt bei Werkzeugen), Prüflisten nur in `docs/STATUS.md` (beim Spielen nicht zur Hand), eigene Test-Szene mit allen Szenen (doppelt zum Menü) |
| Die Prüflisten stehen **im Spiel** statt in `docs/STATUS.md`; STATUS verweist darauf. Abgehakt wird pro Rechner (PlayerPrefs); ein umformulierter Prüfpunkt ist wieder offen | Abhaken beim Spielen, nicht im Editor; Fortschritt pro Tester. Gemeinsamer Fortschritt bräuchte einen Server | Prüflisten-Datei im Repo, die das Spiel beschreibt (Build schreibt nicht ins Repo) |
| Die committeten Build Settings listen alle Szenen aus `BuildTools.Scenes` (vorher nur Sandbox und `SampleScene`) | Im Editor lädt `SceneManager.LoadScene` nur Szenen aus den Build Settings; Hauptmenü → Kompanie ging im Editor nur nach einem Build aus dem Editor-Menü. Test prüft die Gleichheit | Build Settings nur beim Build setzen (wie bisher) |

## 2026-10-21 — Mac als Entwicklungs- und Spielplattform

Anlass (Tarik): Kann ich auf dem Mac (M4) entwickeln und das Spiel ausführen?

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `sb.ps1` und `tools/` laufen auch auf macOS (PowerShell 7 aus Homebrew; `sb.ps1` hat eine Shebang-Zeile und ist ausführbar). Unterschiede nur in `tools/common.ps1`: Unity-Pfad (Hub-Standardpfad, `UNITY_EXE` überschreibt), Spiegeln mit `rsync` statt `robocopy`; Blender unter `/Applications`. Setup über `tools/setup-mac.ps1` | Ein Einstieg für Tarik bleibt `sb.ps1` (CLAUDE.md 8), egal auf welchem Rechner. Unity 6, Burst, Input System, Blender und Git LFS laufen nativ auf Apple Silicon; der Code nutzt nichts Windows-Eigenes | Zweites Skript-Set für die Shell (`sb.sh`): doppelte Pflege |
| Build für das System, auf dem gebaut wird: PC → `SHIELDBREAK.exe`, Mac → `SHIELDBREAK.app` (`BuildTools.BuildMacBatch`, `StandaloneOSX`) | Burst kompiliert für den Mac mit den Xcode Command Line Tools des Macs; Cross-Builds zwischen den Systemen sparen nichts und brauchen weitere Unity-Module | exe auf dem Mac bauen (läuft dort nicht), Universal-Build-Einstellungen von Hand |
| Die Nicht-Garantie aus CLAUDE.md 1 bleibt: kein Determinismus **zwischen** Mac und Windows. Auf einem Rechner sind Tests, Hash und Replays deterministisch; Singleplayer läuft überall. Ob Mac und PC im Multiplayer (M6) zusammen spielen, entscheidet ein Desync-Test dann | Burst rechnet mit `FloatMode.Deterministic` auf ARM und x64 vermutlich gleich, garantiert ist das ohne Messung nicht. Bis M6 kostet die Frage nichts | Cross-Plattform-Lockstep jetzt zusagen |
| `assets -Check` vergleicht auf 1e-5 gerundete Geometrie. Weicht Blender auf ARM in einer Rundung ab, gilt der Stand des PCs, bis es einmal gemessen ist | Bisher nur auf Windows gebaut | – |

## 2026-11-01 — M2 Task 8: Ende der Kompanie-Schlacht

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Entschieden ist die Schlacht, wenn auf einer Seite niemand mehr kämpft (alle tot, fliehend oder geflohen) **oder der Held fällt** (`Sim/Morale/BattleOutcome`, aus der Sicht des Spieler-Teams). Steht nur noch der Held, geht sie weiter | Dieselbe Regel wie im Multiplayer (CLAUDE.md 3.8: Rout oder Kommandant tot), also nur einmal zu bauen. Eine Funktion über den Agenten-Array ist deterministisch und auf jedem Client gleich | Weiterkämpfen nach dem Tod des Helden als Zuschauer (entwertet den Helden; als Idee möglich, falls Tarik es will) |
| Der Ergebnisbildschirm kommt 2,5 Sim-Sekunden nach der Entscheidung (`CompanyDirector.resultDelaySeconds`), damit man die Flucht noch sieht. Inhalt: Sieg, Gefallen oder Niederlage, Verluste beider Seiten (stehen, gefallen, geflohen), Helden-Statistik, Zeit in der Kommandoansicht, Buttons Nochmal und Hauptmenü | Wie im Duell: Ergebnis statt F-Tasten. Die Zeit in der Kommandoansicht hilft beim Fun-Gate (riskant, aber nicht lähmend) | Sofort einblenden (die Flucht ist das Verdiente am Sieg) |
| Ein gemeinsamer Schalter `Presentation/Menu/ResultScreen.IsOpen` für Duell und Kompanie ersetzt `DuelDirector.ResultOpen` | Eingabe, Radialmenü, Kommandoansicht und Szenen-Hilfe ruhen bei jedem Ergebnisbildschirm, nicht nur beim Duell | Zweiter Schalter pro Szene |

## 2026-11-01 — Leichte Meta: Kriegszug

Anlass (Tarik, nach dem Anspielen): „in die Richtung von Mount & Blade II Bannerlord“, mit Einführung und Story, Kaufen und Verkaufen in Städten, Rüstung, Pferden und Schlachten gegen Banditen und andere Gegner.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Die offene Frage „Progression zwischen Schlachten“ ist entschieden: **leichte Meta, genannt Kriegszug**. Er ist eine Folge von Schlachten (Banditen, Rivalen, …) mit kurzer Story-Einführung (Text und Bilder, keine Zwischensequenzen). Zwischen den Schlachten gibt es einen Stadt-Bildschirm: Beute verkaufen, Rüstung, Waffen, Pferde und Rekruten kaufen. Gold ist die einzige Ressource | Liefert die Bannerlord-Fantasie (der eigene Trupp wächst, Ausrüstung zählt) ohne deren Kosten. Shieldwall stirbt nach ~5 h an fehlender Langzeitmotivation | Volle Kampagne mit Weltkarte, Lords, Diplomatie und Wirtschaftssimulation: würde das Budget allein verbrauchen. Die Gründungsentscheidung „keine Kampagne“ gilt dafür weiter |
| Keine Weltkarte: Der Kriegszug ist eine Abfolge von Stationen (Stadt → Auftrag wählen, 2–3 zur Auswahl → Schlacht → Stadt). Kein Reisen in Echtzeit, keine Fraktions-AI | Jede Minute außerhalb der Schlacht soll auf Säule 1 oder 2 einzahlen | Weltkarte light (Knotengraph mit Reisezeit): steht in `IDEAS.md` |
| Die Meta lebt außerhalb der Sim in `Scripts/Meta/` (Spielstand, Städte, Inventar, Aufträge) und speichert als JSON. Eine Schlacht startet aus einem Szenario (Daten) plus Seed; die Sim bekommt nur Aufstellung, Ausrüstung und Werte | Determinismus, Lockstep und Tests bleiben unberührt; der Multiplayer bleibt Skirmish (Säule 5) | Meta-Zustand in `SimWorld` |
| Rüstung als Kaufgegenstand: Die Zonen-Schwellwerte (Beine, Torso, Kopf) liegen heute global in `CombatTuningDefinition` und werden dafür pro Soldat gesetzt (Rüstungs-Definition als ScriptableObject). Das Energiemodell bleibt gleich | Rüstung als Schwellwert ist seit den Gründungsentscheidungen Teil des Schadensmodells; nur der Ort der Zahl ändert sich | Rüstung als prozentuale Reduktion |
| Pferde kommen wie geplant mit der Reiterei in M4; der Kriegszug verkauft sie nur | Der Umfang der Reiterei bleibt unverändert | Pferde vorziehen |
| Neuer Meilenstein **M4b Kriegszug** (~80 h) nach M4 und vor M5 Polish; das Budget steigt von ~650 h auf ~730 h. M2 und M3 bleiben unverändert | M4 liefert Pferde, Maps und Modelle, die Läden brauchen etwas zum Verkaufen. Ein 20-gegen-20 ohne Commander-AI trägt noch keinen Kriegszug; die Schlacht muss zuerst tragen | Kriegszug direkt nach M2; M-Nummern neu zählen (zu viele Verweise auf M5 und M6) |
| Vor der Story muss das Setting entschieden sein (historisch oder Low-Fantasy, CLAUDE.md 9) | Story, Gegnertypen und Städte hängen daran | – |

## 2026-11-01 — Fun-Gate M2 bestanden; M3 Task 1: Haltung

Fun-Gate M2 (Tarik): „joa, ist gut … alles in allem cool“. Befund: Formationen, die man hinter den Feind stellt, greifen nicht an; Wunsch, Einheiten auch angreifen lassen zu können.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Ursache: Nach einem Marschbefehl hält eine Formation und kämpft nur innerhalb ihrer Leine (2 m) im Frontbogen. Zieht der Feind weiter, steht sie. Den Angriff auf eine bestimmte Formation gab es schon (Rechtsklick auf den Feind), aber nur als Zeile in der Hinweisleiste | – | – |
| **Haltung** pro Formation (M3 „Standing Orders, 3 Presets“, vorgezogen als Task 1): Stellung halten, **Reagieren** (Standard; rückt gegen eine feindliche Formation vor, deren Soldaten bis 10 m an die eigenen herankommen, und hält wieder, wenn sie gebrochen ist), Aggressiv (ab 30 m, danach weiter zum nächsten). Nur wenn die Formation hält **und angekommen ist**: Ein Marsch hinter den Feind wird unterwegs nicht abgelenkt, am Ziel greift sie seinen Rücken an | Standing Orders sind ein Formation-Behavior (CLAUDE.md 3.6), die Commander-AI nutzt später dieselben. Säule 2: die Armee wird ohne Spieler nicht dumm | Halten reagiert immer (dann gibt es kein echtes Halten mehr); Vorrücken nach jedem Marsch (zieht Linien aus Stellungen, die der Spieler bewusst gewählt hat) |
| „Halten“ aus dem Radialmenü (und H in der Sandbox) setzt zusätzlich „Stellung halten“ | Wer im Gefecht „Halten“ befiehlt, will stehen bleiben; sonst schickt Reagieren die Linie sofort wieder los | Halten ändert die Haltung nicht (Befehl wirkungslos in Kontakt) |
| Haltung wechseln in der Kommandoansicht mit R (Gamepad Y), für die Auswahl oder alle; die Beschriftung jeder eigenen Formation zeigt sie, ein von selbst begonnener Angriff steht als „(von selbst)“ da. Eine feindliche Formation unter dem Mauszeiger leuchtet auf und zeigt „Rechtsklick: angreifen“ | Befund aus dem Fun-Gate: Der vorhandene Angriffsbefehl wurde nicht gefunden | Neuer Ring-Eintrag im Radialmenü (alle acht Plätze belegt) |
| Folge für die Balance: Mit Reagieren läuft eine haltende Linie einem anrückenden Feind 10 m entgegen und verschenkt den Vorteil des Haltens (Test: schmale gegen breite Linie wird dann ausgeglichen). Wer den Vorteil will, stellt „Stellung halten“ ein. Die Kompanie-Schlacht ohne Befehle bleibt ein Sieg (52 s statt 40 s). Ob Reagieren der richtige Standard ist, entscheidet Tarik beim Spielen | Tuning ist Tariks Sache; die Werte stehen in `Formation_Default` | – |

## 2026-11-01 — assetforge: Asset-Pipeline wird eigenes Repo und Submodul

Anlass (Tarik): Die Pipeline soll auch den Zombie-Rundensurvival (Repo `deadhold`, Nachfolger von zombie-jam und Dead Hold) komplett mit Assets versorgen, als eigenständiges Werkzeug, das am Ende gut aussieht.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Der projektneutrale Kern von `tools/assetgen` (`build.py`, `run.ps1` → `forge.ps1`, `lib/`) zieht in das Repo [assetforge](https://github.com/tarikaydin10/assetforge) und kommt als Git-Submodul unter `tools/assetgen` zurück. Generatoren bleiben im Spiel, jetzt unter `art/generators/`; Palette, Regeln und Specs waren schon in `art/` | Zwei Spiele, ein Werkzeug, ein Fix. Der Kern enthielt keine shieldbreak-Logik außer Pfaden und der Referenzfarbe des Kontaktbogens; beides ist jetzt Konfiguration (`art/forge.json`, Standardpfade entsprechen diesem Repo) | Kopieren wie bei MULE (zweiter Kern, der auseinanderläuft); Generatoren im Werkzeug (sie sind Spiel-Inhalt) |
| `sb.ps1 assets` ruft `tools/assetgen/forge.ps1 -Project .` und `-Check`; `sb.ps1 setup` holt das Submodul. Geometrie-Hashes blieben unverändert (Check grün nach der Umstellung) | Der eine Einstieg bleibt; nichts am Workflow ändert sich für Tarik | – |
| Werkzeug-Änderungen entstehen im Repo `assetforge`, nie im Submodul-Ordner; shieldbreak zieht sie mit `git submodule update --remote tools/assetgen` nach und committet den neuen Stand erst nach grünem `sb.ps1 assets` | Jede Spielversion zeigt auf einen festen Werkzeug-Stand (reproduzierbar) | Submodul auf `main` folgen lassen |
| Gemeinsame Generatoren (in `assetforge/generators/`) nur, wenn zwei Spiele dieselbe Form brauchen | Verhindert, dass das Werkzeug zur Asset-Bibliothek wird | – |
| Shieldbreaks Regel „keine Assets vor M4“ bleibt; der Ausbau des Werkzeugs (Bau-Kit, Waffen-Sockets, Humanoid-Rig) wird von deadhold getrieben, shieldbreak profitiert ab M4 | Kein Scope-Wachstum hier | – |

## 2026-11-06 — M3 Task 2: Bedrohungen

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Eine Formation prüft Bedrohungen nur, wenn sie am Ziel hält oder einen von der Haltung ausgelösten Angriff läuft (`FormationSystem.FaceThreat`). Marsch, befohlenes Vorrücken, Angriff, Folgen und Rückzug bleiben unberührt | Befehle gehen vor (CLAUDE.md 3.1: Spieler und AI nutzen dieselbe API, keine eigenmächtige Formation). Solange der Feind in der Kompanie nur befohlen vorrückt, wirkt auch dein Flankenangriff voll | Jede Formation dreht immer (Befehle des Spielers würden überstimmt) |
| Auslöser: mindestens `threatMinEnemies` (3) Feinde bis `threatDistance` (8 m) am Umriss der Formation, in Flanke oder Rücken, und mehr als vor der Front. Gemessen in den Achsen der Formation: als Front zählt, was vor dem vorderen Glied steht und seitlich weniger über den Flügel hinausragt als es vor ihm steht | Eine Messung vom Mittelpunkt hielte Feinde vor dem Flügel einer breiten Linie für eine Flanke. Wer vorn gebunden ist, dreht sich nicht weg und gibt dem Feind nicht den Rücken frei | Winkel vom Mittelpunkt; jeder einzelne Feind löst aus (ein Held in der Flanke drehte die ganze Linie) |
| Reaktion: Beim Halten dreht die Front zur Mitte der Bedrohung, die Formation bleibt stehen. Über `reformTurnDeg` hinaus formt sie um die Soldaten um (das Glied nächst der Bedrohung wird Front). Ein von der Haltung ausgelöster Angriff wechselt das Ziel auf die Formation des nächsten Bedrohers | Säule 2: die Armee darf ohne Spieler nie dumm werden. Drehen gilt auch bei „Stellung halten“, denn es verlässt die Stellung nicht | Stellung halten dreht nie (Linie lässt sich von hinten abschlachten) |
| Nach einer Drehung und nach jedem Befehl schaut die Formation `threatCooldownMs` (3 s) nicht nach Bedrohungen; ein Befehl setzt dafür `NextThreatTick = -1` | Kein Hin- und Herdrehen; wer eine Blickrichtung befiehlt, behält sie zunächst. `Apply` kennt den Tick nicht, daher der Merker, den der nächste Tick auflöst | Eigene Hysterese über Winkel |
| Kreis prüft nicht | Er hat keine Flanke, seine Slots schauen nach außen | – |
| Beschuss (Schildwall) kommt mit Task 5, weil es noch keinen Fernkampf gibt | Nichts zum Testen | Stummel-Ereignis jetzt |

## 2026-11-07 — M3 Task 3: Commander-Utility-AI

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `Sim/AI/CommanderUtility` (reine Funktionen: lesen, bewerten, entscheiden) und `CommanderSystem` (Burst-Job, dann Befehle). Der Kommandant läuft zuerst im Tick und gibt `FormationOrder`s direkt als Besitzer seiner Formationen | CLAUDE.md 3.1: Spieler und AI sind zwei Clients derselben API. Er kann nichts, was ein Spieler nicht kann. Zustand samt Scores ist Sim-Zustand und geht in den Hash | Eigene Eingriffe in Formationen; Kommandant in der Presentation (bräche Lockstep) |
| Fünf Taktiken: Halten, Frontalangriff, Flanke links, Flanke rechts, Rückzug. „Halten auf Höhe“ wird Halten, weil es noch keine Höhe gibt; Screening kommt mit dem Fernkampf (Task 5) | Nur was heute wirken kann | Taktiken ohne Grundlage |
| Merkmale: Kämpfer pro Seite (Verhältnis), Moral, Frontbreiten, Abstand, wie offen jede Seite des Feindes ist. Alles ist auch für den Spieler sichtbar. Stärke zählt vorerst nur Köpfe; Lanchester (Task 4) wird das verbessern | Säule 4: kein Wissen, das ein Spieler nicht hätte | Bot-Stufen des Gegners mitlesen |
| Schwierigkeit: Denkintervall (2,5 s), Rauschen auf den Scores, Flanke erlaubt oder nicht. Hysterese (+0,15) und Mindestdauer (8 s) gegen Hin und Her; ein Rückzug wartet nicht. Die erste Entscheidung ist frei | CLAUDE.md 3.7. Jede Zahl steht in `Commander_Default` | Stat-Boni |
| Flanke: Die Formation, die am weitesten auf dieser Seite steht, marschiert ans Ende der feindlichen Linie (mit Abstand `flankMargin`), Haltung Aggressiv; die anderen rücken vor und binden. Am Ziel greift die Haltung sie selbst in die Flanke | Nutzt Task 1 und 2 statt neuer Regeln. Das Ziel wandert mit, solange sie marschiert | Eigenes Flanken-Verhalten in der Formation |
| Befehle gehen nur an Formationen, die etwas anderes tun; eine Formation, die von selbst kämpft, bleibt in Ruhe, bis die Taktik wechselt | Wiederholte Befehle würden Märsche und Abklingzeiten zurücksetzen | Jede Entscheidung alle neu befehlen |
| **Ungeduld:** Halten verliert pro Minute 0,6 Punkte. Gefunden per Messung: Bei gleicher Stärke kippte er durch das Rauschen auf Halten, die Hysterese hielt ihn dort, und beide Linien standen für immer 12 m auseinander (3 von 12 Schlachten nach 180 s unentschieden) | Säule 2: die Armee darf nie dumm werden. Test: `Company_EverySeed_IsDecided_NobodyStandsAndStares` | Höherer Grundwert für Angriff (hätte auch die schwächere Seite angreifen lassen) |
| Scores: F3 in der Kompanie (`CommanderPanel`, Action-Map `CommanderDebug`), Taktik zusätzlich im F1-Menü und auf dem Ergebnisbildschirm. Die Sandbox hat noch keinen Kommandanten, das kommt mit dem 50-gegen-50-Szenario (Task 6) | Der Kommandant lebt in der Kompanie; die Anzeige gehört dorthin, wo man ihn trifft | – |

Gemessen (12 Seeds, Stufe 3, Held tut nichts): ohne Kommandant gewinnt der Spieler 9 von 12, mit Kommandant 11 von 12; die Schlachten dauern 34 bis 129 s. Erste Taktik: 5× frontal, 7× Flanke. Der Kommandant ist also abwechslungsreich, aber in dieser Aufstellung nicht stark. Ursache vermutlich die Aufstellung (dichter Schildwall gegen Linie und Keil, der Held beruhigt seine Seite), nicht geprüft; Tuning ist Tariks Sache.

## 2026-11-07 — Moral: Soldaten fliehen später

Anlass (Tarik, nach dem Anspielen): „soldaten fliehen zu früh“.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `panicDrain` von 5 auf 2,5, `routThreshold` von 0,25 auf 0,2 | Gemessen über 12 Kompanie-Schlachten mit Kommandant (Verlierer, 24 Soldaten): Der erste Soldat floh nach 2,9 Toten (12 %), die Seite war bei 12,7 Toten (53 %) gebrochen. Jetzt 8,8 (37 %) und 18,4 (77 %). Der Spieler gewinnt ohne eigenes Zutun 9 von 12 statt 11 von 12 | Noch weniger Panik: Mit `panicDrain` 2 flieht der erste erst nach 13,8 Toten, 1,5 und tiefer ist fast ein Kampf bis zum letzten Mann, Moral wäre kaum noch spürbar |
| **Hoffnungslos unterlegen:** Hat eine Seite weniger als 0,4 eigene Kämpfer pro Feind (`hopelessRatio`), verlieren ihre Soldaten 0,1 Moral pro Sekunde (`hopelessDrainPerSecond`), auch ohne Panik; solange erholen sie sich nicht. Gilt für beide Seiten, Helden brechen nie | Weniger Panik hätte Stillstände gebracht: Reste wie 2 gegen 19 wichen zurück und standen ewig, weil dort niemand mehr stirbt. Bisher verdeckte das frühe Fliehen dieses Problem | Schlacht per Regel beenden (nimmt den Soldaten die Flucht); Kommandant lässt Reste aufgeben (bräche nur für AI-Seiten, wäre ein Sonderpfad) |

## 2026-11-07 — M3 Task 4: Lanchester, und ein Fairness-Fehler aus M2

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `Sim/AI/Lanchester`: lineares Gesetz für Nahkampf. Kampfkraft = Stärke × Soldaten im Kontakt; Kontakt = gemeinsame Breite × Dichte der Front, was den Gegner überragt, zählt zu `flankShare`. Sieger verliert Kraft(Verlierer) / Kraft(Sieger) | CLAUDE.md 3.7. Bildet ab, was die Tests von M2 gemessen haben: Breite und Dichte entscheiden, nicht nur Köpfe | Quadratisches Gesetz (nur für Fernkampf richtig); Rollouts (Phase 2) |
| Der Kommandant nutzt die Prognose statt des Kopfverhältnisses für Vorteil und Rückzug. Ein Held zählt `heroStrength` (4), das passt zu „Wert 3–5 Soldaten“. Eigene Bot-Stufen kennt er (`strengthPerLevel`), feindliche Soldaten hält er für Durchschnitt | Säule 4: Die Stufe eines Soldaten ist auf dem Feld nicht zu sehen. Der Held schon | Feindliche Stufen mitlesen |
| `holdBase` von 0,4 auf 0,35 | Mit dem Helden fühlt sich der Feind am Start leicht unterlegen und hätte nur noch gewartet. Tarik gefiel der angreifende Feind | – |
| Geprüft gegen die Sim (ohne Moral, je 6 Seeds): Schildwall 14 gegen Linie 10, Prognose 1,37, gewinnt 4 von 6 und gespiegelt 5 von 6; Dauer 26 s geschätzt, 22 bis 26 s gekämpft. Nahe 1 ist der Ausgang offen, nur eine klare Prognose muss eintreffen | Ein grobes Modell, kein Orakel. Test: `Forecast_PicksTheWinnerTheSimProduces_AndARoughDuration` | – |
| **Fairness-Fehler behoben:** Dieselbe Formation siegte mit 11 Mann Vorsprung, wenn sie zuerst erzeugt war, sonst mit 6, unabhängig von Team und Seite. In der Kompanie war das immer der Spieler. Ursache: Der Kampf-Job läuft Agenten in fester Reihenfolge ab. Lösung: Die Richtung wechselt jeden Tick, und Bots lesen den Gegner aus einem Schnappschuss vom Tick-Anfang (`Seen`). Jetzt 9,2 gegen 9,4. Test: `Fairness_WhoSpawnsFirst_GivesNoAdvantage` | Säule 4 und Lockstep. Wechsel allein brach die Blocks, weil die Bots den Beginn eines Angriffs je nach Reihenfolge verschieden berechneten | Nur Wechsel; zufällige Reihenfolge (teurer, derselbe Block-Fehler) |
| Alle Sim-Jobs: `CompileSynchronously = true` | Einmal schlug ein Determinismus-Test fehl: Der gleiche Lauf im selben Prozess ergab zwei Hashes. Verdacht, nicht bewiesen: Burst kompiliert im Editor im Hintergrund, der erste Lauf rechnet noch als Mono. Seither nicht wieder aufgetreten | Burst im Editor abschalten (verboten, CLAUDE.md) |
| Kommandant nachgebessert: Der Abstand verbietet nur den *Beginn* einer Flanke, eine laufende geht weiter (vorher brach jede Flanke ab, sobald die Hauptmacht heran war). Die Ungeduld zählt seit dem letzten echten Kampf (`contactDistance`, 6 m) und wirkt auch auf den Rückzug. Eine Taktik mit Score 0 bekommt keinen Bonus, Angreifen bleibt immer möglich | Gefunden, weil nach dem Fairness-Fix wieder Schlachten stehen blieben: Die letzten Reste pendelten zwischen Rückzug und Halten | – |

Moral nach all dem neu gemessen (Kompanie, 12 Seeds): Beim Verlierer flieht der erste Soldat nach 12,8 von 24 Toten, 17,7 sind am Ende tot, die Schlacht dauert im Schnitt 59 s. Ein Held, der nichts tut, gewinnt 9 von 12. Die Werte im Eintrag „Soldaten fliehen später“ stammen von vor dem Fairness-Fix.

## 2026-11-07 — M3 Task 5: Fernkampf

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Archetyp Bogen auf derselben Grammatik: Ausholen = Spannen, Release = Lösen, Erholen = Nachlegen. `AttackData.Resolver` wählt zwischen SwingTrace und Projektil (Erweiterung 2 aus dem Papier-Test). Schaden: `E = ½·m·v²` aus der Flug-Geschwindigkeit, Masse des Pfeils, Spitze; dieselben Zonen-Schwellwerte | CLAUDE.md 3.5: keine Sonderregel für eine Waffe | Eigener Fernkampf-Pfad |
| Pfeile sind `ProjectileData`-Structs mit festem Schritt, im Hash, im `CombatJob`. Ballistik ohne Trigonometrie: Zeit = Weg / Tempo, daraus der Abwurf nach oben. Kein Eigenbeschuss | Determinismus. Pfeile fliegen im Bogen über die eigene Linie; Eigenbeschuss wäre ein eigenes Design-Thema | Raycast ohne Flugzeit (nicht ausweichbar, nicht sichtbar) |
| Zielfehler pro Bot-Stufe (`aimErrorPerMeter`, L1 0,1 bis L5 0,025), mit Vorhalt auf die Bewegung des Ziels | Säule 4: Schwierigkeit ist Fehlerrate, kein Schaden. L5 bringt 70 Pfeile ins Ziel, L1 15 | – |
| Schild blockt Pfeile im Frontbogen für Ausdauer, ohne Perfect Block; der Bogen blockt nicht. Schützen tragen ein Schwert als Zweitwaffe (`sideWeapon`) und wechseln, wenn ein Feind näher als `minRange` (5 m) ist | Papier-Test: Perfect Block gegen Pfeile ist unlesbar und im Lockstep unfair | – |
| Neues Verhalten **Abschirmen** (`FormationBehavior.Screen`): in Reichweite stehen und schießen, zu weit weg aufrücken, unter `screenFallbackDistance` (14 m) hinter die nächste eigene Infanterie zurückfallen. Der Kommandant lässt Schützen immer abschirmen, nie flankieren; im Rückzug gehen sie mit. Spieler: T/X in der Kommandoansicht | CLAUDE.md 3.6 (Fernkampf-Screening, Rückzug-hinter-Infanterie) als ein Verhalten | Zwei getrennte Verhalten |
| **Deckung (Rest von Task 2):** Ab `coverMinHits` (3) Pfeilen bildet eine haltende, angekommene Formation mit Schilden den Schildwall zu den Schützen hin, solange kein Feind bis `threatDistance` nah ist; die Soldaten halten die Schilde oben. 5 s nach dem letzten Pfeil geht sie in ihre Form zurück. Märsche und befohlene Angriffe bleiben wie befohlen | Säule 2. „Befehle gehen vor“ wie bei den Bedrohungen. Der Preis: Die Formation steht und bindet sich | Deckung auch im Marsch |
| Der Bogen für den **Helden** kommt nicht in diesem Task: Er braucht Zielen und gehaltene Spannung (Erweiterung 1 aus dem Papier-Test) und gehört zur Eingabe. Wurfwaffen (Tabelle 3.5) ebenso | Der Task ist die Schlacht: Schützen als Truppe. Steht in `IDEAS.md` | – |
| Lanchester zählt Schützen als Soldaten, aber nicht zur Front; das Modell bleibt Nahkampf, der quadratische Fernkampf (3.7) fehlt noch | Der Kommandant schätzt Schützen damit eher zu niedrig. Reicht für jetzt | – |
| Die Kompanie im Asset `Company_M2` hat je 8 Schützen; `ApplyDefaultLayout` (Tests, neue Assets) bleibt ohne | Bestehende Tests prüfen die vier Grund-Formationen. Tests mit Schützen bauen ihre Einheiten selbst | – |

Gemessen (Kompanie mit je 8 Schützen, 12 Seeds, Held tut nichts): Der Spieler gewinnt 8 von 12, im Schnitt 275 Schüsse pro Schlacht. Drei Schlachten waren nach 20 bis 40 s verloren: Pfeile treffen einen Helden, der stillsteht.

## 2026-11-07 — M3 Task 6: 50 gegen 50, Befehlsreichweite

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `Battle_50v50`: dasselbe `CompanyScenarioDefinition` und dieselbe Szene `M2_Company` wie die Kompanie, nur ein anderes Asset: Karte 140 m, je fünf Formationen (zwei Linien, Schildwall, Keil in Reserve, Schützen) und 50 Soldaten. Start über den Szenen-Katalog | Keine neue Szene, kein neuer Code: Szenarien sind Daten | Eigene Szene pro Größe |
| Kommandoansicht für eine ganze Schlacht: WASD/rechter Stick verschiebt den Blick (`CommandViewCamera.Pan`), L/rechter Stick drücken lässt die Ansicht offen (V schließt sie) | Ohne das ist das Fun-Gate (b) nicht spielbar: Die Kamera klebte am Helden, V musste die ganze Zeit gehalten werden. Die Idee „Verschieben“ aus `IDEAS.md` ist damit erledigt | Eigener Feldherr-Modus (Phase 2) |
| Figuren-Grenze von 64 auf 128 | Sonst wären bei über 100 Soldaten die übrigen nur Kapseln | VAT jetzt (M4) |
| **Experiment Befehlsreichweite:** `commandRange` im Szenario (0 = aus). Befehle des Spielers erreichen nur Formationen bis so weit vom Helden; die Sim prüft das in `ApplyInput`, jeder Peer verwirft also dieselben Befehle. Die Commander-AI hat keinen Helden und keine Reichweite. `Battle_50v50_Range` setzt 45 m, die Kommandoansicht zeigt einen Ring, blasse Formationen und „außer Reichweite“ | CLAUDE.md: Parameter, kein Fundament; bleibt oder fliegt nach dem Playtest. Als eigenes Szenario lässt es sich im Vergleich spielen | Option im Menü (die Welt wird beim Start gebaut); Befehle verzögern statt verwerfen (mehr Sim-Zustand für ein Experiment) |
| `-shotEntry <id>` im Smoke-Test startet einen Katalog-Eintrag; der Lauf loggt die Bildrate | Die Bildrate von über 100 Figuren misst kein EditMode-Test | – |

Gemessen: 0,15 ms pro Sim-Tick (Median, 100 Soldaten im Kampf, Budget 2 ms), p95 0,37 ms. Im Build 151 FPS, langsamstes Bild 22 ms (1280×720 auf diesem Rechner, 50 s Schlacht). Mit Held in der Kommandoansicht und ohne einen einzigen Befehl dauert die Schlacht 72 bis 166 s, die Seite gewinnt 3 von 6; der Held fällt nur spät in verlorenen Schlachten. Ein Held, der als Spieler untätig stehen bleibt, verliert dagegen 6 von 6 nach rund 50 s.

## 2026-11-07 — Fun-Gate M3 bestanden, Entscheidungen für M4

Anlass (Tarik): Fun-Gate M3 mit Ja beantwortet.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| **M3 ist abgeschlossen**, M4 beginnt | Fun-Gate (a) und (b) bestanden | Nachbessern vor M4 |
| **Befehlsreichweite fliegt raus:** `commandRange`, `InCommandRange`, Ring und `Battle_50v50_Range` sind entfernt | Entscheidung Tarik (CLAUDE.md 9); ohne Parameter gibt es keinen Pfad, der ungespielt mitläuft | Als Experiment im Testmodus lassen; zum Standard machen |
| **Modelle ab M4 nur aus der eigenen Pipeline** (assetforge), kein Synty | Einheitlicher Look wie bei Dead Hold, kein Kauf, keine Anpassung fremder Paletten und Proportionen. Pferd und Rüstung müssen dafür generiert werden | Synty POLYGON |
| Speer (Stangenwaffe) wird M4 Task 5 | Steht in CLAUDE.md 3.5 mit Meilenstein M2, wurde dort aber nicht gebaut. Die Reiterei braucht ihn als Gegenpol (Brace) | Reiterei ohne Gegenwaffe |

## 2026-11-07 — M4 Task 1: 100 gegen 100

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `Battle_100v100`: wieder nur Daten in der Szene `M2_Company`: Karte 200 m, je sechs Formationen (zwei Linien, Schildwall, Keil in Reserve, zwei Gruppen Schützen), 100 Soldaten, 400 Pfeile | Szenarien sind Daten; die Schützen verteilen sich auf zwei Gruppen, damit die Mitte nicht alles abbekommt | Eine einzige große Schützengruppe |
| `heroRetreatGap` von 1,5 auf 5 m | Gemessen: In der großen Schlacht fiel der Held aus der Kommandoansicht in 3 von 6 Läufen nach 50 s, während seine Seite mit 93 gegen 77 führte. Er stand direkt hinter einer zweireihigen Linie, der feindliche Keil brach durch, vier Gegner töteten ihn in 2 s. Eine solche Niederlage ist nicht verdient. Mit 5 m fällt er nur noch, wenn die Schlacht ohnehin verloren ist | Held zieht sich aktiv vor Feinden zurück (mehr Verhalten für denselben Zweck) |
| `GroundField` ergänzt den Boden zur Laufzeit bis zur Kartengröße plus 40 m Rand. Neue Kacheln kommen aus dem Modell, nicht als Kopie einer Szenen-Kachel: Die sind statisch gebatcht, eine Kopie zeichnete nichts Sichtbares | Der Boden der Szene war 100 m groß. Karten über 100 m endeten im Nichts, das betraf schon `Battle_50v50` (140 m). Die Kacheln bleiben, bis Task 3 das Gelände bringt | Mehr Kacheln fest in die Szene (eine Szene dient mehreren Kartengrößen) |
| Figuren-Grenze von 128 auf 256 | Sonst wären Soldaten Kapseln | VAT jetzt |
| Massen-Rendering (Task 2) ist bei 200 Soldaten nicht nötig und darf hinter die Reiterei rutschen | Gemessen: 257 bis 505 FPS bei 1080p auf diesem Rechner, langsamstes Bild 14 bis 22 ms. Offen bleiben 150 gegen 150 und schwächere Rechner | Task 2 sofort bauen |

Gemessen: 0,37 ms pro Sim-Tick (Median, 200 Soldaten im Kampf, Budget 2 ms), p95 0,85 ms, Spitze 1,65 ms.

## 2026-11-07 — Schützen folgen ihrer Infanterie

Anlass: Ohne einen Befehl gewann die Spielerseite 100 gegen 100 in 7 von 8 Läufen. Rücken beide Seiten vor, ist es fair (3 von 8), der Vorteil lag also beim Halten. Der Verlauf zeigte: Die Schützen der haltenden Seite schießen auf den herankommenden Feind von 34 bis 14 m, die Schützen des Angreifers bleiben auf 34 m hinter ihrer Linie stehen. Der Zielfehler wächst mit der Distanz, die Flügel des Angreifers verlieren so vor dem Kontakt rund 30 % und fliehen dann in wenigen Sekunden.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `screenFollowDistance` = 10 m: Abschirmende Schützen bleiben höchstens so weit hinter ihrer nächsten Infanterie, aber nie näher am Feind als `screenFallbackDistance` | Gilt für beide Seiten, also kein Sonderpfad des Kommandanten. Gemessen über 8 Läufe ohne Befehl: 100 gegen 100 von 7 auf 3 Siege; Spiegelduell mit vorrückender Spielerseite bleibt fair (5, 4 und 4 von 8) | 6 m: Schützen kamen so nah, dass die Kompanie auf 1 von 8 kippte |
| Kein Sturm auf den letzten Metern und keine Flanken-Vorliebe gegen stehende Feinde | Beides gemessen und wieder entfernt: Sturm ab 16 m und Flanke allein änderten 100 gegen 100 nicht (7 von 8), zusammen mit dem Folgen wurde es schlechter (6 von 8) statt 3 von 8. Ohne Wirkung kein zusätzlicher Regler | Beide als Regler behalten |

## 2026-11-07 — M4 Task 3: Höhe

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Höhen liegen auf den Zellecken des `WorldGrid`, bilinear dazwischen (`Sim/Grid/Terrain`). Agenten bleiben `float2`, die Sim liest die Höhe bei Bedarf nach. Auf flachen Karten ändert das nichts: Alle Messwerte der Szenarien ohne Hügel blieben auf die Stelle gleich | Ein Grid für alles (Abschnitt 3.4); kleinster Eingriff in Structs und Jobs | Dritte Koordinate im `AgentData` |
| Hügel sind Daten im Szenario (Mitte, Radius, Höhe), die Form ist eine Glättungskurve ohne Trigonometrie | Deterministisch und ohne Assets; echte Karten kommen in Task 8 | Höhenkarte als Textur |
| Hang wirkt auf Soldaten und auf den Anker der Formation: `uphillSlowdown` 2, `downhillBoost` 1, Grenzen 0,5 bis 1,25 | Bremste nur der Soldat, liefe der Anker davon. Gemessen: 50 m dauern flach 41 s, bergauf 60 s, bergab 37 s | – |
| **Nahkampf-Traces ignorieren den Boden**, der Vorteil von oben ist ein Energiefaktor: 25 % pro Meter, höchstens 30 % | Erst liefen die Klingen in echter Höhe. Dann ging ein Hieb von oben über den Gegner hinweg und traf nur mit dem langsamen Ende: 59 statt 481 J. Höhe wurde bestraft. Ein Soldat richtet seinen Schlag auf den Gegner aus. Jetzt: flach 481 J, von oben 547 J, von unten 414 J | Klingen in echter Höhe; Posen zum Ziel kippen (viel Aufwand für wenige Zentimeter) |
| Pfeile fliegen in echter Höhe: Start und Ziel auf dem Boden des Schützen und des Ziels, Hügel fangen sie ab, Reichweite plus oder minus 1,5 m pro Meter Höhe | Sim-seitig schon 3D; „Schießt weiter von oben“ entsteht ohne Sonderregel bei der Flugbahn, nur die Zielwahl braucht die Reichweite | – |
| Commander: Merkmale `OwnGround` und `EnemyGround`, Halten plus 0,3 oben, Frontalangriff minus 0,3 bergauf, voll ab 4 m Unterschied. Er besetzt keinen Hügel von sich aus | Task 3 verlangt das Merkmal; ein eigenes Ziel „Hügel besetzen“ ist neues Verhalten | Eigene Taktik „Höhe besetzen“ |
| Hat der Commander keine Infanterie mehr, stürmen seine Schützen mit der Zweitwaffe | Gefunden in `Battle_Hill`: Zwei Reste Schützen standen sich auf 36 m gegenüber, oft mit dem Hügel dazwischen, nach 5 Minuten war nichts entschieden | Pfeile zählen und abbrechen |
| `GroundField` baut auf Karten mit Relief ein Mesh aus den Sim-Höhen (Zelle 3 m, flach schattiert, Farben aus der Palette), auf flachen Karten bleiben die Kacheln. `SimRunner.ToWorld` setzt alles auf den Boden | Die Form kommt aus Szenario-Daten zur Laufzeit, das kann die Asset-Pipeline nicht liefern. Palette und Stil bleiben ihre. Task 9 prüft, was die Pipeline ergänzen kann (Felsen, Gras) | Unity Terrain (eigene Datenhaltung, zweite Wahrheit) |

## 2026-11-07 — M4 Task 4: Wald und Sumpf

| Entscheidung | Grund | Verworfen |
|---|---|---|
| `GroundKind` pro Zelle im `WorldGrid`; Flächen sind Daten im Szenario (Art, Mitte, Radius), mehrere Kreise ergeben jede Form | Ein Grid für alles, bewusst dieselbe Form wie die Hügel | Polygone (mehr Werkzeug für denselben Zweck) |
| Wald und Sumpf sind Zellkosten plus Tempo: Wald 0,7 und Kosten 3, Sumpf 0,5 und Kosten 5. Gilt für Soldaten und den Anker der Formation | Kosten lassen A* selbst entscheiden, wann ein Umweg lohnt. Gemessen: 50 m dauern frei 41 s, im Wald 59 s, im Sumpf 82 s | – |
| Einzelne Bäume kennt die Sim nicht, keine gesperrten Zellen im Wald | Baumstämme als Hindernisse würden Wege und Slots zerlegen und der Formation nur Stau bringen. Soldaten laufen durch gezeichnete Bäume, das fällt bei dieser Dichte kaum auf | Stämme als gesperrte Zellen |
| Reihen lösen sich im Wald über zwei Regeln auf: Jeder Soldat steht bis 1,5 m neben seinem Slot, und er kämpft ohne Leine, Schildwall oder Deckung (`FormationCombatRules.InForest`). Der Rückzug bleibt Rückzug | Eine Formation im Wald soll sichtbar und spürbar keine sein. Die Streuung zählt als Slot, sonst bremst sie den Marsch doppelt (gemessen: 79 statt 59 s) | Formation im Wald auflösen |
| Pfeile: Pro Meter unterhalb der Kronen (7 m) stoppt ein Baum den Pfeil mit 10 %. Die Würfel kommen aus Pfeil, Platz und Tick | So verschiebt die Zahl der Pfeile keine anderen Zufallszahlen. Gemessen: 20 s Schützen auf Schützen, frei 8 Treffer, im Wald 2, im Sumpf 8 | Pfeil-Zufall im Struct speichern |
| Wer im Wald steht, verliert die Deckung der Schildwand. Gemessen: Soldaten nahmen im Wald mehr Treffer als auf freiem Feld (6 zu 3 in 20 s, kleine Stichprobe). Vermutete Ursache: Die Schilde bleiben unten, die Bäume gleichen das bei wenigen Schützen nicht aus. Nicht einzeln belegt | Der Wald deckt gegen Pfeile von außen, kostet aber den Schildwall. Ein Tausch, den das Spielen prüfen muss | – |
| Bäume sind vorerst Kegel aus `GroundField`, per Instancing und in Palettenfarben. Dichte 6 % der Waldzellen | Task 9 bringt Modelle aus der Pipeline; bis dahin muss man den Wald sehen. Die Dichte hält Soldaten aus der Kommandoansicht erkennbar | Unsichtbarer Wald bis Task 9 |
| Hauptmenü: Eine Seite „Schlacht wählen“ statt je eines Knopfes | Bei 720p reichte der Platz nicht mehr, Task 8 bringt weitere Karten | – |

## 2026-11-07 — M4 Task 5: Speer

Anlass (Tarik): Fragt nach Pferden, Städten, Quests und Wirtschaft und will weiter. Reihenfolge bleibt: Speer, Reiterei, Karten, Modelle, dann M4b. Der Speer steht vorn, weil die Infanterie sonst nichts gegen Pferde hätte.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Archetyp `Polearm` auf derselben Grammatik: Stich (auch für die Richtung Oben), Hieb links und rechts, Special Rundumschlag als gewöhnlicher Angriff mit bis zu 4 Zielen (mehr fasst die Trefferliste nicht). Griff bei 95 cm vom Ende, 1,35 m Klinge vor der Hand, wie im Modell | Die Grammatik trägt ohne Änderung: kein neuer Resolver, `ThrustTrace` ist hier eine `SwingTrace` mit geradem Weg. Das Modell `WPN_spear_basic` lag seit M0 bereit | Eigener Resolver für Stiche |
| Zwei neue Werte im `WeaponDefinition`: `blockCost` (1,6) macht den Speer zum schwachen Schild, `engageBonus` (0,7 m) lässt Bots aus größerem Abstand kämpfen | Die Reichweite nützt nur, wenn Bots sie auch nutzen; der schwache Block gehört laut 3.5 zur Waffe | Reichweite pro Bot-Stufe |
| Speere haben keine Schilde und stehen in den großen Schlachten als rechte Linie, auf beiden Seiten. Der Held trägt einen Speer im dritten Slot | Sichtbar und spielbar, ohne neue Szenarien. Die Stärke im Vergleich zum Schwert ist noch nicht austariert | Eigenes Szenario |
| Brace erst mit der Reiterei | Es wirkt nur gegen herankommende Reiter und lässt sich vorher nicht testen | Brace jetzt ohne Gegner |

Gemessen: Ein Stich auf 1,8 m hat 689 J und macht 56 Schaden, ein Stich gegen einen Block kostet mit dem Speer 37 Ausdauer statt 23 mit dem Zweihänder.

## 2026-11-07 — Fluchtregeln nach Bannerlord

Anlass (Tarik): Ein Screenshot nach 94 s: Der Feind hatte 45 Tote, 55 flohen, obwohl er noch 55 Mann gegen 64 hatte. „die schlacht muss erstmal ausgeführt werden“, TDD, Orientierung an Bannerlord, „es geht um Chance“, Verluste im Vergleich zum Gegner zählen.

Bannerlord laut Entwickler-Blog (keine Zahlen veröffentlicht): Startmoral pro Soldat, Tote in der Nähe drücken sie (gedeckelt), Kills heben sie, der Schildwall dämpft, Skills des Kommandanten verändern die Abzüge, jeder Fliehende drückt die Umstehenden, daraus eine Kettenreaktion bis zum Bruch ganzer Formationen. Der Soldat flieht mit einer Wahrscheinlichkeit, nicht per Schalter.

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Flucht ist **Zufall**: Unter der Schwelle bricht ein Soldat mit 0,8 pro Sekunde, tiefer darunter bis zum Vierfachen (`MoraleRules.BreakChance`). Die Würfel kommen aus Tick und Soldat | Soldaten gehen nacheinander statt im selben Tick, und jede Schlacht endet etwas anders | Feste Schwelle |
| **Verlustgefälle:** Hat eine Seite einen um mehr als 25 Punkte größeren Anteil verloren als der Gegner, verliert jeder ihrer Soldaten 0,15 Moral pro Sekunde und Einheit des Gefälles (`MoraleRules.LossGapDrain`). Gleiche Verluste kosten nichts | Tarik: Wer 50 bis 60 % verliert, während der Gegner kaum etwas verliert, wankt. Erst mit 15 Punkten und 0,3 gemessen: Am Hügel und am Wald brach die Armee wieder bei 40 bis 50 Toten, weil Pfeile früh treffen | 15 Punkte, 0,3 |
| **Panik gedeckelt** bei 0,15 (`panicCap`): Was ein Soldat spürt, wird bei vielen Toten um ihn nicht schlimmer | Bannerlord deckelt den Abzug pro Toten | Kein Deckel |
| **Schildwall** dämpft stärker als die Linie: Faktor 0,4 statt 0,7 (`shieldWallSteadiness`) | Bannerlord: Im Schildwall wirken Abzüge schwächer. Größter Hebel der Messung: allein dadurch fielen beim Verlierer 79 statt 61 Mann | – |
| Ansteckung durch Fliehende halbiert (0,3 auf 0,15 pro Sekunde); die Kettenreaktion bleibt | Mit 0,3 und 0,6 brach in manchen Schlachten die halbe Armee bei 50 Toten (Messreihe mit sechs Kombinationen) | 0,3; 0,6 |
| **Hoffnungslos** schon unter 0,55 eigenen Kämpfern pro Feind statt unter 0,4 | Reste standen sonst ewig: 4 gegen 8 kämpften in der Kompanie nach 3 Minuten noch | 0,4 |
| **Verworfen nach Tarik („ist das wirklich optimal?“):** Erst hatte ich eine harte Regel getestet, nach der ein Soldat nur bricht, wenn er vor Ort unterlegen ist | Sie hätte die Kettenreaktion abgewürgt, Linien hätten bis zum letzten Mann gestanden: realistisch gedacht, aber kein Spaß und nicht Bannerlord | Lokale Übermacht als Bedingung |
| Ein „Wert des Generals“ kommt später (`IDEAS.md`) | Bannerlords Skills des Kommandanten verändern die Abzüge; bei uns wäre das ein Faktor auf `panicDrain` pro Seite | Jetzt bauen |

Gemessen (je 6 Schlachten aus der Kommandoansicht, Verlierer Tote + Geflohene): 100 gegen 100 vorher 61 Tote im Schnitt, mindestens 47; jetzt 76, mindestens 71. Am Hügel 65 im Schnitt, einmal 48; am Wald 64, einmal 48. Es fliehen jeweils 20 bis 50. Kompanie: Der erste flieht nach 17 von 24 Toten statt nach 13.

## 2026-11-08 — M4 Task 6: Reiterei in der Sim

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Reiter und Pferd sind **ein Agent** mit Flag `Mounted`; das Pferd hat kein eigenes Leben | CLAUDE.md M4: „Reiter als Einheit“. Ein Agent hält Formationen, Moral und Spatial Hash unverändert | Pferd als eigener Agent mit Reiter darauf |
| Pferd mit **Trägheit**: 9 m/s, 3 m/s² Beschleunigung, 6 m/s² Bremsen, 8 m/s² Querbeschleunigung (Wendekreis v²/8), läuft nur in Blickrichtung | Ein Pferd muss Anlauf nehmen und im Bogen wenden, sonst ist eine Reiterei nur schnelle Infanterie | Gleiche Steuerung wie zu Fuß mit höherem Tempo |
| Die **Sattelhöhe** spielt in der Sim keine Rolle | Wie am Hang: Hiebe zielen auf den Gegner (`DECISIONS.md` 2026-11-07). Pfeile zielen auf die Brust und treffen den Reiter | Eigene Trefferkapseln für Reiter |
| **Lanze = ContactTrace:** Die Strecke Griff bis Spitze wird gegen die Körperkapseln getestet, Energie ½·m·v² aus der Annäherung entlang der Lanze, Wirkmasse 10 kg | CLAUDE.md 3.5: Energie aus Relativgeschwindigkeit. 10 kg sind Lanze, Arm und Sattel: Galopp tötet (118 Schaden), Trab verwundet, Stand tut nichts | Lanzenmasse 4 kg (dann bräuchte es einen Faktor, der kein Gewicht ist) |
| **Niederreiten** mit 500 kg aus der Annäherung, 2,5 % davon treffen den Mann (40 Schaden im Galopp), das Pferd behält 75 % pro Mann | Die Wucht in die Linie soll belohnt werden (CLAUDE.md 3.5), aber ein Reiter darf nicht durch zehn Glieder pflügen | Pferd bleibt am ersten Mann stehen |
| **Brace** ist der Block des Speers gegen einen Reiter, kein neues Verb | Die Grammatik bleibt klein (CLAUDE.md 3.5: kein neues Verb ohne Not); der Bot nutzt dieselbe Reaktionszeit wie gegen ein Schwert | Eigene Taste und eigener Zustand Brace |
| **Schildwall:** Ein Pferd scheut vor einem blockenden Soldaten im Schildwall (15 % Tempo, Reiter 600 ms benommen); eine Linie mit Schilden wird überritten | Reale Pferde laufen nicht in eine geschlossene Wand; der Schildwall bekommt damit eine eigene Aufgabe | Jeder erhobene Schild stoppt das Pferd |
| Soldaten sehen **keinen Reiter im Rücken** | Ein Angriff in Flanke und Rücken muss sich lohnen (Säule 2); fair, weil Bots nur sehen, was ein Spieler sähe | Rundumsicht |
| Bis Task 7 schickt der Kommandant seine Reiter immer in den **Angriff** (außer beim Rückzug); in `Battle_Cavalry` greifen auch deine von Anfang an an. Reiter wählen Soldaten in Formationen vor Einzelnen | Eigene Reiter-Taktik, Formationsabstände und „Schildwall bei Kavallerie“ sind Task 7 | Reiter wie Infanterie führen |
| Ein Soldat achtet auf jeden Reiter, der auf ihn zureitet (ab 1 m/s), nicht erst im Galopp; der unbeaufsichtigte Held blockt immer | Gemessen: Ein Reiter kam im Schritt heran und galoppierte erst die letzten 8 m, der Held (L2) reagierte zu spät und fiel nach 19 s | Erst ab 3 m/s |
| Pferde sind bis Task 9 **Quader in Palettenfarben** | Wie die Kegelbäume: sichtbar und lesbar, das Modell kommt aus der Pipeline | Schnell ein Modell in Blender |

## 2026-11-08 — M4 Task 7: Reiterei führen

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Reiter-Formationen haben für alle Typen dieselben Abstände (2,2 m breit, 3,6 m tief) und traben mit 4,5 m/s | Ein Pferd ist länger als breit; eine eigene Spacing-Tabelle pro Typ brächte heute nichts | Abstände pro Typ |
| Reiter-Vorrücken wird ab 30 m zum Angriff | Reiter, die im Trab bis an den Feind kommen und dort stehen, wären nutzlos; dasselbe gilt für die Haltung Reagieren | Eigenes Verhalten |
| **Schildwall bei Kavallerie** läuft über denselben Mechanismus wie die Deckung unter Pfeilen (`CoverType`): ab 3 Reitern, die in 35 m auf die Formation zureiten, solange kein Feind zu Fuß an ihr steht. Speere und andere ohne Schild drehen sich nur zu den Reitern, wenn sie von außerhalb der Front kommen | Eine Regel statt zwei; Speere stemmen einzeln (Task 6), ein Speerwall wäre eine neue Form | Eigener Formationstyp |
| Der Kommandant hält seine Reiter zurück, bis die Linien sich auf 30 m nähern, und wertet Ziele: Schützen +1, Kämpfende +0,5, Speere −1, Schildwall −0,8, Reiter −1, je Meter −0,01. Er wechselt nur für 0,4 mehr | CLAUDE.md 3.7: Kav-vs-Speer. Nur, was jeder auf dem Feld sieht | Reiter sofort losschicken (Task 6) |
| Reiter zählen für den Kommandanten nicht als Front und nicht für den Abstand der Linien | Sonst misst er seine Schlachtreihe an Reitern, die 100 m daneben stehen | – |
| Der Held reitet über `heroMounted` im Szenario, ohne Auf- und Absitzen | Die Verben der Grammatik bleiben; Absitzen braucht ein eigenes Konzept (`IDEAS.md`) | Absitzen jetzt |
| Steuerung zu Pferd: W reitet in Blickrichtung, die Maus lenkt also, S bremst. Das Bremsen entsteht in `PlayerCombatInput` (kein Weg = bremsen), nicht in der Sim | Erst steckte es in der Sim als „Weg gegen die Blickrichtung“; dann hätte man nie wenden können, wenn die Kamera nach hinten schaut | Bremsen aus dem Winkel in der Sim |
| Zu Pferd gibt es keinen Schritt, Sprung und Sprint; die Lanze blockt und stößt nicht. Sieht der Reiter sich um, dreht sich das Pferd nicht | Schicht D „Beritten“; ungültige Eingaben werden ignoriert, nie geworfen | – |
| Der unbeaufsichtigte Held zieht die Zweitwaffe | CLAUDE.md 3.9: Er soll den Block halten, mit der Lanze kann er das nicht | Lanze behalten |

## 2026-11-08 — M4 Task 8: Karten und Aufstellung

| Entscheidung | Grund | Verworfen |
|---|---|---|
| Karten sind ein eigenes ScriptableObject (`MapDefinition`), Szenarien verweisen darauf | Der Kriegszug (M4b) soll beliebige Heere auf beliebige Karten schicken können | Hügel und Flächen weiter im Szenario |
| Die Aufstellung ist ein Sim-Zustand: Nur Eingaben laufen, kein Kommandant, keine Bewegung, kein Kampf; `Tick` läuft weiter | Eingaben brauchen Ticks, und in M6 muss die Phase im Lockstep laufen | Aufstellung nur in der Presentation vor dem Weltstart |
| Dieselbe `FormationOrder` wirkt in der Aufstellung sofort und wird auf die Zone begrenzt; Verhalten und Haltung gelten ab Schlachtbeginn | CLAUDE.md 3.1: eine Befehls-API, kein Sonderpfad | Eigener Befehl „Aufstellen“ |
| Zonen begrenzen den Ankerpunkt der Formation, nicht ihre ganze Breite | Einfach und lesbar; am Rand ragt eine Formation etwas hinaus | Volle Breite prüfen |
| `EndDeployment` kommt von jedem Spieler mit Held, die Schlacht beginnt, wenn alle bereit sind | Multiplayer-fähig ab Tag 1 (Säule 5) | Nur ein Spieler |
| Der Feind stellt sich nach Szenario-Daten auf, er reagiert nicht auf deine Aufstellung | Eine Aufstellungs-KI ist eigene Arbeit (`IDEAS.md`). Er sieht dich also nicht, du siehst ihn: Das ist dein Vorteil | Aufstellung durch den Kommandanten |
| Der Held steht zu Schlachtbeginn hinter der Mitte seines Heeres | Sonst stünde er nach dem Umstellen allein auf dem Feld | Eigener Platz in der Aufstellung |
| Deine Reiter greifen auf den Karten mit Schlachtbeginn an | „Die Armee darf ohne Spieler nie dumm werden“: Gemessen standen sie sonst tatenlos, während der Feind seine nutzte. In der Aufstellung sichtbar und mit „Halten“ änderbar | Warten auf Befehl |
| Der unbeaufsichtigte Held folgt nie Reitern; zu Pferd auch nicht Schützen, und zu Pferd hält er den Block auch ohne Feind in der Nähe | Gemessen: Er ritt hinter angreifenden Reitern in den Feind (53 s). Die Schützen-Regel gilt bewusst nur zu Pferd, zu Fuß verschlechterte sie 50 gegen 50 (von 2 auf 1 von 6) | Für alle gleich |

## Offen (Entscheidung bis M3)

- Setting (historisch vs. Low-Fantasy); spätestens vor M4b, weil die Story davon abhängt
- Spielname (Arbeitstitel SHIELDBREAK)
