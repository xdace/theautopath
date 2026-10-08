using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Growth
{
    /// <summary>Was eine Komponente (bzw. ein Relais) des Spielers in einem Kampf bewirkt hat, gezählt aus dem Protokoll.</summary>
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

    /// <summary>Zählt pro Komponente Feuern, Kills, Betäubungen und Heilungen. Rechnet nichts neu, liest nur das Protokoll.</summary>
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

        /// <summary>
        /// Dasselbe pro Relais (A-19): gezählt wird, was die Aktionen bewirkt haben, die das Relais ausgelöst hat (die Wirkung
        /// einer Komponente gehört dem Relais, das ihre laufende Aktion gestartet hat).
        /// </summary>
        public static Dictionary<int, RowTally> CountRelays(BattleResult result)
        {
            var relays = new Dictionary<int, RowTally>();
            if (result == null || result.Fighters.Count == 0) return relays;
            Combatant player = result.Fighters[0].Combatant;
            var relayOf = new Dictionary<int, int>();

            RowTally Relay(int component)
            {
                if (!relayOf.TryGetValue(component, out int relay) || relay < 0) return null;
                if (!relays.TryGetValue(relay, out RowTally t)) relays[relay] = t = new RowTally();
                return t;
            }

            foreach (BattleEvent e in result.Events)
            {
                if (e.Source != player || e.RowIndex < 0) continue;
                switch (e.Kind)
                {
                    case BattleEventKind.ActionStarted:
                        relayOf[e.RowIndex] = e.Relay;
                        RowTally fired = Relay(e.RowIndex);
                        if (fired != null) fired.Fired++;
                        break;
                    case BattleEventKind.Death:
                        if (e.Target != null && e.Target.Side != player.Side) { RowTally t = Relay(e.RowIndex); if (t != null) t.Kills++; }
                        break;
                    case BattleEventKind.StatusApplied:
                        if (e.Detail == StatusIds.Stun && e.Target != null && e.Target.Side != player.Side) { RowTally t = Relay(e.RowIndex); if (t != null) t.Stuns++; }
                        break;
                    case BattleEventKind.Healed:
                        if (e.Amount > 0) { RowTally t = Relay(e.RowIndex); if (t != null) t.Heals++; }
                        break;
                }
            }
            return relays;
        }
    }
}
