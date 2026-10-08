using System.Globalization;

namespace Betaknight.Core
{
    /// <summary>
    /// Lose Anzeigetexte der Kataloge (Namen von Skill-Arten, Plätzen, Schwierigkeiten, Zuständen, Wachstums-Regeln …)
    /// an einem Ort, damit eine spätere Lokalisierung sie findet. Die Katalog-Einträge selbst (Namen und Beschreibungen
    /// von Skills, Runen, Teilen, …) stehen weiter in ihren Katalogen.
    /// </summary>
    public static class CatalogTexts
    {
        // ------------------------------------------------------------------ Skill-Arten

        public const string KindAttack = "Attack";
        public const string KindShield = "Shield";
        public const string KindFire = "Fire";
        public const string KindShock = "Shock";
        public const string KindHealing = "Healing";
        public const string KindMovement = "Movement";

        // ------------------------------------------------------------------ Schwierigkeit

        public static readonly string[] DifficultyNames = { "Easy", "Medium", "Hard", "Very Hard" };

        public const string NoBonus = "no bonus";
        public static string BonusPower(int percent) => $"+{percent} % power";
        public static string BonusCast(int percent) => $"−{percent} % Cast Time";
        public static string BonusStatusDuration(string seconds) => $"+{seconds} status duration";
        public static string DifficultyTooltip(string name, string bonus, int maxCells) =>
            $"{name}: {bonus} · powers components up to {Cells(maxCells)}";

        /// <summary>«1 cell», «4 cells».</summary>
        public static string Cells(int count) => count == 1 ? "1 cell" : $"{count} cells";

        // ------------------------------------------------------------------ Erleichterer

        /// <summary>«Eases: "Enemy Stunned" ◆◆ (Own stuns last +1 s)».</summary>
        public static string Eases(string runeNames, string reliefText) => $"Eases: {runeNames} ({reliefText})";
        public static string EasedRune(string runeName, string symbol) => $"\"{runeName}\" {symbol}";
        public const string CounterModifierName = "Counter";

        // ------------------------------------------------------------------ Zustände

        public const string AnchorSummary = "Armor ×2, no Dodge";
        public const string ThrustersSummary = "next enemy hit surely misses";

        // ------------------------------------------------------------------ Ausrüstung

        public const string SlotHelmet = "Helmet";
        public const string SlotGloves = "Gloves";
        public const string SlotChest = "Chest";
        public const string SlotLegs = "Legs";
        public const string SlotWeapon = "Weapon";
        public const string SlotShield = "Shield";
        public const string SlotBoots = "Boots";

        public const string AllSkills = "All Skills";
        public static string KindSkills(string kinds) => $"{kinds} Skills";
        public static string PassivePower(string who, string sign, int value) => $"{who} {sign}{value} % power";
        public static string PassiveCast(string who, string sign, int value) => $"{who} {sign}{value} % Cast Time";

        // ------------------------------------------------------------------ Synergien und Sets

        public static string Pieces(int count) => count == 1 ? "1 piece" : $"{count} pieces";
        public static string TagHeader(string name, int count) => $"{name} {Pieces(count)}";
        public static string TierLine(string marker, int pieces, string text) => $"{marker} {Pieces(pieces)}: {text}";
        public static string ActiveTierLine(int pieces, string text) => $"{Pieces(pieces)}: {text}";
        public static string TagCounter(string name, int count, int next) => $"{name} {count}/{next}";
        public static string TagCounterMax(string name, int count) => $"{name} {count} (max)";
        public static string PreviewTag(string counter) => $"→ {counter}";
        public static string PreviewTagThreshold(string counter) => $"→ {counter}: Tier reached!";
        public static string PreviewDuo(string duoName) => $"→ Duo unlocked: {duoName}";

        // ------------------------------------------------------------------ Runen

        public const string RuneTagBlade = "Blade";
        public const string RuneTagShield = "Shield";
        public const string RuneTagSpark = "Spark";
        public const string RuneTagEmber = "Ember";
        public const string RuneTagPhantom = "Phantom";

        public static string RuneLevel(int level, int max) => $"Level {level}/{max}";
        public static string RuneLevelMax(int level, int max) => $"Level {level}/{max} max";

        // ------------------------------------------------------------------ Gegner

        public static string RelayHolder(int index) => $"Relay {index + 1}";
        public static string ComponentHolder(int index) => $"#{index + 1}";
        public static string EveryLabel(int seconds) => $"Every {seconds} s";
        public static string EnemyBoardLine(string relay, string skill, string shape, string description) =>
            $"{relay} → {skill} ({shape})" + (string.IsNullOrEmpty(description) ? string.Empty : $": {description}");
        public const string EnemyBoardBasicOnly = "Basic Attack only";
        public const string EnemyBoardTitle = "Board";

        /// <summary>«Charges 0.3 s, 300 % damage.» (Sekunden immer mit Punkt).</summary>
        public static string EnemyChargeDescription(double seconds, int damagePercent) =>
            $"Charges for {seconds.ToString("0.#", CultureInfo.InvariantCulture)} s, {damagePercent} % Damage.";

        // ------------------------------------------------------------------ Events

        public const string EncounterContinue = "Continue";

        // ------------------------------------------------------------------ Wachstum

        public const string GrowthRowWins = "+1 Growth per fight won in which it triggered";
        public const string GrowthSkillWins = "+1 Growth per fight won in which it fired";
        public static string GrowthKills(int cap) => $"+1 Damage per kill with this skill (max. +{cap})";
        public static string GrowthStuns(string capSeconds) => $"+0.1 s duration per stun (max. +{capSeconds})";
        public static string GrowthHeals(int cap) => $"+1 % Healing per heal (max. +{cap} %)";
        public static string GrowthWinsPower(int cap) => $"+1 % power per fight won in which it fired (max. +{cap} %)";
        public static string GrowthThreshold(int cap) => $"+1 % threshold per fight won in which it triggered (max. {cap} %)";
    }
}
