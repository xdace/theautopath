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
        private readonly Circuit.CircuitBoard _runes;

        public EncounterResolver(HexMap map, ExplorationService exploration, PlayerStats stats, Random random, Circuit.CircuitBoard runes = null)
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
            if (option.GoldCost > 0) lines.Add(SessionTexts.GoldCost(option.GoldCost));

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
                    return SessionTexts.GoldGain(amount);

                case EffectKind.Heal:
                    int healed = _stats.Heal(amount);
                    return healed > 0 ? SessionTexts.HpGain(healed) : SessionTexts.AlreadyFullHealth;

                case EffectKind.Damage:
                    int dealt = _stats.Damage(amount, lethal: false);
                    return dealt > 0 ? SessionTexts.HpLoss(dealt) : null;

                case EffectKind.MaxHp:
                    _stats.RaiseMaxHp(amount);
                    return SessionTexts.MaxHpGain(amount);

                case EffectKind.Shards:
                    _stats.AddShards(amount);
                    return SessionTexts.ShardGain(amount);

                case EffectKind.ScoutAround:
                    int scouted = _exploration.ScoutAround(origin, amount);
                    return scouted > 0 ? SessionTexts.TilesScouted(scouted) : SessionTexts.NothingNearby;

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
            if (index < 0) return SessionTexts.NoRuneToUpgrade;
            _runes.Upgrade(index);
            return SessionTexts.RuneUpgraded(_runes.Relays[index].Name);
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

            if (best == null) return SessionTexts.NoTrailFound;
            _exploration.Scout(best.Coord);
            return SessionTexts.Discovered(Name(target));
        }

        private static string Name(CellContent content)
        {
            switch (content)
            {
                case CellContent.Shop: return SessionTexts.PlaceShop;
                case CellContent.Treasure: return SessionTexts.PlaceTreasure;
                case CellContent.GoldMine: return SessionTexts.PlaceGoldMine;
                case CellContent.Enemy: return SessionTexts.PlaceEnemy;
                case CellContent.Boss: return SessionTexts.PlaceBoss;
                case CellContent.Elite: return SessionTexts.PlaceElite;
                default: return SessionTexts.PlaceOther;
            }
        }
    }
}
