using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
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
        /// <summary>Die Platine des Spielers.</summary>
        public readonly CircuitBoard Board;

        /// <summary>Getragene Ausrüstung (Skills, Werte, Sets). Null = nur Basisangriff.</summary>
        public readonly Equipment Equipment;

        /// <summary>Feldtyp, Boss ja/nein, Zug. Für Kontext-Runen und Set-Boni.</summary>
        public readonly BattleContext Context;

        /// <summary>Skill-Stufen aus der Ausrüstung (+1 … +3). Null = Grundform.</summary>
        public readonly SkillLevelRules SkillLevels;

        /// <summary>Leben und Schaden der Gegner in Prozent (Elite > 100).</summary>
        public readonly int EnemyHpPercent;
        public readonly int EnemyDamagePercent;

        /// <summary>
        /// Zufall für Gegner und Ausrüstung (aus Karten-Seed und Feld). Gleicher Wert = gleicher Gegner mit gleicher Platine,
        /// wie ihn die Karte vorher zeigt. Null = aus dem Zufall des Kampfes.
        /// </summary>
        public readonly int? EncounterSeed;

        public CombatRequest(CellContent enemy, int tier, PlayerStats stats, CircuitBoard board,
            Equipment equipment = null, BattleContext context = null, SkillLevelRules skillLevels = null,
            int enemyHpPercent = 100, int enemyDamagePercent = 100, int? encounterSeed = null)
        {
            EncounterSeed = encounterSeed;
            SkillLevels = skillLevels;
            EnemyHpPercent = enemyHpPercent;
            EnemyDamagePercent = enemyDamagePercent;
            Enemy = enemy;
            Tier = tier;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Board = board ?? throw new ArgumentNullException(nameof(board));
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

        /// <summary>Was die Gegner benutzt haben und nach einem Sieg geborgen werden kann (leer beim Platzhalter).</summary>
        public readonly IReadOnlyList<EnemyLoot> Loot;

        /// <summary>Wie viele Teile davon geborgen werden dürfen (Elite mehr).</summary>
        public readonly int LootPicks;

        public CombatResult(bool victory, int damageTaken, int goldReward, BattleResult battle = null, string enemyName = null,
            IReadOnlyList<EnemyLoot> loot = null, int lootPicks = 0)
        {
            Loot = loot ?? System.Array.Empty<EnemyLoot>();
            LootPicks = lootPicks;
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
    /// Entscheidet einen Kampf: <see cref="ArenaCombatResolver"/> simuliert die Arena mit der Platine,
    /// <see cref="PlaceholderCombatResolver"/> bleibt für Tests. Wendet selbst nichts an, das macht die Session.
    /// </summary>
    public interface ICombatResolver
    {
        CombatResult Resolve(CombatRequest request, Random random);
    }
}
