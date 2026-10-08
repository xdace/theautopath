using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

namespace Betaknight.Core
{
    /// <summary>
    /// Logik-Chips (A-20): Leiterbahnen, Dioden, Gatter, Kondensatoren und Sicherungen. Seltene Belohnungen (Elite, Boss-Flucht,
    /// Truhe, Shop), ein Chip-Inventar, das durch die Akte mitwandert, und Legen, Verschieben, Drehen auf der Platine.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public ChipCatalog ChipCatalog { get; } = ChipCatalog.CreateDefault();

        private List<ChipDefinition> _chipInventory = new List<ChipDefinition>();
        private int _chipRolls;

        /// <summary>Chips, die nicht auf der Platine liegen. Keine Obergrenze.</summary>
        public IReadOnlyList<ChipDefinition> ChipInventory => _chipInventory;

        /// <summary>Ein Chip kam ins Inventar.</summary>
        public event Action<ChipDefinition> ChipGained;

        public bool CanEditChips => CanChangeLoadout;

        /// <summary>Neuer Chip ins Inventar. Gibt die Meldung zurück oder null bei unbekannter Id.</summary>
        public string GrantChip(string chipId)
        {
            if (!ChipCatalog.TryGet(chipId, out ChipDefinition chip)) return null;
            _chipInventory.Add(chip);
            ChipGained?.Invoke(chip);
            return SessionTexts.ChipLabel(chip.Name);
        }

        /// <summary>Legt einen Chip aus dem Inventar auf eine freie Zelle (gedreht um Vierteldrehungen).</summary>
        public bool PlaceChip(int inventoryIndex, Cell at, int turns = 0)
        {
            if (!CanEditChips || inventoryIndex < 0 || inventoryIndex >= _chipInventory.Count) return false;
            if (Board.AddChip(_chipInventory[inventoryIndex], at, turns) == null) return false;
            _chipInventory.RemoveAt(inventoryIndex);
            return true;
        }

        public bool CanPlaceChip(Cell at) => CanEditChips && Board.IsFree(new CellRect(at, Shape.One));

        /// <summary>Verschiebt einen Chip der Platine (Index in Lesereihenfolge).</summary>
        public bool MoveChip(int chipIndex, Cell to) => CanEditChips && IsChip(chipIndex) && Board.MoveChip(Board.Chips[chipIndex], to);

        /// <summary>Rechtsklick: dreht einen Chip um 90° im Uhrzeigersinn.</summary>
        public bool RotateChip(int chipIndex) => CanEditChips && IsChip(chipIndex) && Board.RotateChip(Board.Chips[chipIndex]);

        /// <summary>Nimmt einen Chip von der Platine zurück ins Inventar.</summary>
        public bool RemoveChip(int chipIndex)
        {
            if (!CanEditChips || !IsChip(chipIndex)) return false;
            BoardChip chip = Board.Chips[chipIndex];
            if (!Board.RemoveChip(chip)) return false;
            _chipInventory.Add(chip.Definition);
            return true;
        }

        private bool IsChip(int index) => index >= 0 && index < Board.Chips.Count;

        // ------------------------------------------------------------------ Erhalt

        /// <summary>
        /// Würfelt einen Chip nach Gewicht. Eigener Zufall aus Karten-Seed und Zähler, damit die übrige Beute gleich bleibt.
        /// </summary>
        private string RollChipId() => ChipCatalog.Roll(ChipRandom())?.Id;

        private Random ChipRandom() => new Random(unchecked(Map.Seed * 7919 + Act * 104729 + ++_chipRolls * 31));

        /// <summary>Elite-Sieg oder Truhe: mit der Chance der Quelle ein Chip. Gibt die Meldung zurück oder null.</summary>
        private string RollRewardChip(string source)
        {
            int chance = Progression.ChipChance(source);
            if (chance <= 0 || ChipRandom().Next(100) >= chance) return null;
            return GrantChip(RollChipId());
        }

        private List<string> RollShopChips()
        {
            var result = new List<string>();
            if (ChipRandom().Next(100) < Progression.ShopChipChance)
            {
                string id = RollChipId();
                if (id != null) result.Add(id);
            }
            return result;
        }

        public bool CanBuyShopChip(int index) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.ChipIds.Count && Stats.Gold >= ShopPrices.Chip;

        public bool BuyShopChip(int index)
        {
            if (!CanBuyShopChip(index)) return false;
            string id = PendingShop.Inventory.ChipIds[index];
            Stats.TrySpendGold(ShopPrices.Chip);
            PendingShop.Inventory.RemoveChipAt(index);
            GrantChip(id);
            return true;
        }

        private void CarryChips(OverworldSession previous)
        {
            _chipInventory = previous._chipInventory;
            _chipRolls = previous._chipRolls;
        }
    }
}
