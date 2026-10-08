using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class OverworldSessionTests
    {
        /// <summary>Leere Testkarte ohne Zufall.</summary>
        private static OverworldSession EmptySession(int radius = 4)
        {
            var map = new HexMap(HexCoord.Zero, radius);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, radius))
                map.AddCell(new HexCell(c, CellContent.Empty));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map));
        }

        [Test]
        public void StartRevealsCenterAndShowsNeighborsAsUnknown()
        {
            OverworldSession s = EmptySession();

            Assert.AreEqual(CellVisibility.Explored, s.Map.GetCell(HexCoord.Zero).Visibility);
            foreach (HexCoord n in HexCoord.Zero.Neighbors())
                Assert.AreEqual(CellVisibility.Unexplored, s.Map.GetCell(n).Visibility);

            Assert.IsTrue(s.Map.Cells.Where(c => c.Coord.DistanceTo(HexCoord.Zero) >= 2)
                .All(c => c.Visibility == CellVisibility.Hidden));
            Assert.AreEqual(0, s.Turns.CurrentTurn);
        }

        [Test]
        public void StepToNeighborMovesExploresAndCostsOneTurn()
        {
            OverworldSession s = EmptySession();
            HexCoord target = HexCoord.Zero.Neighbor(HexDirection.E);

            StepResult result = s.TryStep(target);

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.FirstVisit);
            Assert.AreEqual(target, s.Player.Position);
            Assert.AreEqual(1, s.Turns.CurrentTurn);
            Assert.AreEqual(CellVisibility.Explored, s.Map.GetCell(target).Visibility);
            Assert.AreEqual(CellVisibility.Unexplored, s.Map.GetCell(target.Neighbor(HexDirection.E)).Visibility);
        }

        [Test]
        public void CannotStepTwoCellsAtOnceOrIntoHiddenCells()
        {
            OverworldSession s = EmptySession();
            HexCoord far = new HexCoord(2, 0);

            StepResult result = s.TryStep(far);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.NotAdjacent, result.Failure);
            Assert.AreEqual(HexCoord.Zero, s.Player.Position);
            Assert.AreEqual(0, s.Turns.CurrentTurn);
        }

        [Test]
        public void CannotLeaveTheMap()
        {
            OverworldSession s = EmptySession(radius: 1);
            s.TryStep(new HexCoord(1, 0));

            StepResult result = s.TryStep(new HexCoord(2, 0));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(MoveFailure.OutOfBounds, result.Failure);
        }

        [Test]
        public void BlockedCellsCannotBeEntered()
        {
            OverworldSession s = EmptySession();
            HexCoord target = new HexCoord(0, 1);
            s.Map.SetWalkable(target, false);

            Assert.IsFalse(s.CanStepTo(target));
            Assert.AreEqual(MoveFailure.Blocked, s.TryStep(target).Failure);
        }

        [Test]
        public void RouteUsesOnlyExploredCellsAsWaypoints()
        {
            OverworldSession s = EmptySession();
            // Weg nach Osten erforschen: (0,0) -> (1,0) -> (2,0)
            s.TryStep(new HexCoord(1, 0));
            s.TryStep(new HexCoord(2, 0));
            s.TryStep(new HexCoord(1, 0));
            s.TryStep(new HexCoord(0, 0));

            List<HexCoord> route = s.PlanRoute(new HexCoord(3, 0));

            Assert.IsNotNull(route);
            Assert.AreEqual(3, route.Count);
            Assert.AreEqual(new HexCoord(3, 0), route[route.Count - 1]);
            for (int i = 0; i < route.Count - 1; i++)
                Assert.AreEqual(CellVisibility.Explored, s.Map.GetCell(route[i]).Visibility);
        }

        [Test]
        public void NoRouteThroughUnknownTerritory()
        {
            OverworldSession s = EmptySession();
            // (2,0) ist noch verborgen, (1,0) nur "?". Kein bekannter Weg dorthin.
            Assert.IsNull(s.PlanRoute(new HexCoord(2, 0)));
        }

        [Test]
        public void RouteToSelfIsEmpty()
        {
            OverworldSession s = EmptySession();
            List<HexCoord> route = s.PlanRoute(HexCoord.Zero);
            Assert.IsNotNull(route);
            Assert.AreEqual(0, route.Count);
        }

        [Test]
        public void BacktrackingIsNotAFirstVisit()
        {
            OverworldSession s = EmptySession();
            s.TryStep(new HexCoord(1, 0));
            StepResult back = s.TryStep(HexCoord.Zero);

            Assert.IsTrue(back.Success);
            Assert.IsFalse(back.FirstVisit);
            Assert.IsFalse(back.InterruptsTravel);
            Assert.AreEqual(2, s.Map.GetCell(HexCoord.Zero).VisitCount);
        }

        [Test]
        public void HostileCellsInterruptTravel()
        {
            OverworldSession s = EmptySession();
            HexCoord enemy = new HexCoord(1, 0);
            s.Map.SetContent(enemy, CellContent.Enemy);
            s.TryStep(enemy);
            s.TryStep(HexCoord.Zero);

            StepResult again = s.TryStep(enemy);

            Assert.IsFalse(again.FirstVisit);
            Assert.IsTrue(again.InterruptsTravel);
        }

        [Test]
        public void EventsFireForTurnsMovesAndCellChanges()
        {
            OverworldSession s = EmptySession();
            int turnEvents = 0, moveEvents = 0, enterEvents = 0, cellChanges = 0;
            s.Turns.TurnEnded += _ => turnEvents++;
            s.Player.Moved += (_, __) => moveEvents++;
            s.CellEntered += _ => enterEvents++;
            s.Map.CellChanged += _ => cellChanges++;

            s.TryStep(new HexCoord(1, 0));

            Assert.AreEqual(1, turnEvents);
            Assert.AreEqual(1, moveEvents);
            Assert.AreEqual(1, enterEvents);
            // Zielfeld wird Explored + 3 neue "?"-Felder in östlicher Richtung.
            Assert.AreEqual(4, cellChanges);
        }

        [Test]
        public void IntervalTurnHelperForBossPacing()
        {
            Assert.IsFalse(TurnSystem.IsIntervalTurn(0, 25));
            Assert.IsFalse(TurnSystem.IsIntervalTurn(24, 25));
            Assert.IsTrue(TurnSystem.IsIntervalTurn(25, 25));
            Assert.IsTrue(TurnSystem.IsIntervalTurn(50, 25));
        }

        [Test]
        public void GeneratedSessionStartsInCenter()
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 5, Seed = 99 });
            Assert.AreEqual(HexCoord.Zero, s.Player.Position);
            Assert.AreEqual(7, s.Map.Cells.Count(c => c.Visibility != CellVisibility.Hidden));
        }
    }
}
