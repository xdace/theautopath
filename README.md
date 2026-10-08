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
| Button «Tafel bearbeiten» | Zeilen der Logik-Tafel umsortieren (▲▼). Rune und Skill pro Zeile getrennt wählen: den Skill mit ◀▶ aus den freien Skills der Sammlung (oder Basisangriff), mit × herausnehmen; die Rune aus dem Runen-Inventar einsetzen oder tauschen (↔) und ablegen («ab»). Darunter die Skill-Sammlung: freie Skills an eine Zeile setzen (→n), eingesetzte mit einer anderen Zeile tauschen |
| Button «Inventar» | Links die 7 Ausrüstungsplätze, rechts das Inventar, darunter Runentafel, Runen-Inventar und «Skills» (jedes Exemplar mit Stufe, Arten und Ort). Klick auf ein Teil zeigt Werte, passive Effekte, Set und den Vergleich mit dem angelegten Teil (grün besser, rot schlechter) |
| Arena nach jedem Kampf | Spielt den Kampf ab: Tempo 1×/2×/4×, Pause, «Überspringen». Tafel live mit Zustand pro Zeile, Zustände, Ressourcen und schwebende Zahlen. Danach Auswertung pro Zeile, Protokoll mit Filter, «Tafel bearbeiten» oder «Weiter» (siehe «Die Arena lesen») |
| Button «Shop öffnen» | Erscheint auf einem bereits besuchten Shop-Feld |
| Button «Neuer Run» | Zurück zur Kit-Auswahl, neue Karte |

Eine Reise stoppt automatisch auf feindlichen Feldern (Gegner, Boss), auf neu entdeckten Feldern und sobald ein Fenster offen ist.

### Ablauf eines frühen Runs

1. **Kit wählen:** Klingen-, Schild- oder Funkenritter. Jedes Kit bringt HP, Gold, eine Start-Rune und zwei Start-Skills mit (der erste sitzt an der Start-Rune, der zweite liegt frei in der Sammlung).
2. **Ring 1** um den Start hat nur kleine Events (Münzen, Kräuter, Runensplitter, Wegweiser). Sie wirken sofort und melden sich unten links.
3. **Ab Ring 2** kommen mittlere Events mit einer Entscheidung und die ersten Kämpfe. Truhen sind selten, Shops gibt es erst ab Ring 3. **Ab Ring 3** gibt es Elite-Gegner (Feld «E»): zwei Stufen stärker, mit mehr Leben und Schaden, dafür mehr Gold und oft eine Tafel-Erweiterung.
4. **3 Runensplitter** öffnen eine Runenwahl, ebenso jeder gewonnene Kampf und jede Truhe. Die Tafel startet mit 3 Zeilen und wächst bis 8 (siehe «Belohnungen»).
5. **Sammeln statt ersetzen:** Ausrüstung (12 Plätze) und Runen (6 Plätze) haben ein Inventar. Neue Teile werden angelegt, wenn ihr Platz frei ist, sonst kommen sie ins Inventar; verdrängte Teile (auch der Schild bei einer Zweihandwaffe) wandern ins Inventar. Eine neue Rune bei voller Tafel kommt ins Runen-Inventar. Runen behalten ihre Lagerfeuer-Stufe, beim Tauschen bleibt der Skill an der Zeile. Ist ein Inventar voll, wird gefragt: ein vorhandenes verwerfen oder das neue ablehnen. Wechseln geht jederzeit ausserhalb von Kampf und offenen Fenstern; Set-Boni, verwaiste Zeilen und Set-Runen folgen sofort. Im Shop lässt sich das Inventar für den halben Preis verkaufen. Das Inventar wandert durch die Akte mit.
6. **Aufbauen statt austauschen:** Belohnungen nach Kämpfen enthalten immer mindestens eine Verbesserung des aktuellen Builds. Eine doppelte Rune hebt die vorhandene eine Stufe (wie das Lagerfeuer), ein doppeltes Teil wertet das vorhandene auf (+1 bis +3). Nach jeder Verbesserung erscheint unten links eine Meldung, z. B. «Rüstungsbruch 100 % → 115 %» oder «Tafel 4 → 5 Zeilen». Das HUD zeigt Zeilen x/8 und eine kurze Build-Übersicht.
7. Fällt der Ritter in einem Kampf, ist der Run vorbei. Events auf der Oberwelt töten nie.

### Belohnungen

| Belohnung | Quelle | Wirkung |
|---|---|---|
| Rune | Runenwahl (Sieg, Truhe, 3 Splitter), Shop | Neue Rune auf eine freie Zeile oder ins Runen-Inventar; schon vorhandene Rune: +1 Stufe |
| Ausrüstung | Sieg (50 %), Truhe, Shop | Anlegen oder ins Inventar; schon vorhandenes Teil: +1 Stufe (bis +3), jede Stufe +50 % der Grundwerte |
| Skill | Sieg (35 %), Elite (60 %), Truhe (50 %), Mine (35 %), Runensplitter (30 %), Shop (1 Skill, 14 Gold) | Neues Exemplar frei in die Sammlung. Schon vorhanden: «Stufe erhöhen» (bis +3; jede Stufe +15 % Waffenschaden, +10 % Brennen pro Sekunde, +5 % Heilung) oder «Zweites Exemplar» für eine weitere Zeile |
| Tafel-Erweiterung: +1 Zeile | Garantiert bei jeder Boss-Flucht und beim Akt-Wechsel, als Wahl bei Elite-Siegen (50 %) und seltenen Truhen (10 %), Shop-Platz (20, 35, 50 … Gold pro Run, einer pro Shop) | Bis höchstens 8 Zeilen |
| Gold, Splitter | Kämpfe, Events, Minen, Boss-Flucht | Elite-Siege geben +4 Gold |

Kampfbelohnungen bieten bevorzugt Verbesserungen an: Stufe für einen eigenen Skill, Stufe für ein getragenes Teil, ein fehlendes Set-Teil, Stufe für eine vorhandene Rune oder eine Rune zu einem vorhandenen Tag. Mindestens eine Option ist immer eine Verbesserung. Skill-Angebote bevorzugen Skills, deren Art zum Build passt (×3 Gewicht): Arten eigener Skills, Ziele der passiven Effekte der Ausrüstung und die Runen (Klinge → Angriff, Schild → Schild, Funke → Schock, Glut → Feuer und Heilung, Phantom → Bewegung). Gegner skalieren weiter über Ring und Akt, die Schutzregeln (eine Aktion pro Tick, Überhitzung ab 90 s) bleiben. Alle Werte stehen in `Core/Run/ProgressionConfig.cs`.

### Kampf: die Logik-Tafel

Kämpfe laufen automatisch in festen Ticks (20 pro Sekunde). Jede Zeile der Tafel ist **Rune + Skill**: **Rune = Wann** (Bedingung), **Skill = Was** (ein Exemplar aus der Skill-Sammlung). Von oben nach unten feuert die erste Zeile, deren Bedingung erfüllt und deren Skill bereit ist. Ganz unten steht fest `[Immer] → Basisangriff`. Eine Zeile ohne Skill wird grau und übersprungen; das passiert nur, wenn man den Skill bewusst herausnimmt.

- **Skills sind eigene Exemplare** (`Core/Skills/`: `SkillInstance`, `SkillCollection`): Skill-Id, Stufe, Instanz-Id. Jedes Exemplar sitzt an höchstens einem Ort (`ISkillHolder`, heute eine Tafelzeile, später ein Ausrüstungs-Sockel). Die Stufe gehört dem Exemplar, nicht dem Skill. Die Sammlung hat keine Obergrenze und wandert durch die Akte mit. Den Basisangriff gibt es ohne Exemplar beliebig oft.
- **Ausrüstung = Werte + passive Effekte.** Teile liefern keine Skills mehr. Die Waffe bestimmt Waffenschaden und Basisangriff, alle Teile geben Werte und manche passive Effekte auf eine Skill-Art, z. B. «Schock-Skills +20 % Wirkung» (Schaden, Brennen, Heilung, Chancen) oder «Schild-Skills −1 s Cooldown». Ablegen eines Teils nimmt keinen Skill weg.
- **Skill-Arten** stehen im `SkillCatalog` (Angriff, Schild, Feuer, Schock, Heilung, Bewegung; ein Skill kann mehrere haben) und im Tooltip. Sie dienen nur als Ziel der passiven Effekte und für passende Angebote.
- **Gegner** haben feste Tafeln wie bisher.

#### Cast-Zeit

Jede Ausführung braucht ihre **Cast-Zeit** (das Ausholen bis zur Wirkung), auch Wiederholungen durch Echo. Loops werden nur über Cast-Zeiten begrenzt, nicht über feste Bremsen.

- Grund-Cast-Zeiten: schnell 0,4 s (Entzünden, Schildwall, Bodenanker, Schubdüsen, Schockstich), mittel 0,8 s (Rüstungsbruch, Schildschlag, Kühlmittel, Blendgranate, Echo), schwer 1,5 s (EMP-Schildschlag, Not-Reparatur, Bohrstoß). Ab 1 s gilt ein Cast als sichtbare Aufladung, die Betäubung und «Gegner lädt auf» kontern. Der Basisangriff holt wie bisher 2/3 seines Intervalls aus.
- Ausrüstung, Tag-Stufen und später Module ändern die Cast-Zeit in Prozent («Schock-Skills −20 % Cast-Zeit»). Alle Prozente addieren sich; das Ergebnis fällt nie unter die **Untergrenze 0,1 s** (2 Ticks, `CastTime.DefaultMinTicks`, pro Kampf `BattleSetup.MinCastTicks`). Infozeile und Tooltip zeigen «Cast 0,3 s (Grund 0,4 s)», der Arena-Balken die Cast-Zeit der laufenden Aktion.
- **Echo** merkt sich den letzten eigenen Skill und startet ihn nach der eigenen Erholung als eigene Ausführung mit dessen Cast-Zeit, ohne Cooldown. Betäubung bricht wie jede Aktion ab. Im Protokoll ist die Wiederholung markiert (`BattleEvent.IsRepeat`).
- Ungedeckelte Stellen (Überlast-Tempo, Phantom-Cooldowns) bleiben bewusst stark. Der Test mit absurden Werten (Cast −100000 %, Riesen-Tempo, Riesen-Rüstung) läuft stabil durch, jede Ausführung hat ihren Cast.

#### Synergie-Tags auf der Ausrüstung

Jedes Teil trägt 1–2 Tags (vorläufig, als Daten in `SynergyRegistry.CreateDefault`): **Hitze, Ladung, Phantom, Takt, Toxin, Schrott**. Gezählt werden nur getragene Teile. Schwellen bei **2/4/6** Teilen schalten je Tag eine Stufe frei, die Stufen gelten zusammen:

| Tag | 2 | 4 | 6 |
|---|---|---|---|
| Hitze | Feuer-Skills −20 % Cast-Zeit | +25 % Schaden gegen brennende Gegner | Basisangriffe setzen Brennen (3 s) |
| Ladung | +10 % Block | Schock-Skills −25 % Cast-Zeit | Jeder Block: alle Cooldowns −0,5 s |
| Phantom | +10 % Ausweichen | Bewegung-Skills −30 % Cast-Zeit, −1 s Cooldown | Nach Ausweichen: nächster Angriff +50 % |
| Takt | +10 % Angriffstempo | Alle Skills −15 % Cast-Zeit | Alle Skills weitere −15 % |
| Toxin | Skill-Treffer vergiften (bis 5 Stapel, 1 Schaden/s je Stapel) | +1 Schaden je Gift-Stapel | Auch Basisangriffe vergiften |
| Schrott | +1 Gold je Gegner | +3 Rüstung | Angriffe ignorieren Rüstung |

**Duos:** Haben zwei Tags gleichzeitig mindestens 4, greift ihr Duo: Glutrhythmus (Hitze + Takt), Phasenschild (Ladung + Phantom), Brandgift (Hitze + Toxin), Schrottkondensator (Schrott + Ladung), Geisterschritt (Phantom + Takt), Säurefraß (Toxin + Schrott). Bis ein Duo einmal in einem Kampf aktiv war, zeigt das Spiel es als Silhouette «???»; danach steht es mit Namen und Wirkung im **Rezeptbuch** (Inventar). Das Rezeptbuch gilt für den Run und wandert durch die Akte mit.

**Sets bleiben.** Sie behalten ihre Boni und die Rune «Ladung voll»; ihre Teile tragen zusätzlich passende Tags (Überlast → Hitze/Takt, Aegis → Ladung, Schrott-Ernter → Schrott, Phantom-Signal → Phantom). Grund: Die Set-Boni sind eigene Mechaniken (Tempo-Stapel, Entladung, Minen, Ausweich-Obergrenze), die als Tag-Stufen zu speziell wären, und ein Set ist mit 3 Teilen erreichbar, eine Tag-Stufe 6 erst mit fast voller Ausrüstung. So zahlen Set-Teile auf beide Systeme ein.

Anzeige: Das HUD zeigt die Zähler («Ladung 3/4», erreichte Schwellen gelb) und aktive Duos. Das Inventar listet alle Tags mit ihren Stufen und das Rezeptbuch. Angebote, Shop und das Inventar zeigen, was ein Teil bewirken würde («→ Ladung 4/6: Schwelle!», «→ Duo frei: ???»).

- **Skill-Kennzahlen im Tafel-Editor:** Unter jedem Skill (auch beim Durchblättern mit ◀▶ und beim festen Basisangriff) steht eine Infozeile: Wirkung, Schaden, CD, Cast-Zeit und Erholung. Schaden steht doppelt, als Prozent vom Waffenschaden und als Wert mit der aktuellen Ausrüstung samt aktiver Set-Boni, gegen ein Ziel ohne Rüstung, ohne Block und Krit (z. B. «120 % Waffenschaden ≈ 10 an allen Gegnern», «Brennen 50 % Waffenschaden/s ≈ 3/s, 15 über 5 s», «20 % Chance: betäubt 1 s»). Skills ohne Schaden zeigen «kein Schaden». Mit der Maus über der Zeile erscheinen alle Details als Tooltip, bei vielen Zeilen scrollt das Fenster.
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

### Die Arena lesen

Die Arena zeigt nicht nur, *welche* Zeile feuert, sondern auch *warum* die anderen nicht.

- **Tafel live:** Jede Zeile hat links einen Farbstreifen (ihre Farbe) und ein Zustand-Symbol: ✔ Bedingung erfüllt und Skill bereit, ✖ Bedingung nicht erfüllt, ⏳ Skill im Cooldown (mit Restzeit-Balken unter der Zeile), ⌀ verwaist (kein Skill). Die feuernde Zeile leuchtet gelb. Mit der Maus über einer Zeile steht der Zustand jetzt und der letzte Grund, warum sie übersprungen wurde, z. B. «4,2s: Skill im Cooldown (noch 1,8 s)».
- **Gründe fürs Überspringen:** «Bedingung nicht erfüllt», «Skill im Cooldown (noch x,y s)», «verwaist (kein Skill)» und «Bedingung erfüllt, aber Aktion läuft» (eine höhere Zeile war bereit, während eine Aktion lief, die sich nicht abbrechen lässt; nur das Ausholen eines Basisangriffs darf noch unterbrochen werden).
- **Kämpfer:** Unter dem Lebensbalken stehen Ressourcen als Balken (Hitze, Ladung, Tempo-Stapel …) und aktive Zustände als kleine Kästchen (Brand, Betäubt, R.-Bruch, Schild …) mit Restdauer, Restzeit-Balken und Stapeln (×2). Tooltip mit vollem Namen.
- **Schwebende Zahlen am Ziel:** Schaden weiss, Krit gelb und grösser, Heilung grün, «Block» und «Ausgewichen» als Wort. Kommt die Wirkung von einer Tafel-Zeile, liegt die Zahl auf einem Feld in der Farbe dieser Zeile. Auch Brennen zählt zur Zeile, die es gesetzt hat.
- **Auswertung nach dem Kampf** (vor «Weiter»): Tabelle pro Zeile mit «gefeuert», Schaden und Heilung gesamt, Anteil am Gesamtschaden, wie oft übersprungen und häufigster Grund. Dazu Hinweise wie «Zeile 3 hat nie gefeuert: Bedingung nie erfüllt» oder «Zeile 2 (Bohrstoß) macht 64 % des Schadens». «Tafel bearbeiten» öffnet direkt den Tafel-Editor.
- **Protokoll-Filter:** «Alles», «Meine Aktionen» oder «Nur Schaden». Einträge einer Zeile tragen deren Farbstreifen.
- **Bei 4×:** Hervorhebungen (feuernde Zeile, Treffer-Blitz) bleiben mindestens 0,35 s Echtzeit sichtbar, schwebende Zahlen gut 1 s.

Im Core: `Battle` hält bei jeder Entscheidung des Spielers eine `BattleDecision` fest (gewählte Zeile, Zustand und Cooldown jeder Zeile), nur wenn eine neue Aktion startet oder eine höhere Zeile auf eine laufende Aktion warten muss, nicht jeden Tick. Bedingungen sind zustandslos, das Mitprüfen ändert keinen Kampf (Test mit Fingerabdrücken aus Bot-Runs). Schaden, Heilung und Zustände einer Aktion tragen die Zeile (`BattleEvent.RowIndex`). `BattleReport.Create(result)` rechnet die Auswertung aus dem Protokoll, `BattlePlayback` liefert Zeilen-Zustand, Zustände, Ressourcen, schwebende Zahlen und gefilterte Protokollzeilen.

### Feldsymbole (Platzhalter)

| Symbol | Inhalt |
|---|---|
| `?` | Unerforscht, Inhalt unbekannt |
| Kleinbuchstabe (`c`, `h`, `s`, `w`, `t`, `x`) | Kleines Event: Münzen, Kräuter, Splitter, Wegweiser, Händlerspuren, Dornen |
| Grossbuchstabe (`F`, `W`, `S`, `M`, `V`) | Mittleres Event: Lagerfeuer, Wanderer, Blutschrein, Söldner, Vorrat |
| `!` | Gegner |
| `B` | Boss |
| `E` | Elite-Gegner |
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
│   │   ├── Run/           PlayerStats (HP, Gold, Splitter), KnightKit, ProgressionConfig
│   │   ├── Runes/         Runen (Bedingungen der Logik-Tafel), Loadout, Runenwahl, RuneInventory
│   │   ├── Arena/         Kampfsimulator: Battle (Tick-Schleife), Combatant, LogicBoard, Conditions/ (Runen-Bedingungen + ConditionRegistry),
│   │   │                  Effects/ (ISkillEffect), Statuses/, Skills/ (SkillCatalog), BattleModifier, Playback/ (Wiedergabe + Protokolltext),
│   │   │                  Insight/ (BattleDecision: Gründe je Zeile, BattleReport: Auswertung nach dem Kampf)
│   │   ├── Gear/          Ausrüstung: EquipmentCatalog, Equipment, Inventory, BoardFactory (Runen-Zeilen → Tafel), Sets/ (SetBonusRegistry),
│   │   │                  Synergies/ (SynergyRegistry: Tags, Schwellen, Duos als Daten; Wirkungen als BattleModifier)
│   │   ├── Combat/        ICombatResolver, ArenaCombatResolver, EnemyCatalog (Platzhalter-Resolver nur noch für Tests)
│   │   ├── Shop/          Shop-Bestand und Preise
│   │   ├── OverworldSession.cs              Fassade: Bewegung, kleine/mittlere Events, Runenwahl
│   │   ├── OverworldSession.MajorEvents.cs  Fassade: Kampf, Truhe, Goldmine, Shop
│   │   ├── Skills/        SkillInstance (Exemplar), ISkillHolder (Ort), SkillCollection (Sammlung)
│   │   ├── OverworldSession.Gear.cs         Fassade: Ausrüstung, Skill-Kennzahlen mit passiven Boni, Tafel umsortieren
│   │   ├── OverworldSession.Synergies.cs    Fassade: Tag-Zähler, aktive Duos, Rezeptbuch, Vorschau für Angebote
│   │   ├── OverworldSession.Skills.cs       Fassade: Skill-Sammlung, Einsetzen/Tauschen, Erhalt, Stufe oder zweites Exemplar, Angebote
│   │   ├── OverworldSession.Inventory.cs    Fassade: Inventar, anlegen/ablegen/tauschen, verwerfen, verkaufen
│   │   ├── OverworldSession.Progression.cs  Fassade: Tafel-Erweiterung, Stufen, Angebote mit Verbesserung
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
| Neuer Skill | Eintrag in `SkillCatalog` aus `ISkillEffect`-Bausteinen mit mindestens einer Skill-Art (`kinds:`); neue Wirkung = neue `ISkillEffect`-Klasse mit `Apply` und `Describe` (Kennzahlen für den Tafel-Editor) |
| Neues Ausrüstungsteil | Eintrag in `EquipmentCatalog` |
| Neues Set | Teile mit Set-Id + `SetBonusRegistry.Register(id, name, teile => new …Set())` (ein `BattleModifier`) |
| Neuer Synergie-Tag oder Duo | Eintrag in `SynergyRegistry.CreateDefault` (Text, passive Effekte, `BattleModifier`-Fabrik je Schwelle); Teile bekommen die Tag-Id über `tags:` |
| Neuer Gegner | Eintrag in `EnemyCatalog` mit Stufenbereich und fester Tafel |
| Inverter-Rune | `NotCondition` / `condition.Not()` existiert bereits |
| Akt-spezifische Karten/Gegner | `OverworldSession.CreateNextAct(config, previous)` bekommt die Karten-Konfiguration; `TierAt` und `ActTierBonus` regeln die Stärke pro Akt |
| Gegneralarme beim Zurückreisen | `StepResult.FirstVisit == false` und `HexCell.VisitCount` |
| Hindernisse | `HexCell.IsWalkable` (Regeln und Pfadsuche berücksichtigen es bereits) |
| Neue Events | Eintrag in `EncounterCatalog.CreateDefault()` |

## Hinweise

- Ohne installiertes Input-System-Paket zeigt Unity eventuell eine Warnung zur fehlenden Referenz `Unity.InputSystem` in `Betaknight.Overworld.asmdef`. Das ist unkritisch, der Code fällt dann auf den alten Input Manager zurück.
- Die `.meta`-Dateien erzeugt Unity beim ersten Öffnen. Bitte mitcommitten.
