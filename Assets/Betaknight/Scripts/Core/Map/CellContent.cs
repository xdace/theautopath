namespace Betaknight.Core.Map
{
    /// <summary>
    /// Was ein Feld enthält. Jedes Feld ausser dem Startfeld trägt ein Event:
    /// entweder ein kleines/mittleres <see cref="Encounter"/> (Details über <see cref="HexCell.EncounterId"/>)
    /// oder einen grossen Inhalt wie Kampf, Truhe oder Shop.
    /// </summary>
    public enum CellContent
    {
        /// <summary>Kein Event. Nur noch für das Startfeld.</summary>
        Empty = 0,
        Enemy = 1,
        Boss = 2,
        Shop = 3,
        Treasure = 4,
        GoldMine = 5,

        /// <summary>Kleines oder mittleres Event aus dem <see cref="Encounters.EncounterCatalog"/>.</summary>
        Encounter = 6,
    }

    public static class CellContentExtensions
    {
        /// <summary>Feindliche Felder unterbrechen eine automatische Mehrfeld-Reise.</summary>
        public static bool IsHostile(this CellContent content) =>
            content == CellContent.Enemy || content == CellContent.Boss;

        /// <summary>Grosse Events, die eine eigene Szene bekommen (Kampf, Truhe, Shop, Goldmine).</summary>
        public static bool IsMajor(this CellContent content) =>
            content != CellContent.Empty && content != CellContent.Encounter;
    }
}
