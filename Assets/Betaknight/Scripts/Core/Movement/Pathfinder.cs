using System.Collections.Generic;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;

namespace Betaknight.Core.Movement
{
    /// <summary>
    /// Kürzeste Route über bekanntes Gelände (Breitensuche, alle Schritte kosten 1 Zug).
    /// Zwischenfelder müssen erforscht sein, das Ziel darf auch ein "?"-Feld sein.
    /// </summary>
    public static class Pathfinder
    {
        /// <summary>
        /// Liefert die Schritte von start nach goal ohne das Startfeld,
        /// eine leere Liste wenn start == goal, oder null wenn keine Route existiert.
        /// </summary>
        public static List<HexCoord> FindPath(HexMap map, HexCoord start, HexCoord goal)
        {
            if (!map.TryGetCell(goal, out HexCell goalCell)) return null;
            if (start == goal) return new List<HexCoord>();
            if (MovementRules.CheckEnterable(goalCell) != MoveFailure.None) return null;

            var cameFrom = new Dictionary<HexCoord, HexCoord> { [start] = start };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(start);

            while (frontier.Count > 0)
            {
                HexCoord current = frontier.Dequeue();

                foreach (HexCoord next in current.Neighbors())
                {
                    if (cameFrom.ContainsKey(next)) continue;
                    if (!map.TryGetCell(next, out HexCell nextCell)) continue;

                    if (next == goal)
                    {
                        cameFrom[next] = current;
                        return Reconstruct(cameFrom, start, goal);
                    }

                    if (!MovementRules.IsKnownRoute(nextCell)) continue;

                    cameFrom[next] = current;
                    frontier.Enqueue(next);
                }
            }

            return null;
        }

        private static List<HexCoord> Reconstruct(Dictionary<HexCoord, HexCoord> cameFrom, HexCoord start, HexCoord goal)
        {
            var path = new List<HexCoord>();
            HexCoord current = goal;
            while (current != start)
            {
                path.Add(current);
                current = cameFrom[current];
            }
            path.Reverse();
            return path;
        }
    }
}
