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

## Offen (Entscheidung bis M3)

- Setting (historisch vs. Low-Fantasy)
- Spielname (Arbeitstitel SHIELDBREAK)
- Befehlsreichweite / Sichtlinie: Experiment in M3, Parameter
- Progression zwischen Schlachten
