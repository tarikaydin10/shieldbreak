# SHIELDBREAK

Low-Poly-Schlachtsimulator, Unity 6 (URP). Stand: M1 (Kampf), alles auf `main`.

**Was es gibt, wie man es spielt und testet: [docs/STATUS.md](docs/STATUS.md)** (immer aktuell).

![Hauptmenü](docs/screenshots/main_menu.jpg)
![Duell](docs/screenshots/m1_duel.jpg)
![Zweihänder gegen Schild](docs/screenshots/m1_twohand.jpg)
![Nahkampf](docs/screenshots/proto_close.jpg)
![Kommandoansicht](docs/screenshots/proto_command.jpg)
![Oben-Hieb: Seite und 15 m](docs/screenshots/proto_attack.jpg)

## Setup

```powershell
git lfs install
git clone https://github.com/tarikaydin10/shieldbreak.git
```

Unity Hub → Add → Ordner `game`. Szene `Assets/_Project/Scenes/MainMenu` starten: von dort Duell, Tutorial, Sandbox und Prototypen. **Esc** Pausemenü mit Steuerung und Optionen, **F1** Status und Admin.

## Befehle

Ein Skript für alles, aus dem Repo-Root in PowerShell:

```powershell
.\sb.ps1 play      # Spiel starten (baut vorher, wenn nötig)
.\sb.ps1 test      # alle Tests, Unity darf offen bleiben
.\sb.ps1 build     # Build nach builds\M1-duel\SHIELDBREAK.exe
.\sb.ps1 assets    # Modelle aus art\specs bauen und prüfen
.\sb.ps1 setup     # einmalig nach dem Klonen
```

`.\sb.ps1` ohne Argument zeigt diese Liste. Die Skripte in `tools/` sind die Innereien.

## Doku

[CLAUDE.md](CLAUDE.md) · [DECISIONS.md](DECISIONS.md) · [IDEAS.md](IDEAS.md) · [Unity-Projekt](game/README.md) · [Code](game/Assets/_Project/Scripts/README.md) · [Tests](game/Assets/Tests/EditMode/README.md) · [Art](art/README.md) · [Tools](tools/README.md) · [Docs](docs/README.md)
