namespace Betaknight.Core.Arena
{
    /// <summary>Kampfzeit in festen Ticks. Alle Zeiten im Kampf sind ganze Ticks.</summary>
    public static class Ticks
    {
        public const int PerSecond = 20;

        public static int FromSeconds(int seconds) => seconds * PerSecond;

        /// <summary>Für Werte wie 1,5 s: Zehntelsekunden in Ticks.</summary>
        public static int FromTenths(int tenths) => tenths * PerSecond / 10;
    }

    /// <summary>Prozente als Basispunkte: 10000 = 100 %. Keine Gleitkommazahlen in der Kampflogik.</summary>
    public static class BasisPoints
    {
        public const int Full = 10000;

        public static int Percent(int percent) => percent * 100;

        /// <summary>value × bp / 10000, abgerundet.</summary>
        public static int Of(int value, int bp) => (int)((long)value * bp / Full);
    }
}
