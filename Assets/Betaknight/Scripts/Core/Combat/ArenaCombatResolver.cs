using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;

namespace Betaknight.Core.Combat
{
    /// <summary>
    /// Entscheidet Kämpfe in der Arena: baut den Ritter aus Werten, Ausrüstung und Runen-Zeilen,
    /// würfelt einen Gegner passend zur Stufe und simuliert den Kampf. Das Protokoll geht im Ergebnis mit.
    /// </summary>
    public sealed class ArenaCombatResolver : ICombatResolver
    {
        /// <summary>Grundwerte des Ritters ohne Ausrüstung. Max-HP kommt aus den Oberwelt-Werten.</summary>
        public int BaseDamage = 5;
        public int BaseAttackInterval = Ticks.PerSecond;

        /// <summary>Gegen den Boss muss der Ritter so lange überleben, bis das Fluchtportal offen ist.</summary>
        public int BossSurviveTicks = Ticks.FromSeconds(15);

        private readonly EnemyCatalog _enemies;
        private readonly BoardFactory _boards;
        private readonly SetBonusRegistry _sets;

        public ArenaCombatResolver(EnemyCatalog enemies = null, BoardFactory boards = null, SetBonusRegistry sets = null)
        {
            _enemies = enemies ?? EnemyCatalog.CreateDefault();
            _boards = boards ?? BoardFactory.CreateDefault();
            _sets = sets ?? SetBonusRegistry.CreateDefault();
        }

        public CombatResult Resolve(CombatRequest request, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            bool boss = request.Enemy == CellContent.Boss;
            EnemyDefinition enemy = _enemies.Pick(request.Tier, boss, random);

            BattleSetup setup = CreateSetup(request, enemy.Create(), random.Next());
            BattleResult battle = CombatSimulation.Run(setup);

            int damageTaken = Math.Max(0, request.Stats.Hp - battle.PlayerHp);
            int gold = battle.IsVictory ? random.Next(2, 5) + request.Tier / 2 + battle.BonusGold : 0;
            return new CombatResult(battle.IsSurvived, damageTaken, gold, battle, enemy.Name);
        }

        public BattleSetup CreateSetup(CombatRequest request, List<CombatantSetup> enemies, int seed)
        {
            var baseStats = new CombatStats(Math.Max(1, request.Stats.MaxHp), BaseDamage, BaseAttackInterval);
            CombatantSetup player = PlayerLoadout.CreateCombatant("Ritter", baseStats, request.Equipment,
                request.Runes.ToBoardSpecs(), Math.Max(1, request.Stats.Hp), _boards, _sets);

            return new BattleSetup
            {
                Player = player,
                Enemies = enemies,
                Seed = seed,
                Context = request.Context,
                SurviveTicks = request.Context.VsBoss ? BossSurviveTicks : 0,
            };
        }
    }
}
