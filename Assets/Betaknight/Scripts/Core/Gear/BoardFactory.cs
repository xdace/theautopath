using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Gear
{
    /// <summary>Eine Zeile, wie der Spieler sie baut: Rune mit Stufe und zugeordneter Skill (oder keiner).</summary>
    public readonly struct BoardRowSpec
    {
        public readonly string RuneId;
        public readonly int Level;
        public readonly string SkillId;

        public BoardRowSpec(string runeId, string skillId, int level = 0)
        {
            RuneId = runeId;
            SkillId = skillId;
            Level = level;
        }
    }

    /// <summary>
    /// Baut aus Runen-Zeilen und Ausrüstung eine <see cref="LogicBoard"/>. Fehlt der Skill einer Zeile
    /// (Teil abgelegt, unbekannte Rune), bleibt die Zeile als verwaiste Zeile stehen und wird im Kampf übersprungen.
    /// </summary>
    public sealed class BoardFactory
    {
        private readonly RuneCatalog _runes;
        private readonly ConditionRegistry _conditions;
        private readonly SkillCatalog _skills;

        public BoardFactory(RuneCatalog runes, ConditionRegistry conditions, SkillCatalog skills)
        {
            _runes = runes ?? RuneCatalog.CreateDefault();
            _conditions = conditions ?? ConditionRegistry.CreateDefault();
            _skills = skills ?? SkillCatalog.CreateDefault();
        }

        public static BoardFactory CreateDefault() => new BoardFactory(null, null, null);

        public LogicBoard Create(IEnumerable<BoardRowSpec> rows, Equipment equipment)
        {
            var result = new List<LogicRow>();
            if (rows != null)
                foreach (BoardRowSpec spec in rows) result.Add(CreateRow(spec, equipment));
            return new LogicBoard(result);
        }

        public LogicRow CreateRow(BoardRowSpec spec, Equipment equipment)
        {
            if (!_runes.TryGet(spec.RuneId, out RuneDefinition rune) ||
                !_conditions.TryCreate(spec.RuneId, rune.ParameterAt(spec.Level), out ICondition condition))
            {
                return new LogicRow(AlwaysCondition.Instance, null, spec.RuneId ?? "?");
            }

            string label = rune.NameAt(spec.Level);
            bool available = equipment != null ? equipment.ProvidesSkill(spec.SkillId) : spec.SkillId == SkillDefinition.BasicAttackId;
            SkillDefinition skill = available && _skills.TryGet(spec.SkillId, out SkillDefinition s) ? s : null;
            return new LogicRow(condition, skill, label);
        }
    }
}
