# IDEAS.md — Parkplatz für alles außerhalb des aktuellen Meilensteins

Regel: Neue Ideen landen hier, nicht im Sprint. Bewertung (Wert / Risiko / Aufwand) erst, wenn der aktuelle Meilenstein sein Fun-Gate bestanden hat.

## Phase 2 (nach M6), architektonisch vorbereitet

- **Doktrinen-UI**: Spieler konfiguriert Formation-Behaviors vor der Schlacht („wenn Kavallerie < 50 m: Schildwall"). Behaviors existieren ab M3, hier kommt nur UI dazu. Mindgame im MP: verdeckte Doktrinen.
- **Gebäude-Destruction**: WorldGrid-Kosten dynamisch ändern, Mesh-Swap in Presentation. Nav ist vorbereitet.
- **Wald als Versteck**: Formationen im Wald sind für den Commander und den Gegner nicht sichtbar, Hinterhalte werden möglich (M4 deckt nur gegen Pfeile)
- **Banner als Moral-Senke**: Die Befehlsreichweite wurde in M3 gespielt und ist raus (`DECISIONS.md` 2026-11-07); offen bleibt ein Banner, das nur die Moral stützt.
- **SDF-Formationen**: Formation als Distanzfeld statt Slotliste; stufenlose Übergänge, Verformung um Hindernisse.
- **PBD-Kohäsion / Druck**: Schildwall mit physischem Gedränge, Charge drückt Linie ein. Hohes Risiko (Determinismus bei Multithreading, Spieler im Constraint-System).
- **GPU-Hit-Detection**: Alle Klingenbahnen gegen alle Capsules im Compute-Shader. Erst wenn CPU-Variante bei 150v150 limitiert.
- **MCTS-Commander**: Rollouts auf Lanchester-Abstraktion. Erst wenn Utility-AI vorhersehbar wird.
- **Ranked / Relay-Server**: Inputs verteilen, Replay prüfen. Nach Launch.
- **Feldherr-Modus**: Schlacht nur aus der Kommandoansicht, kein Held auf dem Feld. Holt Total-War-Spieler ab. Nach M3 fast gratis (alle Systeme existieren).
- **Sandtisch-Aufstellung**: Pre-Battle-Phase als Kriegstisch mit Miniaturfiguren; Kamera taucht danach ins Gelände ab. Dieselbe Draufsicht, anders inszeniert.
- **First-Person-Kamera**: optionaler Schalter; Aufwand liegt in Kopf-/Clipping-Ausblendung und Nahansicht der Waffenposen.
- **Spin-off nach Launch**: eigenständiges Strategiespiel auf derselben Sim (kamera-unabhängige, deterministische Architektur macht es möglich).

## Kampf

- **Kombos über Richtungen (Tarik, 2026-10-18):** Die leichte Kette der einfachen Steuerung wechselt schon rechts/links. Ausbau: Abschluss-Hieb nach N Treffern (z. B. rechts, links, oben), Richtung je nach Kombo-Schritt, so dass der Spieler aus mehreren Richtungen schlägt, ohne sie zu wählen. CLAUDE.md schließt Combo-Ketten für v1 aus, Entscheidung nach dem M1-Fun-Gate.
- Sprung-Angriff vom Pferd (Dismount-Attack)
- Waffen brechen bei Energie über Schwellwert (Schilde zuerst)
- Umgebung als Waffe: Stoß in Abgrund, Fluss verlangsamt
- Finisher-Kamera bei entscheidenden Kills (nur Singleplayer, im Lockstep problematisch)

## Fernkampf

- **Bogen für den Helden** (aus M3 Task 5): Zielen über die Kamera, gehaltene Spannung (Erweiterung 1 im [Papier-Test](docs/combat/papier-test-bogen-lanze.md)), Stamina pro Sekunde
- Armbrust (lange Recovery, weniger Bogen), Wurfwaffen (Tabelle 3.5)
- Quadratisches Lanchester-Modell für Fernkampf, damit der Kommandant Schützen richtig bewertet
- Munition, Pfeile im Boden und in Schilden sichtbar stecken lassen
- Eigenbeschuss als Option für höhere Schwierigkeit

## Taktik

- Hinterhalt-Deployment (Wald versteckt Formation bis Sichtkontakt)
- Wetter mit Wirkung (Regen: Bogen-Reichweite −, Schlamm: Kav-Speed −)
- Nachschlachtbericht als Replay mit Zeitstrahl (Replays sind durch Determinismus gratis)
- Captain-Mechanik: Formation ohne Captain verliert Moral-Senke
- **Sammeln nach der Flucht** (aus M2 Task 5): Fliehende, die weit genug vom Feind weg sind und wieder Mut fassen, sammeln sich und kehren zurück (am Banner oder beim Helden). v1: wer flieht, verlässt das Feld
- **Moral pro Truppentyp**: Veteranen oder Elite brechen später (eigener Faktor auf den Moral-Verlust); hängt an der Progressions-Entscheidung
- **Kämpfender Rückzug**: Formation weicht rückwärts mit Front zum Feind (aus M2 Task 4, heute dreht der Rückzug dem Feind den Rücken zu)
- **Kommandoansicht** (aus M2 Task 7): Verschieben gibt es seit M3 (WASD); offen: am Bildrand schieben, Gruppen speichern und per Taste wählen

## Meta (frühestens nach Setting-Entscheidung)

Die leichte Meta ist am 2026-11-01 entschieden: Kriegszug in M4b (`DECISIONS.md`). Hier liegt, was darüber hinausgeht oder in M4b noch nicht gesetzt ist.

- Leichte Progression: Veteranen-Status für Formationen, die eine Schlacht überleben (Kandidat für M4b)
- **Weltkarte light** (Tarik, 2026-11-01, Bannerlord-Richtung): Knotengraph aus Städten und Lagern mit Reisezeit, Banditen tauchen auf Wegen auf. Erst wenn der Kriegszug als Stationenfolge trägt
- Armee-Editor mit Punktebudget für Skirmish und MP
- Mod-Support (data-driven macht das billig; Shieldwall-Reviews fordern es)
