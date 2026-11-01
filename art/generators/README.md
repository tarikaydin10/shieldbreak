# art/generators

Ein Python-Modul pro Form; assetforge (`tools/assetgen`) lädt sie beim Bauen aus diesem Ordner, danach aus seinen gemeinsamen Generatoren. Bis 2026-11-01 lagen sie in `tools/assetgen/generators/`.

| Generator | Baut | Kategorie |
|---|---|---|
| `sword.py` | Ein- und Zweihandschwert: Knauf, Griff, Parierstange, Klinge mit Spitze | `weapon_onehand`, `weapon_twohand` |
| `spear.py` | Speer: Schaft und Blatt | `weapon_polearm` |
| `round_shield.py` | Rundschild mit Buckel und Rand | `shield` |
| `helmet.py` | Nasalhelm, optional Kamm | `armor_head` |
| `dummy_soldier.py` | Platzhalter-Soldat bis M4, statisch, Arme optional weglassbar für prozedurale IK | `character` |
| `limb.py` | Gliedmaßen-Segment für prozedurale Arme | `prop` |
| `ground_patch.py` | Bodenstück | `environment` |

Schnittstelle: `REQUIRED_COLORS`, `OPTIONAL_COLORS`, `build(b, p, c, rng)` mit `b` als `MeshBuilder` aus `tools/assetgen/lib/meshkit.py`. Regeln in [`../style/style-bible.md`](../style/style-bible.md), Abschnitt Reproduzierbarkeit.
