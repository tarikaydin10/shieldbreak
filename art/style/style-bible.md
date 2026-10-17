# Style-Bible — SHIELDBREAK

Verbindlich für jedes Asset. Maschinenlesbare Regeln stehen in `style.json` und `palette.json`; `tools/assetgen/validate.py` prüft sie hart. Dieses Dokument erklärt das Warum.

## Grundsatz

Konstanz entsteht durch Einschränkung, nicht durch Geschmack. Kein Asset wird frei modelliert. Jedes finale Asset entsteht aus einer Spec (`art/specs/*.json`) und einem Generator (`tools/assetgen/generators/`) und besteht den Validator. Wer (Mensch, Claude, anderes Modell) die Spec schreibt, ist egal.

## Farbe

- **Eine Palette, eine Textur, ein Material** (`M_Palette`). Keine eigenen Texturen, keine Vertex-Farben.
- Jede Fläche hat genau **eine** Palettenfarbe (alle UVs der Fläche auf der Mitte eines Palettenfelds).
- Farben werden in Specs nur **per Name** referenziert (`"head": "iron"`), nie als Hex.
- Teamfarben (`team_a*`, `team_b*`) nur auf Flächen, die aus 40 m die Seite verraten sollen (Schildfront, Wappenrock, Helmbusch).
- Palette nur erweitern oder einen Farbwert ändern, nie umsortieren: UVs zeigen auf Indizes.

## Form

- **Flat Shading** überall. Facetten sind der Look, keine Glättung.
- **Überzeichnete Silhouetten** für Lesbarkeit (Säule 3): Speerspitzen, Schildbuckel, Helmkanten etwas größer als historisch. Schaft nicht dünner als ~4 cm Durchmesser.
- Wenige Segmente: Zylinder 6–8, Kugeln 8×4, Scheiben 10–12. Lieber eine klare Kante als drei weiche.
- **Polygon-Budgets** pro Kategorie (Speer 300 Tris, Schild 400, Helm 350, siehe `style.json`).

## Maßstab und Ausrichtung

- 1 Blender-Einheit = 1 m. Maße im Validator gegen Kategorie-Spanne geprüft.
- **Waffen:** entlang +Z (Blender) = +Y (Unity), Griffpunkt im Ursprung.
- **Schilde:** Front zeigt nach −Y (Blender) = +Z/forward (Unity), Griff im Ursprung.
- **Helme:** Ursprung mittig an der Unterkante des Stirnreifs (Befestigungspunkt am Kopf).
- **Props, Umgebung:** Ursprung unten mittig.

## Licht und Nachbearbeitung

Assets werden nicht „schön" gerendert, sie werden vom gemeinsamen URP-Volume-Profil getragen (LUT, leichter Bloom, SSAO, Höhennebel). Der Kontaktbogen (`art/contact-sheet.png`) nutzt festes Licht und feste Kamera, damit Ausreißer sofort auffallen.

## KI-generierte Meshes

Text-/Bild-zu-3D (z. B. `generate_3d` im Blender MCP) liefert nur Rohmaterial. Es kommt erst ins Spiel, wenn ein Normalize-Generator es dezimiert, flach schattiert, auf die Palette backt und der Validator grün ist.

## Ablauf

1. Spec in `art/specs/` anlegen oder ändern.
2. `.\tools\assetgen\run.ps1` → baut alle Specs, validiert, exportiert FBX nach `game/Assets/_Project/Art/Generated/`, schreibt `manifest.json` und `art/contact-sheet.png`.
3. Kontaktbogen ansehen. Passt ein Asset nicht neben die anderen: Generator oder Regel ändern, nicht das Asset von Hand.
4. `.\tools\assetgen\run.ps1 -Check` → baut neu und vergleicht Geometrie-Hashes mit dem Manifest (Reproduzierbarkeit).

## Im Spiel prüfen

`Scenes/Proto_Style.unity` zeigt alle Assets mit Licht, Nebel und dem Post-FX-Profil `Art/PostFX/VP_Style.asset`. Tasten 1–5 wechseln zwischen Nahkampf (4 m), Kampf (15 m), Lesbarkeit (40 m), Kommandoansicht und Asset-Reihe; rechte Maustaste dreht, Mausrad zoomt, O startet einen Orbit. Neu bauen über **Shieldbreak → Art → Build Proto_Style Scene**, Screenshots aller Ansichten über **Capture Proto_Style Shots** (`game/Logs/proto-style/`).

## Reproduzierbarkeit (Regel für Generatoren)

Generatoren nutzen nur `lib/meshkit.py`. Neue Grundformen dort selbst konstruieren statt über bmesh-Ops mit undefinierter Ausgabereihenfolge (Beispiel: `create_uvsphere` lieferte Flächen in wechselnder Reihenfolge; `-Check` hat das gefunden). Zusätzlich sortiert `to_object` alle Flächen kanonisch. Zufall nur über das übergebene `rng` (Seed aus der Spec).
