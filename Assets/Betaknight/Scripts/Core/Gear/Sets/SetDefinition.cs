using System;
using System.Collections.Generic;

namespace Betaknight.Core.Gear
{
    /// <summary>Anzeige-Daten eines Sets: Name und was bei wie vielen Teilen greift.</summary>
    public sealed class SetDefinition
    {
        /// <summary>Ab so vielen Teilen greift der erste Bonus und set-exklusive Runen werden frei.</summary>
        public const int FirstBonusPieces = 2;

        public string Id { get; }
        public string Name { get; }

        /// <summary>Bonustext je benötigter Teilezahl, z. B. 2 → "...", 3 → "...".</summary>
        public IReadOnlyDictionary<int, string> Bonuses { get; }

        public int MaxPieces { get; }

        public SetDefinition(string id, string name, IDictionary<int, string> bonuses, int maxPieces = 3)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            Bonuses = new SortedDictionary<int, string>(bonuses ?? new Dictionary<int, string>());
            MaxPieces = maxPieces;
        }
    }
}
