# Data (Klassen)

ScriptableObject-Klassen. Jede wandelt ihre Werte in einen Sim-Struct um, damit `Sim/` Unity nicht kennt. Die Assets selbst liegen in `Assets/_Project/Data/`.

| Klasse | Wird zu | Inhalt |
|---|---|---|
| `AgentMovementDefinition` | `AgentMoveSettings` | Tempo, Beschleunigung, Abstand, Wand-Ausweichen, Drehrate |
| `FormationDefinition` | `FormationSettings` | Abstände pro Formationstyp, Marschtempo, Zusammenhalt, Umform-Intervall nach Verlusten, Weg-Abstand zu Wänden, Schwenken und Umformen beim Drehen, Kampf in Formation (Leine, Frontbogen, Selbstverteidigung), Verhalten (Kontaktabstand beim Vorrücken, Rückzugsweite, Folgeabstand, wie oft das Ziel geprüft wird) |
| `SandboxScenarioDefinition` | `SimConfig` + Spawns | Karte, Seed, Spawn-Gruppen (als Formation mit Typ oder lose, Bot-Stufe und Waffe), Hindernisse (gesperrt oder teuer), Formations-Tuning; mit Waffen und Kampf-Preset kämpfen die Soldaten, mit Moral brechen sie |
| `MoraleDefinition` | `MoraleSettings` | Panik-Feld (Diffusion, Abklingen), Quellen und Senken (Tote, Flankentreffer, Kills, Fliehende, Held), Moral pro Soldat (Verlust, Formation dämpft, Erholung, Rout-Schwelle), Flucht |
| `AttackDefinition` | `AttackData` + Keys | Phasen in ms, Waffenbahn-Keyframes, Klinge, Ziele pro Hieb, Ausfallschritt; Startwerte Einhand, Zweihand, Sprung-Hieb |
| `WeaponDefinition` | `WeaponData` | Archetyp, Masse, Schneide/Spitze, Blockbogen, 4 Angriffe + Spezial-Angriff |
| `CombatTuningDefinition` | `CombatTuning` | Lebenspunkte, Stamina, Zeitfenster, Tödlichkeit (Feel-Preset) |
| `BotRosterDefinition` | `BotProfile[]` | Bot-Stufen L1–L5 |
| `CombatDatabase` | `CombatSetup` | baut alle Kampf-Tabellen aus den obigen |
| `CompanyScenarioDefinition` | `SimWorld` | Kompanie: Held (Spieler 0) mit Loadout und Tempo, eigene und feindliche Formationen (Typ, Größe, Stufe, Waffe, Startverhalten), Waffen, Preset, Formations- und Moral-Tuning |
| `DuelScenarioDefinition` | `SimWorld` | Duell: Held mit Loadout gegen 1–3 Bots, Presets, Bot-Stufe und -Waffe, Zuschauermodus |
| `ISimScenario` | – | Schnittstelle: ein Szenario baut eine `SimWorld` (Sandbox, Duell) |
| `SoldierVisualDefinition` | – (nur Darstellung) | Meshes und Befestigungspunkte pro Team, Kampf-Figuren-Teile |

Eine Klasse pro Datei mit gleichem Namen, sonst findet Unity die Assets nicht.
