using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Circuit;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Anzeige der eigenen Effekte der Platine (A-21): Symbol und Farbe aus den Daten (<see cref="CircuitEffectCatalog"/>),
    /// Tooltip «Name: Text». Gemeinsam für Build-Fenster, Arena, Shop und Belohnungen. Gerechnet wird nichts.
    /// </summary>
    public static class EffectText
    {
        private static readonly Dictionary<string, Color> Colours = new Dictionary<string, Color>();
        private static readonly Dictionary<string, string> Glyphs = new Dictionary<string, string>();

        public static bool TryGet(string id, out CircuitEffectDefinition effect) => CircuitEffectCatalog.Shared.TryGet(id, out effect);

        /// <summary>Farbe des Effekts aus dem Hex-Wert der Daten (weiss, falls unlesbar).</summary>
        public static Color ColourOf(CircuitEffectDefinition effect)
        {
            if (effect == null) return Color.white;
            if (Colours.TryGetValue(effect.Id, out Color cached)) return cached;
            Color c = ColorUtility.TryParseHtmlString(effect.Colour, out Color parsed) ? parsed : Color.white;
            Colours[effect.Id] = c;
            return c;
        }

        public static Color ColourOf(string id) => TryGet(id, out CircuitEffectDefinition e) ? ColourOf(e) : Color.white;

        /// <summary>Hex-Farbe für Rich-Text («#ffb03d»).</summary>
        public static string ColourHex(string id) => TryGet(id, out CircuitEffectDefinition e) ? e.Colour : "#ffffff";

        /// <summary>Symbol des Effekts; kennt die Schrift ein Zeichen nicht, der erste Buchstabe des Namens.</summary>
        public static string Glyph(CircuitEffectDefinition effect)
        {
            if (effect == null) return "?";
            if (Glyphs.TryGetValue(effect.Id, out string cached)) return cached;
            Font font = GUI.skin != null ? GUI.skin.font : null;
            bool ok = font == null || effect.Icon.All(ch => !char.IsSurrogate(ch) && font.HasCharacter(ch));
            string glyph = ok ? effect.Icon : effect.Name.Substring(0, 1);
            Glyphs[effect.Id] = glyph;
            return glyph;
        }

        /// <summary>Farbiges Symbol als Rich-Text.</summary>
        public static string Icon(CircuitEffectDefinition effect) => effect == null ? string.Empty : $"<color={effect.Colour}>{Glyph(effect)}</color>";

        public static string Icon(string id) => TryGet(id, out CircuitEffectDefinition e) ? Icon(e) : string.Empty;

        /// <summary>Alle bekannten Effekte einer Liste als farbige Symbole («⏫ ↻»), leer ohne.</summary>
        public static string Icons(IEnumerable<string> ids) =>
            ids == null ? string.Empty : string.Join(" ", ids.Where(id => TryGet(id, out _)).Select(Icon));

        /// <summary>«<b>⏫ Overclock</b>: −50 % computing time …».</summary>
        public static string Tip(CircuitEffectDefinition effect) =>
            effect == null ? string.Empty : UiTexts.Effects.Tip(Icon(effect), $"<color={effect.Colour}>{effect.Name}</color>", effect.Description);

        public static string Tip(string id) => TryGet(id, out CircuitEffectDefinition e) ? Tip(e) : string.Empty;

        /// <summary>Tooltip-Zeilen aller Effekte einer Liste.</summary>
        public static IEnumerable<string> Tips(IEnumerable<string> ids) =>
            ids == null ? Enumerable.Empty<string>() : ids.Where(id => TryGet(id, out _)).Select(Tip);

        /// <summary>Effekte einer Komponente der kompilierten Platine (aus Skill, Modulen und berührten Effekt-Chips).</summary>
        public static IReadOnlyList<string> Of(Betaknight.Core.Arena.LogicRow row) =>
            row?.Skill != null ? row.Skill.CircuitEffects : (IReadOnlyList<string>)new string[0];

        /// <summary>Regeln der ganzen Platine (Overflow, Firewall) mit Anzahl, z. B. «▣ Firewall ×2», leer ohne.</summary>
        public static string BoardRules(Betaknight.Core.Arena.LogicBoard board)
        {
            if (board == null) return string.Empty;
            var parts = new List<string>();
            foreach (CircuitEffectDefinition e in CircuitEffectCatalog.Shared.All)
            {
                if (e.Scope != CircuitEffectScope.Board) continue;
                int n = board.BoardEffectCount(e.Id);
                if (n > 0) parts.Add($"{Icon(e)} {e.Name}{(n > 1 ? $" ×{n}" : string.Empty)}");
            }
            return string.Join(", ", parts);
        }

        /// <summary>Tooltip der Platinen-Regeln (jede einmal).</summary>
        public static string BoardRulesTip(Betaknight.Core.Arena.LogicBoard board)
        {
            if (board == null) return string.Empty;
            return string.Join("\n", CircuitEffectCatalog.Shared.All
                .Where(e => e.Scope == CircuitEffectScope.Board && board.HasBoardEffect(e.Id)).Select(Tip));
        }
    }
}
