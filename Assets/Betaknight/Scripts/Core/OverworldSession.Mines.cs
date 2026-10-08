using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Turns;

namespace Betaknight.Core
{
    /// <summary>Ein Angriff auf eine eigene Goldmine. Bis zur Frist verteidigen, sonst ist die Mine verloren.</summary>
    public sealed class MineRaid
    {
        public HexCoord Coord { get; }
        public int DeadlineTurn { get; }

        /// <summary>Frist verstrichen: die Mine bringt kein Gold mehr, bis sie zurückerobert ist.</summary>
        public bool IsLost { get; internal set; }

        public MineRaid(HexCoord coord, int deadlineTurn)
        {
            Coord = coord;
            DeadlineTurn = deadlineTurn;
        }

        public int TurnsLeft(int currentTurn) => Math.Max(0, DeadlineTurn - currentTurn);
    }

    /// <summary>
    /// Goldminen-Verteidigung: In festen Abständen greift eine Gegnergruppe eine eigene Mine an.
    /// Der Kampf dort läuft mit Kontext "Goldmine" (Schrott-Ernter, Rune "Auf Goldmine").
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Alle so viele Züge wird eine eigene Mine angegriffen, sofern keine schon angegriffen ist.</summary>
        public const int MineRaidInterval = 8;

        /// <summary>So viele Züge bleiben, um die Mine zu verteidigen.</summary>
        public const int MineRaidTurns = 6;

        /// <summary>Verteidiger kämpfen um so viele Stufen härter als die Entfernung der Mine.</summary>
        public const int MineRaidTierBonus = 1;

        public const int MineDefenseGold = 4;

        private readonly HashSet<HexCoord> _mines = new HashSet<HexCoord>();
        private readonly Dictionary<HexCoord, MineRaid> _raids = new Dictionary<HexCoord, MineRaid>();

        public IReadOnlyCollection<HexCoord> Mines => _mines;
        public IReadOnlyCollection<MineRaid> Raids => _raids.Values;

        public event Action<MineRaid> MineRaidStarted;
        public event Action<MineRaid> MineLost;

        private int CountProducingMines()
        {
            int count = 0;
            foreach (HexCoord mine in _mines)
                if (!_raids.TryGetValue(mine, out MineRaid raid) || !raid.IsLost) count++;
            return count;
        }

        private void UpdateMineRaids(int turn)
        {
            foreach (MineRaid raid in new List<MineRaid>(_raids.Values))
            {
                if (raid.IsLost || turn < raid.DeadlineTurn) continue;
                raid.IsLost = true;
                MineLost?.Invoke(raid);
            }

            if (!TurnSystem.IsIntervalTurn(turn, MineRaidInterval)) return;

            var targets = new List<HexCoord>();
            foreach (HexCoord mine in _mines)
                if (!_raids.ContainsKey(mine) && mine != Player.Position) targets.Add(mine);
            if (targets.Count == 0) return;

            // Stabile Reihenfolge, damit gleicher Seed gleiche Angriffe ergibt.
            targets.Sort((a, b) => a.Q != b.Q ? a.Q.CompareTo(b.Q) : a.R.CompareTo(b.R));
            StartMineRaid(targets[_random.Next(targets.Count)], turn);
        }

        /// <summary>Startet einen Angriff auf eine eigene Mine. Öffentlich für Tests und spätere Events.</summary>
        public MineRaid StartMineRaid(HexCoord mine, int turn)
        {
            if (!_mines.Contains(mine) || _raids.ContainsKey(mine)) return null;
            var raid = new MineRaid(mine, turn + MineRaidTurns);
            _raids.Add(mine, raid);
            Map.SetUnderAttack(mine, true);
            MineRaidStarted?.Invoke(raid);
            return raid;
        }

        private void DefendMine(HexCell cell)
        {
            _raids.TryGetValue(cell.Coord, out MineRaid raid);
            bool lost = raid?.IsLost == true;
            string title = lost ? "Mine zurückerobert" : "Goldmine verteidigt";

            int tier = TierAt(cell.Coord) + MineRaidTierBonus;
            var context = new BattleContext { OnGoldMine = true, Turn = Turns.CurrentTurn };
            CombatResult result = RunCombat(CellContent.Enemy, tier, context);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(result.EnemyName)) lines.Add(result.EnemyName);
            if (!ApplyCombat(cell, result, "Kampf um die Mine", lines)) return;

            _raids.Remove(cell.Coord);
            Map.SetUnderAttack(cell.Coord, false);
            int gold = result.GoldReward + MineDefenseGold;
            Stats.AddGold(gold);
            lines.Add($"+{gold} Gold");
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, title, lines));
            OfferRunes("Mine verteidigt");
        }
    }
}
