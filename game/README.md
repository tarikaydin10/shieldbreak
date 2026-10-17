# game

Das Unity-Projekt (Unity 6000.5, URP). Im Unity Hub diesen Ordner öffnen.

## Szenen (`Assets/_Project/Scenes/`)

| Szene | Zweck |
|---|---|
| `M1_Duel` | Du gegen einen Bot: WASD, Maus, linke Taste tippen = leicht, halten = schwer, rechte Taste Block, Q Finte, Tab Richtungs-Steuerung; F5–F9 Duell-Optionen |
| `M0_Sandbox` | Draufsicht, 20 gegen 20 marschieren lassen (Auswahl, Rechtsklick-Befehl) |
| `Proto_Animation` | Oben-Hieb mit prozeduralem Arm, Zeitleiste in Ticks |
| `Proto_Style` | Look-Test: Formationen, Licht, Post-FX, Ansichten 4 m bis 40 m |

In jeder Szene: **F1** zeigt alle Tasten und wechselt Szenen, **Esc** pausiert.

## Aufbau (`Assets/_Project/`)

| Ordner | Inhalt |
|---|---|
| [Scripts/](Assets/_Project/Scripts/README.md) | Code, getrennt in Assemblies (Sim, Data, Presentation, DebugTools, Editor) |
| [Data/](Assets/_Project/Data/README.md) | ScriptableObject-Assets: Bewegung, Szenarien, Angriffe, Waffen, Aussehen |
| `Resources/Input/` | `Shieldbreak.inputactions` (alle Eingaben) und `controls.de.json` (Texte fürs F1-Menü) |
| `Art/Generated/` | Modelle aus der Pipeline; nicht von Hand ändern |
| `Art/Palette/`, `Art/PostFX/`, `Art/Debug/` | Palette-Material, Post-FX-Profil, Debug-Materialien |
| `Scenes/` | siehe oben; per Menü **Shieldbreak → Build …** neu erzeugbar |

Tests: [Assets/Tests/EditMode](Assets/Tests/EditMode/README.md).
