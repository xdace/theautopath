namespace Betaknight.Core.Map
{
    /// <summary>Was ein Feld enthält. Die eigentlichen Events (Kampf, Shop, Loot) folgen in späteren Meilensteinen.</summary>
    public enum CellContent
    {
        Empty = 0,
        Enemy = 1,
        Boss = 2,
        Shop = 3,
        Treasure = 4,
        GoldMine = 5,
    }

    public static class CellContentExtensions
    {
        /// <summary>Feindliche Felder unterbrechen eine automatische Mehrfeld-Reise.</summary>
        public static bool IsHostile(this CellContent content) =>
            content == CellContent.Enemy || content == CellContent.Boss;
    }
}
