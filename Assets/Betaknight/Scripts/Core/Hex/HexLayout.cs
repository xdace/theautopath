using System;

namespace Betaknight.Core.Hex
{
    /// <summary>2D-Weltposition ohne Unity-Abhängigkeit.</summary>
    public readonly struct HexPoint
    {
        public readonly float X;
        public readonly float Y;

        public HexPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }

    /// <summary>
    /// Umrechnung zwischen Hex-Koordinaten und Weltpositionen für spitz-oben Hexfelder.
    /// Die Y-Achse zeigt wie in Unity nach oben, d. h. NE liegt rechts oberhalb.
    /// </summary>
    public sealed class HexLayout
    {
        private static readonly double Sqrt3 = Math.Sqrt(3.0);

        /// <summary>Aussenradius (Mittelpunkt bis Ecke) in Welteinheiten.</summary>
        public float Size { get; }
        public HexPoint Origin { get; }

        public HexLayout(float size, HexPoint origin = default)
        {
            if (size <= 0f) throw new ArgumentOutOfRangeException(nameof(size));
            Size = size;
            Origin = origin;
        }

        /// <summary>Abstand zweier horizontaler Nachbarn.</summary>
        public float HorizontalSpacing => (float)(Sqrt3 * Size);

        /// <summary>Abstand zweier Reihen.</summary>
        public float VerticalSpacing => 1.5f * Size;

        public HexPoint ToWorld(HexCoord coord)
        {
            double x = Size * (Sqrt3 * coord.Q + Sqrt3 / 2.0 * coord.R);
            double y = -Size * (1.5 * coord.R);
            return new HexPoint((float)(x + Origin.X), (float)(y + Origin.Y));
        }

        public HexCoord FromWorld(float worldX, float worldY)
        {
            double x = (worldX - Origin.X) / Size;
            double y = -(worldY - Origin.Y) / Size;
            double q = Sqrt3 / 3.0 * x - 1.0 / 3.0 * y;
            double r = 2.0 / 3.0 * y;
            return HexCoord.Round(q, r);
        }
    }
}
