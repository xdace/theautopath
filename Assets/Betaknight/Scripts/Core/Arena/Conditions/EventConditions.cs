using System;

namespace Betaknight.Core.Arena
{
    /// <summary>Bekannte Ressourcen-Ids (Zähler auf einem Kämpfer).</summary>
    public static class ResourceIds
    {
        public const string Tempo = "tempo";
        public const string Charge = "charge";
        public const string Heat = "heat";
    }

    /// <summary>
    /// Ereignis-Bedingung: wahr, wenn das Ereignis in den letzten <see cref="Window"/> Ticks passiert ist
    /// und die Zeile seitdem nicht gefeuert hat (dann ist es verbraucht). Ereignisse werden erst
    /// ab dem Tick nach ihrem Entstehen sichtbar, dadurch gibt es keine Kettenreaktion im selben Tick.
    /// </summary>
    public sealed class EventCondition : IEventCondition
    {
        public const int DefaultWindow = 10;

        private readonly Func<ConditionContext, BattleEvent, bool> _match;
        private readonly Func<ConditionContext, BattleEvent, Combatant> _target;

        public int Window { get; }

        public EventCondition(Func<ConditionContext, BattleEvent, bool> match,
            Func<ConditionContext, BattleEvent, Combatant> target = null, int window = DefaultWindow)
        {
            _match = match ?? throw new ArgumentNullException(nameof(match));
            _target = target;
            Window = window;
        }

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            int from = Math.Max(c.Row.LastFiredTick, c.Tick - Window);
            int to = c.Tick - 1;
            if (to < from) return false;

            ConditionContext ctx = c;
            BattleEvent found = null;
            c.Battle.AnyEvent(from, to, e =>
            {
                if (!_match(ctx, e)) return false;
                found = e;
                return true;
            });
            if (found == null) return false;

            Combatant preferred = _target?.Invoke(ctx, found);
            if (preferred != null && preferred.IsAlive && preferred.Side != c.Self.Side) target = preferred;
            return true;
        }
    }

    /// <summary>Clock: löst alle N Ticks aus, gerechnet ab Kampfbeginn bzw. ab dem letzten Auslösen. Ersetzt «Always».</summary>
    public sealed class ClockCondition : IEventCondition
    {
        public int IntervalTicks { get; }
        public ClockCondition(int intervalTicks) => IntervalTicks = Math.Max(1, intervalTicks);

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Tick - c.Row.LastFiredTick >= IntervalTicks;
        }
    }

    /// <summary>Zähler: löst nach N passenden Ereignissen seit dem letzten Auslösen aus.</summary>
    public sealed class CounterCondition : IEventCondition
    {
        private readonly Func<ConditionContext, BattleEvent, bool> _match;
        public int Count { get; }

        public CounterCondition(int count, Func<ConditionContext, BattleEvent, bool> match)
        {
            Count = Math.Max(1, count);
            _match = match ?? throw new ArgumentNullException(nameof(match));
        }

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            ConditionContext ctx = c;
            return c.Battle.CountEvents(c.Row.LastFiredTick, c.Tick - 1, e => _match(ctx, e)) >= Count;
        }
    }

    /// <summary>[N Ausweicher in Folge] – zählt Ausweicher ohne erlittenen Treffer dazwischen.</summary>
    public sealed class DodgeStreakCondition : IEventCondition
    {
        public int Count { get; }
        public DodgeStreakCondition(int count) => Count = Math.Max(1, count);

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            int streak = 0;
            int hits = 0;
            Combatant self = c.Self;
            // Erleichterung: die Serie übersteht so viele Treffer.
            int tolerance = self.Relief(ReliefIds.DodgeTolerance);
            c.Battle.AnyEvent(c.Row.LastFiredTick, c.Tick - 1, e =>
            {
                if (e.Target != self) return false;
                if (e.Kind == BattleEventKind.Hit && ++hits > tolerance) return true; // Serie gebrochen, ältere Ereignisse zählen nicht
                if (e.Kind == BattleEventKind.Dodged) streak++;
                return false;
            });
            return streak >= Count;
        }
    }

    /// <summary>Ressource: wahr ab N, z. B. Tempo-Stapel oder Ladung.</summary>
    public sealed class ResourceAtLeastCondition : ICondition
    {
        public string ResourceId { get; }
        public int Amount { get; }

        public ResourceAtLeastCondition(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Self.GetResource(ResourceId) >= Amount;
        }
    }

    /// <summary>[Kampfbeginn] – genau einmal pro Kampf.</summary>
    public sealed class BattleStartCondition : IEventCondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Row.FireCount == 0;
        }
    }

    /// <summary>
    /// [Chain] – direkt nachdem eine andere Komponente (nicht eine von diesem Relais versorgte, nicht der Basisangriff)
    /// fertig ist. Baut Combos aus mehreren Komponenten mit einem einzigen Auslöser.
    /// </summary>
    public sealed class ChainCondition : IEventCondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            int last = c.Self.LastActionRow;
            if (last < 0 || last >= c.Self.Board.Rows.Count) return false;
            foreach (int own in c.Row.Powered)
                if (own == last) return false;
            if (c.Tick - c.Self.LastActionEndTick > EventCondition.DefaultWindow) return false;
            // Streng grösser: das Relais feuert im selben Tick, in dem die andere Komponente fertig wird, und nicht noch einmal danach.
            return c.Self.LastActionEndTick > c.Row.LastFiredTick;
        }
    }
}
