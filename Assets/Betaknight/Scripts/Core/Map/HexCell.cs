using Betaknight.Core.Hex;

namespace Betaknight.Core.Map
{
    /// <summary>
    /// Reiner Datencontainer für ein Feld. Änderungen laufen über <see cref="HexMap"/>,
    /// damit Beobachter (z. B. die Darstellung) zuverlässig benachrichtigt werden.
    /// </summary>
    public sealed class HexCell
    {
        public HexCoord Coord { get; }
        public CellContent Content { get; internal set; }
        public CellVisibility Visibility { get; internal set; }

        /// <summary>Vorbereitet für Hindernisse (Berge, Abgründe). Aktuell sind alle Felder begehbar.</summary>
        public bool IsWalkable { get; internal set; } = true;

        /// <summary>Wie oft der Spieler das Feld betreten hat. Grundlage für spätere Gegneralarme beim Zurückreisen.</summary>
        public int VisitCount { get; internal set; }

        public HexCell(HexCoord coord, CellContent content)
        {
            Coord = coord;
            Content = content;
            Visibility = CellVisibility.Hidden;
        }

        public override string ToString() => $"{Coord} {Content} [{Visibility}]";
    }
}
