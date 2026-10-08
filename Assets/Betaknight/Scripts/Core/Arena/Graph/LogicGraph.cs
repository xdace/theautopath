using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

namespace Betaknight.Core.Arena
{
    /// <summary>Knotenart im Graph der Platine: Relais (auch Gatter), Komponente, Chip oder Pin (A-20).</summary>
    public enum GraphNodeKind
    {
        /// <summary>Relais oder Gatter (Index in <see cref="LogicBoard.Relays"/>).</summary>
        Block,

        /// <summary>Komponente (Index in <see cref="LogicBoard.Rows"/>).</summary>
        Skill,

        /// <summary>Logik-Chip (Index in <see cref="LogicBoard.Chips"/>).</summary>
        Chip,

        /// <summary>Pin einer Komponente (laufende Nummer über alle Komponenten).</summary>
        Pin,
    }

    /// <summary>Ein Knoten: Baustein oder Skill einer Zeile (Index im Kampf, aus stabilen Ids aufgelöst).</summary>
    public readonly struct GraphNode : IEquatable<GraphNode>
    {
        public readonly GraphNodeKind Kind;
        public readonly int Row;

        public GraphNode(GraphNodeKind kind, int row)
        {
            Kind = kind;
            Row = row;
        }

        public static GraphNode Block(int row) => new GraphNode(GraphNodeKind.Block, row);
        public static GraphNode Skill(int row) => new GraphNode(GraphNodeKind.Skill, row);
        public static GraphNode Chip(int chip) => new GraphNode(GraphNodeKind.Chip, chip);
        public static GraphNode Pin(int pin) => new GraphNode(GraphNodeKind.Pin, pin);

        public bool Equals(GraphNode other) => Kind == other.Kind && Row == other.Row;
        public override bool Equals(object obj) => obj is GraphNode n && Equals(n);
        public override int GetHashCode() => ((int)Kind * 397) ^ Row;
        public override string ToString() => $"{Kind}@{Row}";
    }

    /// <summary>Kantenart im Graph der Platine.</summary>
    public enum GraphEdgeKind
    {
        /// <summary>
        /// Auslöser-Modul. Von einer Komponente: nach ihrer Ausführung. Von einem Relais: wenn es auslöst.
        /// Das Ziel wird eingereiht und castet normal; steht es schon in der Warteschlange, ist der Auslöser verpasst.
        /// </summary>
        Trigger,

        /// <summary>Eingang eines UND-Gatters (Relais → Gatter).</summary>
        And,

        /// <summary>Eingang eines ODER-Gatters (Relais → Gatter).</summary>
        Or,

        /// <summary>Relais versorgt Komponente.</summary>
        Power,

        /// <summary>Eingang eines NICHT-Gatters oder einer Sicherung (Relais → Gatter).</summary>
        Input,

        /// <summary>Pulsverbindung über Pins und Leiterbahnen (Komponente/Kondensator → Komponente/Kondensator).</summary>
        Pulse,

        /// <summary>Pin gehört zur Komponente.</summary>
        PinOf,

        /// <summary>Gatter ist dieser Chip (Relais → Chip).</summary>
        IsChip,
    }

    public sealed class GraphEdge
    {
        public GraphNode From { get; }
        public GraphNode To { get; }
        public GraphEdgeKind Kind { get; }

        /// <summary>Laufzeit eines Pulses in Ticks (nur bei <see cref="GraphEdgeKind.Pulse"/>).</summary>
        public int Delay { get; }

        public GraphEdge(GraphNode from, GraphNode to, GraphEdgeKind kind = GraphEdgeKind.Trigger, int delay = 0)
        {
            From = from;
            To = to;
            Kind = kind;
            Delay = delay;
        }

        public override string ToString() => $"{From} -{Kind}-> {To}";
    }

    /// <summary>
    /// Graph über den Zeilen der Tafel: Knoten sind Bausteine und Skills, Kanten Auslöser (später UND/ODER).
    /// Kreise sind erlaubt; begrenzt werden sie über Cast-Zeit und die Warteschlange (jede Komponente höchstens einmal eingereiht).
    /// </summary>
    public sealed class LogicGraph
    {
        private readonly List<GraphEdge> _edges;

        public IReadOnlyList<GraphEdge> Edges => _edges;

        public LogicGraph(IEnumerable<GraphEdge> edges = null)
        {
            _edges = new List<GraphEdge>(edges ?? Array.Empty<GraphEdge>());
        }

        public static LogicGraph Empty { get; } = new LogicGraph();

        public IEnumerable<GraphEdge> OfKind(GraphEdgeKind kind)
        {
            foreach (GraphEdge e in _edges)
                if (e.Kind == kind) yield return e;
        }

        /// <summary>
        /// Ergänzt Auslöser-Kanten um die Schaltung (A-20): Versorgung, Gatter-Eingänge, Pins und Pulsverbindungen.
        /// Reihenfolge fest (Lesereihenfolge), damit der Kampf deterministisch bleibt.
        /// </summary>
        public static LogicGraph WithCircuit(LogicGraph triggers, IReadOnlyList<LogicRow> rows, IReadOnlyList<LogicRelay> relays,
            IReadOnlyList<LogicChip> chips, IReadOnlyList<PulseLink> links)
        {
            var edges = new List<GraphEdge>(triggers?.Edges ?? Array.Empty<GraphEdge>());
            foreach (LogicRelay r in relays)
            {
                foreach (int row in r.Powered) edges.Add(new GraphEdge(GraphNode.Block(r.Index), GraphNode.Skill(row), GraphEdgeKind.Power));
                if (r.Gate == null) continue;
                GraphEdgeKind kind = r.Gate == ChipKind.And ? GraphEdgeKind.And : r.Gate == ChipKind.Or ? GraphEdgeKind.Or : GraphEdgeKind.Input;
                foreach (int input in r.Inputs) edges.Add(new GraphEdge(GraphNode.Block(input), GraphNode.Block(r.Index), kind));
                if (r.ChipIndex >= 0) edges.Add(new GraphEdge(GraphNode.Block(r.Index), GraphNode.Chip(r.ChipIndex), GraphEdgeKind.IsChip));
            }
            int pin = 0;
            foreach (LogicRow row in rows)
                foreach (PlacedPin _ in row.Pins)
                    edges.Add(new GraphEdge(GraphNode.Pin(pin++), GraphNode.Skill(row.Index), GraphEdgeKind.PinOf));
            foreach (PulseLink l in links)
                edges.Add(new GraphEdge(NodeOf(l.From), NodeOf(l.To), GraphEdgeKind.Pulse, l.Delay));
            return edges.Count == 0 ? Empty : new LogicGraph(edges);
        }

        public static GraphNode NodeOf(PulseNode node) => node.IsCapacitor ? GraphNode.Chip(node.Index) : GraphNode.Skill(node.Index);

        public IEnumerable<GraphEdge> From(GraphNode node)
        {
            foreach (GraphEdge e in _edges)
                if (e.From.Equals(node)) yield return e;
        }

        /// <summary>Hat ein Baustein ausgehende Auslöser? Dann wird er jeden Tick beobachtet.</summary>
        public bool HasEdgesFrom(GraphNode node)
        {
            foreach (GraphEdge e in _edges)
                if (e.From.Equals(node)) return true;
            return false;
        }
    }
}
