# Data (Assets)

Tunebare Werte als ScriptableObject-Assets. Im Inspector ändern, Play drücken; kein Code nötig. Klassen dazu: [Scripts/Data](../Scripts/Data/README.md).

| Ordner | Assets |
|---|---|
| `Agents/` | `AgentMovement_Default` |
| `Weapons/`, `Attacks/` | Auch `Weapon_Polearm` und `Polearm_*` (Stich, Hieb, Rundumschlag), `Weapon_Lance` und `Lance_Couch` (eingelegte Lanze, `couched`) |
| `Scenarios/` | `Duel_M1`, `Company_M2`, `Battle_50v50`, `Battle_100v100`, `Battle_Hill` (mit Hügeln), `Battle_Forest` (Wald und Sumpf), `Battle_Cavalry` (je 12 Reiter auf der Flanke), `Sandbox_20v20`, `Stress_250v250` |
| `Attacks/` | `OneHand_Overhead/Left/Right/Thrust`, `TwoHand_Overhead/Left/Right/Thrust/Leap`, `Bow_Shot`, `Attack_OneHand_Overhead` (Hieb-Prototyp) |
| `Weapons/` | `Weapon_OneHandShield`, `Weapon_TwoHand`, `Weapon_Bow` |
| `Combat/` | Presets `CombatTuning_Schwer/Mittel/Toedlich`, `BotRoster_Default` (L1–L5) |
| `Morale/` | `Morale_Default` (Panik, Moral, Rout; in der Sandbox aktiv, im Duell nicht) |
| `Commanders/` | `Commander_Default` (Feind-Kommandant: Denkintervall, Gewichte der Taktiken, Rauschen; in der Kompanie aktiv) |
| `Visuals/` | `SoldierVisual_Default` |

Szenen-Builder legen fehlende Assets an, überschreiben aber nie bestehende Werte.

Der Szenen-Katalog (Ziel, Steuerung und Prüfliste pro Szene) liegt nicht hier, sondern in `../Resources/SceneCatalog.asset`, weil ihn jede Szene zur Laufzeit lädt.
