namespace Betaknight.Core.Map
{
    /// <summary>Fog-of-War-Zustand eines Feldes.</summary>
    public enum CellVisibility
    {
        /// <summary>Unbekannt und ausser Sicht. Nicht betretbar.</summary>
        Hidden = 0,

        /// <summary>In Sichtweite, Inhalt aber unbekannt. Wird als "?" dargestellt und kann betreten werden.</summary>
        Unexplored = 1,

        /// <summary>Bereits besucht, Inhalt bekannt. Teil der frei bereisbaren Routen.</summary>
        Explored = 2,
    }
}
