using System;
using System.Collections.Generic;
using Betaknight.Core.Encounters;
using Betaknight.Core.Hex;

namespace Betaknight.Core.Map
{
    /// <summary>
    /// Erzeugt eine sechseckige Karte um <see cref="HexCoord.Zero"/>, auf der jedes Feld ausser dem Start ein Event trägt.
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

            // Deterministische Reihenfolge über die Spirale, nicht über ein Dictionary.
            var coords = new List<HexCoord>(HexCoord.Spiral(center, config.Radius));
            var assigned = new Dictionary<HexCoord, HexCell>();
            var counts = new Dictionary<CellContent, int>();

            // 1. Garantien zuerst setzen, damit Obergrenzen (MaxCount) sicher eingehalten werden.
            PlaceQuotas(config, coords, center, random, assigned, counts);

            // 2. Restliche Felder nach dem Band ihrer Entfernung befüllen.
            foreach (HexCoord coord in coords)
            {
                if (!assigned.TryGetValue(coord, out HexCell cell))
                {
                    cell = coord == center
                        ? new HexCell(coord, CellContent.Empty)
                        : RollCell(config, coord, coord.DistanceTo(center), random, counts);
                }
                map.AddCell(cell);
            }

            return map;
        }

        private static void PlaceQuotas(MapGenerationConfig config, List<HexCoord> coords, HexCoord center, Random random,
            Dictionary<HexCoord, HexCell> assigned, Dictionary<CellContent, int> counts)
        {
            if (config.Quotas == null) return;

            foreach (ContentQuota quota in config.Quotas)
            {
                var candidates = new List<HexCoord>();
                foreach (HexCoord coord in coords)
                {
                    int d = coord.DistanceTo(center);
                    if (d < quota.MinDistance || d > quota.MaxDistance || assigned.ContainsKey(coord)) continue;
                    candidates.Add(coord);
                }

                for (int i = 0; i < quota.Count && candidates.Count > 0; i++)
                {
                    int index = random.Next(candidates.Count);
                    HexCoord coord = candidates[index];
                    candidates.RemoveAt(index);

                    assigned[coord] = new HexCell(coord, quota.Content);
                    Increment(counts, quota.Content);
                }
            }
        }

        private static HexCell RollCell(MapGenerationConfig config, HexCoord coord, int distance, Random random,
            Dictionary<CellContent, int> counts)
        {
            DistanceBand band = config.BandFor(distance);
            EncounterSize size = RollSize(band, random);

            if (size == EncounterSize.Major)
            {
                CellContent major = PickMajor(config, band, distance, random, counts);
                if (major != CellContent.Empty)
                {
                    Increment(counts, major);
                    return new HexCell(coord, major);
                }
                // Kein grosser Inhalt mehr erlaubt (Obergrenzen erreicht): mittleres Event als Ersatz.
                size = EncounterSize.Medium;
            }

            EncounterDefinition encounter = PickEncounter(config.Encounters, size, distance, random)
                ?? PickEncounter(config.Encounters, EncounterSize.Minor, distance, random);

            return encounter != null
                ? new HexCell(coord, CellContent.Encounter, encounter.Id)
                : new HexCell(coord, CellContent.Empty);
        }

        private static EncounterSize RollSize(DistanceBand band, Random random)
        {
            int roll = random.Next(band.MinorWeight + band.MediumWeight + band.MajorWeight);
            if (roll < band.MinorWeight) return EncounterSize.Minor;
            if (roll < band.MinorWeight + band.MediumWeight) return EncounterSize.Medium;
            return EncounterSize.Major;
        }

        private static CellContent PickMajor(MapGenerationConfig config, DistanceBand band, int distance, Random random,
            Dictionary<CellContent, int> counts)
        {
            if (band.MajorWeights == null) return CellContent.Empty;

            bool Allowed(CellContent content)
            {
                if (!config.TryGetRule(content, out ContentRule rule)) return true;
                if (distance < rule.MinDistance) return false;
                counts.TryGetValue(content, out int count);
                return rule.MaxCount <= 0 || count < rule.MaxCount;
            }

            int total = 0;
            foreach (ContentWeight w in band.MajorWeights)
            {
                if (Allowed(w.Content)) total += w.Weight;
            }
            if (total <= 0) return CellContent.Empty;

            int roll = random.Next(total);
            foreach (ContentWeight w in band.MajorWeights)
            {
                if (!Allowed(w.Content)) continue;
                if (roll < w.Weight) return w.Content;
                roll -= w.Weight;
            }
            return CellContent.Empty;
        }

        private static EncounterDefinition PickEncounter(EncounterCatalog catalog, EncounterSize size, int distance, Random random)
        {
            int total = 0;
            foreach (EncounterDefinition d in catalog.All)
            {
                if (d.Size == size && distance >= d.MinDistance) total += d.Weight;
            }
            if (total <= 0) return null;

            int roll = random.Next(total);
            foreach (EncounterDefinition d in catalog.All)
            {
                if (d.Size != size || distance < d.MinDistance) continue;
                if (roll < d.Weight) return d;
                roll -= d.Weight;
            }
            return null;
        }

        private static void Increment(Dictionary<CellContent, int> counts, CellContent content)
        {
            counts.TryGetValue(content, out int count);
            counts[content] = count + 1;
        }
    }
}
