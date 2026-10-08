using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Knotenart im Graph der Logik-Tafel: der Logikbaustein (Bedingung) oder der Skill einer Zeile.</summary>
    public enum GraphNodeKind
    {
        Block,
        Skill,
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

        public bool Equals(GraphNode other) => Kind == other.Kind && Row == other.Row;
        public override bool Equals(object obj) => obj is GraphNode n && Equals(n);
        public override int GetHashCode() => ((int)Kind * 397) ^ Row;
        public override string ToString() => $"{Kind}@{Row}";
    }

    /// <summary>Kantenart. Heute nur Auslöser; UND/ODER sind für spätere Verknüpfungen vorgesehen.</summary>
    public enum GraphEdgeKind
    {
        /// <summary>
        /// Von einem Skill: nach seiner Ausführung. Von einem Baustein: wenn er erfüllt wird (Wechsel von falsch zu wahr).
        /// Das Ziel startet seinen Skill mit voller Cast-Zeit; ist er nicht bereit, verfällt der Auslöser.
        /// </summary>
        Trigger,
        And,
        Or,
    }

    public sealed class GraphEdge
    {
        public GraphNode From { get; }
        public GraphNode To { get; }
        public GraphEdgeKind Kind { get; }

        public GraphEdge(GraphNode from, GraphNode to, GraphEdgeKind kind = GraphEdgeKind.Trigger)
        {
            From = from;
            To = to;
            Kind = kind;
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
