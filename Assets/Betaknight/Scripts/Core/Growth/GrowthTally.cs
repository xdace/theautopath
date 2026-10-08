using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Growth
{
    /// <summary>Was eine Tafel-Zeile des Spielers in einem Kampf bewirkt hat, gezählt aus dem Protokoll.</summary>
    public sealed class RowTally
    {
        public int Fired;
        public int Kills;
        public int Stuns;
        public int Heals;

        /// <summary>Punkte für eine Regel (Sieg zählt einmal, wenn die Zeile gefeuert hat).</summary>
        public int PointsFor(GrowthTrigger trigger, bool victory)
        {
            switch (trigger)
            {
                case GrowthTrigger.Kill: return Kills;
                case GrowthTrigger.Stun: return Stuns;
                case GrowthTrigger.Heal: return Heals;
                case GrowthTrigger.Win: return victory && Fired > 0 ? 1 : 0;
                default: return 0;
            }
        }
    }

    /// <summary>Zählt pro Zeile Feuern, Kills, Betäubungen und Heilungen. Rechnet nichts neu, liest nur das Protokoll.</summary>
    public static class GrowthTally
    {
        public static Dictionary<int, RowTally> Count(BattleResult result)
        {
            var rows = new Dictionary<int, RowTally>();
            if (result == null || result.Fighters.Count == 0) return rows;
            Combatant player = result.Fighters[0].Combatant;

            RowTally Row(int index)
            {
                if (!rows.TryGetValue(index, out RowTally t)) rows[index] = t = new RowTally();
                return t;
            }

            foreach (BattleEvent e in result.Events)
            {
                if (e.Source != player || e.RowIndex < 0) continue;
                switch (e.Kind)
                {
                    case BattleEventKind.ActionStarted: Row(e.RowIndex).Fired++; break;
                    case BattleEventKind.Death:
                        if (e.Target != null && e.Target.Side != player.Side) Row(e.RowIndex).Kills++;
                        break;
                    case BattleEventKind.StatusApplied:
                        if (e.Detail == StatusIds.Stun && e.Target != null && e.Target.Side != player.Side) Row(e.RowIndex).Stuns++;
                        break;
                    case BattleEventKind.Healed:
                        if (e.Amount > 0) Row(e.RowIndex).Heals++;
                        break;
                }
            }
            return rows;
        }
    }
}
