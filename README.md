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
| Taste **B** / Button «Build (B)» | Fenster «Build» (ersetzt den Tafel-Editor): Mitte die Logik-Tafel, eine Zeile pro Rune + Skill (Rune mit Stufen-Abzeichen und Wachstum, Skill mit Kurzwerten, Details im Tooltip). Links das Skill-Inventar, rechts Module und Runen-Inventar oder das Rezeptbuch, oben die Stat-Leiste |
| Ziehen im Build | Skill auf eine Zeile = einsetzen (besetzte Zeile: tauschen), Skill aus der Zeile ins Skill-Inventar = herausnehmen. Zeile am Griff ≡ auf eine andere Zeile = umsortieren. Rune aus dem Runen-Inventar auf eine Zeile = tauschen, auf einen freien Platz = neue Zeile; Rune einer Zeile ins Runen-Inventar = ablegen. Modul auf einen freien Platz ◇ an Baustein oder Skill = einsetzen, zurück in die Modul-Liste = abnehmen; Klick auf einen Auslöser wählt sein Ziel |
| Taste **I** / Button «Inventar (I)» | Fenster «Inventar» wie im Rollenspiel: Figur des Ritters mit den 7 Plätzen (Helm, Handschuhe, Brust, Beinschienen, Stiefel, Waffe, Schild), daneben Details zum gewählten Teil, Tags und Sets, darunter das Item-Raster (12 Zellen) für Ausrüstung und später Verbrauchsgegenstände. Runen und Skills stehen nur im Build |
| Ziehen im Inventar | Teil aus dem Raster auf seinen Platz an der Figur = anlegen (das alte Teil kommt in dieselbe Zelle; ein falscher Platz wird rot und verweigert), Teil von der Figur ins Raster = ablegen (auf ein passendes Teil: tauschen). Zelle auf Zelle = tauschen, auf eine leere Zelle = verschieben; die Reihenfolge bleibt erhalten, auch über Akte |
| Doppel- oder Rechtsklick | Kurzweg: Skill, Rune oder Modul in die erste passende Zeile bzw. heraus; Teil anlegen bzw. ablegen. Einfacher Klick auf ein Teil zeigt die Details (Anlegen, Ablegen, Verwerfen) |
| Stat-Leiste | In beiden Fenstern: HP, Waffenschaden, Angriffe/s, Rüstung, Ausweichen, Block, Krit, Präzision, Flächenschaden und aktive Set-Boni, Tag-Stufen und Duos. Maus über ein Teil oder Ziehen eines Teils zeigt die Änderung («Rüstung 6 → 9», grün besser, rot schlechter); bei Skills, Runen und Modulen stehen die Kurzwerte darunter |
| Während Kampf und offenen Entscheidungen | Beide Fenster bleiben lesbar, Ziehen ist gesperrt. «B» und «I» schliessen sich gegenseitig, Esc schliesst |
| Arena nach jedem Kampf | Spielt den Kampf ab: Tempo 1×/2×/4×, Pause, «Überspringen». Tafel live mit Zustand pro Zeile, Zustände, Ressourcen und schwebende Zahlen. Danach Auswertung pro Zeile, Protokoll mit Filter, «Build öffnen» oder «Weiter» (siehe «Die Arena lesen») |
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
| Skill | Sieg (35 %), Elite (60 %), Truhe (50 %), Mine (35 %), Runensplitter (30 %), Shop (1 Skill, 14 Gold) | Neues Exemplar frei in die Sammlung. Schon vorhanden: «Wachstum +5» für das vorhandene Exemplar (siehe Wachsen und Evolution) oder «Zweites Exemplar» für eine weitere Zeile |
| Tafel-Erweiterung: +1 Zeile | Garantiert bei jeder Boss-Flucht und beim Akt-Wechsel, als Wahl bei Elite-Siegen (50 %) und seltenen Truhen (10 %), Shop-Platz (20, 35, 50 … Gold pro Run, einer pro Shop) | Bis höchstens 8 Zeilen |
| Modul (selten) | Elite (35 %), Truhe (15 %), garantiert bei jeder Boss-Flucht, Shop (in 50 % der Shops ein Platz, 30 Gold) | Neues Exemplar frei in die Sammlung; schon vorhanden: «Stufe erhöhen» (+1, wo das Modul Stufen hat) oder «Weiteres Exemplar» |
| Gold, Splitter | Kämpfe, Events, Minen, Boss-Flucht | Elite-Siege geben +4 Gold |

Kampfbelohnungen bieten bevorzugt Verbesserungen an: Stufe für einen eigenen Skill, Stufe für ein getragenes Teil, ein fehlendes Set-Teil, Stufe für eine vorhandene Rune oder eine Rune zu einem vorhandenen Tag. Mindestens eine Option ist immer eine Verbesserung. Skill-Angebote bevorzugen Skills, deren Art zum Build passt (×3 Gewicht): Arten eigener Skills, Ziele der passiven Effekte der Ausrüstung und die Runen (Klinge → Angriff, Schild → Schild, Funke → Schock, Glut → Feuer und Heilung, Phantom → Bewegung). Gegner skalieren weiter über Ring und Akt, die Schutzregeln (eine Aktion pro Tick, Überhitzung ab 90 s) bleiben. Alle Werte stehen in `Core/Run/ProgressionConfig.cs`.

### Kampf: die Logik-Tafel

Kämpfe laufen automatisch in festen Ticks (20 pro Sekunde). Jede Zeile der Tafel ist **Rune + Skill**: **Rune = Wann** (Bedingung), **Skill = Was** (ein Exemplar aus der Skill-Sammlung). Von oben nach unten feuert die erste Zeile, deren Bedingung erfüllt und deren Skill bereit ist. **Erfüllte Zeilen warten, bis sie dran sind – höhere Zeilen zuerst** (Warteschlange, siehe unten). Ganz unten steht fest `[Immer] → Basisangriff`. Eine Zeile ohne Skill wird grau und übersprungen; das passiert nur, wenn man den Skill bewusst herausnimmt.

- **Skills sind eigene Exemplare** (`Core/Skills/`: `SkillInstance`, `SkillCollection`): Skill-Id, Wachstum (daraus die Stufe), Instanz-Id. Jedes Exemplar sitzt an höchstens einem Ort (`ISkillHolder`, heute eine Tafelzeile, später ein Ausrüstungs-Sockel). Wachstum und Stufe gehören dem Exemplar, nicht dem Skill. Die Sammlung hat keine Obergrenze und wandert durch die Akte mit. Den Basisangriff gibt es ohne Exemplar beliebig oft.
- **Ausrüstung = Werte + passive Effekte.** Teile liefern keine Skills mehr. Die Waffe bestimmt Waffenschaden und Basisangriff, alle Teile geben Werte und manche passive Effekte auf eine Skill-Art, z. B. «Schock-Skills +20 % Wirkung» (Schaden, Brennen, Heilung, Chancen) oder «Schild-Skills −1 s Cooldown». Ablegen eines Teils nimmt keinen Skill weg.
- **Skill-Arten** stehen im `SkillCatalog` (Angriff, Schild, Feuer, Schock, Heilung, Bewegung; ein Skill kann mehrere haben) und im Tooltip. Sie dienen nur als Ziel der passiven Effekte und für passende Angebote.
- **Gegner** haben feste Tafeln wie bisher.

#### Skills sind der Hauptschaden (A-12)

Der Basisangriff ist **Füller und Motor**: Er macht beim Ritter nur noch **60 % Waffenschaden**, und **jeder Treffer verkürzt alle laufenden Skill-Cooldowns um 0,25 s** (Ausweicher zählen nicht). Den Schaden tragen die Skills. Gegner behalten ihren Basisangriff mit 100 %.

Die Regel steht als Daten in `Arena/SkillBudget.cs` (`SkillBudgetConfig.Default`) und wird von einem Test für jeden Skill im Katalog geprüft:

- **Schadens-Skill** (Art Angriff oder Feuer, mit Schaden): Schaden pro Sekunde Aktionszeit (Cast + Erholung) mindestens **2,5×** Basisangriff pro Sekunde (60 % bei 1 s Takt), **Flächen-Skills pro Ziel mindestens 1,5×**. Brennen zählt mit seiner ganzen Dauer, Chancen anteilig.
- **Längerer Cooldown → mehr Wirkung:** Jede Sekunde Cooldown über 3 s verlangt 5 % mehr.
- **Nutzen-Skills** (Betäubung, Schild, Blendung, Heilung) machen höchstens 60 % des Budgets als Schaden und punkten mit ihrer Wirkung.

| Skill | Schaden | Budget | Bemerkung |
|---|---|---|---|
| Schockstich | 80 % | 75 % | 0,5 s Aktion, 3 s CD |
| Rüstungsbruch | 190 % | 188 % | plus Rüstung −50 % für 6 s |
| Entzünden | Brennen 70 %/s × 5 s = 350 % | 104 % | Schaden kommt verzögert |
| Bohrstoß | 180 % an allen | 178 % pro Ziel | Fläche |
| Blitzlanze / Säurebohrer / Feuersturm | 120 % / 200 % an allen / 60 % + 350 % Brennen an allen | erfüllt | Evolutionen |
| Schildschlag | 70 % | Nutzen | betäubt jetzt 2 s |
| EMP-Schildschlag | 50 % an allen | Nutzen | betäubt alle 3 s |
| Schrottramme | 90 % ohne Rüstung | Nutzen | betäubt 2,5 s |

**Gegner-HP** liegen bei 65 % der früheren Werte (`EnemyCatalog.HpPercent`), damit frühe Kämpfe mit dem Start-Kit (1–2 Skills) gut schaffbar bleiben. Der Boss bleibt unbesiegbar; die 15 s bis zum Portal schafft jedes Start-Kit (Test). Gold pro Kampf bleibt unverändert, weil Kämpfe eher kürzer werden. Abgleich mit dem Testspieler über 60 Runs: ungefähr gleich viele Runs erreichen Akt 3 wie vorher. Der Schildritter startet mit zwei Nutzen-Skills und bleibt beim Basisangriff-Anteil hoch, bis er Schadens-Skills findet.

**Anzeige:** Die Kampf-Auswertung zeigt den Anteil gross über der Tabelle («Basisangriff 28 % · Skills 72 %», grün bis 30 %). Der Testspieler-Bericht enthält pro Run `basicAttackSharePercent`, `basicAttackShareFromAct2Percent` (Ziel höchstens 30 %) und `basicAttackShareByAct`, das Log den Anteil in der Run-Zeile.

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

**Duos:** Haben zwei Tags gleichzeitig mindestens 4, greift ihr Duo: Glutrhythmus (Hitze + Takt), Phasenschild (Ladung + Phantom), Brandgift (Hitze + Toxin), Schrottkondensator (Schrott + Ladung), Geisterschritt (Phantom + Takt), Säurefraß (Toxin + Schrott). Bis ein Duo einmal in einem Kampf aktiv war, zeigt das Spiel es als Silhouette «???»; danach steht es mit Namen und Wirkung im **Rezeptbuch** (Inventar). Das Rezeptbuch wandert durch die Akte mit und bleibt über Runs gespeichert (siehe Wachsen und Evolution).

**Sets bleiben.** Sie behalten ihre Boni und die Rune «Ladung voll»; ihre Teile tragen zusätzlich passende Tags (Überlast → Hitze/Takt, Aegis → Ladung, Schrott-Ernter → Schrott, Phantom-Signal → Phantom). Grund: Die Set-Boni sind eigene Mechaniken (Tempo-Stapel, Entladung, Minen, Ausweich-Obergrenze), die als Tag-Stufen zu speziell wären, und ein Set ist mit 3 Teilen erreichbar, eine Tag-Stufe 6 erst mit fast voller Ausrüstung. So zahlen Set-Teile auf beide Systeme ein.

Anzeige: Das HUD zeigt die Zähler («Ladung 3/4», erreichte Schwellen gelb) und aktive Duos. Das Inventar listet alle Tags mit ihren Stufen und das Rezeptbuch. Angebote, Shop und das Inventar zeigen, was ein Teil bewirken würde («→ Ladung 4/6: Schwelle!», «→ Duo frei: ???»).

- **Skill-Kennzahlen im Build:** Jede Zeile und jeder Skill im Skill-Inventar zeigt Kurzwerte (erste Wirkung und Cooldown), der Tooltip alles: Wirkung, Schaden, CD, Cast-Zeit und Erholung. Schaden steht doppelt, als Prozent vom Waffenschaden und als Wert mit der aktuellen Ausrüstung samt aktiver Set-Boni, gegen ein Ziel ohne Rüstung, ohne Block und Krit (z. B. «180 % Waffenschaden ≈ 10 an allen Gegnern», «Brennen 70 % Waffenschaden/s ≈ 4/s, 20 über 5 s», «20 % Chance: betäubt 1 s»). Skills ohne Schaden zeigen «kein Schaden». Bei kleinen Auflösungen scrollt jeder Bereich für sich.
  Die Werte stehen nicht in der UI, sondern kommen aus den Effekten: Jede `ISkillEffect` meldet über `Describe(SkillInfoBuilder)` ihre Kennzahlen mit denselben Formeln wie `Apply`. `SkillInfo.Create(skill, stats)` fasst sie zusammen, `OverworldSession.SkillUserStats()` liefert die Werte des Ritters zu Kampfbeginn. Neue Effekte müssen `Describe` umsetzen und erscheinen dann automatisch richtig.
- 7 Ausrüstungsplätze (Helm, Handschuhe, Brust, Beinschienen, Waffe, Schild, Stiefel). Zweihandwaffen sperren den Schild.
- 4 Sets mit Boni ab 2 und 3 Teilen: Überlast-Protokoll, Aegis-Firewall, Schrott-Ernter, Phantom-Signal.
- Schutzregeln statt Balance-Bremsen: höchstens eine Aktion pro Tick, Reaktionen erst im nächsten Tick, ab 90 s Überhitzung. Kaputte Builds sind erlaubt, die Engine bleibt stabil.
- Lagerfeuer kann eine Rune eine Stufe verstärken (z. B. «HP unter 30 %» → «HP unter 40 %»).
- Sets: Fortschritt steht im HUD, in der Stat-Leiste und im Inventar. Teile angefangener Sets kommen 3× häufiger in Angebote, Shops verkaufen 2 Teile. Aegis-Firewall (ab 2 Teilen) schaltet die Rune «Ladung voll» frei.
- **Goldminen-Verteidigung:** Alle 8 Züge wird eine eigene Mine angegriffen (rot, «!G»). 6 Züge Zeit, sonst ist sie verloren, bis sie zurückerobert ist. Der Kampf dort läuft «auf der Goldmine» (Schrott-Ernter, Rune «Auf Goldmine»).
- **Boss alle 25 Züge:** Er taucht beim Ritter auf und ist unbesiegbar. Wer 15 s überlebt, entkommt durchs Portal (+8 Gold, +2 Splitter). Ausweichen und Betäuben helfen, Phantom-Signal ist dafür gebaut.
- **Akte:** Das Fluchtportal führt auf eine neue Karte (Akt 2, 3 …). Ritter, Ausrüstung, Tafel, Gold und Splitter kommen mit, eroberte Minen bleiben zurück. Das Portal heilt 50 % der Max-HP, Gegner sind pro Akt 2 Stufen stärker, der Zugzähler und der Boss-Takt laufen weiter.

Konzept: `/mnt/project-files/design/kampfsystem-konzept.md` im Projekt.

#### Module und Auslöser

Module sind wie Skills eigene Exemplare (`Core/Modules/`: `ModuleInstance`, `ModuleCollection`, Katalog und Regeln) mit Stufe und Sammlung, die durch die Akte mitwandert. Skill-Exemplare und Logikbausteine (Tafel-Zeilen) haben je **1 Modul-Platz**, durch Wachstum bis zu 3; ein Modul sitzt an genau einem Ort (`IModuleHolder`). Der Basisangriff hat keinen Platz. Verschwindet ein Ort (Zeile abgelegt), wird sein Modul wieder frei.

| Modul | Art | Wirkung (Stufe 0 / +1) |
|---|---|---|
| Mehrfach | Skill | Wirkung wird nach erneuter Cast-Zeit wiederholt (×2 / ×3), ohne weiteren Cooldown |
| Fläche | Skill | Schaden trifft alle Gegner mit 70 % / 85 % |
| Kette | Skill | Zielgerichtete Wirkungen treffen 1 / 2 weitere Gegner |
| Blutzoll | Skill | Kostet 5 % / 4 % Max-HP statt Cooldown |
| Schnellcast | Skill | −30 % / −40 % Cast-Zeit, +30 % Cooldown |
| Umkehren | Baustein | NICHT: die Zeile gilt, wenn die Bedingung nicht erfüllt ist |
| Verlängern | Baustein | Die Bedingung gilt 1 s / 1,5 s länger |
| Schwelle | Baustein | +10 / +15 Prozentpunkte bei Runen mit Prozent-Schwelle («HP unter 30 %» → 40 %), höchstens 100 % |
| Auslöser | beides | Am Skill «nach Ausführung», am Baustein «wenn erfüllt» (beim Wechsel von nicht erfüllt zu erfüllt): löst ein Ziel aus |

**Auslöser** zielen per stabiler Id auf ein Skill-Exemplar oder eine Zeile, nicht auf eine Zeilennummer: Zeilen umsortieren oder den Skill umsetzen nimmt das Ziel mit. Ein Skill-Ziel, das gerade nicht an der Tafel sitzt, löst nichts aus. Das ausgelöste Ziel überspringt seine Bedingung, castet aber ganz normal mit Cast-Zeit und setzt seinen Cooldown. Kann es gerade nicht starten (Cooldown, Aktion läuft, betäubt), verfällt der Auslöser nicht, sondern das Ziel wird **eingereiht** (siehe Warteschlange). Nur ein verwaistes Ziel verfällt. **Kreise sind erlaubt**, das sind die Loops; begrenzt werden sie nur durch Cast-Zeiten und Cooldowns.

Datenmodell: Die Tafel im Kampf ist ein Graph (`Arena/Graph/LogicGraph.cs`): Knoten sind Baustein und Skill jeder Zeile, Kanten sind Auslöser (`GraphEdgeKind.Trigger`; UND/ODER können später als weitere Kantenarten dazukommen). Der Kampf bleibt deterministisch: gleiche Seeds ergeben dieselben Kämpfe, auch mit Kreisen.

Build-Fenster: In jeder Zeile stehen die Modul-Plätze von Baustein und Skill (◆ besetzt, ◇ frei). Ein Modul wird auf einen freien Platz gezogen und zurück in die Modul-Liste abgenommen. Ein Klick auf einen Auslöser wählt das nächste Ziel (alle Zeilen, dann «kein Ziel»). Rechts an den Zeilen sind Auslöser als Linien gezeichnet (orange vom Skill, türkis vom Baustein, Pfeil am Ziel), jede Verbindung auf eigener Spur, sodass Kreise sichtbar bleiben. Das HUD zeigt pro Zeile ◆ (Module) und ↪ (Auslöser-Ziel), die Modul-Liste im Build alle Module mit Ort.

#### Warteschlange (A-13)

**Erfüllte Zeilen warten, bis sie dran sind – höhere Zeilen zuerst.** Ist die Bedingung einer Zeile erfüllt, der Skill kann aber gerade nicht starten (eine Aktion läuft, Skill im Cooldown, betäubt), wird die Zeile eingereiht statt übersprungen. Das gilt für Ereignis-Bausteine («Nach Block», «Nach Krit» …), Zustands-Bausteine und Auslöser.

- **Ausführung nach Priorität:** Ist der Ritter frei, startet die höchste Zeile aus der Warteschlange, deren Skill bereit ist, ohne ihre Bedingung erneut zu prüfen (sie war erfüllt, der Skill ist verdient). Ist kein eingereihter Skill bereit, füllt der Basisangriff die Lücke und wird beim Ausholen abgebrochen, sobald einer bereit wird.
- **Höchstens einmal:** Jede Zeile steht höchstens einmal in der Warteschlange; erneutes Erfüllen, während sie wartet, ändert nichts. Eine durchgehend erfüllte Bedingung (z. B. «HP unter 30 %») reiht während des Cooldowns nicht erneut ein, nur ein neues Erfüllen tut das. Am Kampfende leert sich die Warteschlange.
- **Bonus und Ursprung gehen mit:** Die Ausführung behält die Schwierigkeits-Stufe der Zeile, die sie verdient hat (bei Auslösern die höhere von Quelle und Ziel), und den Auslöser-Ursprung.
- **Gegner** haben dieselbe Warteschlange.
- **Achtung bei Zeilen ohne Cooldown:** Eine wartende höhere Zeile hat immer Vorrang. Eine Zeile «Immer» mit einem Skill ohne Cooldown ganz oben hält also tiefere Zeilen dauerhaft hin.

Die Regeln stehen als Daten in `Arena/RowQueue.cs` (`RowQueueConfig.Default`, pro Kampf über `BattleSetup.Queue`): `Enabled`, `MaxEntriesPerRow` (1), `QueueTriggers`, `ForEnemies`, `OnlyNewFulfilmentDuringCooldown`. `RowQueueConfig.Off` ist die alte Regel (überspringen, Auslöser verfallen), z. B. für Vergleiche.

#### Wachsen und Evolution

**Wachstum** ist ein Zähler pro Exemplar: jedes Skill-Exemplar und jeder Logikbaustein (die Rune einer Zeile) zählt selbst mit. Er gilt für den ganzen Run, wandert durch die Akte, bleibt beim Umsetzen eines Skills und beim Ablegen einer Rune ins Runen-Inventar. Gezählt wird nach jedem Kampf aus dem Kampfprotokoll (`Growth/GrowthTally`), nur für Zeilen, die gefeuert haben. Was wächst, steht als Daten in `GrowthCatalog.CreateDefault`:

| Regel | Wirkung | Skills / Bausteine |
|---|---|---|
| Pro Kill | +1 Schaden (max. +30) | Rüstungsbruch, Bohrstoß, Schockstich, Blitzlanze, Säurebohrer |
| Pro Betäubung | +0,1 s Betäubung (max. +2 s) | Schildschlag, EMP-Schildschlag, Schrottramme |
| Pro Heilung | +1 % Heilung (max. +50 %) | Not-Reparatur, Kühlmittel-Injektion |
| Pro gewonnenem Kampf | +1 % Wirkung (max. +50 %) | Entzünden, Feuersturm |
| Pro gewonnenem Kampf | nur Zähler (Stufen, Modul-Plätze) | Notfall-Schildwall, Blendgranate, Bodenanker, Schubdüsen, Echo-Protokoll, Resonanz |
| Pro gewonnenem Kampf | +1 Prozentpunkt Schwelle (max. 50 %) | Bausteine «HP unter … %» (beide), «Gegner unter … %», «HP unter … % oder ausgewichen» |
| Pro gewonnenem Kampf | nur Zähler | alle anderen Bausteine |

**Meilensteine:** Wachstum 5 / 15 / 30 ergibt Skill-Stufe 1 / 2 / 3 (Höchststufe). Wachstum 10 und 25 öffnet je einen weiteren Modul-Platz, für Skills und Bausteine (also bis 3 Plätze). Die Karte zeigt das Wachstum am Namen («Bohrstoß +7», «[HP unter 30 % +4] → Bohrstoß +7»), das Build-Fenster im Tooltip jeder Zeile die Regel, die aktuelle Wirkung und den nächsten Meilenstein.

**Zusammenführung mit den Stufen +1…+3 (A-03).** Früher gab ein doppelter Skill eine Stufe, und jede Stufe hatte eigene Werte (+15 % Waffenschaden usw.). Jetzt gibt es **nur noch eine Zahl pro Exemplar, das Wachstum**: Die Stufe ist ein Meilenstein daraus und bringt selbst keine Werte mehr; ein doppelter Skill aus einer Belohnung gibt **+5 Wachstum** (genau eine Stufe, wenn man auf einer Schwelle steht). Die ganze Kraft kommt aus der Wachstums-Regel des Skills. Grund: Hätten Stufe und Wachstum beide Werte, zählte jeder Punkt doppelt (der Stufensprung durch Wachstum und das Wachstum selbst), und ein Duplikat wäre gegenüber Kämpfen unvergleichbar. So bleibt sichtbar, woher ein Wert kommt, Duplikate und Kämpfe zahlen auf dasselbe Konto ein, und Stufe 3 bleibt das Tor für Evolutionen. Lagerfeuer-Stufen von Runen bleiben unverändert (sie verschieben die Schwelle in festen Schritten); das Schwellen-Wachstum legt Prozentpunkte darauf, nie über 50 % und nie unter den Grundwert.

**Evolution:** Ein Exemplar auf Höchststufe (Skill: Stufe 3 aus Wachstum 30, Baustein: höchste Lagerfeuer-Stufe der Rune) plus eine Rezeptbedingung entwickelt sich **nach dem nächsten überlebten Boss** zur Evolutionsform. Wachstum, Instanz-Id, Ort und Module bleiben. Die Bedingung ist eines von: ein bestimmtes Modul am Skill bzw. Baustein, ein bestimmter Baustein in derselben Zeile, oder ein Ausrüstungs-Tag auf Schwelle 4. Sechs Rezepte als Daten (`Evolution/EvolutionCatalog.CreateDefault`), eines pro Tag:

| Tag | Aus | Bedingung | Evolution |
|---|---|---|---|
| Hitze | Entzünden | Modul Fläche | Feuersturm: 60 % an alle Gegner, alle brennen 5 s |
| Ladung | Schockstich | Tag Ladung 4 | Blitzlanze: 120 %, 50 % Chance auf 1,5 s Betäubung, Cooldown 3 s |
| Phantom | Baustein «HP unter 30/40/50 %» | Modul Verlängern | «HP unter 50 % oder ausgewichen» (gilt auch direkt nach einem Ausweichen) |
| Takt | Echo-Protokoll | Modul Mehrfach | Resonanz: wiederholt den letzten Skill zweimal, Cooldown 10 s |
| Toxin | Bohrstoß | Baustein «Gegner unter … %» in derselben Zeile | Säurebohrer: 200 % an alle Gegner, doppeltes Gift |
| Schrott | Schildschlag | Tag Schrott 4 | Schrottramme: 90 % durch Rüstung, unterbricht, 2,5 s Betäubung |

Evolutionsformen werden nie angeboten, man erreicht sie nur über ein Rezept. Angebote, Shop, Inventar und das Rezeptbuch im Build zeigen den Fortschritt («Evolution ???: fehlt Modul Fläche», «→ Ladung 3/4 für Evolution von Schockstich»), das Build-Fenster pro Zeile (Tooltip und ✦), das HUD ein ✦ an Zeilen, die nach dem nächsten Boss evolvieren.

**Rezeptbuch:** Unentdeckte Evolutionen und Duos stehen als Silhouette «???» mit einem Hinweis im Inventar, entdeckte mit ihrem Rezept. Das Buch wird über Runs gespeichert (in Unity in den PlayerPrefs, `Overworld/Persistence/PlayerPrefsRecipeBookStore`, im Core hinter `IRecipeBookStore`). Es ist reines Wissen: Gespeichert werden nur Einträge wie «evo:evo_inferno», nie Werte oder Boni.

#### Schwierigkeit und Bonus der Bausteine

Jeder Logikbaustein hat eine feste **Grundschwierigkeit 0–3** (Daten im `RuneCatalog`, Parameter `difficulty` und `invertedDifficulty`). Je seltener eine Bedingung von selbst eintritt, desto stärker wird der Skill ihrer Zeile. Die Bonus-Tabelle steht als Daten in `Arena/Difficulty.cs` (`DifficultyBonusConfig.Default`):

| Stufe | Symbol | Beispiele | Bonus auf den Skill der Zeile |
|---|---|---|---|
| 0 Leicht | ◇ | Immer, Kampfbeginn | keiner |
| 1 Mittel | ◆ | Bei Treffer, Jeder 3. Angriff, Alle 5 Sekunden | −15 % Cooldown |
| 2 Schwer | ◆◆ | Nach Krit, Gegner betäubt, Nach Block, HP unter 30 % | −30 % Cooldown, +25 % Wirkung |
| 3 Sehr schwer | ◆◆◆ | Ladung voll, 3 Ausweicher in Folge, HP unter 15 %, Alle 20 Sekunden, Gegen Boss | −50 % Cooldown, +50 % Wirkung, −30 % Cast-Zeit (nie unter 0,1 s), +1 s Dauer von Status-Wirkungen |

«Wirkung» heisst Schaden, Heilung (auch Brennen, Gift, Heilung über Zeit) und Schild bzw. eigene Buffs. Der Basisangriff bekommt nie einen Bonus.

- **Der Bonus sinkt nie.** Ausrüstung, Module, Wachstum, Tags oder Skills, die eine Bedingung leichter erfüllbar machen, ändern die Stufe nicht. Genau das ist der gewollte Weg zu starken Builds.
- **Umkehren** hat eine eigene Stufe: «NICHT Immer» ist ◆◆◆, «NICHT alle 20 Sekunden» ist ◇.
- **Der Bonus wandert mit:** Löst eine Zeile per Auslöser ein Ziel aus, läuft diese Ausführung mit der Stufe der auslösenden Zeile, wenn sie höher ist (nicht stapelnd, die höhere zählt). Wiederholungen («Mehrfach», Echo) behalten die Stufe.

**Erleichterer** (`Arena/Reliefs.cs`, `ReliefCatalog.CreateDefault`) sind neue Inhalte, die schwere Bausteine öfter erfüllen:

| Erleichterer | Art | Wirkung | Macht leichter |
|---|---|---|---|
| Lähmhandschuhe | Ausrüstung (Hände), Ladung | Eigene Betäubungen dauern +1 s | Gegner betäubt |
| Nachbild-Visier | Ausrüstung (Kopf), Phantom | Gegner gilt 0,5 s nach einer Betäubung noch als betäubt | Gegner betäubt |
| Vorgeladene Zelle | Ausrüstung (Brust), Ladung | Ladung startet bei 3 | Ladung voll |
| Phantomschritt-Stiefel | Ausrüstung (Füsse), Phantom | Ausweicher-Serie bricht erst beim 2. Treffer | 3 Ausweicher in Folge |
| Konterschild | Ausrüstung (Schild), Ladung | Krit-Chance +15 % für 2 s nach einem Block | Nach Krit |
| Giftbrenner | Ausrüstung (Waffe), Toxin | «Gegner brennt» gilt auch bei Gift | Gegner brennt |
| Schmerzleiter-Beinschienen | Ausrüstung (Beine), Schrott | «Schwerer Treffer» gilt 5 Prozentpunkte früher | Schwerer Treffer |
| Alarmfühler | Baustein-Modul | HP-Schwellen-Bausteine gelten 10 Prozentpunkte früher | HP unter 30 %, HP unter 15 %, HP unter … oder ausgewichen |
| Witterung | Baustein-Modul | «Gegner unter x %» gilt 10 Prozentpunkte früher | Gegner unter 25 % |
| Ladungsspule | Skill (Schild) | +3 Ladung (höchstens 5) | Ladung voll |
| Lähmnebel | Skill (Schock) | 20 % Schaden und 0,6 s Betäubung an alle Gegner | Gegner betäubt |

Module wirken, solange sie an irgendeinem Baustein sitzen. Eine «Barriere» gibt es im Spiel nicht; statt «HP-Schwellen zählen die Barriere nicht mit» verschiebt der Alarmfühler die HP-Schwellen.

**Anzeige:** Build-Fenster, Runen-Inventar, HUD, Runen-Angebote und Shop zeigen das Symbol farbig am Baustein (◇ grau, ◆ blau, ◆◆ orange, ◆◆◆ rot), der Tooltip den Bonus. Die Infozeile des Skills in einer Zeile zeigt die Werte **inklusive** Bonus (Cooldown, Cast-Zeit, Schaden) und im Tooltip «Baustein ◆◆ Schwer: … (eingerechnet)». Angebote von Teilen, Modulen und Skills nennen, welche Bausteine sie leichter machen («Erleichtert: «Gegner betäubt» ◆◆ (Eigene Betäubungen dauern +1 s)»).

### Die Arena lesen

Die Arena zeigt nicht nur, *welche* Zeile feuert, sondern auch *warum* die anderen nicht.

- **Tafel live:** Jede Zeile hat links einen Farbstreifen (ihre Farbe) und ein Zustand-Symbol: ✔ Bedingung erfüllt und Skill bereit, ✖ Bedingung nicht erfüllt, ⏳ Skill im Cooldown (mit Restzeit-Balken unter der Zeile), ⧗ eingereiht (Zeile hellblau, ersatzweise »), ⌀ verwaist (kein Skill). Die feuernde Zeile leuchtet gelb. Mit der Maus über einer Zeile steht der Zustand jetzt und der letzte Grund, warum sie übersprungen wurde, z. B. «4,2s: Skill im Cooldown (noch 1,8 s)».
- **Warteschlange:** Unter der Tafel steht, was wartet, in der Reihenfolge der Tafel, z. B. «Wartet: 2. Schildschlag ⏳1,2 s · 4. Bohrstoß bereit» («bereit» = Cooldown vorbei, eine andere Aktion läuft noch). Im Protokoll steht «Zeile 2 (Schildschlag) eingereiht, wartet auf Cooldown (noch 1,2s)» statt «übersprungen» und beim Start «⏳ aus der Warteschlange nach 1,2s».
- **Gründe fürs Überspringen:** «Bedingung nicht erfüllt», «Skill im Cooldown (noch x,y s)», «verwaist (kein Skill)» und «Bedingung erfüllt, aber Aktion läuft» (eine höhere Zeile war bereit, während eine Aktion lief, die sich nicht abbrechen lässt; nur das Ausholen eines Basisangriffs darf noch unterbrochen werden).
- **Kämpfer:** Unter dem Lebensbalken stehen Ressourcen als Balken (Hitze, Ladung, Tempo-Stapel …) und aktive Zustände als kleine Kästchen (Brand, Betäubt, R.-Bruch, Schild …) mit Restdauer, Restzeit-Balken und Stapeln (×2). Tooltip mit vollem Namen.
- **Schwebende Zahlen am Ziel:** Schaden weiss, Krit gelb und grösser, Heilung grün, «Block» und «Ausgewichen» als Wort. Kommt die Wirkung von einer Tafel-Zeile, liegt die Zahl auf einem Feld in der Farbe dieser Zeile. Auch Brennen zählt zur Zeile, die es gesetzt hat.
- **Auswertung nach dem Kampf** (vor «Weiter»): Tabelle pro Zeile mit «gefeuert», Schaden und Heilung gesamt, Anteil am Gesamtschaden, «eingereiht» (wie oft und mittlere Wartezeit bis zum Start, z. B. «4× · Ø 1,2 s»), wie oft übersprungen und häufigster Grund. Dazu Hinweise wie «Zeile 3 hat nie gefeuert: Bedingung nie erfüllt» oder «Zeile 2 (Bohrstoß) macht 64 % des Schadens». «Build öffnen» öffnet direkt das Fenster «Build».
- **Auslöser und Wiederholungen:** Der Cast-Balken zeigt «↪ von Zeile 1» bei ausgelösten und «↻ Wiederholung» bei wiederholten Aktionen. Im Protokoll steht beim Start «↪ ausgelöst von Zeile n» und Auslöser auf ein beschäftigtes Ziel als «Zeile 2 (Bohrstoß) eingereiht … ↪ ausgelöst von Zeile 1», so lässt sich jede Kette verfolgen. Die Auswertung zählt in «gefeuert» ausgelöste (↪) und wiederholte (↻) Starts mit und gibt Hinweise wie «Zeile 2 (Bohrstoß) wurde 4× ausgelöst, von Zeile 1 ×4» oder bei verwaisten Zielen «3 Auslöser auf Zeile 2 verfielen».
- **Schwierigkeit in der Auswertung:** Spalte «erfüllt» (wie oft die Bedingung von nicht erfüllt zu erfüllt wechselte) und «Bonus» (Zusatzschaden bzw. -heilung aus dem Schwierigkeits-Bonus und eingesparter Cooldown), das Symbol vor jeder Zeile. Hinweise wie «Zeile 2: Bonus ◆◆ brachte +140 Schaden (20 % des Zeilenschadens)» oder «Zeile 3: schwerer Baustein (Sehr schwer) nie erfüllt. Erleichterer helfen, ohne den Bonus zu senken.»
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
│   │   ├── Gear/          Ausrüstung: EquipmentCatalog, Equipment, Inventory (Item-Raster mit fester Reihenfolge, IInventoryItem), BuildStats, BoardFactory (Runen-Zeilen → Tafel), Sets/ (SetBonusRegistry),
│   │   │                  Synergies/ (SynergyRegistry: Tags, Schwellen, Duos als Daten; Wirkungen als BattleModifier)
│   │   ├── Combat/        ICombatResolver, ArenaCombatResolver, EnemyCatalog (Platzhalter-Resolver nur noch für Tests)
│   │   ├── Shop/          Shop-Bestand und Preise
│   │   ├── Autoplay/      Testspieler: AutoplayBot (Strategie), BotAction, AutoplayRecorder + AutoplayReport/AutoplaySummary (JSON),
│   │   │                  AutoplayOptions (Kommandozeile), HeadlessAutoplay (Lauf ohne Darstellung)
│   │   ├── OverworldSession.cs              Fassade: Bewegung, kleine/mittlere Events, Runenwahl
│   │   ├── OverworldSession.MajorEvents.cs  Fassade: Kampf, Truhe, Goldmine, Shop
│   │   ├── Skills/        SkillInstance (Exemplar), ISkillHolder (Ort), SkillCollection (Sammlung)
│   │   ├── Modules/       ModuleDefinition + ModuleCatalog, ModuleInstance (Exemplar, Ziel), IModuleHolder (Ort), ModuleCollection, ModuleRules
│   │   ├── Growth/        GrowthRule + GrowthCatalog (Regeln als Daten), GrowthStages (Meilensteine), GrowthApplier, GrowthTally (Zählen aus dem Protokoll)
│   │   ├── Evolution/     EvolutionRecipe + EvolutionCatalog (6 Rezepte), RecipeBook + IRecipeBookStore (Wissen über Runs)
│   │   ├── OverworldSession.Gear.cs         Fassade: Ausrüstung, Skill-Kennzahlen mit passiven Boni, Tafel umsortieren
│   │   ├── OverworldSession.Synergies.cs    Fassade: Tag-Zähler, aktive Duos, Rezeptbuch, Vorschau für Angebote
│   │   ├── OverworldSession.Skills.cs       Fassade: Skill-Sammlung, Einsetzen/Tauschen, Erhalt, Stufe oder zweites Exemplar, Angebote
│   │   ├── OverworldSession.Modules.cs      Fassade: Modul-Sammlung, Einsetzen/Abnehmen, Auslöser-Ziele, seltener Erhalt
│   │   ├── OverworldSession.Build.cs        Fassade: Stat-Leiste (BuildStats) und Vorschau «vorher → nachher» für Teile
│   │   ├── OverworldSession.Growth.cs       Fassade: Wachstum nach Kämpfen, Meilensteine, Evolution nach dem Boss, Fortschritt für Angebote
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
│       ├── Persistence/   PlayerPrefsRecipeBookStore (Rezeptbuch über Runs)
│       ├── Autoplay/      AutoplayRunner (Testspieler im Spiel: echte Fenster, FPS, Log, Hänger, Bericht, Exit-Code)
│       ├── UI/            OverworldHud, Kit-Auswahl, Event-, Runen-, Shop-, Game-Over-Fenster, ArenaWindow, BuildWindow, InventoryWindow (Figur + Item-Raster), InventoryFullWindow;
│       │                  UiTheme (deckender Stil, Schriftgrössen 18/15/13), DragDrop (Ziehen und Ablegen), StatBar (Stat-Leiste mit Vorschau)
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

## Automatischer Testspieler

Ein Bot spielt komplette Runs, um Abstürze, Fehler-Logs, Hänger und Ruckler zu finden. Er spielt über dieselben
`OverworldSession`-Methoden wie die UI und öffnet dabei die echten Fenster (Kit-Wahl, Ereignisse, Angebote, Shop,
Arena, Build und Inventar), damit auch UI-Fehler auffallen. Spiellogik steckt im Bot keine.

**Aufruf** (Windows-Build, unter macOS/Linux entsprechend):

```
Betaknight.exe -autoplay [-seed N] [-runs N] [-speed N] [-report pfad.json] [-quit]
```

| Argument | Bedeutung |
|---|---|
| `-autoplay` | Testspieler statt normalem Spielstart |
| `-seed N` | Seed des ersten Runs; weitere Runs nehmen N+1, N+2, … Ohne: zufällig |
| `-runs N` | Anzahl Runs nacheinander (Standard 1), Bericht pro Run plus Summe |
| `-speed N` | Tempo (Standard 1): Bot-Takt, Schritt-Animation und Arena-Wiedergabe laufen N-mal so schnell |
| `-report pfad.json` | Ziel des JSON-Berichts. Ohne: `autoplay-report.json` in `Application.persistentDataPath` |
| `-quit` | Nach dem letzten Run beenden. Exit-Code 0 = kein Run mit Exception oder Hänger, sonst 1. Ohne bleibt das Fenster offen |
| `-act N` | Optional: Run endet beim Erreichen von Akt N (Standard 3) |

Ohne `-quit` kann man zuschauen: Kämpfe laufen in der Arena mit Wiedergabe, unten rechts steht Run, Seed, Kit und die
letzte Aktion. Im Editor geht es ohne Build nicht über die Kommandozeile; dafür gibt es den EditMode-Test
`BotSpieltVollenRunOhneFehler`, der den Bot ohne Darstellung mit drei festen Seeds bis Akt 2 oder Game Over spielt.

**Strategie «einfach, aber vollständig»:** Kit nach Seed; unbekannte Felder zuerst (Gegner bei unter 40 % HP meiden);
Angebote nach Wertung Verbesserung > neue Karte > Gold, der erste Auslöser hat Vorrang; bessere Teile anlegen (Wertung
aus der Stat-Leiste); freie Skills in Zeilen ohne Skill oder mit Basisangriff, Runen aus dem Inventar in freie Zeilen,
Module an den ersten passenden Ort, Auslöser bekommen ein Ziel; Lagerfeuer: ruhen unter 60 % HP, sonst Rune verstärken;
Shop: heilen, Modul, Skill, besseres Teil, Tafel-Zeile, Rune, dann verlassen; Minen erobern und verteidigen; nach dem
Boss durchs Portal; bis Game Over oder Akt 3.

**Bericht** (`total` mit Summen, `runs` mit einem Eintrag pro Run; zusätzlich eine Zeile pro Run im Log mit `[Autoplay]`):

| Feld | Inhalt |
|---|---|
| `seed`, `kit` | Seed und gewähltes Kit |
| `ok`, `endReason` | Run ohne Exception und Hänger; Endgrund `Game Over`, `Akt 3 erreicht`, `Hänger` oder `Zuglimit` |
| `act`, `turns`, `actions` | Erreichter Akt, Züge, ausgeführte Bot-Aktionen |
| `hp`, `maxHp`, `gold` | Stand am Ende |
| `fightsWon`, `fightsLost` | Kämpfe (ohne Boss) gewonnen/verloren |
| `elitesWon`, `elitesLost`, `bosses`, `bossesSurvived` | Elite-Kämpfe und Boss-Begegnungen |
| `rewards` | Belohnungen je Art (Rune, Teil, Skill, Modul, Tafel-Erweiterung, Gold, Heilung, Runen-Stufe, Runensplitter) |
| `boardRows` | Tafel-Zeilen am Ende: Rune → Skill [Module] |
| `modules`, `triggersSet`, `triggerLinks`, `duos` | Eingesetzte Module, gelegte Auslöser, Auslöser-Verbindungen, entdeckte/aktive Duos |
| `durationSeconds`, `fpsAverage`, `fpsMin` | Dauer und FPS (ohne Darstellung `null`) |
| `exceptionCount`, `exceptions` | Exceptions mit Text und Stacktrace (höchstens 40 Texte, gezählt wird alles) |
| `errorLogCount`, `errorLogs` | Fehler-Logs (`Debug.LogError`, Asserts) mit Text |
| `hangCount`, `hangs` | Hänger: keine Aktion länger als 10 s (danach Befreiungsversuch, ab 3 Hängern Abbruch des Runs) |
| `totalDamage`, `basicAttackDamage`, `basicAttackSharePercent`, `basicAttackShareFromAct2Percent`, `basicAttackShareByAct` | Schaden des Ritters und Anteil des Basisangriffs, gesamt, ab Akt 2 (Ziel höchstens 30 %) und pro Akt |
| `runeStats` | Pro Baustein: Grundschwierigkeit, Kämpfe auf der Tafel, Kämpfe mit erfüllter Bedingung, wie oft erfüllt und gefeuert, Feuern pro Minute |

**Messung für die Schwierigkeit:** Aus `runeStats` (pro Run und summiert in `total`) lässt sich ablesen, wie oft jeder Baustein in Bot-Kämpfen feuert. Am Ende steht dieselbe Tabelle im Log (`[Autoplay] Bausteine in Bot-Kämpfen …`), im Code `RuneFireStats.Table(runs)`. Bausteine, die der Bot oft erfüllt, sind Kandidaten für eine niedrigere Stufe, und umgekehrt.

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
| Neuer Skill | Eintrag in `SkillCatalog` aus `ISkillEffect`-Bausteinen mit mindestens einer Skill-Art (`kinds:`); neue Wirkung = neue `ISkillEffect`-Klasse mit `Apply` und `Describe` (Kennzahlen für das Build-Fenster) |
| Neues Ausrüstungsteil | Eintrag in `EquipmentCatalog` |
| Neues Set | Teile mit Set-Id + `SetBonusRegistry.Register(id, name, teile => new …Set())` (ein `BattleModifier`) |
| Neuer Synergie-Tag oder Duo | Eintrag in `SynergyRegistry.CreateDefault` (Text, passive Effekte, `BattleModifier`-Fabrik je Schwelle); Teile bekommen die Tag-Id über `tags:` |
| Neues Modul | Eintrag in `ModuleCatalog.CreateDefault` (Name, Art, Text je Stufe) und Regel in `ModuleRules` (`ApplyToSkill` bzw. `ApplyToCondition`) |
| Neue Wachstums-Regel | `GrowthCatalog.CreateDefault`: `SetSkill`/`SetRune` mit einer `GrowthRule` (Auslöser, Wirkung, pro Punkt, Obergrenze, Text) |
| Neues Evolutions-Rezept | Eintrag in `EvolutionCatalog.CreateDefault`; die Evolutionsform als Skill mit `isEvolution: true` bzw. als Rune mit Gewicht 0 |
| Neuer Gegner | Eintrag in `EnemyCatalog` mit Stufenbereich und fester Tafel |
| Inverter-Rune | `NotCondition` / `condition.Not()` existiert bereits |
| Akt-spezifische Karten/Gegner | `OverworldSession.CreateNextAct(config, previous)` bekommt die Karten-Konfiguration; `TierAt` und `ActTierBonus` regeln die Stärke pro Akt |
| Gegneralarme beim Zurückreisen | `StepResult.FirstVisit == false` und `HexCell.VisitCount` |
| Hindernisse | `HexCell.IsWalkable` (Regeln und Pfadsuche berücksichtigen es bereits) |
| Neue Events | Eintrag in `EncounterCatalog.CreateDefault()` |

## Hinweise

- Ohne installiertes Input-System-Paket zeigt Unity eventuell eine Warnung zur fehlenden Referenz `Unity.InputSystem` in `Betaknight.Overworld.asmdef`. Das ist unkritisch, der Code fällt dann auf den alten Input Manager zurück.
- Die `.meta`-Dateien erzeugt Unity beim ersten Öffnen. Bitte mitcommitten.
