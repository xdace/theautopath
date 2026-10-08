using System.Linq;

namespace Betaknight.Core.Arena
{
    /// <summary>Heilt den Anwender um einen Anteil seines Max-HP.</summary>
    public sealed class HealEffect : ISkillEffect, ILevelableEffect, IBoostableEffect
    {
        public int MaxHpBp { get; }
        public HealEffect(int maxHpBp) => MaxHpBp = maxHpBp;

        public ISkillEffect AtLevel(int level, SkillLevelRules rules) => level <= 0 || rules == null ? this
            : new HealEffect(MaxHpBp + level * rules.HealBpPerLevel);

        public ISkillEffect Boosted(int percent) => new HealEffect(MaxHpBp * (100 + percent) / 100);

        public void Apply(in SkillContext c) =>
            c.Battle.Heal(c.User, System.Math.Max(1, BasisPoints.Of(c.User.MaxHp, MaxHpBp)), c.User, c.Skill.Id);

        public void Describe(SkillInfoBuilder info)
        {
            int amount = System.Math.Max(1, BasisPoints.Of(info.Stats.MaxHp, MaxHpBp));
            info.Add(new EffectInfo(EffectInfoKind.Heal, $"heilt {SkillInfo.Percent(MaxHpBp)} Max-HP ≈ {amount}", amount: amount));
        }
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

        public void Describe(SkillInfoBuilder info) => info.Add(new EffectInfo(EffectInfoKind.Stun,
            $"betäubt {(AllEnemies ? "alle " : string.Empty)}{SkillInfo.Seconds(Ticks)}", durationTicks: Ticks, allEnemies: AllEnemies));
    }

    /// <summary>Bricht eine laufende Aufladung des Ziels ab, ohne es zu betäuben.</summary>
    public sealed class InterruptChargeEffect : ISkillEffect
    {
        public void Apply(in SkillContext c)
        {
            if (c.Target != null && c.Target.IsCharging) c.Battle.Interrupt(c.Target);
        }

        public void Describe(SkillInfoBuilder info) => info.Add(new EffectInfo(EffectInfoKind.Interrupt, "bricht Aufladung ab"));
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

        public void Describe(SkillInfoBuilder info) => info.Add(new EffectInfo(EffectInfoKind.StatChange,
            $"{(OnTarget ? "Gegner " : string.Empty)}{SkillInfo.StatChange(Stat, Amount)} für {SkillInfo.Seconds(Ticks)}", durationTicks: Ticks));
    }

    /// <summary>Setzt das Ziel in Brand: Schaden pro Sekunde in Prozent des eigenen Waffenschadens.</summary>
    public sealed class BurnEffect : ISkillEffect, ILevelableEffect, IBoostableEffect
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
            int dps = DamagePerSecondFor(c.User.GetStat(StatKind.Damage));
            c.Battle.ApplyStatus(c.Target, new BurnStatus(Ticks, dps), c.User);
        }

        public ISkillEffect AtLevel(int level, SkillLevelRules rules) => level <= 0 || rules == null ? this
            : new BurnEffect(Ticks, DamageBpPerSecond + level * rules.DamageOverTimeBpPerLevel);

        public ISkillEffect Boosted(int percent) => new BurnEffect(Ticks, DamageBpPerSecond * (100 + percent) / 100);

        /// <summary>Schaden pro Sekunde aus dem Waffenschaden, mindestens 1.</summary>
        public int DamagePerSecondFor(int weaponDamage) => System.Math.Max(1, BasisPoints.Of(weaponDamage, DamageBpPerSecond));

        public void Describe(SkillInfoBuilder info)
        {
            int dps = DamagePerSecondFor(info.Stats.WeaponDamage);
            int total = dps * BurnStatus.TicksOfDamage(Ticks);
            info.Add(new EffectInfo(EffectInfoKind.DamageOverTime,
                $"Brennen {SkillInfo.Percent(DamageBpPerSecond)} Waffenschaden/s ≈ {dps}/s, {total} über {SkillInfo.Seconds(Ticks)}",
                DamageBpPerSecond, dps, total, Ticks));
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

        public void Describe(SkillInfoBuilder info)
        {
            StatusEffect sample = _factory();
            info.Add(new EffectInfo(EffectInfoKind.Status,
                $"{(OnTarget ? "Gegner: " : string.Empty)}{sample.Summary} ({SkillInfo.Seconds(sample.TicksLeft)})", durationTicks: sample.TicksLeft));
        }
    }

    /// <summary>Wendet eine Wirkung nur mit einer Chance an (z. B. 20 % Betäubung).</summary>
    public sealed class ChanceEffect : ISkillEffect, ILevelableEffect, IBoostableEffect
    {
        public int ChanceBp { get; }
        public ISkillEffect Inner { get; }

        public ChanceEffect(int chanceBp, ISkillEffect inner)
        {
            ChanceBp = chanceBp;
            Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
        }

        public ISkillEffect AtLevel(int level, SkillLevelRules rules) =>
            Inner is ILevelableEffect inner && level > 0 ? new ChanceEffect(ChanceBp, inner.AtLevel(level, rules)) : this;

        public ISkillEffect Boosted(int percent) => Inner is IBoostableEffect inner ? new ChanceEffect(ChanceBp, inner.Boosted(percent)) : this;

        public void Apply(in SkillContext c)
        {
            if (ChanceBp <= 0) return;
            if (ChanceBp >= BasisPoints.Full || c.Battle.Random.Next(BasisPoints.Full) < ChanceBp) Inner.Apply(c);
        }

        public void Describe(SkillInfoBuilder info)
        {
            SkillInfoBuilder inner = info.Nested();
            Inner.Describe(inner);
            foreach (EffectInfo e in inner.Effects) info.Add(ChanceBp >= BasisPoints.Full ? e : e.WithChance(ChanceBp));
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

        public void Describe(SkillInfoBuilder info) =>
            info.Add(new EffectInfo(EffectInfoKind.Resource, $"{SkillInfo.ResourceName(ResourceId)} auf {Value}"));
    }

    /// <summary>
    /// Echo: wiederholt den zuletzt ausgeführten eigenen Skill als eigene Ausführung mit dessen Cast-Zeit,
    /// ohne dessen Cooldown zu setzen. Skills, die sich nicht wiederholen lassen (Echo selbst), werden nie als "zuletzt" gemerkt.
    /// </summary>
    public sealed class RepeatLastSkillEffect : ISkillEffect
    {
        public void Apply(in SkillContext c)
        {
            SkillDefinition last = c.User.LastRepeatableSkill;
            if (last == null || !last.CanBeRepeated || last == c.Skill) return;
            c.Battle.QueueRepeat(c.User, last);
        }

        public void Describe(SkillInfoBuilder info) =>
            info.Add(new EffectInfo(EffectInfoKind.Repeat, "wiederholt den letzten eigenen Skill mit dessen Cast-Zeit, ohne Cooldown"));
    }
}

namespace Betaknight.Core.Arena
{
    /// <summary>Wendet eine zielgerichtete Wirkung auf jeden Gegner an (z. B. Brennen oder Gift an allen).</summary>
    public sealed class AllEnemiesEffect : ISkillEffect
    {
        public ISkillEffect Inner { get; }

        public AllEnemiesEffect(ISkillEffect inner) => Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));

        public void Apply(in SkillContext c)
        {
            foreach (Combatant enemy in c.Battle.OpponentsOf(c.User).ToArray())
            {
                if (!c.User.IsAlive) break;
                Inner.Apply(new SkillContext(c.Battle, c.User, enemy, c.Skill, c.RowIndex));
            }
        }

        public void Describe(SkillInfoBuilder info)
        {
            SkillInfoBuilder inner = info.Nested();
            Inner.Describe(inner);
            foreach (EffectInfo e in inner.Effects) info.Add(e.ForAllEnemies());
        }
    }

    public static class SkillEffects
    {
        /// <summary>
        /// Trifft die Wirkung das Ziel (Schaden, Betäubung, Brennen, Debuffs)? Solche Wirkungen gehen beim Modul «Kette»
        /// auf weitere Gegner über; Wirkungen auf sich selbst (Heilung, Buffs) nicht.
        /// </summary>
        public static bool HitsTarget(ISkillEffect effect)
        {
            switch (effect)
            {
                case DamageEffect d: return !d.AllEnemies;
                case StunEffect s: return !s.AllEnemies;
                case BurnEffect _: return true;
                case InterruptChargeEffect _: return true;
                case StatModifierEffect m: return m.OnTarget;
                case ApplyStatusEffect a: return a.OnTarget;
                case ChanceEffect c: return HitsTarget(c.Inner);
                case AllEnemiesEffect _: return false;
                default: return false;
            }
        }
    }
}
