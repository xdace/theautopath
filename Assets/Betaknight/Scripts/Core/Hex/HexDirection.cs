namespace Betaknight.Core.Hex
{
    /// <summary>
    /// Die sechs Nachbarrichtungen eines spitz-oben ("pointy-top") Hexfelds,
    /// gegen den Uhrzeigersinn ab Osten.
    /// </summary>
    public enum HexDirection
    {
        E = 0,
        NE = 1,
        NW = 2,
        W = 3,
        SW = 4,
        SE = 5,
    }

    public static class HexDirectionExtensions
    {
        public static HexDirection Opposite(this HexDirection direction) => (HexDirection)(((int)direction + 3) % 6);
    }
}
