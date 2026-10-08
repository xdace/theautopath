using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;

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
        private readonly SynergyRegistry _synergies;

        public ArenaCombatResolver(EnemyCatalog enemies = null, BoardFactory boards = null, SetBonusRegistry sets = null,
            SynergyRegistry synergies = null)
        {
            _synergies = synergies ?? SynergyRegistry.CreateDefault();
            _enemies = enemies ?? EnemyCatalog.CreateDefault();
            _boards = boards ?? BoardFactory.CreateDefault();
            _sets = sets ?? SetBonusRegistry.CreateDefault();
        }

        public CombatResult Resolve(CombatRequest request, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            bool boss = request.Enemy == CellContent.Boss;
            EnemyDefinition enemy = _enemies.Pick(request.Tier, boss, random);

            List<CombatantSetup> enemies = enemy.Create();
            Scale(enemies, request.EnemyHpPercent, request.EnemyDamagePercent);
            BattleSetup setup = CreateSetup(request, enemies, random.Next());
            BattleResult battle = CombatSimulation.Run(setup);

            int damageTaken = Math.Max(0, request.Stats.Hp - battle.PlayerHp);
            int gold = battle.IsVictory ? random.Next(2, 5) + request.Tier / 2 + battle.BonusGold : 0;
            string name = request.Enemy == CellContent.Elite ? $"Elite: {enemy.Name}" : enemy.Name;
            return new CombatResult(battle.IsSurvived, damageTaken, gold, battle, name);
        }

        /// <summary>Verstärkt Gegner (z. B. Elite) über Leben und Schaden in Prozent.</summary>
        private static void Scale(List<CombatantSetup> enemies, int hpPercent, int damagePercent)
        {
            if (hpPercent == 100 && damagePercent == 100) return;
            foreach (CombatantSetup e in enemies)
            {
                e.Stats = e.Stats.Clone();
                e.Stats[StatKind.MaxHp] = Math.Max(1, e.Stats[StatKind.MaxHp] * hpPercent / 100);
                e.Stats[StatKind.Damage] = Math.Max(0, e.Stats[StatKind.Damage] * damagePercent / 100);
            }
        }

        /// <summary>
        /// Werte des Ritters zu Kampfbeginn, genau wie der Simulator ihn baut (Grundwerte, Ausrüstung, aktive Set-Boni).
        /// Für Kennzahlen im Tafel-Editor; gekämpft wird dabei nicht.
        /// </summary>
        public SkillUserStats PreviewStats(PlayerStats stats, RuneLoadout runes, Equipment equipment, BattleContext context = null,
            SkillLevelRules skillLevels = null)
        {
            return SkillUserStats.From(PreviewCombatant(stats, runes, equipment, context, skillLevels));
        }

        /// <summary>Der Ritter zu Kampfbeginn, gebaut wie im Kampf (für Stat-Leiste und Vorschau). Gekämpft wird nicht.</summary>
        public Combatant PreviewCombatant(PlayerStats stats, RuneLoadout runes, Equipment equipment, BattleContext context = null,
            SkillLevelRules skillLevels = null)
        {
            var request = new CombatRequest(CellContent.Enemy, 0, stats, runes, equipment, context, skillLevels);
            var target = new CombatantSetup { Name = "Ziel", Stats = new CombatStats(1, 0) };
            return new Battle(CreateSetup(request, new List<CombatantSetup> { target }, 0)).Player;
        }

        public BattleSetup CreateSetup(CombatRequest request, List<CombatantSetup> enemies, int seed)
        {
            var baseStats = new CombatStats(Math.Max(1, request.Stats.MaxHp), BaseDamage, BaseAttackInterval);
            CombatantSetup player = PlayerLoadout.CreateCombatant("Ritter", baseStats, request.Equipment,
                request.Runes.ToBoardSpecs(), Math.Max(1, request.Stats.Hp), _boards, _sets, request.SkillLevels, _synergies);

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
