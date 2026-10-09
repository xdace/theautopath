# UI-Gesamtkonzept: Übersicht und Lesbarkeit

Zusammenführung der vier Detailkonzepte in `docs/ui-konzepte/`:
[01 Arena](ui-konzepte/01-arena.md) · [02 Overworld](ui-konzepte/02-overworld.md) · [03 HUD & Build](ui-konzepte/03-hud-build.md) · [04 Stil](ui-konzepte/04-style.md).

## Die drei Kernprobleme

1. **Der Kampf ist zu schnell und zerstreut.** Ursache (Relais auf der Platine), Wirkung (Zahl über dem Gegner) und Erklärung (Log) liegen an drei Orten. Kämpfer sind kleine Rechtecke mit einem 12-px-Balken, Gegnerangriffe sind kaum angekündigt, das Ende springt sofort in den Bericht.
2. **Die Karte sagt nicht, worum es geht.** Der Boss steht nicht auf der Karte: Er kommt nach 25 Zügen **zum Ritter**, und seine Stärke hängt davon ab, wie weit man von der Mitte entfernt steht. Ein Akt ist also ein Countdown zum Vorbereiten. Das sagt die UI nirgends. Dazu kommen ein Debug-HUD statt einer Statusleiste ohne HP-Balken und Meldungen, die nach 4 s verschwinden. **Bug:** Kampfergebnisse verfallen, während die Arena offen ist.
3. **Farben und Symbole bedeuten zu viel.** Gelb hat rund 12 Bedeutungen, Angriff-Rot ist gleich „nicht versorgt“-Rot, und eine Komponente wechselt zwischen den Fenstern ihre Farbe. Viele Symbole (◆ ✖ ✔ ⚡ ↪ ⏳ ✦ ★) fehlen in Unitys Standardschrift. Das Flackern liegt mit 5–10 Hz über der Empfehlung für Lichtempfindliche (≤ 3 Hz).

## Leitregeln

- **Ein Fokus pro Bildschirm.** Arena: das Duell. Karte: Ziel + Route. Build: die Platine.
- **Jede Aktion ist ein Satz:** Ursache → Komponente → Ziel → Wirkung, an einer Stelle groß lesbar.
- **Farbe hat eine Bedeutung:** Farbton = Skill-Art, Rahmen/Animation = Zustand, Weiß = Fokus, Gold = nur Gold.
- **Nichts Wichtiges verschwindet ungesehen:** Meldungen werden gepuffert, es gibt einen Verlauf (Journal), das Kampfende bleibt stehen.
- **Nur sichere Zeichen:** Jedes Symbol hat einen ASCII-Ersatz.

## Umsetzungsreihenfolge

| Etappe | Inhalt | Löst |
|---|---|---|
| 1 | **Arena-Tempo:** 0.5×, neues 1× = halbes Tempo, Tasten (Space Pause, 1–4 Tempo, N nächste Aktion, . ein Schritt), Tempo wird gemerkt, Sieg/Niederlage bleibt 1,5 s stehen, Warteschlange in Auslöse-Reihenfolge angezeigt | „zu schnell“ |
| 2 | **Arena-Lesbarkeit:** „JETZT“-Karte (Relais ⇒ Komponente → Ziel −Schaden), große HP-Balken direkt am Kämpfer mit Ghost-Schaden und Schild, Gegner-Telegraph „⚠ Ram in 0.8 s → Ritter −25“ mit exaktem Schaden | „was passiert?“ |
| 3 | **Top-Bar:** HP-Balken, Gold, Shards-Pips, Akt/Zug, Boss-Uhr, Gefahrenstufe hier; Ziel-Panel („Überlebe den Boss in 7 Zügen · Stufe hier 5, Mitte 2“); Debug-Infos weg (F3), „New Run“ mit Rückfrage | „kein HP-Balken“, „wohin?“ |
| 4 | **Meldungen:** Bugfix (kein Verfall während der Arena), Toasts oben rechts mit Hintergrund, **Journal** (Taste J) aus einem Core-Log, das Aktwechsel überlebt | „was ist passiert?“ |
| 5 | **Karte:** Cursor-Tooltip mit Reisekosten, Stopp-Grund und Grund für ungültige Ziele; Gefahrenstufe pro Kampffeld; kleine Events farblich von leeren Feldern getrennt; Legende (Taste L) | „wohin?“ |
| 6 | **Stil:** Farb-Tokens, sichere Symbole mit Ersatz, Flackern ≤ 3 Hz, Skill-Art als farbige linke Kante überall (statt Zeilenfarben in der Arena), „nicht versorgt“ grau statt rot | Konsistenz |
| 7 | **Build-Fenster:** Detailpanel für das gewählte Teil, Listen unter der Platine entfallen, Verknüpfen-Modus als Banner mit Abbrechen | Dichte |

Später (P2/P3 aus den Detailkonzepten): Zeitlupe/Hit-Stop vor Schlüsselmomenten, Strahl Komponente → Ziel, Zeitleiste nach dem Kampf mit Replay-Sprung, Kampf-Einschätzung vor dem Betreten, Zoom-Übersicht mit Ring-Linien, Codex/Glossar („?“), Fenster-Manager mit einem Modal, Karten statt Text-Buttons in Belohnungen/Shop, prozedurale Pixel-Icons, eigene Schrift.
