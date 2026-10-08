using System;
using System.Collections.Generic;

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

    /// <summary>Mindestanzahl eines Feldinhalts, die garantiert auf der Karte liegt.</summary>
    [Serializable]
    public struct ContentQuota
    {
        public CellContent Content;
        public int Count;

        public ContentQuota(CellContent content, int count)
        {
            Content = content;
            Count = count;
        }
    }

    /// <summary>Parameter für <see cref="MapGenerator"/>. Unity-frei, wird aus den OverworldSettings befüllt.</summary>
    public sealed class MapGenerationConfig
    {
        /// <summary>Kartenradius in Feldern um die Mitte. Radius 6 ergibt 127 Felder.</summary>
        public int Radius = 6;

        public int Seed = 12345;

        /// <summary>Felder bis zu diesem Abstand vom Start enthalten keine Gegner, Bosse oder Goldminen.</summary>
        public int SafeZoneRadius = 1;

        public List<ContentWeight> Weights = DefaultWeights();

        public List<ContentQuota> Quotas = new List<ContentQuota>
        {
            new ContentQuota(CellContent.Shop, 2),
            new ContentQuota(CellContent.GoldMine, 2),
        };

        public static List<ContentWeight> DefaultWeights() => new List<ContentWeight>
        {
            new ContentWeight(CellContent.Empty, 45),
            new ContentWeight(CellContent.Enemy, 30),
            new ContentWeight(CellContent.Treasure, 12),
            new ContentWeight(CellContent.Shop, 5),
            new ContentWeight(CellContent.GoldMine, 8),
        };

        /// <summary>Inhalte, die in der Sicherheitszone um den Start erlaubt sind.</summary>
        public static bool IsAllowedInSafeZone(CellContent content) =>
            content == CellContent.Empty || content == CellContent.Treasure || content == CellContent.Shop;

        public void Validate()
        {
            if (Radius < 1) throw new ArgumentException("Radius muss mindestens 1 sein.");
            if (SafeZoneRadius < 0 || SafeZoneRadius >= Radius)
                throw new ArgumentException("SafeZoneRadius muss zwischen 0 und Radius - 1 liegen.");
            if (Weights == null || Weights.Count == 0) throw new ArgumentException("Weights darf nicht leer sein.");

            int total = 0;
            foreach (ContentWeight w in Weights)
            {
                if (w.Weight < 0) throw new ArgumentException($"Negatives Gewicht für {w.Content}.");
                if (w.Content == CellContent.Boss)
                    throw new ArgumentException("Bosse werden zeitgesteuert gespawnt, nicht bei der Kartengenerierung.");
                total += w.Weight;
            }
            if (total <= 0) throw new ArgumentException("Die Summe der Gewichte muss grösser als 0 sein.");
        }
    }
}
