using System.Collections.Generic;
using System;
using Betaknight.Core.Arena;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
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

        /// <summary>Module am Skill-Exemplar der Zeile (Skill-Module, Auslöser «nach Ausführung»).</summary>
        public readonly IReadOnlyList<ModuleSpec> SkillModules;

        /// <summary>Module am Logikbaustein der Zeile (Baustein-Module, Auslöser «wenn erfüllt»).</summary>
        public readonly IReadOnlyList<ModuleSpec> BlockModules;

        /// <summary>Wachstum des Skill-Exemplars bzw. des Bausteins (wirkt über die Regel aus dem <see cref="GrowthCatalog"/>).</summary>
        public readonly int SkillGrowth;
        public readonly int BlockGrowth;

        public BoardRowSpec(string runeId, string skillId, int level = 0, int skillLevel = 0,
            IReadOnlyList<ModuleSpec> skillModules = null, IReadOnlyList<ModuleSpec> blockModules = null, int skillGrowth = 0, int blockGrowth = 0)
        {
            SkillGrowth = skillGrowth;
            BlockGrowth = blockGrowth;
            RuneId = runeId;
            SkillId = skillId;
            Level = level;
            SkillLevel = skillLevel;
            SkillModules = skillModules ?? Array.Empty<ModuleSpec>();
            BlockModules = blockModules ?? Array.Empty<ModuleSpec>();
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
        private readonly ModuleCatalog _modules;
        private readonly GrowthCatalog _growth;

        public BoardFactory(RuneCatalog runes, ConditionRegistry conditions, SkillCatalog skills, ModuleCatalog modules = null,
            GrowthCatalog growth = null)
        {
            _growth = growth ?? GrowthCatalog.CreateDefault();
            _modules = modules ?? ModuleCatalog.CreateDefault();
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
            var edges = new List<GraphEdge>();
            if (rows != null)
            {
                foreach (BoardRowSpec spec in rows)
                {
                    int index = result.Count;
                    result.Add(CreateRow(spec, equipment, skillLevels, extraPassives));
                    AddTriggers(edges, spec.SkillModules, GraphNode.Skill(index));
                    AddTriggers(edges, spec.BlockModules, GraphNode.Block(index));
                }
            }
            edges.RemoveAll(e => e.To.Row >= result.Count);
            return new LogicBoard(result, null, edges.Count > 0 ? new LogicGraph(edges) : null);
        }

        /// <summary>Auslöser werden zu Kanten im Graph: vom Skill bzw. Baustein der Zeile zum Skill der Zielzeile.</summary>
        private static void AddTriggers(List<GraphEdge> edges, IReadOnlyList<ModuleSpec> modules, GraphNode from)
        {
            foreach (ModuleSpec m in modules)
                if (m.ModuleId == ModuleIds.Trigger && m.TargetRow >= 0) edges.Add(new GraphEdge(from, GraphNode.Skill(m.TargetRow)));
        }

        public LogicRow CreateRow(BoardRowSpec spec, Equipment equipment, SkillLevelRules skillLevels = null,
            IReadOnlyList<SkillPassive> extraPassives = null)
        {
            if (!_runes.TryGet(spec.RuneId, out RuneDefinition rune)) return new LogicRow(AlwaysCondition.Instance, null, spec.RuneId ?? "?");

            // Schwelle: Grundwert der Stufe, dann Wachstum (bis zur Obergrenze der Regel), dann Module.
            int grown = GrowthApplier.ApplyToParameter(rune, rune.ParameterAt(spec.Level), _growth.ForRune(rune.Id), spec.BlockGrowth);
            int parameter = ModuleRules.ApplyToParameter(rune, grown, spec.BlockModules);
            if (!_conditions.TryCreate(spec.RuneId, parameter, out ICondition condition))
                return new LogicRow(AlwaysCondition.Instance, null, spec.RuneId ?? "?");

            string label = string.Format(rune.NameTemplate, parameter);
            foreach (ModuleSpec m in spec.BlockModules)
            {
                condition = ModuleRules.ApplyToCondition(condition, m);
                if (m.ModuleId == ModuleIds.Invert) label = $"NICHT {label}";
                else if (m.ModuleId == ModuleIds.Extend) label = $"{label} (+{SkillInfo.Seconds(ModuleRules.ExtendTicks + ModuleRules.ExtendTicksPerLevel * m.Level)})";
            }

            // Set-exklusive Runen wirken nur, solange das Set getragen wird.
            if (rune.UnlockSetId != null && (equipment == null || equipment.SetPieces(rune.UnlockSetId) < SetDefinition.FirstBonusPieces))
                return new LogicRow(condition, null, label);

            SkillDefinition skill = _skills.TryGet(spec.SkillId, out SkillDefinition s) ? s.AtLevel(spec.SkillLevel, skillLevels) : null;
            skill = GrowthApplier.Apply(skill, _growth.ForSkill(spec.SkillId), spec.SkillGrowth);
            foreach (ModuleSpec m in spec.SkillModules)
                skill = ModuleRules.ApplyToSkill(skill, m, _modules.TryGet(m.ModuleId, out ModuleDefinition d) ? d.Name : m.ModuleId);
            if (skill != null && equipment != null) skill = equipment.Boost(skill, extraPassives);
            else if (skill != null && extraPassives != null) skill = SkillPassive.Apply(skill, extraPassives);
            return new LogicRow(condition, skill, label);
        }
    }
}
