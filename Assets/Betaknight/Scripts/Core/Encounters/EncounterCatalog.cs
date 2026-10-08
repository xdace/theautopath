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
            new EncounterDefinition(id, title, symbol, EncounterSize.Minor, weight, minDistance, text, new EncounterOption("Weiter", effects));

        private static EncounterDefinition Medium(string id, string title, string symbol, int weight, int minDistance, string text, params EncounterOption[] options) =>
            new EncounterDefinition(id, title, symbol, EncounterSize.Medium, weight, minDistance, text, options);

        /// <summary>Standard-Events des Spiels.</summary>
        public static EncounterCatalog CreateDefault() => new EncounterCatalog(new[]
        {
            // Kleine Events: wirken sofort.
            Minor("coins", "Verstreute Münzen", "c", 30, 0, "Ein paar Münzen glänzen im Gras.", E.Gold(2, 4)),
            Minor("herbs", "Heilkräuter", "h", 20, 0, "Du kaust bittere Kräuter. Die Wunden schliessen sich ein wenig.", E.Heal(2, 4)),
            Minor("shard", "Runensplitter", "s", 20, 0, "Ein glimmender Runensplitter steckt im Boden.", E.Shards(1)),
            Minor("signpost", "Wegweiser", "w", 12, 0, "Ein alter Wegweiser verrät, was in der Nähe liegt.", E.ScoutAround(2)),
            Minor("tracks", "Händlerspuren", "t", 8, 2, "Wagenspuren führen zu einem Händler.", E.ScoutNearest(CellContent.Shop)),
            Minor("thorns", "Dornengestrüpp", "x", 10, 2, "Dornen kratzen an der Rüstung.", E.Damage(1, 2)),

            // Mittlere Events: eine Entscheidung.
            Medium("campfire", "Lagerfeuer", "F", 20, 0, "Ein verlassenes Lagerfeuer glimmt noch.",
                new EncounterOption("Ausruhen (+10 HP)", E.Heal(10)),
                new EncounterOption("Rune verstärken (eine Stufe)", E.UpgradeRune())),
            Medium("wanderer", "Wanderer", "W", 20, 0, "Ein Wanderer bietet dir einen Handel an.",
                new EncounterOption("Runensplitter kaufen (5 Gold)", 5, E.Shards(1)),
                new EncounterOption("Nach dem Weg fragen", E.ScoutAround(3))),
            Medium("shrine", "Blutschrein", "S", 15, 0, "Ein Schrein verlangt ein Opfer.",
                new EncounterOption("Blut opfern (-5 HP, +2 Splitter)", E.Damage(5), E.Shards(2)),
                new EncounterOption("Weitergehen")),
            Medium("mercenary", "Verletzter Söldner", "M", 12, 3, "Ein verwundeter Söldner lehnt an einem Baum.",
                new EncounterOption("Wunden versorgen (6 Gold)", 6, E.Shards(1), E.ScoutNearest(CellContent.Treasure)),
                new EncounterOption("Seine Börse nehmen (+6 Gold, -3 HP)", E.Gold(6), E.Damage(3))),
            Medium("cache", "Verschütteter Vorrat", "V", 15, 0, "Unter Geröll liegt etwas begraben.",
                new EncounterOption("Ausgraben (-2 HP, +6–9 Gold)", E.Damage(2), E.Gold(6, 9)),
                new EncounterOption("Vorsichtig suchen (+2–3 Gold)", E.Gold(2, 3))),
        });
    }
}
