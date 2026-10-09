# 03 – Persistentes HUD, Informationsarchitektur und Build-Fenster

Pfade relativ zu `Assets/Betaknight/Scripts/Overworld/UI/`, sofern nicht anders angegeben.

## 1. Befund: Warum es „unübersichtlich“ wirkt

### 1.1 HUD: ein scrollender Debug-Textblock statt Statusleiste
- `OverworldHud` bezeichnet sich selbst als „Schlichtes Debug-HUD … wird ersetzt“ (`OverworldHud.cs:18-21`). Es ist eine 380 px breite Spalte über die **ganze Bildschirmhöhe** (`InfoRect`, `:51`) mit ScrollView (`:76`).
- **Kein HP-Balken:** HP ist eine Textzeile `"HP: 34/50   Gold: 12   Shards: 2"` (`:82`, `UiTexts.Hud.Resources` `UiTexts.cs:52`), gleich gewichtet wie Seed und Koordinaten. In der Arena gibt es dagegen echte Balken (`ArenaWindow.cs:421-431`) – der Code für Füllbalken existiert also schon, nur nicht auf der Karte.
- Reihenfolge ohne Hierarchie: Titel, Zug/Boss, Ressourcen, **komplette Platinenliste** (`BoardList()`, `:83`, `:143-166`), Build-Summary, Gear, Sets, Tags, Minen, `Position`, `Tile`, `Seed` (`:101-103`), danach Hover-Info und Gegner-Platinen (`:105-113`). Spielrelevantes (HP, Boss-Countdown) und Debug-Daten (Seed, Koordinate) haben dieselbe Schrift und Farbe.
- Hover-Infos zum Feld (`DrawExactEnemies`, `:202-220`) landen **unten im Scrollbereich** – bei langer Platinenliste sind sie aus dem Sichtfeld geschoben; der Spieler sieht nicht, dass dort etwas erschienen ist.
- „New Run“ steht ohne Rückfrage direkt neben „Build“/„Inventory“ (`:132-135`) – ein Fehlklick beendet den Run.
- Die Spalte überdeckt ~30 % der Karte dauerhaft (`ContainsScreenPoint`, `:59-63`).

### 1.2 Fensterstapel ohne Regeln
- Jedes Fenster ist ein eigenes `MonoBehaviour` mit eigenem `OnGUI` und eigener Rechteck-Berechnung (`ShopWindow.cs:49-51`, `RuneOfferWindow.cs:53-55`, `SalvageWindow.cs:65-67`, `EncounterWindow.cs:83`, …). `GUI.depth` ist uneinheitlich: Arena −10, InventoryFull −6, Build/Inventory −5, Reward/Shop/Salvage/Encounter **gar nicht** gesetzt.
- Die Verwaltung ist im Bootstrapper verstreut: Build und Inventory schliessen sich gegenseitig (`OverworldBootstrapper.cs:136-146`), alle Entscheidungsfenster hängen nur an `Hidden = arenaOpen` (`:84-91`). B/I funktionieren auch, wenn ein Reward-, Shop- oder Event-Fenster offen ist (`:149-162`) – dann liegen zwei zentrierte Fenster übereinander, ohne Abdunkelung.
- `InputBlocked` prüft nur Arena/Build/Inventory (`:225`); die Konsistenz hängt an `_session.IsBusy`.
- Kein Fenster hat Tabs ausser dem Toggle Module/Recipe Book (`BuildWindow.cs:736-750`); Tastenhinweise stehen in Tooltips („Close (I or Esc)“) statt sichtbar.

### 1.3 Meldungen: flüchtig, ohne Verlauf, an der falschen Stelle
- `EncounterWindow` zeigt Meldungen unten links für **4 s**, max. 5 (`EncounterWindow.cs:15-16`, `:104-112`), als blankes Label ohne Hintergrund – genau dort, wo die HUD-Knopfleiste liegt (`OverworldHud.ButtonRect`). Text über Knöpfen ist schlecht lesbar.
- Kein Verlauf: „Gold Mine (3,-2) under attack!“ ist nach 4 s weg; laufen gleichzeitig Arena oder ein Fenster, sieht man die Meldung nie. Die Meldungslogik gehört auch nicht in die Event-Klasse.

### 1.4 Zu viel Text, zu wenig Form
- `StatBar.Draw` packt 9 Werte plus Boni in eine Textzeile (`StatBar.cs:37-69`) – HP ist dort `Hp 34/50` zwischen „Area Damage“ und „Crit“.
- Build-Fenster: Hinweistext über 4 Zeilen am Fuss (`BuildWindow.cs:165`, `UiTexts.cs:377-380`), dazu Untertitel im Header (`:183`), Board-Legende (`:316`), Preview-Zeile (`:192-206`), Fallback-Zeile und Board-Regeln (`:326-331`).
- Komponenten auf der Platine tragen 3–4 Textzeilen + Effekt-Badges + Modul-Kärtchen (`DrawComponentChip`, `:406-441`) – bei Zellgrösse < 60 px kaum lesbar.
- Tooltips sind Wände: `ComponentTip` (`:1125-1154`) baut bis zu ~12 Zeilen (Details, Status, Core, Pins, Effekte, Growth, Module, Evolution, Drag-Hinweis). Dieselbe Info erscheint zusätzlich in der Preview-Zeile (`Hover(...)` `:440`) – **doppelte Ausgabe** pro Hover.

### 1.5 Module dreifach, Auswahl ohne Wirkung
- Ein ausgerüstetes Modul erscheint (a) als Kärtchen auf dem Teil (`DrawModuleBadges`, `:1239-1265`), (b) als Chip in der Teile-Liste unter dem Board (`DrawModuleSlots`, `:706-734`) und (c) in der Modul-Spalte rechts mit „✖“ (`DrawModuleColumn`, `:758-793`). Drei Klickziele für dieselbe Aktion.
- `_selected` wird gesetzt (`:171`, `:282`, `ClickComponent` `:1268ff`), aber nur zur Rahmenfarbe benutzt – **es gibt kein Detailpanel**. Details gibt es ausschliesslich per Tooltip, der beim Ziehen verschwindet (`:173`, `if (!_drag.IsDragging) UiTheme.DrawTooltip()`).
- Link-Modus (Trigger/Charge Link) wird nur über die Preview-Zeile und gelbe Rahmen kommuniziert (`:192-203`, `:436`); kein Abbrechen-Knopf, nur Esc.

### 1.6 Inkonsistente Begriffe, Farben, Symbole
- 49 hartkodierte Hex-Farben in `BuildWindow.cs`, 32 in `ArenaWindow.cs`; dasselbe Grau als `#9aa4b2`, `#888888`, `#666c78`, Rot als `#ff7a6b`, `#ff6b61`, `#ff8a80`, `UiTheme.Bad`. `UiTheme` definiert nur 5 semantische Farben (`UiTheme.cs:16-25`).
- Symbol-Überladung: **◆** = Modulanzahl (`OverworldHud.cs:152`), Modul-Kärtchen (`BuildWindow.cs:579`) *und* Schwierigkeitsbonus (README Keywords „Difficulty … (◆)“). **⚡** = Charge Link *und* Ladezustand „⚡2/4“. „Chip“ = `RelayChip`, `LogicChip`, `BoardChip` und `ModuleText.Chip`.
- Rune vs. Relay: Inventar heisst „Rune Inventory“, auf dem Board „Relay“, Tooltips wechseln (`RelayDragHint`: „onto the chip inventory“, `UiTexts.cs:374`, obwohl die Ablage „Rune Inventory“ heisst).
- README beschreibt Build noch als Zeilenliste („one row per rune + skill … handle ≡“, README Controls) – Doku und UI widersprechen sich.

### 1.7 Lernen
- Kein Glossar im Spiel; die Keyword-Tabelle (README „Keywords“, ~50 Begriffe) existiert nur für das Team. Neue Begriffe (Pulse, Typed Pin, Easer, Charge) werden nur in Tooltips definiert, nie zentral.

## 2. Konzept

### 2.1 Persistente Top-Bar (P1, M)
Neue Klasse `TopBar` (ersetzt den Infoblock in `OverworldHud.OnGUI`), 44 px hoch, volle Breite, **immer sichtbar** – auch über Build/Inventory/Shop (eigenes `GUI.depth = -20`, Fenster beginnen bei y = 52). In der Arena ausgeblendet (dort gibt es eigene Balken).

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ ♥ [██████████████░░░░░▒▒▒] 34/50 +8   ● 120 Gold   ◈ 2/3 Shards │ Act 1 · Turn 14 · ☠ Boss in 3 │
│ [Heat 4●] [Static 2] [Set Ironclad 2/3●] [Duo ???]      ⚠ Mine (3,-2) 2 Z.   [B] [I] [≡] [?] │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```
- **HP-Balken** 220 px, Farbverlauf wie Arena (`ArenaWindow.cs:424` extrahieren nach `UiTheme.DrawBar(rect, value, max, color)`); Schild/Block als blaues Overlay rechts (`▒`) oder als Zahl „+8“, sobald die Session einen Startschild kennt. Unter 30 % pulsiert der Rahmen rot. Bei HP-Verlust kurz weisser „Damage-Chunk“, der in 0,4 s abschmilzt.
- Shards als 3 Pips (◈◈◇), weil „3 Shards = Reward“ – Fortschritt statt Zahl.
- Boss-Countdown rechts, rot ab ≤ 3 (Logik aus `OverworldHud.cs:79-80`).
- Zweite Zeile: **Tag-/Set-/Duo-Icons** als kleine Pillen (Tag-Farbe, Zähler, Punkt = Schwelle erreicht); Hover = Kurzzeile, Shift = `SynergyCounter.Tag.Describe` (heute `TagTooltip`, `:227-234`). Minenwarnungen als Pille mit Countdown.
- Rechts Knopf-Icons mit **sichtbarem Shortcut** `[B] [I] [≡ Log] [? Help]`. „New Run“ wandert ins Esc-Menü mit Bestätigung.
- Ausgemistet: Seed, Position, Tile → Debug-Overlay (F3). `BoardList()` und `BuildSummary()` entfallen im HUD (gehören ins Build-Fenster).
- Feld-Hover (Gegner, Loot, `DrawExactEnemies`) wird ein **Inspector-Panel** rechts unten, das nur bei Hover erscheint (fixe Position, nicht im Scroll).

### 2.2 Toast-System mit Verlauf (P1, S–M)
`Notifications` (statische Queue + `ToastLayer`-Behaviour), ersetzt `EncounterWindow.Post/_messages`.
- Toasts rechts oben unter der Top-Bar, gestapelt, Hintergrund `UiTheme.Tooltip`, Icon + Kategorie-Farbe (Gefahr rot, Belohnung gold, Info grau). Dauer nach Länge (3–7 s), Hover pausiert, Klick schliesst.
- **Wichtig-Stufe** (Mine angegriffen, Board gewachsen): bleibt, bis bestätigt.
- Während Arena/Modal werden Toasts **gepuffert** und danach gezeigt.
- Verlauf `[≡]` / Taste L: letzte 50 Einträge mit Zug-Nummer, gruppiert nach Zug.

### 2.3 Window-Framework: ein Modal zur Zeit (P1, M)
`UiWindowManager` (ein `OnGUI`, alle Fenster als `IUiWindow { Title, Tabs, Priority, Draw(Rect), CanClose }`).
- Zwei Ebenen: **Panels** (Build, Inventory, Codex/Help – vom Spieler geöffnet) und **Decisions** (Encounter, Reward, Salvage, Shop, InventoryFull, Portal, GameOver – vom Spiel geöffnet). Höchstens *ein* Element sichtbar; Decision hat Vorrang und hält eine Warteschlange (`Salvage → Reward` ist heute implizit über `PendingSalvage`).
- Aus einer Decision heraus darf ein Panel *als Tab* geöffnet werden („Build ansehen“), Rückkehr per Esc – statt Überlagerung.
- Einheitlicher Rahmen: Titelzeile mit Tabs `[Build B] [Inventory I] [Codex ?]`, rechts `Esc ✕`; Fusszeile mit **Tasten-Chips** statt Prosa (`[Drag] place · [RMB] rotate · [R] rotate while dragging · [Dbl] auto`).
- Halbtransparente Abdunkelung hinter jedem Modal; `controller.InputBlocked = manager.AnyOpen` (`OverworldBootstrapper.cs:225`).
- Fenster-Rects einmal zentral (Grössen S 520 / M 760 / L 1560), statt pro Datei.

### 2.4 Tooltip-Hierarchie (P1, M)
Drei Stufen, eine API `UiTheme.Tip(rect, short, details, glossaryKeys)`:
1. **Hover** (sofort): eine Zeile – Name, Kernwert, Status. Beispiel: „#3 Shock Stab · 2×1 · 0.6 s · ⚠ too large“.
2. **Shift halten / 0,6 s ruhig** : Detailkarte (heutiger `ComponentTip`), aber gegliedert in Abschnitte mit Überschriften und **ohne Drag-Hinweise** (die stehen in der Fusszeile).
3. **Klick**: Detailpanel (Build) bzw. Codex-Eintrag (Begriffe unterstrichen → Klick öffnet Glossar).
- Die doppelte Ausgabe Preview-Zeile + Tooltip entfällt: `DrawPreviewLine` (`BuildWindow.cs:192`) zeigt nur noch Modus-Infos (Drag, Link).

### 2.5 Progressive Disclosure / Text kürzen (P2, S je Fenster)
- Hint-Prosa (`UiTexts.Build.Hint`, `Inventory.Hint`, `DetailsHint`) → Tasten-Chips in der Fusszeile; ausführliche Fassung nur im Codex.
- Board-Teile zeigen standardmässig **Name + Status-Icon**; Shape/Cast-Zeit erst bei Hover/Auswahl. Ab Zellgrösse < 60 px nur Icon + #n.
- StatBar: 4 Hauptwerte (HP, Damage, Attacks/s, Armor) sichtbar, Rest hinter „▸ more“.
- Erstbegegnung: Beim ersten Erhalt eines Modultyps/Chips ein einmaliger „Neu“-Toast mit Link in den Codex (Flag pro Begriff in `RecipeStore`-artiger Persistenz).

### 2.6 Codex / Help-Overlay „?“ (P2, M)
- Taste `?`/F1: Overlay als Panel mit Tabs **Controls · Glossary · Icons · Recipe Book** (Rezeptbuch zieht aus dem Build-Fenster hierher, `DrawRecipeBook` `:838`).
- Glossar aus einer Datentabelle (Inhalt = README „Keywords“, Englisch), durchsuchbar (`GUILayout.TextField`).
- **Icon-Legende** als Single Source of Truth: neue Klasse `UiIcons` (Konstanten + Farbe + Glossar-Key). Bereinigung: ◆ nur noch Modul; Schwierigkeitsbonus bekommt ▲; ⚡ nur Charge, Charge Link wird ⇢; „Chip“ im UI nur für Logic Chips, Module heissen Module, Relais konsequent „Relay (Rune …)“.
- Farben: `UiTheme` um semantische Tokens erweitern (`Muted`, `Disabled`, `Danger`, `Warning`, `Core`, `Evolution`, `Growth`, `Link`) und alle Hex-Literale ersetzen (Fleissarbeit, mechanisch).

### 2.7 Build-Fenster entdichten (P1, L)
Ziel: Board in der Mitte bekommt Platz, Listen unter dem Board verschwinden, ein **Detailpanel** rechts zeigt das ausgewählte Teil.

```
┌ Build ── [Build B] [Inventory I] [Codex ?] ───────────────── 4×4 (→5×5) ─ [✖ Discard] ─ Esc ✕ ┐
│ HP 34/50  Dmg 12  Atk/s 1.4  Armor 6   ▸ more        Tags: [Heat 4●][Static 2]               │
├──────────────┬────────────────────────────────────────────┬───────────────────────────────────┤
│ COLLECTION   │                 CIRCUIT BOARD               │ DETAIL  #3 Shock Stab   [Lv2 ✦]   │
│ [Skills|Runes│   ┌────┬────┬────┬────┐                     │ 2×1 · Cast 0.6 s · Dmg 18         │
│  |Modules|   │   │ R1 │ #1 ████████ │    │                │ Status: ✔ powered by R1 On Hit    │
│  Chips]      │   │ ⏱  │ Slash     ↪3│    │                │ Core +10 %                         │
│ ┌──┬──┬──┐   │   ├────┼────┼────┼────┤                     │ ─ Modules (2/2) ────────────────  │
│ │▓▓│▓ │  │   │   │ #2 │ ◉  │ #3 ███ │                     │ [◆ Chain    ✕] [⇢ Charge→#1  ✕]   │
│ │▓▓│▓▓│▓ │   │   │ ▲  │Core│ ShSt ⚠ │                     │ ─ Growth ───────────────────────  │
│ └──┴──┴──┘   │   ├────┼────┼────┼────┤                     │ 7/15 ▓▓▓▓▓░░░  next: +1 Chain     │
│ (gefiltert   │   │    │ R2 │    │    │                     │ ─ Effects ──────────────────────  │
│  nach Tab)   │   └────┴────┴────┴────┘                     │ 🔥 Burn 3 s                         │
│              │  ↓ Basic Attack fills gaps · 12 dmg         │ [Rotate RMB] [Remove Dbl] [Link]  │
├──────────────┴────────────────────────────────────────────┴───────────────────────────────────┤
│ [Drag] place  [RMB] rotate  [R] turn while dragging  [Dbl] auto  [Shift] details  [Esc] close │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
 Link-Modus: Kopfzeile wird gelb „Pick target for Charge Link (from #3) – [Cancel Esc]“, gültige Ziele leuchten.
```
- **Links: eine Sammlung mit Tabs** Skills | Runes | Modules | Chips (heute drei Spalten + Chip-Streifen: `DrawSkillColumn`, `DrawModuleColumn`, `DrawRuneColumn`, `DrawChipStrip`). Alles im Formraster-Stil des Skill-Inventars; Tab-Zähler „Modules 3 free“. Beim Ziehen eines Board-Teils springt der passende Tab auf und wird Ablageziel.
- **Mitte: nur Board** + Fallback-Zeile. `DrawComponentList`/`DrawRelayList` (`:644-704`) entfallen; die Reihenfolge #n steht auf dem Teil.
- **Rechts: Detailpanel** für `_selected` (endlich genutzt) bzw. Hover, wenn nichts gewählt ist. Inhalt = heutiger `ComponentTip`/`RelayTip`, aber in Sektionen. Modul-Slots nur noch **hier** und als Kärtchen auf dem Teil (Variante (b) fällt weg); freie Slots `◇` als Ablageziele.
- **Link-Modus** sichtbar: farbiger Banner mit Abbrechen-Knopf, ungültige Ziele abgedunkelt, bestehende Links als Pfeile auf dem Board (Pfeil-Overlay analog `DrawPulseLinks`, `:921`).
- **Trash** bleibt im Header, aber Bestätigung als kleines Popover am Mauszeiger statt Header-Umbau (`DrawTrash`, `:1179-1202`).
- Board-Teil: maximal 2 Zeilen (`#n Name` + Statusicon ✔/⚠/✖), Modul-Kärtchen als reine Icons ab kleiner Zellgrösse.

### 2.8 Inventory und Entscheidungsfenster angleichen (P2, M)
- Inventory nutzt denselben Rahmen (Tabs, Fusszeile, Detailpanel existiert schon: `DrawDetails`).
- Reward/Shop/Salvage: Karten statt Text-Buttons (Icon, Name, 1 Zeile Wirkung, ★-Badge), Shift = Details; Status-Zeile („Board 4×4, 3 relays …“, `RuneOfferWindow.cs:72`, `ShopWindow.cs:67`) entfällt, weil Gold/HP in der Top-Bar stehen.

## 3. Priorisierung

| # | Massnahme | Prio | Aufwand | Hauptsächlich betroffen |
|---|---|---|---|---|
| 1 | Top-Bar mit HP-Balken, Gold, Shards, Act/Turn/Boss | P1 | M | neu `TopBar`, `OverworldHud.OnGUI`, `UiTheme.DrawBar`, `ArenaWindow.cs:421` (extrahieren) |
| 2 | Debug-Infos (Seed, Position, BoardList) raus, Hover-Inspector | P1 | S | `OverworldHud.cs:83-113` |
| 3 | Toasts + Verlauf | P1 | S–M | neu `Notifications`, `EncounterWindow.Post/DrawMessages` |
| 4 | Window-Manager, ein Modal, Abdunkelung, B/I gesperrt bei Decision | P1 | M | `OverworldBootstrapper.cs:84-162,225`, alle `*Window.OnGUI` |
| 5 | Build: Detailpanel + Listen unter dem Board entfernen | P1 | L | `BuildWindow.DrawBoardColumn/DrawComponentList/DrawRelayList/DrawModuleSlots` |
| 6 | Tooltip-Stufen (Hover kurz, Shift Details), Preview-Duplikat weg | P1 | M | `UiTheme.DrawTooltip`, `BuildWindow.Hover/DrawPreviewLine`, `*Tip`-Methoden |
| 7 | Sammlung als Tabs links | P2 | M | `DrawSkillColumn/DrawModuleColumn/DrawRuneColumn/DrawChipStrip/DrawRightColumn` |
| 8 | Sichtbarer Link-Modus | P2 | S | `BuildWindow.DrawPreviewLine`, `ClickComponent`, `DrawGrid` |
| 9 | Codex/Help „?“ inkl. Recipe Book, Icon-Legende | P2 | M | neu `CodexWindow`, `DrawRecipeBook`, Daten aus README-Keywords |
| 10 | Farb-Tokens + `UiIcons`, Begriffe vereinheitlichen | P2 | M (mechanisch) | `UiTheme`, `UiTexts`, alle Hex-Literale |
| 11 | Reward/Shop/Salvage als Karten | P3 | M | `RuneOfferWindow`, `ShopWindow`, `SalvageWindow` |
| 12 | Erstbegegnungs-Hinweise, StatBar „▸ more“ | P3 | S | `StatBar.Draw`, Persistenz |
| 13 | README Build-Abschnitt an aktuelles Board anpassen | P3 | S | `README.md` Controls |

**Empfohlene Reihenfolge:** 1 → 2 → 3 (sichtbarer Gewinn in 1–2 Tagen, löst „kein HP-Balken“), dann 4 + 6 als Fundament, danach 5/7/8 als Build-Umbau in einem Zug.

## 4. IMGUI-Hinweise
- Ein zentrales `OnGUI` (Manager) statt 12 verteilter beseitigt die Depth-Probleme; IMGUI respektiert `GUI.depth` nur zwischen MonoBehaviours, nicht innerhalb – ein Manager zeichnet deterministisch in Reihenfolge.
- Balken/Pillen mit `UiTheme.Fill/Outline` (vorhanden, `UiTheme.cs:146-161`); keine Sprites nötig.
- Shift-Erkennung über `Event.current.shift`; „Hover-Verweilen“ per `Time.unscaledTime` seit letztem Hover-Wechsel (Muster wie `_hover/_hoverNext` in `BuildWindow`).
- Top-Bar-Rect in `OverworldHud.ContainsScreenPoint` aufnehmen, damit Kartenklicks darunter nicht durchgehen.
