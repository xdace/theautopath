using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;

namespace Betaknight.Core.Combat
{
    /// <summary>
    /// Ein Teil einer Gegner-Platine: ein Relais (Bedingung) und die Komponente, die es versorgt. Gegner können Module an
    /// ihren eigenen Skills tragen (<see cref="Modules"/>), genau wie der Ritter.
    /// </summary>
    public sealed class EnemyPart
    {
        public ICondition Condition { get; }
        public string Label { get; }
        public SkillDefinition Skill { get; }

        /// <summary>Module am Skill (Ids aus dem Modul-Katalog).</summary>
        public IReadOnlyList<string> Modules { get; }

        public EnemyPart(ICondition condition, string label, SkillDefinition skill, IReadOnlyList<string> modules = null)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Label = label;
            Skill = skill ?? throw new ArgumentNullException(nameof(skill));
            Modules = modules ?? Array.Empty<string>();
        }

        public EnemyPart WithModule(string moduleId) => new EnemyPart(Condition, Label, Skill, Modules.Concat(new[] { moduleId }).ToList());
    }

    /// <summary>Bauplan eines Gegners: Name, Werte und die festen Teile seiner Platine.</summary>
    public sealed class EnemyFighter
    {
        public string Name { get; }
        public CombatStats Stats { get; }
        public IReadOnlyList<EnemyPart> Parts { get; set; }

        public EnemyFighter(string name, CombatStats stats, IReadOnlyList<EnemyPart> parts)
        {
            Name = name;
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Parts = parts ?? Array.Empty<EnemyPart>();
        }

        /// <summary>Der Kämpfer mit seiner festen Platine, ohne zusätzliche Ausrüstung.</summary>
        public CombatantSetup ToSetup(LogicBoard board = null) =>
            new CombatantSetup { Name = Name, Stats = Stats.Clone(), Board = board ?? EnemyBoard.Build(Parts.ToArray()) };
    }

    /// <summary>
    /// Gegner-Platinen als Daten (A-19): Jeder Teil liegt in einer eigenen Zeile, links das Relais (1×1), rechts daneben die
    /// Komponente in ihrer Form. Gegner-Relais haben keine Grössen-Grenze. Die Platine ist in Arena und Karte lesbar
    /// (<see cref="Lines"/>, oder als Raster über <see cref="LogicBoard.Layout"/>).
    /// </summary>
    public static class EnemyBoard
    {
        public static LogicBoard Build(params EnemyPart[] parts) => Build(parts, null, null);

        /// <summary>
        /// Baut die Platine aus festen Teilen und Ausrüstung (<see cref="EnemyGear"/>): erst die festen Teile (Relais links,
        /// Komponente rechts), darunter mit einer Zeile Abstand je Ausrüstungs-Komponente ihr Runen-Relais und der Skill mit
        /// Modulen und Pins (wie beim Ritter gebaut), Chips rechts neben den Komponenten (zuerst neben der Ausrüstung).
        /// </summary>
        public static LogicBoard Build(IReadOnlyList<EnemyPart> parts, EnemyGear gear, EnemyLoadout loadout)
        {
            parts = parts ?? Array.Empty<EnemyPart>();
            var components = new List<LogicRow>();
            var relays = new List<LogicRelay>();
            var anchors = new List<(int y, int right)>();
            int y = 0, width = 1;
            foreach (EnemyPart part in parts)
            {
                SkillDefinition skill = part.Skill;
                foreach (string m in part.Modules) skill = ModuleRules.ApplyToSkill(skill, new ModuleSpec(m), loadout?.ModuleName(m) ?? m);
                Shape shape = part.Skill.Shape;
                relays.Add(new LogicRelay(part.Condition, part.Label, rect: new CellRect(0, y)));
                components.Add(new LogicRow(skill, new CellRect(new Cell(1, y), shape)));
                anchors.Add((y, 1 + shape.Width));
                width = Math.Max(width, 1 + shape.Width);
                y += shape.Height;
            }

            var chips = new List<LogicChip>();
            if (gear != null && loadout != null)
            {
                var gearAnchors = new List<(int y, int right)>();
                foreach (EnemyGearComponent g in gear.Components)
                {
                    if (y > 0) y++;
                    relays.Add(loadout.Factory.CreateRelay(new RelaySpec(g.RuneId, new Cell(0, y)), null));
                    LogicRow row = loadout.Factory.CreateComponent(new ComponentSpec(g.SkillId, new Cell(1, y), false,
                        g.Modules.Select(m => new ModuleSpec(m)).ToList()), null);
                    components.Add(row);
                    Shape shape = row.Rect?.Shape ?? Shape.One;
                    gearAnchors.Add((y, 1 + shape.Width));
                    width = Math.Max(width, 1 + shape.Width);
                    y += shape.Height;
                }
                gearAnchors.Reverse();
                anchors.InsertRange(0, gearAnchors);
                for (int i = 0; i < gear.Chips.Count; i++)
                {
                    if (!loadout.Chips.TryGet(gear.Chips[i], out ChipDefinition chip)) continue;
                    (int ay, int right) = anchors.Count > 0 ? anchors[i % anchors.Count] : (0, 1);
                    int x = right + (anchors.Count > 0 ? i / anchors.Count : i);
                    chips.Add(new LogicChip(chip, new Cell(x, ay)));
                    width = Math.Max(width, x + 1);
                }
            }

            return LogicBoard.Compile(new BoardLayout(width, Math.Max(1, y), null), components, relays, null, null, 0, null,
                chips, loadout?.Chips.Config, null, loadout?.Effects.Config, loadout?.Effects);
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
                string skill = row.Skill.Modules.Count > 0 ? row.Skill.Name + CatalogTexts.EnemyBoardModules(string.Join(", ", row.Skill.Modules)) : row.Skill.Name;
                lines.Add(CatalogTexts.EnemyBoardLine(relays, skill, row.Skill.Shape.ToString(), row.Skill.Description));
            }
            if (board.Chips.Count > 0) lines.Add(CatalogTexts.EnemyBoardChips(string.Join(", ", board.Chips.Select(c => c.Name))));
            if (lines.Count == 0) lines.Add(CatalogTexts.EnemyBoardBasicOnly);
            return lines;
        }
    }
}
