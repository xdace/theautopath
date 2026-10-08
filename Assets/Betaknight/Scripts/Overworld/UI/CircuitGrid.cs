using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Zeichen-Hilfen für die Platine (A-19), gemeinsam für Build-Fenster und Arena: Zellgrösse, Rechteck einer Zelle oder
    /// Form, Zelle unter der Maus, Hintergrund-Raster, Kern und Chips. A-20: Pins als Kerben, Logik-Chips (Leiterbahnen, Diode,
    /// Gatter, Kondensator, Sicherung), Pulsverbindungen und Pulse. Gerechnet wird nichts; die Fenster entscheiden, was wo liegt.
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

        // A-20: Pins, Logik-Chips, Pulse.
        public static readonly Color PinColor = new Color(0.62f, 0.66f, 0.74f, 1f);
        public static readonly Color PinLinked = new Color(1.00f, 0.84f, 0.37f, 1f);
        public static readonly Color ChipColor = new Color(0.075f, 0.12f, 0.11f, 1f);
        public static readonly Color ChipTrace = new Color(0.42f, 0.80f, 0.55f, 1f);
        public static readonly Color GateColor = new Color(0.20f, 0.16f, 0.30f, 1f);
        public static readonly Color GateBorder = new Color(0.70f, 0.55f, 1.00f, 1f);
        public static readonly Color GateOpen = new Color(0.49f, 0.86f, 0.44f, 1f);
        public static readonly Color BlownColor = new Color(0.30f, 0.30f, 0.33f, 1f);
        public static readonly Color LinkColor = new Color(1.00f, 0.84f, 0.37f, 0.60f);
        public static readonly Color PulseColor = new Color(1.00f, 0.95f, 0.55f, 1f);

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

        // ------------------------------------------------------------------ A-20: Pins

        /// <summary>Farbe eines typisierten Pins nach Skill-Art (untypisiert grau).</summary>
        public static Color KindColor(SkillKind kind)
        {
            switch (kind)
            {
                case SkillKind.Attack: return new Color(1.00f, 0.45f, 0.40f, 1f);
                case SkillKind.Shield: return new Color(0.45f, 0.70f, 1.00f, 1f);
                case SkillKind.Fire: return new Color(1.00f, 0.60f, 0.20f, 1f);
                case SkillKind.Shock: return new Color(1.00f, 0.92f, 0.30f, 1f);
                case SkillKind.Healing: return new Color(0.45f, 0.95f, 0.50f, 1f);
                case SkillKind.Movement: return new Color(0.85f, 0.55f, 1.00f, 1f);
                default: return PinColor;
            }
        }

        /// <summary>Komponente (Zeile) der kompilierten Platine, die die Zelle belegt, oder null.</summary>
        public static LogicRow RowAt(LogicBoard board, Cell cell)
        {
            if (board == null) return null;
            foreach (LogicRow row in board.Rows)
                if (row.Rect.HasValue && row.Rect.Value.Contains(cell)) return row;
            return null;
        }

        /// <summary>Typisierter Pin mit passendem Nachbarn (gibt den Pin-Bonus), wie in <see cref="CircuitWiring.MatchedTypedPins"/>.</summary>
        public static bool IsPinMatched(LogicBoard board, LogicRow row, PlacedPin pin)
        {
            if (!pin.IsTyped) return false;
            LogicRow neighbour = RowAt(board, pin.Facing);
            return neighbour != null && neighbour != row && neighbour.Skill != null && (neighbour.Skill.Kinds & pin.Kind) != 0;
        }

        /// <summary>Läuft eine Pulsverbindung durch diesen Pin (Start oder Ziel, Nachbarzelle auf dem Weg)?</summary>
        public static bool IsPinLinked(LogicBoard board, PlacedPin pin)
        {
            if (board == null) return false;
            Cell facing = pin.Facing;
            foreach (PulseLink link in board.Links)
            {
                Cell first = link.Path.Count > 0 ? link.Path[0] : link.ToCell;
                Cell last = link.Path.Count > 0 ? link.Path[link.Path.Count - 1] : link.FromCell;
                if (link.FromCell == pin.Cell && first == facing && !link.From.IsCapacitor) return true;
                if (link.ToCell == pin.Cell && last == facing && !link.To.IsCapacitor) return true;
            }
            return false;
        }

        /// <summary>
        /// Ein Pin als kleine Kerbe an der Kante seiner Zelle, leicht über den Rand hinaus. Typisiert in der Farbe der Art
        /// (gedämpft, solange kein passender Nachbar davor liegt; passend mit weissem Rahmen), verbunden in Gold.
        /// </summary>
        public static Rect DrawPin(Rect grid, float size, PlacedPin pin, bool matched, bool linked, string tooltip = null)
        {
            Rect cell = RectOf(grid, size, pin.Cell.X, pin.Cell.Y);
            float length = Mathf.Clamp(size * 0.32f, 7f, 22f);
            float depth = Mathf.Clamp(size * 0.08f, 3f, 6f) + Gap;
            Rect notch;
            switch (pin.Side)
            {
                case Edge.Up: notch = new Rect(cell.center.x - length * 0.5f, cell.yMin - Gap, length, depth); break;
                case Edge.Down: notch = new Rect(cell.center.x - length * 0.5f, cell.yMax - depth + Gap, length, depth); break;
                case Edge.Left: notch = new Rect(cell.xMin - Gap, cell.center.y - length * 0.5f, depth, length); break;
                default: notch = new Rect(cell.xMax - depth + Gap, cell.center.y - length * 0.5f, depth, length); break;
            }

            Color color = pin.IsTyped ? KindColor(pin.Kind) : linked ? PinLinked : PinColor;
            if (pin.IsTyped && !matched) color = Color.Lerp(color, ComponentColor, 0.5f);
            UiTheme.Fill(notch, color);
            if (matched) UiTheme.Outline(notch, Color.white, 1f);
            else if (pin.IsTyped && linked) UiTheme.Outline(notch, PinLinked, 1f);
            if (!string.IsNullOrEmpty(tooltip)) GUI.Label(notch, new GUIContent(string.Empty, tooltip));
            return notch;
        }

        // ------------------------------------------------------------------ A-20: Logik-Chips

        /// <summary>Kurzname eines Gatters: AND, OR, NOT, FUSE; null für leitende Chips.</summary>
        public static string GateShort(ChipKind kind)
        {
            switch (kind)
            {
                case ChipKind.And: return UiTexts.Circuit.And;
                case ChipKind.Or: return UiTexts.Circuit.Or;
                case ChipKind.Not: return UiTexts.Circuit.Not;
                case ChipKind.Fuse: return UiTexts.Circuit.Fuse;
                default: return null;
            }
        }

        /// <summary>Anzeige-Zustand eines Logik-Chips (Arena: offen, durchgebrannt, Ladung; Build: nur Fokus).</summary>
        public struct ChipLook
        {
            /// <summary>Gatter: Zustand bekannt (Arena) – dann zeigt der Chip «open»/«closed».</summary>
            public bool ShowState;
            public bool Open;
            public bool Blown;

            /// <summary>Kondensator: gespeicherte Pulse und Kapazität (Kapazität 0 = keine Anzeige).</summary>
            public int Charge;
            public int Capacity;

            public bool Focus;
            public Color? Border;

            /// <summary>Zusatzzeile unter dem Gatter-Namen (z. B. Schwierigkeit).</summary>
            public string Caption;
        }

        /// <summary>
        /// Ein Logik-Chip in seiner Zelle: Leiterbahnen von der Mitte zu jeder Öffnung (bis an den Zellrand, damit Nachbarn
        /// sichtbar anschliessen), Diode mit Pfeil zum Ausgang, Kondensator mit Platten-Symbol und Ladungs-Punkten, Gatter und
        /// Sicherung als beschriftetes Feld (offen grün, durchgebrannt grau).
        /// </summary>
        public static void DrawLogicChip(Rect rect, ChipDefinition chip, int turns, ChipLook look, string tooltip, GUIStyle style = null)
        {
            if (chip == null) return;
            bool gate = chip.IsGate;
            Color fill = gate ? GateColor : ChipColor;
            if (gate && look.ShowState && look.Open) fill = Color.Lerp(GateColor, GateOpen, 0.55f);
            if (gate && look.Blown) fill = BlownColor;
            if (look.Focus) fill = Color.Lerp(fill, UiTheme.CellHover, 0.6f);
            UiTheme.Fill(rect, fill);

            Color trace = look.Blown ? UiTheme.MutedColor : ChipTrace;
            float t = Mathf.Clamp(rect.width * 0.13f, 3f, 9f);
            Vector2 c = rect.center;
            if (chip.Conducts)
            {
                foreach (Edge side in chip.OpeningsAt(turns)) DrawArm(rect, side, t, trace);
                UiTheme.Fill(new Rect(c.x - t, c.y - t, t * 2f, t * 2f), trace);
            }

            switch (chip.Kind)
            {
                case ChipKind.Diode:
                {
                    Edge outSide = ChipDefinition.DiodeOut(turns);
                    Vector2 tip = Toward(c, outSide, rect.width * 0.36f);
                    DrawArrowHead(tip, outSide, Mathf.Max(8f, rect.width * 0.30f), UiTheme.Accent);
                    // Eingang: dunkler Balken quer zur Bahn.
                    Vector2 inPoint = Toward(c, ChipDefinition.DiodeIn(turns), rect.width * 0.22f);
                    bool horizontal = outSide == Edge.Left || outSide == Edge.Right;
                    float bar = rect.width * 0.34f;
                    UiTheme.Fill(horizontal ? new Rect(inPoint.x - 1.5f, inPoint.y - bar * 0.5f, 3f, bar) : new Rect(inPoint.x - bar * 0.5f, inPoint.y - 1.5f, bar, 3f),
                        UiTheme.Accent);
                    break;
                }
                case ChipKind.Capacitor:
                {
                    // Zwei Platten mit Spalt.
                    float plate = rect.width * 0.46f;
                    float gap = Mathf.Max(3f, rect.width * 0.08f);
                    UiTheme.Fill(new Rect(c.x - gap * 1.5f, c.y - plate * 0.5f, gap * 1.5f, plate), ChipColor);
                    UiTheme.Fill(new Rect(c.x - gap * 0.5f - 3f, c.y - plate * 0.5f, 3f, plate), UiTheme.Accent);
                    UiTheme.Fill(new Rect(c.x + gap * 0.5f, c.y - plate * 0.5f, 3f, plate), UiTheme.Accent);
                    if (look.Capacity > 0) DrawPips(rect, look.Charge, look.Capacity);
                    break;
                }
            }

            if (gate)
            {
                string name = GateShort(chip.Kind);
                string state = string.Empty;
                if (look.Blown) state = $"\n<color=#9aa4b2>{UiTexts.Circuit.Blown}</color>";
                else if (look.ShowState)
                    state = look.Open ? $"\n<color=#0b1a10>{UiTexts.Circuit.Open}</color>" : $"\n<color=#9aa4b2>{UiTexts.Circuit.Closed}</color>";
                else if (!string.IsNullOrEmpty(look.Caption)) state = $"\n{look.Caption}";
                string head = look.ShowState && look.Open && !look.Blown ? $"<color=#0b1a10><b>{name}</b></color>" : $"<b>{name}</b>";
                GUI.Label(rect, new GUIContent(head + state, tooltip), style ?? Label);
            }
            else
            {
                GUI.Label(rect, new GUIContent(string.Empty, tooltip), style ?? Label);
            }

            Color border = look.Border ?? (gate ? GateBorder : new Color(ChipTrace.r, ChipTrace.g, ChipTrace.b, 0.6f));
            UiTheme.Outline(rect, border, look.Focus ? 3f : 1f);
        }

        /// <summary>Leiterbahn-Arm von der Mitte bis an den Zellrand (über den Abstand hinaus).</summary>
        private static void DrawArm(Rect rect, Edge side, float t, Color color)
        {
            Vector2 c = rect.center;
            switch (side)
            {
                case Edge.Up: UiTheme.Fill(new Rect(c.x - t * 0.5f, rect.yMin - Gap, t, c.y - rect.yMin + Gap), color); break;
                case Edge.Down: UiTheme.Fill(new Rect(c.x - t * 0.5f, c.y, t, rect.yMax - c.y + Gap), color); break;
                case Edge.Left: UiTheme.Fill(new Rect(rect.xMin - Gap, c.y - t * 0.5f, c.x - rect.xMin + Gap, t), color); break;
                default: UiTheme.Fill(new Rect(c.x, c.y - t * 0.5f, rect.xMax - c.x + Gap, t), color); break;
            }
        }

        private static Vector2 Toward(Vector2 from, Edge side, float distance)
        {
            switch (side)
            {
                case Edge.Up: return new Vector2(from.x, from.y - distance);
                case Edge.Down: return new Vector2(from.x, from.y + distance);
                case Edge.Left: return new Vector2(from.x - distance, from.y);
                default: return new Vector2(from.x + distance, from.y);
            }
        }

        /// <summary>Pfeilspitze aus gestuften Balken, Spitze bei <paramref name="tip"/>, zeigt nach <paramref name="side"/>.</summary>
        public static void DrawArrowHead(Vector2 tip, Edge side, float length, Color color)
        {
            const int steps = 5;
            float step = length / steps;
            for (int i = 0; i < steps; i++)
            {
                float along = (i + 1) * step;
                float across = (i * 2 + 1) * step * 0.5f;
                switch (side)
                {
                    case Edge.Right: UiTheme.Fill(new Rect(tip.x - along, tip.y - across, step, across * 2f), color); break;
                    case Edge.Left: UiTheme.Fill(new Rect(tip.x + along - step, tip.y - across, step, across * 2f), color); break;
                    case Edge.Down: UiTheme.Fill(new Rect(tip.x - across, tip.y - along, across * 2f, step), color); break;
                    default: UiTheme.Fill(new Rect(tip.x - across, tip.y + along - step, across * 2f, step), color); break;
                }
            }
        }

        /// <summary>Ladung als Punkte unten im Chip: gefüllt = gespeicherter Puls.</summary>
        private static void DrawPips(Rect rect, int charge, int capacity)
        {
            float pip = Mathf.Clamp(rect.width * 0.12f, 4f, 9f);
            float gap = pip * 0.5f;
            float width = capacity * pip + (capacity - 1) * gap;
            float x = rect.center.x - width * 0.5f;
            float y = rect.yMax - pip - 3f;
            for (int i = 0; i < capacity; i++)
            {
                var r = new Rect(x + i * (pip + gap), y, pip, pip);
                UiTheme.Fill(r, i < charge ? PulseColor : new Color(0f, 0f, 0f, 0.6f));
                UiTheme.Outline(r, PulseColor, 1f);
            }
        }

        // ------------------------------------------------------------------ A-20: Pulsverbindungen und Pulse

        /// <summary>Mitte einer Zelle auf dem Bildschirm.</summary>
        public static Vector2 CellCenter(Rect grid, float size, Cell cell) =>
            new Vector2(grid.x + (cell.X + 0.5f) * size, grid.y + (cell.Y + 0.5f) * size);

        /// <summary>Punkte einer Verbindung: Start-Zelle, Zellen der Chips unterwegs, Ziel-Zelle (jeweils die Mitte).</summary>
        public static List<Vector2> LinkPoints(Rect grid, float size, PulseLink link)
        {
            var points = new List<Vector2> { CellCenter(grid, size, link.FromCell) };
            foreach (Cell cell in link.Path) points.Add(CellCenter(grid, size, cell));
            points.Add(CellCenter(grid, size, link.ToCell));
            return points;
        }

        /// <summary>Linie entlang einer Verbindung (achsenparallele Teilstücke; schräg wird zu einem Knick).</summary>
        public static void DrawLink(List<Vector2> points, Color color, float thickness = 3f)
        {
            for (int i = 0; i + 1 < points.Count; i++)
            {
                Vector2 a = points[i], b = points[i + 1];
                var corner = new Vector2(b.x, a.y);
                DrawSegment(a, corner, color, thickness);
                DrawSegment(corner, b, color, thickness);
            }
        }

        private static void DrawSegment(Vector2 a, Vector2 b, Color color, float thickness)
        {
            if (Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y)) return;
            float h = thickness * 0.5f;
            if (Mathf.Approximately(a.y, b.y))
                UiTheme.Fill(new Rect(Mathf.Min(a.x, b.x) - h, a.y - h, Mathf.Abs(b.x - a.x) + thickness, thickness), color);
            else
                UiTheme.Fill(new Rect(a.x - h, Mathf.Min(a.y, b.y) - h, thickness, Mathf.Abs(b.y - a.y) + thickness), color);
        }

        /// <summary>Punkt bei Anteil <paramref name="t"/> (0–1) des Wegs; jedes Teilstück gleich lang (ein Tick pro Teilstück).</summary>
        public static Vector2 PointOnLink(List<Vector2> points, float t)
        {
            if (points.Count == 0) return Vector2.zero;
            if (points.Count == 1) return points[0];
            float along = Mathf.Clamp01(t) * (points.Count - 1);
            int i = Mathf.Min(points.Count - 2, Mathf.FloorToInt(along));
            return Vector2.Lerp(points[i], points[i + 1], along - i);
        }

        /// <summary>Ein Puls: leuchtender Punkt mit Hof.</summary>
        public static void DrawPulse(Vector2 at, float size, Color color)
        {
            float glow = Mathf.Clamp(size * 0.34f, 10f, 22f);
            float core = glow * 0.5f;
            UiTheme.Fill(new Rect(at.x - glow * 0.5f, at.y - glow * 0.5f, glow, glow), new Color(color.r, color.g, color.b, 0.35f));
            UiTheme.Fill(new Rect(at.x - core * 0.5f, at.y - core * 0.5f, core, core), color);
            UiTheme.Outline(new Rect(at.x - core * 0.5f, at.y - core * 0.5f, core, core), Color.white, 1f);
        }

        /// <summary>«(2, 1)» – Zelle für Texte, 1-basiert wie die Nummern der Komponenten.</summary>
        public static string CellText(Cell cell) => $"({cell.X + 1}, {cell.Y + 1})";
    }
}
