using System.Collections.Generic;
using Betaknight.Core.Circuit;
using Betaknight.Core.Modules;

namespace Betaknight.Core.Gear
{
    public static class CircuitBoardSpec
    {
        /// <summary>
        /// Die Platine des Spielers als Bauplan für den Kampf, mit Modulen an Skills und Relais. Auslöser-Ziele (stabile Ids)
        /// werden hier zu Komponenten-Indizes aufgelöst; ein fehlendes Ziel ergibt keinen Auslöser.
        /// </summary>
        public static CircuitSpec ToSpec(this CircuitBoard board)
        {
            var spec = new CircuitSpec();
            if (board == null) return spec;
            spec.Width = board.Width;
            spec.Height = board.Height;
            spec.Core = board.Core;
            spec.CoreBonusPercent = board.Config.CoreBonusPercent;
            foreach (RelayChip r in board.Relays)
                spec.Relays.Add(new RelaySpec(r.Rune.Id, r.Position, r.Level, Specs(board, r.Modules), r.Growth));
            foreach (ComponentSlot c in board.Components)
                spec.Components.Add(new ComponentSpec(c.Skill?.SkillId, c.Origin, c.Rotated, Specs(board, c.Skill?.Modules), c.Skill?.Growth ?? 0));
            return spec;
        }

        private static List<ModuleSpec> Specs(CircuitBoard board, IReadOnlyList<ModuleInstance> modules)
        {
            var result = new List<ModuleSpec>();
            if (modules == null) return result;
            foreach (ModuleInstance m in modules) result.Add(new ModuleSpec(m.ModuleId, m.Level, TargetComponent(board, m.Target)));
            return result;
        }

        /// <summary>Komponente eines Auslöser-Ziels (Index in Lesereihenfolge): per Komponenten-Id oder Skill-Exemplar, sonst -1.</summary>
        public static int TargetComponent(CircuitBoard board, ModuleTarget? target)
        {
            if (!target.HasValue || board == null) return -1;
            for (int i = 0; i < board.Components.Count; i++)
            {
                ComponentSlot c = board.Components[i];
                if (target.Value.Kind == ModuleTargetKind.Row && c.SlotId == target.Value.Id) return i;
                if (target.Value.Kind == ModuleTargetKind.Skill && c.Skill != null && c.Skill.InstanceId == target.Value.Id) return i;
            }
            return -1;
        }
    }
}
