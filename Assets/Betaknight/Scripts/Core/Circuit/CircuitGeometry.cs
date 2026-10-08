using System;
using System.Collections.Generic;

namespace Betaknight.Core.Circuit
{
    /// <summary>Eine Zelle der Platine. (0, 0) ist oben links; X wächst nach rechts, Y nach unten.</summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Cell c && Equals(c);
        public override int GetHashCode() => X * 397 ^ Y;
        public static bool operator ==(Cell a, Cell b) => a.Equals(b);
        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);
        public override string ToString() => $"({X}, {Y})";

        /// <summary>Lesereihenfolge: Zeile für Zeile von oben, in der Zeile von links.</summary>
        public static int CompareReading(Cell a, Cell b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
    }

    /// <summary>Grösse einer Komponente in Zellen (Breite × Höhe), als Daten je Skill. Drehen tauscht Breite und Höhe.</summary>
    public readonly struct Shape : IEquatable<Shape>
    {
        public readonly int Width;
        public readonly int Height;

        public Shape(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
        }

        public static Shape One => new Shape(1, 1);

        public int Cells => Width * Height;
        public Shape Rotated => new Shape(Height, Width);
        public Shape Turned(bool rotated) => rotated ? Rotated : this;
        public bool IsSquare => Width == Height;

        public bool Equals(Shape other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is Shape s && Equals(s);
        public override int GetHashCode() => Width * 31 + Height;

        /// <summary>«2×1»</summary>
        public override string ToString() => $"{Width}×{Height}";
    }

    /// <summary>Ein Rechteck auf der Platine: die Zellen einer Komponente, eines Relais oder des Kerns.</summary>
    public readonly struct CellRect
    {
        public readonly Cell Origin;
        public readonly Shape Shape;

        public CellRect(Cell origin, Shape shape)
        {
            Origin = origin;
            Shape = shape;
        }

        public CellRect(int x, int y, int width = 1, int height = 1) : this(new Cell(x, y), new Shape(width, height)) { }

        public int Left => Origin.X;
        public int Top => Origin.Y;
        public int Right => Origin.X + Shape.Width; // exklusiv
        public int Bottom => Origin.Y + Shape.Height; // exklusiv

        public bool Contains(Cell c) => c.X >= Left && c.X < Right && c.Y >= Top && c.Y < Bottom;

        public bool Overlaps(CellRect other) => Left < other.Right && other.Left < Right && Top < other.Bottom && other.Top < Bottom;

        /// <summary>Berühren sich zwei Rechtecke an einer Kante (nicht nur an einer Ecke) und überlappen nicht?</summary>
        public bool Touches(CellRect other)
        {
            if (Overlaps(other)) return false;
            bool sideBySide = (Right == other.Left || other.Right == Left) && Top < other.Bottom && other.Top < Bottom;
            bool stacked = (Bottom == other.Top || other.Bottom == Top) && Left < other.Right && other.Left < Right;
            return sideBySide || stacked;
        }

        public bool FitsIn(int width, int height) => Left >= 0 && Top >= 0 && Right <= width && Bottom <= height;

        public IEnumerable<Cell> Cells()
        {
            for (int y = Top; y < Bottom; y++)
                for (int x = Left; x < Right; x++)
                    yield return new Cell(x, y);
        }

        public override string ToString() => $"{Origin} {Shape}";
    }
}
