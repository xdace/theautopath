using System;
using System.Collections.Generic;

namespace Betaknight.Core.Encounters
{
    /// <summary>Alle kleinen und mittleren Events, nachschlagbar über die Id.</summary>
    public sealed class EncounterCatalog
    {
        private readonly Dictionary<string, EncounterDefinition> _byId = new Dictionary<string, EncounterDefinition>();
        private readonly List<EncounterDefinition> _all = new List<EncounterDefinition>();

        public IReadOnlyList<EncounterDefinition> All => _all;

        public EncounterCatalog(IEnumerable<EncounterDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            foreach (EncounterDefinition d in definitions)
            {
                if (_byId.ContainsKey(d.Id)) throw new ArgumentException($"Doppelte Event-Id {d.Id}.");
                _byId.Add(d.Id, d);
                _all.Add(d);
            }
        }

        public bool TryGet(string id, out EncounterDefinition definition)
        {
            definition = null;
            return id != null && _byId.TryGetValue(id, out definition);
        }

        public EncounterDefinition Get(string id)
        {
            if (!TryGet(id, out EncounterDefinition d)) throw new KeyNotFoundException($"Unbekanntes Event {id}.");
            return d;
        }

        /// <summary>Standard-Events des Spiels.</summary>
        public static EncounterCatalog CreateDefault() => new EncounterCatalog(new[]
        {
            // Kleine Events: wirken sofort.
            new EncounterDefinition("coins", "Verstreute Münzen", "c", EncounterSize.Minor, 30),
            new EncounterDefinition("herbs", "Heilkräuter", "h", EncounterSize.Minor, 20),
            new EncounterDefinition("shard", "Runensplitter", "s", EncounterSize.Minor, 20),
            new EncounterDefinition("signpost", "Wegweiser", "w", EncounterSize.Minor, 12),
            new EncounterDefinition("tracks", "Händlerspuren", "t", EncounterSize.Minor, 8, minDistance: 2),
            new EncounterDefinition("thorns", "Dornengestrüpp", "x", EncounterSize.Minor, 10, minDistance: 2),

            // Mittlere Events: eine Entscheidung.
            new EncounterDefinition("campfire", "Lagerfeuer", "F", EncounterSize.Medium, 20),
            new EncounterDefinition("wanderer", "Wanderer", "W", EncounterSize.Medium, 20),
            new EncounterDefinition("shrine", "Blutschrein", "S", EncounterSize.Medium, 15),
            new EncounterDefinition("mercenary", "Verletzter Söldner", "M", EncounterSize.Medium, 12, minDistance: 3),
            new EncounterDefinition("cache", "Verschütteter Vorrat", "V", EncounterSize.Medium, 15),
        });
    }
}
