using System;
using System.Collections.Generic;
using Betaknight.Core.Hex;

namespace Betaknight.Core.Map
{
    /// <summary>
    /// Die Oberweltkarte: alle Felder plus Benachrichtigung bei Änderungen.
    /// </summary>
    public sealed class HexMap
    {
        private readonly Dictionary<HexCoord, HexCell> _cells = new Dictionary<HexCoord, HexCell>();

        public HexCoord Center { get; }
        public int Radius { get; }
        public int Seed { get; }

        public int Count => _cells.Count;
        public IEnumerable<HexCell> Cells => _cells.Values;

        /// <summary>Wird ausgelöst, wenn sich Inhalt, Sichtbarkeit oder Begehbarkeit eines Feldes ändert.</summary>
        public event Action<HexCell> CellChanged;

        public HexMap(HexCoord center, int radius, int seed = 0)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            Center = center;
            Radius = radius;
            Seed = seed;
        }

        public void AddCell(HexCell cell)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));
            if (_cells.ContainsKey(cell.Coord))
                throw new InvalidOperationException($"Feld {cell.Coord} existiert bereits.");
            _cells.Add(cell.Coord, cell);
        }

        public bool Contains(HexCoord coord) => _cells.ContainsKey(coord);

        public bool TryGetCell(HexCoord coord, out HexCell cell) => _cells.TryGetValue(coord, out cell);

        public HexCell GetCell(HexCoord coord)
        {
            if (!_cells.TryGetValue(coord, out HexCell cell))
                throw new KeyNotFoundException($"Kein Feld bei {coord}.");
            return cell;
        }

        public IEnumerable<HexCell> GetNeighbors(HexCoord coord)
        {
            foreach (HexCoord n in coord.Neighbors())
            {
                if (_cells.TryGetValue(n, out HexCell cell)) yield return cell;
            }
        }

        public void SetVisibility(HexCoord coord, CellVisibility visibility)
        {
            HexCell cell = GetCell(coord);
            if (cell.Visibility == visibility) return;
            cell.Visibility = visibility;
            CellChanged?.Invoke(cell);
        }

        public void SetContent(HexCoord coord, CellContent content)
        {
            HexCell cell = GetCell(coord);
            if (cell.Content == content) return;
            cell.Content = content;
            CellChanged?.Invoke(cell);
        }

        public void SetWalkable(HexCoord coord, bool walkable)
        {
            HexCell cell = GetCell(coord);
            if (cell.IsWalkable == walkable) return;
            cell.IsWalkable = walkable;
            CellChanged?.Invoke(cell);
        }

        internal void RegisterVisit(HexCell cell)
        {
            cell.VisitCount++;
        }
    }
}
