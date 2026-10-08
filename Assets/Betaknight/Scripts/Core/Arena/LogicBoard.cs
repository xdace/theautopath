using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Eine Zeile der Logik-Tafel: Bedingung + Skill. Ohne Skill ist die Zeile verwaist und wird übersprungen.</summary>
    public sealed class LogicRow
    {
        public ICondition Condition { get; }
        public SkillDefinition Skill { get; }

        /// <summary>Anzeigename der Bedingung, z. B. der Runenname.</summary>
        public string Label { get; }

        public bool IsOrphaned => Skill == null;

        public LogicRow(ICondition condition, SkillDefinition skill, string label = null)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Skill = skill;
            Label = label ?? condition.GetType().Name;
        }

        public override string ToString() => $"[{Label}] -> [{Skill?.Name ?? "—"}]";
    }

    /// <summary>
    /// Die Logik-Tafel: Zeilen von oben nach unten, die erste erfüllte Zeile mit bereitem Skill feuert.
    /// Darunter steht immer die feste Zeile [Immer] -> [Basisangriff]. Gegner haben eine feste Tafel.
    /// </summary>
    public sealed class LogicBoard
    {
        public IReadOnlyList<LogicRow> Rows { get; }
        public LogicRow Fallback { get; }

        /// <summary>Index, mit dem die Fallback-Zeile im Protokoll erscheint.</summary>
        public int FallbackIndex => Rows.Count;

        public LogicBoard(IEnumerable<LogicRow> rows, SkillDefinition fallbackSkill = null)
        {
            Rows = new List<LogicRow>(rows ?? Array.Empty<LogicRow>());
            Fallback = new LogicRow(AlwaysCondition.Instance, fallbackSkill ?? SkillDefinition.BasicAttack, "Immer");
        }

        public static LogicBoard FallbackOnly { get; } = new LogicBoard(null);

        /// <summary>Zeile nach Index inklusive Fallback.</summary>
        public LogicRow RowAt(int index) => index == FallbackIndex ? Fallback : Rows[index];
    }
}
