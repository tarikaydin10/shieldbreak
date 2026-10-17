# SHIELDBREAK

Low-Poly-Schlachtsimulator, Unity 6 (URP). Stand: M1 (Kampf), alles auf `main`.

![Duell](docs/screenshots/m1_duel.jpg)
![Nahkampf](docs/screenshots/proto_close.jpg)
![Kommandoansicht](docs/screenshots/proto_command.jpg)
![Oben-Hieb: Seite und 15 m](docs/screenshots/proto_attack.jpg)

## Setup

```powershell
git lfs install
git clone https://github.com/tarikaydin10/shieldbreak.git
```

Unity Hub → Add → Ordner `game`. Szenen in `Assets/_Project/Scenes/`: `M1_Duel` (du gegen Bot), `M0_Sandbox` (20 gegen 20), `Proto_Animation` (Hieb), `Proto_Style` (Look). **F1** zeigt alle Tasten, **Esc** pausiert.

## Befehle

```powershell
.\tools\run-tests.ps1             # EditMode-Tests (Unity geschlossen)
.\tools\run-tests.ps1 -Shadow     # dasselbe bei offenem Unity, auf einer Kopie
.\tools\assetgen\run.ps1          # Assets bauen
.\tools\assetgen\run.ps1 -Check   # Assets auf Reproduzierbarkeit prüfen
.\tools\build-windows.ps1 -Shadow  # spielbarer Build nach builds\<datum>
```

## Doku

[CLAUDE.md](CLAUDE.md) · [DECISIONS.md](DECISIONS.md) · [IDEAS.md](IDEAS.md) · [Unity-Projekt](game/README.md) · [Code](game/Assets/_Project/Scripts/README.md) · [Tests](game/Assets/Tests/EditMode/README.md) · [Art](art/README.md) · [Tools](tools/README.md) · [Docs](docs/README.md)
