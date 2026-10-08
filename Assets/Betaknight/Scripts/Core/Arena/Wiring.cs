using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;
using PinSide = Betaknight.Core.Circuit.Edge;

namespace Betaknight.Core.Arena
{
    /// <summary>Ein Chip im Kampf (A-20): Art, Zelle, Drehung und was er im Kampf ist (Gatter-Relais, Kondensator).</summary>
    public sealed class LogicChip
    {
        public ChipDefinition Definition { get; }
        public CellRect Rect { get; }
        public int Turns { get; }

        public ChipKind Kind => Definition.Kind;
        public string Name => Definition.Name;

        /// <summary>Position in <see cref="LogicBoard.Chips"/>.</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>Gatter und Sicherung: das Relais, als das der Chip im Kampf auslöst (Index in <see cref="LogicBoard.Relays"/>), sonst -1.</summary>
        public int RelayIndex { get; internal set; } = -1;

        /// <summary>Kondensator: Relais, die ihn berühren und bei ihrem Auslösen entladen.</summary>
        public IReadOnlyList<int> ReleaseRelays => ReleaseList;
        internal readonly List<int> ReleaseList = new List<int>();

        public LogicChip(ChipDefinition definition, Cell position, int turns = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Rect = new CellRect(position, Shape.One);
            Turns = ((turns % 4) + 4) % 4;
        }

        public IEnumerable<PinSide> Openings => Definition.OpeningsAt(Turns);

        public override string ToString() => $"{Definition.Id} @{Rect.Origin}";
    }

    /// <summary>Knoten, den ein Puls erreichen kann: eine Komponente oder ein Kondensator.</summary>
    public readonly struct PulseNode : IEquatable<PulseNode>
    {
        public readonly bool IsCapacitor;

        /// <summary>Komponente (Index in <see cref="LogicBoard.Rows"/>) bzw. Chip (Index in <see cref="LogicBoard.Chips"/>).</summary>
        public readonly int Index;

        private PulseNode(bool capacitor, int index)
        {
            IsCapacitor = capacitor;
            Index = index;
        }

        public static PulseNode Component(int row) => new PulseNode(false, row);
        public static PulseNode Capacitor(int chip) => new PulseNode(true, chip);

        public bool Equals(PulseNode other) => IsCapacitor == other.IsCapacitor && Index == other.Index;
        public override bool Equals(object obj) => obj is PulseNode n && Equals(n);
        public override int GetHashCode() => (IsCapacitor ? 1000 : 0) + Index;
        public override string ToString() => IsCapacitor ? $"C{Index}" : $"#{Index + 1}";
    }

    /// <summary>
    /// Eine Verbindung für Pulse (A-20): von einer Komponente (oder einem Kondensator) über Pins, Leiterbahnen und Dioden zu
    /// einem Ziel. Ein Puls läuft eine Verbindung pro Tick: <see cref="Delay"/> = Zahl der Teilstücke + 1.
    /// </summary>
    public sealed class PulseLink
    {
        public PulseNode From { get; }
        public PulseNode To { get; }
        public int Delay { get; }

        /// <summary>Zellen der Chips unterwegs (für die Anzeige des Wegs), ohne Start und Ziel.</summary>
        public IReadOnlyList<Cell> Path { get; }

        /// <summary>Zelle, in der der Puls startet bzw. ankommt (Pin der Komponente oder Kondensator).</summary>
        public Cell FromCell { get; }
        public Cell ToCell { get; }

        /// <summary>Verstärker (A-21) auf dem Weg: jeder gibt dem Puls mehr Wirkung.</summary>
        public int Amplifiers { get; internal set; }

        public PulseLink(PulseNode from, PulseNode to, IReadOnlyList<Cell> path, Cell fromCell, Cell toCell)
        {
            From = from;
            To = to;
            Path = path ?? Array.Empty<Cell>();
            Delay = Path.Count + 1;
            FromCell = fromCell;
            ToCell = toCell;
        }

        public override string ToString() => $"{From} → {To} ({Delay})";
    }

    /// <summary>
    /// Verdrahtung der Platine (A-20): findet aus Pins und Chips die Pulsverbindungen und die Pin-Boni. Rein geometrisch und
    /// deterministisch (Lesereihenfolge, kürzester Weg).
    /// </summary>
    public static class CircuitWiring
    {
        /// <summary>Wie viele typisierte Pins einer Komponente einen passenden Nachbarn haben.</summary>
        public static int MatchedTypedPins(IReadOnlyList<LogicRow> rows, int index)
        {
            int matched = 0;
            LogicRow row = rows[index];
            foreach (PlacedPin pin in row.Pins)
            {
                if (!pin.IsTyped) continue;
                LogicRow neighbour = RowAt(rows, pin.Facing);
                if (neighbour != null && neighbour != row && neighbour.Skill != null && (neighbour.Skill.Kinds & pin.Kind) != 0) matched++;
            }
            return matched;
        }

        public static List<PulseLink> Links(IReadOnlyList<LogicRow> rows, IReadOnlyList<LogicChip> chips)
        {
            var links = new List<PulseLink>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Skill == null) continue;
                AddFrom(links, rows, chips, PulseNode.Component(i), StartsOf(rows[i]));
            }
            for (int k = 0; k < chips.Count; k++)
            {
                if (chips[k].Kind != ChipKind.Capacitor) continue;
                var starts = new List<(Cell, PinSide)>();
                foreach (PinSide s in chips[k].Openings) starts.Add((chips[k].Rect.Origin, s));
                AddFrom(links, rows, chips, PulseNode.Capacitor(k), starts);
            }
            foreach (PulseLink link in links)
                foreach (Cell cell in link.Path)
                {
                    int chip = ChipIndexAt(chips, cell);
                    if (chip >= 0 && chips[chip].Kind == ChipKind.Amplifier) link.Amplifiers++;
                }
            return links;
        }

        private static List<(Cell, PinSide)> StartsOf(LogicRow row)
        {
            var starts = new List<(Cell, PinSide)>();
            foreach (PlacedPin pin in row.Pins) starts.Add((pin.Cell, pin.Side));
            return starts;
        }

        /// <summary>Breitensuche von allen Ausgängen eines Knotens; je Ziel zählt der kürzeste Weg.</summary>
        private static void AddFrom(List<PulseLink> links, IReadOnlyList<LogicRow> rows, IReadOnlyList<LogicChip> chips, PulseNode from,
            List<(Cell cell, PinSide side)> starts)
        {
            var found = new Dictionary<PulseNode, PulseLink>();
            var order = new List<PulseNode>();
            var visited = new HashSet<(Cell, PinSide)>();
            var queue = new Queue<(Cell cell, PinSide side, List<Cell> path, Cell start)>();
            foreach ((Cell cell, PinSide side) s in starts) queue.Enqueue((s.cell, s.side, new List<Cell>(), s.cell));

            while (queue.Count > 0)
            {
                (Cell cell, PinSide exit, List<Cell> path, Cell start) = queue.Dequeue();
                Cell next = cell.Step(exit);
                PinSide entry = exit.Opposite();

                // Ziel Komponente: dort muss ein Pin zurück zeigen.
                int target = RowIndexAt(rows, next);
                if (target >= 0)
                {
                    if (HasPin(rows[target], next, entry) && !(from.Equals(PulseNode.Component(target))))
                        Add(found, order, new PulseLink(from, PulseNode.Component(target), path, start, next));
                    continue;
                }

                int chipIndex = ChipIndexAt(chips, next);
                if (chipIndex < 0) continue;
                LogicChip chip = chips[chipIndex];
                if (!chip.Definition.Conducts || !Opens(chip, entry)) continue;
                if (chip.Kind == ChipKind.Diode && entry != ChipDefinition.DiodeIn(chip.Turns)) continue;
                if (chip.Kind == ChipKind.Capacitor)
                {
                    if (!from.Equals(PulseNode.Capacitor(chipIndex))) Add(found, order, new PulseLink(from, PulseNode.Capacitor(chipIndex), path, start, next));
                    continue;
                }
                if (!visited.Add((next, entry))) continue;

                var longer = new List<Cell>(path) { next };
                foreach (PinSide out_ in chip.Openings)
                {
                    if (out_ == entry) continue;
                    if (chip.Kind == ChipKind.Diode && out_ != ChipDefinition.DiodeOut(chip.Turns)) continue;
                    queue.Enqueue((next, out_, longer, start));
                }
            }
            foreach (PulseNode n in order) links.Add(found[n]);
        }

        private static void Add(Dictionary<PulseNode, PulseLink> found, List<PulseNode> order, PulseLink link)
        {
            if (found.TryGetValue(link.To, out PulseLink known) && known.Delay <= link.Delay) return;
            if (!found.ContainsKey(link.To)) order.Add(link.To);
            found[link.To] = link;
        }

        private static bool Opens(LogicChip chip, PinSide side)
        {
            foreach (PinSide s in chip.Openings)
                if (s == side) return true;
            return false;
        }

        private static bool HasPin(LogicRow row, Cell cell, PinSide side)
        {
            foreach (PlacedPin pin in row.Pins)
                if (pin.Cell == cell && pin.Side == side) return true;
            return false;
        }

        private static LogicRow RowAt(IReadOnlyList<LogicRow> rows, Cell cell)
        {
            int i = RowIndexAt(rows, cell);
            return i >= 0 ? rows[i] : null;
        }

        private static int RowIndexAt(IReadOnlyList<LogicRow> rows, Cell cell)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Rect.HasValue && rows[i].Rect.Value.Contains(cell)) return i;
            return -1;
        }

        private static int ChipIndexAt(IReadOnlyList<LogicChip> chips, Cell cell)
        {
            for (int i = 0; i < chips.Count; i++)
                if (chips[i].Rect.Origin == cell) return i;
            return -1;
        }
    }
}
