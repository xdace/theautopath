namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Thermal Throttling (A-21) statt eines festen Zeitlimits: ab <see cref="StartTicks"/> heizen beide Platinen in Stufen
    /// auf. Jede Stufe macht Cast-Zeiten länger und Schaden grösser, sodass jeder Kampf ein Ende findet. Die 15 s des
    /// Bosses bleiben davon unberührt.
    /// </summary>
    public sealed class ThermalConfig
    {
        /// <summary>Erste Stufe nach so vielen Ticks.</summary>
        public int StartTicks { get; set; } = Ticks.FromSeconds(30);

        /// <summary>Danach alle so viele Ticks eine weitere Stufe.</summary>
        public int StepTicks { get; set; } = Ticks.FromSeconds(5);

        /// <summary>Cast-Zeit je Stufe in Prozent (Komponenten beider Seiten).</summary>
        public int CastPercentPerStep { get; set; } = 10;

        /// <summary>Verursachter Schaden je Stufe in Prozent (beide Seiten).</summary>
        public int DamagePercentPerStep { get; set; } = 15;

        public static ThermalConfig Default { get; } = new ThermalConfig();
    }
}
