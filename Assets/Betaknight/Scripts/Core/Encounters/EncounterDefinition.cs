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

        public EncounterDefinition(string id, string title, string symbol, EncounterSize size, int weight, int minDistance = 0)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (size == EncounterSize.Major) throw new ArgumentException("Grosse Events laufen über CellContent.", nameof(size));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));

            Id = id;
            Title = title ?? id;
            Symbol = symbol ?? string.Empty;
            Size = size;
            Weight = weight;
            MinDistance = minDistance;
        }

        public override string ToString() => $"{Id} ({Size})";
    }
}
