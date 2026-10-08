using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Bonus einer Schwierigkeitsstufe auf den Skill einer Zeile.</summary>
    public readonly struct DifficultyBonus
    {
        /// <summary>Cooldown in Prozent kürzer (15 = −15 %).</summary>
        public readonly int CooldownReductionPercent;

        /// <summary>Wirkung (Schaden, Heilung, Schild) in Prozent stärker.</summary>
        public readonly int PowerPercent;

        /// <summary>Cast-Zeit in Prozent kürzer; die Untergrenze 0,1 s bleibt.</summary>
        public readonly int CastReductionPercent;

        /// <summary>Zusätzliche Dauer von Status-Wirkungen (Betäubung, Brennen, Buffs, Debuffs) in Ticks.</summary>
        public readonly int ExtraStatusTicks;

        public DifficultyBonus(int cooldownReductionPercent, int powerPercent = 0, int castReductionPercent = 0, int extraStatusTicks = 0)
        {
            CooldownReductionPercent = Math.Max(0, Math.Min(100, cooldownReductionPercent));
            PowerPercent = Math.Max(0, powerPercent);
            CastReductionPercent = Math.Max(0, Math.Min(100, castReductionPercent));
            ExtraStatusTicks = Math.Max(0, extraStatusTicks);
        }

        public bool IsNone => CooldownReductionPercent == 0 && PowerPercent == 0 && CastReductionPercent == 0 && ExtraStatusTicks == 0;

        /// <summary>«−30 % Cooldown, +25 % Wirkung» oder «kein Bonus».</summary>
        public string Text
        {
            get
            {
                var parts = new List<string>();
                if (CooldownReductionPercent > 0) parts.Add($"−{CooldownReductionPercent} % Cooldown");
                if (PowerPercent > 0) parts.Add($"+{PowerPercent} % Wirkung");
                if (CastReductionPercent > 0) parts.Add($"−{CastReductionPercent} % Cast-Zeit");
                if (ExtraStatusTicks > 0) parts.Add($"+{SkillInfo.Seconds(ExtraStatusTicks)} Dauer von Status-Wirkungen");
                return parts.Count > 0 ? string.Join(", ", parts) : "kein Bonus";
            }
        }
    }

    /// <summary>
    /// Schwierigkeits-Bonus als Daten: je schwerer ein Logikbaustein (Grundschwierigkeit 0–3), desto stärker der Skill
    /// seiner Zeile. Die Stufe hängt nur am Baustein; Ausrüstung, Module, Wachstum, Tags oder Skills, die ihn leichter
    /// erfüllbar machen, ändern sie nicht. Das ist der gewollte Weg zu starken Builds.
    /// </summary>
    public sealed class DifficultyBonusConfig
    {
        public const int MaxTier = 3;

        private readonly DifficultyBonus[] _tiers;

        public DifficultyBonusConfig(params DifficultyBonus[] tiers)
        {
            if (tiers == null || tiers.Length != MaxTier + 1) throw new ArgumentException("Genau vier Stufen (0 bis 3).", nameof(tiers));
            _tiers = (DifficultyBonus[])tiers.Clone();
        }

        public DifficultyBonus this[int tier] => _tiers[Clamp(tier)];

        public static int Clamp(int tier) => Math.Max(0, Math.Min(MaxTier, tier));

        /// <summary>
        /// Startwerte: 1 = −15 % Cooldown · 2 = −30 % Cooldown, +25 % Wirkung ·
        /// 3 = −50 % Cooldown, +50 % Wirkung, −30 % Cast-Zeit, +1 s Dauer von Status-Wirkungen.
        /// </summary>
        public static DifficultyBonusConfig Default { get; } = new DifficultyBonusConfig(
            new DifficultyBonus(0),
            new DifficultyBonus(15),
            new DifficultyBonus(30, 25),
            new DifficultyBonus(50, 50, 30, Ticks.PerSecond));

        /// <summary>Ohne Bonus (z. B. für Vergleiche in Tests).</summary>
        public static DifficultyBonusConfig None { get; } = new DifficultyBonusConfig(
            new DifficultyBonus(0), new DifficultyBonus(0), new DifficultyBonus(0), new DifficultyBonus(0));

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
        private static readonly string[] Symbols = { "◇", "◆", "◆◆", "◆◆◆" };
        private static readonly string[] Names = { "Leicht", "Mittel", "Schwer", "Sehr schwer" };

        /// <summary>◇ ◆ ◆◆ ◆◆◆</summary>
        public static string Symbol(int tier) => Symbols[DifficultyBonusConfig.Clamp(tier)];

        /// <summary>Leicht, Mittel, Schwer, Sehr schwer.</summary>
        public static string Name(int tier) => Names[DifficultyBonusConfig.Clamp(tier)];

        /// <summary>Tooltip: «Schwer: −30 % Cooldown, +25 % Wirkung».</summary>
        public static string Tooltip(int tier, DifficultyBonusConfig config = null)
        {
            DifficultyBonus bonus = (config ?? DifficultyBonusConfig.Default)[tier];
            return $"{Name(tier)}: {bonus.Text}";
        }
    }
}
