namespace Betaknight.Core
{
    /// <summary>
    /// Alle Spieler-Texte der Overworld-Session an einem Ort: Belohnungen, Shop, Wachstum, Evolution, Boss und Portal,
    /// Minen, Module, Fortschritt, «Build verbessert»-Meldungen, Event-Ergebnisse, Fehlergründe und die Stat-Leiste.
    /// Grundlage für eine spätere Lokalisierung.
    /// </summary>
    public static class SessionTexts
    {
        // ------------------------------------------------------------------ Journal

        public static string JournalMineRaid(string coord) => $"Gold Mine {coord} under attack!";
        public static string JournalMineRaidText(int turns) => $"{turns} turns to defend it";
        public static string JournalMineLost(string coord) => $"Gold Mine {coord} lost";
        public const string JournalMineLostText = "Recapture it to win it back";
        public const string JournalImproved = "Improved";
        public const string JournalChip = "New logic chip (place it in Build, B)";

        // ------------------------------------------------------------------ Belohnungen und Werte

        public static string GoldGain(int amount) => $"+{amount} Gold";
        public static string GoldCost(int amount) => $"-{amount} Gold";
        public static string HpGain(int amount) => $"+{amount} HP";
        public static string HpLoss(int amount) => $"-{amount} HP";
        public static string MaxHpGain(int amount) => $"+{amount} Max HP";
        public static string ShardGain(int amount) => amount == 1 ? "+1 Rune Shard" : $"+{amount} Rune Shards";
        public static string MineIncome(int gold, int turns) => $"+{gold} Gold every {turns} turns";

        // ------------------------------------------------------------------ Events (kleine Begegnungen)

        public const string AlreadyFullHealth = "Already fully healed";
        public static string TilesScouted(int count) => $"{count} tiles scouted";
        public const string NothingNearby = "Nothing new nearby";
        public const string NoRuneToUpgrade = "No rune can be upgraded";
        public static string RuneUpgraded(string name) => $"Rune upgraded: {name}";
        public const string NoTrailFound = "No trail found";
        public static string Discovered(string place) => $"{place} discovered";
        public const string NothingHappened = "Nothing happened.";

        public const string PlaceShop = "Shop";
        public const string PlaceTreasure = "Treasure Chest";
        public const string PlaceGoldMine = "Gold Mine";
        public const string PlaceEnemy = "Enemy";
        public const string PlaceBoss = "Boss";
        public const string PlaceElite = "Elite Enemy";
        public const string PlaceOther = "Location";

        // ------------------------------------------------------------------ Grosse Ereignisse (Kämpfe, Boss, Minen)

        public const string FightBoss = "Boss";
        public const string FightElite = "Elite Fight";
        public const string Fight = "Fight";
        public static string FightWon(string title) => $"{title} won";
        public const string Defeat = "Defeat";
        public const string Treasure = "Treasure Chest";
        public const string GoldMineCaptured = "Gold Mine captured";
        public const string MineRecaptured = "Mine recaptured";
        public const string GoldMineDefended = "Gold Mine defended";
        public const string MineFight = "Fight for the mine";

        /// <summary>Titel nach dem Boss-Kampf. Die Autoplay-Auswertung hängt nicht mehr an diesen Texten.</summary>
        public const string BossDefeated = "Boss defeated";
        public const string EscapedThroughPortal = "Escaped through the portal";
        public static string BoardExpansion(string before, string after) => $"Board expansion: {before} → {after}";
        public static string PortalOpen(int act) => $"Portal to Act {act} open";

        // ------------------------------------------------------------------ Build verbessert

        public static string BoardGrown(string before, string after) => $"Board {before} → {after}";
        public static string RuneLevelUp(string before, string after) => $"Rune {before} → {after}";
        public static string Upgrade(string before, string after) => $"{before} → {after}";
        public static string HealChange(string skill, int before, int after) => $"{skill} heals {before} → {after}";
        public static string NewSkill(string name, bool extraCopy) => extraCopy ? $"New Skill: {name} (another copy)" : $"New Skill: {name}";
        public static string NewModule(string name) => $"New Module: {name}";
        public static string ModuleUpgrade(string before, string after) => $"Module {before} → {after}";
        public static string DuoDiscovered(string duo, string tagA, string tagB) => $"Duo discovered: {duo} ({tagA} + {tagB})";
        public static string LevelReached(string name, int level) => $"{name}: Level {level} reached";
        public static string ModuleSlotUnlocked(string name, int slot) => $"{name}: Module Slot {slot} unlocked";

        // ------------------------------------------------------------------ Wachstum

        public static string GrowthDamage(int bonus) => $"+{bonus} Damage";
        public static string GrowthStun(string time) => $"+{time} stun";
        public static string GrowthPower(int bonus) => $"+{bonus} % power";
        public static string GrowthThreshold(int before, int after) => $"Threshold {before} → {after} %";
        public static string Growth(int growth) => $"Growth {growth}";
        public const string MilestoneLevelAndSlot = "Level and Module Slot";
        public static string MilestoneLevel(int level) => $"Level {level}";
        public const string MilestoneSlot = "Module Slot";
        public static string GrowthMilestone(int growth, int next, string what) => $"Growth {growth}, at {next}: {what}";
        public static string RuneQuoted(string name) => $"Rune \"{name}\"";

        // ------------------------------------------------------------------ Evolution

        public const string Unknown = "???";
        public static string RequirementModule(string module) => $"Module {module}";
        public static string RequirementRuneInRow(string rune) => $"powered by the relay \"{rune}\"";
        public static string RequirementTag(string tag, int threshold) => $"Tag {tag} {threshold}";
        public static string Recipe(string from, string requirement, string to) => $"{from} at max level + {requirement} → {to}";
        public static string MissingSkill(string skill) => $"Skill {skill}";
        public static string MissingLevel(int level) => $"Level {level}";
        public static string MissingLevelWithGrowth(int level, int growth, int needed) => $"Level {level} (Growth {growth}/{needed})";
        public const string MissingOnBoard = "on the board";
        public static string MissingRune(string rune) => $"Rune {rune}";
        public const string MissingMaxLevel = "max level";
        public static string MissingMaxLevelRune(int level, int max) => $"max level (Level {level}/{max}, Campfire or duplicate rune)";
        public static string MissingTag(string tag, int count, int threshold) => $"Tag {tag} {count}/{threshold}";
        public static string EvolutionReady(string name) => $"Evolution {name}: ready, after the next boss";
        public static string EvolutionMissing(string name, string missing) => $"Evolution {name}: missing {missing}";
        public static string EvolutionHintModule(string progress, string from) => $"{progress} (part of the recipe for {from})";
        public static string EvolutionHintRune(string progress, string from) => $"{progress} (as relay next to {from})";
        public static string EvolutionHintTag(string tag, int count, int threshold, string from) => $"→ {tag} {count}/{threshold} for evolution of {from}";
        public static string Evolution(string change) => $"Evolution: {change}";

        // ------------------------------------------------------------------ Module

        public static string SkillInRow(string skill, int component) => $"{skill} (#{component + 1})";
        public const string NoTarget = "no target";
        public const string TargetNotOnBoard = "target not on the board";
        public static string RuneRow(int relay) => $"Relay {relay + 1}";
        public static string HolderSkill(string skill, int component) => $"Skill {skill} (#{component + 1})";
        public static string HolderSkillFree(string skill) => $"Skill {skill} (free)";
        public const string Free = "free";
        public const string WhenMet = "When triggered";
        public const string AfterExecution = "After execution";
        public static string ModuleTrigger(string when, string target) => $"{when} → {target}";
        public static string ModuleChargeLink(string when, string target) => $"{when} » charges {target}";
        public static string ModuleLabel(string name) => $"Module: {name}";
        public static string ChipLabel(string name) => $"Chip: {name}";

        // ------------------------------------------------------------------ Synergien

        public const string DuoUnknown = "Duo ???\nEffect reveals itself in the next fight.";
        public static string DuoHint(string tag) => $"{tag} and a second tag, both at 4";

        // ------------------------------------------------------------------ Stat-Leiste

        public const string StatHp = "HP";
        public const string StatWeaponDamage = "Weapon Damage";
        public const string StatAttacksPerSecond = "Attacks/s";
        public const string StatArmor = "Armor";
        public const string StatDodge = "Dodge";
        public const string StatBlock = "Block";
        public const string StatCrit = "Crit";
        public const string StatAccuracy = "Accuracy";
        public const string StatAreaDamage = "Area Damage";
        public const string BonusActive = "active";

        // ------------------------------------------------------------------ Fehlergründe

        public const string NoItem = "No item.";
        public const string ShieldLocked = "The two-handed weapon blocks the shield slot.";
    }
}
