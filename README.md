# SHIELDBREAK (Arbeitstitel)

Low-Poly Third-Person Tactical Battle Sim. General und Krieger zugleich. 50v50 bis 150v150, deterministische Sim, Lockstep-Multiplayer.

- **CLAUDE.md** — Verfassung des Projekts: Vision, Säulen, Architektur, Meilensteine, Regeln. Zuerst lesen.
- **DECISIONS.md** — Warum etwas so ist, wie es ist.
- **IDEAS.md** — Alles, was nicht im aktuellen Meilenstein ist.
- **docs/** — Research, Posen-Vorgaben, Design-Notizen.
- **tools/** — Setup-Skripte (Windows).
- **game/** — Unity-Projekt (6000.5, URP). Wird von Unity Hub erzeugt, siehe CLAUDE.md Abschnitt 10.

## Erster Start

```powershell
.\tools\setup-windows.ps1
# Unity Hub: New Project > Universal 3D > Location = dieser Ordner, Name = game
.\tools\add-packages.ps1
```

Dann `claude` im Repo-Root starten.
