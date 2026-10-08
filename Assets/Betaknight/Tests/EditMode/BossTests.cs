using System;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Turns;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Boss alle 25 Züge: unbesiegbar, Überleben bis zum Portal. Phantom-Signal ist dafür gebaut.</summary>
    public class BossTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        private static OverworldSession Session(int hp)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(hp, 0));
        }

        private static void Walk(OverworldSession s, int steps)
        {
            for (int i = 0; i < steps && !s.IsGameOver; i++)
            {
                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.TryStep(s.Player.Position == HexCoord.Zero ? East : HexCoord.Zero);
            }
        }

        [Test]
        public void SurviveTicksEndTheFightAsAnEscape()
        {
            BattleSetup setup = Duel(Fighter("A", 100, 1), Fighter("Boss", 99999, 1));
            setup.SurviveTicks = Ticks.FromSeconds(5);
            BattleResult r = CombatSimulation.Run(setup);

            Assert.AreEqual(BattleOutcome.Escaped, r.Outcome);
            Assert.AreEqual(Ticks.FromSeconds(5), r.EndTick);
            Assert.IsTrue(r.IsSurvived);
            Assert.IsFalse(r.IsVictory);
        }

        [Test]
        public void BossAppearsEveryTwentyFiveTurns()
        {
            OverworldSession s = Session(60);
            s.Gear.Equip(s.Items.Get("round_shield"));
            s.Runes.TryAdd(s.RuneCatalog.Get("enemy_charging"), SkillIds.ShieldBash);
            CombatResult? boss = null;
            s.BossEncountered += r => boss = r;

            Walk(s, OverworldSession.BossInterval - 1);
            Assert.IsNull(boss);
            Assert.AreEqual(1, s.TurnsUntilBoss);

            Walk(s, 1);
            Assert.IsTrue(boss.HasValue);
            Assert.AreEqual(BattleOutcome.Escaped, boss.Value.Outcome);
            Assert.IsFalse(s.IsGameOver);
            Assert.GreaterOrEqual(s.Stats.Gold, OverworldSession.BossEscapeGold);
        }

        [Test]
        public void AWeakKnightDiesToTheBoss()
        {
            OverworldSession s = Session(10);
            Walk(s, OverworldSession.BossInterval);
            Assert.IsTrue(s.IsGameOver);
        }

        [Test]
        public void PhantomSignalOutlastsTheBossBetterThanAPlainKnight()
        {
            var items = EquipmentCatalog.CreateDefault();
            var runes = RuneCatalog.CreateDefault();
            int RestHp(string[] gear, params (string rune, string skill)[] rows)
            {
                var eq = new Equipment();
                foreach (string g in gear) eq.Equip(items.Get(g));
                var loadout = new RuneLoadout(4);
                foreach ((string rune, string skill) in rows) loadout.TryAdd(runes.Get(rune), skill);
                CombatResult r = new ArenaCombatResolver().Resolve(new CombatRequest(CellContent.Boss, 5, new PlayerStats(24, 0), loadout, eq,
                    new BattleContext { VsBoss = true }), new Random(1));
                return r.Victory ? 24 - r.DamageTaken : 0;
            }

            int plain = RestHp(new[] { "short_blade" }, ("on_hit", SkillIds.ArmorBreak));
            int phantom = RestHp(new[] { "gyro_thrusters", "holo_projector", "shock_dagger" },
                ("always", SkillIds.Flashbang), ("enemy_charging", SkillIds.Thrusters));
            Assert.Greater(phantom, plain);
            Assert.Greater(phantom, 0);
        }
    }
}
