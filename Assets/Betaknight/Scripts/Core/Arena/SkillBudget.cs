using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Skill-Budget als Daten (A-12): Skills sind der Hauptschaden, der Basisangriff ist Füller und Motor.
    /// Ein Schadens-Skill macht pro Sekunde Aktionszeit (Cast + Erholung) ein Vielfaches des Basisangriffs pro
    /// Sekunde; Flächen-Skills pro Ziel etwas weniger. Längerer Cooldown verlangt mehr Wirkung.
    /// </summary>
    public sealed class SkillBudgetConfig
    {
        /// <summary>Schaden des Basisangriffs des Ritters in Basispunkten des Waffenschadens (6000 = 60 %).</summary>
        public int BasicAttackDamageBp { get; set; } = BasisPoints.Percent(60);

        /// <summary>Jeder Treffer des Basisangriffs verkürzt alle laufenden Skill-Cooldowns um so viele Ticks.</summary>
        public int BasicAttackCooldownCutTicks { get; set; } = Ticks.PerSecond / 4;

        /// <summary>Angriffsintervall, auf das sich «Basisangriff pro Sekunde» bezieht (1 s wie beim Start-Ritter).</summary>
        public int ReferenceIntervalTicks { get; set; } = Ticks.PerSecond;

        /// <summary>Einzelziel: Schaden pro Sekunde Aktionszeit mindestens so viel Prozent des Basisangriffs pro Sekunde.</summary>
        public int SingleTargetFactorPercent { get; set; } = 250;

        /// <summary>Fläche: pro Ziel mindestens so viel Prozent des Basisangriffs pro Sekunde.</summary>
        public int AreaFactorPercent { get; set; } = 150;

        /// <summary>Bis zu diesem Cooldown gilt der Grundfaktor.</summary>
        public int ReferenceCooldownTicks { get; set; } = Ticks.FromSeconds(3);

        /// <summary>Jede Sekunde Cooldown darüber verlangt so viel Prozent mehr Wirkung.</summary>
        public int CooldownPercentPerSecond { get; set; } = 5;

        /// <summary>Nutzen-Skills (Betäubung, Schild, Blendung): höchstens so viel Prozent des Einzelziel-Budgets als Schaden.</summary>
        public int UtilityMaxShareOfBudgetPercent { get; set; } = 60;

        public static SkillBudgetConfig Default { get; } = new SkillBudgetConfig();

        /// <summary>Basisangriff pro Sekunde in Basispunkten des Waffenschadens.</summary>
        public int BasicDamagePerSecondBp => (int)((long)BasicAttackDamageBp * Ticks.PerSecond / Math.Max(1, ReferenceIntervalTicks));

        /// <summary>Der Basisangriff des Ritters nach dieser Konfiguration.</summary>
        public SkillDefinition CreateKnightBasicAttack() => SkillDefinition.CreateBasicAttack(BasicAttackDamageBp, BasicAttackCooldownCutTicks);

        /// <summary>Schadens-Skill: Art Angriff oder Feuer und mit Schaden. Alles andere mit Schaden ist ein Nutzen-Skill.</summary>
        public static bool IsDamageSkill(SkillDefinition skill) =>
            skill != null && !skill.IsBasicAttack && (skill.Kinds & (SkillKind.Attack | SkillKind.Fire)) != 0 && DamagePerTargetBp(skill) > 0;

        /// <summary>Alle Schadens-Wirkungen treffen alle Gegner.</summary>
        public static bool IsArea(SkillDefinition skill)
        {
            List<EffectInfo> damage = Describe(skill).Where(e => e.IsDamage).ToList();
            return damage.Count > 0 && damage.All(e => e.AllEnemies);
        }

        /// <summary>Erwarteter Schaden einer Ausführung pro Ziel in Basispunkten des Waffenschadens (Chancen eingerechnet, Brennen über die ganze Dauer).</summary>
        public static int DamagePerTargetBp(SkillDefinition skill)
        {
            long total = 0;
            foreach (EffectInfo e in Describe(skill))
                if (e.IsDamage) total += (long)e.Total * e.ChanceBp / BasisPoints.Full;
            return (int)total;
        }

        /// <summary>Aktionszeit: Cast plus Erholung, in Ticks.</summary>
        public static int ActionTicks(SkillDefinition skill) => skill.CastTicks() + skill.RecoveryTicks;

        /// <summary>Mindestschaden pro Ausführung und Ziel nach dem Budget.</summary>
        public int RequiredDamageBp(SkillDefinition skill)
        {
            int factor = IsArea(skill) ? AreaFactorPercent : SingleTargetFactorPercent;
            int extraSeconds = Math.Max(0, skill.CooldownTicks - ReferenceCooldownTicks) / Ticks.PerSecond;
            long perSecond = (long)BasicDamagePerSecondBp * factor / 100 * (100 + CooldownPercentPerSecond * extraSeconds) / 100;
            return (int)(perSecond * ActionTicks(skill) / Ticks.PerSecond);
        }

        /// <summary>«Bohrstoß: 180 % pro Ziel, Budget 178 % (Fläche, 1,8 s Aktion, 5 s CD)».</summary>
        public string Explain(SkillDefinition skill) =>
            ArenaTexts.Budget(skill.Name, SkillInfo.Percent(DamagePerTargetBp(skill)), IsArea(skill), SkillInfo.Percent(RequiredDamageBp(skill)),
                SkillInfo.Seconds(ActionTicks(skill)), SkillInfo.Seconds(skill.CooldownTicks));

        /// <summary>Weapon 10000: Beträge entsprechen direkt Basispunkten des Waffenschadens.</summary>
        private static IReadOnlyList<EffectInfo> Describe(SkillDefinition skill) =>
            SkillInfo.Create(skill, new SkillUserStats(BasisPoints.Full)).Effects;
    }
}
