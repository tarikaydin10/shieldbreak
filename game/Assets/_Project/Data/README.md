# Data (Assets)

Tunebare Werte als ScriptableObject-Assets. Im Inspector ändern, Play drücken; kein Code nötig. Klassen dazu: [Scripts/Data](../Scripts/Data/README.md).

| Ordner | Assets |
|---|---|
| `Agents/` | `AgentMovement_Default` |
| `Scenarios/` | `Duel_M1`, `Company_M2`, `Sandbox_20v20`, `Stress_250v250` |
| `Attacks/` | `OneHand_Overhead/Left/Right/Thrust`, `TwoHand_Overhead/Left/Right/Thrust/Leap`, `Attack_OneHand_Overhead` (Hieb-Prototyp) |
| `Weapons/` | `Weapon_OneHandShield`, `Weapon_TwoHand` |
| `Combat/` | Presets `CombatTuning_Schwer/Mittel/Toedlich`, `BotRoster_Default` (L1–L5) |
| `Morale/` | `Morale_Default` (Panik, Moral, Rout; in der Sandbox aktiv, im Duell nicht) |
| `Visuals/` | `SoldierVisual_Default` |

Szenen-Builder legen fehlende Assets an, überschreiben aber nie bestehende Werte.

Der Szenen-Katalog (Ziel, Steuerung und Prüfliste pro Szene) liegt nicht hier, sondern in `../Resources/SceneCatalog.asset`, weil ihn jede Szene zur Laufzeit lädt.
