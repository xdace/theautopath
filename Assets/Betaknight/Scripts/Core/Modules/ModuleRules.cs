using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Modules
{
    /// <summary>Ein Modul, wie es in den Bauplan einer Zeile geht: Id, Stufe und (bei Auslösern) die aufgelöste Zielzeile.</summary>
    public readonly struct ModuleSpec
    {
        public readonly string ModuleId;
        public readonly int Level;

        /// <summary>Zielzeile eines Auslösers im Kampf, -1 ohne (gültiges) Ziel.</summary>
        public readonly int TargetRow;

        public ModuleSpec(string moduleId, int level = 0, int targetRow = -1)
        {
            ModuleId = moduleId;
            Level = level;
            TargetRow = targetRow;
        }
    }

    /// <summary>Was Module im Kampf bewirken. Reine Regeln, angewendet beim Bau der Tafel.</summary>
    public static class ModuleRules
    {
        public const int MaxLevel = 1;

        public const int AreaPercent = 70;
        public const int AreaPercentPerLevel = 15;
        public const int BloodCostPercent = 5;
        public const int BloodPowerPercent = 40;
        public const int BloodPowerPercentPerLevel = 10;
        public const int QuickcastPercent = -30;
        public const int QuickcastPercentPerLevel = -10;
        public const int QuickcastPowerPercent = -15;
        public static readonly int ExtendTicks = Ticks.PerSecond;
        public static readonly int ExtendTicksPerLevel = Ticks.FromTenths(5);
        public const int ThresholdPercent = 10;
        public const int ThresholdPercentPerLevel = 5;

        /// <summary>Wendet ein Skill-Modul an. Auslöser und unbekannte Module ändern den Skill nicht.</summary>
        public static SkillDefinition ApplyToSkill(SkillDefinition skill, ModuleSpec module, string name)
        {
            if (skill == null || skill.IsBasicAttack) return skill;
            int level = module.Level;
            switch (module.ModuleId)
            {
                case ModuleIds.Multicast:
                    return skill.WithModule(name, extraCasts: 1 + level);
                case ModuleIds.Area:
                    int percent = AreaPercent + AreaPercentPerLevel * level;
                    return skill.WithModule(name, e => e is DamageEffect d && !d.AllEnemies
                        ? new DamageEffect(d.DamageBp * percent / 100, true, d.IgnoreArmor) : e);
                case ModuleIds.Chain:
                    return skill.WithModule(name, extraTargets: 1 + level);
                case ModuleIds.BloodCost:
                    return skill.WithModule(name, hpCostBp: BasisPoints.Percent(System.Math.Max(1, BloodCostPercent - level)))
                        .WithBonus(BloodPowerPercent + BloodPowerPercentPerLevel * level);
                case ModuleIds.Quickcast:
                    return skill.WithModule(name, castPercent: QuickcastPercent + QuickcastPercentPerLevel * level)
                        .WithBonus(QuickcastPowerPercent);
                default:
                    // Eigene Effekte der Platine (A-21): die Komponente hat den Effekt, die Wirkung steht im Kampf.
                    return Circuit.CircuitEffectCatalog.Shared.Contains(module.ModuleId)
                        ? skill.WithModule(name).WithCircuitEffect(module.ModuleId)
                        : skill;
            }
        }

        /// <summary>Wendet ein Baustein-Modul auf die Bedingung an (Umkehren, Verlängern).</summary>
        public static ICondition ApplyToCondition(ICondition condition, ModuleSpec module)
        {
            switch (module.ModuleId)
            {
                case ModuleIds.Invert: return condition.Not();
                case ModuleIds.Extend: return new ExtendedCondition(condition, ExtendTicks + ExtendTicksPerLevel * module.Level);
                default: return condition;
            }
        }

        /// <summary>Hat die Rune eine Prozent-Schwelle («HP unter {0} %»)? Nur dann wirkt «Schwelle».</summary>
        public static bool HasPercentThreshold(RuneDefinition rune) => rune != null && rune.NameTemplate.Contains("{0} %");

        /// <summary>Parameter der Rune mit «Schwelle»-Modulen (+10 Prozentpunkte, +5 je Stufe).</summary>
        public static int ApplyToParameter(RuneDefinition rune, int parameter, IEnumerable<ModuleSpec> modules)
        {
            if (!HasPercentThreshold(rune) || modules == null) return parameter;
            foreach (ModuleSpec m in modules)
                if (m.ModuleId == ModuleIds.Threshold) parameter += ThresholdPercent + ThresholdPercentPerLevel * m.Level;
            return System.Math.Min(100, parameter);
        }
    }
}
