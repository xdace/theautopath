namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Alle Spieler-Texte der IMGUI-Oberfläche an einem Ort: Fenstertitel, Knöpfe, Spaltenköpfe, Tooltips, Legenden,
    /// Hinweise und Format-Vorlagen. Vorbereitung für eine spätere Lokalisierung; Katalog-Texte (Namen und Beschreibungen
    /// von Skills, Runen, Teilen …) stehen weiter in ihren Katalogen. Farb-Tags bleiben in den Fenstern.
    /// </summary>
    public static class UiTexts
    {
        // ------------------------------------------------------------------ Allgemein

        public const string Back = "Back";
        public const string Free = "free";
        public const string Close = "✕";
        public const string BasicAttack = "Basic Attack";
        public const string ReadOnly = "Read only: fight or open decision";
        public const string FallbackLine = BasicAttack + " fills the gaps when nothing is queued";
        public const string SwapRelayHint = "… or swap the rune of a relay (the old rune goes to the inventory with its level; position and modules stay):";
        public const string NotPowered = "not powered";
        public const string NotPoweredTooLarge = "charging (too large)";
        public const string Powered = "powered";

        public static string OnBoard(int number) => $"#{number} on board";
        public static string BoardFull(string name) => $"<b>{name}</b>: no free cell on the board";
        public static string Cells(int cells) => cells == 1 ? "1 cell" : $"{cells} cells";
        public static string Owned(string list) => $"Owned: {list}";
        public static string Grows(string rule) => $"Grows: {rule}";
        public static string PriceTag(int price) => $"  – {price} Gold";

        // ------------------------------------------------------------------ HUD

        public static class Hud
        {
            public const string Unknown = "unknown";
            public const string Nothing = "nothing";
            public const string MineLost = "lost";
            public const string BuildButton = "Build (B)";
            public const string BuildTip = "Circuit Board, skills, modules and runes";
            public const string PossibleEnemies = "Possible enemies (rolled when you enter):";
            public const string EnemyHere = "Enemy waiting here:";
            public static string Loot(string list) => $"Loot: {list}";
            public static string SalvageAfterVictory(int picks) => $"Salvage {picks} after victory";
            public const string NoComponents = "<color=#888888>no components</color>";
            public const string InventoryTip = "Gear and items";
            public const string OpenShop = "Open Shop";
            public const string NewRun = "New Run";
            public const string UnknownDuo = "Duo ???: its effect shows in the next fight.";

            public static string Title(string kit, int act) => $"<b>Betaknight{kit}</b>   Act {act}";
            public static string BossIn(int turns) => $"Boss in {turns} turns";
            public static string Turn(int turn, string boss) => $"Turn: {turn}   {boss}";
            public static string Resources(int hp, int maxHp, int gold, int shards) => $"HP: {hp}/{maxHp}   Gold: {gold}   Shards: {shards}";
            public static string Board(string size, string max, int relays, int components, string list) =>
                $"Circuit Board {size} (max {max}): {relays} relays, {components} components\n{list}";
            public static string EnemyBoard(string name, string lines) => $"<b>{name}</b>\n{lines}";
            public static string Gear(string list) => $"Gear: {list}";
            public static string Sets(string list) => $"Sets: {list}";
            public static string Tags(string list) => $"Tags: {list}";
            public static string MineAttacked(int turnsLeft) => $"under attack, {turnsLeft} turns left";
            public static string Mine(string coord, string state) => $"Mine {coord}: {state}";
            public static string Position(string coord) => $"Position: {coord}";
            public static string Tile(string text) => $"Tile: {text}";
            public static string Seed(int seed) => $"Seed: {seed}";
            public static string Pointer(string coord, string info) => $"Pointer: {coord} – {info}";
            public static string InventoryButton(int count, int capacity) => $"Inventory (I)  {count}/{capacity}";
            public static string Duo(string name, string effect) => $"Duo {name}: {effect}";
            public static string DuoName(string name) => $"Duo {name}";
            public static string Resolved(string text) => $"{text} (resolved)";

            public static string BuildSummary(int weapon, int itemLevels, int runeLevels, int items, int runes) =>
                $"Build: Weapon Damage {weapon}, gear +{itemLevels}, rune levels +{runeLevels}, "
                + $"inventory {items} items / {runes} runes";

            public const string Enemy = "Enemy";
            public const string Boss = "Boss";
            public const string Elite = "Elite Enemy";
            public const string Shop = "Shop";
            public const string Treasure = "Treasure Chest";
            public const string GoldMine = "Gold Mine";
            public const string Start = "Start";
        }

        // ------------------------------------------------------------------ Meldungen unten links

        public static class Messages
        {
            public static string MineRaid(string coord, int turns) =>
                $"<color=#ff7a6b><b>Gold Mine {coord} under attack!</b></color> {turns} turns to defend it";

            public static string MineLost(string coord) =>
                $"<color=#ff7a6b><b>Gold Mine {coord} lost.</b></color> Recapture it to win it back";

            public static string Improved(string text) => $"<color=#7ddc6f><b>Improved:</b> {text}</color>";

            public static string ChipGained(string name) =>
                $"<color=#ffd75e><b>New logic chip:</b> {name}</color> – place it on the board in Build (B)";
        }

        // ------------------------------------------------------------------ Portal, Game Over, Kit-Wahl

        public static class Portal
        {
            public const string Title = "<b>The escape portal is open</b>";

            public static string Text(int nextAct) =>
                $"It leads to Act {nextAct}: a new map with stronger enemies. "
                + "Knight, gear, board, gold and shards come along; captured mines stay behind.";

            public static string Button(int nextAct) => $"Through the portal → Act {nextAct}";
        }

        public static class GameOver
        {
            public const string Title = "<b>The knight has fallen</b>";
            public const string NewRun = "New Run";

            public static string Summary(int act, int turn, string boardSize, int relays, int gold) =>
                $"Act {act}, Turn {turn}, board {boardSize} with {relays} relays, {gold} Gold";
        }

        public static class Kit
        {
            public const string Title = "<b>Choose your knight</b>";
            public const string NoRune = "none";
            public const string NoSkill = "no skill";

            public static string Label(string name, string tag, int maxHp, int gold, string description, string rune, string skill) =>
                $"<b>{name}</b>  [{tag}]   {maxHp} HP, {gold} Gold\n{description}\nStart: relay {rune} → {skill} (next to the Core)";
        }

        // ------------------------------------------------------------------ Inventar voll

        public static class InventoryFull
        {
            public const string ItemTitle = "<b>Inventory full</b>";
            public const string ItemHint = "Discard an item to make room for the new one:";
            public const string RuneTitle = "<b>Rune inventory full</b>";
            public const string RuneHint = "Discard a rune to make room for the new one:";

            public static string New(string text) => $"New: {text}";
            public static string DiscardItem(string name, string slot, string details) => $"Discard <b>{name}</b> [{slot}]\n<size=13>{details}</size>";
            public static string DiscardRune(string name, string description) => $"Discard <b>{name}</b>\n<size=13>{description}</size>";
            public static string Reject(string name) => $"Decline {name}";
        }

        // ------------------------------------------------------------------ Runen, Skills, Module, Teile

        public static class Rune
        {
            public const string EasersKeepBonus = "\nEasers (gear, modules, skills) do not lower the bonus.";

            public static string Difficulty(string symbol, string tooltip) => $"Difficulty {symbol} {tooltip}";
            public static string Inverted(string symbol, string name) => $"\nInverted: {symbol} {name}";
        }

        public static class Module
        {
            public const string SkillKind = "Skill Module";
            public const string BlockKind = "Rune Module";
            public const string TriggerKind = "Trigger";
            public const string AnotherCopy = "Another Copy";
            public const string AnotherCopyTip = "A second module at level 0 for another slot";

            public static string Title(string name, string kind) => $"<b>Module: {name}</b>  [{kind}]";
            public static string Upgrade(string from, int level) => $"▲ Level Up ({from} → +{level})";
            public static string UpgradeTip(string from, string where) => $"{from} ({where}) gets stronger";
        }

        public static class Skill
        {
            public const string SecondCopy = "Second Copy";
            public const string SecondCopyTip = "Another copy with growth 0, e.g. as a second component";

            public static string Title(string name) => $"<b>Skill: {name}</b>";
            public static string Growth(int amount, string from, int grown) => $"▲ Growth +{amount} ({from} → +{grown})";
            public static string GrowthTip(string from, string where, int amount, string milestone) => $"{from} ({where}) grows by {amount}: {milestone}";
            public static string TakeAnother(string takeLabel) => $"{takeLabel} (another copy)";
        }

        public static class Item
        {
            public const string TwoHandedLocksShield = "two-handed, locks shield";
            public const string TwoHanded = ", two-handed";
            public const string Equipped = "equipped";
            public const string NoStats = "no stats";
            public const string SlotFree = "slot free: ";
            public const string New = "  (new)";
            public const string StatsNone = "Stats: none";
            public const string StatsPrefix = "Stats: ";

            public static string SameStats(string worn) => $"same stats as {worn}";
            public static string Instead(string worn) => $"instead of {worn}: ";
            public static string SetWorn(int pieces, int max) => $"{pieces}/{max} worn";
            public static string SetWith(int pieces, int max) => $"with this item {pieces}/{max}";
            public static string SetHead(string name, string head) => $"Set <b>{name}</b> ({head})";
            public static string Pieces(int pieces, string text) => $"{pieces} pieces: {text}";
            public static string TagWith(int count) => $"with this item {count}";
            public static string TagWorn(int count) => $"{count} worn";
            public static string TagHead(string name, string head) => $"Tag <b>{name}</b> ({head}; tiers at 2/4/6 pieces)";
            public static string Passive(string text) => $"Passive: <color=#ffd75e>{text}</color>";
            public static string Tags(string names) => $"Tags: {names}";
            public static string Set(string name) => $"Set: {name}";

            public static string MaxHp(string sign, int value) => $"{sign}{value} Max HP";
            public static string Damage(string sign, int value) => $"{sign}{value} Damage";
            public static string Armor(string sign, int value) => $"{sign}{value} Armor";
            public static string Faster(int ticks) => $"faster ({ticks} ticks)";
            public static string Slower(int ticks) => $"slower ({ticks} ticks)";
            public static string Dodge(string sign, int percent) => $"{sign}{percent} % Dodge";
            public static string Block(string sign, int percent) => $"{sign}{percent} % Block";
            public static string Crit(string sign, int percent) => $"{sign}{percent} % Crit";
        }

        // ------------------------------------------------------------------ Stat-Leiste

        public static class Stats
        {
            public const string Bonuses = "Bonuses";
            public const string NoBonuses = "no active set bonuses or tag tiers";
            public const string BonusTip = "Tag tiers apply from 2/4/6 pieces with the same tag, set bonuses from 2 pieces of the same set. Everything is listed in the Inventory.";

            public static string Lost(string bonus) => $"{bonus} lost";
            public static string Preview(string title) => $"<b>Preview {title}:</b>  ";
        }

        // ------------------------------------------------------------------ Shop

        public static class Shop
        {
            public const string Title = "<b>Shop</b>";
            public const string SoldOut = "Sold out.";
            public const string BuyAndEquip = "Buy and Equip";
            public const string BuyToInventory = "Buy, to Inventory";
            public const string BuySkill = "Buy Skill";
            public const string BuyModule = "Buy Module";
            public const string BuyToRuneInventory = "Buy, to Rune Inventory";
            public const string Leave = "Leave Shop";

            public static string Status(int gold, int hp, int maxHp, string size, int relays) => $"Gold: {gold}   HP: {hp}/{maxHp}   Board: {size}, {relays} relays";
            public static string RuneLabel(string badge, string name, string tag, int price, string description, string hints) =>
                $"{badge}  <b>{name}</b>  [{tag}]  – {price} Gold\n{description}{hints}";
            public static string ItemHead(string name, string slot, int price, string set) => $"<b>{name}</b>  [{slot}]  – {price} Gold{set}";
            public static string BuyAndEquipSwap(string worn) => $"Buy and Equip ({worn} to inventory)";
            public static string Heal(int amount, int price) => $"Heal (+{amount} HP) – {price} Gold";
            public static string BoardExpansion(string from, string to, int price) => $"Board Expansion {from} → {to} – {price} Gold";
            public static string BoardMaxed(string max) => $"Board at maximum size ({max})";
            public const string BoardExpansionSold = "Board Expansion sold out here";
            public static string Reroll(int price) => $"Reroll Offer – {price} Gold";
            public static string SellTitle(int item, int rune) => $"<b>Sell</b> (half price: item {item} Gold, rune {rune} Gold)";
            public static string SellItem(string name, string slot, int price) => $"Sell {name} [{slot}]  +{price} Gold";
            public static string SellRune(string name, int price) => $"Sell Rune {name}  +{price} Gold";
            public static string ChipLabel(string name, int price, string description) =>
                $"<color=#ffd75e>▣</color>  <b>{name}</b>  [Logic Chip]  – {price} Gold\n{description}";
            public const string ChipTip = "Logic chip (rare): goes to your logic chip inventory. Place, move and rotate it in Build (B).";

            // A-21: Lock und steigender Reroll-Preis.
            public const string Lock = "Lock";
            public const string Locked = "<color=#ffd75e><b>■ Locked</b></color>";
            public const string LockedMark = "  <color=#ffd75e>■ locked</color>";
            public static string LockTip(int slots) =>
                $"Lock this offer: it stays when you reroll and comes back in your next shop until you buy or unlock it. Up to {slots} locks at once.";
            public static string LockFullTip(int slots) => $"All {slots} locks are in use – unlock another offer first.";
            public const string UnlockTip = "Locked: stays on reroll and in your next shop. Click to unlock.";
            public static string Locks(int used, int slots) => $"Locks: {used}/{slots}";
            public static string RerollTip(int next) => $"Rerolls runes, items, skills, modules and chips. Locked offers stay. Each reroll this visit costs more (next after this: {next} Gold).";
        }

        // ------------------------------------------------------------------ Belohnung (Angebot)

        public static class Offer
        {
            public const string LevelUp = "▲ Level Up";
            public const string Upgrade = "▲ Upgrade";
            public const string Equipped = "equipped";
            public const string InInventory = "in inventory";
            public const string Equip = "Equip";
            public const string ToInventory = "To Inventory";
            public const string TakeSkill = "Take Skill (free, into the collection)";
            public const string TakeModule = "Take Module (free, into the collection)";
            public const string Rare = "rare";
            public const string ToRuneInventory = "Put in Rune Inventory";

            public static string Title(string source) => $"<b>Choose a Reward</b> – {source}";
            public static string Status(string size, int relays, int items, int itemCapacity, int stored, int runeCapacity) =>
                $"Board: {size}, {relays} relays   Inventory: {items}/{itemCapacity} items, {stored}/{runeCapacity} runes";
            public static string UpgradeLevel(int level) => $"Level +{level}";
            public static string EquipSwap(string worn) => $"Equip ({worn} to inventory)";
            public static string BoardExpansion(string from, string to, string max) =>
                $"<color=#7ddc6f>▲ Board Expansion</color>  ({from} → {to}, max {max})";
            public static string BoardMaxed(string max) => $"Board Expansion (board already at its maximum {max})";
            public static string Skip(int gold) => $"Skip (+{gold} Gold)";
            public static string MaxLevel(string name) => $"{name} (max level)";
            public static string Instead(string name) => $"instead of <b>{name}</b>";
        }

        // ------------------------------------------------------------------ Inventar

        public static class Inventory
        {
            public const string CloseTip = "Close (I or Esc)";
            public const string Knight = "<b>Knight</b>";
            public const string Locked = "locked (two-handed)";
            public const string Empty = "empty";
            public const string Details = "<b>Details</b>";
            public const string DetailsHint = "Click an item to see its stats, passive effects, set and the comparison with the equipped item. "
                + "Hovering an item shows in the stat bar above what would change.";
            public const string Unequip = "Unequip (to Grid)";
            public const string Equip = "Equip";
            public const string Discard = "Discard";
            public const string TagsTitle = "<b>Synergy Tags and Sets</b>";
            public const string TagsLegend = "thresholds at 2/4/6 worn pieces, Duo from 4 + 4";
            public const string DuoUnknown = "Its effect shows in the next fight.";
            public const string GridTitle = "<b>Items</b>";
            public const string GridLegend = "gear and later consumables, your order is kept";
            public const string Hint = "Drag: item from the grid onto its slot on the figure = equip (wrong slot turns red), back to the grid = unequip, "
                + "cell onto cell = swap, onto an empty cell = move. Double-/right-click: equip or unequip.";

            public static string Title(int count, int capacity, int gold) => $"{count}/{capacity} cells used · Gold {gold}";
            public const string TitleName = "Inventory";
            public static string PreviewEquip(string name) => $"\"{name}\" equip";
            public static string PreviewUnequip(string name) => $"\"{name}\" unequip";
            public static string DuoActive(string name, string text) => $"<b>Duo {name}</b> active: {text}";
        }

        // ------------------------------------------------------------------ Build (Platinen-Editor)

        public static class Build
        {
            public const string TitleName = "Build";
            public const string FiresRule = "a relay that triggers queues the components it powers";
            public const string CloseTip = "Close (B or Esc)";
            public const string HoverHint = "Hover a component, relay, skill, rune or module to see its short stats here.";
            public const string SkillsTitle = "<b>Skills</b>";
            public const string NoSkills = "no skills yet";
            public const string AllSkillsOnBoard = "all skills are on the board – drag one here to take it off";
            public const string AlwaysAvailable = "fills the gaps";
            public const string BasicAttackTip = "The basic attack is not placed on the board: it fires whenever nothing is queued.";
            public const string SkillsKeepBar = "– skills do not change the stats in the bar";
            public const string BoardTitle = "<b>Circuit Board</b>";
            public const string BoardLegend = "drag to move · right-click rotates · R rotates while dragging";
            public const string CoreName = "CORE";
            public const string EmptyCellTip = "Free cell: drop a skill (component), a rune (relay) or a logic chip here.";
            public const string ComponentsTitle = "<b>Components</b>";
            public const string RelaysTitle = "<b>Relays</b>";
            public const string NoComponents = "no components yet – drag a skill from the left onto the board";
            public const string NoRelays = "no relays – drag a rune from the chip inventory onto a free cell";
            public const string PowersNothing = "powers nothing yet – place a component next to it (edge, not corner)";
            public const string NotPoweredTip = "No relay touches this component at an edge, so it never fires. Put a relay next to it.";
            public const string EvolvesTip = "Evolves after the next boss";
            public const string TriggerClick = "\nClick, then click a component on the board: link it as the target (Esc cancels)";
            public const string ModuleBadgeTip = "Right-click or double-click: take off · drag: move to another part or back to the module list";
            public const string ModuleBadgeTipTargeted = "Click, then click a component: link the target · right-click: take off · drag: move";
            public const string TakeOffTip = "Take this module off (it stays in your collection)";
            public const string Trash = "Discard";
            public const string TrashTip = "Drag a skill, component, relay, rune, module or chip here to throw it away for good (you are asked first). Modules on it become free.";
            public static string DiscardAsk(string name) => $"Discard {name} for good?";
            public const string DiscardYes = "Yes";
            public const string DiscardNo = "No";
            public const string LinkClick = "click, then click a component on the board to link it";
            public static string LinkingHint(string module, string where) =>
                $"Linking {module} ({where}): click the component it should trigger or charge · Esc cancels";
            public const string FreeRuneModuleSlot = "Free module slot on the relay: drag a relay module here";
            public const string FreeSkillModuleSlot = "Free module slot on the component: drag a skill module here";
            public const string ModulesAndRunes = "Modules and Chips";
            public const string RecipeBook = "Recipe Book";
            public const string ModulesTitle = "<b>Modules</b>";
            public const string NoModules = "none yet – rare from elites, chests, boss escape and shop";
            public const string RuneInventoryTitle = "<b>Chips</b> (rune inventory)";
            public const string RuneInventoryEmpty = "empty – drag a relay here to take it off the board";
            public const string RecipeBookTitle = "<b>Recipe Book</b>";
            public const string RecipeBookLegend = "kept across runs, knowledge only";
            public const string UnknownDuo = "<b>Duo ???</b>";
            public const string NoEffect = "no effect";
            public const string RelayDragHint = "Drag to move · onto the chip inventory = take off · right-click = take off";
            public const string ComponentDragHint = "Drag to move · right-click = rotate · onto the skill list = remove";
            public const string RuneDropHint = "Drag onto a free cell = new relay, onto a relay = swap · double-click = best free cell";
            public const string Hint = "Drag: skill onto the board = place, component or relay = move (R rotates while dragging), right-click a component = rotate, "
                + "rune onto a free cell = new relay, rune onto a relay = swap, module onto a part or ◇ = place, back into a list = remove. "
                + "Logic chips: drag from the strip onto a free cell, right-click rotates, back onto the strip = take off. "
                + "Double-click: place on the best cell / take off.";

            public static string BoardSize(string size, string max, bool canGrow) =>
                canGrow ? $"{size} (grows to {max} via Board Expansions)" : $"{size} (maximum)";
            public static string Dragging(string label) => $"Dragging: {label}";
            public static string DraggingRotated(string label) => $"Dragging: {label} (rotated)";
            public static string DraggingTurned(string label, int degrees) => degrees == 0 ? Dragging(label) : $"Dragging: {label} (rotated {degrees}°)";
            public static string FreeOf(int free, int all) => $"{free} free of {all}";
            public static string BoardRule(string limits, int corePercent, string rule) =>
                "<b>Circuit Board</b>: A relay powers every component it touches at an edge (not a corner), up to its size limit "
                + $"({limits}). Bigger ones are not powered (too large); unpowered components never fire. When a relay triggers, "
                + $"its powered components are queued and fire with their cast time. No cooldowns. Components touching the Core get +{corePercent} % effect. {rule}";
            public static string Limit(string symbol, int cells) => $"{symbol} {Cells(cells)}";
            public static string CoreTip(int percent) => $"<b>Core</b>: components touching it get +{percent} % effect. Nothing can be placed here.";
            public static string CoreBonus(int percent) => $"Core +{percent} %";
            public static string PoweredBy(string relays) => $"powered by {relays}";
            public static string TooLargeTip(int cells, int limit) =>
                $"{Cells(cells)}, the strongest touching relay gives {limit} charge per trigger: it needs {(cells + limit - 1) / System.Math.Max(1, limit)} triggers to run. The charge is kept until it is full, and all touching relays fill the same store. A harder relay (◆ 2, ◆◆ 4, ◆◆◆ 6) needs fewer.";
            public static string Powers(string components) => $"powers {components}";
            public static string TooLargeHere(string components) => $"too large here: {components}";
            public static string RelayLimit(int cells) => $"powers up to {Cells(cells)}";
            public static string MaxCells(int cells) => $"≤{cells}";
            public static string TriggersTo(int number) => $"↪#{number}";
            public static string ChargesTo(int number) => $"⚡#{number}";
            public static string Evolution(string name) => $"<b>Evolution {name}</b>";
            public static string Duo(string name, string effect) => $"<b>Duo {name}</b>: {effect}";
            public static string GrowsNow(string rule, string effect, string milestone) =>
                $"Grows: {rule}{(effect.Length > 0 ? $" · now {effect}" : string.Empty)} ({milestone})";
            public static string GrowsRelay(string rule, string effect, string milestone) =>
                $"Grows: {rule}{(effect.Length > 0 ? $" · {effect}" : string.Empty)} ({milestone})";
            public static string ModuleSlots(int used, int slots) => $"Module slots {used}/{slots}";
        }

        // ------------------------------------------------------------------ Pins, Logik-Chips und Pulse (A-20)

        public static class Circuit
        {
            public const string ChipsTitle = "<b>Logic Chips</b>";
            public const string ChipsEmpty = "none yet – rare from elites, chests, boss escape and shop";
            public const string ChipsLegend = "drag onto a free cell · R rotates while dragging · drop a placed chip here to take it off";
            public const string StripDropHint = "Drop a placed logic chip here to take it off the board.";
            public const string ChipDragHint = "Drag to move · right-click = rotate · onto the logic chip strip = take off";
            public const string InventoryDragHint = "Drag onto a free cell = place · R rotates while dragging · double-click = first free cell";
            public const string And = "AND";
            public const string Or = "OR";
            public const string Not = "NOT";
            public const string Fuse = "FUSE";
            public const string Open = "open";
            public const string Closed = "closed";
            public const string Blown = "blown";
            public const string NoInputs = "no touching relay yet – place it next to a relay";
            public const string PlainPin = "Pin";
            public const string PulseLegend = "⚡ pulses run along the lines";
            public const string Watchdog = "WDOG";

            public static string ChipTip(string name, string description) => $"<b>{name}</b>: {description}";
            public static string GateInputs(string inputs) => $"Inputs: {inputs}";
            public static string GateDifficulty(string tooltip) => $"Difficulty: {tooltip}";
            public static string GatePowers(string components) => $"powers {components}";
            public const string GatePowersNothing = "powers nothing yet – place a component next to it";
            public static string GateState(string state, int count) => $"Now: {state} · triggered {count}× so far";
            public static string DiodeDirection(string from, string to) => $"Pulses pass only from {from} to {to}.";
            public static string Capacitor(int stored, int capacity) => $"Stored pulses: {stored}/{capacity}";
            public static string CarriesLinks(string links) => $"Carries: {links}";
            public static string Link(string from, string to, int hops) =>
                hops <= 1 ? $"{from} → {to}" : $"{from} → {to} ({hops - 1} chip{(hops - 1 == 1 ? "" : "s")})";
            public static string PulsesTo(string links) => $"⚡ Pulses to: {links}";
            public static string PulsesFrom(string links) => $"⚡ Pulses from: {links}";
            public static string PulsePath(string path) => $"⚡ Path: {path}";
            public static string PinsLine(string pins) => $"Pins: {pins}";
            public static string TypedPin(string kind, bool matched, int percent) =>
                matched ? $"{kind} pin ✔ +{percent} %" : $"{kind} pin (needs a {kind} skill in front)";
            public static string PinTip(string kind, bool matched, bool linked, int percent)
            {
                string head = kind == null ? "<b>Pin</b>: connects to a touching pin or a trace." : matched
                    ? $"<b>{kind} pin</b>: matched, +{percent} % effect."
                    : $"<b>{kind} pin</b>: put a {kind} skill in front of it for +{percent} % effect. Also connects like a normal pin.";
                return linked ? $"{head}\nConnected: pulses run through this pin." : head;
            }
            public static string SideName(int side) => side == 0 ? "top" : side == 1 ? "right" : side == 2 ? "bottom" : "left";
            public static string FiredByPulse(string path) => $"Why: fired by a pulse ({path})";
            public static string Why(string text) => $"Why: {text}";
            public static string AmplifierGain(int percent) => $"+{percent}%";
            public static string AmplifiedLink(int amplifiers, int percent) =>
                $"amplified ×{amplifiers}: +{percent} % effect";
        }

        // ------------------------------------------------------------------ Eigene Effekte der Platine (A-21)

        public static class Effects
        {
            public static string Tip(string icon, string name, string description) => $"<b>{icon} {name}</b>: {description}";
            public static string Line(string icons) => $"Effects: {icons}";
            public static string SkillLine(string icon, string name) => $"Circuit effect: {icon} {name}";
            public static string ModuleLine(string icon, string name) => $"Effect module: {icon} {name}";
            public static string Board(string effects) => $"Board rules: {effects}";

            public static string Heat(int heat, int max) => $"Heat {heat}/{max}";
            public static string HeatTip(int heat, int max) =>
                $"Heat {heat}/{max} (Overclock). At {max} Heat this component skips one execution, then its Heat resets.";
            public const string HeatSkip = "OVERHEAT";
            public static string Depth(int depth) => $"↻{depth}";
            public static string DepthTip(int depth, int percent) => $"Recursion depth {depth}: +{percent} % effect";
            public static string Power(int percent) => $"+{percent}%";
            public static string PowerTip(int percent) => $"Amplified: +{percent} % effect";
            public const string Parallel = "parallel";
            public const string QueueJump = "interrupt";
            public const string Overflow = "overflow";
            public const string StackLimit = "stack limit";
            public const string ShortCircuit = "short circuit";
            public const string Hijacked = "hijacked";

            public static string Flipped(string left) => $"FLIP {left}";
            public static string Jammed(int left) => $"JAM ×{left}";
            public const string HijackMark = "HIJACK";
            public static string FlippedTip(string relay, string left) => $"<b>Hacked: Bit Flip</b> – {relay} is inverted for {left} more (its condition flips).";
            public static string JammedTip(string relay, int left) => $"<b>Hacked: Jam</b> – {relay} ignores its next {left} trigger{(left == 1 ? "" : "s")}.";
            public static string HijackedTip(string component) => $"<b>Hacked: Hijack</b> – the next execution of {component} happens for the hacker.";
            public static string Firewall(int charges) => $"Firewall ×{charges}";
            public static string FirewallTip(int charges) => $"<b>Firewall</b>: blocks the next {charges} enemy hack{(charges == 1 ? "" : "s")}.";

            public static string Thermal(int level, int castPercent, int damagePercent) =>
                $"Thermal Throttling {level}: +{castPercent}% computing time, +{damagePercent}% damage";
            public const string ThermalTip =
                "Thermal Throttling: the fight ran long, both boards heat up. Each step makes computing times longer and damage higher for everyone, so every fight ends.";
        }

        // ------------------------------------------------------------------ Bergen (Gegner-Platinen)

        public static class Salvage
        {
            public const string KindSkill = "Skill";
            public const string KindModule = "Module";
            public const string KindChip = "Chip";
            public const string KindRune = "Rune";
            public const string Take = "Take";
            public const string LeaveRest = "Leave the rest";
            public const string BoardTitle = "<b>Enemy board</b>  <size=13><color=#9aa4b2>hover a part below to find it on the board</color></size>";
            public const string NoBoard = "<color=#9aa4b2>The enemy board of this fight is not available.</color>";
            public const string TakeTip = "Skills and modules go into your collection, chips into the chip inventory, runes onto a free cell of the board (or the rune inventory).";

            public static string Title(string enemy) => $"<b>Salvage</b> – {enemy}";
            public static string PicksLeft(int picks) => $"Pick {picks} more";
            public static string RuneLine(string badge, string name, string tag, string description) => $"{badge}  <b>{name}</b>  [{tag}]\n<size=13>{description}</size>";
            public static string ChipLine(string icon, string name, string description) => $"{icon}<b>{name}</b>\n<size=13>{description}</size>";
        }

        // ------------------------------------------------------------------ Arena

        public static class Arena
        {
            public const string Enemy = "Enemy";
            public const string PortalOpen = "Portal open";
            public const string Repeat = "↻ Repeat";
            public const string BoardTitle = "<b>Circuit Board</b>";
            public const string BoardLegend = "relays light up when they trigger · hover a part";
            public const string QueueEmpty = "Waiting: –";
            public const string EnemyBoardTitle = "<b>Enemy board</b>";
            public const string EnemyBoardHint = "hover an enemy to read its board";

            public const string StateIdle = "idle";
            public const string StateQueued = "queued";
            public const string StateFiring = "firing";
            public const string StateFrozen = "frozen";
            public const string StateUnpowered = "not powered";
            public const string StateTooLarge = "charging";
            public const string StateOrphaned = "no skill";

            public const string NowIdle = "waiting for its relay to trigger";
            public const string NowQueued = "queued, waiting for its turn (in trigger order)";
            public const string NowFiring = "firing";
            public const string NowUnpowered = "not powered: no relay touches it, it never fires";
            public const string NowTooLarge = "charging (too large): each trigger adds the relay's limit, it runs once the charge reaches its size";
            public const string NowOrphaned = "no skill";

            public const string ReportTitle = "<b>Report</b>";
            public const string SplitTip = "Share of damage: basic attack vs skills. Goal with 3 or more damage skills: basic attack at most about 30 %.";
            public const string NoRowDamage = "without a component (set bonuses, recoil)";

            public const string HeaderRow = "<b>Component</b>";
            public const string HeaderPower = "<b>Power</b>";
            public const string HeaderPowerTip = "Is a relay powering this component? Unpowered or too large components never fire.";
            public const string HeaderFired = "<b>Fired</b>";
            public const string HeaderFiredTip = "How often this component started an action (↪ by a trigger module, ⚡ by a pulse, ↻ repeated).";
            public static string PulsedByTip(string by) => $"⚡ started by pulses from {by}";
            public const string HeaderTriggered = "<b>Triggered</b>";
            public const string HeaderTriggeredTip = "How often a relay or trigger reached this component (queued or missed). Hard relays need easers or a build that makes them happen.";
            public const string HeaderDamage = "<b>Damage</b>";
            public const string HeaderHealing = "<b>Healing</b>";
            public const string HeaderShare = "<b>Share</b>";
            public const string HeaderShareTip = "Share of total damage.";
            public const string HeaderBonus = "<b>Bonus</b>";
            public const string HeaderBonusTip = "Extra damage, healing and saved cast time from the relay's difficulty bonus.";
            public const string HeaderQueued = "<b>Queued</b>";
            public const string HeaderQueuedTip = "How often this component was queued, and the average wait until it started.";
            public const string HeaderCharged = "<b>Charged</b>";
            public const string HeaderChargedTip = "Triggers that only stored charge (+ how much, and what was left at the end). The charge is kept: the component runs as soon as it reaches its size, and queues again after its cast while charge is left.";
            public const string HeaderMissed = "<b>Missed Trigger</b>";
            public const string HeaderMissedTip = "Triggers that really did nothing: already queued without charge, no charge left for it, frozen, or no skill.";
            public const string HeaderOther = "<b>Main reason</b>";
            public const string HeaderOtherTip = "Most common reason for the missed triggers.";

            public const string LogTitle = "<b>Log</b>";
            public const string FilterAll = "All";
            public const string FilterMine = "My Actions";
            public const string FilterDamage = "Damage Only";

            public const string OpenBuild = "Open Build (B)";
            public const string Continue = "Continue";
            public const string Pause = "Pause";
            public const string PauseKeyTip = "Pause / continue (Space)";
            public const string NextAction = "Next action ▸";
            public const string NextActionTip = "Jump to the next skill you cast and pause there (N). «.» steps one tick.";
            public const string KeysHint = "Space pause · 1–4 speed · N next · . step";
            public static string SpeedKeyTip(int key) => $"Playback speed (key {key}); 1× is half the game speed, remembered";
            public const string Skip = "Skip";

            public const string StatusBurn = "Burn";
            public const string StatusStun = "Stun";
            public const string StatusArmorBreak = "A. Break";
            public const string StatusShieldWall = "Wall";
            public const string StatusBlind = "Blind";
            public const string StatusAnchor = "Anchor";
            public const string StatusThrusters = "Thrust";
            public const string StatusLatency = "Lag";

            public static string Title(string enemy, string time, string portal) =>
                $"<b>Arena</b> – Knight vs {enemy}   <color=#9aa4b2>{time}</color>{portal}";
            public static string PortalOpensIn(string time) => $"Portal opens in {time}";
            public static string Cast(string seconds) => $"Cast {seconds}";
            public static string FromComponent(int number) => $"↪ from #{number}";
            public static string Charging(string skill, string cast) => $"charging: {skill} ({cast})";
            public static string StatusTip(string name, string left, int stacks) =>
                $"{name}: {left} left{(stacks > 1 ? $", {stacks} stacks" : string.Empty)}";
            public static string NowFrozen(string left) => $"frozen ({left} left)";
            public static string RelayTip(string name, int count, string powers) =>
                $"<b>{name}</b>: triggered {count}× so far\n{powers}";
            public static string Powers(string components) => $"powers {components}";
            public const string PowersNothing = "powers nothing";
            public static string ComponentHead(string name, string shape, string relays) => $"<b>{name}</b> ({shape})\n{relays}";
            public static string PoweredBy(string relays) => $"powered by {relays}";
            public static string EnemyTip(string name, string lines) => $"<b>{name}</b> – board:\n{lines}";
            public static string EnemyModules(string list) => $"Modules: {list}";
            public static string EnemyRelayTip(string name, string powers) => $"<b>{name}</b>\n{powers}";
            public const string EnemyBoardsLive = "boards of the enemies on the stage · hover a part";
            public static string LootWaiting(int picks) => $"Salvage {picks} {(picks == 1 ? "part" : "parts")} from the enemy board next";
            public static string Now(string now) => $"Now: {now}";
            public static string NowAndLast(string now, string last) => $"Now: {now}\nLast: {last}";
            public static string Report(string outcome, string time, int damage, int healing) =>
                $"<b>Report</b>   {outcome}, {time}, total damage {damage}, healing {healing}";
            public static string QueueAverage(int queued, string wait) => $"{queued}× · avg {wait}";
            public static string CastSaved(string time) => $"−{time} cast";
        }
    }
}
