using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core.Circuit
{
    /// <summary>Platine als Daten (A-19): Startgrösse, Wachstum, Kern.</summary>
    public sealed class CircuitConfig
    {
        /// <summary>Grössen der Platine nach Erweiterungen: Stufe 0 = Start (4×3), jede Erweiterung eine Stufe weiter.</summary>
        public IReadOnlyList<Shape> Sizes { get; set; } = new[]
        {
            new Shape(4, 3),
            new Shape(4, 4),
            new Shape(5, 4),
            new Shape(5, 5),
            new Shape(6, 5),
            new Shape(6, 6),
        };

        /// <summary>Fester 1×1-Kern: berührte Komponenten bekommen <see cref="CoreBonusPercent"/> % Wirkung.</summary>
        public Cell Core { get; set; } = new Cell(1, 1);

        public int CoreBonusPercent { get; set; } = 10;

        public static CircuitConfig Default { get; } = new CircuitConfig();

        public Shape SizeAt(int step) => Sizes[Math.Max(0, Math.Min(step, Sizes.Count - 1))];

        public int MaxStep => Sizes.Count - 1;

        public Shape MaxSize => Sizes[Sizes.Count - 1];
    }

    /// <summary>
    /// Ein Relais-Chip (A-19, früher Rune einer Zeile): 1×1 auf der Platine. Er versorgt die Komponenten, die er an einer
    /// Kante berührt, bis zur Grössen-Grenze seiner Schwierigkeit. Stufe, Wachstum und Module bleiben am Chip.
    /// </summary>
    public sealed class RelayChip : IModuleHolder
    {
        private readonly CircuitBoard _owner;
        private readonly ModuleSlotList _modules = new ModuleSlotList();

        /// <summary>Stabile Id (bleibt beim Verschieben gleich).</summary>
        public int ChipId { get; }

        public RuneDefinition Rune { get; internal set; }
        public int Level { get; internal set; }

        /// <summary>Wachstum des Relais (gewonnene Kämpfe, in denen es ausgelöst hat). Wandert mit ins Inventar.</summary>
        public int Growth { get; internal set; }

        public Cell Position { get; internal set; }
        public CellRect Rect => new CellRect(Position, Shape.One);

        /// <summary>Modul-Plätze: 1, mehr ab Wachstum 10 und 25.</summary>
        public int ModuleSlots => GrowthStages.ModuleSlotsFor(Growth);

        public IReadOnlyList<ModuleInstance> Modules => _modules.Modules;
        string IModuleHolder.ModuleHolderName => HolderName;
        bool IModuleHolder.IsSkillHolder => false;
        void IModuleHolder.AttachModule(ModuleInstance module) => _modules.Attach(module);
        void IModuleHolder.DetachModule(ModuleInstance module) => _modules.Detach(module);

        internal RelayChip(CircuitBoard owner, int chipId, RuneDefinition rune, Cell position)
        {
            _owner = owner;
            ChipId = chipId;
            Rune = rune;
            Position = position;
        }

        /// <summary>«Relay 2» (Lesereihenfolge).</summary>
        public string HolderName => CatalogTexts.RelayHolder(_owner.IndexOf(this));

        public string Name => Rune.NameAt(Level);
        public string Description => Rune.DescriptionAt(Level);
        public bool CanUpgrade => Level < Rune.MaxLevel;

        public override string ToString() => $"{Rune?.Id} @{Position}";
    }

    /// <summary>
    /// Eine Komponente auf der Platine: ein Skill-Exemplar mit Form, Lage und Drehung. Als <see cref="ISkillHolder"/> ist sie
    /// der Ort des Exemplars; wird ihr das Exemplar genommen, verschwindet die Komponente von der Platine.
    /// </summary>
    public sealed class ComponentSlot : ISkillHolder
    {
        private readonly CircuitBoard _owner;

        /// <summary>Stabile Id (Ziel für Auslöser-Module, bleibt beim Verschieben gleich).</summary>
        public int SlotId { get; }

        public SkillInstance Skill { get; private set; }
        public Cell Origin { get; internal set; }
        public bool Rotated { get; internal set; }

        /// <summary>Form laut Katalog (ungedreht).</summary>
        public Shape BaseShape => _owner.ShapeOf(Skill?.SkillId);
        public Shape Shape => BaseShape.Turned(Rotated);
        public CellRect Rect => new CellRect(Origin, Shape);
        public int Cells => BaseShape.Cells;

        internal ComponentSlot(CircuitBoard owner, int slotId, Cell origin, bool rotated)
        {
            _owner = owner;
            SlotId = slotId;
            Origin = origin;
            Rotated = rotated;
        }

        /// <summary>«#2» (Lesereihenfolge).</summary>
        public string HolderName => CatalogTexts.ComponentHolder(_owner.IndexOf(this));

        void ISkillHolder.Hold(SkillInstance skill)
        {
            Skill = skill;
            if (skill == null) _owner.Drop(this);
            else _owner.NotifyChanged();
        }

        internal void Set(SkillInstance skill) => Skill = skill;

        public override string ToString() => $"{Skill?.SkillId} @{Origin} {Shape}";
    }

    /// <summary>
    /// Die Platine des Spielers (A-19): ein Raster, das mit Erweiterungen wächst. Darauf liegen Relais-Chips (1×1),
    /// Komponenten (Skills in ihrer Form) und der feste Kern. Nichts überlappt. Lesereihenfolge (oben links zuerst)
    /// ist die Priorität der Warteschlange und die Reihenfolge von <see cref="Relays"/> und <see cref="Components"/>.
    /// </summary>
    public sealed class CircuitBoard
    {
        private static SkillCatalog _defaultSkills;
        private readonly List<RelayChip> _relays = new List<RelayChip>();
        private readonly List<ComponentSlot> _components = new List<ComponentSlot>();
        private readonly Func<string, Shape> _shapeOf;
        private int _nextId = 1;

        public CircuitConfig Config { get; }

        /// <summary>Erweiterungs-Stufe (0 = Startgrösse).</summary>
        public int Step { get; private set; }
        public int Width => Config.SizeAt(Step).Width;
        public int Height => Config.SizeAt(Step).Height;
        public Cell Core => Config.Core;
        public CellRect CoreRect => new CellRect(Core, Shape.One);

        /// <summary>Relais in Lesereihenfolge (= Index im Kampf).</summary>
        public IReadOnlyList<RelayChip> Relays => _relays;

        /// <summary>Komponenten in Lesereihenfolge (= Priorität und Index im Kampf).</summary>
        public IReadOnlyList<ComponentSlot> Components => _components;

        /// <summary>Nur die Runen der Relais, in Lesereihenfolge.</summary>
        public IReadOnlyList<RuneDefinition> Runes => _relays.Select(r => r.Rune).ToList();

        public bool CanExpand => Step < Config.MaxStep;

        /// <summary>Keine freie Zelle mehr (auch kein Platz für ein Relais).</summary>
        public bool IsFull => !FreeCellFor(Shape.One).HasValue;

        public event Action Changed;

        /// <param name="shapeOf">Form eines Skills nach Id; ohne Angabe aus dem Standard-Katalog.</param>
        public CircuitBoard(CircuitConfig config = null, Func<string, Shape> shapeOf = null, int step = 0)
        {
            Config = config ?? CircuitConfig.Default;
            _shapeOf = shapeOf ?? DefaultShape;
            Step = Math.Max(0, Math.Min(step, Config.MaxStep));
        }

        private static Shape DefaultShape(string skillId)
        {
            _defaultSkills = _defaultSkills ?? SkillCatalog.CreateDefault();
            return skillId != null && _defaultSkills.TryGet(skillId, out SkillDefinition s) ? s.Shape : Shape.One;
        }

        internal Shape ShapeOf(string skillId) => _shapeOf(skillId);

        /// <summary>Form eines Skills (ungedreht), wie diese Platine sie kennt.</summary>
        public Shape ShapeOfSkill(string skillId) => _shapeOf(skillId);

        public int IndexOf(RelayChip relay) => _relays.IndexOf(relay);
        public int IndexOf(ComponentSlot component) => _components.IndexOf(component);

        internal void NotifyChanged() => Changed?.Invoke();

        // ------------------------------------------------------------------ Raster

        /// <summary>Liegt das Rechteck in der Platine und ist frei (Kern, Relais, Komponenten; <paramref name="ignore"/> zählt nicht)?</summary>
        public bool IsFree(CellRect rect, object ignore = null)
        {
            if (!rect.FitsIn(Width, Height) || rect.Overlaps(CoreRect)) return false;
            foreach (RelayChip r in _relays)
                if (!ReferenceEquals(r, ignore) && rect.Overlaps(r.Rect)) return false;
            foreach (ComponentSlot c in _components)
                if (!ReferenceEquals(c, ignore) && rect.Overlaps(c.Rect)) return false;
            return true;
        }

        /// <summary>Erste freie Lage für eine Form in Lesereihenfolge, oder null.</summary>
        public Cell? FreeCellFor(Shape shape, object ignore = null)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (IsFree(new CellRect(new Cell(x, y), shape), ignore)) return new Cell(x, y);
            return null;
        }

        /// <summary>
        /// Erste freie Lage (Lesereihenfolge, flach vor hoch) für eine Komponente, die das Relais berührt. Gibt die Drehung mit.
        /// </summary>
        public bool FreeSpotTouching(RelayChip relay, Shape shape, out Cell origin, out bool rotated)
        {
            origin = default;
            rotated = false;
            if (relay == null) return false;
            var turns = shape.IsSquare ? new[] { false } : shape.Width >= shape.Height ? new[] { false, true } : new[] { true, false };
            foreach (bool turn in turns)
            {
                Shape s = shape.Turned(turn);
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        var rect = new CellRect(new Cell(x, y), s);
                        if (!rect.Touches(relay.Rect) || !IsFree(rect)) continue;
                        origin = rect.Origin;
                        rotated = turn;
                        return true;
                    }
            }
            return false;
        }

        /// <summary>Was liegt auf der Zelle? Relais, Komponente oder null (frei oder Kern).</summary>
        public object At(Cell cell)
        {
            foreach (RelayChip r in _relays) if (r.Position == cell) return r;
            foreach (ComponentSlot c in _components) if (c.Rect.Contains(cell)) return c;
            return null;
        }

        public bool IsCore(Cell cell) => cell == Core;

        /// <summary>Relais, die die Komponente an einer Kante berühren (Lesereihenfolge).</summary>
        public IReadOnlyList<RelayChip> RelaysTouching(ComponentSlot component) =>
            component == null ? new List<RelayChip>() : _relays.Where(r => r.Rect.Touches(component.Rect)).ToList();

        /// <summary>Komponenten, die das Relais an einer Kante berührt (Lesereihenfolge).</summary>
        public IReadOnlyList<ComponentSlot> ComponentsTouching(RelayChip relay) =>
            relay == null ? new List<ComponentSlot>() : _components.Where(c => c.Rect.Touches(relay.Rect)).ToList();

        public bool TouchesCore(ComponentSlot component) => component != null && component.Rect.Touches(CoreRect);

        public RelayChip RelayById(int chipId) => _relays.FirstOrDefault(r => r.ChipId == chipId);
        public ComponentSlot ComponentById(int slotId) => _components.FirstOrDefault(c => c.SlotId == slotId);
        public ComponentSlot ComponentOf(SkillInstance skill) => skill == null ? null : _components.FirstOrDefault(c => c.Skill == skill);

        // ------------------------------------------------------------------ Relais

        public bool Contains(RuneDefinition rune) => rune != null && _relays.Any(r => r.Rune.Id == rune.Id);

        public int IndexOf(RuneDefinition rune) => rune == null ? -1 : _relays.FindIndex(r => r.Rune.Id == rune.Id);

        public bool HasTag(RuneTag tag) => _relays.Any(r => r.Rune.Tag == tag);

        /// <summary>Neues Relais auf einer freien Zelle (ohne Angabe: die erste freie). Jede Rune nur einmal.</summary>
        public RelayChip AddRelay(RuneDefinition rune, Cell? at = null, int level = 0, int growth = 0)
        {
            if (rune == null || Contains(rune)) return null;
            Cell? cell = at ?? FreeCellFor(Shape.One);
            if (!cell.HasValue || !IsFree(new CellRect(cell.Value, Shape.One))) return null;
            var relay = new RelayChip(this, _nextId++, rune, cell.Value)
            {
                Level = Math.Max(0, Math.Min(level, rune.MaxLevel)),
                Growth = Math.Max(0, growth),
            };
            _relays.Add(relay);
            Sort();
            Changed?.Invoke();
            return relay;
        }

        public bool MoveRelay(RelayChip relay, Cell to)
        {
            if (relay == null || !_relays.Contains(relay)) return false;
            if (relay.Position == to) return true;
            if (!IsFree(new CellRect(to, Shape.One), relay)) return false;
            relay.Position = to;
            Sort();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Entfernt ein Relais (z. B. ins Inventar). Die Module bleiben am Chip; die Session gibt sie danach frei.</summary>
        public bool RemoveRelay(RelayChip relay)
        {
            if (relay == null || !_relays.Remove(relay)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Tauscht die Rune eines Relais (mit Stufe und Wachstum); die alte kommt zurück.</summary>
        public bool SwapRune(RelayChip relay, RuneDefinition rune, int level, int growth, out RuneDefinition oldRune, out int oldLevel, out int oldGrowth)
        {
            oldRune = null;
            oldLevel = 0;
            oldGrowth = 0;
            if (relay == null || rune == null || !_relays.Contains(relay)) return false;
            if (_relays.Any(r => r != relay && r.Rune.Id == rune.Id)) return false;
            oldRune = relay.Rune;
            oldLevel = relay.Level;
            oldGrowth = relay.Growth;
            relay.Rune = rune;
            relay.Level = Math.Max(0, Math.Min(level, rune.MaxLevel));
            relay.Growth = Math.Max(0, growth);
            Changed?.Invoke();
            return true;
        }

        public void Grow(RelayChip relay, int points)
        {
            if (relay == null || !_relays.Contains(relay) || points <= 0) return;
            relay.Growth = (int)Math.Min(int.MaxValue, (long)relay.Growth + points);
            Changed?.Invoke();
        }

        /// <summary>Evolution eines Relais: es bekommt die Evolutionsform der Rune; Lage, Stufe, Wachstum und Module bleiben.</summary>
        public bool Evolve(RelayChip relay, RuneDefinition evolved)
        {
            if (relay == null || evolved == null || !_relays.Contains(relay)) return false;
            relay.Rune = evolved;
            relay.Level = Math.Min(relay.Level, evolved.MaxLevel);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hebt die Rune eines Relais eine Stufe an (Lagerfeuer).</summary>
        public bool Upgrade(int index)
        {
            if (index < 0 || index >= _relays.Count || !_relays[index].CanUpgrade) return false;
            _relays[index].Level++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Erstes Relais mit der niedrigsten Stufe, die noch steigen kann, oder -1.</summary>
        public int BestUpgradeTarget()
        {
            int best = -1;
            for (int i = 0; i < _relays.Count; i++)
                if (_relays[i].CanUpgrade && (best < 0 || _relays[i].Level < _relays[best].Level)) best = i;
            return best;
        }

        public Dictionary<RuneTag, int> CountByTag()
        {
            var result = new Dictionary<RuneTag, int>();
            foreach (RelayChip r in _relays)
            {
                result.TryGetValue(r.Rune.Tag, out int n);
                result[r.Rune.Tag] = n + 1;
            }
            return result;
        }

        // ------------------------------------------------------------------ Komponenten

        /// <summary>Passt die Form des Skills an diese Lage (frei, in der Platine)?</summary>
        public bool CanPlace(string skillId, Cell at, bool rotated, ComponentSlot ignore = null) =>
            skillId != null && skillId != SkillDefinition.BasicAttackId && IsFree(new CellRect(at, ShapeOf(skillId).Turned(rotated)), ignore);

        /// <summary>
        /// Legt ein Exemplar als Komponente auf die Platine. Sitzt es schon woanders, wird der alte Ort frei; liegt es
        /// schon auf der Platine, wird es verschoben. Der Basisangriff ist keine Komponente (er füllt die Lücken).
        /// </summary>
        public ComponentSlot Place(SkillInstance skill, Cell at, bool rotated = false)
        {
            if (skill == null || skill.IsBasicAttack) return null;
            ComponentSlot existing = ComponentOf(skill);
            if (existing != null) return Move(existing, at, rotated) ? existing : null;
            if (!CanPlace(skill.SkillId, at, rotated)) return null;
            if (skill.Holder != null)
            {
                ISkillHolder old = skill.Holder;
                skill.Holder = null;
                old.Hold(null);
            }
            var slot = new ComponentSlot(this, _nextId++, at, rotated);
            slot.Set(skill);
            skill.Holder = slot;
            _components.Add(slot);
            Sort();
            Changed?.Invoke();
            return slot;
        }

        /// <summary>Verschiebt (und dreht) eine Komponente, wenn die neue Lage frei ist.</summary>
        public bool Move(ComponentSlot component, Cell to, bool rotated)
        {
            if (component == null || !_components.Contains(component)) return false;
            if (component.Origin == to && component.Rotated == rotated) return true;
            if (!IsFree(new CellRect(to, component.BaseShape.Turned(rotated)), component)) return false;
            component.Origin = to;
            component.Rotated = rotated;
            Sort();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Rechtsklick: dreht um 90° an derselben Ecke oben links, wenn der Platz reicht.</summary>
        public bool Rotate(ComponentSlot component) =>
            component != null && !component.BaseShape.IsSquare && Move(component, component.Origin, !component.Rotated);

        /// <summary>Nimmt eine Komponente von der Platine; ihr Exemplar wird frei.</summary>
        public SkillInstance Remove(ComponentSlot component)
        {
            if (component == null || !_components.Contains(component)) return null;
            SkillInstance skill = component.Skill;
            if (skill != null) skill.Holder = null;
            component.Set(null);
            _components.Remove(component);
            Changed?.Invoke();
            return skill;
        }

        /// <summary>Das Exemplar wurde über die Sammlung genommen: die Komponente verschwindet.</summary>
        internal void Drop(ComponentSlot component)
        {
            if (_components.Remove(component)) Changed?.Invoke();
        }

        // ------------------------------------------------------------------ Wachstum der Platine

        /// <summary>Eine Erweiterung: die Platine wächst nach rechts bzw. unten, alles bleibt liegen.</summary>
        public bool Expand()
        {
            if (!CanExpand) return false;
            Step++;
            Changed?.Invoke();
            return true;
        }

        private void Sort()
        {
            _relays.Sort((a, b) => Cell.CompareReading(a.Position, b.Position));
            _components.Sort((a, b) => Cell.CompareReading(a.Origin, b.Origin));
        }
    }
}
