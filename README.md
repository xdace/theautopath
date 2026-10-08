# Betaknight

Singleplayer-Autobattler-Roguelite in Unity 2D. Ein abtrünniger Android in Ritterrüstung erkundet eine Hexfeld-Oberwelt, rüstet sich aus und programmiert seine Skills mit Logik-Runen für automatische Kämpfe.

Dieses Repository enthält den **Meilenstein 1: Oberwelt bis einschliesslich Bewegung** und den **Meilenstein 2: Feld-Events** (jedes Feld trägt ein Event, Runen, Ritter-Kits, Shop; Kämpfe noch als Platzhalter bis zur Kampfarena).

## Schnellstart

1. **Unity installieren:** 2022.3 LTS oder Unity 6.
2. **Projekt öffnen:** In Unity Hub auf *Add → Add project from disk* klicken und diesen Repo-Ordner wählen. Unity legt `ProjectSettings/` und `Packages/` beim ersten Öffnen selbst an.
   Alternativ ein neues 2D-Projekt anlegen und den Ordner `Assets/Betaknight` hineinkopieren.
3. **Szene einrichten:** Neue leere Szene, ein leeres GameObject anlegen und die Komponente **`OverworldBootstrapper`** daraufziehen.
4. **Play drücken** und ein Ritter-Kit wählen.

Es sind keine Prefabs, Sprites oder Fonts nötig. Hexfelder, Spielfigur und Labels werden zur Laufzeit erzeugt.

### Bedienung

| Aktion | Wirkung |
|---|---|
| Maus über ein Feld | Zeigt die geplante Route (gelb) oder rot, wenn das Feld nicht erreichbar ist |
| Klick auf ein «?»-Nachbarfeld | Erkunden: 1 Schritt, 1 Zug |
| Klick auf ein entferntes, erforschtes Feld | Reise über bekannte Routen, Schritt für Schritt, jeder Schritt kostet einen Zug |
| Fenster bei mittleren Events | Eine der Optionen wählen (ausgegraute sind nicht bezahlbar) |
| Runenwahl | Eine Rune oder ein Ausrüstungsteil nehmen (★ = passt zu einem vorhandenen Tag) oder für 3 Gold verzichten. Teile: «Anlegen» oder «Ins Inventar»; Runen bei voller Tafel: «Ins Runen-Inventar» oder eine Zeile tauschen |
| Button «Tafel bearbeiten» | Zeilen der Logik-Tafel umsortieren (▲▼), jeder Rune einen Skill aus der Ausrüstung zuordnen (◀▶), Runen ablegen («ab») und aus dem Runen-Inventar einsetzen oder tauschen (↔) |
| Button «Inventar» | Links die 7 Ausrüstungsplätze, rechts das Inventar, darunter Runentafel und Runen-Inventar. Klick auf ein Teil zeigt Werte, Skills, Set und den Vergleich mit dem angelegten Teil (grün besser, rot schlechter) |
| Arena nach jedem Kampf | Spielt den Kampf ab: Tempo 1×/2×/4×, Pause, «Überspringen», danach das ganze Protokoll und «Weiter» |
| Button «Shop öffnen» | Erscheint auf einem bereits besuchten Shop-Feld |
| Button «Neuer Run» | Zurück zur Kit-Auswahl, neue Karte |

Eine Reise stoppt automatisch auf feindlichen Feldern (Gegner, Boss), auf neu entdeckten Feldern und sobald ein Fenster offen ist.

### Ablauf eines frühen Runs

1. **Kit wählen:** Klingen-, Schild- oder Funkenritter. Jedes Kit bringt HP, Gold und eine Start-Rune mit.
2. **Ring 1** um den Start hat nur kleine Events (Münzen, Kräuter, Runensplitter, Wegweiser). Sie wirken sofort und melden sich unten links.
3. **Ab Ring 2** kommen mittlere Events mit einer Entscheidung und die ersten Kämpfe. Truhen sind selten, Shops gibt es erst ab Ring 3.
4. **3 Runensplitter** öffnen eine Runenwahl, ebenso jeder gewonnene Kampf und jede Truhe. Es gibt 3 Runenplätze, ein vierter ist im Shop käuflich.
5. **Sammeln statt ersetzen:** Ausrüstung (12 Plätze) und Runen (6 Plätze) haben ein Inventar. Neue Teile werden angelegt, wenn ihr Platz frei ist, sonst kommen sie ins Inventar; verdrängte Teile (auch der Schild bei einer Zweihandwaffe) wandern ins Inventar. Eine neue Rune bei voller Tafel kommt ins Runen-Inventar. Runen behalten ihre Lagerfeuer-Stufe, beim Tauschen bleibt der Skill an der Zeile. Ist ein Inventar voll, wird gefragt: ein vorhandenes verwerfen oder das neue ablehnen. Wechseln geht jederzeit ausserhalb von Kampf und offenen Fenstern; Set-Boni, verwaiste Zeilen und Set-Runen folgen sofort. Im Shop lässt sich das Inventar für den halben Preis verkaufen. Das Inventar wandert durch die Akte mit.
6. Fällt der Ritter in einem Kampf, ist der Run vorbei. Events auf der Oberwelt töten nie.

### Kampf: die Logik-Tafel

Kämpfe laufen automatisch in festen Ticks (20 pro Sekunde). Jede Rune ist eine Zeile der Tafel: **Rune = Wann** (Bedingung), **Skill = Was** (kommt aus der Ausrüstung). Von oben nach unten feuert die erste Zeile, deren Bedingung erfüllt und deren Skill bereit ist. Ganz unten steht fest `[Immer] → Basisangriff`. Fehlt der Skill einer Zeile (Teil abgelegt), wird sie grau und übersprungen.

- **Skill-Kennzahlen im Tafel-Editor:** Unter jedem Skill (auch beim Durchblättern mit ◀▶ und beim festen Basisangriff) steht eine Infozeile: Wirkung, Schaden, CD, Ausholen und Erholung. Schaden steht doppelt, als Prozent vom Waffenschaden und als Wert mit der aktuellen Ausrüstung samt aktiver Set-Boni, gegen ein Ziel ohne Rüstung, ohne Block und Krit (z. B. «120 % Waffenschaden ≈ 10 an allen Gegnern», «Brennen 50 % Waffenschaden/s ≈ 3/s, 15 über 5 s», «20 % Chance: betäubt 1 s»). Skills ohne Schaden zeigen «kein Schaden». Mit der Maus über der Zeile erscheinen alle Details als Tooltip, bei vielen Zeilen scrollt das Fenster.
  Die Werte stehen nicht in der UI, sondern kommen aus den Effekten: Jede `ISkillEffect` meldet über `Describe(SkillInfoBuilder)` ihre Kennzahlen mit denselben Formeln wie `Apply`. `SkillInfo.Create(skill, stats)` fasst sie zusammen, `OverworldSession.SkillUserStats()` liefert die Werte des Ritters zu Kampfbeginn. Neue Effekte müssen `Describe` umsetzen und erscheinen dann automatisch richtig.
- 7 Ausrüstungsplätze (Helm, Handschuhe, Brust, Beinschienen, Waffe, Schild, Stiefel). Zweihandwaffen sperren den Schild.
- 4 Sets mit Boni ab 2 und 3 Teilen: Überlast-Protokoll, Aegis-Firewall, Schrott-Ernter, Phantom-Signal.
- Schutzregeln statt Balance-Bremsen: höchstens eine Aktion pro Tick, Reaktionen erst im nächsten Tick, ab 90 s Überhitzung. Kaputte Builds sind erlaubt, die Engine bleibt stabil.
- Lagerfeuer kann eine Rune eine Stufe verstärken (z. B. «HP unter 30 %» → «HP unter 40 %»).
- Sets: Fortschritt steht im HUD und im Tafel-Editor. Teile angefangener Sets kommen 3× häufiger in Angebote, Shops verkaufen 2 Teile. Aegis-Firewall (ab 2 Teilen) schaltet die Rune «Ladung voll» frei.
- **Goldminen-Verteidigung:** Alle 8 Züge wird eine eigene Mine angegriffen (rot, «!G»). 6 Züge Zeit, sonst ist sie verloren, bis sie zurückerobert ist. Der Kampf dort läuft «auf der Goldmine» (Schrott-Ernter, Rune «Auf Goldmine»).
- **Boss alle 25 Züge:** Er taucht beim Ritter auf und ist unbesiegbar. Wer 15 s überlebt, entkommt durchs Portal (+8 Gold, +2 Splitter). Ausweichen und Betäuben helfen, Phantom-Signal ist dafür gebaut.
- **Akte:** Das Fluchtportal führt auf eine neue Karte (Akt 2, 3 …). Ritter, Ausrüstung, Tafel, Gold und Splitter kommen mit, eroberte Minen bleiben zurück. Das Portal heilt 50 % der Max-HP, Gegner sind pro Akt 2 Stufen stärker, der Zugzähler und der Boss-Takt laufen weiter.

Konzept: `/mnt/project-files/design/kampfsystem-konzept.md` im Projekt.

### Feldsymbole (Platzhalter)

| Symbol | Inhalt |
|---|---|
| `?` | Unerforscht, Inhalt unbekannt |
| Kleinbuchstabe (`c`, `h`, `s`, `w`, `t`, `x`) | Kleines Event: Münzen, Kräuter, Splitter, Wegweiser, Händlerspuren, Dornen |
| Grossbuchstabe (`F`, `W`, `S`, `M`, `V`) | Mittleres Event: Lagerfeuer, Wanderer, Blutschrein, Söldner, Vorrat |
| `!` | Gegner |
| `B` | Boss |
| `$` | Shop |
| `*` | Schatztruhe |
| `G` | Goldmine |

Erledigte Events werden abgedunkelt. Ausgekundschaftete Felder (Wegweiser, Händlerspuren) zeigen ihr Symbol schon vor dem Betreten.

### Einstellungen anpassen

Rechtsklick im Project-Fenster → *Create → Betaknight → Overworld Settings*. Das Asset im Bootstrapper zuweisen. Dort lassen sich Kartenradius, Seed (0 = zufällig), Event-Mischung nach Entfernung (`distanceBands`), Regeln für grosse Inhalte (`contentRules`: frühestens ab Entfernung, höchstens Anzahl), Garantien (`contentQuotas`), Sichtweite, Farben, Feldgrösse und Animationstempo einstellen.

Standard-Verteilung (Radius 6):

| Ring | Klein | Mittel | Gross |
|---|---|---|---|
| 1 | 100 % | – | – |
| 2 | 60 % | 30 % | 10 % (nur Kämpfe und Truhen) |
| 3 | 40 % | 35 % | 25 % |
| 4–6 | 25 % | 35 % | 40 % |

Shops frühestens ab Ring 3 und höchstens 2, Truhen höchstens 6, Goldminen höchstens 4. Garantiert: 2 Kämpfe in Ring 2, 1 Truhe in Ring 2–3, je 1 Shop in Ring 3–4 und 5–6, 2 Goldminen.

## Architektur

```
Assets/Betaknight/
├── Scripts/
│   ├── Core/        Betaknight.Core       – reine Spiellogik, KEINE Unity-Abhängigkeit
│   │   ├── Hex/           HexCoord, HexDirection, HexLayout
│   │   ├── Map/           HexCell, HexMap, MapGenerator, MapGenerationConfig (Distanz-Bänder, Regeln, Garantien)
│   │   ├── Exploration/   ExplorationService (Fog of War, Auskundschaften)
│   │   ├── Turns/         TurnSystem
│   │   ├── Movement/      PlayerModel, MovementRules, Pathfinder
│   │   ├── Encounters/    Kleine und mittlere Events: Katalog, Optionen, Wirkungen, Resolver
│   │   ├── Run/           PlayerStats (HP, Gold, Splitter), KnightKit
│   │   ├── Runes/         Runen (Bedingungen der Logik-Tafel), Loadout, Runenwahl, RuneInventory
│   │   ├── Arena/         Kampfsimulator: Battle (Tick-Schleife), Combatant, LogicBoard, Conditions/ (Runen-Bedingungen + ConditionRegistry),
│   │   │                  Effects/ (ISkillEffect), Statuses/, Skills/ (SkillCatalog), BattleModifier, Playback/ (Wiedergabe + Protokolltext)
│   │   ├── Gear/          Ausrüstung: EquipmentCatalog, Equipment, Inventory, BoardFactory (Runen-Zeilen → Tafel), Sets/ (SetBonusRegistry)
│   │   ├── Combat/        ICombatResolver, ArenaCombatResolver, EnemyCatalog (Platzhalter-Resolver nur noch für Tests)
│   │   ├── Shop/          Shop-Bestand und Preise
│   │   ├── OverworldSession.cs              Fassade: Bewegung, kleine/mittlere Events, Runenwahl
│   │   ├── OverworldSession.MajorEvents.cs  Fassade: Kampf, Truhe, Goldmine, Shop
│   │   ├── OverworldSession.Gear.cs         Fassade: Ausrüstung, Skill-Zuordnung, Tafel umsortieren
│   │   ├── OverworldSession.Inventory.cs    Fassade: Inventar, anlegen/ablegen/tauschen, verwerfen, verkaufen
│   │   ├── OverworldSession.Mines.cs        Fassade: Goldminen-Raids und Verteidigung
│   │   ├── OverworldSession.Boss.cs         Fassade: Boss alle 25 Züge, Flucht durchs Portal
│   │   └── OverworldSession.Acts.cs         Fassade: Portal, Akte, Gegnerstufe pro Akt
│   └── Overworld/   Betaknight.Overworld  – Unity-Darstellung und Eingabe
│       ├── Config/        OverworldSettings (ScriptableObject)
│       ├── Views/         HexGridView, HexCellView, PlayerView, ProceduralSprites
│       ├── Controllers/   OverworldController, CameraFollow2D
│       ├── Input/         PointerInput (neues Input System und alter Input Manager)
│       ├── UI/            OverworldHud, Kit-Auswahl, Event-, Runen-, Shop-, Game-Over-Fenster, ArenaWindow, BoardEditorWindow, InventoryWindow, InventoryFullWindow (IMGUI-Platzhalter)
│       └── OverworldBootstrapper.cs
└── Tests/EditMode/  Unit-Tests für die Core-Logik
```

**Leitprinzipien**

- **Logik und Darstellung sind getrennt.** `Betaknight.Core` hat `noEngineReferences: true` und weiss nichts von Unity. Die Darstellung beobachtet die Logik nur über Events (`HexMap.CellChanged`, `PlayerModel.Moved`, `TurnSystem.TurnEnded`, `PlayerStats.Changed`, `OverworldSession.CellEntered`, `EncounterResolved`, `MajorEventResolved`, `RunEnded`).
- **Eine Fassade.** `OverworldSession` ist der einzige Einstieg für Spielaktionen. Die wichtigste Methode ist `TryStep(HexCoord)`: Sie prüft die Regeln, bewegt den Spieler, deckt den Nebel auf, beendet den Zug und löst das Feld-Event aus. Entscheidungen laufen über `ChooseEncounterOption`, `TakeRune`/`SkipRuneOffer` und die Shop-Methoden. Solange eine Entscheidung offen ist (`IsBusy`), ist Bewegung gesperrt.
- **Reproduzierbar.** Gleicher Seed ergibt die gleiche Karte. Der Seed steht im HUD.
- **Keine Singletons.** Der Bootstrapper erzeugt alle Objekte und übergibt Abhängigkeiten explizit.

**Hex-System:** Axiale Koordinaten (q, r), spitz-oben ausgerichtet, Y-Achse nach oben wie in Unity. Grundlage ist die Referenz von [Red Blob Games](https://www.redblobgames.com/grids/hexagons/).

**Fog of War:** `Hidden` (unsichtbar, nicht betretbar) → `Unexplored` (in Sichtweite, als «?» dargestellt, betretbar) → `Explored` (besucht, Inhalt bekannt, Teil der frei bereisbaren Routen).

## Phasenplan Meilenstein 1

| Phase | Inhalt | Status |
|---|---|---|
| 1 | Hex-Mathematik: Koordinaten, Nachbarn, Distanz, Ringe, Umrechnung Welt ↔ Hex | ✅ |
| 2 | Kartenmodell und Seed-basierte Generierung mit Gewichtungen, sicherer Startzone und Mindestanzahlen | ✅ |
| 3 | Fog of War mit drei Sichtbarkeitsstufen | ✅ |
| 4 | Zugsystem, Bewegungsregeln, Pfadsuche über bekannte Routen, Unterbrechung bei Gefahr | ✅ |
| 5 | Unit-Tests für die gesamte Core-Logik | ✅ |
| 6 | Unity-Darstellung: prozedurale Hexfelder, Spielfigur, Routenvorschau, Kamera | ✅ |
| 7 | Bootstrapper und Debug-HUD | ✅ |

## Tests

Im Unity-Editor: *Window → General → Test Runner → EditMode → Run All*.
Falls der Test Runner fehlt, im Package Manager das Paket **Test Framework** installieren.

## Vorbereitete Andockpunkte für die nächsten Meilensteine

| Konzept-Feature | Andockpunkt |
|---|---|
| Neue Rune | Eintrag in `RuneCatalog` + `ConditionRegistry.Register(id, parameter => new …Condition())` |
| Neuer Skill | Eintrag in `SkillCatalog` aus `ISkillEffect`-Bausteinen; neue Wirkung = neue `ISkillEffect`-Klasse mit `Apply` und `Describe` (Kennzahlen für den Tafel-Editor) |
| Neues Ausrüstungsteil | Eintrag in `EquipmentCatalog` |
| Neues Set | Teile mit Set-Id + `SetBonusRegistry.Register(id, name, teile => new …Set())` (ein `BattleModifier`) |
| Neuer Gegner | Eintrag in `EnemyCatalog` mit Stufenbereich und fester Tafel |
| Inverter-Rune | `NotCondition` / `condition.Not()` existiert bereits |
| Akt-spezifische Karten/Gegner | `OverworldSession.CreateNextAct(config, previous)` bekommt die Karten-Konfiguration; `TierAt` und `ActTierBonus` regeln die Stärke pro Akt |
| Gegneralarme beim Zurückreisen | `StepResult.FirstVisit == false` und `HexCell.VisitCount` |
| Hindernisse | `HexCell.IsWalkable` (Regeln und Pfadsuche berücksichtigen es bereits) |
| Neue Events | Eintrag in `EncounterCatalog.CreateDefault()` |

## Hinweise

- Ohne installiertes Input-System-Paket zeigt Unity eventuell eine Warnung zur fehlenden Referenz `Unity.InputSystem` in `Betaknight.Overworld.asmdef`. Das ist unkritisch, der Code fällt dann auf den alten Input Manager zurück.
- Die `.meta`-Dateien erzeugt Unity beim ersten Öffnen. Bitte mitcommitten.
