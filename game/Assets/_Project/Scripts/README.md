# Scripts

Jeder Ordner ist eine eigene Assembly. Abhängigkeiten zeigen nur nach unten:

```
Editor  ──►  DebugTools  ──►  Presentation  ──►  Data  ──►  Sim
```

| Ordner | Assembly | Inhalt | Regel |
|---|---|---|---|
| [Sim/](Sim/README.md) | `Shieldbreak.Sim` | deterministische Simulation | kein `UnityEngine`, nur Structs, NativeArrays, Burst |
| [Data/](Data/README.md) | `Shieldbreak.Data` | ScriptableObject-Klassen, bauen Sim-Structs | Balancing-Werte nur hier |
| [Presentation/](Presentation/README.md) | `Shieldbreak.Presentation` | SimRunner, Rendering, Kameras, F1-Menü | liest die Sim, ändert sie nur über Befehle |
| `Debug/` | `Shieldbreak.DebugTools` | Sandbox-Auswahl und -Befehle, Grid-Overlay | reine Werkzeuge |
| `Editor/` | `Shieldbreak.Editor` | Szenen-Builder, Build, Import-Regeln | nur im Editor |

Eingaben: jede Taste ist eine Action in `Resources/Input/Shieldbreak.inputactions` mit Beschreibung in `controls.de.json`. Kein `Keyboard.current` o. ä. (Test prüft das).
