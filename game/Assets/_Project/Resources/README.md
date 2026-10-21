# Resources

Daten, die das Spiel zur Laufzeit per `Resources.Load` findet, in jeder Szene und ohne Verweis aus der Szene.

| Datei | Inhalt | Geprüft von |
|---|---|---|
| `Input/Shieldbreak.inputactions` | jede Eingabe als Action, eine Action-Map pro Feature | `ControlsDocumentationTests` |
| `Input/controls.de.json` | deutsche Beschreibung jeder Action und jeder Map (F1-Menü, Steuerungsseite, Szenenhilfe) | `ControlsDocumentationTests` |
| `SceneCatalog.asset` | alles Spielbare: pro Eintrag Szene, Startart, optional Ersatz-Szenario, Ziel, Eingabe-Maps, Prüfliste. Hauptmenü → Testmodus listet ihn, F2 zeigt den Eintrag der laufenden Szene, Esc → Prüfliste hakt ab | `SceneCatalogTests` |

Neue Szene, neuer Modus oder neues Szenario: hier einen Eintrag im Katalog anlegen (im Inspector), sonst schlägt `SceneCatalogTests` fehl. Prüfpunkte so schreiben, dass Tarik sie ohne Erklärung abarbeiten kann: was tun, was sehen. Ein umformulierter Prüfpunkt gilt als neu und ist wieder offen.
