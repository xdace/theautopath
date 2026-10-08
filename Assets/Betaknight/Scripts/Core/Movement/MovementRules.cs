using Betaknight.Core.Hex;
using Betaknight.Core.Map;

namespace Betaknight.Core.Movement
{
    public enum MoveFailure
    {
        None = 0,
        OutOfBounds,
        NotAdjacent,
        Hidden,
        Blocked,
    }

    /// <summary>Zentrale Regeln, welche Felder betreten werden dürfen.</summary>
    public static class MovementRules
    {
        /// <summary>Darf der Spieler in einem einzelnen Schritt von from nach to?</summary>
        public static MoveFailure CheckStep(HexMap map, HexCoord from, HexCoord to)
        {
            if (!map.TryGetCell(to, out HexCell cell)) return MoveFailure.OutOfBounds;
            if (!from.IsAdjacentTo(to)) return MoveFailure.NotAdjacent;
            return CheckEnterable(cell);
        }

        public static MoveFailure CheckEnterable(HexCell cell)
        {
            if (cell == null) return MoveFailure.OutOfBounds;
            if (cell.Visibility == CellVisibility.Hidden) return MoveFailure.Hidden;
            if (!cell.IsWalkable) return MoveFailure.Blocked;
            return MoveFailure.None;
        }

        /// <summary>
        /// Darf ein Feld als Zwischenstation einer Reise genutzt werden?
        /// Nur bereits erforschte Felder gelten als bekannte Route.
        /// </summary>
        public static bool IsKnownRoute(HexCell cell) =>
            cell != null && cell.IsWalkable && cell.Visibility == CellVisibility.Explored;
    }
}
