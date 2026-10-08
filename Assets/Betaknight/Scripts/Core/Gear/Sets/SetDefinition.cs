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

        /// <summary>Ist der Bonus für <paramref name="needed"/> Teile bei <paramref name="pieces"/> getragenen Teilen aktiv?</summary>
        public static bool IsActive(int needed, int pieces) => pieces >= needed;

        /// <summary>
        /// Name, Teile und alle Boni, z. B. «Aegis-Firewall 2/3» und darunter «● 2 Teile: …» (aktiv) bzw.
        /// «○ 3 Teile: …» (noch nicht). Ohne Farben; die Anzeige färbt selbst.
        /// </summary>
        public string Describe(int pieces)
        {
            var lines = new List<string> { $"{Name} {pieces}/{MaxPieces}" };
            foreach (KeyValuePair<int, string> bonus in Bonuses)
                lines.Add(CatalogTexts.TierLine(IsActive(bonus.Key, pieces) ? "●" : "○", bonus.Key, bonus.Value));
            return string.Join("\n", lines);
        }

        /// <summary>Aktive Boni als ein Text («2 Teile: …»), leer, wenn noch keiner greift.</summary>
        public string ActiveText(int pieces)
        {
            var lines = new List<string>();
            foreach (KeyValuePair<int, string> bonus in Bonuses)
                if (IsActive(bonus.Key, pieces)) lines.Add(CatalogTexts.ActiveTierLine(bonus.Key, bonus.Value));
            return string.Join("\n", lines);
        }
    }
}
