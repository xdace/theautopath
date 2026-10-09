using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Circuit;
using Betaknight.Core.Skills;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Skill-Inventar als Raster: Formen liegen ohne Überlappung darin, grosse zuerst, das Raster wächst nach unten.</summary>
    public class SkillInventoryLayoutTests
    {
        [Test]
        public void PartsArePackedWithoutOverlapAndBigOnesFirst()
        {
            var parts = new List<(int, Shape)>
            {
                (1, new Shape(1, 1)), (2, new Shape(2, 2)), (3, new Shape(1, 2)), (4, new Shape(2, 3)), (5, new Shape(2, 1)),
            };
            List<SkillInventoryLayout.Placement> placed = SkillInventoryLayout.Pack(parts, out int rows);

            Assert.AreEqual(5, placed.Count);
            Assert.AreEqual(4, placed[0].Id, "die grösste Form zuerst, oben links");
            Assert.AreEqual(new Cell(0, 0), placed[0].Rect.Origin);
            var cells = new HashSet<Cell>();
            foreach (SkillInventoryLayout.Placement p in placed)
                for (int y = p.Rect.Top; y < p.Rect.Top + p.Rect.Shape.Height; y++)
                    for (int x = p.Rect.Left; x < p.Rect.Left + p.Rect.Shape.Width; x++)
                    {
                        Assert.IsTrue(cells.Add(new Cell(x, y)), $"Überlappung bei {x},{y}");
                        Assert.Less(x, SkillInventoryLayout.Columns);
                    }
            Assert.AreEqual(SkillInventoryLayout.MinRows, rows, "wenige Teile: Mindesthöhe");
        }

        [Test]
        public void TheGridGrowsDownwardWithASpareRow()
        {
            List<(int, Shape)> parts = Enumerable.Range(0, 20).Select(i => (i, new Shape(2, 2))).ToList();
            List<SkillInventoryLayout.Placement> placed = SkillInventoryLayout.Pack(parts, out int rows);
            Assert.AreEqual(20, placed.Count);
            int bottom = placed.Max(p => p.Rect.Top + p.Rect.Shape.Height);
            Assert.AreEqual(bottom + 1, rows);
        }

        [Test]
        public void ShapesWiderThanTheGridAreTurned()
        {
            List<SkillInventoryLayout.Placement> placed = SkillInventoryLayout.Pack(new[] { (7, new Shape(8, 1)) }, out _, columns: 6);
            Assert.IsTrue(placed[0].Rotated);
            Assert.AreEqual(new Shape(1, 8), placed[0].Rect.Shape);
        }
    }
}
