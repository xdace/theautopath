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

        /// <summary>Überlebens-Kampf: lebt der Spieler nach so vielen Ticks noch, entkommt er. 0 = aus.</summary>
        public int SurviveTicks;
    }

    public enum BattleOutcome
    {
        Victory,
        Defeat,

        /// <summary>Nur wenn das Sicherheitsnetz greift. Zählt als Niederlage.</summary>
        Timeout,

        /// <summary>Überlebt, bis das Fluchtportal offen war (Boss). Kein Sieg, aber auch keine Niederlage.</summary>
        Escaped,
    }

    /// <summary>Ein Kämpfer, wie er in den Kampf ging. Für die Wiedergabe des Protokolls.</summary>
    public sealed class FighterInfo
    {
        public Combatant Combatant { get; }
        public string Name => Combatant.Name;
        public Side Side => Combatant.Side;
        public int MaxHp { get; }
        public int StartHp { get; }

        /// <summary>Ressourcen zu Kampfbeginn (Hitze, Ladung, ...).</summary>
        public IReadOnlyDictionary<string, int> StartResources { get; }

        public FighterInfo(Combatant combatant, int maxHp, int startHp, IReadOnlyDictionary<string, int> startResources = null)
        {
            StartResources = startResources ?? new Dictionary<string, int>();
            Combatant = combatant;
            MaxHp = maxHp;
            StartHp = startHp;
        }
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome { get; }
        public bool IsVictory => Outcome == BattleOutcome.Victory;

        /// <summary>Gewonnen oder entkommen: der Run geht weiter.</summary>
        public bool IsSurvived => Outcome == BattleOutcome.Victory || Outcome == BattleOutcome.Escaped;

        /// <summary>Ticks bis zur Flucht bei Überlebens-Kämpfen, sonst 0.</summary>
        public int SurviveTicks { get; internal set; }
        public int EndTick { get; }
        public int PlayerHp { get; }
        public int PlayerMaxHp { get; }
        public int EnemiesDefeated { get; }

        /// <summary>Zusätzliches Gold aus Set-Boni o. Ä.</summary>
        public int BonusGold { get; }
        public IReadOnlyList<BattleEvent> Events { get; }

        /// <summary>Namen der Tafel-Zeilen des Spielers, für die Anzeige im Protokoll.</summary>
        public IReadOnlyList<string> PlayerRowLabels { get; }

        /// <summary>Skill-Namen der Tafel-Zeilen des Spielers ("—" bei verwaisten Zeilen).</summary>
        public IReadOnlyList<string> PlayerRowSkills { get; }

        /// <summary>Alle Kämpfer mit Start-Leben, Spieler zuerst.</summary>
        public IReadOnlyList<FighterInfo> Fighters { get; }

        /// <summary>Entscheidungen des Spielers mit Gründen je Zeile, siehe <see cref="BattleDecision"/>.</summary>
        public IReadOnlyList<BattleDecision> Decisions { get; internal set; } = new BattleDecision[0];

        public BattleResult(BattleOutcome outcome, int endTick, int playerHp, int playerMaxHp, int enemiesDefeated, int bonusGold,
            IReadOnlyList<BattleEvent> events, IReadOnlyList<string> playerRowLabels,
            IReadOnlyList<string> playerRowSkills = null, IReadOnlyList<FighterInfo> fighters = null)
        {
            PlayerRowSkills = playerRowSkills ?? new string[0];
            Fighters = fighters ?? new FighterInfo[0];
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
