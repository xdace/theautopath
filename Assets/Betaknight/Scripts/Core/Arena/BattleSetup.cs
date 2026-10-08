using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Oberwelt-Umstände eines Kampfes. Für Kontext-Bedingungen und Set-Boni, für den ganzen Kampf fest.</summary>
    public sealed class BattleContext
    {
        public bool OnGoldMine;
        public bool VsBoss;
        public int Turn;
    }

    /// <summary>Alles, was ein Kampf braucht. Gleiche Einstellungen und gleicher Seed ergeben denselben Kampf.</summary>
    public sealed class BattleSetup
    {
        public CombatantSetup Player = new CombatantSetup { Name = "Betaknight" };
        public List<CombatantSetup> Enemies = new List<CombatantSetup>();
        public int Seed;
        public BattleContext Context = new BattleContext();

        /// <summary>Danach beginnt die Überhitzung (steigender Prozent-Schaden für beide Seiten).</summary>
        public int TimeLimitTicks = Ticks.FromSeconds(90);

        /// <summary>Sicherheitsnetz. Durch die Überhitzung wird es nie erreicht.</summary>
        public int MaxTicks = 10000;
    }

    public enum BattleOutcome
    {
        Victory,
        Defeat,

        /// <summary>Nur wenn das Sicherheitsnetz greift. Zählt als Niederlage.</summary>
        Timeout,
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }
        public bool IsVictory => Outcome == BattleOutcome.Victory;
        public int EndTick { get; }
        public int PlayerHp { get; }
        public int PlayerMaxHp { get; }
        public int EnemiesDefeated { get; }

        /// <summary>Zusätzliches Gold aus Set-Boni o. Ä.</summary>
        public int BonusGold { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        /// <summary>Namen der Tafel-Zeilen des Spielers, für die Anzeige im Protokoll.</summary>
        public IReadOnlyList<string> PlayerRowLabels { get; }

        public BattleResult(BattleOutcome outcome, int endTick, int playerHp, int playerMaxHp, int enemiesDefeated, int bonusGold,
            IReadOnlyList<BattleEvent> events, IReadOnlyList<string> playerRowLabels)
        {
            Outcome = outcome;
            EndTick = endTick;
            PlayerHp = playerHp;
            PlayerMaxHp = playerMaxHp;
            EnemiesDefeated = enemiesDefeated;
            BonusGold = bonusGold;
            Events = events;
            PlayerRowLabels = playerRowLabels;
        }
    }

    public static class CombatSimulation
    {
        /// <summary>Rechnet einen Kampf komplett durch. Die Darstellung spielt danach nur das Protokoll ab.</summary>
        public static BattleResult Run(BattleSetup setup) => new Battle(setup).Run();
    }
}
