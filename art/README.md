# art

Quelle für den Look. Modelle entstehen nur hieraus, gebaut von [assetforge](../tools/assetgen/README.md) (Submodul `tools/assetgen`).

![Kontaktbogen](contact-sheet.png)

| Pfad | Inhalt |
|---|---|
| `style/palette.json` | die 40 erlaubten Farben, per Name referenziert |
| `style/style.json` | Regeln: Polygon-Budget, Größe, Pivot pro Kategorie |
| `style/style-bible.md` | warum die Regeln so sind |
| `specs/*.json` | eine Spec pro Asset: Generator, Parameter, Farbnamen |
| `generators/*.py` | ein Generator pro Form (Schwert, Speer, Schild, Helm, Figur, Gliedmaße, Bodenstück), siehe [generators/README.md](generators/README.md) |
| `contact-sheet.png` | alle Assets nebeneinander, von der Pipeline gerendert |

Ausgabe: FBX und `manifest.json` nach `game/Assets/_Project/Art/Generated/`, Palettentextur nach `game/Assets/_Project/Art/Palette/palette.png` (Standardpfade von assetforge, keine `forge.json` nötig).

Neues Asset: Spec in `specs/` kopieren, anpassen, `.\sb.ps1 assets`, Kontaktbogen ansehen.
