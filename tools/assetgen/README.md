# assetgen

Baut jede Spec aus `art/specs/` in headless Blender 5.1, prüft sie gegen `art/style/` und exportiert nach `game/Assets/_Project/Art/Generated/`.

![Kontaktbogen](../../art/contact-sheet.png)

```powershell
.\tools\assetgen\run.ps1            # bauen, prüfen, exportieren, Kontaktbogen
.\tools\assetgen\run.ps1 -Check     # neu bauen, Hashes mit manifest.json vergleichen
.\tools\assetgen\run.ps1 -NoRender  # ohne Kontaktbogen
```

Neues Asset: Spec kopieren, Name, Parameter und Farbnamen ändern, `run.ps1`. Neue Form: neuer Generator in `generators/`, nie von Hand modellieren.

| Datei | Zweck |
|---|---|
| `build.py` | Einstieg in Blender |
| `lib/meshkit.py` | Grundformen, Palette-UVs, feste Flächenreihenfolge |
| `lib/validate.py` | Stilregeln, Geometrie-Hash |
| `lib/contact_sheet.py` | Kontaktbogen |
| `generators/*.py` | ein Modul pro Asset-Typ |
