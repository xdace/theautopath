# 04 – Visuelle Sprache & Style-System (Betaknight)

Pfade relativ zu `Assets/Betaknight/Scripts/`. Ziel: Ein Token-System, in dem **Farbe genau eine Bedeutung trägt**, Glyphen auf jeder Plattform rendern und Build, Arena und Overworld wie ein Produkt aussehen.

## 1. Befund

**Farbe ist mehrfach belegt – Skill-Art und Zustand kollidieren (Kernproblem).**
- Attack `(1,.45,.40)` (`Overworld/UI/CircuitGrid.cs:153`) ≈ Unpowered `(1,.42,.38)` (`CircuitGrid.cs:25`) = `UiTheme.Bad` (`UiTheme.cs:24`).
- Healing `(.45,.95,.50)` (`CircuitGrid.cs:157`) ≈ Powered (`CircuitGrid.cs:23`) = `UiTheme.Good`.
- Fire `(1,.60,.20)` (`:155`) ≈ TooLarge `(1,.62,.22)` (`:24`).
- Shield `(.45,.70,1)` ≈ Queued/Frozen (`ArenaWindow.cs:1338-1339`). Queued und Frozen sind fast gleich hellblau.
- Shock `(1,.92,.30)` ≈ `UiTheme.Accent` (Auswahl, Hover, Firing, Tooltip-Rahmen, PinLinked, Route auf der Karte `Config/OverworldSettings.cs:53`, Charge-Badge, Lock, Stun, Crit-Popup). Gelb bedeutet ~12 Dinge.
- Movement `(.85,.55,1)` ≈ Core `(.55,.40,.95)` ≈ GateBorder ≈ Evolution `#d29bff` (`BuildWindow.cs:418`).

**Skill-Identität wechselt zwischen Screens.** Inventar färbt nach Art (`BuildWindow.cs:253-260`), auf der Platine ist jede Komponente neutral grau mit Zustandsrahmen (`BuildWindow.cs:424-425`), in der Arena bekommt sie eine **Index-Farbe** aus `RowColors` (`ArenaWindow.cs:33-43, 732`), also orange für Zeile #2, egal ob Schild oder Feuer. Der Spieler kann Farbe nicht lernen.

**Token-Wildwuchs.** 52 `new Color` in `ArenaWindow.cs`, 44 in `CircuitGrid.cs`; Rich-Text-Hex-Literale: `#9aa4b2` 44× (≈ `MutedColor`), `#ffd75e` 40× (≈ `Accent`), `#888888` 12×, dazu ~35 Einzelwerte. `ArenaWindow.EnsureStyles` (`:1437-1451`) baut eigene Text-Styles statt `UiTheme` zu nutzen; Kampf-Hintergründe sind wieder eigene Werte (`:309, :371`).

**Typografie zerfasert.** Theme kennt 18/15/13 (`UiTheme.cs:12-14`), real im Einsatz: 9 (`EnemyBoardView.cs:64`), 10 (`<size=10>` `CircuitGrid.cs:311`), 11 (`CircuitGrid.cs:70`), 12, 13, 14, 15, 18, 19 (`ArenaWindow.cs:1444`). 9–10 px sind mit dem Default-Font unlesbar, besonders bei 4×-Wiedergabe.

**Riskante Glyphen.** Der Default-Font (`LegacyRuntime.ttf`/Arial, vgl. `Views/ProceduralSprites.cs:43-45`) deckt etwa WGL4 ab: `→ ← ↓ ≈ ≤ ≥ • ■ ▲ ○ ● ▌` sind sicher. **Nicht** enthalten: `◆ ◇ ✖ ✔ ✕ ⚡ ↪ ↻ ⏳ ⧗ ❄ ⌀ ★ ✦ ▶ ▣ ▧ ⚠ ⏫ ⤒ ⇉ ☰ ⤳ ⌚ ⇅ ⇄ ϟ`. Auf Windows rettet oft das OS-Fallback, auf WebGL/Linux gibt es Tofu oder leere Zellen; `⚡ ⏳ ⌚` können zudem als Farb-Emoji erscheinen und ignorieren `<color>`. Fallbacks gibt es nur in `EffectText.Glyph` (`EffectText.cs:35-44`, Fallback = erster Buchstabe) und `ArenaWindow.StateGlyph` (`:1401-1424`). Ungeschützt sind u. a. `ChargeBadge "⚡"` und Log-Präfixe `↪ ⚡ ↻ ⏳` (`Core/Arena/ArenaTexts.cs:131-136`), Schwierigkeit `◇◆◆◆` (`Core/Arena/Difficulty.cs:96`), Modulzahl `◆` (`BuildWindow.cs:254`, kollidiert semantisch mit Schwierigkeit), `✖ Discard` (`BuildWindow.cs:778, 1071`), Evolution `✦` (`BuildWindow.cs:418`, `OverworldHud.cs:156`), Synergie `★` (`RuneOfferWindow.cs:89`), `✔/✖` im Report (`ArenaWindow.cs:1134-1137`), Chip `▣` (`UiTexts.cs:253`).

**Motion ist hart und schnell.** Flackern mit 5–10 Hz: Hitze-Skip (`CircuitGrid.cs:522`, weiss/rot mit 10 Hz), Hack-Flicker (`CircuitGrid.cs:541`, 6 Hz), Overheat-Banner (`ArenaWindow.cs:357`). Das liegt über der Photosensitivitäts-Empfehlung (≤ 3 Hz). Es gibt kein Easing, keinen Glow-Ramp und keinen Treffer-Shake.

**Flache Oberflächen.** `UiTheme.Framed` (`UiTheme.cs:132`) ist eine 6-px-Textur mit 1-px-Rahmen. Panel, Zelle und Tooltip unterscheiden sich nur um wenige Prozent Helligkeit (`.075/.115/.16`), deshalb fehlt eine Tiefenhierarchie. Die Overworld nutzt ASCII-Labels `! B E $ * G` (`OverworldSettings.cs:60-66`), robust, aber stilfremd.

## 2. Farb-Tokens

Prinzip: **Hue = Skill-Art, Rahmen/Animation = Zustand, Neutral/Weiss = Interaktion.** Zustände bekommen eigene Hues *außerhalb* des Art-Spektrums oder werden über Form und Helligkeit codiert.

| Gruppe | Token | Wert | Verwendung |
|---|---|---|---|
| Fläche | `bg.0` | `#0B0E14` | Screen, Arena-Hintergrund |
| | `bg.1` | `#121722` | Fenster/Panel |
| | `bg.2` | `#1A2130` | Karten, Zellen |
| | `bg.3` | `#243045` | Hover, erhöhte Fläche |
| | `line.dim` / `line` / `line.hi` | `#222A38` / `#34405A` / `#56688C` | Raster, Rahmen, Fokus |
| Text | `text.hi` / `text` / `text.mute` / `text.off` | `#F2F5FA` / `#D5DBE6` / `#8C96A8` / `#59627A` | Titel, Fliesstext, Hinweise, deaktiviert |
| Interaktion | `focus` | `#FFFFFF` (2 px) + `bg.3` | Auswahl/Hover, **nicht gelb** |
| Brand | `accent` | `#3FE0D0` (Circuit-Cyan) | Leiterbahnen, Pulse, Primärbutton |
| Wert | `gold` | `#FFC94A` | **nur** Gold/Preis/Loot |
| Semantik | `good` / `bad` / `warn` / `info` | `#5BE38A` / `#FF5C6C` / `#FFA23A` / `#6FB7FF` | Vergleich besser/schlechter, Fehler, Hinweis |
| Skill-Art | `kind.attack` | `#FF6B5A` (Rot) | |
| | `kind.shield` | `#5B8CFF` (Kobalt) | |
| | `kind.fire` | `#FF8F2E` (Orange) | |
| | `kind.shock` | `#F5E04A` (Zitron) | |
| | `kind.heal` | `#4FD69C` (Mint) | |
| | `kind.move` | `#C77DFF` (Violett) | |
| Zustand | `state.powered` | `line.hi` + grüne Lampe | Normalzustand, nur kleiner Indikator |
| | `state.charging` | `accent` als Füll-Balken | Ladeanzeige |
| | `state.queued` | `#A9B8FF` gestrichelter Rahmen | Warteschlange |
| | `state.firing` | `#FFFFFF` Glow + Art-Farbe hell | Auslösen |
| | `state.frozen` | `#9FE8FF` + Eis-Overlay (Schraffur) | |
| | `state.unpowered` | `text.off`, Fläche entsättigt, Kreuz-Icon | statt Rot |
| | `state.toolarge` | `warn` | |
| Sonder | `core` `#8E6CFF`, `evolve` `#E59CFF`, `hack` Effektfarbe | | |

Weil Attack und Bad zwangsläufig beide rot sind, wird `unpowered` **nicht** rot, sondern grau, entsättigt und mit Icon dargestellt. Rot bleibt Schaden/Fehler. Die Effekt-Farben aus `Core/Circuit/Effects.cs:216-268` bleiben datengetrieben, laufen aber durch `Palette.Clamp()`, damit Helligkeit und Sättigung zur Palette passen (Mindestkontrast 4.5:1 gegen `bg.2`).

## 3. Typografie

| Stufe | px | Stil | Wann |
|---|---|---|---|
| `title` | 20 bold | Fenstertitel, Ergebnis-Banner |
| `heading` | 16 bold | Abschnitte, Kartennamen |
| `body` | 14 | Fliesstext, Tooltips |
| `small` | 12 | Metadaten (Form, Castzeit), Log |
| `tiny` | 11 bold | Badges/Chips auf der Platine; **Minimum** |
| `number` | 22–28 bold | Floating Numbers, Popups |

Unter 11 px wird nichts gesetzt: statt `<size=10>` wird ein Icon gezeichnet oder das Element entfällt (Tooltip-only). Zahlen werden rechtsbündig gesetzt, Großbuchstaben mit Sperrung für kurze Status-Tags («FIRING», «QUEUED») passen zum Sci-Fi-Look. Mittelfristig kommt ein eigener Font (z. B. Inter/Rajdhani, OFL) mit Fallback-Kette ins Projekt. Damit ist auch das Glyphenproblem gelöst.

## 4. Icon-Set

Benötigt (~30): 6 Skill-Arten, 7 Zustände (powered, charging, queued, firing, frozen, unpowered, too large, orphaned), 15 Effekte (Overclock … Latency), Core, Relay, Chip, Modul, Modul-Slot, Trigger-Link, Evolution, Synergie, Schwierigkeit, Gold, HP, Lock, Discard, Map-Events (Enemy, Elite, Boss, Shop, Truhe, Mine, Event).

**Stufe A (sofort, S):** `Ui.Icon(Rect, IconId, Color)` als prozedurale Pixel-Icons: 12×12 Bitmasken (string[]-Raster, z. B. `"..XX.."`) werden einmal in `Texture2D` (Point-Filter) gerendert und gecacht, analog zu `ProceduralSprites`. So hängt nichts mehr vom Font ab, alles bleibt einfärbbar und skaliert in ganzen Vielfachen (12/24/36). Rich-Text-Glyphen werden durch ASCII-sichere Tokens ersetzt (`<b>2/4</b>` statt `⚡2/4`), das Icon wird daneben gezeichnet.

**Stufe B (später, M):** 32-px-Sprite-Atlas (1 PNG, monochrom weiss) via `Resources.Load`, gleiche `IconId`-API. Die Bitmasken bleiben als Fallback erhalten.

Formsprache: 1-px-Strich, 45°-Schrägen, Pad-Punkte an Linienenden (Leiterbahn-Optik). Art-Icons: Klinge, Schild, Flamme, Blitz, Kreuz, Pfeil-Doppel.

## 5. Komponenten

- **Panel:** `bg.1`, 1 px `line`, 4-px-Eckschnitte (gezeichnet als Pixel-Fase im 9-Slice), Kopfzeile mit 2-px-`accent`-Strich links.
- **Card** (Belohnung, Shop, Item): `bg.2`, Kopf mit Art-Farbbalken 3 px oben, Icon links, `heading` + `small`; Hover → `bg.3` + `focus`-Rahmen.
- **Chip** (Komponente auf der Platine): Fläche = `lerp(bg.2, kind, .22)`, linke Kante 4 px volle Art-Farbe (überall gleich: Inventar, Build, Arena, Gegner). Rahmen = Zustand, Ecke oben rechts = Zustands-Icon, unten = Ladebalken.
- **Badge:** 16 px hoch, `bg.0` mit 85 % Deckkraft, 1 px Farbrahmen, Icon + `tiny`-Zahl. Gemeinsame Funktion statt 6 Varianten (`CircuitGrid.cs:445-529`, `ArenaWindow.cs:766`).
- **Bar** (HP, Hitze, Ladung): Track `bg.0`, Segmente mit 1 px Lücke, Füllung in Token-Farbe; HP nicht mehr per Lerp Rot→Grün (`ArenaWindow.cs:424`), sondern `good`, unter 30 % `bad`.
- **Button:** primary (`accent`-Füllung, dunkler Text), secondary (`bg.2` + `line`), danger (`bad`-Rahmen, z. B. Discard). Hover hellt auf, Gelb gibt es nicht mehr.
- **Tooltip:** `bg.0`, Rahmen `line.hi` (nicht Accent), max. 340 px, Titelzeile `heading` mit Icon und Art-Farbe, Rest `body`/`small`.

## 6. Motion

Alle Animationen nutzen `Time.unscaledTime` mit Easing (`EaseOutCubic`) und laufen über eine zentrale `Ui.Anim`-Hilfe.

| Ereignis | Bewegung | Dauer |
|---|---|---|
| Trigger/Firing | Pulse: Rahmen 2→4 px, Glow-Halo (3 Alpha-Rechtecke) in Art-Farbe, ausblenden | 250 ms |
| Charging | Glow atmet mit 1.2 Hz, Intensität = Ladung/Zellen | kontinuierlich |
| Pulse auf Leiterbahn | Kopf + 3 Schweif-Punkte mit abnehmender Alpha | pro Tick |
| Treffer am Fighter | Shake ±3 px, gedämpft | 150 ms |
| Frozen | Overlay-Schraffur, statisch | – |
| Hack/Overheat | Flackern **≤ 3 Hz**, oder Alpha-Sinus | – |
| Fenster öffnen | Fade + 8 px Slide | 120 ms |

Eine Option «Reduce Motion» schaltet Shake und Flackern ab.

## 7. «Sci-fi circuit», aber lesbar

- Board-Hintergrund `bg.0` mit 1-px-Punktraster statt Vollgitter, Leiterbahnen als 45°-Polylinien mit Pads.
- **Leuchten nur für Zustandswechsel**, Ruhezustand matt. Nie mehr als ein Glow pro Komponente.
- Text liegt immer auf ≥ 85 % deckender Fläche, nie direkt auf Glow.
- Arena-Fighter- und Map-Hexes übernehmen die Panel-Fasen und Art-Farben (Enemy = `bad`, Elite = `warn` + Doppelrand, Boss = `core`, Shop = `gold`).

## 8. UiTheme-Umbau

```csharp
public static class Tok {            // nur Werte, keine Logik
  public static class Bg   { Color L0, L1, L2, L3; }
  public static class Line { Color Dim, Base, Hi; }
  public static class Txt  { Color Hi, Base, Mute, Off; }
  public static class Sem  { Color Good, Bad, Warn, Info, Gold, Accent, Focus; }
  public static Color Kind(SkillKind k);   // ersetzt CircuitGrid.KindColor
  public static Color State(RowDisplay s); // ersetzt ArenaWindow.StateColor + Build-StateColor
  public static string Hex(Color c);       // + vorberechnete Hex-Strings: Tok.HexMute, Tok.HexGold
}
public static class Type { GUIStyle Title, Heading, Body, Small, Tiny, Number; }
public static class Ui   { Panel(), Card(), Chip(), Badge(), Bar(), Icon(), Tooltip(), Anim }
```
`UiTheme` bleibt als Fassade für `Apply()` und Skin-Bau, `CircuitGrid` behält nur die Geometrie. Effektfarben werden über `Tok.Effect(id)` normalisiert.

## 9. Migration & Prioritäten

| Prio | Maßnahme | Aufwand |
|---|---|---|
| P1 | `Tok`-Klasse anlegen, `#9aa4b2`/`#ffd75e`/`#888888` und alle `new Color` in UI-Code per Suchen/Ersetzen auf Tokens umstellen (keine optische Änderung) | M |
| P1 | Kollisionen auflösen: Unpowered = grau + Icon, Accent ≠ Shock, Queued ≠ Frozen, Gold nur für Gold | S |
| P1 | Art-Farbe als linke Kante auf allen Komponenten-Darstellungen; `RowColors` (`ArenaWindow.cs:33`) durch `Tok.Kind` ersetzen | S |
| P1 | Glyph-Audit: zentrale `Glyph.Safe(id)` mit ASCII-Fallback für alle unter §1 genannten Stellen; Flackern auf ≤ 3 Hz | S |
| P2 | Typo-Skala, `ArenaWindow.EnsureStyles` und `CircuitGrid.Label/Tiny` auf `Type.*` umstellen, Minimum 11 px | S |
| P2 | Prozedurale Pixel-Icons (Stufe A) für Arten, Zustände und Effekte | M |
| P2 | Komponenten-Helfer (`Ui.Badge`, `Ui.Bar`, `Ui.Card`), Duplikate in Arena/EnemyBoardView/Build zusammenführen | M |
| P2 | Motion-Helfer: Pulse, Charge-Glow, Hit-Shake, Reduce Motion | M |
| P3 | Eigener Font + Sprite-Atlas (Stufe B), gefaste Panels, Punktraster und 45°-Leiterbahnen | L |
| P3 | Overworld-Hexes an Token-Palette und Icons angleichen (`OverworldSettings.cs:50-76`) | M |

Reihenfolge: zuerst die reine Token-Extraktion als Refactor ohne sichtbare Änderung (gut reviewbar), danach die Kollisions-Fixes als ein sichtbarer Schritt mit Vorher/Nachher-Screenshots, danach Icons und Motion.
