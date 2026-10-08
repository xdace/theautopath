using System.Collections.Generic;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Gear
{
    public static class RuneLoadoutBoard
    {
        /// <summary>
        /// Die Zeilen der Runen-Plätze als Bauplan für die Logik-Tafel, mit Modulen an Skill und Baustein.
        /// Auslöser-Ziele (stabile Ids) werden hier zu Zeilen aufgelöst; ein freies oder fehlendes Ziel ergibt keinen Auslöser.
        /// </summary>
        public static List<BoardRowSpec> ToBoardSpecs(this RuneLoadout loadout)
        {
            var specs = new List<BoardRowSpec>();
            if (loadout == null) return specs;
            foreach (RuneSlot row in loadout.Rows)
            {
                // Die Skill-Stufe wirkt nicht mehr selbst (A-08): ihre Werte kommen über das Wachstum.
                specs.Add(new BoardRowSpec(row.Rune.Id, row.SkillId, row.Level, 0,
                    Specs(loadout, row.Skill?.Modules), Specs(loadout, row.Modules), row.Skill?.Growth ?? 0, row.Growth));
            }
            return specs;
        }

        private static List<ModuleSpec> Specs(RuneLoadout loadout, IReadOnlyList<ModuleInstance> modules)
        {
            var result = new List<ModuleSpec>();
            if (modules == null) return result;
            foreach (ModuleInstance m in modules) result.Add(new ModuleSpec(m.ModuleId, m.Level, TargetRow(loadout, m.Target)));
            return result;
        }

        /// <summary>Zeile eines Auslöser-Ziels: die Zeile mit diesem Skill-Exemplar bzw. dieser Zeilen-Id, sonst -1.</summary>
        public static int TargetRow(RuneLoadout loadout, ModuleTarget? target)
        {
            if (!target.HasValue) return -1;
            for (int i = 0; i < loadout.Rows.Count; i++)
            {
                RuneSlot row = loadout.Rows[i];
                if (target.Value.Kind == ModuleTargetKind.Row && row.RowId == target.Value.Id) return i;
                if (target.Value.Kind == ModuleTargetKind.Skill && row.Skill != null && !row.Skill.IsBasicAttack
                    && row.Skill.InstanceId == target.Value.Id) return i;
            }
            return -1;
        }
    }
}
