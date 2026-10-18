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

## Offen (Entscheidung bis M3)

- Setting (historisch vs. Low-Fantasy)
- Spielname (Arbeitstitel SHIELDBREAK)
- Befehlsreichweite / Sichtlinie: Experiment in M3, Parameter
- Progression zwischen Schlachten
- Synty POLYGON ab M4 vs. eigener Pipeline-Style (Synty passt nur, wenn Palette und Proportionen übernommen werden)
