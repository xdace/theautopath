using System;
using System.Collections.Generic;
using Betaknight.Core.Hex;

namespace Betaknight.Core.Map
{
    /// <summary>
    /// Erzeugt eine sechseckige Karte um <see cref="HexCoord.Zero"/>.
    /// Gleicher Seed + gleiche Konfiguration = identische Karte (wichtig für Debugging und spätere Seeds-Runs).
    /// </summary>
    public static class MapGenerator
    {
        public static HexMap Generate(MapGenerationConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Validate();

            var random = new Random(config.Seed);
            HexCoord center = HexCoord.Zero;
            var map = new HexMap(center, config.Radius, config.Seed);

            // 1. Alle Felder per Gewichtung befüllen.
            foreach (HexCoord coord in HexCoord.Spiral(center, config.Radius))
            {
                CellContent content;
                if (coord == center)
                {
                    content = CellContent.Empty;
                }
                else if (coord.DistanceTo(center) <= config.SafeZoneRadius)
                {
                    content = PickWeighted(random, config.Weights, MapGenerationConfig.IsAllowedInSafeZone);
                }
                else
                {
                    content = PickWeighted(random, config.Weights, null);
                }

                map.AddCell(new HexCell(coord, content));
            }

            // 2. Mindestanzahlen garantieren, indem leere Felder ausserhalb der Sicherheitszone umgewidmet werden.
            ApplyQuotas(map, config, random);

            return map;
        }

        private static CellContent PickWeighted(Random random, List<ContentWeight> weights, Func<CellContent, bool> filter)
        {
            int total = 0;
            foreach (ContentWeight w in weights)
            {
                if (filter == null || filter(w.Content)) total += w.Weight;
            }

            if (total <= 0) return CellContent.Empty;

            int roll = random.Next(total);
            foreach (ContentWeight w in weights)
            {
                if (filter != null && !filter(w.Content)) continue;
                if (roll < w.Weight) return w.Content;
                roll -= w.Weight;
            }

            return CellContent.Empty;
        }

        private static void ApplyQuotas(HexMap map, MapGenerationConfig config, Random random)
        {
            if (config.Quotas == null) return;

            foreach (ContentQuota quota in config.Quotas)
            {
                int existing = 0;
                var candidates = new List<HexCell>();

                // Deterministische Reihenfolge über die Spirale, nicht über das Dictionary.
                foreach (HexCoord coord in HexCoord.Spiral(map.Center, map.Radius))
                {
                    HexCell cell = map.GetCell(coord);
                    if (cell.Content == quota.Content) existing++;
                    else if (cell.Content == CellContent.Empty && coord.DistanceTo(map.Center) > config.SafeZoneRadius)
                        candidates.Add(cell);
                }

                int missing = quota.Count - existing;
                while (missing > 0 && candidates.Count > 0)
                {
                    int index = random.Next(candidates.Count);
                    candidates[index].Content = quota.Content;
                    candidates.RemoveAt(index);
                    missing--;
                }
            }
        }
    }
}
