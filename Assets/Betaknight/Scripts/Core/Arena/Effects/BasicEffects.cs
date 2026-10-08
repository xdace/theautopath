namespace Betaknight.Core.Arena
{
    /// <summary>Heilt den Anwender um einen Anteil seines Max-HP.</summary>
    public sealed class HealEffect : ISkillEffect
    {
        public int MaxHpBp { get; }
        public HealEffect(int maxHpBp) => MaxHpBp = maxHpBp;

        public void Apply(in SkillContext c) =>
            c.Battle.Heal(c.User, System.Math.Max(1, BasisPoints.Of(c.User.MaxHp, MaxHpBp)), c.User, c.Skill.Id);
    }

    /// <summary>Betäubt das Ziel (oder alle Gegner). Bricht dessen laufende Aktion ab.</summary>
    public sealed class StunEffect : ISkillEffect
    {
        public int Ticks { get; }
        public bool AllEnemies { get; }

        public StunEffect(int ticks, bool allEnemies = false)
        {
            Ticks = ticks;
            AllEnemies = allEnemies;
        }

        public void Apply(in SkillContext c)
        {
            if (AllEnemies)
            {
                foreach (Combatant e in c.Battle.OpponentsOf(c.User)) c.Battle.ApplyStatus(e, new StunStatus(Ticks), c.User);
            }
            else if (c.Target != null)
            {
                c.Battle.ApplyStatus(c.Target, new StunStatus(Ticks), c.User);
            }
        }
    }

    /// <summary>Bricht eine laufende Aufladung des Ziels ab, ohne es zu betäuben.</summary>
    public sealed class InterruptChargeEffect : ISkillEffect
    {
        public void Apply(in SkillContext c)
        {
            if (c.Target != null && c.Target.IsCharging) c.Battle.Interrupt(c.Target);
        }
    }

    /// <summary>Zeitlicher Wertebonus auf den Anwender oder Malus auf das Ziel.</summary>
    public sealed class StatModifierEffect : ISkillEffect
    {
        public string StatusId { get; }
        public StatKind Stat { get; }
        public int Amount { get; }
        public int Ticks { get; }
        public bool OnTarget { get; }

        public StatModifierEffect(string statusId, StatKind stat, int amount, int ticks, bool onTarget = false)
        {
            StatusId = statusId;
            Stat = stat;
            Amount = amount;
            Ticks = ticks;
            OnTarget = onTarget;
        }

        public void Apply(in SkillContext c)
        {
            Combatant who = OnTarget ? c.Target : c.User;
            if (who != null) c.Battle.ApplyStatus(who, new StatModifierStatus(StatusId, Stat, Amount, Ticks), c.User);
        }
    }
}
