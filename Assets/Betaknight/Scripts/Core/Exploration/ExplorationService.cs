using System;
using System.Collections.Generic;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;

namespace Betaknight.Core.Exploration
{
    /// <summary>
    /// Fog of War: Betretene Felder werden aufgedeckt (Explored),
    /// Felder in Sichtweite werden als "?" (Unexplored) sichtbar.
    /// </summary>
    public sealed class ExplorationService
    {
        private readonly HexMap _map;

        /// <summary>Sichtweite in Feldern. 1 = nur direkte Nachbarn (laut Konzept).</summary>
        public int SightRadius { get; }

        public ExplorationService(HexMap map, int sightRadius = 1)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            if (sightRadius < 0) throw new ArgumentOutOfRangeException(nameof(sightRadius));
            SightRadius = sightRadius;
        }

        /// <summary>
        /// Deckt das Feld bei <paramref name="center"/> auf und macht die Umgebung als "?" sichtbar.
        /// Gibt alle Felder zurück, deren Sichtbarkeit sich geändert hat.
        /// </summary>
        public IReadOnlyList<HexCell> RevealAround(HexCoord center)
        {
            var changed = new List<HexCell>();

            if (_map.TryGetCell(center, out HexCell centerCell) && centerCell.Visibility != CellVisibility.Explored)
            {
                _map.SetVisibility(center, CellVisibility.Explored);
                changed.Add(centerCell);
            }

            for (int k = 1; k <= SightRadius; k++)
            {
                foreach (HexCoord coord in HexCoord.Ring(center, k))
                {
                    if (!_map.TryGetCell(coord, out HexCell cell)) continue;
                    if (cell.Visibility != CellVisibility.Hidden) continue;

                    _map.SetVisibility(coord, CellVisibility.Unexplored);
                    changed.Add(cell);
                }
            }

            return changed;
        }
    }
}
