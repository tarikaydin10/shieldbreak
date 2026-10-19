# Debug (Assembly `Shieldbreak.DebugTools`)

Werkzeuge für die Debug-Draufsicht (`M0_Sandbox`): auswählen, Befehle geben, sehen, was die Sim tut. Ändert die Sim nur über Befehle in der `InputQueue`.

| Datei | Inhalt |
|---|---|
| `SandboxOrders` | Auswahl (Klick, Rahmen, Teams), Formationsbefehle (Klick = hinmarschieren, Ziehen = Frontlinie, 4–8 = Form, H = anhalten) über `FormationOrder` und `FormationLinePlanner`; lose Soldaten bekommen Einzelziele wie in M0 |
| `SimDebugOverlay` | Grid, Kartenrand, Gelände (gesperrt/teuer), Geschwindigkeiten, Ziele der Auswahl, Formationen (Slots, Anker mit Blickrichtung, geplanter Weg, Marschziel mit künftiger Front, Typ-Beschriftung) |
| `DebugLines` | GL-Linien auf dem Boden (Linie, Kreuz, Pfeil), immer obenauf |

Jede Taste ist eine Action in den Maps `DebugOrders` und `DebugOverlay` (Beschreibung in `controls.de.json`, im Spiel unter F1).
