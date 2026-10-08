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
        public const string Always = "Always";
        public const string FallbackRow = "↓ [" + Always + "] → " + BasicAttack;
        public const string SwapRowHint = "… or swap a row (the old rune goes to the inventory with its level, the skill stays):";

        public static string Row(int number) => $"Row {number}";
        public static string BoardFull(string name) => $"<b>{name}</b>: board is full";
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
            public const string BuildTip = "Logic Board, skills, modules and runes";
            public const string InventoryTip = "Gear and items";
            public const string OpenShop = "Open Shop";
            public const string NewRun = "New Run";
            public const string UnknownDuo = "Duo ???: its effect shows in the next fight.";

            public static string Title(string kit, int act) => $"<b>Betaknight{kit}</b>   Act {act}";
            public static string BossIn(int turns) => $"Boss in {turns} turns";
            public static string Turn(int turn, string boss) => $"Turn: {turn}   {boss}";
            public static string Resources(int hp, int maxHp, int gold, int shards) => $"HP: {hp}/{maxHp}   Gold: {gold}   Shards: {shards}";
            public static string Board(int runes, int rows, int maxRows, string list) => $"Logic Board: {runes} runes, rows {rows}/{maxRows}\n{list}";
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

            public static string Summary(int act, int turn, int runes, int gold) => $"Act {act}, Turn {turn}, {runes} runes, {gold} Gold";
        }

        public static class Kit
        {
            public const string Title = "<b>Choose your knight</b>";
            public const string NoRune = "none";

            public static string Label(string name, string tag, int maxHp, int gold, string description, string rune) =>
                $"<b>{name}</b>  [{tag}]   {maxHp} HP, {gold} Gold\n{description}\nStarting Rune {rune}";
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
            public const string SecondCopyTip = "Another copy with growth 0, e.g. for a second row";

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

            public static string Status(int gold, int hp, int maxHp, int runes, int slots) => $"Gold: {gold}   HP: {hp}/{maxHp}   Runes: {runes}/{slots}";
            public static string RuneLabel(string badge, string name, string tag, int price, string description, string hints) =>
                $"{badge}  <b>{name}</b>  [{tag}]  – {price} Gold\n{description}{hints}";
            public static string ItemHead(string name, string slot, int price, string set) => $"<b>{name}</b>  [{slot}]  – {price} Gold{set}";
            public static string BuyAndEquipSwap(string worn) => $"Buy and Equip ({worn} to inventory)";
            public static string Heal(int amount, int price) => $"Heal (+{amount} HP) – {price} Gold";
            public static string RuneSlot(int price) => $"Extra Rune Slot – {price} Gold";
            public static string BoardFull(int rows) => $"Board full ({rows} rows)";
            public static string Reroll(int price) => $"Reroll Offer – {price} Gold";
            public static string SellTitle(int item, int rune) => $"<b>Sell</b> (half price: item {item} Gold, rune {rune} Gold)";
            public static string SellItem(string name, string slot, int price) => $"Sell {name} [{slot}]  +{price} Gold";
            public static string SellRune(string name, int price) => $"Sell Rune {name}  +{price} Gold";
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
            public static string Status(int runes, int slots, int items, int itemCapacity, int stored, int runeCapacity) =>
                $"Board: {runes}/{slots}   Inventory: {items}/{itemCapacity} items, {stored}/{runeCapacity} runes";
            public static string UpgradeLevel(int level) => $"Level +{level}";
            public static string EquipSwap(string worn) => $"Equip ({worn} to inventory)";
            public static string BoardExpansion(int from, int to, int max) =>
                $"<color=#7ddc6f>▲ Board Expansion: +1 row</color>  ({from} → {to} of {max})";
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

        // ------------------------------------------------------------------ Build (Tafel-Editor)

        public static class Build
        {
            public const string TitleName = "Build";
            public const string FiresRule = "topmost met row with a ready skill fires";
            public const string CloseTip = "Close (B or Esc)";
            public const string HoverHint = "Hover a skill, rune or module to see its short stats here.";
            public const string SkillsTitle = "<b>Skills</b>";
            public const string NoSkills = "no skills yet";
            public const string AlwaysAvailable = "always available";
            public const string BasicAttackTip = "Drag the basic attack onto a row. No copy needed.";
            public const string SkillsKeepBar = "– skills do not change the stats in the bar";
            public const string BoardTitle = "<b>Logic Board</b>";
            public const string BoardLegend = "Rune (When) → Skill (What) · drag ≡ to reorder · hover for details";
            public const string EmptyRow = "— empty: drag a skill here (row paused)";
            public const string EvolvesTip = "Evolves after the next boss";
            public const string FallbackLegend = "fixed, always last";
            public const string TriggerClick = "\nClick: choose next target";
            public const string FreeRuneModuleSlot = "Free module slot on the rune: drag a rune module here";
            public const string FreeSkillModuleSlot = "Free module slot on the skill: drag a skill module here";
            public const string ModulesAndRunes = "Modules and Runes";
            public const string RecipeBook = "Recipe Book";
            public const string ModulesTitle = "<b>Modules</b>";
            public const string NoModules = "none yet – rare from elites, chests, boss escape and shop";
            public const string RuneInventoryTitle = "<b>Rune Inventory</b>";
            public const string RuneInventoryEmpty = "empty – drag a row here to unequip its rune";
            public const string RecipeBookTitle = "<b>Recipe Book</b>";
            public const string RecipeBookLegend = "kept across runs, knowledge only";
            public const string UnknownDuo = "<b>Duo ???</b>";
            public const string NoEffect = "no effect";
            public const string NoCooldown = " · no CD";
            public const string RowDragHint = "Drag onto the rune inventory = unequip · right-click = unequip";
            public const string Hint = "Drag: skill onto row = place/swap, row onto row = reorder, rune onto row = swap, "
                + "rune onto free slot = new row, module onto ◇ = place, back into a list = remove. Double-/right-click: into row / out of row.";

            public static string Rows(int rows, int slots, int max) => $"Board {rows}/{slots} rows (max. {max})";
            public static string Dragging(string label) => $"Dragging: {label}";
            public static string FreeOf(int free, int all) => $"{free} free of {all}";
            public static string BoardRule(string rule) =>
                $"<b>Logic Board</b>: From top to bottom, the first row whose condition is met and whose skill is ready fires. {rule}";
            public static string RowLabel(int number, string name) => $"Row {number}: {name}";
            public static string FreeSlot(int number) => $"{number}.  + free slot – drag a rune from the rune inventory here";
            public static string Evolution(string name) => $"<b>Evolution {name}</b>";
            public static string Duo(string name, string effect) => $"<b>Duo {name}</b>: {effect}";
            public static string Cooldown(string seconds) => $" · CD {seconds}";
            public static string GrowsNow(string rule, string effect, string milestone) =>
                $"Grows: {rule}{(effect.Length > 0 ? $" · now {effect}" : string.Empty)} ({milestone})";
            public static string GrowsRow(string rule, string effect, string milestone) =>
                $"Grows: {rule}{(effect.Length > 0 ? $" · {effect}" : string.Empty)} ({milestone})";
            public static string ModuleSlots(int used, int slots) => $"Module slots {used}/{slots}";
        }

        // ------------------------------------------------------------------ Arena

        public static class Arena
        {
            public const string Enemy = "Enemy";
            public const string PortalOpen = "Portal open";
            public const string Repeat = "↻ Repeat";
            public const string BoardTitle = "<b>Logic Board</b>";
            public const string BoardLegend = "hover a row for the reason";
            public const string QueueEmpty = "Waiting: –";

            public const string StateReady = "ready";
            public const string StateConditionFalse = "condition false";
            public const string StateCooldown = "Cooldown";
            public const string StateQueued = "queued";
            public const string StateOrphaned = "orphaned";

            public const string NowReady = "condition met, skill ready";
            public const string NowConditionFalse = "condition not met";
            public const string NowOrphaned = "skipped (no skill)";
            public const string NowQueuedWaiting = "queued, waiting (action running)";
            public const string NowUndecided = "no decision yet";

            public const string ReportTitle = "<b>Report</b>";
            public const string SplitTip = "Share of damage: basic attack vs skills. Goal with 3 or more damage skills: basic attack at most about 30 %.";
            public const string NoRowDamage = "without a row (set bonuses, recoil)";

            public const string HeaderRow = "<b>Row</b>";
            public const string HeaderFired = "<b>Fired</b>";
            public const string HeaderFiredTip = "How often this row started an action (↪ by a trigger, ↻ repeated).";
            public const string HeaderTriggered = "<b>Triggered</b>";
            public const string HeaderTriggeredTip = "How often this row's condition was met. Hard blocks need easers or a build that makes them happen.";
            public const string HeaderDamage = "<b>Damage</b>";
            public const string HeaderHealing = "<b>Healing</b>";
            public const string HeaderShare = "<b>Share</b>";
            public const string HeaderShareTip = "Share of total damage.";
            public const string HeaderBonus = "<b>Bonus</b>";
            public const string HeaderBonusTip = "Extra damage, healing and saved cooldown from the block's difficulty bonus.";
            public const string HeaderQueued = "<b>Queued</b>";
            public const string HeaderQueuedTip = "How often this row was queued, and the average wait until it started.";
            public const string HeaderMissed = "<b>Missed Trigger</b>";
            public const string HeaderMissedTip = "How often this row's condition was not met while a lower row or the basic attack fired.";
            public const string HeaderOther = "<b>Other reason</b>";
            public const string HeaderOtherTip = "Most common reason besides a missed trigger, e.g. no skill (orphaned) or another action running.";

            public const string LogTitle = "<b>Log</b>";
            public const string FilterAll = "All";
            public const string FilterMine = "My Actions";
            public const string FilterDamage = "Damage Only";

            public const string OpenBuild = "Open Build (B)";
            public const string Continue = "Continue";
            public const string Pause = "Pause";
            public const string Skip = "Skip";

            public const string StatusBurn = "Burn";
            public const string StatusStun = "Stun";
            public const string StatusArmorBreak = "A. Break";
            public const string StatusShieldWall = "Wall";
            public const string StatusBlind = "Blind";
            public const string StatusAnchor = "Anchor";
            public const string StatusThrusters = "Thrust";

            public static string Title(string enemy, string time, string portal) =>
                $"<b>Arena</b> – Knight vs {enemy}   <color=#9aa4b2>{time}</color>{portal}";
            public static string PortalOpensIn(string time) => $"Portal opens in {time}";
            public static string Cast(string seconds) => $"Cast {seconds}";
            public static string FromRow(int number) => $"↪ from row {number}";
            public static string Charging(string skill, string cast) => $"charging: {skill} ({cast})";
            public static string StatusTip(string name, string left, int stacks) =>
                $"{name}: {left} left{(stacks > 1 ? $", {stacks} stacks" : string.Empty)}";
            public static string NowCooldown(string left) => $"skill on cooldown ({left} left)";
            public static string NowQueuedCooldown(string left) => $"queued, waiting for cooldown ({left} left)";
            public static string Now(string now) => $"Now: {now}";
            public static string NowAndLast(string now, string last) => $"Now: {now}\nLast: {last}";
            public static string Report(string outcome, string time, int damage, int healing) =>
                $"<b>Report</b>   {outcome}, {time}, total damage {damage}, healing {healing}";
            public static string QueueAverage(int queued, string wait) => $"{queued}× · avg {wait}";
        }
    }
}
