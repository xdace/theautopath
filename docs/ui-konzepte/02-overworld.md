# Konzept 02 – Overworld: Orientierung und Feedback

> «Wohin laufe ich? Was ist passiert?» – Analyse des aktuellen Stands (IMGUI + world-space Hex-Views) und ein priorisiertes Konzept.
> Pfade relativ zu `Assets/Betaknight/Scripts/`.

## 1. Kernbefund vorweg

Das Spiel hat **kein Ziel, das man auf der Karte suchen kann**: Der Boss wird nicht platziert (`Core/Map/MapGenerationConfig.cs:203-204` verbietet Boss-Felder), sondern taucht alle 25 Züge **beim Ritter** auf (`Core/OverworldSession.Boss.cs:16,23,33-41`). Ein Akt ist also in Wahrheit ein **25-Zug-Countdown zum Vorbereiten**, und die Bossstärke hängt davon ab, *wo man gerade steht*. `TierAt` ist Entfernung zur Mitte + 2 pro Akt (`Core/OverworldSession.Acts.cs:36`), der Boss nutzt `TierAt(cell.Coord)` (`Boss.cs:39`). Spieler wissen das nicht. Das Ziel lautet also nicht «finde den Boss», sondern: **«Werde in N Zügen stark genug und steh dann an einer sicheren Stelle.»** Genau das muss die UI sagen.

## 2. Schwächen im aktuellen Stand (mit Belegen)

**Ziel und Fortschritt**
- Das HUD nennt sich selbst Debug-HUD («wird ersetzt», `Overworld/UI/OverworldHud.cs:19-20`). Es ist ein 380 px breites Panel über die volle Höhe (`:47-51`), das ca. 30 % des Bildschirms abdeckt (bei 1280 px).
- «Boss in N turns» ist eine Textzeile (`:79-81`, `UiTexts.cs:50-51`), rot erst ab ≤ 3. Es gibt keinen Hinweis, was beim Boss passiert (überleben 15 s, Portal) oder dass der Standort zählt.
- Ring bzw. Gefahrenstufe werden nirgends angezeigt, obwohl `TierAt` sie direkt liefert.

**HP und Ressourcen**
- HP ist reiner Text in einer Zeile mit Gold und Shards (`OverworldHud.cs:82`, `UiTexts.cs:52`). Es gibt keinen Balken, keine Farbe bei wenig HP und kein Delta-Feedback bei Schaden.
- Darüber und darunter steht Debug-Inhalt: Platinen-Liste (`:83`, `BoardList()` `:143-166`), Gear, Sets, Tags, **Position als Achsen-Koordinate** (`:101`) und **Seed** (`:103`). Minen-Raids nennen ebenfalls nur Koordinaten «Mine (3, -2)» (`:98-99`, `HexCoord.cs:116`).

**Karte und Kacheln**
- Unerforschte Nachbarn sind nur ein graues «?» (`Views/HexCellView.cs:103-106`). Bei Sichtweite 1 (`Core/Exploration/ExplorationService.cs:16`) weiss man vor dem Schritt nie, ob ein Kampf kommt.
- Die Symbole sind Platzhalter-Buchstaben (`c h s w t x F W S M V ! E $ * G`, README «Feldsymbole») und werden im Spiel nirgends erklärt.
- Kleine Events und leere Felder haben fast dieselbe Farbe: Empty `(0.45, 0.52, 0.45)` gegenüber minorEvent `(0.45, 0.55, 0.45)` (`Overworld/Config/OverworldSettings.cs:60` gegenüber `:70`).
- Erledigte Felder werden nur abgedunkelt (`HexCellView.cs:118-122`, Text-Alpha 0,35). Ob etwas geplündert, besiegt oder verloren wurde, sieht man nicht.
- Die Gefahr unterscheidet sich nur über die Farbe (rot «!» gegenüber pink «E»). Die Gegnerstufe pro Feld sieht man nicht.

**Route**
- Die Routen-Vorschau ist nur eine gelbe Einfärbung (`Views/HexGridView.cs:218-231`, `HexCellView.cs:136-140`). Es fehlen Kosten («1 Zug»), Stopps und Warnungen (Reise hält an auf Feind oder Neuland, `Core/OverworldSession.cs:38,201`).
- Ein ungültiges Ziel wird rot ohne Begründung (`HexGridView.cs:233-241`). `PlanRoute` liefert nur null (`OverworldSession.cs:365`), der `MoveFailure`-Grund (`Core/Movement/MovementRules.cs:25-37`, z. B. Hidden) geht verloren (`Controllers/OverworldController.cs:100-102`).
- Die Hover-Info steht **am Ende der scrollenden HUD-Spalte** (`OverworldHud.cs:105-113`), also unter Board-Liste, Gear, Sets und Tags. Bei einem vollen Board ist sie aus dem Sichtfeld gescrollt, und ausgerechnet die Gegner-Vorschau (`DrawExactEnemies` `:202-220`) liegt dort.

**«Was ist passiert?»**
- Ergebnisse erscheinen als Text unten links, **4 s**, maximal 5 Zeilen (`UI/EncounterWindow.cs:15-16,49-53,104-113`). Danach sind sie weg, und es gibt keinen Verlauf.
- **Bug-artig:** Kampfergebnisse (`MajorEventResolved` in `OverworldSession.MajorEvents.cs:69,106`, Boss in `Boss.cs:63`) werden gepostet, *während die Arena öffnet*. Die Meldungen sind dann ausgeblendet (`OverworldBootstrapper.cs:85`, `_encounterWindow.Hidden = arenaOpen`), laufen aber über `Time.time` weiter ab. Nach einer Arena-Playback von mehr als 4 s sind Elite-Gold, Chip-Drop, Board-Erweiterung, Modul und Evolutionen nach dem Boss **nie sichtbar gewesen**. Die Arena zeigt nur «−HP +Gold» (`UI/ArenaWindow.cs:1260`).
- Kleine Events (Thorns −1–2 HP, Herbs, `Core/Encounters/EncounterCatalog.cs:50-54`) wirken still. Es gibt kein Feedback auf der Kachel selbst (keine Floating-Number, kein HP-Flash).
- Beim Eintreffen des Boss gibt es keine Vorwarnung oder Inszenierung: `TriggerBossIfDue` startet den Kampf direkt nach dem Schritt (`OverworldSession.cs:244`, `Boss.cs:33`).

## 3. Konzept

### 3.1 Top-Bar (ersetzt den Info-Block des HUD) – **P1 · M**
Neue Klasse `UI/OverworldTopBar.cs` (IMGUI, volle Breite, 44 px hoch). Inhalt aus `OverworldHud.cs:77-82` übernehmen. Board, Gear, Sets und Tags wandern in Build/Inventar (sind dort schon vorhanden). Position und Seed kommen in ein Debug-Toggle (F1).
- **HP-Balken** 200 px: Farbe grün > 50 %, gelb > 25 %, sonst rot und pulsierend. Bei Änderung ein Ghost-Balken (weisser Rest, der 0,6 s nachläuft) und ein «−3»-Popup. Der Wert wird pro Frame mit dem letzten verglichen, Session-Events sind nicht nötig.
- Gold, Shards «2/3 → Rune» (Fortschritt zu `ShardsPerRune`), Inventar 8/12.
- **Act-Block:** «Act 2 · Turn 31 · Ring 3/6 (Tier 5)».
- **Boss-Uhr:** Segmentbalken mit 25 Segmenten «Boss in 7». Ab ≤ 5 orange, ab ≤ 2 rot mit Text «Boss arrives next turn – fight here at Tier X». X = `TierAt(Player.Position)`.
- Buttons Build (B) / Inventory (I) / Journal (J) rechts. `OverworldHud.ContainsScreenPoint` (`:59-63`) muss die neuen Rects kennen.

### 3.2 Objective-Panel (oben rechts) – **P1 · S**
Drei Zeilen, generiert in einer neuen Core-Methode `OverworldSession.Objectives()` (testbar, Unity-frei):
1. **Hauptziel:** «Survive the Boss in 7 turns (15 s) → Portal to Act 3».
2. **Tipp zum Standort:** «Boss tier = distance from center. Here: Tier 5 · Center: Tier 2».
3. **Nebenziele (dynamisch):** «Mine (NE) under attack – 4 turns», «Shop seen (2 tiles W)», «Elite nearby: board upgrade 50 %».
Richtungen statt Koordinaten: Helfer `HexDirection` aus Delta zu Spieler → «NE, 3 tiles».

### 3.3 Kachel-Lesbarkeit und Legende – **P1 · M**
- `HexCellView`: zusätzlich ein **Gefahren-Pip** (kleine TextMesh-Zeile unter dem Symbol: «T5» oder ●●○) für Enemy/Elite/Raid. Die Stufe kommt über eine neue Session-Methode `DangerAt(coord)` (gleiche Logik wie `BoardView.cs:101-103`). Farbe relativ zur eigenen Stärke (siehe 3.6).
- Unterschiedliche Formen statt nur Farben: Kampf mit rotem Rand (zweites Hex-Sprite, leicht grösser, `ProceduralSprites.Hex`), Event-Entscheidung mit violettem Rand, Laden/Truhe mit goldenem Rand. Die Farbe `minorEventColor` deutlich vom leeren Feld trennen (`OverworldSettings.cs:70`).
- Erledigt-Zustand mit Häkchen bzw. «✕» (besiegt) statt nur Dimmen. Verlorene Mine mit eigenem Symbol.
- **Legende** einklappbar unten rechts (Taste L). Ausserdem zeigt der erste Hover über ein neues Symbol eine einmalige Tooltip-Karte («$ Shop – buy, reroll, lock»). Die Texte kommen als `UiTexts.Legend` aus der README-Tabelle.
- Mittelfristig (P3 · L): echte Icons statt Buchstaben (prozedural oder Sprite-Atlas).

### 3.4 Routen-Vorschau mit Kosten und Risiken – **P1 · M**
- `OverworldSession.PlanRoute` um `RoutePreview PreviewRoute(target)` ergänzen: Schritte, Zugkosten (immer 1 bzw. Schritte bis zum ersten Stopp), **Stopp-Grund** (Neuland, Feind, Mine) und `MoveFailure`-Grund bei Ungültigkeit (`MovementRules.CheckEnterable`).
- `HexGridView.ShowRoute`: Pfeil bzw. Punktspur statt Flächenfärbung. Das Zielfeld mit Rahmen, der Stopp-Punkt mit «■».
- **Cursor-Tooltip** direkt an der Maus (nicht im HUD): «Travel · 1 turn · stops at ‹!› Tier 4» oder rot «Can't reach – hidden tile, explore a neighbour first». Bei «?»-Feldern: «Unknown · Ring 4: 40 % chance of a big event (fight, chest, mine)» (Daten aus `MapGenerationConfig.distanceBands`).
- Die Hover-Gegnervorschau zieht aus der HUD-Spalte in den Cursor-Tooltip bzw. ein schwebendes Panel neben der Kachel (`DrawExactEnemies` wiederverwenden, `OverworldHud.cs:202`).

### 3.5 Toasts und Journal – **P1 · M** (der Bugfix allein: **S**)
- **Sofort-Fix:** In `EncounterWindow` Meldungen nicht nach `Time.time` ablaufen lassen, solange `Hidden()` true ist (Ablaufzeit beim Wiedereinblenden neu setzen). Alternativ Meldungen in eine Warteschlange stellen und erst nach der Arena zeigen.
- **Toasts** oben mittig unter der Top-Bar, gestapelt, 6 s, mit Kategorie-Farbstreifen (Kampf, Event, Belohnung, Warnung) und Icon. Zahlen-Deltas fett («−3 HP», «+8 Gold»). Bei Kampf und Boss ein **Ergebnis-Panel** nach der Arena: alle `MajorEventOutcome.Lines` als Liste, Knopf «OK».
- **World-Feedback:** Floating Text über der Kachel beim kleinen Event (`PlayerView`-Position + TextMesh, 1 s aufsteigend).
- **Journal (J):** neue Klasse `UI/JournalWindow.cs`. Datenquelle ist ein Core-Log `OverworldSession.Journal` (List<JournalEntry{Turn, Coord, Title, Lines, Kind}>), das an denselben Stellen befüllt wird, die heute `EncounterResolved`, `MajorEventResolved`, `MineRaidStarted`, `ChipGained` und `BuildImproved` feuern. Es überlebt den Akt-Wechsel (`CreateNextAct`). Darstellung gruppiert nach Zug, Filter Alles/Kämpfe/Beute/Warnungen. Klick auf einen Eintrag pingt die Kachel (Kamera-Pan). Nebeneffekt: Das Autoplay bzw. die Tests können das Journal ebenfalls auswerten.

### 3.6 Kampf-Vorschau vor dem Betreten – **P2 · M**
- Beim Hover über ein bekanntes Kampffeld ein Panel neben der Kachel: Gegnername, Platine (`EnemyBoardView.Draw`), Loot, und neu eine **Einschätzung**: «Expected: Easy / Even / Risky / Deadly». Kurzfristige Heuristik: Vergleich von Gegner-HP×DPS mit eigenen BuildStats. Besser: **N=20 Probe-Simulationen** über `ArenaCombatResolver` mit fixen Seeds im Hintergrund, Ergebnis «Win ~85 %, ~−6 HP», gecacht pro Feld (`_enemyCoord`-Cache existiert, `OverworldHud.cs:168-192`).
- Klick auf ein Kampffeld verlangt eine **Bestätigung** («Fight Elite (Tier 7)? Risky · HP 18/40 – [Fight] [Cancel]»), wenn die Einschätzung Risky oder Deadly ist oder HP < 30 %. Umsetzung in `OverworldController.Update` vor `StartCoroutine(Travel…)`.
- Boss-Vorwarnung: Bei `TurnsUntilBoss == 1` zeigt jede Route den Hinweis «The Boss will attack at the destination (Tier X)». Der Boss-Kampf beginnt mit einer Einblendung «The Boss arrives! Survive 15 s».

### 3.7 Übersicht / Kompass – **P2 · S–M**
Ein Minimap-Ersatz ist hier billiger als echte Navigation: Die Karte (Radius 6) passt fast ganz ins Bild, nur das HUD verdeckt sie. Daher:
- **Taste Tab / Mausrad:** Zoom-out-Übersicht (`CameraFollow2D` mit zweitem `orthographicSize`, Zentrum = Kartenmitte). Dazu **Ring-Linien** (6 dünne Hex-Ringe, LineRenderer) mit Tier-Beschriftung, damit «weiter raus = härter» sichtbar wird.
- **Randpfeile** für wichtige Ziele ausserhalb des Bildes: angegriffene Mine (rot, «4 turns»), bekannter Shop, unerkundete Richtung mit den meisten «?».

### 3.8 Onboarding-Hints – **P2 · S**
Kontextuelle Einmal-Hinweise (gespeichert über `PlayerPrefs`, analog `PlayerPrefsRecipeBookStore`), als Toast mit «Got it»:
1. Start: «Click a ? next to you to explore. Every trip costs 1 turn.»
2. Erster Hover über eine besuchte Kachel weiter weg: «Travelling over known tiles costs only 1 turn.»
3. Boss ≤ 10: «The Boss comes to YOU. Distance from center = difficulty.»
4. Erste Shards: «3 Shards = Rune choice.»
5. Erster Mine-Raid: «Return within 6 turns or lose the mine.»
6. Nach dem ersten Kampf: «Open Build (B) to place what you salvaged.»

### 3.9 Event-Fenster – **P3 · S**
`EncounterWindow.DrawPrompt` (`:79-102`): Die aktuellen HP und Gold im Fenster anzeigen. Bei ausgegrauten Optionen den Grund nennen («needs 6 Gold»). Optionen mit Risiko (−HP) rot markieren, wenn sie unter 25 % HP führen würden.

## 4. Wireframe

```
┌───────────────────────────────────────────────────────────────────────────────────────────┐
│ HP ████████████░░░░ 28/40   ◎ 34 Gold   ◆ 2/3 Shards   ▣ 8/12  │ ACT 2 · Turn 31 · Ring 3/6 │
│                                                               │ BOSS ▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮▮▯▯▯▯▯▯▯ 7 │
│                                                [Build B] [Inventory I] [Journal J]  [≡]   │
├───────────────────────────────────────────────────────────────────────────────────────────┤
│           ┌─ Toast ───────────────────────────┐              ┌─ OBJECTIVE ───────────────┐│
│           │▌Fight won: Rust Hound  −4 HP +7 Gold│            │ ★ Survive the Boss in 7    ││
│           └───────────────────────────────────┘              │   turns → Portal (Act 3)   ││
│                                                              │ ⚑ Boss tier here: 5        ││
│              ?     ?                                         │   (center: 2)              ││
│          ?    ‹!›T6   ?          ┌ Rust Hound · Tier 6 ────┐  │ ! Mine NE 3 tiles: 4 turns ││
│        ·   [E]T8  ·  ?           │ [enemy board grid]      │  └────────────────────────────┘│
│      $    ·  ◉ ─ ─ ▶ ‹!›         │ Loot: Shock Stab, …     │                                 │
│        ✓    h    ·   ?           │ Expected: RISKY ~60 %   │                                 │
│          ✓    ·    ?             │ Travel: 1 turn · stops  │                                 │
│                                  └─────────────────────────┘                                │
│  ◄ Shop (W)                                                          Mine under attack ► !  │
│                                                                         ┌ Legend (L) ─────┐ │
│                                                                         │ ? unknown  ! foe │ │
│                                                                         │ E elite   $ shop │ │
│                                                                         │ * chest  G mine  │ │
│                                                                         └─────────────────┘ │
└───────────────────────────────────────────────────────────────────────────────────────────┘
 Journal (J): Turn 31 ▸ Fight won: Rust Hound · −4 HP · +7 Gold · Salvaged Shock Stab
              Turn 30 ▸ Thorn Thicket · −2 HP     Turn 29 ▸ Gold Mine (NE) captured · +3 Gold
```

## 5. Priorisierung

| Prio | Massnahme | Aufwand | Wo |
|---|---|---|---|
| P1 | Meldungen während der Arena nicht verfallen lassen, Ergebnis-Panel nach dem Kampf | S | `EncounterWindow.cs:49-53,104-113`, `OverworldBootstrapper.cs:85` |
| P1 | Top-Bar mit HP-Balken, Boss-Uhr, Act/Ring/Tier; Debug-Infos weg | M | neue `OverworldTopBar`, `OverworldHud.cs:75-116` |
| P1 | Objective-Panel inkl. Boss-Standort-Hinweis | S | `OverworldSession.Objectives()`, `Acts.cs:36` |
| P1 | Cursor-Tooltip: Route-Kosten, Stopp, Ungültig-Grund, Gegnervorschau | M | `OverworldController.cs:88-103`, `HexGridView.cs:218-241`, `PreviewRoute` |
| P1 | Kachel-Lesbarkeit: Tier-Pip, Ränder, Farbtrennung, Erledigt-Marker, Legende | M | `HexCellView.cs:86-167`, `OverworldSettings.cs:60-76` |
| P1 | Journal (Core-Log + Fenster) | M | neu `OverworldSession.Journal`, `UI/JournalWindow.cs` |
| P2 | Kampf-Einschätzung + Bestätigung bei Risiko | M | `OverworldHud.EnemyBoards`, `OverworldController.Update` |
| P2 | Zoom-Übersicht, Ring-Linien, Randpfeile | S–M | `CameraFollow2D`, `HexGridView` |
| P2 | Onboarding-Hints | S | neu `HintService` + PlayerPrefs |
| P2 | World-Floating-Text bei kleinen Events, HP-Ghost-Balken | S | `PlayerView`, TopBar |
| P3 | Event-Fenster mit Ressourcen und Gründen | S | `EncounterWindow.DrawPrompt` |
| P3 | Echte Icons statt Buchstaben | L | `ProceduralSprites`, `HexCellView` |

**Empfohlene Reihenfolge:** Bugfix Toasts → Top-Bar + Objective → Tooltip/Route → Kacheln/Legende → Journal → Einschätzung. Nach den ersten drei Schritten sind beide Owner-Fragen beantwortet: «wohin?» (Objective, Tier, Route-Tooltip) und «was ist passiert?» (Toasts, HP-Feedback).
