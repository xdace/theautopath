using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using Betaknight.Core.Turns;

namespace Betaknight.Core
{
    /// <summary>Ergebnis eines grossen Events (Kampf, Truhe, Goldmine) mit lesbaren Zeilen für die Anzeige.</summary>
    public sealed class MajorEventOutcome
    {
        public HexCell Cell { get; }
        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }

        public MajorEventOutcome(HexCell cell, string title, IReadOnlyList<string> lines)
        {
            Cell = cell;
            Title = title;
            Lines = lines;
        }

        public string Summary => Lines.Count == 0 ? string.Empty : string.Join(", ", Lines);
    }

    /// <summary>Ein geöffneter Shop.</summary>
    public sealed class ShopVisit
    {
        public HexCell Cell { get; }
        public ShopInventory Inventory { get; }

        public ShopVisit(HexCell cell, ShopInventory inventory)
        {
            Cell = cell;
            Inventory = inventory;
        }
    }

    /// <summary>Grosse Events: Kampf (Platzhalter bis zur Arena), Schatztruhe, Goldmine und Shop.</summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Alle so viele Züge bringt jede eroberte Goldmine Gold.</summary>
        public const int MineIncomeInterval = 3;

        /// <summary>Gold pro Mine und Auszahlung.</summary>
        public const int MineIncomeGold = 1;

        public ShopPrices ShopPrices { get; } = new ShopPrices();

        /// <summary>Geöffneter Shop. Solange gesetzt, ist Bewegung gesperrt.</summary>
        public ShopVisit PendingShop { get; private set; }

        /// <summary>Letzter Kampf mit Protokoll, für die Arena-Wiedergabe.</summary>
        public CombatResult? LastCombat { get; private set; }

        /// <summary>Ein Kampf wurde simuliert, bevor sein Ergebnis angewendet wird.</summary>
        public event Action<CombatResult> CombatFinished;

        /// <summary>Eroberte Minen, die gerade Gold bringen (nicht verlorene).</summary>
        public int ClaimedMines => CountProducingMines();

        public bool IsGameOver => Stats.IsDead;

        /// <summary>Kampf, Truhe oder Goldmine hat gewirkt.</summary>
        public event Action<MajorEventOutcome> MajorEventResolved;

        public event Action<ShopVisit> ShopOpened;

        /// <summary>Der Ritter ist gefallen.</summary>
        public event Action RunEnded;

        private readonly ICombatResolver _combat;
        private readonly Dictionary<HexCoord, ShopInventory> _shops = new Dictionary<HexCoord, ShopInventory>();

        private void TriggerMajorEvent(HexCell cell, bool firstVisit)
        {
            switch (cell.Content)
            {
                case CellContent.Enemy:
                case CellContent.Boss:
                    if (!cell.IsResolved) Fight(cell);
                    break;

                case CellContent.Treasure:
                    if (!cell.IsResolved) OpenTreasure(cell);
                    break;

                case CellContent.GoldMine:
                    if (cell.IsUnderAttack) DefendMine(cell);
                    else if (!cell.IsResolved) ClaimMine(cell);
                    break;

                case CellContent.Shop:
                    // Beim ersten Betreten öffnet der Shop von selbst, danach über OpenShop.
                    if (firstVisit) OpenShop();
                    break;
            }
        }

        private void Fight(HexCell cell)
        {
            int tier = TierAt(cell.Coord);
            var context = new BattleContext { VsBoss = cell.Content == CellContent.Boss, Turn = Turns.CurrentTurn };
            CombatResult result = RunCombat(cell.Content, tier, context);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(result.EnemyName)) lines.Add(result.EnemyName);
            string title = cell.Content == CellContent.Boss ? "Boss" : "Kampf";

            // Ein verlorener Kampf endet tödlich, auch wenn der Resolver noch HP übrig liess.
            if (!ApplyCombat(cell, result, title, lines)) return;

            Stats.AddGold(result.GoldReward);
            lines.Add($"+{result.GoldReward} Gold");
            Map.MarkResolved(cell.Coord);
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, $"{title} gewonnen", lines));
            OfferRunes("Sieg");
        }

        private CombatResult RunCombat(CellContent enemy, int tier, BattleContext context)
        {
            CombatResult result = _combat.Resolve(new CombatRequest(enemy, tier, Stats, Runes, Gear, context), _random);
            LastCombat = result;
            CombatFinished?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Wendet Schaden an und beendet bei Niederlage den Run. Gibt true zurück, wenn der Ritter gewonnen hat.
        /// </summary>
        private bool ApplyCombat(HexCell cell, CombatResult result, string title, List<string> lines)
        {
            int dealt = Stats.Damage(result.DamageTaken);
            if (dealt > 0) lines.Add($"-{dealt} HP");
            if (result.Victory && !Stats.IsDead) return true;

            if (!Stats.IsDead) Stats.Damage(Stats.Hp);
            lines.Add("Niederlage");
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, title, lines));
            RunEnded?.Invoke();
            return false;
        }

        private void OpenTreasure(HexCell cell)
        {
            int gold = _random.Next(6, 11);
            Stats.AddGold(gold);
            Map.MarkResolved(cell.Coord);
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, "Schatztruhe", new[] { $"+{gold} Gold" }));
            OfferRunes("Schatztruhe");
        }

        private void ClaimMine(HexCell cell)
        {
            _mines.Add(cell.Coord);
            Stats.AddGold(3);
            Map.MarkResolved(cell.Coord);
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, "Goldmine erobert",
                new[] { "+3 Gold", $"+{MineIncomeGold} Gold alle {MineIncomeInterval} Züge" }));
        }

        private void OnTurnEnded(int turn)
        {
            if (ClaimedMines > 0 && TurnSystem.IsIntervalTurn(turn, MineIncomeInterval))
                Stats.AddGold(ClaimedMines * MineIncomeGold);
            UpdateMineRaids(turn);
            ScheduleBoss(turn);
        }

        /// <summary>Steht der Spieler auf einem Shop-Feld und wartet nichts anderes?</summary>
        public bool CanOpenShop => !IsBusy && !IsGameOver && CurrentCell.Content == CellContent.Shop;

        /// <summary>Öffnet den Shop auf dem aktuellen Feld. Der Bestand bleibt pro Shop erhalten.</summary>
        public bool OpenShop()
        {
            if (!CanOpenShop) return false;

            HexCell cell = CurrentCell;
            if (!_shops.TryGetValue(cell.Coord, out ShopInventory inventory))
            {
                inventory = new ShopInventory(RuneOffer.Create("Shop", RuneCatalog, Runes, _random).Options, PickItems(ShopItemCount));
                _shops.Add(cell.Coord, inventory);
            }

            PendingShop = new ShopVisit(cell, inventory);
            ShopOpened?.Invoke(PendingShop);
            return true;
        }

        public bool CanBuyShopRune(int index) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.Runes.Count
            && Stats.Gold >= ShopPrices.Rune && !Runes.Contains(PendingShop.Inventory.Runes[index]);

        /// <summary>Kauft eine Rune. Bei vollen Plätzen muss <paramref name="replaceSlot"/> angegeben werden.</summary>
        public bool BuyShopRune(int index, int replaceSlot = -1)
        {
            if (!CanBuyShopRune(index)) return false;

            RuneDefinition rune = PendingShop.Inventory.Runes[index];
            bool ok = Runes.IsFull ? Runes.TryReplace(replaceSlot, rune) : Runes.TryAdd(rune, DefaultSkillForNewRow());
            if (!ok) return false;

            Stats.TrySpendGold(ShopPrices.Rune);
            PendingShop.Inventory.Remove(rune);
            RuneTaken?.Invoke(rune);
            return true;
        }

        public bool CanBuyHeal => PendingShop != null && Stats.Gold >= ShopPrices.Heal && Stats.Hp < Stats.MaxHp;

        public bool BuyHeal()
        {
            if (!CanBuyHeal) return false;
            Stats.TrySpendGold(ShopPrices.Heal);
            Stats.Heal(ShopPrices.HealAmount);
            return true;
        }

        public bool CanBuyRuneSlot => PendingShop != null && !PendingShop.Inventory.SlotSold && Stats.Gold >= ShopPrices.Slot;

        public bool BuyRuneSlot()
        {
            if (!CanBuyRuneSlot) return false;
            Stats.TrySpendGold(ShopPrices.Slot);
            Runes.AddSlot();
            PendingShop.Inventory.SlotSold = true;
            return true;
        }

        public bool CanRerollShop => PendingShop != null && Stats.Gold >= ShopPrices.Reroll;

        /// <summary>Würfelt die Runen im Shop neu.</summary>
        public bool RerollShop()
        {
            if (!CanRerollShop) return false;
            Stats.TrySpendGold(ShopPrices.Reroll);
            PendingShop.Inventory.Replace(RuneOffer.Create("Shop", RuneCatalog, Runes, _random).Options);
            PendingShop.Inventory.ReplaceItems(PickItems(ShopItemCount));
            return true;
        }

        public void LeaveShop()
        {
            if (PendingShop == null) return;
            PendingShop = null;
            CheckShards();
        }
    }
}
