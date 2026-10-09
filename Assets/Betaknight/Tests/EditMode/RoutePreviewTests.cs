using Betaknight.Core;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Routen-Vorschau für den Karten-Tooltip: Kosten, Stopp-Grund, Grund für ungültige Ziele, Gefahrenstufe.</summary>
    public class RoutePreviewTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly HexCoord East2 = new HexCoord(2, 0);

        private static OverworldSession Session()
        {
            var map = new HexMap(HexCoord.Zero, 4, 5);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, c == East2 ? CellContent.Enemy : CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map));
        }

        [Test]
        public void HiddenTilesSayWhyTheyCantBeReached()
        {
            OverworldSession s = Session();
            RoutePreview r = s.PreviewRoute(new HexCoord(3, 0));
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(RouteProblem.Hidden, r.Problem);
            Assert.AreEqual(RouteProblem.Here, s.PreviewRoute(HexCoord.Zero).Problem);
        }

        [Test]
        public void ANewTileStopsTheTripAndCostsOneTurn()
        {
            OverworldSession s = Session();
            RoutePreview r = s.PreviewRoute(East);
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(1, r.Turns);
            Assert.AreEqual(1, r.Steps.Count);
            Assert.AreEqual(RouteStop.Unexplored, r.StopReason);
            Assert.IsFalse(r.StopsEarly);
        }

        [Test]
        public void AnEnemyOnTheWayIsNamedWithItsTier()
        {
            OverworldSession s = Session();
            Assert.IsTrue(s.TryStep(East).Success);
            Assert.IsTrue(s.TryStep(HexCoord.Zero).Success);
            RoutePreview r = s.PreviewRoute(East2);
            Assert.IsTrue(r.IsValid, r.Problem.ToString());
            Assert.AreEqual(2, r.Steps.Count);
            Assert.AreEqual(RouteStop.Enemy, r.StopReason);
            Assert.AreEqual(1, r.StopIndex);
            Assert.AreEqual(s.TierAt(East2), s.DangerAt(East2));
            Assert.AreEqual(-1, s.DangerAt(East), "leere Felder haben keine Stufe");
        }
    }
}
