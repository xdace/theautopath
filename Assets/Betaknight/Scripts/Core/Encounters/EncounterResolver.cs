using System;
using System.Collections.Generic;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Encounters
{
    /// <summary>Wendet die Wirkungen einer Event-Option auf Spielerwerte und Karte an.</summary>
    public sealed class EncounterResolver
    {
        private readonly HexMap _map;
        private readonly ExplorationService _exploration;
        private readonly PlayerStats _stats;
        private readonly Random _random;
        private readonly RuneLoadout _runes;

        public EncounterResolver(HexMap map, ExplorationService exploration, PlayerStats stats, Random random, RuneLoadout runes = null)
        {
            _runes = runes;
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _exploration = exploration ?? throw new ArgumentNullException(nameof(exploration));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Bezahlt die Kosten und wendet alle Wirkungen an. Gibt die Ergebniszeilen zurück
        /// oder null, wenn die Option nicht bezahlbar ist.
        /// </summary>
        public List<string> Apply(EncounterOption option, HexCoord origin)
        {
            if (option == null) throw new ArgumentNullException(nameof(option));
            if (!option.IsAvailable(_stats) || !_stats.TrySpendGold(option.GoldCost)) return null;

            var lines = new List<string>();
            if (option.GoldCost > 0) lines.Add($"-{option.GoldCost} Gold");

            foreach (EncounterEffect effect in option.Effects)
            {
                string line = ApplyEffect(effect, origin);
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
            return lines;
        }

        private string ApplyEffect(EncounterEffect effect, HexCoord origin)
        {
            int amount = effect.Min == effect.Max ? effect.Min : _random.Next(effect.Min, effect.Max + 1);

            switch (effect.Kind)
            {
                case EffectKind.Gold:
                    _stats.AddGold(amount);
                    return $"+{amount} Gold";

                case EffectKind.Heal:
                    int healed = _stats.Heal(amount);
                    return healed > 0 ? $"+{healed} HP" : "Schon voll geheilt";

                case EffectKind.Damage:
                    int dealt = _stats.Damage(amount, lethal: false);
                    return dealt > 0 ? $"-{dealt} HP" : null;

                case EffectKind.MaxHp:
                    _stats.RaiseMaxHp(amount);
                    return $"+{amount} Max-HP";

                case EffectKind.Shards:
                    _stats.AddShards(amount);
                    return amount == 1 ? "+1 Runensplitter" : $"+{amount} Runensplitter";

                case EffectKind.ScoutAround:
                    int scouted = _exploration.ScoutAround(origin, amount);
                    return scouted > 0 ? $"{scouted} Felder ausgekundschaftet" : "Nichts Neues in der Nähe";

                case EffectKind.ScoutNearest:
                    return ScoutNearest(effect.Target, origin);

                case EffectKind.UpgradeRune:
                    return UpgradeRune();

                default:
                    return null;
            }
        }

        private string UpgradeRune()
        {
            int index = _runes?.BestUpgradeTarget() ?? -1;
            if (index < 0) return "Keine Rune lässt sich verstärken";
            _runes.Upgrade(index);
            return $"Rune verstärkt: {_runes.Rows[index].Name}";
        }

        private string ScoutNearest(CellContent target, HexCoord origin)
        {
            HexCell best = null;
            int bestDistance = int.MaxValue;

            // Spiralreihenfolge hält das Ergebnis bei gleicher Entfernung deterministisch.
            foreach (HexCoord coord in HexCoord.Spiral(_map.Center, _map.Radius))
            {
                HexCell cell = _map.GetCell(coord);
                if (cell.Content != target || cell.IsContentKnown) continue;

                int d = coord.DistanceTo(origin);
                if (d < bestDistance)
                {
                    best = cell;
                    bestDistance = d;
                }
            }

            if (best == null) return "Keine Spur gefunden";
            _exploration.Scout(best.Coord);
            return $"{Name(target)} entdeckt";
        }

        private static string Name(CellContent content)
        {
            switch (content)
            {
                case CellContent.Shop: return "Shop";
                case CellContent.Treasure: return "Schatztruhe";
                case CellContent.GoldMine: return "Goldmine";
                case CellContent.Enemy: return "Gegner";
                case CellContent.Boss: return "Boss";
                case CellContent.Elite: return "Elite-Gegner";
                default: return "Ort";
            }
        }
    }
}
