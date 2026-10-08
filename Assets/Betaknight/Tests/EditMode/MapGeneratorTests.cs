using System.Linq;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class MapGeneratorTests
    {
        private static MapGenerationConfig Config(int seed = 42) => new MapGenerationConfig { Radius = 6, Seed = seed, SafeZoneRadius = 1 };

        [Test]
        public void GeneratesHexagonOfExpectedSize()
        {
            HexMap map = MapGenerator.Generate(Config());
            Assert.AreEqual(127, map.Count);
            Assert.IsTrue(map.Cells.All(c => c.Coord.DistanceTo(HexCoord.Zero) <= 6));
        }

        [Test]
        public void SameSeedProducesIdenticalMap()
        {
            HexMap a = MapGenerator.Generate(Config(7));
            HexMap b = MapGenerator.Generate(Config(7));

            foreach (HexCell cell in a.Cells)
            {
                Assert.AreEqual(cell.Content, b.GetCell(cell.Coord).Content);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentMaps()
        {
            HexMap a = MapGenerator.Generate(Config(1));
            HexMap b = MapGenerator.Generate(Config(2));
            Assert.IsTrue(a.Cells.Any(c => c.Content != b.GetCell(c.Coord).Content));
        }

        [Test]
        public void StartIsEmptyAndSafeZoneHasNoThreats()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                HexMap map = MapGenerator.Generate(Config(seed));
                Assert.AreEqual(CellContent.Empty, map.GetCell(HexCoord.Zero).Content);

                foreach (HexCoord coord in HexCoord.Spiral(HexCoord.Zero, 1))
                {
                    Assert.IsTrue(MapGenerationConfig.IsAllowedInSafeZone(map.GetCell(coord).Content));
                }
            }
        }

        [Test]
        public void QuotasAreGuaranteed()
        {
            var config = Config(3);
            config.Weights = new System.Collections.Generic.List<ContentWeight> { new ContentWeight(CellContent.Empty, 1) };
            config.Quotas = new System.Collections.Generic.List<ContentQuota>
            {
                new ContentQuota(CellContent.Shop, 3),
                new ContentQuota(CellContent.GoldMine, 2),
            };

            HexMap map = MapGenerator.Generate(config);
            Assert.AreEqual(3, map.Cells.Count(c => c.Content == CellContent.Shop));
            Assert.AreEqual(2, map.Cells.Count(c => c.Content == CellContent.GoldMine));
        }

        [Test]
        public void AllCellsStartHidden()
        {
            HexMap map = MapGenerator.Generate(Config());
            Assert.IsTrue(map.Cells.All(c => c.Visibility == CellVisibility.Hidden));
        }
    }
}
