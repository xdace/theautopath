using System.Collections.Generic;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;

namespace Betaknight.Core
{
    /// <summary>Warum eine Reise vor dem Ziel anhält.</summary>
    public enum RouteStop
    {
        None,
        Unexplored,
        Enemy,
        MineRaid,
    }

    /// <summary>Warum ein Ziel nicht erreichbar ist.</summary>
    public enum RouteProblem
    {
        None,
        Here,
        OutOfBounds,
        Hidden,
        Blocked,
        NoKnownPath,
        Busy,
        GameOver,
    }

    /// <summary>
    /// Vorschau einer Reise für den Cursor-Tooltip: Schritte, Zugkosten, wo und warum sie anhält, ob der Boss beim Ankommen
    /// kommt, und bei ungültigen Zielen der Grund.
    /// </summary>
    public sealed class RoutePreview
    {
        public HexCoord Target { get; internal set; }
        public List<HexCoord> Steps { get; internal set; }
        public RouteProblem Problem { get; internal set; }
        public bool IsValid => Problem == RouteProblem.None;

        /// <summary>Index in <see cref="Steps"/>, an dem die Reise anhält (letzter Schritt, wenn sie durchläuft).</summary>
        public int StopIndex { get; internal set; } = -1;
        public RouteStop StopReason { get; internal set; }
        public HexCoord StopAt => StopIndex >= 0 ? Steps[StopIndex] : Target;
        public bool StopsEarly => StopIndex >= 0 && StopIndex < Steps.Count - 1;

        /// <summary>Eine Reise kostet immer einen Zug, egal wie viele Felder.</summary>
        public int Turns => IsValid ? 1 : 0;

        /// <summary>Mit diesem Zug ist der Boss fällig: Er kommt dort, wo die Reise endet.</summary>
        public bool BossArrives { get; internal set; }

        /// <summary>Gefahrenstufe am Ende der Reise (dort trifft der Boss ein bzw. wird gekämpft).</summary>
        public int TierAtStop { get; internal set; }
    }

    public sealed partial class OverworldSession
    {
        /// <summary>
        /// Gefahrenstufe eines Kampffeldes (Gegner, Elite, Boss, angegriffene Mine), -1 für andere Felder. Gleiche Rechnung wie
        /// beim Kampf.
        /// </summary>
        public int DangerAt(HexCoord coord)
        {
            if (!Map.TryGetCell(coord, out HexCell cell) || cell.IsResolved) return -1;
            switch (cell.Content)
            {
                case CellContent.Enemy:
                case CellContent.Boss: return TierAt(coord);
                case CellContent.Elite: return TierAt(coord) + Progression.EliteTierBonus;
                case CellContent.GoldMine: return cell.IsUnderAttack ? TierAt(coord) + MineRaidTierBonus : -1;
                default: return -1;
            }
        }

        /// <summary>Plant die Reise zu <paramref name="target"/> und sagt, was dabei passiert (siehe <see cref="RoutePreview"/>).</summary>
        public RoutePreview PreviewRoute(HexCoord target)
        {
            var preview = new RoutePreview { Target = target, Steps = new List<HexCoord>() };
            if (IsGameOver) { preview.Problem = RouteProblem.GameOver; return preview; }
            if (IsBusy) { preview.Problem = RouteProblem.Busy; return preview; }
            if (!Map.TryGetCell(target, out HexCell goal)) { preview.Problem = RouteProblem.OutOfBounds; return preview; }
            if (target == Player.Position) { preview.Problem = RouteProblem.Here; return preview; }
            MoveFailure enter = MovementRules.CheckEnterable(goal);
            if (enter == MoveFailure.Hidden) { preview.Problem = RouteProblem.Hidden; return preview; }
            if (enter != MoveFailure.None) { preview.Problem = RouteProblem.Blocked; return preview; }

            List<HexCoord> path = PlanRoute(target);
            if (path == null || path.Count == 0) { preview.Problem = RouteProblem.NoKnownPath; return preview; }
            preview.Steps = path;

            // Gleiche Regel wie TryTravelStep: neues Feld, Gegner oder angegriffene Mine halten die Reise an.
            preview.StopIndex = path.Count - 1;
            for (int i = 0; i < path.Count; i++)
            {
                HexCell cell = Map.GetCell(path[i]);
                RouteStop reason = cell.Content.IsHostile() ? RouteStop.Enemy
                    : cell.IsUnderAttack ? RouteStop.MineRaid
                    : cell.VisitCount == 0 ? RouteStop.Unexplored
                    : RouteStop.None;
                if (reason == RouteStop.None) continue;
                preview.StopIndex = i;
                preview.StopReason = reason;
                break;
            }
            preview.BossArrives = TurnsUntilBoss == 1;
            preview.TierAtStop = TierAt(preview.StopAt);
            return preview;
        }
    }
}
