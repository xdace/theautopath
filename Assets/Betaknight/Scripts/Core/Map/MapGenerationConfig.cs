using System;
using System.Collections.Generic;
using Betaknight.Core.Encounters;

namespace Betaknight.Core.Map
{
    /// <summary>Gewichtung eines Feldinhalts bei der Zufallsverteilung.</summary>
    [Serializable]
    public struct ContentWeight
    {
        public CellContent Content;
        public int Weight;

        public ContentWeight(CellContent content, int weight)
        {
            Content = content;
            Weight = weight;
        }
    }

    /// <summary>
    /// Garantierte Anzahl eines grossen Inhalts in einem Entfernungsbereich vom Start.
    /// Garantien werden vor der Zufallsverteilung gesetzt.
    /// </summary>
    [Serializable]
    public struct ContentQuota
    {
        public CellContent Content;
        public int Count;
        public int MinDistance;
        public int MaxDistance;

        public ContentQuota(CellContent content, int count, int minDistance = 1, int maxDistance = int.MaxValue)
        {
            Content = content;
            Count = count;
            MinDistance = minDistance;
            MaxDistance = maxDistance;
        }
    }

    /// <summary>Grenzen für einen grossen Inhalt: frühestens ab welcher Entfernung und höchstens wie oft.</summary>
    [Serializable]
    public struct ContentRule
    {
        public CellContent Content;
        public int MinDistance;

        /// <summary>0 = unbegrenzt.</summary>
        public int MaxCount;

        public ContentRule(CellContent content, int minDistance, int maxCount = 0)
        {
            Content = content;
            MinDistance = minDistance;
            MaxCount = maxCount;
        }
    }

    /// <summary>
    /// Event-Mischung für einen Ring um den Start. Gilt für alle Felder bis <see cref="MaxDistance"/>,
    /// die nicht schon von einem näheren Band abgedeckt sind. Das letzte Band gilt bis zum Kartenrand.
    /// </summary>
    [Serializable]
    public class DistanceBand
    {
        public int MaxDistance;
        public int MinorWeight;
        public int MediumWeight;
        public int MajorWeight;

        /// <summary>Welche grossen Inhalte in diesem Band vorkommen und wie häufig.</summary>
        public List<ContentWeight> MajorWeights = new List<ContentWeight>();

        public DistanceBand() { }

        public DistanceBand(int maxDistance, int minor, int medium, int major, params ContentWeight[] majorWeights)
        {
            MaxDistance = maxDistance;
            MinorWeight = minor;
            MediumWeight = medium;
            MajorWeight = major;
            MajorWeights = new List<ContentWeight>(majorWeights);
        }
    }

    /// <summary>Parameter für <see cref="MapGenerator"/>. Unity-frei, wird aus den OverworldSettings befüllt.</summary>
    public sealed class MapGenerationConfig
    {
        /// <summary>Kartenradius in Feldern um die Mitte. Radius 6 ergibt 127 Felder.</summary>
        public int Radius = 6;

        public int Seed = 12345;

        /// <summary>Event-Mischung nach Entfernung, aufsteigend nach MaxDistance.</summary>
        public List<DistanceBand> Bands = DefaultBands();

        public List<ContentRule> Rules = DefaultRules();

        public List<ContentQuota> Quotas = DefaultQuotas();

        /// <summary>Quelle für kleine und mittlere Events.</summary>
        public EncounterCatalog Encounters = EncounterCatalog.CreateDefault();

        /// <summary>Gleiche Einstellungen mit anderem Seed, z. B. für die Karte des nächsten Akts.</summary>
        public MapGenerationConfig WithSeed(int seed)
        {
            var copy = (MapGenerationConfig)MemberwiseClone();
            copy.Seed = seed;
            return copy;
        }

        /// <summary>
        /// Ring 1 nur kleine Events, danach wachsen mittlere und grosse Events.
        /// Truhen und Shops sind anfangs selten (siehe <see cref="DefaultRules"/>).
        /// </summary>
        public static List<DistanceBand> DefaultBands() => new List<DistanceBand>
        {
            new DistanceBand(1, 100, 0, 0),
            new DistanceBand(2, 60, 30, 10,
                new ContentWeight(CellContent.Enemy, 85),
                new ContentWeight(CellContent.Treasure, 15)),
            new DistanceBand(3, 40, 35, 25,
                new ContentWeight(CellContent.Enemy, 60),
                new ContentWeight(CellContent.Treasure, 12),
                new ContentWeight(CellContent.GoldMine, 15),
                new ContentWeight(CellContent.Shop, 13)),
            new DistanceBand(int.MaxValue, 25, 35, 40,
                new ContentWeight(CellContent.Enemy, 65),
                new ContentWeight(CellContent.Treasure, 12),
                new ContentWeight(CellContent.GoldMine, 13),
                new ContentWeight(CellContent.Shop, 10)),
        };

        public static List<ContentRule> DefaultRules() => new List<ContentRule>
        {
            new ContentRule(CellContent.Enemy, 2),
            new ContentRule(CellContent.Treasure, 2, 6),
            new ContentRule(CellContent.Shop, 3, 2),
            new ContentRule(CellContent.GoldMine, 3, 4),
        };

        public static List<ContentQuota> DefaultQuotas() => new List<ContentQuota>
        {
            new ContentQuota(CellContent.Enemy, 2, 2, 2),
            new ContentQuota(CellContent.Treasure, 1, 2, 3),
            new ContentQuota(CellContent.Shop, 1, 3, 4),
            new ContentQuota(CellContent.Shop, 1, 5, 6),
            new ContentQuota(CellContent.GoldMine, 2, 3, 6),
        };

        public DistanceBand BandFor(int distance)
        {
            foreach (DistanceBand band in Bands)
            {
                if (distance <= band.MaxDistance) return band;
            }
            return Bands[Bands.Count - 1];
        }

        public bool TryGetRule(CellContent content, out ContentRule rule)
        {
            if (Rules != null)
            {
                foreach (ContentRule r in Rules)
                {
                    if (r.Content == content)
                    {
                        rule = r;
                        return true;
                    }
                }
            }
            rule = default;
            return false;
        }

        public void Validate()
        {
            if (Radius < 1) throw new ArgumentException("Radius muss mindestens 1 sein.");
            if (Bands == null || Bands.Count == 0) throw new ArgumentException("Es braucht mindestens ein DistanceBand.");
            if (Encounters == null) throw new ArgumentException("Encounters darf nicht null sein.");

            int lastMax = 0;
            foreach (DistanceBand band in Bands)
            {
                if (band == null) throw new ArgumentException("DistanceBand darf nicht null sein.");
                if (band.MaxDistance < lastMax) throw new ArgumentException("Bands müssen nach MaxDistance aufsteigend sortiert sein.");
                lastMax = band.MaxDistance;

                if (band.MinorWeight < 0 || band.MediumWeight < 0 || band.MajorWeight < 0)
                    throw new ArgumentException("Negative Grössen-Gewichte sind nicht erlaubt.");
                if (band.MinorWeight + band.MediumWeight + band.MajorWeight <= 0)
                    throw new ArgumentException($"Band bis {band.MaxDistance} hat keine Gewichte.");

                if (band.MajorWeights == null) continue;
                foreach (ContentWeight w in band.MajorWeights)
                {
                    if (w.Weight < 0) throw new ArgumentException($"Negatives Gewicht für {w.Content}.");
                    if (w.Content == CellContent.Boss)
                        throw new ArgumentException("Bosse werden zeitgesteuert gespawnt, nicht bei der Kartengenerierung.");
                    if (!w.Content.IsMajor())
                        throw new ArgumentException($"{w.Content} ist kein grosser Inhalt.");
                }
            }

            if (Quotas == null) return;
            var quotaTotals = new Dictionary<CellContent, int>();
            foreach (ContentQuota q in Quotas)
            {
                if (!q.Content.IsMajor()) throw new ArgumentException($"Garantien gibt es nur für grosse Inhalte, nicht für {q.Content}.");
                if (q.Count < 0 || q.MinDistance < 1 || q.MaxDistance < q.MinDistance)
                    throw new ArgumentException($"Ungültige Garantie für {q.Content}.");
                if (TryGetRule(q.Content, out ContentRule qRule) && q.MinDistance < qRule.MinDistance)
                    throw new ArgumentException($"Garantie für {q.Content} liegt näher als dessen MinDistance {qRule.MinDistance}.");
                quotaTotals.TryGetValue(q.Content, out int sum);
                quotaTotals[q.Content] = sum + q.Count;
            }

            foreach (KeyValuePair<CellContent, int> total in quotaTotals)
            {
                if (TryGetRule(total.Key, out ContentRule rule) && rule.MaxCount > 0 && total.Value > rule.MaxCount)
                    throw new ArgumentException($"Garantien für {total.Key} überschreiten MaxCount {rule.MaxCount}.");
            }
        }
    }
}
