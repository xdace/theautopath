using System;
using System.Collections.Generic;

namespace Betaknight.Core.Encounters
{
    /// <summary>
    /// Beschreibung eines kleinen oder mittleren Feld-Events. Reine Daten, die Wirkung steckt in den Optionen.
    /// </summary>
    public sealed class EncounterDefinition
    {
        /// <summary>Stabiler Schlüssel, wird auf dem Feld gespeichert.</summary>
        public string Id { get; }
        public string Title { get; }

        /// <summary>Kurzes ASCII-Symbol für die Karte.</summary>
        public string Symbol { get; }

        public EncounterSize Size { get; }

        /// <summary>Relative Häufigkeit innerhalb derselben Grösse.</summary>
        public int Weight { get; }

        /// <summary>Taucht erst ab dieser Entfernung vom Start auf.</summary>
        public int MinDistance { get; }

        /// <summary>Kurzer Beschreibungstext für das Event-Fenster bzw. den Hinweis.</summary>
        public string Text { get; }

        public IReadOnlyList<EncounterOption> Options { get; }

        public EncounterDefinition(string id, string title, string symbol, EncounterSize size, int weight, int minDistance,
            string text, params EncounterOption[] options)
        {
            if (options == null || options.Length == 0) throw new ArgumentException("Ein Event braucht mindestens eine Option.", nameof(options));
            if (size == EncounterSize.Minor && options.Length != 1)
                throw new ArgumentException("Kleine Events haben genau eine Option.", nameof(options));
            if (size == EncounterSize.Medium && options.Length < 2)
                throw new ArgumentException("Mittlere Events brauchen eine Entscheidung (mind. 2 Optionen).", nameof(options));
            if (size == EncounterSize.Medium && options[options.Length - 1].GoldCost > 0)
                throw new ArgumentException("Die letzte Option eines mittleren Events muss immer wählbar sein.", nameof(options));

            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (size == EncounterSize.Major) throw new ArgumentException("Grosse Events laufen über CellContent.", nameof(size));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));

            Id = id;
            Title = title ?? id;
            Symbol = symbol ?? string.Empty;
            Size = size;
            Weight = weight;
            MinDistance = minDistance;
            Text = text ?? string.Empty;
            Options = options;
        }

        public override string ToString() => $"{Id} ({Size})";
    }
}
