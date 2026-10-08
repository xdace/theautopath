using System;
using System.Collections.Generic;
using Betaknight.Core.Map;
using E = Betaknight.Core.Encounters.EncounterEffect;

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

        private static EncounterDefinition Minor(string id, string title, string symbol, int weight, int minDistance, string text, params E[] effects) =>
            new EncounterDefinition(id, title, symbol, EncounterSize.Minor, weight, minDistance, text, new EncounterOption(CatalogTexts.EncounterContinue, effects));

        private static EncounterDefinition Medium(string id, string title, string symbol, int weight, int minDistance, string text, params EncounterOption[] options) =>
            new EncounterDefinition(id, title, symbol, EncounterSize.Medium, weight, minDistance, text, options);

        /// <summary>Standard-Events des Spiels.</summary>
        public static EncounterCatalog CreateDefault() => new EncounterCatalog(new[]
        {
            // Kleine Events: wirken sofort.
            Minor("coins", "Scattered Coins", "c", 30, 0, "A few coins glint in the grass.", E.Gold(2, 4)),
            Minor("herbs", "Healing Herbs", "h", 20, 0, "You chew bitter herbs. Your wounds close a little.", E.Heal(2, 4)),
            Minor("shard", "Rune Shard", "s", 20, 0, "A glowing Rune Shard is stuck in the ground.", E.Shards(1)),
            Minor("signpost", "Signpost", "w", 12, 0, "An old signpost reveals what lies nearby.", E.ScoutAround(2)),
            Minor("tracks", "Merchant Tracks", "t", 8, 2, "Wagon tracks lead to a merchant.", E.ScoutNearest(CellContent.Shop)),
            Minor("thorns", "Thorn Thicket", "x", 10, 2, "Thorns scratch at your armor.", E.Damage(1, 2)),

            // Mittlere Events: eine Entscheidung.
            Medium("campfire", "Campfire", "F", 20, 0, "An abandoned campfire is still smoldering.",
                new EncounterOption("Rest (+10 HP)", E.Heal(10)),
                new EncounterOption("Upgrade a rune (one level)", E.UpgradeRune())),
            Medium("wanderer", "Wanderer", "W", 20, 0, "A wanderer offers you a trade.",
                new EncounterOption("Buy a Rune Shard (5 Gold)", 5, E.Shards(1)),
                new EncounterOption("Ask for directions", E.ScoutAround(3))),
            Medium("shrine", "Blood Shrine", "S", 15, 0, "A shrine demands a sacrifice.",
                new EncounterOption("Sacrifice blood (-5 HP, +2 Shards)", E.Damage(5), E.Shards(2)),
                new EncounterOption("Move on")),
            Medium("mercenary", "Wounded Mercenary", "M", 12, 3, "A wounded mercenary leans against a tree.",
                new EncounterOption("Tend his wounds (6 Gold)", 6, E.Shards(1), E.ScoutNearest(CellContent.Treasure)),
                new EncounterOption("Take his purse (+6 Gold, -3 HP)", E.Gold(6), E.Damage(3))),
            Medium("cache", "Buried Cache", "V", 15, 0, "Something lies buried under the rubble.",
                new EncounterOption("Dig it out (-2 HP, +6–9 Gold)", E.Damage(2), E.Gold(6, 9)),
                new EncounterOption("Search carefully (+2–3 Gold)", E.Gold(2, 3))),
        });
    }
}
