using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Circuit;

namespace Betaknight.Core.Skills
{
    /// <summary>
    /// Skill-Inventar als Raster wie die Platine: jede freie Komponente liegt in ihrer echten Form darin. Grosse zuerst, dann
    /// in Lesereihenfolge an die erste freie Stelle; zu breite Formen werden gedreht. Das Raster wächst nach unten.
    /// </summary>
    public static class SkillInventoryLayout
    {
        public const int Columns = 6;
        public const int MinRows = 6;

        public readonly struct Placement
        {
            public readonly int Id;
            public readonly CellRect Rect;
            public readonly bool Rotated;

            public Placement(int id, CellRect rect, bool rotated)
            {
                Id = id;
                Rect = rect;
                Rotated = rotated;
            }
        }

        /// <summary>Legt die Formen ins Raster. <paramref name="rows"/> = Höhe inklusive einer freien Reserve-Zeile.</summary>
        public static List<Placement> Pack(IEnumerable<(int id, Shape shape)> parts, out int rows, int columns = Columns, int minRows = MinRows)
        {
            columns = Math.Max(1, columns);
            var used = new List<bool[]>();
            var result = new List<Placement>();
            foreach ((int id, Shape shape) in (parts ?? Enumerable.Empty<(int, Shape)>())
                         .OrderByDescending(p => p.shape.Cells).ThenByDescending(p => p.shape.Height).ThenBy(p => p.id))
            {
                bool rotated = shape.Width > columns && shape.Height <= columns;
                Shape s = shape.Turned(rotated);
                int w = Math.Min(s.Width, columns), h = Math.Max(1, s.Height);
                for (int y = 0; ; y++)
                {
                    int found = -1;
                    for (int x = 0; x + w <= columns && found < 0; x++)
                        if (Fits(used, x, y, w, h)) found = x;
                    if (found < 0) continue;
                    Mark(used, found, y, w, h, columns);
                    result.Add(new Placement(id, new CellRect(found, y, w, h), rotated));
                    break;
                }
            }
            rows = Math.Max(minRows, used.Count + 1);
            return result;
        }

        private static bool Fits(List<bool[]> used, int x, int y, int w, int h)
        {
            for (int dy = 0; dy < h; dy++)
            {
                if (y + dy >= used.Count) continue;
                for (int dx = 0; dx < w; dx++)
                    if (used[y + dy][x + dx]) return false;
            }
            return true;
        }

        private static void Mark(List<bool[]> used, int x, int y, int w, int h, int columns)
        {
            while (used.Count < y + h) used.Add(new bool[columns]);
            for (int dy = 0; dy < h; dy++)
                for (int dx = 0; dx < w; dx++)
                    used[y + dy][x + dx] = true;
        }
    }
}
