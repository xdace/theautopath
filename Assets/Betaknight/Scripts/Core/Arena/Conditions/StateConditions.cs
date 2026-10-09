namespace Betaknight.Core.Arena
{
    /// <summary>[HP unter N %] – eigenes Leben unter einem Schwellwert (Basispunkte).</summary>
    public sealed class HpBelowCondition : ICondition
    {
        public int ThresholdBp { get; }
        public HpBelowCondition(int thresholdBp) => ThresholdBp = thresholdBp;

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            // Erleichterung: HP-Schwellen gelten früher.
            int threshold = ThresholdBp + BasisPoints.Percent(c.Self.Relief(ReliefIds.HpThresholdUp));
            return c.Self.HpPercentBp < threshold;
        }
    }

    /// <summary>[HP voll]</summary>
    public sealed class HpFullCondition : ICondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Self.Hp >= c.Self.MaxHp;
        }
    }

    /// <summary>
    /// Basis für Bedingungen, die einen Gegner prüfen. Der erste passende Gegner wird zum Ziel des Skills.
    /// </summary>
    public abstract class OpponentCondition : ICondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            foreach (Combatant enemy in c.Battle.OpponentsOf(c.Self))
            {
                if (Matches(c, enemy))
                {
                    target = enemy;
                    return true;
                }
            }
            target = null;
            return false;
        }

        protected abstract bool Matches(in ConditionContext c, Combatant enemy);
    }

    /// <summary>[Gegner geschwächt] – ein Gegner unter N % Leben.</summary>
    public sealed class EnemyHpBelowCondition : OpponentCondition
    {
        public int ThresholdBp { get; }
        public EnemyHpBelowCondition(int thresholdBp) => ThresholdBp = thresholdBp;
        protected override bool Matches(in ConditionContext c, Combatant enemy) =>
            enemy.HpPercentBp < ThresholdBp + BasisPoints.Percent(c.Self.Relief(ReliefIds.EnemyLowUp));
    }

    /// <summary>[Gegner gepanzert]</summary>
    public sealed class EnemyArmoredCondition : OpponentCondition
    {
        protected override bool Matches(in ConditionContext c, Combatant enemy) => enemy.EffectiveArmor > 0;
    }

    /// <summary>[Gegner betäubt]</summary>
    public sealed class EnemyStunnedCondition : OpponentCondition
    {
        protected override bool Matches(in ConditionContext c, Combatant enemy)
        {
            if (enemy.IsStunned) return true;
            // Erleichterung: kurz nach einer Betäubung gilt der Gegner noch als betäubt.
            int afterglow = c.Self.Relief(ReliefIds.StunAfterglow);
            if (afterglow <= 0) return false;
            return c.Battle.AnyEvent(c.Tick - afterglow, c.Tick - 1,
                e => e.Kind == BattleEventKind.StatusExpired && e.Target == enemy && e.Detail == StatusIds.Stun);
        }
    }

    /// <summary>[Gegner lädt auf] – ein Gegner holt eine sichtbare Aufladung aus. Ziel ist dieser Gegner.</summary>
    public sealed class EnemyChargingCondition : OpponentCondition
    {
        protected override bool Matches(in ConditionContext c, Combatant enemy) => enemy.IsCharging;
    }

    /// <summary>[Gegner hat mehr HP als ich] – absolut, nicht prozentual.</summary>
    public sealed class EnemyStrongerCondition : OpponentCondition
    {
        protected override bool Matches(in ConditionContext c, Combatant enemy) => enemy.Hp > c.Self.Hp;
    }

    /// <summary>[Gegner hat Zustand X], z. B. [Gegner brennt].</summary>
    public sealed class EnemyHasStatusCondition : OpponentCondition
    {
        public string StatusId { get; }
        public EnemyHasStatusCondition(string statusId) => StatusId = statusId;
        protected override bool Matches(in ConditionContext c, Combatant enemy) =>
            enemy.HasStatus(StatusId)
            || (StatusId == StatusIds.Burn && c.Self.HasRelief(ReliefIds.BurnCountsPoison) && enemy.HasStatus(StatusIds.Poison));
    }

    /// <summary>[In Unterzahl] – mindestens N lebende Gegner.</summary>
    public sealed class OutnumberedCondition : ICondition
    {
        public int MinEnemies { get; }
        public OutnumberedCondition(int minEnemies) => MinEnemies = minEnemies;

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Battle.OpponentsOf(c.Self).Count >= MinEnemies;
        }
    }

    /// <summary>[Letzter Gegner]</summary>
    public sealed class LastEnemyCondition : ICondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            var opponents = c.Battle.OpponentsOf(c.Self);
            target = opponents.Count == 1 ? opponents[0] : null;
            return opponents.Count == 1;
        }
    }

    /// <summary>[Überhitzung] – das Zeitlimit ist überschritten.</summary>
    public sealed class OverheatCondition : ICondition
    {
        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Battle.IsOverheated;
        }
    }

    /// <summary>Kampfzeit: wahr in den ersten N Sekunden («Opening») bzw. ab N Sekunden («Overtime»).</summary>
    public sealed class FightTimeCondition : ICondition
    {
        public int Ticks { get; }
        public bool From { get; }

        public FightTimeCondition(int ticks, bool from)
        {
            Ticks = System.Math.Max(0, ticks);
            From = from;
        }

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return From ? c.Tick >= Ticks : c.Tick <= Ticks;
        }
    }

    /// <summary>Warteschlange: wahr, solange mindestens N eigene Komponenten warten («Backlog»).</summary>
    public sealed class QueueAtLeastCondition : ICondition
    {
        public int Count { get; }
        public QueueAtLeastCondition(int count) => Count = System.Math.Max(1, count);

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return c.Self.Queue.Count >= Count;
        }
    }

    /// <summary>Kontext-Bedingungen: für den ganzen Kampf fest (Goldmine, Boss).</summary>
    public sealed class ContextCondition : ICondition
    {
        public enum Kind { OnGoldMine, VsBoss }

        public Kind Which { get; }
        public ContextCondition(Kind which) => Which = which;

        public bool IsMet(in ConditionContext c, out Combatant target)
        {
            target = null;
            return Which == Kind.OnGoldMine ? c.Battle.Context.OnGoldMine : c.Battle.Context.VsBoss;
        }
    }
}
