using Betaknight.Core.Arena;
using Betaknight.Core.Runes;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Stufe einer Rune als farbiges Abzeichen: grau unverstärkt, grün verstärkt, gold auf höchster Stufe.
    /// Dazu die Grundschwierigkeit als Symbol (○ ● ●● ●●●) mit dem Bonus als Tooltip.
    /// </summary>
    public static class RuneText
    {
        private static readonly string[] DifficultyColors = { "#9aa4b2", "#8fd3ff", "#ffae42", "#ff6b6b" };

        /// <summary>Farbiges Schwierigkeits-Symbol, z. B. «<color=…>●●</color>».</summary>
        public static string Difficulty(int tier) =>
            $"<color={DifficultyColors[DifficultyBonusConfig.Clamp(tier)]}>{DifficultyText.Symbol(tier)}</color>";

        /// <summary>«Difficulty ●● Hard: +30 % power, −20 % Cast Time · powers up to 4 cells» (mit Umkehrung, wenn sie eine andere Stufe hat).</summary>
        public static string DifficultyTip(RuneDefinition rune, bool inverted = false)
        {
            if (rune == null) return string.Empty;
            int tier = rune.DifficultyFor(inverted);
            string text = UiTexts.Rune.Difficulty(DifficultyText.Symbol(tier), DifficultyText.Tooltip(tier));
            if (!inverted && rune.InvertedDifficulty != rune.Difficulty)
                text += UiTexts.Rune.Inverted(DifficultyText.Symbol(rune.InvertedDifficulty), DifficultyText.Name(rune.InvertedDifficulty));
            text += UiTexts.Rune.EasersKeepBonus;
            return text;
        }

        /// <summary>Kurz für Angebote: «●● Schwer».</summary>
        public static string DifficultyBadge(RuneDefinition rune) =>
            rune == null ? string.Empty : $"{Difficulty(rune.Difficulty)} <size=13>{DifficultyText.Name(rune.Difficulty)}</size>";

        /// <summary>
        /// Für Angebote von Teilen, Modulen und Skills: «Erleichtert: «Gegner betäubt» ●● (…)» als eigene Zeile, leer wenn
        /// der Träger nichts erleichtert.
        /// </summary>
        public static string Eases(Betaknight.Core.OverworldSession session, string carrierId)
        {
            string text = session.EasesText(carrierId);
            return text.Length > 0 ? $"\n<color=#8fd3ff>{text}</color>" : string.Empty;
        }

        public static string LevelBadge(RuneDefinition rune, int level)
        {
            if (rune == null || rune.MaxLevel == 0) return string.Empty;
            string color = level <= 0 ? "#888888" : level >= rune.MaxLevel ? "#ffd75e" : "#7ddc6f";
            string mark = level > 0 ? "▲ " : string.Empty;
            return $"  <color={color}><size=13>{mark}{rune.LevelText(level)}</size></color>";
        }
    }
}