using System;
using System.Collections.Generic;

namespace Betaknight.Core.Hex
{
    /// <summary>
    /// Axiale Hex-Koordinate (q, r). Die dritte Kubus-Achse s ergibt sich aus q + r + s = 0.
    /// Unveränderlicher Werttyp, als Dictionary-Schlüssel geeignet.
    /// </summary>
    [Serializable]
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public static readonly HexCoord Zero = new HexCoord(0, 0);

        public readonly int Q;
        public readonly int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        // Reihenfolge entspricht HexDirection (E, NE, NW, W, SW, SE).
        private static readonly HexCoord[] DirectionOffsets =
        {
            new HexCoord(+1, 0),
            new HexCoord(+1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, +1),
            new HexCoord(0, +1),
        };

        public static HexCoord DirectionOffset(HexDirection direction) => DirectionOffsets[(int)direction];

        public HexCoord Neighbor(HexDirection direction) => this + DirectionOffset(direction);

        public IEnumerable<HexCoord> Neighbors()
        {
            for (int i = 0; i < DirectionOffsets.Length; i++)
            {
                yield return this + DirectionOffsets[i];
            }
        }

        public int DistanceTo(HexCoord other)
        {
            HexCoord d = this - other;
            return (Math.Abs(d.Q) + Math.Abs(d.R) + Math.Abs(d.S)) / 2;
        }

        public bool IsAdjacentTo(HexCoord other) => DistanceTo(other) == 1;

        /// <summary>Alle Felder mit exakt dem gegebenen Abstand zu center.</summary>
        public static IEnumerable<HexCoord> Ring(HexCoord center, int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            if (radius == 0)
            {
                yield return center;
                yield break;
            }

            HexCoord current = center + DirectionOffset(HexDirection.SW) * radius;
            for (int side = 0; side < 6; side++)
            {
                for (int step = 0; step < radius; step++)
                {
                    yield return current;
                    current = current.Neighbor((HexDirection)side);
                }
            }
        }

        /// <summary>Alle Felder mit Abstand 0..radius zu center, von innen nach aussen.</summary>
        public static IEnumerable<HexCoord> Spiral(HexCoord center, int radius)
        {
            for (int k = 0; k <= radius; k++)
            {
                foreach (HexCoord c in Ring(center, k))
                {
                    yield return c;
                }
            }
        }

        /// <summary>Rundet gebrochene Kubus-Koordinaten auf das nächste Hexfeld.</summary>
        public static HexCoord Round(double q, double r)
        {
            double s = -q - r;
            double rq = Math.Round(q);
            double rr = Math.Round(r);
            double rs = Math.Round(s);

            double dq = Math.Abs(rq - q);
            double dr = Math.Abs(rr - r);
            double ds = Math.Abs(rs - s);

            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            return new HexCoord((int)rq, (int)rr);
        }

        public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);
        public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);
        public static HexCoord operator *(HexCoord a, int k) => new HexCoord(a.Q * k, a.R * k);
        public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
        public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => unchecked((Q * 397) ^ R);
        public override string ToString() => $"({Q}, {R})";
    }
}
