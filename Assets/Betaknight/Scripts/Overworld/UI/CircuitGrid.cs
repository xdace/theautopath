using Betaknight.Core.Circuit;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Zeichen-Hilfen für die Platine (A-19), gemeinsam für Build-Fenster und Arena: Zellgrösse, Rechteck einer Zelle oder
    /// Form, Zelle unter der Maus, Hintergrund-Raster, Kern und Chips. Gerechnet wird nichts; die Fenster entscheiden, was wo liegt.
    /// </summary>
    public static class CircuitGrid
    {
        public static readonly Color GridLine = new Color(0.24f, 0.27f, 0.33f, 1f);
        public static readonly Color FreeCell = new Color(0.10f, 0.115f, 0.145f, 1f);
        public static readonly Color CoreColor = new Color(0.55f, 0.40f, 0.95f, 1f);
        public static readonly Color RelayColor = new Color(0.12f, 0.36f, 0.40f, 1f);
        public static readonly Color RelayLit = new Color(0.45f, 0.95f, 1.00f, 1f);
        public static readonly Color ComponentColor = new Color(0.17f, 0.20f, 0.26f, 1f);
        public static readonly Color PoweredBorder = new Color(0.49f, 0.86f, 0.44f, 1f);
        public static readonly Color TooLargeBorder = new Color(1f, 0.62f, 0.22f, 1f);
        public static readonly Color UnpoweredBorder = new Color(1f, 0.42f, 0.38f, 1f);
        public static readonly Color TraceColor = new Color(0.45f, 0.95f, 1.00f, 0.55f);

        private const float Gap = 3f;

        private static GUIStyle _label;
        private static GUIStyle _tiny;

        /// <summary>Kleiner, zentrierter, umbrechender Text für Chips.</summary>
        public static GUIStyle Label
        {
            get
            {
                if (_label == null)
                {
                    _label = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = UiTheme.SmallSize, richText = true, wordWrap = true, alignment = TextAnchor.MiddleCenter,
                        clipping = TextClipping.Clip, padding = new RectOffset(2, 2, 1, 1), margin = new RectOffset(0, 0, 0, 0),
                    };
                    _label.normal.textColor = UiTheme.TextColor;
                }
                return _label;
            }
        }

        /// <summary>Sehr kleiner Text (Arena, kleine Zellen).</summary>
        public static GUIStyle Tiny
        {
            get
            {
                if (_tiny == null)
                {
                    _tiny = new GUIStyle(Label) { fontSize = 11 };
                }
                return _tiny;
            }
        }

        /// <summary>Grösste Zellgrösse, mit der ein <paramref name="width"/>×<paramref name="height"/>-Raster in den Platz passt.</summary>
        public static float CellSize(int width, int height, float maxWidth, float maxHeight, float max = 92f, float min = 26f) =>
            Mathf.Clamp(Mathf.Floor(Mathf.Min(maxWidth / Mathf.Max(1, width), maxHeight / Mathf.Max(1, height))), min, max);

        /// <summary>Bildschirm-Rechteck einer Form an einer Zelle (mit kleinem Abstand zum Raster).</summary>
        public static Rect RectOf(Rect grid, float size, int x, int y, int w = 1, int h = 1) =>
            new Rect(grid.x + x * size + Gap, grid.y + y * size + Gap, w * size - Gap * 2f, h * size - Gap * 2f);

        public static Rect RectOf(Rect grid, float size, CellRect rect) =>
            RectOf(grid, size, rect.Left, rect.Top, rect.Shape.Width, rect.Shape.Height);

        /// <summary>Volle Zelle ohne Abstand (Ziele für Drag &amp; Drop).</summary>
        public static Rect SlotOf(Rect grid, float size, int x, int y) => new Rect(grid.x + x * size, grid.y + y * size, size, size);

        /// <summary>Zelle unter einem Punkt, false ausserhalb des Rasters.</summary>
        public static bool CellAt(Rect grid, float size, int width, int height, Vector2 point, out Cell cell)
        {
            int x = Mathf.FloorToInt((point.x - grid.x) / size);
            int y = Mathf.FloorToInt((point.y - grid.y) / size);
            cell = new Cell(x, y);
            return point.x >= grid.x && point.y >= grid.y && x >= 0 && y >= 0 && x < width && y < height;
        }

        /// <summary>Hintergrund: Rahmen und freie Zellen.</summary>
        public static void DrawBackground(Rect grid, float size, int width, int height)
        {
            UiTheme.Fill(grid, GridLine);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    UiTheme.Fill(new Rect(grid.x + x * size + 1f, grid.y + y * size + 1f, size - 2f, size - 2f), FreeCell);
        }

        /// <summary>Der Kern: violettes Feld mit Beschriftung und Tooltip.</summary>
        public static void DrawCore(Rect rect, string label, string tooltip, GUIStyle style = null)
        {
            UiTheme.Fill(rect, new Color(CoreColor.r * 0.45f, CoreColor.g * 0.45f, CoreColor.b * 0.45f, 1f));
            UiTheme.Outline(rect, CoreColor, 2f);
            GUI.Label(rect, new GUIContent($"<b>{label}</b>", tooltip), style ?? Label);
        }

        /// <summary>Ein Chip (Relais oder Komponente): Fläche, Rahmen und Text mit Tooltip.</summary>
        public static void DrawChip(Rect rect, Color fill, Color border, float thickness, string text, string tooltip, GUIStyle style = null)
        {
            UiTheme.Fill(rect, fill);
            if (thickness > 0f) UiTheme.Outline(rect, border, thickness);
            GUI.Label(rect, new GUIContent(text, tooltip), style ?? Label);
        }

        /// <summary>Leiterbahn zwischen zwei berührenden Teilen: kurzer Balken über die gemeinsame Kante.</summary>
        public static void DrawTrace(Rect a, Rect b, Color color)
        {
            float thickness = 4f;
            if (a.xMax <= b.xMin + 0.5f || b.xMax <= a.xMin + 0.5f)
            {
                // nebeneinander
                float top = Mathf.Max(a.yMin, b.yMin), bottom = Mathf.Min(a.yMax, b.yMax);
                if (bottom <= top) return;
                float x = a.xMax <= b.xMin + 0.5f ? a.xMax : b.xMax;
                float y = (top + bottom) * 0.5f;
                UiTheme.Fill(new Rect(x - 4f, y - thickness * 0.5f, 2f * Gap + 8f, thickness), color);
            }
            else
            {
                // übereinander
                float left = Mathf.Max(a.xMin, b.xMin), right = Mathf.Min(a.xMax, b.xMax);
                if (right <= left) return;
                float y = a.yMax <= b.yMin + 0.5f ? a.yMax : b.yMax;
                float x = (left + right) * 0.5f;
                UiTheme.Fill(new Rect(x - thickness * 0.5f, y - 4f, thickness, 2f * Gap + 8f), color);
            }
        }
    }
}
