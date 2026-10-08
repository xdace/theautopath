using Betaknight.Core.Runes;

namespace Betaknight.Overworld.UI
{
    /// <summary>Stufe einer Rune als farbiges Abzeichen: grau unverstärkt, grün verstärkt, gold auf höchster Stufe.</summary>
    public static class RuneText
    {
        public static string LevelBadge(RuneDefinition rune, int level)
        {
            if (rune == null || rune.MaxLevel == 0) return string.Empty;
            string color = level <= 0 ? "#888888" : level >= rune.MaxLevel ? "#ffd75e" : "#7ddc6f";
            string mark = level > 0 ? "▲ " : string.Empty;
            return $"  <color={color}><size=12>{mark}{rune.LevelText(level)}</size></color>";
        }
    }
}