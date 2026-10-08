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

        [Tooltip("Event-Mischung nach Entfernung vom Start (klein / mittel / gross). Das letzte Band gilt bis zum Kartenrand.")]
        public List<DistanceBand> distanceBands = MapGenerationConfig.DefaultBands();

        [Tooltip("Frühestens ab welcher Entfernung und höchstens wie oft ein grosser Inhalt vorkommt (MaxCount 0 = unbegrenzt).")]
        public List<ContentRule> contentRules = MapGenerationConfig.DefaultRules();

        [Tooltip("Garantierte grosse Inhalte in einem Entfernungsbereich.")]
        public List<ContentQuota> contentQuotas = MapGenerationConfig.DefaultQuotas();

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

        [Tooltip("Farbe kleiner Events. Das Symbol kommt aus dem Event-Katalog.")]
        public Color minorEventColor = new Color(0.45f, 0.55f, 0.45f);

        [Tooltip("Farbe mittlerer Events (Entscheidung).")]
        public Color mediumEventColor = new Color(0.55f, 0.45f, 0.70f);

        [Tooltip("Erledigte Events werden in Richtung dieser Farbe abgedunkelt.")]
        public Color resolvedColor = new Color(0.25f, 0.27f, 0.30f);

        [Header("Bewegung & Kamera")]
        [Min(0.01f)] public float stepDuration = 0.18f;
        [Min(0.5f)] public float cameraOrthoSize = 5f;
        [Min(0.1f)] public float cameraFollowSpeed = 6f;

        public MapGenerationConfig ToGenerationConfig(int effectiveSeed)
        {
            // Kopien, damit die Generierung das Asset nie verändert.
            var bands = new List<DistanceBand>();
            foreach (DistanceBand b in distanceBands)
                bands.Add(new DistanceBand(b.MaxDistance, b.MinorWeight, b.MediumWeight, b.MajorWeight, b.MajorWeights != null ? b.MajorWeights.ToArray() : new ContentWeight[0]));

            return new MapGenerationConfig
            {
                Radius = mapRadius,
                Seed = effectiveSeed,
                Bands = bands,
                Rules = new List<ContentRule>(contentRules),
                Quotas = new List<ContentQuota>(contentQuotas),
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
