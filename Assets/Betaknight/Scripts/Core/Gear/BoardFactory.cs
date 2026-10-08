using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Gear
{
    /// <summary>Eine Zeile, wie der Spieler sie baut: Rune mit Stufe und Skill mit der Stufe seines Exemplars (oder keiner).</summary>
    public readonly struct BoardRowSpec
    {
        public readonly string RuneId;
        public readonly int Level;
        public readonly string SkillId;
        public readonly int SkillLevel;

        public BoardRowSpec(string runeId, string skillId, int level = 0, int skillLevel = 0)
        {
            RuneId = runeId;
            SkillId = skillId;
            Level = level;
            SkillLevel = skillLevel;
        }
    }

    /// <summary>
    /// Baut aus Runen-Zeilen und Ausrüstung eine <see cref="LogicBoard"/>. Der Skill kommt aus der Zeile (mit der Stufe
    /// seines Exemplars), die Ausrüstung gibt passive Boni auf passende Skill-Arten. Ohne Skill (bewusst herausgenommen,
    /// unbekannte Rune, Set-Rune ohne Set) bleibt die Zeile verwaist und wird im Kampf übersprungen.
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

        /// <param name="skillLevels">Regeln für Skill-Stufen. Null = alle Skills in Grundform.</param>
        /// <param name="extraPassives">Weitere passive Effekte neben denen der Ausrüstung, z. B. aus Tag-Stufen.</param>
        public LogicBoard Create(IEnumerable<BoardRowSpec> rows, Equipment equipment, SkillLevelRules skillLevels = null,
            IReadOnlyList<SkillPassive> extraPassives = null)
        {
            var result = new List<LogicRow>();
            if (rows != null)
                foreach (BoardRowSpec spec in rows) result.Add(CreateRow(spec, equipment, skillLevels, extraPassives));
            return new LogicBoard(result);
        }

        public LogicRow CreateRow(BoardRowSpec spec, Equipment equipment, SkillLevelRules skillLevels = null,
            IReadOnlyList<SkillPassive> extraPassives = null)
        {
            if (!_runes.TryGet(spec.RuneId, out RuneDefinition rune) ||
                !_conditions.TryCreate(spec.RuneId, rune.ParameterAt(spec.Level), out ICondition condition))
            {
                return new LogicRow(AlwaysCondition.Instance, null, spec.RuneId ?? "?");
            }

            string label = rune.NameAt(spec.Level);

            // Set-exklusive Runen wirken nur, solange das Set getragen wird.
            if (rune.UnlockSetId != null && (equipment == null || equipment.SetPieces(rune.UnlockSetId) < SetDefinition.FirstBonusPieces))
                return new LogicRow(condition, null, label);

            SkillDefinition skill = _skills.TryGet(spec.SkillId, out SkillDefinition s) ? s.AtLevel(spec.SkillLevel, skillLevels) : null;
            if (skill != null && equipment != null) skill = equipment.Boost(skill, extraPassives);
            else if (skill != null && extraPassives != null) skill = SkillPassive.Apply(skill, extraPassives);
            return new LogicRow(condition, skill, label);
        }
    }
}
