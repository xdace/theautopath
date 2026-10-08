using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;

namespace Betaknight.Core.Combat
{
    /// <summary>Ein Teil einer Gegner-Platine: ein Relais (Bedingung) und die Komponente, die es versorgt.</summary>
    public sealed class EnemyPart
    {
        public ICondition Condition { get; }
        public string Label { get; }
        public SkillDefinition Skill { get; }

        public EnemyPart(ICondition condition, string label, SkillDefinition skill)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Label = label;
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        }
    }

    /// <summary>
    /// Gegner-Platinen als Daten (A-19): Jeder Teil liegt in einer eigenen Zeile, links das Relais (1×1), rechts daneben die
    /// Komponente in ihrer Form. Gegner-Relais haben keine Grössen-Grenze. Die Platine ist in Arena und Karte lesbar
    /// (<see cref="Lines"/>, oder als Raster über <see cref="LogicBoard.Layout"/>).
    /// </summary>
    public static class EnemyBoard
    {
        public static LogicBoard Build(params EnemyPart[] parts)
        {
            parts = parts ?? Array.Empty<EnemyPart>();
            var components = new List<LogicRow>();
            var relays = new List<LogicRelay>();
            int y = 0, width = 1;
            foreach (EnemyPart part in parts)
            {
                Shape shape = part.Skill.Shape;
                relays.Add(new LogicRelay(part.Condition, part.Label, rect: new CellRect(0, y)));
                components.Add(new LogicRow(part.Skill, new CellRect(new Cell(1, y), shape)));
                width = Math.Max(width, 1 + shape.Width);
                y += shape.Height;
            }
            return LogicBoard.Compile(new BoardLayout(width, Math.Max(1, y), null), components, relays);
        }

        /// <summary>Eine Zeile pro Komponente: «Every 7 s → Ram (2×1)»; ohne Komponenten nur der Basisangriff.</summary>
        public static IReadOnlyList<string> Lines(LogicBoard board)
        {
            var lines = new List<string>();
            if (board == null) return lines;
            foreach (LogicRow row in board.Rows)
            {
                if (row.Skill == null) continue;
                string relays = row.Relays.Count > 0 ? string.Join(" / ", row.Relays.Select(r => r.Label)) : ArenaTexts.NotPowered;
                lines.Add(CatalogTexts.EnemyBoardLine(relays, row.Skill.Name, row.Skill.Shape.ToString(), row.Skill.Description));
            }
            if (lines.Count == 0) lines.Add(CatalogTexts.EnemyBoardBasicOnly);
            return lines;
        }
    }
}
