# art

Quelle für den Look. Modelle entstehen nur hieraus, gebaut von [tools/assetgen](../tools/assetgen/README.md).

![Kontaktbogen](contact-sheet.png)

| Pfad | Inhalt |
|---|---|
| `style/palette.json` | die 40 erlaubten Farben, per Name referenziert |
| `style/style.json` | Regeln: Polygon-Budget, Größe, Pivot pro Kategorie |
| `style/style-bible.md` | warum die Regeln so sind |
| `specs/*.json` | eine Spec pro Asset: Generator, Parameter, Farbnamen |
| `contact-sheet.png` | alle Assets nebeneinander, von der Pipeline gerendert |

Neues Asset: Spec in `specs/` kopieren, anpassen, `.\tools\assetgen\run.ps1`, Kontaktbogen ansehen.
