using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Circuit
{
    /// <summary>Art eines Logik-Chips auf der Platine (A-20). Alle Chips sind 1×1 und lassen sich drehen.</summary>
    public enum ChipKind
    {
        /// <summary>Leiterbahn gerade (links–rechts).</summary>
        Trace,

        /// <summary>Leiterbahn Ecke (oben–rechts).</summary>
        TraceCorner,

        /// <summary>Leiterbahn T (links–oben–rechts).</summary>
        TraceTee,

        /// <summary>Leiterbahn Kreuz (alle vier Seiten).</summary>
        TraceCross,

        /// <summary>Diode: Pulse laufen nur von links nach rechts (in der ungedrehten Lage).</summary>
        Diode,

        /// <summary>UND: zwei berührte Relais zusammen lösen aus.</summary>
        And,

        /// <summary>ODER: eines von zwei berührten Relais löst aus.</summary>
        Or,

        /// <summary>NICHT: löst aus, wenn das berührte Relais nicht mehr gilt.</summary>
        Not,

        /// <summary>Kondensator: speichert Pulse und gibt sie gesammelt wieder ab.</summary>
        Capacitor,

        /// <summary>Sicherung: einmal pro Kampf ein Relais der Schwierigkeit 3, ausgelöst vom berührten Relais.</summary>
        Fuse,

        /// <summary>Verstärker (A-21): leitet wie eine gerade Leiterbahn, jeder durchlaufende Puls wird stärker.</summary>
        Amplifier,

        /// <summary>Watchdog (A-21): ein Relais, das bei Stillstand die grösste berührte Komponente auslöst.</summary>
        Watchdog,

        /// <summary>Effekt-Chip (A-21): berührte Komponenten haben seinen Effekt (<see cref="ChipDefinition.EffectId"/>).</summary>
        Effect,
    }

    /// <summary>Werte der Chips als Daten.</summary>
    public sealed class ChipConfig
    {
        /// <summary>So viele Pulse fasst ein Kondensator; weitere gehen verloren.</summary>
        public int CapacitorCapacity { get; set; } = 3;

        /// <summary>Spätestens so lange nach dem ersten gespeicherten Puls gibt der Kondensator ab.</summary>
        public int CapacitorReleaseTicks { get; set; } = Arena.Ticks.FromSeconds(3);

        /// <summary>Ein Ereignis-Relais gilt für Gatter so lange nach dem Auslösen als «an».</summary>
        public int GateHoldTicks { get; set; } = Arena.Ticks.PerSecond / 2;

        /// <summary>UND: Schwierigkeit = höhere der beiden + dieser Wert (höchstens 3).</summary>
        public int AndDifficultyBonus { get; set; } = 1;

        /// <summary>Schwierigkeit der Sicherung.</summary>
        public int FuseDifficulty { get; set; } = 3;

        public static ChipConfig Default { get; } = new ChipConfig();
    }

    /// <summary>Ein Chip als Daten: Name, Text, Öffnungen (ungedreht), Seltenheit.</summary>
    public sealed class ChipDefinition
    {
        public string Id { get; }
        public ChipKind Kind { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>Seiten, über die Pulse laufen (ungedreht). Gatter und Sicherung leiten keine Pulse.</summary>
        public IReadOnlyList<Edge> Openings { get; }

        /// <summary>Gewicht beim Würfeln einer Belohnung.</summary>
        public int Weight { get; }

        /// <summary>Eigener Effekt der Platine (A-21), den der Chip trägt, sonst null.</summary>
        public string EffectId { get; }

        public ChipDefinition(string id, ChipKind kind, string name, string description, IEnumerable<Edge> openings, int weight,
            string effectId = null)
        {
            EffectId = effectId;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            Name = name ?? id;
            Description = description ?? string.Empty;
            Openings = (openings ?? Array.Empty<Edge>()).ToList();
            Weight = Math.Max(0, weight);
        }

        public bool IsTrace => Kind == ChipKind.Trace || Kind == ChipKind.TraceCorner || Kind == ChipKind.TraceTee || Kind == ChipKind.TraceCross;

        /// <summary>Leitet Pulse (Leiterbahn, Diode, Kondensator).</summary>
        public bool Conducts => IsTrace || Kind == ChipKind.Diode || Kind == ChipKind.Capacitor || Kind == ChipKind.Amplifier;

        /// <summary>Gatter oder Sicherung: wirkt wie ein Relais, gespeist von berührten Relais.</summary>
        public bool IsGate => Kind == ChipKind.And || Kind == ChipKind.Or || Kind == ChipKind.Not || Kind == ChipKind.Fuse || Kind == ChipKind.Watchdog;

        /// <summary>Öffnungen nach <paramref name="turns"/> Vierteldrehungen im Uhrzeigersinn.</summary>
        public IEnumerable<Edge> OpeningsAt(int turns) => Openings.Select(s => s.Turn(turns));

        /// <summary>Bei der Diode: Eingangsseite (ungedreht links) bzw. Ausgang (rechts).</summary>
        public static Edge DiodeIn(int turns) => Edge.Left.Turn(turns);
        public static Edge DiodeOut(int turns) => Edge.Right.Turn(turns);
    }

    public static class ChipIds
    {
        public const string Trace = "trace";
        public const string TraceCorner = "trace_corner";
        public const string TraceTee = "trace_tee";
        public const string TraceCross = "trace_cross";
        public const string Diode = "diode";
        public const string And = "gate_and";
        public const string Or = "gate_or";
        public const string Not = "gate_not";
        public const string Capacitor = "capacitor";
        public const string Fuse = "fuse";
    }

    /// <summary>Alle Chips nach Id (A-20). Seltene Belohnungen: Elite, Boss-Flucht, Truhen, Shop.</summary>
    public sealed class ChipCatalog
    {
        private readonly Dictionary<string, ChipDefinition> _byId = new Dictionary<string, ChipDefinition>();
        private readonly List<ChipDefinition> _all = new List<ChipDefinition>();

        public ChipConfig Config { get; }

        public ChipCatalog(ChipConfig config = null) => Config = config ?? ChipConfig.Default;

        public IReadOnlyList<ChipDefinition> All => _all;

        public void Register(ChipDefinition chip)
        {
            if (_byId.ContainsKey(chip.Id)) throw new ArgumentException($"Chip '{chip.Id}' already registered.");
            _byId[chip.Id] = chip;
            _all.Add(chip);
        }

        public bool TryGet(string id, out ChipDefinition chip)
        {
            chip = null;
            return id != null && _byId.TryGetValue(id, out chip);
        }

        public ChipDefinition Get(string id) => TryGet(id, out ChipDefinition c) ? c : throw new KeyNotFoundException($"Unknown chip '{id}'.");

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        /// <summary>Würfelt einen Chip nach Gewicht.</summary>
        public ChipDefinition Roll(Random random)
        {
            // Ohne Pins leiten Leiterbahnen, Diode, Kondensator und Verstärker nichts: sie werden nicht mehr angeboten.
            List<ChipDefinition> offered = _all.Where(c => PinConfig.Default.Enabled || !c.Conducts).ToList();
            int total = offered.Sum(c => c.Weight);
            if (total <= 0) return null;
            int roll = random.Next(total);
            foreach (ChipDefinition c in offered)
            {
                roll -= c.Weight;
                if (roll < 0) return c;
            }
            return offered[offered.Count - 1];
        }

        /// <summary>Chip eines eigenen Effekts (A-21): Verstärker leitet links–rechts, Watchdog ist ein Relais, sonst Effekt-Chip.</summary>
        public static ChipDefinition EffectChip(CircuitEffectDefinition e)
        {
            switch (e.Id)
            {
                case CircuitEffectIds.Amplifier:
                    return new ChipDefinition(e.Id, ChipKind.Amplifier, e.Name, e.Description, new[] { Edge.Left, Edge.Right }, e.Weight, e.Id);
                case CircuitEffectIds.Watchdog:
                    return new ChipDefinition(e.Id, ChipKind.Watchdog, e.Name, e.Description, null, e.Weight, e.Id);
                default:
                    string text = e.Scope == CircuitEffectScope.Board ? e.Description : $"Touching components get: {e.Description}";
                    return new ChipDefinition(e.Id, ChipKind.Effect, e.Name, text, null, e.Weight, e.Id);
            }
        }

        private static ChipCatalog _default;

        /// <summary>Gemeinsamer Standard-Katalog (nur lesend genutzt).</summary>
        public static ChipCatalog Shared => _default ?? (_default = CreateDefault());

        /// <param name="effects">Eigene Effekte der Platine (A-21): die in Form «Chip» kommen als Chips dazu.</param>
        public static ChipCatalog CreateDefault(ChipConfig config = null, CircuitEffectCatalog effects = null)
        {
            config = config ?? ChipConfig.Default;
            var c = new ChipCatalog(config);
            c.Register(new ChipDefinition(ChipIds.Trace, ChipKind.Trace, "Trace", "Connects pins in a straight line.",
                new[] { Edge.Left, Edge.Right }, 6));
            c.Register(new ChipDefinition(ChipIds.TraceCorner, ChipKind.TraceCorner, "Trace Corner", "Connects pins around a corner.",
                new[] { Edge.Up, Edge.Right }, 6));
            c.Register(new ChipDefinition(ChipIds.TraceTee, ChipKind.TraceTee, "Trace T", "Splits a connection into two.",
                new[] { Edge.Left, Edge.Up, Edge.Right }, 4));
            c.Register(new ChipDefinition(ChipIds.TraceCross, ChipKind.TraceCross, "Trace Cross", "Joins all four sides.",
                Edges.All, 3));
            c.Register(new ChipDefinition(ChipIds.Diode, ChipKind.Diode, "Diode", "One-way connection: pulses only pass from the in side to the out side.",
                new[] { Edge.Left, Edge.Right }, 3));
            c.Register(new ChipDefinition(ChipIds.And, ChipKind.And, "AND Gate",
                $"Triggers when both touching relays are on. Difficulty: the higher one +{config.AndDifficultyBonus} (max. 3). Powers touching components.",
                null, 3));
            c.Register(new ChipDefinition(ChipIds.Or, ChipKind.Or, "OR Gate",
                "Triggers whenever one of the two touching relays triggers. Difficulty: the lower one. Powers touching components.", null, 3));
            c.Register(new ChipDefinition(ChipIds.Not, ChipKind.Not, "NOT Gate",
                "Triggers when the touching relay turns off. Difficulty: the relay's own inverted value. Powers touching components.", null, 3));
            c.Register(new ChipDefinition(ChipIds.Capacitor, ChipKind.Capacitor, "Capacitor",
                $"Stores up to {config.CapacitorCapacity} pulses and releases them when a touching relay triggers or {Arena.ArenaTexts.Seconds(config.CapacitorReleaseTicks)} after the first one.",
                Edges.All, 2));
            c.Register(new ChipDefinition(ChipIds.Fuse, ChipKind.Fuse, "Fuse",
                $"Once per fight: when the touching relay triggers, it powers touching components as a difficulty {config.FuseDifficulty} relay.", null, 2));
            foreach (CircuitEffectDefinition e in (effects ?? CircuitEffectCatalog.Shared).InForm(CircuitEffectForm.Chip))
                c.Register(EffectChip(e));
            return c;
        }
    }

    /// <summary>Ein Chip auf der Platine: Art, Zelle und Drehung (Vierteldrehungen im Uhrzeigersinn).</summary>
    public sealed class BoardChip
    {
        /// <summary>Stabile Id (bleibt beim Verschieben gleich).</summary>
        public int ChipId { get; }
        public ChipDefinition Definition { get; }
        public Cell Position { get; internal set; }
        public int Turns { get; internal set; }

        public ChipKind Kind => Definition.Kind;
        public string Name => Definition.Name;
        public CellRect Rect => new CellRect(Position, Shape.One);

        internal BoardChip(int chipId, ChipDefinition definition, Cell position, int turns)
        {
            ChipId = chipId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Position = position;
            Turns = ((turns % 4) + 4) % 4;
        }

        public IEnumerable<Edge> Openings => Definition.OpeningsAt(Turns);

        public override string ToString() => $"{Definition.Id} @{Position} ↻{Turns}";
    }
}
