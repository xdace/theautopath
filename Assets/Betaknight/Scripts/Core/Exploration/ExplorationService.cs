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

        /// <summary>
        /// Kundschaftet ein Feld aus: Verborgene Felder werden als "?" sichtbar und ihr Inhalt wird angezeigt.
        /// Erforschte Felder bleiben unverändert. Gibt true zurück, wenn sich etwas geändert hat.
        /// </summary>
        public bool Scout(HexCoord coord)
        {
            if (!_map.TryGetCell(coord, out HexCell cell)) return false;
            if (cell.Visibility == CellVisibility.Explored || cell.IsScouted) return false;

            if (cell.Visibility == CellVisibility.Hidden) _map.SetVisibility(coord, CellVisibility.Unexplored);
            _map.SetScouted(coord);
            return true;
        }

        /// <summary>Kundschaftet alle Felder im Umkreis aus. Gibt die Anzahl neu bekannter Felder zurück.</summary>
        public int ScoutAround(HexCoord center, int radius)
        {
            int count = 0;
            foreach (HexCoord coord in HexCoord.Spiral(center, radius))
            {
                if (Scout(coord)) count++;
            }
            return count;
        }
    }
}
