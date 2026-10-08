using System;
using Betaknight.Core;
using Betaknight.Core.Combat;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Akte: Fluchtportal nach dem Boss, neue härtere Karte, Ritter und Build kommen mit.</summary>
    public class ActTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        private sealed class RecordingCombat : ICombatResolver
        {
            public CombatRequest LastRequest;

            public CombatResult Resolve(CombatRequest request, Random random)
            {
                LastRequest = request;
                return new CombatResult(true, 1, 3);
            }
        }

        private static OverworldSession Session(ICombatResolver combat)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(60, 0), null, null, null, combat);
        }

        private static void WalkToBoss(OverworldSession s)
        {
            while (!s.PendingPortal && !s.IsGameOver && s.Turns.CurrentTurn < OverworldSession.BossInterval)
            {
                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.TryStep(s.Player.Position == HexCoord.Zero ? East : HexCoord.Zero);
            }
        }

        [Test]
        public void SurvivingTheBossOpensThePortalAndBlocksMovement()
        {
            OverworldSession s = Session(new RecordingCombat());
            WalkToBoss(s);

            Assert.IsTrue(s.PendingPortal);
            Assert.IsTrue(s.IsBusy);
            Assert.IsTrue(s.CanEnterPortal);
            Assert.IsFalse(s.TryStep(s.Player.Position == HexCoord.Zero ? East : HexCoord.Zero).Success);
        }

        [Test]
        public void EnteringThePortalRaisesActCompletedOnce()
        {
            OverworldSession s = Session(new RecordingCombat());
            int completed = 0;
            s.ActCompleted += _ => completed++;

            Assert.IsFalse(s.EnterPortal());
            WalkToBoss(s);
            Assert.IsTrue(s.EnterPortal());
            Assert.IsFalse(s.EnterPortal());
            Assert.AreEqual(1, completed);
            Assert.IsFalse(s.PendingPortal);
        }

        [Test]
        public void ThePortalHealsHalfOfMaxHp()
        {
            OverworldSession s = Session(new RecordingCombat());
            WalkToBoss(s);
            s.Stats.Damage(40);
            int before = s.Stats.Hp;

            s.EnterPortal();
            Assert.AreEqual(before + s.Stats.MaxHp * OverworldSession.PortalHealPercent / 100, s.Stats.Hp);
        }

        [Test]
        public void NextActKeepsKnightBuildAndTurnsOnANewMap()
        {
            var config = new MapGenerationConfig { Seed = 42 };
            OverworldSession first = OverworldSession.Create(config, 2, KnightKit.Defaults[0]);
            first.Stats.AddGold(17);
            first.Stats.AddShards(2);

            OverworldSession next = OverworldSession.CreateNextAct(config, first);

            Assert.AreEqual(2, next.Act);
            Assert.AreNotEqual(first.Map.Seed, next.Map.Seed);
            Assert.AreSame(first.Stats, next.Stats);
            Assert.AreSame(first.Runes, next.Runes);
            Assert.AreSame(first.Gear, next.Gear);
            Assert.AreSame(first.Kit, next.Kit);
            Assert.AreEqual(first.Turns.CurrentTurn, next.Turns.CurrentTurn);
            Assert.AreEqual(2, next.Exploration.SightRadius);
            Assert.AreEqual(next.Map.Center, next.Player.Position);
            Assert.AreEqual(0, next.ClaimedMines);
            Assert.AreEqual(42, config.Seed, "Die Vorlage bleibt unverändert.");
        }

        [Test]
        public void EnemiesInTheNextActAreTougher()
        {
            var combat = new RecordingCombat();
            OverworldSession act1 = Session(combat);
            act1.Map.SetContent(East, CellContent.Enemy);
            act1.TryStep(East);
            int tier1 = combat.LastRequest.Tier;

            var config = new MapGenerationConfig { Seed = 7 };
            OverworldSession act2 = OverworldSession.CreateNextAct(config, act1);
            HexCoord target = act2.Map.Center + East;
            act2.Map.SetContent(target, CellContent.Enemy);
            act2.TryStep(target);

            Assert.AreEqual(1, tier1);
            Assert.AreEqual(1 + OverworldSession.ActTierBonus, combat.LastRequest.Tier);
            Assert.AreEqual(act2.TierAt(target), combat.LastRequest.Tier);
        }

        [Test]
        public void BossFiresAgainInTheNextActAfterTwentyFiveMoreTurns()
        {
            OverworldSession act1 = Session(new RecordingCombat());
            WalkToBoss(act1);
            act1.EnterPortal();

            OverworldSession act2 = OverworldSession.CreateNextAct(new MapGenerationConfig { Seed = 3 }, act1);
            Assert.AreEqual(OverworldSession.BossInterval, act2.TurnsUntilBoss);
            Assert.IsFalse(act2.PendingPortal);
        }
    }
}
