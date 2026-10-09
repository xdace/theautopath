using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Bonus und Grössen-Grenze einer Schwierigkeitsstufe (A-19, ohne Cooldown).</summary>
    public readonly struct DifficultyBonus
    {
        /// <summary>Wirkung (Schaden, Heilung, Schild) in Prozent stärker.</summary>
        public readonly int PowerPercent;

        /// <summary>Cast-Zeit in Prozent kürzer; die Untergrenze 0.1 s bleibt.</summary>
        public readonly int CastReductionPercent;

        /// <summary>Zusätzliche Dauer von Status-Wirkungen (Betäubung, Brennen, Buffs, Debuffs) in Ticks.</summary>
        public readonly int ExtraStatusTicks;

        /// <summary>Grösste Komponente in Zellen, die ein Relais dieser Stufe versorgen kann.</summary>
        public readonly int MaxCells;

        public DifficultyBonus(int powerPercent, int castReductionPercent = 0, int extraStatusTicks = 0, int maxCells = 1)
        {
            PowerPercent = Math.Max(0, powerPercent);
            CastReductionPercent = Math.Max(0, Math.Min(100, castReductionPercent));
            ExtraStatusTicks = Math.Max(0, extraStatusTicks);
            MaxCells = Math.Max(1, maxCells);
        }

        /// <summary>Kein Bonus auf den Skill (die Grössen-Grenze zählt nicht als Bonus).</summary>
        public bool IsNone => PowerPercent == 0 && CastReductionPercent == 0 && ExtraStatusTicks == 0;

        /// <summary>«+30 % power, −20 % Cast Time» oder «no bonus».</summary>
        public string Text
        {
            get
            {
                var parts = new List<string>();
                if (PowerPercent > 0) parts.Add(CatalogTexts.BonusPower(PowerPercent));
                if (CastReductionPercent > 0) parts.Add(CatalogTexts.BonusCast(CastReductionPercent));
                if (ExtraStatusTicks > 0) parts.Add(CatalogTexts.BonusStatusDuration(SkillInfo.Seconds(ExtraStatusTicks)));
                return parts.Count > 0 ? string.Join(", ", parts) : CatalogTexts.NoBonus;
            }
        }
    }

    /// <summary>
    /// Schwierigkeit als Daten (A-11, A-19): je schwerer ein Relais (Grundschwierigkeit 0–3), desto stärker die
    /// Komponenten, die es auslöst, und desto grösser dürfen sie sein. Die Stufe hängt nur am Relais; Ausrüstung,
    /// Module, Wachstum, Tags oder Skills, die es leichter erfüllbar machen (Erleichterer), ändern sie nicht.
    /// </summary>
    public sealed class DifficultyBonusConfig
    {
        public const int MaxTier = 3;

        private readonly DifficultyBonus[] _tiers;

        public DifficultyBonusConfig(params DifficultyBonus[] tiers)
        {
            if (tiers == null || tiers.Length != MaxTier + 1) throw new ArgumentException("Exactly four tiers (0 to 3).", nameof(tiers));
            _tiers = (DifficultyBonus[])tiers.Clone();
        }

        public DifficultyBonus this[int tier] => _tiers[Clamp(tier)];

        public static int Clamp(int tier) => Math.Max(0, Math.Min(MaxTier, tier));

        /// <summary>Grösste versorgbare Komponente für ein Relais dieser Stufe.</summary>
        public int MaxCells(int tier) => this[tier].MaxCells;

        /// <summary>
        /// Startwerte: 0 = bis 1 Zelle · 1 = +15 % Wirkung, bis 2 Zellen · 2 = +30 % Wirkung, −20 % Cast-Zeit, bis 4 Zellen ·
        /// 3 = +60 % Wirkung, −35 % Cast-Zeit, +1 s Status-Dauer, bis 6 Zellen.
        /// </summary>
        public static DifficultyBonusConfig Default { get; } = new DifficultyBonusConfig(
            new DifficultyBonus(0, maxCells: 1),
            new DifficultyBonus(15, maxCells: 2),
            new DifficultyBonus(30, 20, maxCells: 4),
            new DifficultyBonus(60, 35, Ticks.PerSecond, maxCells: 6));

        /// <summary>Ohne Bonus, Grenzen wie Standard (z. B. für Vergleiche in Tests).</summary>
        public static DifficultyBonusConfig None { get; } = new DifficultyBonusConfig(
            new DifficultyBonus(0, maxCells: 1), new DifficultyBonus(0, maxCells: 2), new DifficultyBonus(0, maxCells: 4), new DifficultyBonus(0, maxCells: 6));

        /// <summary>Der Skill mit dem Bonus dieser Stufe. Stufe 0, Basisangriff und null bleiben unverändert.</summary>
        public SkillDefinition Apply(SkillDefinition skill, int tier)
        {
            if (skill == null || skill.IsBasicAttack) return skill;
            DifficultyBonus bonus = this[tier];
            return bonus.IsNone ? skill : skill.WithDifficultyBonus(Clamp(tier), bonus);
        }
    }

    /// <summary>Symbole und Texte zur Schwierigkeit für Anzeige und Tooltips.</summary>
    public static class DifficultyText
    {
        private static readonly string[] Symbols = { "○", "●", "●●", "●●●" };
        private static readonly string[] Names = CatalogTexts.DifficultyNames;

        /// <summary>○ ● ●● ●●●</summary>
        public static string Symbol(int tier) => Symbols[DifficultyBonusConfig.Clamp(tier)];

        /// <summary>Leicht, Mittel, Schwer, Sehr schwer.</summary>
        public static string Name(int tier) => Names[DifficultyBonusConfig.Clamp(tier)];

        /// <summary>Tooltip: «Hard: +30 % power, −20 % Cast Time · powers up to 4 cells».</summary>
        public static string Tooltip(int tier, DifficultyBonusConfig config = null)
        {
            DifficultyBonus bonus = (config ?? DifficultyBonusConfig.Default)[tier];
            return CatalogTexts.DifficultyTooltip(Name(tier), bonus.Text, bonus.MaxCells);
        }
    }
}
