using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Skill-Budget als Daten (A-12, A-19): Ohne Cooldowns bestimmt die Grösse einer Komponente ihre Wirkung. Eine
    /// Ausführung macht so viel Prozent Waffenschaden, wie die Tabelle für ihre Zellen angibt (1 Zelle ≈ 60 %,
    /// 2 ≈ 150 %, 4 ≈ 350 %, 6 ≈ 600 %); grosse Komponenten brauchen schwere Relais, die seltener auslösen.
    /// Flächen-Skills machen pro Ziel einen Teil davon, Nutzen-Skills einen Teil als Schaden und den Rest als Wirkung.
    /// </summary>
    public sealed class SkillBudgetConfig
    {
        /// <summary>Schaden des Basisangriffs des Ritters in Basispunkten des Waffenschadens (6000 = 60 %).</summary>
        public int BasicAttackDamageBp { get; set; } = BasisPoints.Percent(60);

        /// <summary>Wirkung einer Ausführung nach Zellen, in Prozent Waffenschaden. Fehlende Grössen werden linear ergänzt.</summary>
        public IReadOnlyDictionary<int, int> PowerPercentByCells { get; set; } = new Dictionary<int, int>
        {
            { 1, 60 },
            { 2, 150 },
            { 4, 350 },
            { 6, 600 },
        };

        /// <summary>Fläche: pro Ziel so viel Prozent des Budgets.</summary>
        public int AreaSharePercent { get; set; } = 60;

        /// <summary>Nutzen-Skills (Betäubung, Schild, Blendung): höchstens so viel Prozent des Budgets als Schaden.</summary>
        public int UtilityMaxShareOfBudgetPercent { get; set; } = 60;

        /// <summary>Spielraum der Prüfung: Schadens-Skills liegen zwischen so viel Prozent des Budgets …</summary>
        public int MinOfBudgetPercent { get; set; } = 80;

        /// <summary>… und so viel Prozent.</summary>
        public int MaxOfBudgetPercent { get; set; } = 130;

        public static SkillBudgetConfig Default { get; } = new SkillBudgetConfig();

        /// <summary>Der Basisangriff des Ritters nach dieser Konfiguration.</summary>
        public SkillDefinition CreateKnightBasicAttack() => SkillDefinition.CreateBasicAttack(BasicAttackDamageBp);

        /// <summary>Wirkung einer Ausführung für <paramref name="cells"/> Zellen in Prozent Waffenschaden.</summary>
        public int PowerPercent(int cells)
        {
            cells = Math.Max(1, cells);
            if (PowerPercentByCells.TryGetValue(cells, out int exact)) return exact;
            List<int> keys = PowerPercentByCells.Keys.OrderBy(k => k).ToList();
            int below = keys.LastOrDefault(k => k < cells);
            int above = keys.FirstOrDefault(k => k > cells);
            if (below == 0) return PowerPercentByCells[keys[0]] * cells / keys[0];
            if (above == 0)
            {
                // Über der Tabelle: so viel mehr pro Zelle wie zwischen den letzten beiden Einträgen.
                int prev = keys.Count > 1 ? keys[keys.Count - 2] : 0;
                int step = prev == 0 ? PowerPercentByCells[below] / below : (PowerPercentByCells[below] - PowerPercentByCells[prev]) / (below - prev);
                return PowerPercentByCells[below] + step * (cells - below);
            }
            int lo = PowerPercentByCells[below], hi = PowerPercentByCells[above];
            return lo + (hi - lo) * (cells - below) / (above - below);
        }

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

        /// <summary>Budget einer Ausführung pro Ziel in Basispunkten des Waffenschadens, nach Zellen und Fläche.</summary>
        public int RequiredDamageBp(SkillDefinition skill)
        {
            int bp = BasisPoints.Percent(PowerPercent(skill.Shape.Cells));
            return IsArea(skill) ? bp * AreaSharePercent / 100 : bp;
        }

        /// <summary>Liegt ein Schadens-Skill im Spielraum seines Budgets?</summary>
        public bool IsWithinBudget(SkillDefinition skill)
        {
            int actual = DamagePerTargetBp(skill), required = RequiredDamageBp(skill);
            return actual * 100 >= required * MinOfBudgetPercent && actual * 100 <= required * MaxOfBudgetPercent;
        }

        /// <summary>«Drill Strike: 210 % per target, Budget 210 % (area, 2×2 = 4 cells)».</summary>
        public string Explain(SkillDefinition skill) =>
            ArenaTexts.Budget(skill.Name, SkillInfo.Percent(DamagePerTargetBp(skill)), IsArea(skill), SkillInfo.Percent(RequiredDamageBp(skill)),
                skill.Shape.ToString(), skill.Shape.Cells);

        /// <summary>Weapon 10000: Beträge entsprechen direkt Basispunkten des Waffenschadens.</summary>
        private static IReadOnlyList<EffectInfo> Describe(SkillDefinition skill) =>
            SkillInfo.Create(skill, new SkillUserStats(BasisPoints.Full)).Effects;
    }
}
