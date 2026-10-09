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

### Controls

The game is in English since A-18. This README stays German for the team; German names of game terms map to the in-game English names in the «Glossar Deutsch → Englisch» at the end.

| Action | Effect |
|---|---|
| Hover a tile | Shows the planned route (yellow), or red if the tile can't be reached |
| Click a "?" neighbour tile | Explore: 1 step, 1 turn |
| Click a distant tile reachable over visited tiles | Travel along known routes step by step; the whole trip costs **one turn**. It stops on a new tile, at enemies and at raided mines, and still counts as one turn |
| Window on medium events | Pick one of the options (greyed out = can't afford) |
| Choose a Reward | Take a rune, item, skill or module (★ = fits a tag you have) or skip for 3 Gold. Items: "Equip" or "To Inventory"; runes on a full board: "Put in Rune Inventory" or swap a row |
| Key **B** / button "Build (B)" | Build window: the Logic Board in the middle, one row per rune + skill (rune with level badge and growth, skill with short stats, details in the tooltip). Skill inventory on the left, modules and rune inventory or the Recipe Book on the right, stat bar on top |
| Drag in Build | Skill onto a row = place (taken row: swap), skill from a row to the skill inventory = remove. Row by its handle ≡ onto another row = reorder. Rune from the rune inventory onto a row = swap, onto a free slot = new row; a row's rune into the rune inventory = unequip. Module onto a free slot ◇ on a rune or skill = insert, back to the module list = remove; click a trigger to pick its target |
| Key **I** / button "Inventory (I)" | Inventory window: the knight with 7 slots (Helmet, Gloves, Chest, Legs, Boots, Weapon, Shield), details of the selected item, tags and sets, and the item grid (12 cells). Runes and skills are only in Build |
| Drag in Inventory | Item from the grid onto its slot = equip (the old item goes to the same cell; a wrong slot turns red), item from the knight to the grid = unequip (onto a matching item: swap). Cell onto cell = swap, onto an empty cell = move; the order is kept, also across acts |
| Double or right click | Shortcut: skill, rune or module into the first fitting row or back out; equip or unequip an item. A single click on an item shows its details (Equip, Unequip, Discard) |
| Stat bar | In both windows: HP, Weapon Damage, Attacks/s, Armor, Dodge, Block, Crit, Accuracy, Area Damage, plus active set bonuses, tag tiers and duos. Hovering or dragging an item shows the change ("Armor 6 → 9", green better, red worse); skills, runes and modules show their short stats below |
| During a fight or an open decision | Both windows stay readable, dragging is locked. "B" and "I" close each other, Esc closes |
| Arena after every fight | Plays the fight back: speed 1×/2×/4×, Pause, Skip. Live board with the state of each row, statuses, resources and floating numbers. Afterwards the Report per row, the Log with filters, "Open Build (B)" or "Continue" (see «Die Arena lesen») |
| Button "Open Shop" | Appears on a shop tile you visited before |
| Button "New Run" | Back to kit selection, new map |

Travel stops by itself on hostile tiles (enemy, boss), on newly discovered tiles and whenever a window opens.

### Ablauf eines frühen Runs

1. **Kit wählen:** Klingen-, Schild- oder Funkenritter. Jedes Kit bringt HP, Gold, eine Start-Rune und Start-Skills mit: der erste ist ein Schadens-Skill und sitzt an der Start-Rune, die übrigen liegen frei in der Sammlung. Klinge: Rüstungsbruch, Schockstich. Schild: Bohrstoß (an «Wenn getroffen»), Schildschlag, Notfall-Schildwall. Funken: Entzünden, Kühlmittel-Injektion.
2. **Ring 1** um den Start hat nur kleine Events (Münzen, Kräuter, Runensplitter, Wegweiser). Sie wirken sofort und melden sich unten links.
3. **Ab Ring 2** kommen mittlere Events mit einer Entscheidung und die ersten Kämpfe. Truhen sind selten, Shops gibt es erst ab Ring 3. **Ab Ring 3** gibt es Elite-Gegner (Feld «E»): zwei Stufen stärker, mit mehr Leben und Schaden, dafür mehr Gold und oft eine Tafel-Erweiterung.
4. **3 Runensplitter** öffnen eine Runenwahl, ebenso jeder gewonnene Kampf und jede Truhe. Die Platine startet mit 4×3 Feldern und wächst bis 6×6 (siehe «Belohnungen» und «Circuit Board»).
5. **Sammeln statt ersetzen:** Ausrüstung (12 Plätze) und Runen (6 Plätze) haben ein Inventar. Neue Teile werden angelegt, wenn ihr Platz frei ist, sonst kommen sie ins Inventar; verdrängte Teile (auch der Schild bei einer Zweihandwaffe) wandern ins Inventar. Eine neue Rune bei voller Tafel kommt ins Runen-Inventar. Runen behalten ihre Lagerfeuer-Stufe, beim Tauschen bleibt der Skill an der Zeile. Ist ein Inventar voll, wird gefragt: ein vorhandenes verwerfen oder das neue ablehnen. Wechseln geht jederzeit ausserhalb von Kampf und offenen Fenstern; Set-Boni, verwaiste Zeilen und Set-Runen folgen sofort. Im Shop lässt sich das Inventar für den halben Preis verkaufen. Das Inventar wandert durch die Akte mit.
6. **Aufbauen statt austauschen:** Belohnungen nach Kämpfen enthalten immer mindestens eine Verbesserung des aktuellen Builds. Eine doppelte Rune hebt die vorhandene eine Stufe (wie das Lagerfeuer), ein doppeltes Teil wertet das vorhandene auf (+1 bis +3). Nach jeder Verbesserung erscheint unten links eine Meldung, z. B. «Rüstungsbruch 100 % → 115 %» oder «Tafel 4 → 5 Zeilen». Das HUD zeigt Zeilen x/8 und eine kurze Build-Übersicht.
7. Fällt der Ritter in einem Kampf, ist der Run vorbei. Events auf der Oberwelt töten nie.

### Belohnungen

| Belohnung | Quelle | Wirkung |
|---|---|---|
| Rune | Runenwahl (Sieg, Truhe, 3 Splitter), Shop | Neue Rune als Relais auf ein freies Feld oder ins Runen-Inventar; schon vorhandene Rune: +1 Stufe |
| Ausrüstung | Sieg (50 %), Truhe, Shop | Anlegen oder ins Inventar; schon vorhandenes Teil: +1 Stufe (bis +3), jede Stufe +50 % der Grundwerte |
| Skill | Sieg (35 %), Elite (60 %), Truhe (50 %), Mine (35 %), Runensplitter (30 %), Shop (1 Skill, 14 Gold) | Neues Exemplar frei in die Sammlung. Schon vorhanden: «Wachstum +5» für das vorhandene Exemplar (siehe Wachsen und Evolution) oder «Zweites Exemplar» für eine weitere Komponente |
| Platinen-Erweiterung: nächste Grösse | Garantiert bei jeder Boss-Flucht und beim Akt-Wechsel, als Wahl bei Elite-Siegen (50 %) und seltenen Truhen (10 %), Shop-Platz (20, 35, 50 … Gold pro Run, einer pro Shop) | 4×3 → 4×4 → 5×4 → 5×5 → 6×5 → 6×6 |
| Modul (selten) | Elite (35 %), Truhe (15 %), garantiert bei jeder Boss-Flucht, Shop (in 50 % der Shops ein Platz, 30 Gold) | Neues Exemplar frei in die Sammlung; schon vorhanden: «Stufe erhöhen» (+1, wo das Modul Stufen hat) oder «Weiteres Exemplar» |
| Chip (selten) | Elite (35 %), Truhe (15 %), garantiert 1 bei jeder Boss-Flucht, Shop (in 50 % der Shops ein Platz, 15 Gold) | Leiterbahn, Diode, Gatter, Kondensator, Sicherung oder Effekt-Chip (Amplifier, Watchdog, Overflow, Firewall) ins Chip-Inventar (siehe «Pins, Traces and Logic Chips» und «Circuit Effects») |
| Bergen (Gegner-Teile) | Jeder Sieg über einen Gegner mit Ausrüstung: 1 Wahl, Elite 2 | Skill, Rune, Modul oder Chip, das der Gegner benutzt hat (siehe «Enemy Boards and Salvage») |
| Gold, Splitter | Kämpfe, Events, Minen, Boss-Flucht | Elite-Siege geben +4 Gold |

Kampfbelohnungen bieten bevorzugt Verbesserungen an: Stufe für einen eigenen Skill, Stufe für ein getragenes Teil, ein fehlendes Set-Teil, Stufe für eine vorhandene Rune oder eine Rune zu einem vorhandenen Tag. Mindestens eine Option ist immer eine Verbesserung. Skill-Angebote bevorzugen Skills, deren Art zum Build passt (×3 Gewicht): Arten eigener Skills, Ziele der passiven Effekte der Ausrüstung und die Runen (Klinge → Angriff, Schild → Schild, Funke → Schock, Glut → Feuer und Heilung, Phantom → Bewegung). Gegner skalieren weiter über Ring und Akt, die Schutzregeln (eine Aktion pro Tick, Thermal Throttling ab 30 s) bleiben. Alle Werte stehen in `Core/Run/ProgressionConfig.cs`.

### Circuit Board (A-19)

Combat is programmed on a **circuit board**, a grid instead of a list of rows. Fights still run automatically in fixed ticks (20 per second).

- **Grid:** The board starts at **4×3** and grows with board expansions: 4×3 → 4×4 → 5×4 → 5×5 → 6×5 → 6×6 (data in `Core/Circuit/CircuitBoard.cs`, `CircuitConfig.Default`). Expansions come from boss escapes, new acts, elite rewards, rare chests and the shop.
- **Core:** A fixed 1×1 Core sits at (1, 1). Every component touching the Core gets **+10 % effect**.
- **Relays (When)** are the former runes: 1×1 chips with a condition. A relay **powers every component it touches at an edge** (corners don't count).
- **Components (What)** are skills with a shape: 1×1, 1×2, 2×1, 2×2 or 2×3. Nothing may overlap. Drag & drop places them, a right click rotates them (width and height swap).
- **No cooldowns.** When a relay triggers, all components it powers are queued and fire one after another with their cast time. Loops are limited by cast times only.
- **Queue:** Priority is reading order: top-left first, row by row (the index `#n` in the UI). Each component is queued at most once; further triggers while it waits are shown as **missed** (already queued, too large, frozen, no skill). The Basic Attack fills the gaps and is interrupted during its windup as soon as a component is queued.
- **Triggers:** Event relays (On Hit, When Hit, After Crit, Every n Attacks, Clock …) trigger on **every** event. State relays (HP Full, HP Below 30 %, Enemy Charging …) trigger only on the **rising edge**, when the state turns true. The relay module **Repeat while true** re-queues its components after they fired, as long as the state still holds.
- **Clock replaces Always:** The rune "Always" is gone. The new relay **Clock 2 s** (rare, level 1: 1 s) ticks at a fixed interval, first tick after one interval. Enemies that used "Always + cooldown" now have Clock relays ("Every 7 s").
- **Enemy boards** are data (`Combat/EnemyBoard.cs`) and readable on hover on the map and in the arena, e.g. "Every 7 s → Ram (2×1): …".

#### Size limit by difficulty

The harder a relay's condition is to meet, the larger the components it can power and the stronger they get. A component larger than the limit of every relay touching it is **not powered (too large)** and never fires. Values are data in `Arena/Difficulty.cs` (`DifficultyBonusConfig.Default`).

| Difficulty | Symbol | Example relays | Max component size | Bonus on powered components |
|---|---|---|---|---|
| 0 Easy | ◇ | Clock, Battle Start | 1 cell | none |
| 1 Medium | ◆ | On Hit, When Hit, Every 3 Attacks, Every 5 Seconds | 2 cells | +15 % effect |
| 2 Hard | ◆◆ | After Crit, Enemy Stunned, After Block, HP Below 30 % | 4 cells | +30 % effect, −20 % cast time |
| 3 Very hard | ◆◆◆ | Charge Full, 2 Dodges in a Row, HP Below 15 %, Every 20 Seconds, Vs. Boss | 6 cells | +60 % effect, −35 % cast time, +1 s status duration |

Cast time never drops below 0.1 s. The bonus never decreases: gear, modules or growth that make a condition easier keep its tier. "Invert" has its own tier. Enemy relays have no size limit. An execution gets the bonus of the relay that triggered it.

#### Skill power by size

Bigger components hit harder per execution (`Arena/SkillBudget.cs`, `SkillBudgetConfig.PowerPercentByCells`, checked by a test for every skill in the catalog). Area skills deal 60 % of that per target; utility skills (stun, shield, blind, heal) spend at most 60 % of their budget on damage.

| Size | Cells | Weapon damage per execution | Examples |
|---|---|---|---|
| 1×1 | 1 | 60 % | Shock Stab, Thrusters, Charge Coil |
| 1×2 / 2×1 | 2 | 150 % | Ignite, Shield Bash, Coolant, Cryo Grenade, Echo |
| 2×2 | 4 | 350 % | Armor Break, Drill, EMP Bash, Emergency Repair |
| 2×3 | 6 | 600 % | Rail Cannon |

Evolutions keep the shape of their base skill.

#### Haste, Slow and Freeze

- **Haste** shortens cast time by x %, **Slow** lengthens it (`StatKind.CastPercent`, `Battle.Haste`). Everything that used to reduce cooldowns is now Haste: Charge at 6 parts ("Every Block: −20 % cast time for 2 s"), Phantom set bonus and the Ember Rhythm duo. Gear passives "−1 s cooldown" became "−15 % cast time".
- **Freeze** (Cryo Grenade) stops the target's largest powered component for 3 s; its triggers are missed while frozen.
- **Numbing Mist** slows all enemies (+30 % cast time for 3 s).

#### Modules on the board

Modules sit on relays and components. Blood Toll now costs HP per cast and gives +40 % effect (+10 % per level). Quickcast gives −30 % cast time (−10 % per level) at −15 % effect. **Repeat while true** is a new relay module (see above). Trigger modules target a component by its skill instance, so moving or rotating it keeps the link.

#### Build window and arena

- **Build:** The board with drag & drop and right-click rotation; skill, chip and module inventories next to it. Every component shows its size, cast time, effect, the relay powering it and "not powered (too large)" when it does not fit.
- **Arena:** Relays light up when they trigger, the queue is shown in reading order, the firing component is highlighted, frozen and unpowered components are marked. The report after the fight lists every component: fired, triggered, queued with average wait, missed with the main reason, damage and share.

### Pins, Traces and Logic Chips (A-20)

Components now talk to each other. When a component fires, it sends **pulses** along its connections; logic chips combine relays.

- **Pins** sit on component edges and are data per skill (`Core/Circuit/Pins.cs`, `PinCatalog.CreateDefault`). Two components are **connected** when two pins touch: neighbouring cells, pins facing each other. Pins turn with the component (90° clockwise).
- **Typed pins** ("Shock pin", "Fire pin" …) ask for a skill kind. With a matching neighbour on that pin the component gets **+15 % effect** per matched pin (`PinConfig`). Shown as a coloured notch, lit when matched.
- **Traces** connect distant pins: Straight, Corner, T and Cross (1×1 chips, right click rotates). A **Diode** lets pulses pass one way only (in → out, arrow on the chip).
- **Pulses** travel **one connection per tick** (every trace piece and the final pin each count as one). When a pulse arrives, the target component is queued and casts normally with its cast time. It counts as **powered by the original relay**: that relay's size limit and bonus tier apply, bonuses never stack. Too large for the original relay → missed (too large). Circles are allowed; they are limited by cast times only. If several paths lead to the same target, the shortest one counts.
- **Gates** are 1×1 chips that read the relays they touch (reading order) and **power the components they touch** like a relay:

| Chip | Triggers when | Difficulty |
|---|---|---|
| AND | both touching relays are on (rising edge) | the higher of the two **+1** (max. 3) |
| OR | either touching relay triggers | the lower of the two |
| NOT | the touching relay turns off | the relay's own inverted value (rune data, `invertedDifficulty`) |
| Fuse | its touching relay triggers, **once per fight** | always 3 (Very hard) |

  A state relay is "on" while its condition holds; an event relay counts as on for 0.5 s after it triggered (`ChipConfig.GateHoldTicks`). A relay used by a gate still powers its own neighbours as usual.
- **Capacitor:** stores up to **3 pulses** (more are lost) and releases them all when a relay touching it triggers, or **3 s** after the first stored pulse at the latest. Released pulses keep their original relay. It is a node: pulses end there and start again from all its open sides.
- **Chips are rare rewards:** Elite wins (35 %), chests (15 %), every boss escape (1 chip, guaranteed) and the shop (a chip slot in 50 % of shops, 15 gold). They go straight into the chip inventory, which travels through the acts. All values are data in `ChipConfig`, `ChipCatalog.CreateDefault` and `ProgressionConfig`.
- **Arena:** Pulses move along their path, gates and relays show open/closed, a blown fuse is marked, capacitors show their charge (e.g. 2/3). The log and the component tooltip say why something fired: "pulsed by Shock Stab (via 2 connections), powered by Battle Start".
- **Data model:** `LogicGraph` holds relays, components, chips, pins and connections (edge kinds Power, Input, Pulse, PinOf). Fights stay deterministic: same seed, same pulses.

#### Example circuits

Coordinates are (column, row) from the top left, starting at 0. `>` is a diode pointing right. All three are checked by tests in `CircuitPulseTests` (`ReadmeExample…`).

**1. Opening combo (pulse chain)**

```
      0              1             2        3
0  [Battle Start] [Shock Stab]  [ > ]  [Thrusters]
```

Battle Start powers Shock Stab. After Shock Stab hits, its right pin sends a pulse through the diode; two ticks later Thrusters are queued, powered by Battle Start (Easy, 1 cell). The diode stops the pulse from bouncing back, so this fires once. Place Thrusters directly next to Shock Stab instead and their pins touch both ways: the two loop for the whole fight. Swap Battle Start for **HP Full** (Medium) and a 2×1 Cryo Grenade fits at the end of the chain.

**2. Hard opener (AND gate)**

```
        0          1             2              3
0                                          [HP Full]
1                [ Core ]  [Shock Stab]    [ AND ]
2                                          [Battle Start]
```

The AND gate touches both relays and powers the Shock Stab next to it. HP Full is Medium, Battle Start is Easy, so the gate is **Hard** (higher +1): Shock Stab casts with +30 % effect and −20 % cast time (plus +10 % from the Core), once at the start of the fight while your HP is full. Fits the starting 4×3 board.

**3. Saved counter (capacitor)**

```
      0          1          2        3          4          5
0  [Clock]  [Shock Stab]  [ > ]  [Capacitor]  [Trace]  [Lightning Lance]
1                                [When Hit]
```

Every Clock tick fires Shock Stab, whose pulse is stored in the capacitor (the diode blocks the way back). When you get hit, When Hit touches the capacitor and releases it: the pulse runs along the trace and Lightning Lance strikes, powered by Clock. Without a hit, the capacitor releases on its own after 3 s. Needs a board 6 columns wide.

### Circuit Effects, Hacks and Thermal Throttling (A-21)

Effects change how your own board runs; hacks attack the enemy board. Every effect is data in `Core/Circuit/Effects.cs` (`CircuitEffectCatalog`, values in `CircuitEffectConfig`). The **form** of each effect (module, chip or skill) is a field there too: `WithForm(id, form)` turns e.g. Overclock into a chip, and it works the same (`CircuitEffectsTests.AnEffectWorksTheSameInAnotherForm`).

- **Modules** go onto a component like any other module (Workshop, rewards, shop).
- **Effect chips** are 1×1 logic chips. A chip that touches a component gives it the effect; board effects (Overflow, Firewall) work anywhere on the board, Firewall once per chip.
- **Hack skills** are components like any other skill (Shock kind, sizes 1×1 to 2×2). When they execute, they hack the enemy instead of dealing damage.

#### Glossary of effects

| Effect | Form | Size / Kind | What it does |
|---|---|---|---|
| **Overclock** | Module | own component | −50 % computing time. Each execution gives touching components 1 **Heat**; at 5 Heat a component skips one execution, then its Heat resets. |
| **Interrupt** | Module | own component | Jumps to the front of the queue when queued. |
| **Parallel Thread** | Module | own component | Triggered while another execution runs, it runs at the same time, bypassing the queue (once per trigger). |
| **Buffer** | Module | own component | May stand in the queue up to 3 times instead of once. |
| **Recursion** | Module | own component | If its relay's condition still holds after it executed, it calls itself again; each depth +20 % effect (max. depth 5). |
| **Amplifier** | Chip | conducts like a straight trace | Each pulse passing through gains +15 % effect (per amplifier on the path, not carried to the next component). |
| **Watchdog** | Chip | gate-like relay | If none of your components fired for 2 s, it triggers your largest touching component (as a Hard relay). The idle time restarts after every execution. |
| **Overflow** | Chip | whole board | While your queue holds 3 entries, every further entry turns into a shock against all enemies instead: 50 % of the size bonus × weapon damage, ignoring armor. |
| **Firewall** | Chip | whole board | Blocks the next enemy hack (one per chip). |
| **Bit Flip** | Skill | Hack, 1×1 | An enemy state relay that currently holds (the one powering the most cells) is inverted for 3 s. Event relays like Clock can't be flipped; then the hack fails. |
| **Jam** | Skill | Hack, 1×2 | The enemy's most important relay (most powered cells) ignores its next 2 triggers. |
| **Hijack** | Skill | Hack, 2×2 | The next execution of the enemy's largest powered component happens for you: your weapon damage, against the enemy. |
| **Short Circuit** | Skill | Hack, 2×1 | The enemy's largest powered component fires at once and hits its own side (the next ally, else itself). |
| **Latency** | Skill | Hack, 1×2 | All enemies get +50 % computing time for 4 s. |
| **Thermal Throttling** | – | both boards | From 30 s on, every 5 s one step: +10 % computing time and ×1.15 damage per step (compounding, after armor). |

**Enemies hack too:** Spark Drone jams every 9 s, Smelter sends Latency every 11 s, Siege Golem flips a relay every 10 s. A Firewall chip blocks the first hack.

**Thermal Throttling** replaces the old Overheat damage: nobody takes damage from time alone any more, but fights speed toward an end because every hit grows. The rune "Overheat" now holds while Thermal Throttling is active. The boss fight (survive 15 s) is unchanged. Values: `ThermalConfig`.

**Arena:** hacked components and relays flicker, components with Heat show a heat bar, recursion shows its depth, pulses show their amplifier gain (e.g. +15 %), and the top bar shows the Thermal Throttling step. The log explains every effect ("Charge Coil overheated at 5 Heat and skips this execution", "Every 1 s is jammed and ignores this trigger (1 left)", "Shock Stab calls itself again (Recursion depth 2, +40 % effect)" …).

#### Shop: Reroll and Lock

- **Reroll** costs 3 gold, each further reroll in the same visit +2 (3 → 5 → 7 …). Leaving and reopening the shop resets the price.
- **Lock** (padlock on an offer) keeps it: it stays through rerolls and appears again, in front, at the next shop visit, also in another shop or act. Up to 2 locks at once; buying the offer releases its lock, clicking again unlocks it. Values: `ShopPrices.Reroll`, `RerollStep`, `LockSlots`.

### Enemy Boards and Salvage

Enemies fight with real boards, built exactly like the knight's (`Core/Combat/EnemyLoadout.cs`, values in `EnemyLoadoutConfig`).

- **Own parts:** every enemy keeps its fixed parts (e.g. Rust Warden: Every 7 s → Ram). Some of them are skills the knight can own too, like the hacks of Spark Drone, Smelter and Siege Golem.
- **Gear:** from tier 2 on, an enemy also carries a component from the knight's catalogs: a skill on its rune relay, with pins. From tier 5 it carries two. The rune always has the difficulty to power the skill. Each gear component has a 25 % chance of a module.
- **Elite:** carries one more component, has a 50 % module chance on every component (its own parts too, at least one module guaranteed) and 1 chip (Firewall, Overflow or Watchdog; 2 from tier 7). Modules work for enemies exactly as for the knight (Multicast casts twice, Overclock heats neighbours …).
- **Fixed per tile:** enemy and board come from the map seed and the tile. Hovering a fight tile shows exactly the enemy waiting there, its board and its loot. Gold-mine raids still list the possible attackers. The boss carries no gear.
- **Salvage:** after a won fight you pick parts the enemy used: skills, runes, modules and chips. 1 pick after normal fights, **2 after elite fights**. "Leave the rest" skips. The normal reward choice follows afterwards and is unchanged.
- **Arena:** each enemy's board is drawn like yours, with the same live highlights.

### Kampf: Skills, Ausrüstung und Werte

Wie die Platine feuert, steht oben unter «Circuit Board». Dieser Abschnitt beschreibt Skills, Ausrüstung, Tags, Module und Wachstum.

- **Skills sind eigene Exemplare** (`Core/Skills/`: `SkillInstance`, `SkillCollection`): Skill-Id, Wachstum (daraus die Stufe), Instanz-Id. Jedes Exemplar sitzt an höchstens einem Ort (`ISkillHolder`, heute eine Komponente auf der Platine, später ein Ausrüstungs-Sockel). Wachstum und Stufe gehören dem Exemplar, nicht dem Skill. Die Sammlung hat keine Obergrenze und wandert durch die Akte mit. Den Basisangriff gibt es ohne Exemplar beliebig oft.
- **Ausrüstung = Werte + passive Effekte.** Teile liefern keine Skills mehr. Die Waffe bestimmt Waffenschaden und Basisangriff, alle Teile geben Werte und manche passive Effekte auf eine Skill-Art, z. B. «Schock-Skills +20 % Wirkung» (Schaden, Brennen, Heilung, Chancen) oder «Schild-Skills −15 % Cast-Zeit». Ablegen eines Teils nimmt keinen Skill weg.
- **Skill-Arten** stehen im `SkillCatalog` (Angriff, Schild, Feuer, Schock, Heilung, Bewegung; ein Skill kann mehrere haben) und im Tooltip. Sie dienen nur als Ziel der passiven Effekte und für passende Angebote.
- **Gegner** haben feste Platinen als Daten (`Combat/EnemyBoard.cs`).

#### Skills sind der Hauptschaden (A-12)

Der Basisangriff ist **Füller und Motor**: Er macht beim Ritter nur noch **60 % Waffenschaden** und füllt die Lücken der Warteschlange (seit A-19 ohne Cooldown-Verkürzung, es gibt keine Cooldowns mehr). Den Schaden tragen die Skills. Gegner behalten ihren Basisangriff mit 100 %.

Die Regel steht als Daten in `Arena/SkillBudget.cs` (`SkillBudgetConfig.Default`) und wird von einem Test für jeden Skill im Katalog geprüft:

Seit A-19 hängt die Wirkung pro Ausführung von der Grösse der Komponente ab (Tabelle «Skill power by size» oben). Nutzen-Skills (Betäubung, Schild, Blendung, Heilung) machen höchstens 60 % des Budgets als Schaden.

**Gegner-HP** liegen bei 65 % der früheren Werte (`EnemyCatalog.HpPercent`), damit frühe Kämpfe mit dem Start-Kit (1–2 Skills) gut schaffbar bleiben. Der Boss bleibt unbesiegbar; die 15 s bis zum Portal schafft jedes Start-Kit (Test). Gold pro Kampf bleibt unverändert, weil Kämpfe eher kürzer werden. Abgleich mit dem Testspieler über 60 Runs: ungefähr gleich viele Runs erreichen Akt 3 wie vorher. Der Schildritter startet zusätzlich mit Bohrstoß an «Wenn getroffen» (vorher nur Schildschlag und Schildwall, beides Nutzen); sein Basisangriff-Anteil ab Akt 2 sank damit im Testspieler von etwa 65 % auf etwa 23 % (ähnlich wie bei Klinge und Funken), die Überlebensrate blieb gleich (160 Schild-Runs: Akt 3 in 61 statt 60 Runs).

**Anzeige:** Die Kampf-Auswertung zeigt den Anteil gross über der Tabelle («Basisangriff 28 % · Skills 72 %», grün bis 30 %). Der Testspieler-Bericht enthält pro Run `basicAttackSharePercent`, `basicAttackShareFromAct2Percent` (Ziel höchstens 30 %) und `basicAttackShareByAct`, das Log den Anteil in der Run-Zeile.

#### Cast-Zeit

Jede Ausführung braucht ihre **Cast-Zeit** (das Ausholen bis zur Wirkung), auch Wiederholungen durch Echo. Loops werden nur über Cast-Zeiten begrenzt, nicht über feste Bremsen.

- Grund-Cast-Zeiten: schnell 0,4 s (Entzünden, Schildwall, Bodenanker, Schubdüsen, Schockstich), mittel 0,8 s (Rüstungsbruch, Schildschlag, Kühlmittel, Blendgranate, Echo), schwer 1,5 s (EMP-Schildschlag, Not-Reparatur, Bohrstoß). Ab 1 s gilt ein Cast als sichtbare Aufladung, die Betäubung und «Gegner lädt auf» kontern. Der Basisangriff holt wie bisher 2/3 seines Intervalls aus.
- Ausrüstung, Tag-Stufen und später Module ändern die Cast-Zeit in Prozent («Schock-Skills −20 % Cast-Zeit»). Alle Prozente addieren sich; das Ergebnis fällt nie unter die **Untergrenze 0,1 s** (2 Ticks, `CastTime.DefaultMinTicks`, pro Kampf `BattleSetup.MinCastTicks`). Infozeile und Tooltip zeigen «Cast 0,3 s (Grund 0,4 s)», der Arena-Balken die Cast-Zeit der laufenden Aktion.
- **Echo** merkt sich den letzten eigenen Skill und startet ihn nach der eigenen Erholung als eigene Ausführung mit dessen Cast-Zeit. Betäubung bricht wie jede Aktion ab. Im Protokoll ist die Wiederholung markiert (`BattleEvent.IsRepeat`).
- Ungedeckelte Stellen (Überlast-Tempo, Phantom-Haste) bleiben bewusst stark. Der Test mit absurden Werten (Cast −100000 %, Riesen-Tempo, Riesen-Rüstung) läuft stabil durch, jede Ausführung hat ihren Cast.

#### Synergie-Tags auf der Ausrüstung

Jedes Teil trägt 1–2 Tags (vorläufig, als Daten in `SynergyRegistry.CreateDefault`): **Hitze, Ladung, Phantom, Takt, Toxin, Schrott**. Gezählt werden nur getragene Teile. Schwellen bei **2/4/6** Teilen schalten je Tag eine Stufe frei, die Stufen gelten zusammen:

| Tag | 2 | 4 | 6 |
|---|---|---|---|
| Hitze | Feuer-Skills −20 % Cast-Zeit | +25 % Schaden gegen brennende Gegner | Basisangriffe setzen Brennen (3 s) |
| Ladung | +10 % Block | Schock-Skills −25 % Cast-Zeit | Jeder Block: Haste −20 % Cast-Zeit für 2 s |
| Phantom | +10 % Ausweichen | Bewegung-Skills −45 % Cast-Zeit | Nach Ausweichen: nächster Angriff +50 % |
| Takt | +10 % Angriffstempo | Alle Skills −15 % Cast-Zeit | Alle Skills weitere −15 % |
| Toxin | Skill-Treffer vergiften (bis 5 Stapel, 1 Schaden/s je Stapel) | +1 Schaden je Gift-Stapel | Auch Basisangriffe vergiften |
| Schrott | +1 Gold je Gegner | +3 Rüstung | Angriffe ignorieren Rüstung |

**Duos:** Haben zwei Tags gleichzeitig mindestens 4, greift ihr Duo: Glutrhythmus (Hitze + Takt), Phasenschild (Ladung + Phantom), Brandgift (Hitze + Toxin), Schrottkondensator (Schrott + Ladung), Geisterschritt (Phantom + Takt), Säurefraß (Toxin + Schrott). Bis ein Duo einmal in einem Kampf aktiv war, zeigt das Spiel es als Silhouette «???»; danach steht es mit Namen und Wirkung im **Rezeptbuch** (Inventar). Das Rezeptbuch wandert durch die Akte mit und bleibt über Runs gespeichert (siehe Wachsen und Evolution).

**Sets bleiben.** Sie behalten ihre Boni und die Rune «Ladung voll»; ihre Teile tragen zusätzlich passende Tags (Überlast → Hitze/Takt, Aegis → Ladung, Schrott-Ernter → Schrott, Phantom-Signal → Phantom). Grund: Die Set-Boni sind eigene Mechaniken (Tempo-Stapel, Entladung, Minen, Ausweich-Obergrenze), die als Tag-Stufen zu speziell wären, und ein Set ist mit 3 Teilen erreichbar, eine Tag-Stufe 6 erst mit fast voller Ausrüstung. So zahlen Set-Teile auf beide Systeme ein.

**Tag-Wirkungen und Set-Boni sehen:** Jedes Teil zeigt im Tooltip und in den Details für jeden seiner Tags (Hitze, Ladung, Phantom, Takt, Toxin, Schrott) alle drei Stufen mit Wirkung und Teilezahl, dazu die Boni seines Sets (● aktiv, ○ noch nicht; grün mit «neu», wenn genau dieses Teil den Bonus freischaltet), ebenso Shop und Belohnungen («mit diesem Teil 2/3»). Das Inventar listet unter «Synergie-Tags und Sets» alle sechs Tags mit ihren Stufen, aktive Duos und alle vier Sets mit ihren Boni, auch ohne getragenes Teil. Maus über «Boni» in der Stat-Leiste, «Tags» und «Sets» im HUD zeigt, was gerade aktiv ist. Unentdeckte Duos bleiben «???», bis sie einmal im Kampf gewirkt haben.

Anzeige: Das HUD zeigt die Zähler («Ladung 3/4», erreichte Schwellen gelb) und aktive Duos. Das Inventar listet alle Tags mit ihren Stufen und das Rezeptbuch. Angebote, Shop und das Inventar zeigen, was ein Teil bewirken würde («→ Ladung 4/6: Schwelle!», «→ Duo frei: ???»).

- **Skill-Kennzahlen im Build:** Jede Komponente und jeder Skill im Skill-Inventar zeigt Kurzwerte (erste Wirkung und Grösse), der Tooltip alles: Wirkung, Schaden, Grösse, Cast-Zeit und Erholung. Schaden steht doppelt, als Prozent vom Waffenschaden und als Wert mit der aktuellen Ausrüstung samt aktiver Set-Boni, gegen ein Ziel ohne Rüstung, ohne Block und Krit (z. B. «180 % Waffenschaden ≈ 10 an allen Gegnern», «Brennen 70 % Waffenschaden/s ≈ 4/s, 20 über 5 s», «20 % Chance: betäubt 1 s»). Skills ohne Schaden zeigen «kein Schaden». Bei kleinen Auflösungen scrollt jeder Bereich für sich.
  Die Werte stehen nicht in der UI, sondern kommen aus den Effekten: Jede `ISkillEffect` meldet über `Describe(SkillInfoBuilder)` ihre Kennzahlen mit denselben Formeln wie `Apply`. `SkillInfo.Create(skill, stats)` fasst sie zusammen, `OverworldSession.SkillUserStats()` liefert die Werte des Ritters zu Kampfbeginn. Neue Effekte müssen `Describe` umsetzen und erscheinen dann automatisch richtig.
- 7 Ausrüstungsplätze (Helm, Handschuhe, Brust, Beinschienen, Waffe, Schild, Stiefel). Zweihandwaffen sperren den Schild.
- 4 Sets mit Boni ab 2 und 3 Teilen: Überlast-Protokoll, Aegis-Firewall, Schrott-Ernter, Phantom-Signal.
- Schutzregeln statt Balance-Bremsen: höchstens eine Aktion pro Tick, Reaktionen erst im nächsten Tick, ab 30 s Thermal Throttling (Cast-Zeit und Schaden steigen in Stufen). Kaputte Builds sind erlaubt, die Engine bleibt stabil.
- Lagerfeuer kann eine Rune eine Stufe verstärken (z. B. «HP unter 30 %» → «HP unter 40 %»).
- Sets: Fortschritt steht im HUD, in der Stat-Leiste und im Inventar. Teile angefangener Sets kommen 3× häufiger in Angebote, Shops verkaufen 2 Teile. Aegis-Firewall (ab 2 Teilen) schaltet die Rune «Ladung voll» frei.
- **Goldminen-Verteidigung:** Alle 8 Züge wird eine eigene Mine angegriffen (rot, «!G»). 6 Züge Zeit, sonst ist sie verloren, bis sie zurückerobert ist. Der Kampf dort läuft «auf der Goldmine» (Schrott-Ernter, Rune «Auf Goldmine»).
- **Boss alle 25 Züge:** Er taucht beim Ritter auf und ist unbesiegbar. Wer 15 s überlebt, entkommt durchs Portal (+8 Gold, +2 Splitter). Ausweichen und Betäuben helfen, Phantom-Signal ist dafür gebaut.
- **Akte:** Das Fluchtportal führt auf eine neue Karte (Akt 2, 3 …). Ritter, Ausrüstung, Platine, Gold und Splitter kommen mit, eroberte Minen bleiben zurück. Das Portal heilt 50 % der Max-HP, Gegner sind pro Akt 2 Stufen stärker, der Zugzähler und der Boss-Takt laufen weiter.

Konzept: `/mnt/project-files/design/kampfsystem-konzept.md` im Projekt.

#### Module und Auslöser

Module sind wie Skills eigene Exemplare (`Core/Modules/`: `ModuleInstance`, `ModuleCollection`, Katalog und Regeln) mit Stufe und Sammlung, die durch die Akte mitwandert. Skill-Exemplare (Komponenten) und Relais haben je **1 Modul-Platz**, durch Wachstum bis zu 3; ein Modul sitzt an genau einem Ort (`IModuleHolder`). Der Basisangriff hat keinen Platz. Verschwindet ein Ort (Relais abgelegt), wird sein Modul wieder frei.

| Modul | Art | Wirkung (Stufe 0 / +1) |
|---|---|---|
| Mehrfach | Skill | Wirkung wird nach erneuter Cast-Zeit wiederholt (×2 / ×3) |
| Fläche | Skill | Schaden trifft alle Gegner mit 70 % / 85 % |
| Kette | Skill | Zielgerichtete Wirkungen treffen 1 / 2 weitere Gegner |
| Blutzoll | Skill | Kostet 5 % / 4 % Max-HP pro Cast, +40 % / +50 % Wirkung |
| Schnellcast | Skill | −30 % / −40 % Cast-Zeit, −15 % Wirkung |
| Umkehren | Baustein | NICHT: das Relais gilt, wenn die Bedingung nicht erfüllt ist |
| Wiederholen solange wahr (Repeat while true) | Baustein | Reiht die versorgten Komponenten nach der Ausführung erneut ein, solange der Zustand gilt |
| Verlängern | Baustein | Die Bedingung gilt 1 s / 1,5 s länger |
| Schwelle | Baustein | +10 / +15 Prozentpunkte bei Runen mit Prozent-Schwelle («HP unter 30 %» → 40 %), höchstens 100 % |
| Auslöser | beides | Am Skill «nach Ausführung», am Baustein «wenn erfüllt» (beim Wechsel von nicht erfüllt zu erfüllt): löst ein Ziel aus |

**Auslöser** zielen per stabiler Id auf ein Skill-Exemplar (Komponente), nicht auf eine Position: Verschieben oder Drehen nimmt das Ziel mit. Ein Ziel, das gerade nicht auf der Platine liegt, löst nichts aus. Das ausgelöste Ziel castet ganz normal mit Cast-Zeit und wird eingereiht; steht es schon in der Warteschlange, zählt der Auslöser als verpasst. **Kreise sind erlaubt**, das sind die Loops; begrenzt werden sie nur durch Cast-Zeiten.

Datenmodell: Die Platine im Kampf ist ein Graph (`Arena/Graph/LogicGraph.cs`): Knoten sind Relais und Komponenten, Kanten sind Auslöser (`GraphEdgeKind.Trigger`). Seit A-20 auch Chips und Pins als Knoten sowie Versorgung, Gatter-Eingänge und Pulsverbindungen als Kanten (siehe «Pins, Traces and Logic Chips»). Der Kampf bleibt deterministisch: gleiche Seeds ergeben dieselben Kämpfe, auch mit Kreisen.

Build-Fenster: In jeder Zeile stehen die Modul-Plätze von Baustein und Skill (◆ besetzt, ◇ frei). Ein Modul wird auf einen freien Platz gezogen und zurück in die Modul-Liste abgenommen. Ein Klick auf einen Auslöser wählt das nächste Ziel (alle Zeilen, dann «kein Ziel»). Rechts an den Zeilen sind Auslöser als Linien gezeichnet (orange vom Skill, türkis vom Baustein, Pfeil am Ziel), jede Verbindung auf eigener Spur, sodass Kreise sichtbar bleiben. Das HUD zeigt pro Zeile ◆ (Module) und ↪ (Auslöser-Ziel), die Modul-Liste im Build alle Module mit Ort.

#### Warteschlange (A-13, seit A-19 ohne Cooldowns)

Die Regeln stehen jetzt oben unter «Circuit Board» (Queue). Daten in `Arena/RowQueue.cs` (`QueueConfig.Default`, pro Kampf über `BattleSetup.Queue`, `MaxEntriesPerComponent` = 1).

#### Wachsen und Evolution

**Wachstum** ist ein Zähler pro Exemplar: jedes Skill-Exemplar und jedes Relais zählt selbst mit. Er gilt für den ganzen Run, wandert durch die Akte, bleibt beim Umsetzen eines Skills und beim Ablegen einer Rune ins Runen-Inventar. Gezählt wird nach jedem Kampf aus dem Kampfprotokoll (`Growth/GrowthTally`), nur für Komponenten, die gefeuert haben, und die Relais, die sie ausgelöst haben. Was wächst, steht als Daten in `GrowthCatalog.CreateDefault`:

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

**Evolution:** Ein Exemplar auf Höchststufe (Skill: Stufe 3 aus Wachstum 30, Baustein: höchste Lagerfeuer-Stufe der Rune) plus eine Rezeptbedingung entwickelt sich **nach dem nächsten überlebten Boss** zur Evolutionsform. Wachstum, Instanz-Id, Ort und Module bleiben. Die Bedingung ist eines von: ein bestimmtes Modul am Skill bzw. Baustein, ein bestimmtes Relais, das die Komponente berührt (versorgt), oder ein Ausrüstungs-Tag auf Schwelle 4. Sechs Rezepte als Daten (`Evolution/EvolutionCatalog.CreateDefault`), eines pro Tag:

| Tag | Aus | Bedingung | Evolution |
|---|---|---|---|
| Hitze | Entzünden | Modul Fläche | Feuersturm: 60 % an alle Gegner, alle brennen 5 s |
| Ladung | Schockstich | Tag Ladung 4 | Blitzlanze (1×1): 80 %, 50 % Chance auf 1,5 s Betäubung |
| Phantom | Baustein «HP unter 30/40/50 %» | Modul Verlängern | «HP unter 50 % oder ausgewichen» (gilt auch direkt nach einem Ausweichen) |
| Takt | Echo-Protokoll | Modul Mehrfach | Resonanz (2×1): wiederholt den letzten Skill zweimal |
| Toxin | Bohrstoß | Relais «Gegner unter … %» versorgt ihn | Säurebohrer (2×2): 230 % an alle Gegner, doppeltes Gift |
| Schrott | Schildschlag | Tag Schrott 4 | Schrottramme: 90 % durch Rüstung, unterbricht, 2,5 s Betäubung |

Evolutionsformen werden nie angeboten, man erreicht sie nur über ein Rezept. Angebote, Shop, Inventar und das Rezeptbuch im Build zeigen den Fortschritt («Evolution ???: fehlt Modul Fläche», «→ Ladung 3/4 für Evolution von Schockstich»), das Build-Fenster pro Zeile (Tooltip und ✦), das HUD ein ✦ an Zeilen, die nach dem nächsten Boss evolvieren.

**Rezeptbuch:** Unentdeckte Evolutionen und Duos stehen als Silhouette «???» mit einem Hinweis im Inventar, entdeckte mit ihrem Rezept. Das Buch wird über Runs gespeichert (in Unity in den PlayerPrefs, `Overworld/Persistence/PlayerPrefsRecipeBookStore`, im Core hinter `IRecipeBookStore`). Es ist reines Wissen: Gespeichert werden nur Einträge wie «evo:evo_inferno», nie Werte oder Boni.

#### Schwierigkeit und Bonus der Relais

Grundschwierigkeit 0–3 pro Relais (Daten im `RuneCatalog`, `difficulty` und `invertedDifficulty`). Tabelle mit Grössen-Grenze und Bonus oben unter «Size limit by difficulty». Der Basisangriff bekommt nie einen Bonus.

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

**Anzeige:** Build-Fenster, Runen-Inventar, HUD, Runen-Angebote und Shop zeigen das Symbol farbig am Baustein (◇ grau, ◆ blau, ◆◆ orange, ◆◆◆ rot), der Tooltip den Bonus. Die Infozeile einer Komponente zeigt die Werte **inklusive** Bonus (Grösse, Cast-Zeit, Schaden) und im Tooltip «Baustein ◆◆ Schwer: … (eingerechnet)». Angebote von Teilen, Modulen und Skills nennen, welche Bausteine sie leichter machen («Erleichtert: «Gegner betäubt» ◆◆ (Eigene Betäubungen dauern +1 s)»).

### Die Arena lesen

Die Arena zeigt die eigene Platine live und erklärt, *warum* eine Komponente feuert oder wartet.

- **Platine live:** Relais leuchten auf, wenn sie auslösen, und zählen mit, wie oft. Komponenten tragen ihren Farbstreifen und einen Zustand: feuert (hervorgehoben), eingereiht, eingefroren (mit Restzeit), «not powered (too large)», «not powered», ohne Skill. Der Tooltip nennt die versorgenden Relais und den letzten verpassten Auslöser.
- **Warteschlange:** Unter der Platine steht, was wartet, in Lesereihenfolge (#n), mit der Regel als Tooltip (`QueueConfig.RuleText`). Darunter eine Legende der Zustände und die Basisangriff-Zeile («fills the gaps»).
- **Gegner-Platine:** Maus über dem Gegner zeigt seine Platine (`EnemyBoard.Lines`), ebenso auf der Karte über Gegner-, Elite- und Bossfeldern (mögliche Gegner des Feldes).
- **Missed Trigger:** Ein Auslösen, das die Komponente nicht einreihen konnte, steht im Protokoll mit Grund: schon eingereiht, zu gross, eingefroren, ohne Skill.
- **Kämpfer, schwebende Zahlen, Protokoll-Filter, 4×:** wie bisher (Ressourcen und Zustände unter dem Lebensbalken, Zahlen in der Farbe der Komponente, Filter «Alles / Meine Aktionen / Nur Schaden», Hervorhebungen bei 4× mindestens 0,35 s).
- **Auswertung nach dem Kampf** (vor «Weiter»): Tabelle pro Komponente mit Power, «Fired», «Triggered», Schaden, Heilung, Anteil, «Bonus» (inkl. eingesparter Cast-Zeit), «Queued» (Anzahl und mittlere Wartezeit), «Missed Trigger» und dem Hauptgrund. Dazu Hinweise, z. B. für nie versorgte oder zu grosse Komponenten.

Im Core: Schaden, Heilung und Zustände einer Aktion tragen die Komponente (`BattleEvent.RowIndex`), Relais-Auslösungen `BattleEventKind.RelayTriggered` mit `BattleEvent.Relay`. `BattleReport.Create(result)` rechnet die Auswertung aus dem Protokoll, `BattlePlayback` liefert Zustände (`RowStateAt`, `IsRelayLit`, `FrozenLeft`), Warteschlange und gefilterte Protokollzeilen.

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

### Keywords

| Keyword | Meaning |
|---|---|
| Circuit Board | Your combat program: a grid (4×3 up to 6×6) with a Core, relays and components |
| Relay | 1×1 chip with a condition (a former rune), e.g. "On Hit", "Clock 2 s". Powers the components it touches |
| Component | A skill placed on the board with a shape (1×1 … 2×3). Reading order (#n) is its queue priority |
| Core | Fixed 1×1 cell; touching components get +10 % effect |
| Rune | The condition of a relay. Has a level (better parameter) and a difficulty |
| Skill | What a component does. Each skill is a copy in your Collection with its own Growth and Level |
| Basic Attack | Weapon attack that fills every gap |
| Cast Time | Wind-up before a skill hits. Floor 0.1 s |
| Recovery | Short pause after a skill |
| Not powered (too large) | The component is bigger than the size limit of every relay touching it |
| Haste / Slow | Lower / higher Cast Time for a while. The Haste tag also lowers Cast Time, Haste stacks raise attack speed |
| Freeze | Stops the target's largest powered component for a few seconds |
| Queue / Queued | Triggered components wait in the queue in reading order, at most once each |
| Triggered | How often a relay triggered a component |
| Missed Trigger | A trigger that could not queue the component (already queued, too large, frozen) |
| Trigger | Module that starts another component after this one fires (↪) |
| Pin | Contact on a component edge. Touching pins connect two components |
| Typed Pin | Pin that asks for a skill kind; a matching neighbour gives +15 % effect |
| Trace | Chip that connects distant pins (Straight, Corner, T, Cross) |
| Pulse | Signal a firing component sends along its connections, one connection per tick |
| Diode | One-way connection |
| AND / OR / NOT Gate | Chips that combine the relays they touch and power touching components |
| Capacitor | Stores up to 3 pulses and releases them together |
| Fuse | Once per fight, a Very hard relay |
| Repeat | An action that runs a second time (↻), e.g. from Multicast or Echo |
| Module | Rare add-on for a skill or rune (Multicast, Area, Chain, Invert, Threshold, Trigger …) |
| Difficulty | Easy, Medium, Hard, Very Hard: harder relays power bigger components and give a bigger bonus (◆) |
| Easer | Item or module that makes a hard rune happen more often, without losing its bonus |
| Growth / Level | Growth points from fights; Levels at 5/15/30 |
| Evolution | A skill or rune turns into a stronger one after a survived boss when its recipe is met. Found recipes go into the Recipe Book |
| Set | Items of one set give bonuses at 2 and 3 pieces |
| Tag | Synergy tag on items (Heat, Charge, Phantom, Haste, Toxin, Scrap); tiers at 2/4/6 pieces |
| Duo | Bonus for two tags both at 4 pieces; hidden until found |
| Burn / Poison | Damage over time |
| Stun | The target can't act |
| Blind | Lower Accuracy |
| Armor Break | Lower Armor |
| Heat / Charge | Resources of the knight, shown as bars |
| Thermal Throttling | From 30 s on, every 5 s: longer computing time and more damage for both sides |
| Circuit effects, Hacks | See «Circuit Effects, Hacks and Thermal Throttling» |
| Rune Shards | 3 shards open a reward choice |
| Gold Mine | Gives gold every few turns, can be raided |
| Portal | Escape from the boss after surviving |

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
│   │   ├── Runes/         Runen (Bedingungen der Relais), Runenwahl, RuneInventory
│   │   ├── Circuit/       CircuitBoard (Raster, Kern, Relais, Komponenten, Chips), CircuitConfig, Formen und Zellen, Pins (PinCatalog), Chips (ChipCatalog), Effects (CircuitEffectCatalog: Effekte, Form als Daten)
│   │   ├── Arena/         Kampfsimulator: Battle (Tick-Schleife, Pulse, Gatter, Kondensatoren, Effekte, Hacks, Thermal Throttling), Thermal (ThermalConfig), Combatant, LogicBoard, Wiring (Pulsverbindungen), Conditions/ (Runen-Bedingungen + ConditionRegistry),
│   │   │                  Effects/ (ISkillEffect), Statuses/, Skills/ (SkillCatalog), BattleModifier, Playback/ (Wiedergabe + Protokolltext),
│   │   │                  Insight/ (BattleReport: Auswertung pro Komponente nach dem Kampf)
│   │   ├── Gear/          Ausrüstung: EquipmentCatalog, Equipment, Inventory (Item-Raster mit fester Reihenfolge, IInventoryItem), BuildStats, BoardFactory (Platine → Kampf-Tafel), Sets/ (SetBonusRegistry),
│   │   │                  Synergies/ (SynergyRegistry: Tags, Schwellen, Duos als Daten; Wirkungen als BattleModifier)
│   │   ├── Combat/        ICombatResolver, ArenaCombatResolver, EnemyCatalog, EnemyBoard, EnemyLoadout (Gegner-Ausrüstung, Beute, EnemyEncounter fest je Feld; Platzhalter-Resolver nur noch für Tests)
│   │   ├── Shop/          Shop-Bestand, Preise, Angebote für Lock (ShopOffer)
│   │   ├── Autoplay/      Testspieler: AutoplayBot (Strategie), BotAction, AutoplayRecorder + AutoplayReport/AutoplaySummary (JSON),
│   │   │                  AutoplayOptions (Kommandozeile), HeadlessAutoplay (Lauf ohne Darstellung)
│   │   ├── OverworldSession.cs              Fassade: Bewegung, kleine/mittlere Events, Runenwahl
│   │   ├── OverworldSession.MajorEvents.cs  Fassade: Kampf, Truhe, Goldmine, Shop (Reroll mit steigendem Preis)
│   │   ├── OverworldSession.ShopLocks.cs    Fassade: gesperrte Shop-Angebote über Besuche und Akte
│   │   ├── OverworldSession.Salvage.cs      Fassade: Bergen nach dem Sieg (Teile der Gegner-Platine), fester Gegner je Feld
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
- **Eine Fassade.** `OverworldSession` ist der einzige Einstieg für Spielaktionen. Die wichtigste Methode ist `TryStep(HexCoord)`: Sie prüft die Regeln, bewegt den Spieler, deckt den Nebel auf, beendet den Zug und löst das Feld-Event aus. Reisen laufen über `TryTravelStep(HexCoord, lastStep)` und `FinishTravel()`: nur der letzte (oder anhaltende) Schritt beendet den Zug, `FinishTravel` schliesst einen abgebrochenen ab. Entscheidungen laufen über `ChooseEncounterOption`, `TakeRune`/`SkipRuneOffer` und die Shop-Methoden. Solange eine Entscheidung offen ist (`IsBusy`), ist Bewegung gesperrt.
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
aus der Stat-Leiste); freie Skills als Komponenten neben ein Relais, das sie versorgen kann (bevorzugt am Kern), Runen als Relais auf freie Felder,
Module an den ersten passenden Ort, Chips dorthin, wo sie am meisten verbinden (Pulsverbindungen, Gatter mit Eingängen), Auslöser bekommen ein Ziel; Lagerfeuer: ruhen unter 60 % HP, sonst Rune verstärken;
Shop: heilen, Modul, Skill, besseres Teil, Platinen-Erweiterung, Rune, Chip, dann verlassen; Minen erobern und verteidigen; nach dem
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
| `boardRows` | Platine am Ende: Komponenten und Relais [Module] |
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

## Glossar Deutsch → Englisch (Team)

Seit A-18 sind alle Spieltexte Englisch. Texte stehen pro Bereich an einem Ort: `Core/CatalogTexts.cs` (lose Katalogtexte), `Core/Arena/ArenaTexts.cs` (Kampf, Protokoll, Auswertung, Zahlenformat), `Core/SessionTexts.cs` (Oberwelt-Meldungen), `Core/Autoplay/AutoplayTexts.cs` (Testspieler) und `Overworld/UI/UiTexts.cs` (Fenster). Namen und Beschreibungen von Skills, Runen, Teilen usw. bleiben in ihren Katalogen. Zahlen: `0.4 s`, `25 %`. Ein Test (`EnglishTextTests`) prüft alle Kataloge auf Umlaute.

| Deutsch | Englisch |
|---|---|
| Platine, Relais, Komponente, Kern | Circuit Board, Relay, Component, Core |
| nicht versorgt (zu gross) | not powered (too large) |
| Pin, typisierter Pin, Leiterbahn, Puls | Pin, Typed Pin, Trace, Pulse |
| UND-/ODER-/NICHT-Gatter, Kondensator, Sicherung, Diode | AND/OR/NOT Gate, Capacitor, Fuse, Diode |
| Rune / Logikbaustein / Baustein | Rune (in Hinweisen zur Schwierigkeit auch "block") |
| Bedingung, Basisangriff | Condition, Basic Attack |
| Auslöser, ausgelöst, Wiederholung | Trigger, triggered, Repeat |
| Warteschlange, eingereiht | Queue, queued |
| übersprungen (nur verwaist), verwaist | skipped, orphaned |
| Modul, Erleichterer | Module, Easer |
| Leicht / Mittel / Schwer / Sehr schwer | Easy / Medium / Hard / Very Hard |
| Wachstum, Stufe (Skill), Stufe (Tag) | Growth, Level, Tier |
| Rezeptbuch, Sammlung, Inventar, Ausrüstung | Recipe Book, Collection, Inventory, Gear |
| Teil, Platz, Synergie-Tag | Item / piece, Slot, Tag |
| Wirkung (passiv) | power |
| Cast-Zeit, Erholung, Takt (Basisangriff) | Cast Time, Recovery, Interval |
| Waffenschaden, Flächenschaden, Heilung | Weapon Damage, Area Damage, Healing |
| Rüstung, Ausweichen, Krit, Präzision, Angriffe/s | Armor, Dodge, Crit, Accuracy, Attacks/s |
| Betäubung, Brennen, Gift, Blendung, Rüstungsbruch | Stun, Burn, Poison, Blind, Armor Break |
| Hitze, Ladung, Takt (Tag), Schrott | Heat, Charge, Haste, Scrap |
| Tempo-Stapel | Haste stacks |
| Überhitzung (Zeitlimit), Hitze (Overclock) | Thermal Throttling, Heat |
| Übertakten, Unterbrechung, Paralleler Thread, Puffer, Rekursion | Overclock, Interrupt, Parallel Thread, Buffer, Recursion |
| Verstärker, Wachhund, Überlauf, Firewall | Amplifier, Watchdog, Overflow, Firewall |
| Bit-Kipper, Störung, Kapern, Kurzschluss, Verzögerung | Bit Flip, Jam, Hijack, Short Circuit, Latency |
| Neu würfeln, Sperren | Reroll, Lock |
| Bergen, Gegner-Ausrüstung | Salvage, enemy gear |
| Rechenzeit (= Cast-Zeit) | Computing time |
| Protokoll, Auswertung | Log, Report |
| Zug, Akt, Runensplitter, Lagerfeuer, Truhe, Goldmine | Turn, Act, Rune Shards, Campfire, Chest, Gold Mine |
| Spiel vorbei, Testspieler | Game Over, Autoplay |
| Runenwahl | Choose a Reward |
| Klingen-, Schild-, Funkenritter | Blade Knight, Shield Knight, Spark Knight |

Katalognamen:

| Bereich | Deutsch → Englisch |
|---|---|
| Skills | Rüstungsbruch → Armor Break, Entzünden → Ignite, Schildschlag → Shield Bash, EMP-Schildschlag → EMP Shield Bash, Not-Reparatur → Emergency Repair, Kühlmittel-Injektion → Coolant Injection, Notfall-Schildwall → Emergency Shield Wall, Blendgranate → Flashbang, Bohrstoß → Drill Strike, Bodenanker → Ground Anchor, Schubdüsen → Thrusters, Schockstich → Shock Stab, Echo-Protokoll → Echo Protocol, Ladungsspule → Charge Coil, Lähmnebel → Numbing Mist |
| Evolutionen | Feuersturm → Firestorm, Blitzlanze → Lightning Lance, Resonanz → Resonance, Säurebohrer → Acid Drill, Schrottramme → Scrap Ram |
| Skill-Arten | Angriff → Attack, Schild → Shield, Feuer → Fire, Schock → Shock, Heilung → Healing, Bewegung → Movement |
| Runen-Tags | Klinge → Blade, Schild → Shield, Funke → Spark, Glut → Ember, Phantom → Phantom |
| Runen | Jeder n. Angriff → Every n Attacks, Nach jedem n. Angriff → After Every n Attacks, Alle n Sekunden → Every n Seconds, Kampfbeginn → Battle Start, Jeder n. erlittene Treffer → Every n Hits Taken, Kette → Chain, Nach eigenem Skill → After Own Skill, Bei Treffer → On Hit, Nach Krit → After Crit, Gegner unter n % → Enemy Below n %, Gegner gepanzert → Enemy Armored, Gegner betäubt → Enemy Stunned, Gegner stärker → Enemy Stronger, Tempo ≥ n → Haste ≥ n, Wenn getroffen → When Hit, Nach Block → After Block, Gegner lädt auf → Enemy Charging, HP voll → HP Full, Schwerer Treffer → Heavy Hit, Ladung voll → Charge Full, HP unter n % → HP Below n %, Nach Selbstschaden → After Self-Damage, Nach Heilung → After Healing, Gegner brennt → Enemy Burning, Gegner fällt → Enemy Falls, Überhitzung → Overheat, Nach Ausweichen → After Dodge, n Ausweicher in Folge → n Dodges in a Row, Immer → Always, Auf Goldmine → On Gold Mine, Gegen Boss → Vs. Boss, In Unterzahl → Outnumbered, Letzter Gegner → Last Enemy, HP unter n % oder ausgewichen → HP Below n % or Dodged |
| Module | Mehrfach → Multicast, Fläche → Area, Kette → Chain, Blutzoll → Blood Toll, Schnellcast → Quickcast, Umkehren → Invert (Label "NOT"), Verlängern → Extend, Schwelle → Threshold, Auslöser → Trigger, Alarmfühler → Alarm Sensor, Witterung → Scent |
| Sets | Überlast-Protokoll → Overload Protocol, Aegis-Firewall → Aegis Firewall, Schrott-Ernter → Scrap Harvester, Phantom-Signal → Phantom Signal |
| Duos | Glutrhythmus → Ember Rhythm, Phasenschild → Phase Shield, Brandgift → Fire Venom, Schrottkondensator → Scrap Capacitor, Geisterschritt → Ghost Step, Säurefraß → Acid Bite |
| Plätze | Helm → Helmet, Handschuhe → Gloves, Brust → Chest, Beinschienen → Legs, Waffe → Weapon, Schild → Shield, Stiefel → Boots |
| Teile (Auswahl) | Kurzklinge → Short Blade, Kurzschwert → Short Sword, Rundschild → Round Shield, Funkenstab → Spark Staff, Titanhammer → Titan Hammer, Turmschild → Tower Shield, Holo-Projektor → Holo Projector, Gift-Injektor → Toxin Injector, Konterschild → Counter Shield |
| Gegner | Schrottratte → Scrap Rat, Rostwächter → Rust Warden, Funkendrohne → Spark Drone, Panzerkäfer → Armor Beetle, Rattenrudel → Rat Pack, Schmelzläufer → Smelter, Belagerungs-Golem → Siege Golem, Der Verwalter → The Overseer |
| Events | Verstreute Münzen → Scattered Coins, Heilkräuter → Healing Herbs, Wegweiser → Signpost, Händlerspuren → Merchant Tracks, Dornengestrüpp → Thorn Thicket, Wanderer → Wanderer, Blutschrein → Blood Shrine, Verletzter Söldner → Wounded Mercenary, Verschütteter Vorrat → Buried Cache |
| Belohnungs-Quellen (auch Schlüssel in `ProgressionConfig`) | Sieg → Victory, Elite-Sieg → Elite Victory, Mine verteidigt → Mine Defended, Schatztruhe → Treasure Chest, Runensplitter → Rune Shards |
| Autoplay-Endgründe / Belohnungs-Schlüssel im JSON | Akt n erreicht → Act n reached, Zuglimit → Turn limit, Hänger → Hang; Heilung → Healing, Runen-Stufe → Rune Level, Tafel-Erweiterung → Board Expansion |

## Hinweise

- Ohne installiertes Input-System-Paket zeigt Unity eventuell eine Warnung zur fehlenden Referenz `Unity.InputSystem` in `Betaknight.Overworld.asmdef`. Das ist unkritisch, der Code fällt dann auf den alten Input Manager zurück.
- Die `.meta`-Dateien erzeugt Unity beim ersten Öffnen. Bitte mitcommitten.
