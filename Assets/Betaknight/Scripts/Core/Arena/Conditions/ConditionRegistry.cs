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
            return r;
        }

        private static void RegisterStateConditions(ConditionRegistry r)
        {
            r.Register("always", _ => AlwaysCondition.Instance);
            r.Register("hp_low", p => new HpBelowCondition(BasisPoints.Percent(p)));
            r.Register("hp_critical", p => new HpBelowCondition(BasisPoints.Percent(p)));
            r.Register("hp_full", _ => new HpFullCondition());
            r.Register("enemy_low", p => new EnemyHpBelowCondition(BasisPoints.Percent(p)));
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
    }
}
