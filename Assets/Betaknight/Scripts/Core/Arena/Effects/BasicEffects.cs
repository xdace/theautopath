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

    /// <summary>Setzt das Ziel in Brand: Schaden pro Sekunde in Prozent des eigenen Waffenschadens.</summary>
    public sealed class BurnEffect : ISkillEffect
    {
        public int Ticks { get; }
        public int DamageBpPerSecond { get; }

        public BurnEffect(int ticks, int damageBpPerSecond)
        {
            Ticks = ticks;
            DamageBpPerSecond = damageBpPerSecond;
        }

        public void Apply(in SkillContext c)
        {
            if (c.Target == null) return;
            int dps = System.Math.Max(1, BasisPoints.Of(c.User.GetStat(StatKind.Damage), DamageBpPerSecond));
            c.Battle.ApplyStatus(c.Target, new BurnStatus(Ticks, dps), c.User);
        }
    }

    /// <summary>Legt einen beliebigen Zustand auf Anwender oder Ziel. Die Fabrik erzeugt pro Anwendung einen neuen.</summary>
    public sealed class ApplyStatusEffect : ISkillEffect
    {
        private readonly System.Func<StatusEffect> _factory;
        public bool OnTarget { get; }

        public ApplyStatusEffect(System.Func<StatusEffect> factory, bool onTarget = false)
        {
            _factory = factory ?? throw new System.ArgumentNullException(nameof(factory));
            OnTarget = onTarget;
        }

        public void Apply(in SkillContext c)
        {
            Combatant who = OnTarget ? c.Target : c.User;
            if (who != null) c.Battle.ApplyStatus(who, _factory(), c.User);
        }
    }

    /// <summary>Wendet eine Wirkung nur mit einer Chance an (z. B. 20 % Betäubung).</summary>
    public sealed class ChanceEffect : ISkillEffect
    {
        public int ChanceBp { get; }
        public ISkillEffect Inner { get; }

        public ChanceEffect(int chanceBp, ISkillEffect inner)
        {
            ChanceBp = chanceBp;
            Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        public void Apply(in SkillContext c)
        {
            if (ChanceBp <= 0) return;
            if (ChanceBp >= BasisPoints.Full || c.Battle.Random.Next(BasisPoints.Full) < ChanceBp) Inner.Apply(c);
        }
    }

    /// <summary>Setzt eine eigene Ressource auf einen festen Wert, z. B. Hitze auf 0.</summary>
    public sealed class SetResourceEffect : ISkillEffect
    {
        public string ResourceId { get; }
        public int Value { get; }

        public SetResourceEffect(string resourceId, int value)
        {
            ResourceId = resourceId;
            Value = value;
        }

        public void Apply(in SkillContext c) =>
            c.Battle.ChangeResource(c.User, ResourceId, Value - c.User.GetResource(ResourceId), int.MaxValue, int.MinValue);
    }

    /// <summary>
    /// Echo: wiederholt die Wirkungen des zuletzt ausgeführten eigenen Skills, ohne dessen Cooldown zu setzen.
    /// Skills, die sich nicht wiederholen lassen (Echo selbst), werden nie als "zuletzt" gemerkt.
    /// </summary>
    public sealed class RepeatLastSkillEffect : ISkillEffect
    {
        public void Apply(in SkillContext c)
        {
            SkillDefinition last = c.User.LastRepeatableSkill;
            if (last == null || !last.CanBeRepeated || last == c.Skill) return;

            var repeat = new SkillContext(c.Battle, c.User, c.Target, last, c.RowIndex);
            foreach (ISkillEffect effect in last.Effects)
            {
                if (!c.User.IsAlive) break;
                effect.Apply(repeat);
            }
        }
    }
}
