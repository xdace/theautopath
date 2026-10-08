using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Kampfwerte. Neue Werte kommen als neuer Eintrag dazu, Modifikatoren und Status-Effekte
    /// greifen über dieselbe Aufzählung.
    /// </summary>
    public enum StatKind
    {
        MaxHp,

        /// <summary>Waffenschaden pro Basisangriff. Skills rechnen in Prozent davon.</summary>
        Damage,

        /// <summary>Ticks zwischen zwei Basisangriffen ohne Tempo-Bonus.</summary>
        AttackInterval,

        /// <summary>Tempo-Bonus in Basispunkten, additiv (+1000 = 10 % schneller).</summary>
        AttackSpeed,
        Armor,

        /// <summary>Rüstung in Basispunkten multipliziert (10000 = unverändert). Wirkt nach Rüstungsbruch.</summary>
        ArmorMultiplier,
        Dodge,

        /// <summary>Obergrenze der Ausweichchance in Basispunkten.</summary>
        DodgeCap,
        Block,
        Crit,

        /// <summary>Präzision in Basispunkten: senkt die Ausweichchance des Ziels. Negativ = Gegner weicht leichter aus.</summary>
        Accuracy,

        /// <summary>Bonus auf Flächenschaden in Basispunkten.</summary>
        AreaDamage,
    }

    /// <summary>Grundwerte eines Kämpfers. Nicht gesetzte Werte haben ihren Standard.</summary>
    public sealed class CombatStats
    {
        private readonly Dictionary<StatKind, int> _values = new Dictionary<StatKind, int>();

        public int this[StatKind kind]
        {
            get => _values.TryGetValue(kind, out int v) ? v : Default(kind);
            set => _values[kind] = value;
        }

        public CombatStats(int maxHp = 30, int damage = 4, int attackInterval = Ticks.PerSecond, int armor = 0)
        {
            if (maxHp < 1) throw new ArgumentOutOfRangeException(nameof(maxHp));
            this[StatKind.MaxHp] = maxHp;
            this[StatKind.Damage] = damage;
            this[StatKind.AttackInterval] = attackInterval;
            this[StatKind.Armor] = armor;
        }

        public CombatStats Clone()
        {
            var copy = new CombatStats(this[StatKind.MaxHp]);
            foreach (KeyValuePair<StatKind, int> pair in _values) copy._values[pair.Key] = pair.Value;
            return copy;
        }

        public static int Default(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.AttackInterval: return Ticks.PerSecond;
                case StatKind.ArmorMultiplier: return BasisPoints.Full;
                case StatKind.DodgeCap: return BasisPoints.Percent(60);
                default: return 0;
            }
        }
    }
}
