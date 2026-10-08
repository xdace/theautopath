using System;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Cast-Zeit: das Ausholen jeder Ausführung bis zur Wirkung. Loops werden nur über Cast-Zeiten begrenzt.
    /// Reduktionen in Prozent addieren sich und wirken bis zur Untergrenze, nie darunter.
    /// </summary>
    public static class CastTime
    {
        /// <summary>Absolute Untergrenze in Ticks (0,1 s). Pro Kampf über <see cref="BattleSetup.MinCastTicks"/> änderbar.</summary>
        public const int DefaultMinTicks = 2;

        /// <summary>Grund-Cast-Zeiten: schnell 0,4 s, mittel 0,8 s, schwer 1,5 s.</summary>
        public static readonly int Fast = Ticks.FromTenths(4);
        public static readonly int Medium = Ticks.FromTenths(8);
        public static readonly int Heavy = Ticks.FromTenths(15);

        /// <summary>Grundwert mit Änderung in Prozent (−20 = 20 % schneller), mindestens <paramref name="minTicks"/>.</summary>
        public static int Apply(int baseTicks, int percent, int minTicks = DefaultMinTicks)
        {
            long factor = Math.Max(0L, 100L + percent);
            long ticks = Math.Max(0L, baseTicks) * factor / 100L;
            return (int)Math.Max(Math.Max(0, minTicks), Math.Min(int.MaxValue / 2, ticks));
        }
    }
}
