using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Einheitlicher, deckender Stil für alle IMGUI-Fenster: dunkler undurchsichtiger Hintergrund, heller Text, Rahmen
    /// pro Bereich und feste Schriftgrößen (Titel 18, Text 15, Klein 13). Jedes Fenster ruft zu Beginn von OnGUI
    /// <see cref="Apply"/> auf (IMGUI setzt den Skin pro OnGUI-Aufruf zurück).
    /// </summary>
    public static class UiTheme
    {
        public const int TitleSize = 18;
        public const int TextSize = 15;
        public const int SmallSize = 13;

        public static readonly Color Background = new Color(0.075f, 0.085f, 0.11f, 1f);
        public static readonly Color PanelColor = new Color(0.115f, 0.13f, 0.165f, 1f);
        public static readonly Color CellColor = new Color(0.16f, 0.18f, 0.225f, 1f);
        public static readonly Color CellHover = new Color(0.21f, 0.235f, 0.29f, 1f);
        public static readonly Color BorderColor = new Color(0.30f, 0.34f, 0.42f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.93f, 0.95f, 1f);
        public static readonly Color MutedColor = new Color(0.58f, 0.62f, 0.68f, 1f);
        public static readonly Color Good = new Color(0.49f, 0.86f, 0.44f, 1f);
        public static readonly Color Bad = new Color(1f, 0.42f, 0.38f, 1f);
        public static readonly Color Accent = new Color(1f, 0.84f, 0.37f, 1f);

        // ------------------------------------------------------------------ Farb-Tokens: eine Farbe, eine Bedeutung

        /// <summary>Gold (nur für Gold und Kosten).</summary>
        public static readonly Color Gold = Accent;

        /// <summary>Warnung, z. B. «zu gross, lädt».</summary>
        public static readonly Color Warning = new Color(1f, 0.62f, 0.22f, 1f);

        /// <summary>«Nicht versorgt»: grau statt rot, damit Rot nur Gefahr/Angriff bedeutet.</summary>
        public static readonly Color Disabled = new Color(0.55f, 0.57f, 0.62f, 1f);

        /// <summary>Kern und Boss.</summary>
        public static readonly Color Core = new Color(0.69f, 0.55f, 1f, 1f);

        /// <summary>Verknüpfungen (Trigger/Charge Link).</summary>
        public static readonly Color Link = new Color(1f, 0.68f, 0.26f, 1f);

        /// <summary>Basisangriff und Teile ohne Art.</summary>
        public static readonly Color Neutral = new Color(0.72f, 0.74f, 0.80f, 1f);

        /// <summary>
        /// Farbe der Skill-Art (überall gleich: Inventar, Platine, Arena, Gegner). Bei mehreren Arten gewinnt die besondere vor
        /// dem Angriff (Schockstich = Schock).
        /// </summary>
        public static Color Kind(Betaknight.Core.Arena.SkillKind kinds)
        {
            if ((kinds & Betaknight.Core.Arena.SkillKind.Shock) != 0) return new Color(0.96f, 0.88f, 0.29f, 1f);
            if ((kinds & Betaknight.Core.Arena.SkillKind.Fire) != 0) return new Color(1.00f, 0.56f, 0.18f, 1f);
            if ((kinds & Betaknight.Core.Arena.SkillKind.Healing) != 0) return new Color(0.31f, 0.84f, 0.61f, 1f);
            if ((kinds & Betaknight.Core.Arena.SkillKind.Shield) != 0) return new Color(0.36f, 0.55f, 1.00f, 1f);
            if ((kinds & Betaknight.Core.Arena.SkillKind.Movement) != 0) return new Color(0.78f, 0.49f, 1.00f, 1f);
            if ((kinds & Betaknight.Core.Arena.SkillKind.Attack) != 0) return new Color(1.00f, 0.42f, 0.35f, 1f);
            return Neutral;
        }

        private static GUISkin _skin;

        /// <summary>Fenster-Titel, 18.</summary>
        public static GUIStyle Title { get; private set; }

        /// <summary>Fliesstext, 15.</summary>
        public static GUIStyle Text { get; private set; }

        /// <summary>Kleiner Text, 13.</summary>
        public static GUIStyle Small { get; private set; }

        /// <summary>Kleiner Text ohne Umbruch, für einzeilige Zellen.</summary>
        public static GUIStyle SmallLine { get; private set; }

        /// <summary>Bereich mit Rahmen (Spalten, Abschnitte).</summary>
        public static GUIStyle Section { get; private set; }

        /// <summary>Ziehbare Zelle (Skill, Rune, Modul, Gegenstand, Teil der Platine).</summary>
        public static GUIStyle Cell { get; private set; }

        /// <summary>Ausgewählte Zelle.</summary>
        public static GUIStyle CellSelected { get; private set; }

        /// <summary>Leere Zelle oder freier Platz.</summary>
        public static GUIStyle EmptyCell { get; private set; }

        /// <summary>Deckender Tooltip.</summary>
        public static GUIStyle Tooltip { get; private set; }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        /// <summary>Setzt den deckenden Skin für dieses OnGUI.</summary>
        public static void Apply()
        {
            if (_skin == null) Build();
            GUI.skin = _skin;
        }

        private static void Build()
        {
            _skin = Object.Instantiate(GUI.skin);
            _skin.name = "Betaknight (deckend)";
            Texture2D window = Framed(Background, BorderColor);
            Texture2D panel = Framed(PanelColor, BorderColor);
            Texture2D button = Framed(CellColor, BorderColor);
            Texture2D buttonHover = Framed(CellHover, Accent * 0.8f);
            Texture2D buttonActive = Framed(PanelColor, Accent);

            Style(_skin.box, window, TextSize);
            _skin.box.alignment = TextAnchor.UpperLeft;
            _skin.box.padding = new RectOffset(10, 10, 8, 8);
            Style(_skin.window, window, TextSize);

            _skin.label.fontSize = TextSize;
            _skin.label.richText = true;
            _skin.label.normal.textColor = TextColor;
            _skin.label.hover.textColor = TextColor;

            Style(_skin.button, button, TextSize);
            _skin.button.hover.background = buttonHover;
            _skin.button.active.background = buttonActive;
            _skin.button.focused.background = button;
            _skin.button.alignment = TextAnchor.MiddleCenter;
            foreach (GUIStyleState s in new[] { _skin.button.hover, _skin.button.active, _skin.button.focused })
                s.textColor = TextColor;

            Style(_skin.textField, panel, TextSize);
            _skin.scrollView.normal.background = null;

            Title = new GUIStyle(_skin.label) { fontSize = TitleSize, fontStyle = FontStyle.Bold, wordWrap = true };
            Text = new GUIStyle(_skin.label) { fontSize = TextSize, wordWrap = true };
            Small = new GUIStyle(_skin.label) { fontSize = SmallSize, wordWrap = true };
            SmallLine = new GUIStyle(_skin.label) { fontSize = SmallSize, wordWrap = false, clipping = TextClipping.Clip };

            Section = new GUIStyle(_skin.box) { padding = new RectOffset(8, 8, 6, 8), margin = new RectOffset(3, 3, 3, 3) };
            Section.normal.background = panel;

            Cell = new GUIStyle(_skin.box)
            {
                fontSize = SmallSize, padding = new RectOffset(6, 6, 3, 3), margin = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleLeft, wordWrap = false, clipping = TextClipping.Clip,
            };
            Cell.normal.background = button;
            CellSelected = new GUIStyle(Cell);
            CellSelected.normal.background = Framed(CellHover, Accent);
            EmptyCell = new GUIStyle(Cell) { alignment = TextAnchor.MiddleCenter };
            EmptyCell.normal.background = Framed(PanelColor, new Color(0.22f, 0.25f, 0.31f, 1f));
            EmptyCell.normal.textColor = MutedColor;

            Tooltip = new GUIStyle(_skin.box) { fontSize = SmallSize, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            Tooltip.normal.background = Framed(new Color(0.05f, 0.055f, 0.075f, 1f), Accent * 0.9f);
        }

        private static void Style(GUIStyle style, Texture2D background, int fontSize)
        {
            style.normal.background = background;
            style.normal.textColor = TextColor;
            style.onNormal.background = background;
            style.onNormal.textColor = TextColor;
            style.border = new RectOffset(2, 2, 2, 2);
            style.fontSize = fontSize;
            style.richText = true;
        }

        /// <summary>Undurchsichtige Fläche mit 1-Pixel-Rahmen (9-Slice über den Style-Rand).</summary>
        public static Texture2D Framed(Color fill, Color border)
        {
            fill.a = 1f;
            border.a = 1f;
            const int size = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                tex.SetPixel(x, y, x == 0 || y == 0 || x == size - 1 || y == size - 1 ? border : fill);
            tex.Apply();
            return tex;
        }

        /// <summary>Füllt ein Rechteck mit einer Farbe (Alpha wird beachtet, für Markierungen beim Ziehen).</summary>
        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        /// <summary>Rahmen um ein Rechteck.</summary>
        public static void Outline(Rect r, Color color, float thickness = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), color);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Fill(new Rect(r.x, r.y, thickness, r.height), color);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), color);
        }

        /// <summary>Deckender Tooltip an der Maus aus <see cref="GUI.tooltip"/>. Am Ende von OnGUI aufrufen.</summary>
        public static void DrawTooltip(float width = 360f)
        {
            string text = GUI.tooltip;
            // GUI.tooltip ist global und wird nur gesetzt, solange die Maus über einem Element steht.
            // Ohne Zurücksetzen bliebe der letzte Text stehen (auch in anderen Fenstern).
            if (Event.current.type == EventType.Repaint) GUI.tooltip = string.Empty;
            if (string.IsNullOrEmpty(text)) return;
            DrawTooltip(text, Event.current.mousePosition, width);
        }

        public static void DrawTooltip(string text, Vector2 mouse, float width = 360f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Tooltip == null) Apply();
            var content = new GUIContent(text);
            float height = Tooltip.CalcHeight(content, width);
            float x = Mathf.Min(mouse.x + 16f, Screen.width - width - 8f);
            float y = Mathf.Min(mouse.y + 16f, Screen.height - height - 8f);
            GUI.Label(new Rect(Mathf.Max(8f, x), Mathf.Max(8f, y), width, height), content, Tooltip);
        }
    }
}
