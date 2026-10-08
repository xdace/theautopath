using System;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Combat
{
    /// <summary>Alles, was ein Kampf über den Spieler und den Gegner wissen muss.</summary>
    public readonly struct CombatRequest
    {
        public readonly CellContent Enemy;

        /// <summary>Entfernung vom Start, dient als Gegnerstufe.</summary>
        public readonly int Tier;
        public readonly PlayerStats Stats;
        public readonly RuneLoadout Runes;

        public CombatRequest(CellContent enemy, int tier, PlayerStats stats, RuneLoadout runes)
        {
            Enemy = enemy;
            Tier = tier;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Runes = runes ?? throw new ArgumentNullException(nameof(runes));
        }
    }

    public readonly struct CombatResult
    {
        public readonly bool Victory;
        public readonly int DamageTaken;
        public readonly int GoldReward;

        public CombatResult(bool victory, int damageTaken, int goldReward)
        {
            Victory = victory;
            DamageTaken = damageTaken;
            GoldReward = goldReward;
        }
    }

    /// <summary>
    /// Entscheidet einen Kampf. Heute ein Platzhalter (<see cref="PlaceholderCombatResolver"/>),
    /// später die Kampfarena mit Logik-Runen. Wendet selbst nichts an, das macht die Session.
    /// </summary>
    public interface ICombatResolver
    {
        CombatResult Resolve(CombatRequest request, Random random);
    }
}
