using System;

namespace Betaknight.Core.Runes
{
    /// <summary>
    /// Ausrichtung einer Rune. Angebote bevorzugen Tags, die der Spieler schon hat, damit ein Build entsteht.
    /// </summary>
    public enum RuneTag
    {
        /// <summary>Angriff und Druck.</summary>
        Blade = 0,

        /// <summary>Verteidigung, Block und Konter.</summary>
        Shield = 1,

        /// <summary>Takt und Zähler.</summary>
        Spark = 2,

        /// <summary>Notfall, Feuer und Risiko.</summary>
        Ember = 3,
    }

    public static class RuneTagExtensions
    {
        public static string DisplayName(this RuneTag tag)
        {
            switch (tag)
            {
                case RuneTag.Blade: return "Klinge";
                case RuneTag.Shield: return "Schild";
                case RuneTag.Spark: return "Funke";
                case RuneTag.Ember: return "Glut";
                default: return tag.ToString();
            }
        }
    }

    /// <summary>
    /// Eine Logik-Rune als reine Daten: eine Bedingung ("Wann") für eine Zeile der Logik-Tafel.
    /// Was dann passiert, kommt aus der Ausrüstung. Die Auswertung folgt mit der Kampfarena.
    /// </summary>
    public sealed class RuneDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RuneTag Tag { get; }

        /// <summary>Relative Häufigkeit in Angeboten.</summary>
        public int Weight { get; }

        public RuneDefinition(string id, string name, RuneTag tag, string description, int weight = 10)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Id = id;
            Name = name ?? id;
            Tag = tag;
            Description = description ?? string.Empty;
            Weight = weight;
        }

        public override string ToString() => $"{Name} [{Tag.DisplayName()}]";
    }
}
