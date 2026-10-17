# SHIELDBREAK

Low-Poly-Schlachtsimulator, Unity 6 (URP). Stand: M0, laufende Arbeit auf `m0`.

![Nahkampf](docs/screenshots/proto_close.jpg)
![Kommandoansicht](docs/screenshots/proto_command.jpg)
![Oben-Hieb: Seite und 15 m](docs/screenshots/proto_attack.jpg)

## Setup

```powershell
git lfs install
git clone https://github.com/tarikaydin10/shieldbreak.git
```

Unity Hub → Add → Ordner `game`. Szenen in `Assets/_Project/Scenes/`: `M0_Sandbox` (spielbar), `Proto_Animation` (Hieb), `Proto_Style` (Look). **F1** zeigt alle Tasten, **Esc** pausiert.

## Befehle

```powershell
.\tools\run-tests.ps1             # EditMode-Tests (Unity geschlossen)
.\tools\run-tests.ps1 -Shadow     # dasselbe bei offenem Unity, auf einer Kopie
.\tools\assetgen\run.ps1          # Assets bauen
.\tools\assetgen\run.ps1 -Check   # Assets auf Reproduzierbarkeit prüfen
.\tools\build-windows.ps1 -Shadow  # spielbarer Build nach builds\<datum>
```

## Doku

[CLAUDE.md](CLAUDE.md) · [DECISIONS.md](DECISIONS.md) · [IDEAS.md](IDEAS.md) · [Style-Bible](art/style/style-bible.md) · [Asset-Pipeline](tools/assetgen/README.md)
