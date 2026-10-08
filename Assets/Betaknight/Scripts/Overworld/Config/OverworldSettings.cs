using System;
using System.Collections.Generic;
using Betaknight.Core.Map;
using UnityEngine;

namespace Betaknight.Overworld.Config
{
    /// <summary>
    /// Alle Stellschrauben der Oberwelt an einem Ort.
    /// Anlegen über Rechtsklick im Project-Fenster: Create → Betaknight → Overworld Settings.
    /// Ohne Asset verwendet der Bootstrapper diese Standardwerte.
    /// </summary>
    [CreateAssetMenu(fileName = "OverworldSettings", menuName = "Betaknight/Overworld Settings")]
    public sealed class OverworldSettings : ScriptableObject
    {
        [Serializable]
        public struct WeightEntry
        {
            public CellContent content;
            [Min(0)] public int weight;
        }

        [Serializable]
        public struct QuotaEntry
        {
            public CellContent content;
            [Min(0)] public int count;
        }

        [Serializable]
        public struct ContentStyle
        {
            public CellContent content;
            public Color color;
            public string label;
        }

        [Header("Kartengenerierung")]
        [Min(1)] public int mapRadius = 6;

        [Tooltip("0 = bei jedem Start ein neuer Zufalls-Seed.")]
        public int seed = 0;

        [Min(0)] public int safeZoneRadius = 1;

        public WeightEntry[] contentWeights =
        {
            new WeightEntry { content = CellContent.Empty, weight = 45 },
            new WeightEntry { content = CellContent.Enemy, weight = 30 },
            new WeightEntry { content = CellContent.Treasure, weight = 12 },
            new WeightEntry { content = CellContent.Shop, weight = 5 },
            new WeightEntry { content = CellContent.GoldMine, weight = 8 },
        };

        public QuotaEntry[] contentQuotas =
        {
            new QuotaEntry { content = CellContent.Shop, count = 2 },
            new QuotaEntry { content = CellContent.GoldMine, count = 2 },
        };

        [Header("Erkundung")]
        [Min(0)] public int sightRadius = 1;

        [Header("Darstellung")]
        [Min(0.05f)] public float hexSize = 0.6f;

        [Range(0f, 0.3f)] public float hexGap = 0.06f;

        [Tooltip("Verborgene Felder dunkel anzeigen, damit die Kartengrenze erkennbar ist.")]
        public bool showHiddenCells = true;

        public Color hiddenColor = new Color(0.10f, 0.11f, 0.14f);
        public Color unexploredColor = new Color(0.30f, 0.33f, 0.40f);
        public Color unexploredLabelColor = new Color(0.85f, 0.88f, 0.95f);
        public Color highlightColor = new Color(1.00f, 0.85f, 0.35f);
        public Color blockedHighlightColor = new Color(0.85f, 0.25f, 0.25f);
        public Color backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        public Color playerColor = new Color(0.40f, 0.85f, 1.00f);

        public ContentStyle[] contentStyles =
        {
            new ContentStyle { content = CellContent.Empty, color = new Color(0.45f, 0.52f, 0.45f), label = "" },
            new ContentStyle { content = CellContent.Enemy, color = new Color(0.70f, 0.30f, 0.28f), label = "!" },
            new ContentStyle { content = CellContent.Boss, color = new Color(0.50f, 0.10f, 0.35f), label = "B" },
            new ContentStyle { content = CellContent.Shop, color = new Color(0.30f, 0.50f, 0.75f), label = "$" },
            new ContentStyle { content = CellContent.Treasure, color = new Color(0.75f, 0.60f, 0.25f), label = "*" },
            new ContentStyle { content = CellContent.GoldMine, color = new Color(0.85f, 0.72f, 0.20f), label = "G" },
        };

        [Header("Bewegung & Kamera")]
        [Min(0.01f)] public float stepDuration = 0.18f;
        [Min(0.5f)] public float cameraOrthoSize = 5f;
        [Min(0.1f)] public float cameraFollowSpeed = 6f;

        public MapGenerationConfig ToGenerationConfig(int effectiveSeed)
        {
            var weights = new List<ContentWeight>();
            foreach (WeightEntry w in contentWeights)
                weights.Add(new ContentWeight(w.content, w.weight));

            var quotas = new List<ContentQuota>();
            foreach (QuotaEntry q in contentQuotas)
                quotas.Add(new ContentQuota(q.content, q.count));

            return new MapGenerationConfig
            {
                Radius = mapRadius,
                Seed = effectiveSeed,
                SafeZoneRadius = Mathf.Clamp(safeZoneRadius, 0, mapRadius - 1),
                Weights = weights,
                Quotas = quotas,
            };
        }

        public ContentStyle GetStyle(CellContent content)
        {
            foreach (ContentStyle style in contentStyles)
            {
                if (style.content == content) return style;
            }
            return new ContentStyle { content = content, color = Color.magenta, label = "?" };
        }
    }
}
