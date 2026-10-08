namespace Betaknight.Core.Encounters
{
    /// <summary>Wie gross ein Feld-Event ist. Bestimmt Ablauf und Darstellung.</summary>
    public enum EncounterSize
    {
        /// <summary>Wirkt sofort beim Betreten, nur ein kurzer Hinweis.</summary>
        Minor = 0,

        /// <summary>Kleines Fenster mit einer Entscheidung zwischen mehreren Optionen.</summary>
        Medium = 1,

        /// <summary>Eigene Szene: Kampf, Truhe, Shop, Goldmine. Liegt als <see cref="Map.CellContent"/> auf dem Feld.</summary>
        Major = 2,
    }
}
