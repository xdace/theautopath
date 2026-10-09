# Konzept 01 – Arena: Kämpfe für Zuschauer lesbar machen

> «Der Kampf ist aktuell sehr schnell, ich als Zuschauer habe es sehr schwierig zu folgen, was überhaupt passiert.»

**Kernidee:** Die Arena ist eine **Wiedergabe** (`BattlePlayback` spielt ein fertiges `BattleResult` ab). Der ganze Kampf ist also im Voraus bekannt. Diesen Vorteil nutzt die Anzeige heute kaum. Wir können vorausschauen, also langsamer werden, *bevor* etwas Wichtiges passiert, Angriffe ankündigen, deren Schaden schon feststeht, und Aktionen zu einer Geschichte zusammenfassen. Die Information ist vollständig vorhanden, es fehlt die **Regie**.

---

## 1. Ist-Analyse: wo der Zuschauer heute verliert

| # | Schwäche | Beleg |
|---|---|---|
| 1 | **Echtzeit ohne Regie.** Die Zeit läuft linear: `ticks = unscaledDeltaTime × 20 × Speed`. Es gibt weder Zeitlupe noch Hit-Stop, kein Pausieren bei Ereignissen und keinen Einzelschritt. Speeds sind nur `{1,2,4}`, kein Wert unter 1×. Tasten gibt es keine (nur Buttons). | `ArenaWindow.cs:26`, `:221`, `:1272-1277` |
| 2 | **Gleichzeitiges verschmilzt.** Alle Events bis zum Tick werden in einem Frame angewendet (`Advance` → `Apply`-Schleife). Relais, Einreihen, Start, Schaden und Status erscheinen im selben Augenblick. Hervorhebungen dauern nur 0,35 s (Minimum) bzw. 0,5 s Spielzeit. | `BattlePlayback.cs:412-413`, `:260`; `ArenaWindow.cs:29` |
| 3 | **Kämpfer sind kleine Rechtecke** (max. 70×105 px). Der HP-Balken ist 12 px hoch und hängt **oben** (`area.y+64`), weit weg vom Körper. Name und HP stehen in 13 pt darüber. Ritter und Gegner unterscheiden sich nur durch die Farbe. Es gibt keine eigene Schild-Anzeige: Shield Wall ist nur ein 64×30-Statuskästchen. | `ArenaWindow.cs:392-393`, `:419-425`, `:567-583` |
| 4 | **«Was passiert gerade» ist ein 13-pt-Label** unter dem HP-Balken: «Shock Stab (Cast 0.4 s, ↪ from #3)». Es fehlen das **Ziel**, das **auslösende Relais** und der Puls als Ursache: `ActionCause.Pulse` wird hier nicht ausgewertet, obwohl `BattleEvent` `Target` und `Relay` kennt. `FighterView` speichert beides nicht. | `ArenaWindow.cs:428-441`; `BattlePlayback.cs:448-455`; `BattleEvent.cs:154-163` |
| 5 | **Ursache und Wirkung liegen an drei Orten.** Das Relais leuchtet rechts auf der Platine (`DrawCircuit`), der Schaden fliegt links auf der Bühne (`DrawPopups`), die Erklärung steht unten im Log. Es gibt keine visuelle Verbindung. Die Zuordnung läuft nur über 8 Zeilenfarben und einen 52 px breiten Farbchip mit 55 % Alpha hinter der Zahl, und die muss man auswendig kennen. | `ArenaWindow.cs:616-739`, `:600-607`, `:33-43` |
| 6 | **Gegner-Zahlen tragen keine Herkunft** (`Row = -1` für Nicht-Spieler). Gegnerische Basisangriffe erscheinen gar nicht im Log. | `BattlePlayback.cs:436`, `:466-467` |
| 7 | **Telegraphen sind kaum sichtbar.** Das Ausholen ist ein 6 px hoher Balken. «charging» erscheint erst ab 1 s Cast (`ChargeThreshold = Ticks.PerSecond`). Es gibt keine Zielanzeige, keine Schadenserwartung und keinen Hinweis auf die *nächste* Gegneraktion. Die Gegner-Warteschlange wird bewusst nicht gezeigt. | `ArenaWindow.cs:428-434`; `SkillDefinition.cs:52`; `BattlePlayback.cs:540` |
| 8 | **Die Warteschlange ist eine Textzeile** (13 pt, «Waiting: #2 … ⏳0.4 s · #4 …»). Ihre Reihenfolge ist die Lesereihenfolge (`Insert` nach Row-Index), die Keyword-Tabelle im README sagt dagegen «in the order they were triggered». Das ist ein Widerspruch, den der Zuschauer nicht auflösen kann. | `ArenaWindow.cs:645-647`; `BattlePlayback.cs:544-546`; `RowQueue.cs:6-8` |
| 9 | **Die Zahlen überdecken sich.** Mehrere Treffer innerhalb von 0,25 s werden nur um 18 px versetzt. Es gibt keine Summierung, auch bei 4× nicht, und keine Abstufung nach Wichtigkeit. Nur der Krit ist grösser. | `ArenaWindow.cs:266-270`, `:595` |
| 10 | **Das Live-Log ist ein Ticker ohne Struktur.** Es zeigt die letzten N Zeilen ohne Scrollen und ohne Gruppierung pro Aktion. Zeitstempel, Relais-Label und Komponentennummer stehen in jeder Zeile. Es nimmt die volle Breite ein und konkurriert mit der Bühne. | `ArenaWindow.cs:1213-1217`; `BattleLogText.cs:28-34` |
| 11 | **Das Ende ist abrupt.** Mit `IsFinished` ersetzt der Report sofort die Bühne. Es gibt keinen Todesmoment und kein «Victory»-Halten. Nachher gibt es keine Zeitleiste und keinen Sprung zu einem Zeitpunkt, nur Tabelle und Log. | `ArenaWindow.cs:330-339` |
| 12 | **Erklärungen stecken in Tooltips** (Relais, Komponenten, Gegner). Hovern während der Wiedergabe pausiert nichts, und der Zustand ändert sich unter der Maus. | `ArenaWindow.cs:1022-1064` |

---

## 2. Zielbild

Drei Regeln:

1. **Eine Bühne, ein Fokus.** Der Blick gehört dem Duell Ritter gegen Gegner. Die Platine erklärt nur das *Warum*.
2. **Jede Aktion ist ein Satz** (Ursache → Komponente → Ziel → Wirkung). Sie erscheint als Karte, als Linie und als Zahl, immer in derselben Zeilenfarbe.
3. **Die Regie bestimmt das Tempo, nicht die Uhr.** Ruhige Phasen laufen schneller, Schlüsselmomente langsamer.

### Wireframe (1920×1080, Kampf läuft)

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ Arena – Scrap Rat ×2        0:07.4 / ~0:12        ⚠ Thermal 2         [?] Legende         │
├───────────────────────────────────────────────────────────┬──────────────────────────────┤
│  ┌─ JETZT ─────────────────────────────────────────────┐  │ CIRCUIT BOARD (kompakt)       │
│  │▌[On Hit ◆] ⇒ #2 Shock Stab ⚡4/4  →  Scrap Rat  −12 │  │ ┌────┬──────────┬────┐        │
│  └─────────────────────────────────────────────────────┘  │ │OnHit│#2 Shock ▶│Core│        │
│   ▌Clock 2s ⇒ #1 Drill Thrust → Scrap Rat −8   (blasser)  │ │ ×3 ═╪══ Stab ══╪═══╪══╗     │
│                                                           │ ├────┼──────────┼────┤  ║     │
│   RITTER                         ⚠ RAM in 0.8 s → −25     │ │#3  │#4 Shield │Clk │  ║     │
│   ███████████████░░░ 84/120      ████████░░░░ 31/60       │ │⧗1  │ Bash ⧗2  │2s  │  ║     │
│   ▓▓▓▓ Schild 20                 ▓▓▓▓▓▓▓▓░░ (Ausholen)    │ └────┴──────────┴────┘  ║     │
│   [Burn 2s][Haste]               [Stun 1s] [⛓ Jam]        │ NÄCHSTE: ①#3 Spark ②#4 Bash   ║
│                                                           │ ↓ Basic Attack (füllt Lücken)  ║
│      ┌──┐                 −12 ✦                ┌──┐ ┌──┐  │                         ║     │
│      │♞ │  ══════════════════════════════════▶ │🐀│ │🐀│  │◀════ Strahl: Komponente → Ziel │
│      └──┘                                      └──┘ └──┘  │                              │
│ ▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔▔ │ Gegner-Platine: Hover/Toggle  │
├───────────────────────────────────────────────────────────┴──────────────────────────────┤
│ ▁▂▃▅▆▇ HP-Verlauf  ●──●───●──◆──●───────▲(jetzt)─────────────      (Mini-Zeitleiste)      │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ [⏸ Space] [⏭ Aktion N] [·Tick .]  Tempo: [0.5×][1×][2×][4×]  ☑Zeitlupe ☑Pause bei Krit/Kill│
│ ☐Pause bei jeder Aktion   Log ▾ (einklappbar)                                [Skip ▸▸]   │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

Die Bühne wächst auf etwa 65 % der Höhe. Das Log wird zur einklappbaren Schublade (Standard: zu, eine Zeile Vorschau). Gegner-Platinen stehen nicht mehr fest auf der Bühne (`DrawSide`, `ArenaWindow.cs:397-406`). Sie öffnen sich per Hover oder Toggle als Overlay, so bekommen die Kämpfer Platz.

---

## 3. Vorschläge

### 3.1 Tempo und Regie (`PlaybackDirector`)

Eine neue, Unity-freie Klasse `PlaybackDirector` in `Core/Arena/Playback/` liest `BattleResult.Events` **voraus** und liefert für jeden Tick einen Zeitfaktor und Haltepunkte. `ArenaWindow.Update` (`:221`) multipliziert dann mit `director.ScaleAt(tick)` und zieht die Hit-Stop-Zeit ab, bevor `Advance` läuft.

- **Neuer Standard 1× = 10 Ticks/s** (heute 20). Die Liste wird `{0.5, 1, 2, 4}`, und «Turbo» bleibt das alte 4×. Die Wahl wird in `PlayerPrefs` gespeichert. Ruhige Strecken, in denen nur Basisangriffe laufen und nichts eingereiht ist, laufen automatisch ×1,5.
- **Zeitlupe (0,3×) für 0,6 s Echtzeit** bei Kill, Krit, Treffern über 15 % der Max-HP, Auslösung eines gegnerischen Charge-Angriffs, Ritter unter 30 % HP, Hack, neuer Thermal-Stufe und Combo-Kette (≥3 Aktionen über Trigger/Pulse). Weil die Wiedergabe vorausschaut, beginnt die Zeitlupe **5 Ticks vor** dem Ereignis.
- **Hit-Stop:** 60–120 ms Echtzeit-Freeze auf dem Treffer-Tick, skaliert mit dem Schaden. Dazu kommen ein Knockback-Offset des Körpers (kurze Verschiebung, die zurückfedert) und ein weisser Blitz (existiert schon über `_flashUntil`).
- **Pause bei Ereignis** (Häkchen): Krit/Kill, Gegner-Charge, jede eigene Skill-Aktion. Beim Halt zeigt die «JETZT»-Karte den Satz, und Space läuft weiter.
- **Schrittmodus:** `N` springt bis zum nächsten `ActionStarted` und hält dort, `.` geht einen Tick weiter. Space ist Pause, die Tasten 1–4 wählen das Tempo. Dafür bekommt `BattlePlayback` die Methode `NextTick(Func<BattleEvent,bool>)` (Lesezugriff auf `Events[_next..]`).
- **Hover pausiert:** Steht die Maus über Platine oder Gegner, läuft die Wiedergabe mit 0,25× (optional). Das behebt Schwäche 12.
- **Ende:** Nach `BattleEnd` bleibt die Bühne 1,5 s mit «VICTORY» bzw. «DEFEAT» stehen, und der Todesmoment läuft in Zeitlupe. Erst dann kommt `DrawReport` (`:330`).

### 3.2 Bühne: grosse, ehrliche Kämpfer

Das betrifft `DrawSide` (`:384-449`).

- Körper bis 140×200 px mit Silhouette oder Glyphe (♞, Gegner-Icon) statt reinem Rechteck. Der Ritter hat einen Rahmen in Akzentfarbe.
- **HP-Balken direkt über dem Körper**, 20 px hoch (Ritter 28 px), die Zahl steht **im** Balken. Dazu kommt ein **«Ghost»-Abschnitt**: Verlorene HP bleiben 0,5 s als heller Block stehen und schrumpfen dann. So sieht man jeden Treffer auch ohne Zahl.
- **Schild-Balken** (Shield Wall, Block) als blaue Schicht über dem HP-Balken. Statussymbole als Icons in einer Reihe mit Restzeit-Ring statt der Textkästchen. Hacks bleiben Schilder (`DrawHacks`).
- **Ziel-Markierung:** Der aktuelle Angriffsziel-Gegner bekommt einen Pfeil bzw. Rahmen in der Zeilenfarbe der feuernden Komponente.
- Tote Gegner kippen um und werden ausgegraut statt nur dunkelgrau.

### 3.3 «JETZT»-Karte und Action-Ticker

Das ist das wichtigste Element. Oben auf der Bühne steht eine grosse Karte (18–20 pt), darunter die zwei vorherigen Karten verblasst. Ein Satz:

> `▌[On Hit ◆◆] ⇒ #2 Shock Stab  ⚡4/4 · ↪ von #3 · aus Queue 0.6 s  →  Scrap Rat  −12  Krit!`

- Die Karte wird bei `ActionStarted` angelegt, die Wirkung (Schaden, Heilung, Status, Block, Ausweichen) füllt sich **live nach**, bis `ActionExecuted` kommt. Bei Multi-Target und Area steht dort «→ 3 Ziele −27».
- Gegneraktionen erscheinen rechtsbündig mit rotem Rand: «Scrap Rat: Ram → Ritter −25».
- Basisangriffe kommen als kleine Zeile, ab 2× gar nicht.
- **Datenänderung:** `FighterView` bekommt `ActionTarget`, `ActionRelay` und `ActionPower`. Neu ist `ActionStory` (Source, Row, Cause, Relay, CauseRow, Targets + Summen), die `BattlePlayback.Apply` beim Start anlegt und bei Damage/Healed/StatusApplied mit derselben Quelle und Row ergänzt (`:448-470`, `:555-587`). Die Texte gehen über die neue Methode `BattleLogText.Sentence(ActionStory)`, damit Log und Karte dieselben Worte nutzen.

### 3.4 Ursache → Wirkung als Linie

- In `DrawCircuit` werden die Bildschirm-Rects jeder Komponente gespeichert. Weil `DrawBoard` in `GUILayout.BeginArea` läuft, wird der Area-Offset addiert bzw. `GUIUtility.GUIToScreenPoint` verwendet.
- Bei `ActionExecuted` zeichnet die Arena einen **Strahl** (Bezier in der Zeilenfarbe, 0,4 s Echtzeit) vom Komponenten-Chip über die Bühnenkante zum Zielkörper. Davor leuchtet die schon vorhandene Spur Relais → Komponente (`CircuitGrid.DrawTrace`, `:672-674`) für die Dauer der Zeitlupe auf. Damit ist die Kette **Relais → Komponente → Ziel** in einem Blick sichtbar.
- Die Zahl über dem Ziel trägt ein kleines Etikett «#2» in der Zeilenfarbe statt des unbeschrifteten Farbchips (`:600-607`).

### 3.5 Gegner-Telegraphen

Möglich, weil die Wiedergabe den Ausgang kennt:

- Startet ein Gegner `ActionStarted` mit Windup ≥ 0,5 s, erscheint über ihm ein **Warnschild** «⚠ RAM in 0.8 s → Ritter ~25» mit dickem, rot pulsierendem Countdown-Balken. Der Schaden ist **exakt**: Er wird aus den Damage-Events derselben Quelle am Ausführungs-Tick nachgeschlagen. Zusätzlich leuchtet der HP-Balken des Ritters in dieser Höhe rot vor (Vorschau des Verlusts).
- **«Als Nächstes»-Uhr** pro Gegner: kleiner Text «nächste Aktion in 1.2 s», berechnet aus dem nächsten `ActionStarted` dieses Gegners.
- Kommt ein Interrupt, Stun oder Freeze dazwischen, wird das Schild durchgestrichen: «unterbrochen!». So wird der Wert der eigenen Kontrollskills sichtbar.

### 3.6 Warteschlange sichtbar machen

Die Textzeile (`:645-647`) wird zu einer **Reihe von Kärtchen**: ①②③ in Zeilenfarbe mit Wartebalken, und das gerade feuernde Kärtchen steht links. Beim Einreihen gleitet ein Kärtchen von seinem Chip herein, beim Start fliegt es hinaus. Ein Missed Trigger lässt ein kurzes graues «✖ schon eingereiht» am Chip aufblitzen. Die Regel muss als ein Satz feststehen: Lesereihenfolge oder FIFO. Den Widerspruch zwischen README-Keyword-Tabelle und `RowQueue.cs`/`BattlePlayback.cs:544` vorher klären.

### 3.7 Rauschen nach Tempo

| Element | 0.5× / 1× | 2× | 4× |
|---|---|---|---|
| JETZT-Karte | alle Aktionen | nur Skills | nur Skills ≥ Charge, Kills |
| Zahlen | jede einzeln | pro Ziel und 0,3 s summiert | pro Ziel und 0,6 s summiert, nur > 5 % HP |
| Pulse-Punkte, Relais-Zähler | an | an | aus (nur Relais-Blitz) |
| Strahl Komponente → Ziel | an | an | nur Krit/Kill |
| Log-Vorschau | live | live | eingefroren bis Pause |
| Zeitlupe/Hit-Stop | an | an | nur Kill/Boss-Charge |

Dazu kommt ein Schalter **«Fokus»**, der die Platine während des Kampfs auf Chips mit Zustandsfarbe ohne Text reduziert.

### 3.8 Log

Die Zeilen werden pro Aktion gruppiert: Kopfzeile = Satz aus 3.3, Wirkungen eingerückt. Spieler steht links und blau, Gegner rechts und rot. Ein neuer Filter «Wichtiges» zeigt nur Kills, Krits, Charges, Hacks und Missed Triggers. Gegner-Basisangriffe werden ebenfalls geloggt (`BattlePlayback.cs:466-467`). Ein Klick auf eine Zeile springt in der Zeitleiste zu diesem Tick (siehe 3.9).

### 3.9 Zeitleiste nach dem Kampf (und Mini-Version live)

- Über dem Report liegt ein Streifen über die Kampfdauer: HP-Kurven von Ritter und Gegnern, darunter eine Spur pro Komponente mit Punkten für jede Ausführung (Zeilenfarbe, Grösse = Schaden). Markierungen für Kill ◆, Krit ✦, Gegner-Charge ▲ und Missed ✖.
- **Klick = Replay ab hier:** `new BattlePlayback(result)` plus `Advance(tick)` ist billig, weil nichts gerechnet wird. Danach läuft es mit 0,5×. So wird aus «ich hab's verpasst» ein «nochmal anschauen».
- Live gibt es eine dünne Mini-Leiste mit «jetzt»-Marker und HP-Verlauf. Später kommt Scrubbing dazu (P3).

---

## 4. Prioritäten und Aufwand (IMGUI)

| Prio | Vorschlag | Aufwand | Wo |
|---|---|---|---|
| **P1** | Tempo: 0.5× und neues 1× = 10 t/s, Tasten (Space, 1–4, N), Tempo merken | S | `ArenaWindow.Speeds/Update/DrawControls` |
| **P1** | JETZT-Karte + `ActionStory` (Ursache, Ziel, Wirkung) | M | `BattlePlayback.Apply`, `FighterView`, `BattleLogText`, neues `DrawNowCard` |
| **P1** | Grosse HP-Balken am Körper, Ghost-Schaden, Schild-Schicht, grössere Kämpfer | M | `DrawSide`, `DrawStatuses` |
| **P1** | Gegner-Telegraph mit exaktem Schaden und Countdown | M | Look-ahead in `BattlePlayback`, `DrawSide` |
| **P1** | Zeitlupe + Hit-Stop + Ende halten (`PlaybackDirector`) | M | neue Klasse, `Update`, `OnGUI:330` |
| **P2** | Strahl Komponente → Ziel, Relais-Spur in Zeitlupe | M | `DrawCircuit` (Rect-Cache), neues `DrawBeams` |
| **P2** | Rauschfilter nach Tempo, Zahlen summieren | S | `TrackHighlights:266-270`, `DrawPopups` |
| **P2** | Pause-bei-Ereignis, Schrittmodus, Hover verlangsamt | S | `PlaybackDirector`, `Update` |
| **P2** | Warteschlange als Kärtchen, Regel klären | M | `DrawBoard:645` |
| **P2** | Layout: Bühne 65 %, Log als Schublade, Gegner-Platine als Overlay | M | `OnGUI:311-316`, `DrawSide:397` |
| **P3** | Zeitleiste nach dem Kampf mit Klick-Replay | L | `DrawReport`, `BattleReport` |
| **P3** | Log gruppiert, Filter «Wichtiges», Klick → Zeitleiste | M | `DrawLog`, `LogEntry` |
| **P3** | Live-Scrubbing, Combo-Banner, Bildschirmrand-Blitz bei grossem Treffer | M | diverse |

**Empfohlene Reihenfolge:** zuerst Tempo-Tasten und 0.5× (eine Stunde Arbeit, sofort spürbar), dann `ActionStory` und die JETZT-Karte als Rückgrat, auf dem Telegraph, Strahl, Log und Zeitleiste aufbauen. Danach HP-Balken und `PlaybackDirector`.

**Tests:** `ActionStory`, Look-ahead und `PlaybackDirector` sind Unity-frei und lassen sich wie `BattlePlayback` in den EditMode-Tests mit festem Seed prüfen. Zum Beispiel: «Karte nennt Relais und Ziel» oder «Zeitlupe beginnt 5 Ticks vor Kill».
