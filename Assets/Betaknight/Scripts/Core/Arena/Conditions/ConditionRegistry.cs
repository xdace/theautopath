using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Verbindet Runen-Ids mit Bedingungen. Eine neue Rune braucht nur einen Katalog-Eintrag
    /// und eine Registrierung hier; der Simulator bleibt unverändert.
    /// Der Parameter ist der Wert der aktuellen Runen-Stufe (Prozent, Sekunden oder Anzahl).
    /// </summary>
    public sealed class ConditionRegistry
    {
        private readonly Dictionary<string, Func<int, ICondition>> _factories = new Dictionary<string, Func<int, ICondition>>();

        public void Register(string runeId, Func<int, ICondition> factory)
        {
            if (string.IsNullOrEmpty(runeId)) throw new ArgumentException("Runen-Id fehlt.", nameof(runeId));
            _factories[runeId] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public bool Contains(string runeId) => runeId != null && _factories.ContainsKey(runeId);

        public bool TryCreate(string runeId, int parameter, out ICondition condition)
        {
            condition = null;
            if (!Contains(runeId)) return false;
            condition = _factories[runeId](parameter);
            return condition != null;
        }

        public ICondition Create(string runeId, int parameter)
        {
            if (!TryCreate(runeId, parameter, out ICondition c)) throw new KeyNotFoundException($"Keine Bedingung für Rune {runeId}.");
            return c;
        }

        public static ConditionRegistry CreateDefault()
        {
            var r = new ConditionRegistry();
            RegisterStateConditions(r);
            RegisterEventConditions(r);
            RegisterClockAndCounterConditions(r);
            return r;
        }

        private static void RegisterStateConditions(ConditionRegistry r)
        {
            r.Register("always", _ => AlwaysCondition.Instance);
            r.Register("hp_low", p => new HpBelowCondition(BasisPoints.Percent(p)));
            r.Register("hp_critical", p => new HpBelowCondition(BasisPoints.Percent(p)));
            r.Register("hp_full", _ => new HpFullCondition());
            r.Register("enemy_low", p => new EnemyHpBelowCondition(BasisPoints.Percent(p)));
            // Evolution von «HP unter x %» (Rezept Phantom): auch direkt nach dem Ausweichen.
            r.Register(Runes.EvolvedRuneIds.PhantomReflex, p => new AnyCondition(new HpBelowCondition(BasisPoints.Percent(p)), r.Create("after_dodge", 0)));
            r.Register("enemy_armored", _ => new EnemyArmoredCondition());
            r.Register("enemy_stunned", _ => new EnemyStunnedCondition());
            r.Register("enemy_charging", _ => new EnemyChargingCondition());
            r.Register("enemy_stronger", _ => new EnemyStrongerCondition());
            r.Register("enemy_burning", _ => new EnemyHasStatusCondition(StatusIds.Burn));
            r.Register("outnumbered", p => new OutnumberedCondition(p));
            r.Register("last_enemy", _ => new LastEnemyCondition());
            r.Register("overheat", _ => new OverheatCondition());
            r.Register("on_goldmine", _ => new ContextCondition(ContextCondition.Kind.OnGoldMine));
            r.Register("vs_boss", _ => new ContextCondition(ContextCondition.Kind.VsBoss));
        }

        private static bool Own(ConditionContext c, BattleEvent e, BattleEventKind kind) => e.Kind == kind && e.Source == c.Self;
        private static bool OnSelf(ConditionContext c, BattleEvent e, BattleEventKind kind) => e.Kind == kind && e.Target == c.Self;

        private static void RegisterEventConditions(ConditionRegistry r)
        {
            r.Register("on_hit", _ => new EventCondition((c, e) => Own(c, e, BattleEventKind.Hit), (c, e) => e.Target));
            r.Register("on_crit", _ => new EventCondition((c, e) => Own(c, e, BattleEventKind.Crit), (c, e) => e.Target));
            r.Register("when_hit", _ => new EventCondition((c, e) => OnSelf(c, e, BattleEventKind.Hit), (c, e) => e.Source));
            r.Register("after_block", _ => new EventCondition((c, e) => OnSelf(c, e, BattleEventKind.Blocked), (c, e) => e.Source));
            r.Register("after_dodge", _ => new EventCondition((c, e) => OnSelf(c, e, BattleEventKind.Dodged), (c, e) => e.Source));
            r.Register("after_self_damage", _ => new EventCondition((c, e) => OnSelf(c, e, BattleEventKind.SelfDamage) && e.Amount > 0));
            r.Register("after_heal", _ => new EventCondition((c, e) => OnSelf(c, e, BattleEventKind.Healed)));
            r.Register("enemy_dies", _ => new EventCondition((c, e) => e.Kind == BattleEventKind.Death && e.Target != null && e.Target.Side != c.Self.Side));
            r.Register("big_hit_taken", p => new EventCondition((c, e) =>
                OnSelf(c, e, BattleEventKind.Damage)
                && (long)e.Amount * 100 > (long)Math.Max(1, p - c.Self.Relief(ReliefIds.BigHitLower)) * c.Self.MaxHp, (c, e) => e.Source));
            r.Register("after_own_skill", _ => new EventCondition((c, e) =>
                Own(c, e, BattleEventKind.ActionExecuted) && e.Detail != SkillDefinition.BasicAttackId && e.RowIndex != c.Row.Index));
            r.Register("battle_start", _ => new BattleStartCondition());
            r.Register("chain", _ => new ChainCondition());
        }

        private static void RegisterClockAndCounterConditions(ConditionRegistry r)
        {
            r.Register("every_5s", p => new ClockCondition(Ticks.FromSeconds(p)));
            r.Register("every_20s", p => new ClockCondition(Ticks.FromSeconds(p)));
            r.Register("every_3rd", p => new CounterCondition(p, (c, e) => Own(c, e, BattleEventKind.ActionExecuted) && e.Amount == 1));
            r.Register("every_nth_attack", p => new CounterCondition(p, (c, e) => Own(c, e, BattleEventKind.ActionExecuted) && e.Amount == 1));
            r.Register("every_nth_hit_taken", p => new CounterCondition(p, (c, e) => OnSelf(c, e, BattleEventKind.Hit)));
            r.Register("dodge_streak", p => new DodgeStreakCondition(p));
            r.Register("tempo_stacks", p => new ResourceAtLeastCondition(ResourceIds.Tempo, p));
            r.Register("charge_full", p => new ResourceAtLeastCondition(ResourceIds.Charge, p));
        }
    }
}
