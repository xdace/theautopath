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

        /// <summary>Id im Event-Katalog, nur bei <see cref="CellContent.Encounter"/> gesetzt.</summary>
        public string EncounterId { get; internal set; }

        /// <summary>True, sobald das Event des Feldes ausgelöst wurde. Jedes Event wirkt nur einmal.</summary>
        public bool IsResolved { get; internal set; }

        /// <summary>
        /// Inhalt ist bekannt, obwohl das Feld noch nicht betreten wurde (z. B. durch einen Wegweiser).
        /// Wirkt sich nur auf die Anzeige aus, nicht auf Routen.
        /// </summary>
        public bool IsScouted { get; internal set; }

        /// <summary>Darf die Darstellung den Inhalt zeigen?</summary>
        public bool IsContentKnown => Visibility == CellVisibility.Explored || IsScouted;

        /// <summary>Hat das Feld noch ein offenes Event?</summary>
        public bool HasPendingEvent => Content != CellContent.Empty && !IsResolved;

        /// <summary>Vorbereitet für Hindernisse (Berge, Abgründe). Aktuell sind alle Felder begehbar.</summary>
        public bool IsWalkable { get; internal set; } = true;

        /// <summary>Wie oft der Spieler das Feld betreten hat. Grundlage für spätere Gegneralarme beim Zurückreisen.</summary>
        public int VisitCount { get; internal set; }

        public HexCell(HexCoord coord, CellContent content, string encounterId = null)
        {
            if (content == CellContent.Encounter && string.IsNullOrEmpty(encounterId))
                throw new System.ArgumentException("Encounter-Felder brauchen eine Event-Id.", nameof(encounterId));

            Coord = coord;
            Content = content;
            EncounterId = content == CellContent.Encounter ? encounterId : null;
            Visibility = CellVisibility.Hidden;
        }

        public override string ToString() =>
            Content == CellContent.Encounter ? $"{Coord} {EncounterId} [{Visibility}]" : $"{Coord} {Content} [{Visibility}]";
    }
}
