using System.Linq;
using Betaknight.Core.Hex;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class HexCoordTests
    {
        [Test]
        public void EveryCoordHasSixDistinctNeighborsAtDistanceOne()
        {
            var center = new HexCoord(2, -3);
            var neighbors = center.Neighbors().ToList();

            Assert.AreEqual(6, neighbors.Count);
            Assert.AreEqual(6, neighbors.Distinct().Count());
            Assert.IsTrue(neighbors.All(n => n.DistanceTo(center) == 1));
        }

        [Test]
        public void OppositeDirectionsCancelOut()
        {
            var start = new HexCoord(1, 1);
            for (int i = 0; i < 6; i++)
            {
                var dir = (HexDirection)i;
                Assert.AreEqual(start, start.Neighbor(dir).Neighbor(dir.Opposite()));
            }
        }

        [Test]
        public void DistanceIsSymmetric()
        {
            var a = new HexCoord(3, -1);
            var b = new HexCoord(-2, 4);
            Assert.AreEqual(5, a.DistanceTo(b));
            Assert.AreEqual(a.DistanceTo(b), b.DistanceTo(a));
        }

        [Test]
        public void RingHasSixTimesRadiusCells()
        {
            for (int radius = 1; radius <= 5; radius++)
            {
                var ring = HexCoord.Ring(HexCoord.Zero, radius).ToList();
                Assert.AreEqual(6 * radius, ring.Count);
                Assert.AreEqual(ring.Count, ring.Distinct().Count());
                Assert.IsTrue(ring.All(c => c.DistanceTo(HexCoord.Zero) == radius));
            }
        }

        [Test]
        public void SpiralCountMatchesHexagonFormula()
        {
            // Felder in einem Sechseck mit Radius n: 3n(n+1) + 1
            for (int radius = 0; radius <= 6; radius++)
            {
                int expected = 3 * radius * (radius + 1) + 1;
                Assert.AreEqual(expected, HexCoord.Spiral(HexCoord.Zero, radius).Count());
            }
        }

        [Test]
        public void LayoutRoundTripsEveryCell()
        {
            var layout = new HexLayout(0.5f, new HexPoint(3f, -2f));
            foreach (HexCoord coord in HexCoord.Spiral(HexCoord.Zero, 8))
            {
                HexPoint p = layout.ToWorld(coord);
                Assert.AreEqual(coord, layout.FromWorld(p.X, p.Y));
            }
        }

        [Test]
        public void LayoutPointsNorthEastUpAndRight()
        {
            var layout = new HexLayout(1f);
            HexPoint origin = layout.ToWorld(HexCoord.Zero);
            HexPoint ne = layout.ToWorld(HexCoord.Zero.Neighbor(HexDirection.NE));

            Assert.IsTrue(ne.X > origin.X);
            Assert.IsTrue(ne.Y > origin.Y);
        }

        [Test]
        public void FromWorldPicksNearestCellNearEdges()
        {
            var layout = new HexLayout(1f);
            HexPoint east = layout.ToWorld(HexCoord.Zero.Neighbor(HexDirection.E));

            // Knapp diesseits bzw. jenseits der Mitte zwischen zwei Feldern.
            Assert.AreEqual(HexCoord.Zero, layout.FromWorld(east.X * 0.45f, 0f));
            Assert.AreEqual(HexCoord.Zero.Neighbor(HexDirection.E), layout.FromWorld(east.X * 0.55f, 0f));
        }
    }
}
