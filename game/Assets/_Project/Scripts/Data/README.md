# Data (Klassen)

ScriptableObject-Klassen. Jede wandelt ihre Werte in einen Sim-Struct um, damit `Sim/` Unity nicht kennt. Die Assets selbst liegen in `Assets/_Project/Data/`.

| Klasse | Wird zu | Inhalt |
|---|---|---|
| `AgentMovementDefinition` | `AgentMoveSettings` | Tempo, Beschleunigung, Abstand, Drehrate |
| `SandboxScenarioDefinition` | `SimConfig` + Spawns | Karte, Seed, Spawn-Gruppen |
| `AttackDefinition` | `AttackData` + Keys | Phasen in ms, Waffenbahn-Keyframes, Klinge, Ziele pro Hieb |
| `WeaponDefinition` | `WeaponData` | Masse, Schneide/Spitze, Blockbogen, 4 Angriffe |
| `CombatTuningDefinition` | `CombatTuning` | Lebenspunkte, Stamina, Zeitfenster, Tödlichkeit (Feel-Preset) |
| `BotRosterDefinition` | `BotProfile[]` | Bot-Stufen L1–L5 |
| `CombatDatabase` | `CombatSetup` | baut alle Kampf-Tabellen aus den obigen |
| `DuelScenarioDefinition` | `SimWorld` | Duell: Held gegen Bots, Presets, Bot-Stufe, Zuschauermodus |
| `ISimScenario` | – | Schnittstelle: ein Szenario baut eine `SimWorld` (Sandbox, Duell) |
| `SoldierVisualDefinition` | – (nur Darstellung) | Meshes und Befestigungspunkte pro Team, Kampf-Figuren-Teile |

Eine Klasse pro Datei mit gleichem Namen, sonst findet Unity die Assets nicht.
