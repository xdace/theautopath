using System;
using System.Linq;
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
    /// <summary>Goldminen-Verteidigung: Angriff, Frist, Verlust, Verteidigungskampf mit Minen-Kontext.</summary>
    public class MineDefenseTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly HexCoord West = new HexCoord(-1, 0);

        private sealed class RecordingCombat : ICombatResolver
        {
            public CombatRequest LastRequest;
            public bool Victory = true;

            public CombatResult Resolve(CombatRequest request, Random random)
            {
                LastRequest = request;
                return new CombatResult(Victory, 2, 3);
            }
        }

        private static OverworldSession Session(ICombatResolver combat)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            var s = new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(30, 0), null, null, null, combat);
            s.Map.SetContent(East, CellContent.GoldMine);
            return s;
        }

        /// <summary>Pendelt zwischen Start und Westfeld, bis der Zug erreicht ist.</summary>
        private static void WaitUntil(OverworldSession s, int turn)
        {
            while (s.Turns.CurrentTurn < turn)
            {
                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.TryStep(s.Player.Position == HexCoord.Zero ? West : HexCoord.Zero);
            }
        }

        [Test]
        public void ClaimedMinesGetRaidedOnSchedule()
        {
            OverworldSession s = Session(new RecordingCombat());
            MineRaid started = null;
            s.MineRaidStarted += r => started = r;

            s.TryStep(East);
            s.TryStep(HexCoord.Zero);
            Assert.AreEqual(1, s.ClaimedMines);

            WaitUntil(s, OverworldSession.MineRaidInterval);
            Assert.IsNotNull(started);
            Assert.AreEqual(East, started.Coord);
            Assert.IsTrue(s.Map.GetCell(East).IsUnderAttack);
            Assert.AreEqual(1, s.ClaimedMines, "Während des Angriffs bringt die Mine noch Gold.");
        }

        [Test]
        public void UndefendedMinesAreLost()
        {
            OverworldSession s = Session(new RecordingCombat());
            s.TryStep(East);
            s.TryStep(HexCoord.Zero);
            MineRaid raid = s.StartMineRaid(East, s.Turns.CurrentTurn);
            MineRaid lost = null;
            s.MineLost += r => lost = r;

            WaitUntil(s, raid.DeadlineTurn);
            Assert.AreSame(raid, lost);
            Assert.IsTrue(raid.IsLost);
            Assert.AreEqual(0, s.ClaimedMines);

            int gold = s.Stats.Gold;
            WaitUntil(s, s.Turns.CurrentTurn + OverworldSession.MineIncomeInterval * 2);
            Assert.AreEqual(gold, s.Stats.Gold, "Verlorene Minen zahlen nichts.");
        }

        [Test]
        public void DefendingFightsOnTheMineAndClearsTheRaid()
        {
            var combat = new RecordingCombat();
            OverworldSession s = Session(combat);
            s.TryStep(East);
            s.TryStep(HexCoord.Zero);
            s.StartMineRaid(East, s.Turns.CurrentTurn);
            MajorEventOutcome outcome = null;
            s.MajorEventResolved += o => outcome = o;

            StepResult step = s.TryStep(East);

            Assert.IsTrue(combat.LastRequest.Context.OnGoldMine);
            Assert.AreEqual(2, combat.LastRequest.Tier, "Entfernung 1 + 1 Stufe Bonus.");
            Assert.IsFalse(s.Map.GetCell(East).IsUnderAttack);
            Assert.IsEmpty(s.Raids);
            Assert.AreEqual("Gold Mine defended", outcome.Title);
            CollectionAssert.Contains(outcome.Lines, $"+{3 + OverworldSession.MineDefenseGold} Gold");
            Assert.AreEqual("Mine Defended", s.PendingRuneOffer.Source);
            Assert.IsTrue(step.InterruptsTravel || s.IsBusy);
        }

        [Test]
        public void LosingTheDefenseEndsTheRun()
        {
            var combat = new RecordingCombat();
            OverworldSession s = Session(combat);
            s.TryStep(East);
            s.TryStep(HexCoord.Zero);
            s.StartMineRaid(East, s.Turns.CurrentTurn);
            combat.Victory = false;

            s.TryStep(East);
            Assert.IsTrue(s.IsGameOver);
        }

        [Test]
        public void ArenaDefenseUsesTheScrapHarvesterBonus()
        {
            OverworldSession s = Session(new ArenaCombatResolver());
            foreach (string id in new[] { "plasma_drill", "crawler_tracks", "resource_compactor" }) s.Gear.Equip(s.Items.Get(id));
            // «On Gold Mine» (◆◆◆, bis 6 Zellen) rechts oben, der Bohrer (2×2) darunter, damit er neben dem Kern Platz hat.
            Assert.IsNotNull(s.Board.AddRelay(s.RuneCatalog.Get("on_goldmine"), new Betaknight.Core.Circuit.Cell(3, 0)));
            Betaknight.Core.Skills.SkillInstance drill = s.GainSkill(Betaknight.Core.Arena.SkillIds.Drill);
            Assert.IsTrue(s.PlaceSkill(drill.InstanceId, new Betaknight.Core.Circuit.Cell(2, 1)));
            Assert.IsTrue(s.IsPowered(s.Board.ComponentOf(drill)));
            s.TryStep(East);
            s.TryStep(HexCoord.Zero);
            s.StartMineRaid(East, s.Turns.CurrentTurn);
            CombatResult? fought = null;
            s.CombatFinished += r => fought = r;

            s.TryStep(East);
            Assert.IsTrue(fought.HasValue);
            Assert.IsTrue(fought.Value.Battle.Events.Any(e => e.Kind == Betaknight.Core.Arena.BattleEventKind.ActionStarted
                && e.Detail == Betaknight.Core.Arena.SkillIds.Drill), "Rune «Auf Goldmine» feuert im Verteidigungskampf.");
        }
    }
}
