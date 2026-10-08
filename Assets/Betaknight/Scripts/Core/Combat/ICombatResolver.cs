using System;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
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

        /// <summary>Getragene Ausrüstung (Skills, Werte, Sets). Null = nur Basisangriff.</summary>
        public readonly Equipment Equipment;

        /// <summary>Feldtyp, Boss ja/nein, Zug. Für Kontext-Runen und Set-Boni.</summary>
        public readonly BattleContext Context;

        public CombatRequest(CellContent enemy, int tier, PlayerStats stats, RuneLoadout runes,
            Equipment equipment = null, BattleContext context = null)
        {
            Enemy = enemy;
            Tier = tier;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Runes = runes ?? throw new ArgumentNullException(nameof(runes));
            Equipment = equipment;
            Context = context ?? new BattleContext { VsBoss = enemy == CellContent.Boss };
        }
    }

    public readonly struct CombatResult
    {
        /// <summary>Run geht weiter: gewonnen oder (beim Boss) entkommen.</summary>
        public readonly bool Victory;
        public readonly int DamageTaken;

        /// <summary>Gesamtes Gold inklusive Set-Bonus.</summary>
        public readonly int GoldReward;

        /// <summary>Protokoll des Arena-Kampfs für Wiedergabe und Auswertung. Null beim Platzhalter.</summary>
        public readonly BattleResult Battle;

        /// <summary>Namen der Gegner, für Anzeige und Protokoll.</summary>
        public readonly string EnemyName;

        public CombatResult(bool victory, int damageTaken, int goldReward, BattleResult battle = null, string enemyName = null)
        {
            Victory = victory;
            DamageTaken = damageTaken;
            GoldReward = goldReward;
            Battle = battle;
            EnemyName = enemyName;
        }

        public BattleOutcome Outcome => Battle?.Outcome ?? (Victory ? BattleOutcome.Victory : BattleOutcome.Defeat);

        public bool Escaped => Outcome == BattleOutcome.Escaped;
    }

    /// <summary>
    /// Entscheidet einen Kampf: <see cref="ArenaCombatResolver"/> simuliert die Arena mit Logik-Tafel,
    /// <see cref="PlaceholderCombatResolver"/> bleibt für Tests. Wendet selbst nichts an, das macht die Session.
    /// </summary>
    public interface ICombatResolver
    {
        CombatResult Resolve(CombatRequest request, Random random);
    }
}
