using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Gear
{
    /// <summary>Ein Relais, wie der Spieler es gelegt hat: Rune mit Stufe, Modulen und Wachstum auf einer Zelle.</summary>
    public readonly struct RelaySpec
    {
        public readonly string RuneId;
        public readonly int Level;
        public readonly Cell Position;

        /// <summary>Module am Relais (Relais-Module, Auslöser «wenn ausgelöst»).</summary>
        public readonly IReadOnlyList<ModuleSpec> Modules;
        public readonly int Growth;

        public RelaySpec(string runeId, Cell position, int level = 0, IReadOnlyList<ModuleSpec> modules = null, int growth = 0)
        {
            RuneId = runeId;
            Position = position;
            Level = level;
            Modules = modules ?? Array.Empty<ModuleSpec>();
            Growth = growth;
        }
    }

    /// <summary>Eine Komponente, wie der Spieler sie gelegt hat: Skill mit Modulen und Wachstum, Lage mit Drehung.</summary>
    public readonly struct ComponentSpec
    {
        public readonly string SkillId;
        public readonly Cell Origin;
        public readonly bool Rotated;

        /// <summary>Module am Skill-Exemplar (Skill-Module, Auslöser «nach Ausführung»).</summary>
        public readonly IReadOnlyList<ModuleSpec> Modules;
        public readonly int Growth;

        public ComponentSpec(string skillId, Cell origin, bool rotated = false, IReadOnlyList<ModuleSpec> modules = null, int growth = 0)
        {
            SkillId = skillId;
            Origin = origin;
            Rotated = rotated;
            Modules = modules ?? Array.Empty<ModuleSpec>();
            Growth = growth;
        }
    }

    /// <summary>Ein Logik-Chip, wie der Spieler ihn gelegt hat (A-20): Chip-Id, Zelle, Vierteldrehungen.</summary>
    public readonly struct ChipSpec
    {
        public readonly string ChipId;
        public readonly Cell Position;
        public readonly int Turns;

        public ChipSpec(string chipId, Cell position, int turns = 0)
        {
            ChipId = chipId;
            Position = position;
            Turns = turns;
        }
    }

    /// <summary>
    /// Bauplan einer Platine für den Kampf: Grösse, Kern, Relais und Komponenten. Auslöser-Ziele in den Modulen sind
    /// Komponenten-Indizes in Lesereihenfolge (wie <see cref="LogicBoard.Rows"/>).
    /// </summary>
    public sealed class CircuitSpec
    {
        public int Width { get; set; } = 4;
        public int Height { get; set; } = 3;
        public Cell? Core { get; set; }
        public int CoreBonusPercent { get; set; }
        public List<RelaySpec> Relays { get; } = new List<RelaySpec>();
        public List<ComponentSpec> Components { get; } = new List<ComponentSpec>();
        public List<ChipSpec> Chips { get; } = new List<ChipSpec>();

        public IEnumerable<ModuleSpec> AllModules => Relays.SelectMany(r => r.Modules).Concat(Components.SelectMany(c => c.Modules));
    }

    /// <summary>
    /// Baut aus einem <see cref="CircuitSpec"/> und der Ausrüstung eine <see cref="LogicBoard"/> (A-19). Jedes Relais
    /// bekommt Bedingung, Schwierigkeit und damit seine Grössen-Grenze aus der Rune; die Komponenten ihren Skill mit Wachstum,
    /// Modulen und passiven Boni. Unbekannte oder gesperrte Runen (Set-Rune ohne Set) lösen nie aus.
    /// </summary>
    public sealed class BoardFactory
    {
        private readonly RuneCatalog _runes;
        private readonly ConditionRegistry _conditions;
        private readonly SkillCatalog _skills;
        private readonly ModuleCatalog _modules;
        private readonly GrowthCatalog _growth;
        private readonly PinCatalog _pins;
        private readonly ChipCatalog _chips;

        public DifficultyBonusConfig Bonus { get; }

        private readonly CircuitEffectCatalog _effects;

        /// <param name="effects">Eigene Effekte der Platine (A-21) mit ihrer Form; bestimmt, welche Module, Chips und Skills es gibt.</param>
        public BoardFactory(RuneCatalog runes, ConditionRegistry conditions, SkillCatalog skills, ModuleCatalog modules = null,
            GrowthCatalog growth = null, DifficultyBonusConfig bonus = null, PinCatalog pins = null, ChipCatalog chips = null,
            CircuitEffectCatalog effects = null)
        {
            _effects = effects ?? CircuitEffectCatalog.Shared;
            _pins = pins ?? PinCatalog.CreateDefault();
            _chips = chips ?? ChipCatalog.CreateDefault(null, _effects);
            _growth = growth ?? GrowthCatalog.CreateDefault();
            _modules = modules ?? ModuleCatalog.CreateDefault(_effects);
            _runes = runes ?? RuneCatalog.CreateDefault();
            _conditions = conditions ?? ConditionRegistry.CreateDefault();
            _skills = skills ?? SkillCatalog.CreateDefault(_effects);
            Bonus = bonus ?? DifficultyBonusConfig.Default;
        }

        public static BoardFactory CreateDefault() => new BoardFactory(null, null, null);

        /// <param name="extraPassives">Weitere passive Effekte neben denen der Ausrüstung, z. B. aus Tag-Stufen.</param>
        public LogicBoard Create(CircuitSpec spec, Equipment equipment, SkillLevelRules skillLevels = null,
            IReadOnlyList<SkillPassive> extraPassives = null)
        {
            spec = spec ?? new CircuitSpec();
            var relaySpecs = spec.Relays.OrderBy(r => r.Position, ReadingOrder.Instance).ToList();
            var componentSpecs = spec.Components.OrderBy(c => c.Origin, ReadingOrder.Instance).ToList();

            var relays = relaySpecs.Select(r => CreateRelay(r, equipment)).ToList();
            var components = componentSpecs.Select(c => CreateComponent(c, equipment, skillLevels, extraPassives)).ToList();

            var edges = new List<GraphEdge>();
            for (int i = 0; i < componentSpecs.Count; i++) AddTriggers(edges, componentSpecs[i].Modules, GraphNode.Skill(i), components.Count);
            for (int i = 0; i < relaySpecs.Count; i++) AddTriggers(edges, relaySpecs[i].Modules, GraphNode.Block(i), components.Count);

            var chips = new List<LogicChip>();
            foreach (ChipSpec c in spec.Chips)
                if (_chips.TryGet(c.ChipId, out ChipDefinition chip)) chips.Add(new LogicChip(chip, c.Position, c.Turns));

            SkillDefinition basic = _skills.TryGet(SkillDefinition.BasicAttackId, out SkillDefinition b) ? b : null;
            return LogicBoard.Compile(new BoardLayout(spec.Width, spec.Height, spec.Core), components, relays, basic, Bonus,
                spec.CoreBonusPercent, edges.Count > 0 ? (_, __) => new LogicGraph(edges) : (Func<IReadOnlyList<LogicRow>, IReadOnlyList<LogicRelay>, LogicGraph>)null,
                chips, _chips.Config, _pins.Config, _effects.Config, _effects);
        }

        /// <summary>Auslöser werden zu Kanten: vom Skill bzw. Relais zur Ziel-Komponente.</summary>
        private static void AddTriggers(List<GraphEdge> edges, IReadOnlyList<ModuleSpec> modules, GraphNode from, int components)
        {
            foreach (ModuleSpec m in modules)
                if (ModuleRules.IsTargeted(m.ModuleId) && m.TargetRow >= 0 && m.TargetRow < components)
                    edges.Add(new GraphEdge(from, GraphNode.Skill(m.TargetRow), m.ModuleId == ModuleIds.ChargeLink ? GraphEdgeKind.Charge : GraphEdgeKind.Trigger));
        }

        /// <summary>Ein Relais aus seiner Rune: Bedingung (mit Schwelle, Wachstum und Modulen), Schwierigkeit, Grenze, Name.</summary>
        public LogicRelay CreateRelay(RelaySpec spec, Equipment equipment)
        {
            var rect = new CellRect(spec.Position, Shape.One);
            if (!_runes.TryGet(spec.RuneId, out RuneDefinition rune)) return Dead(spec.RuneId ?? "?", rect);

            // Schwelle: Grundwert der Stufe, dann Wachstum (bis zur Obergrenze der Regel), dann Module.
            int grown = GrowthApplier.ApplyToParameter(rune, rune.ParameterAt(spec.Level), _growth.ForRune(rune.Id), spec.Growth);
            int parameter = ModuleRules.ApplyToParameter(rune, grown, spec.Modules);
            if (!_conditions.TryCreate(spec.RuneId, parameter, out ICondition condition)) return Dead(spec.RuneId, rect);

            // Schwierigkeit hängt nur am Relais (umgekehrt: eigene Stufe), nie an Erleichterungen.
            int difficulty = rune.DifficultyFor(IsInverted(spec.Modules));
            string label = string.Format(rune.NameTemplate, parameter);
            if (spec.Level > 0) label = $"{label} ▲{spec.Level}";
            bool repeat = false;
            foreach (ModuleSpec m in spec.Modules)
            {
                condition = ModuleRules.ApplyToCondition(condition, m);
                if (m.ModuleId == ModuleIds.Invert) label = ArenaTexts.InvertedLabel(label);
                else if (m.ModuleId == ModuleIds.Extend) label = $"{label} (+{SkillInfo.Seconds(ModuleRules.ExtendTicks + ModuleRules.ExtendTicksPerLevel * m.Level)})";
                else if (m.ModuleId == ModuleIds.RepeatWhileTrue) repeat = true;
            }

            // Set-exklusive Runen wirken nur, solange das Set getragen wird.
            if (rune.UnlockSetId != null && (equipment == null || equipment.SetPieces(rune.UnlockSetId) < SetDefinition.FirstBonusPieces))
                condition = AlwaysCondition.Instance.Not();

            return new LogicRelay(condition, label, difficulty, Bonus.MaxCells(difficulty), rect, repeat, rune.Id,
                rune.DifficultyFor(!IsInverted(spec.Modules)))
            {
                PulseTicks = Ticks.FromSeconds(rune.EffectivePulseSeconds),
            };
        }

        private LogicRelay Dead(string label, CellRect rect) =>
            new LogicRelay(AlwaysCondition.Instance.Not(), label, 0, Bonus.MaxCells(0), rect);

        /// <summary>Eine Komponente: Skill mit Wachstum, Modulen und passiven Boni. Unbekannter Skill = keine Wirkung.</summary>
        public LogicRow CreateComponent(ComponentSpec spec, Equipment equipment, SkillLevelRules skillLevels = null,
            IReadOnlyList<SkillPassive> extraPassives = null)
        {
            SkillDefinition skill = _skills.TryGet(spec.SkillId, out SkillDefinition s) ? s.AtLevel(0, skillLevels) : null;
            Shape shape = (skill?.Shape ?? Shape.One).Turned(spec.Rotated);
            skill = GrowthApplier.Apply(skill, _growth.ForSkill(spec.SkillId), spec.Growth);
            foreach (ModuleSpec m in spec.Modules)
                skill = ModuleRules.ApplyToSkill(skill, m, _modules.TryGet(m.ModuleId, out ModuleDefinition d) ? d.Name : m.ModuleId);
            if (skill != null && equipment != null) skill = equipment.Boost(skill, extraPassives);
            else if (skill != null && extraPassives != null) skill = SkillPassive.Apply(skill, extraPassives);
            Shape baseShape = _skills.TryGet(spec.SkillId, out SkillDefinition d0) ? d0.Shape : Shape.One;
            return new LogicRow(skill, new CellRect(spec.Origin, shape), null, _pins.Place(spec.SkillId, baseShape, spec.Origin, spec.Rotated));
        }

        /// <summary>Kehrt ein Modul «Umkehren» das Relais um?</summary>
        public static bool IsInverted(IReadOnlyList<ModuleSpec> modules)
        {
            if (modules == null) return false;
            foreach (ModuleSpec m in modules)
                if (m.ModuleId == ModuleIds.Invert) return true;
            return false;
        }

        /// <summary>Grundschwierigkeit eines Relais (0–3), wie sie im Kampf zählt.</summary>
        public int DifficultyOf(RelaySpec spec) =>
            _runes.TryGet(spec.RuneId, out RuneDefinition rune) ? rune.DifficultyFor(IsInverted(spec.Modules)) : 0;

        /// <summary>Grössen-Grenze eines Relais in Zellen.</summary>
        public int MaxCellsOf(RelaySpec spec) => Bonus.MaxCells(DifficultyOf(spec));

        private sealed class ReadingOrder : IComparer<Cell>
        {
            public static readonly ReadingOrder Instance = new ReadingOrder();
            public int Compare(Cell a, Cell b) => Cell.CompareReading(a, b);
        }
    }
}
