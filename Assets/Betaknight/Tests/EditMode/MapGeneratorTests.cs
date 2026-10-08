using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Encounters;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class MapGeneratorTests
    {
        private static MapGenerationConfig Config(int seed = 42) => new MapGenerationConfig { Radius = 6, Seed = seed };

        private static IEnumerable<HexMap> ManyMaps(int count = 200)
        {
            for (int seed = 0; seed < count; seed++) yield return MapGenerator.Generate(Config(seed));
        }

        private static EncounterSize SizeOf(HexCell cell, EncounterCatalog catalog) =>
            cell.Content == CellContent.Encounter ? catalog.Get(cell.EncounterId).Size : EncounterSize.Major;

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
                HexCell other = b.GetCell(cell.Coord);
                Assert.AreEqual(cell.Content, other.Content);
                Assert.AreEqual(cell.EncounterId, other.EncounterId);
            }
        }

        [Test]
        public void DifferentSeedsProduceDifferentMaps()
        {
            HexMap a = MapGenerator.Generate(Config(1));
            HexMap b = MapGenerator.Generate(Config(2));
            Assert.IsTrue(a.Cells.Any(c => c.Content != b.GetCell(c.Coord).Content
                                           || c.EncounterId != b.GetCell(c.Coord).EncounterId));
        }

        [Test]
        public void EveryCellExceptStartHasAnEvent()
        {
            foreach (HexMap map in ManyMaps())
            {
                Assert.AreEqual(CellContent.Empty, map.GetCell(HexCoord.Zero).Content);
                Assert.IsTrue(map.Cells.Where(c => c.Coord != HexCoord.Zero).All(c => c.Content != CellContent.Empty));
                Assert.IsTrue(map.Cells.All(c => c.Content != CellContent.Encounter || !string.IsNullOrEmpty(c.EncounterId)));
            }
        }

        [Test]
        public void FirstRingHasOnlyMinorEvents()
        {
            var catalog = EncounterCatalog.CreateDefault();
            foreach (HexMap map in ManyMaps())
            {
                foreach (HexCoord coord in HexCoord.Ring(HexCoord.Zero, 1))
                    Assert.AreEqual(EncounterSize.Minor, SizeOf(map.GetCell(coord), catalog));
            }
        }

        [Test]
        public void ContentRulesAreRespected()
        {
            foreach (HexMap map in ManyMaps())
            {
                var cells = map.Cells.ToList();
                Assert.LessOrEqual(cells.Count(c => c.Content == CellContent.Shop), 2);
                Assert.LessOrEqual(cells.Count(c => c.Content == CellContent.Treasure), 6);
                Assert.LessOrEqual(cells.Count(c => c.Content == CellContent.GoldMine), 4);

                Assert.IsTrue(cells.Where(c => c.Content == CellContent.Shop).All(c => c.Coord.DistanceTo(HexCoord.Zero) >= 3));
                Assert.IsTrue(cells.Where(c => c.Content == CellContent.Treasure).All(c => c.Coord.DistanceTo(HexCoord.Zero) >= 2));
                Assert.IsTrue(cells.Where(c => c.Content == CellContent.Enemy).All(c => c.Coord.DistanceTo(HexCoord.Zero) >= 2));
            }
        }

        [Test]
        public void DefaultQuotasAreGuaranteed()
        {
            foreach (HexMap map in ManyMaps(50))
            {
                int Count(CellContent content, int min, int max) =>
                    map.Cells.Count(c => c.Content == content && c.Coord.DistanceTo(HexCoord.Zero) >= min && c.Coord.DistanceTo(HexCoord.Zero) <= max);

                Assert.GreaterOrEqual(Count(CellContent.Enemy, 2, 2), 2);
                Assert.GreaterOrEqual(Count(CellContent.Treasure, 2, 3), 1);
                Assert.GreaterOrEqual(Count(CellContent.Shop, 3, 4), 1);
                Assert.GreaterOrEqual(Count(CellContent.Shop, 5, 6), 1);
                Assert.GreaterOrEqual(Count(CellContent.GoldMine, 3, 6), 2);
            }
        }

        [Test]
        public void EncountersRespectTheirMinDistance()
        {
            var catalog = EncounterCatalog.CreateDefault();
            foreach (HexMap map in ManyMaps())
            {
                foreach (HexCell cell in map.Cells.Where(c => c.Content == CellContent.Encounter))
                    Assert.GreaterOrEqual(cell.Coord.DistanceTo(HexCoord.Zero), catalog.Get(cell.EncounterId).MinDistance);
            }
        }

        [Test]
        public void MajorEventsGetDenserFurtherOut()
        {
            var catalog = EncounterCatalog.CreateDefault();
            int ring2 = 0, ring2Cells = 0, outer = 0, outerCells = 0;
            foreach (HexMap map in ManyMaps())
            {
                foreach (HexCell cell in map.Cells)
                {
                    int d = cell.Coord.DistanceTo(HexCoord.Zero);
                    bool major = SizeOf(cell, catalog) == EncounterSize.Major;
                    if (d == 2) { ring2Cells++; if (major) ring2++; }
                    if (d >= 4) { outerCells++; if (major) outer++; }
                }
            }

            Assert.Less((float)ring2 / ring2Cells, (float)outer / outerCells);
        }

        [Test]
        public void QuotaAboveMaxCountIsRejected()
        {
            var config = Config();
            config.Quotas = new List<ContentQuota> { new ContentQuota(CellContent.Shop, 3, 3, 6) };
            Assert.Throws<System.ArgumentException>(() => MapGenerator.Generate(config));
        }

        [Test]
        public void QuotaCloserThanRuleIsRejected()
        {
            var config = Config();
            config.Quotas = new List<ContentQuota> { new ContentQuota(CellContent.Shop, 1, 1, 2) };
            Assert.Throws<System.ArgumentException>(() => MapGenerator.Generate(config));
        }

        [Test]
        public void MaxCountFallsBackToEncounters()
        {
            var config = Config();
            config.Bands = new List<DistanceBand> { new DistanceBand(int.MaxValue, 0, 0, 1, new ContentWeight(CellContent.Shop, 1)) };
            config.Rules = new List<ContentRule> { new ContentRule(CellContent.Shop, 1, 3) };
            config.Quotas = new List<ContentQuota>();

            HexMap map = MapGenerator.Generate(config);

            Assert.AreEqual(3, map.Cells.Count(c => c.Content == CellContent.Shop));
            Assert.IsTrue(map.Cells.Where(c => c.Coord != HexCoord.Zero).All(c => c.Content != CellContent.Empty));
        }

        [Test]
        public void AllCellsStartHiddenAndUnresolved()
        {
            HexMap map = MapGenerator.Generate(Config());
            Assert.IsTrue(map.Cells.All(c => c.Visibility == CellVisibility.Hidden));
            Assert.IsTrue(map.Cells.All(c => !c.IsResolved));
        }
    }
}
