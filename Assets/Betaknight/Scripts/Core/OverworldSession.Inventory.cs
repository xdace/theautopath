using System;
using System.Collections.Generic;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;

namespace Betaknight.Core
{
    /// <summary>Wohin ein neues Ausrüstungsteil kommt.</summary>
    public enum ItemPlacement
    {
        /// <summary>Anlegen, wenn sein Platz frei ist, sonst ins Inventar.</summary>
        Auto,

        /// <summary>Anlegen; das bisherige Teil (und bei Zweihand der Schild) wandert ins Inventar.</summary>
        Equip,

        /// <summary>Direkt ins Inventar.</summary>
        Inventory,
    }

    /// <summary>
    /// Inventar: Ausrüstung und Runen sammeln und frei wechseln statt ersetzen. Verdrängtes wandert ins Inventar.
    /// Passt etwas nicht mehr hinein, wartet es als <see cref="PendingItem"/> bzw. <see cref="PendingRune"/>
    /// auf die Entscheidung: ein Teil aus dem Inventar verwerfen oder das neue ablehnen.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public Inventory Inventory { get; }
        public RuneInventory RuneInventory { get; }

        private readonly List<EquipmentDefinition> _overflowItems = new List<EquipmentDefinition>();
        private readonly List<StoredRune> _overflowRunes = new List<StoredRune>();

        /// <summary>Teil, das nicht mehr ins volle Inventar passt. Solange gesetzt, ist Bewegung gesperrt.</summary>
        public EquipmentDefinition PendingItem => _overflowItems.Count > 0 ? _overflowItems[0] : null;

        /// <summary>Rune, die nicht mehr ins volle Runen-Inventar passt.</summary>
        public StoredRune PendingRune => _overflowRunes.Count > 0 ? _overflowRunes[0] : null;

        /// <summary>Läuft gerade ein Kampf? Dann ist jeder Wechsel gesperrt.</summary>
        public bool IsInCombat { get; private set; }

        /// <summary>Wechseln ist erlaubt: kein Kampf, kein offenes Fenster, Ritter lebt.</summary>
        public bool CanChangeLoadout => !IsInCombat && !IsBusy && !IsGameOver;

        /// <summary>Etwas passt nicht mehr ins Inventar und wartet auf eine Entscheidung.</summary>
        public event Action InventoryOverflow;

        // ------------------------------------------------------------------ Ausrüstung

        /// <summary>
        /// Legt das Teil einer Raster-Zelle an. Das bisherige Teil kommt in dieselbe Zelle, ein weiteres verdrängtes
        /// (Schild bei Zweihand) in die erste freie.
        /// </summary>
        public bool EquipFromInventory(int cell)
        {
            if (!CanEquipFromInventory(cell)) return false;
            EquipmentDefinition item = Inventory.RemoveAt(cell);
            List<EquipmentDefinition> removed = EquipItem(item);
            for (int i = 0; i < removed.Count; i++)
                if (i > 0 || !Inventory.TryPlace(removed[i], cell)) StoreItem(removed[i]);
            ItemTaken?.Invoke(item, removed);
            return true;
        }

        public bool CanEquipFromInventory(int cell) =>
            CanChangeLoadout && Inventory[cell] != null && Gear.CanEquip(Inventory[cell], out _);

        /// <summary>Ziehen auf einen bestimmten Platz an der Figur: nur der passende Platz nimmt das Teil an.</summary>
        public bool EquipFromInventoryTo(int cell, EquipmentSlot slot) => CanEquipTo(cell, slot) && EquipFromInventory(cell);

        /// <summary>Passt das Teil dieser Zelle auf diesen Platz (und darf es jetzt angelegt werden)?</summary>
        public bool CanEquipTo(int cell, EquipmentSlot slot) => CanEquipFromInventory(cell) && Inventory[cell].Slot == slot;

        /// <summary>
        /// Legt das Teil eines Platzes ab und ins Raster: ohne <paramref name="cell"/> in die erste freie Zelle, sonst in
        /// diese Zelle. Liegt dort ein Teil für denselben Platz, wird getauscht (es wird angelegt).
        /// </summary>
        public bool UnequipToInventory(EquipmentSlot slot, int cell = -1)
        {
            if (!CanUnequipToInventory(slot, cell)) return false;
            if (cell >= 0 && Inventory.IsValid(cell)) return EquipFromInventory(cell);
            EquipmentDefinition item = Gear.Unequip(slot);
            return cell >= 0 ? Inventory.TryPlace(item, cell) : Inventory.TryAdd(item);
        }

        public bool CanUnequipToInventory(EquipmentSlot slot, int cell = -1)
        {
            if (!CanChangeLoadout || Gear.Get(slot) == null) return false;
            if (cell < 0) return !Inventory.IsFull;
            if (!Inventory.IsCell(cell)) return false;
            if (!Inventory.IsValid(cell)) return true;
            return Inventory[cell]?.Slot == slot && CanEquipFromInventory(cell);
        }

        /// <summary>Sortieren im Raster: auf eine leere Zelle verschieben, auf eine volle tauschen.</summary>
        public bool MoveInventoryItem(int from, int to) => CanChangeLoadout && Inventory.Move(from, to);

        /// <summary>
        /// Verwirft ein Teil aus dem Inventar. Wartet ein Teil auf Platz, rückt es nach. Sonst nur ausserhalb von Kampf und Fenstern.
        /// </summary>
        public bool DiscardItem(int index)
        {
            if (!Inventory.IsValid(index)) return false;
            if (PendingItem == null && !CanChangeLoadout) return false;

            Inventory.RemoveAt(index);
            if (PendingItem != null)
            {
                Inventory.TryAdd(PendingItem);
                _overflowItems.RemoveAt(0);
                AfterOverflowResolved();
            }
            return true;
        }

        /// <summary>Lehnt das wartende Teil ab; es verschwindet.</summary>
        public bool RejectPendingItem()
        {
            if (PendingItem == null) return false;
            _overflowItems.RemoveAt(0);
            AfterOverflowResolved();
            return true;
        }

        /// <summary>Verkauft ein Teil aus dem Inventar im offenen Shop für den halben Preis.</summary>
        public bool SellItem(int index)
        {
            if (PendingShop == null || PendingItem != null || PendingRune != null || !Inventory.IsValid(index)) return false;
            Inventory.RemoveAt(index);
            Stats.AddGold(ShopPrices.SellItem);
            return true;
        }

        /// <summary>Neues Teil aus Belohnung oder Shop: anlegen oder ins Inventar, Verdrängtes ins Inventar.</summary>
        private void PlaceNewItem(EquipmentDefinition item, ItemPlacement placement)
        {
            // Doppeltes Teil: das vorhandene steigt eine Stufe, statt ein zweites zu lagern.
            if (UpgradeItem(item.Id))
            {
                ItemTaken?.Invoke(item, Array.Empty<EquipmentDefinition>());
                return;
            }

            bool equip = placement == ItemPlacement.Equip
                || (placement == ItemPlacement.Auto && Gear.Get(item.Slot) == null && Gear.CanEquip(item, out _));

            if (!equip)
            {
                StoreItem(item);
                ItemTaken?.Invoke(item, Array.Empty<EquipmentDefinition>());
                return;
            }

            List<EquipmentDefinition> removed = EquipItem(item);
            foreach (EquipmentDefinition old in removed) StoreItem(old);
            ItemTaken?.Invoke(item, removed);
        }

        private void StoreItem(EquipmentDefinition item)
        {
            if (item == null || Inventory.TryAdd(item)) return;
            _overflowItems.Add(item);
            InventoryOverflow?.Invoke();
        }

        // ------------------------------------------------------------------ Runen

        /// <summary>
        /// Setzt eine Rune aus dem Inventar in eine Zeile der Tafel ein. Die bisherige Rune kommt mit ihrer Stufe
        /// an dieselbe Inventarstelle, der Skill bleibt an der Zeile.
        /// </summary>
        public bool SwapRune(int row, int inventoryIndex)
        {
            if (!CanChangeLoadout || !RuneInventory.IsValid(inventoryIndex) || row < 0 || row >= Runes.Rows.Count) return false;

            StoredRune stored = RuneInventory.RemoveAt(inventoryIndex);
            if (!Runes.SwapRune(row, stored.Rune, stored.Level, stored.Growth, out RuneDefinition old, out int oldLevel, out int oldGrowth))
            {
                RuneInventory.TryAdd(stored, inventoryIndex);
                return false;
            }
            RuneInventory.TryAdd(new StoredRune(old, oldLevel, oldGrowth), inventoryIndex);
            return true;
        }

        /// <summary>
        /// Setzt eine Rune aus dem Inventar als neue Zeile ein (braucht eine freie Zeile). Mit <paramref name="atRow"/>
        /// landet die neue Zeile an dieser Stelle, sonst unten.
        /// </summary>
        public bool EquipRuneFromInventory(int inventoryIndex, int atRow = -1)
        {
            if (!CanChangeLoadout || !RuneInventory.IsValid(inventoryIndex) || Runes.IsFull) return false;
            StoredRune stored = RuneInventory.RemoveAt(inventoryIndex);
            if (!Runes.TryAdd(stored.Rune, SkillForNewRow(), stored.Level, stored.Growth)) return false;
            int last = Runes.Rows.Count - 1;
            if (atRow >= 0 && atRow < last) Runes.Move(last, atRow);
            return true;
        }

        /// <summary>Nimmt eine Zeile von der Tafel; die Rune kommt mit Stufe ins Inventar.</summary>
        public bool UnequipRune(int row)
        {
            if (!CanChangeLoadout || RuneInventory.IsFull || row < 0 || row >= Runes.Rows.Count) return false;
            RuneSlot removed = Runes.RemoveAt(row);
            return RuneInventory.TryAdd(new StoredRune(removed.Rune, removed.Level, removed.Growth));
        }

        /// <summary>Verwirft eine Rune aus dem Inventar; eine wartende Rune rückt nach.</summary>
        public bool DiscardRune(int inventoryIndex)
        {
            if (!RuneInventory.IsValid(inventoryIndex)) return false;
            if (PendingRune == null && !CanChangeLoadout) return false;

            RuneInventory.RemoveAt(inventoryIndex);
            if (PendingRune != null)
            {
                RuneInventory.TryAdd(PendingRune);
                _overflowRunes.RemoveAt(0);
                AfterOverflowResolved();
            }
            return true;
        }

        public bool RejectPendingRune()
        {
            if (PendingRune == null) return false;
            _overflowRunes.RemoveAt(0);
            AfterOverflowResolved();
            return true;
        }

        /// <summary>Verkauft eine Rune aus dem Inventar im offenen Shop für den halben Preis.</summary>
        public bool SellRune(int inventoryIndex)
        {
            if (PendingShop == null || PendingItem != null || PendingRune != null || !RuneInventory.IsValid(inventoryIndex)) return false;
            RuneInventory.RemoveAt(inventoryIndex);
            Stats.AddGold(ShopPrices.SellRune);
            return true;
        }

        /// <summary>
        /// Neue Rune aus Belohnung oder Shop: in die Zeile <paramref name="replaceSlot"/> (alte Rune ins Inventar),
        /// sonst auf eine freie Zeile, sonst ins Inventar.
        /// </summary>
        private bool PlaceNewRune(RuneDefinition rune, int replaceSlot)
        {
            if (rune == null) return false;

            // Doppelte Rune: die vorhandene steigt eine Stufe (wie am Lagerfeuer).
            if (OwnsRune(rune)) return UpgradeOwnedRune(rune);

            if (replaceSlot >= 0)
            {
                if (!Runes.SwapRune(replaceSlot, rune, 0, 0, out RuneDefinition old, out int oldLevel, out int oldGrowth)) return false;
                StoreRune(new StoredRune(old, oldLevel, oldGrowth));
                return true;
            }

            if (!Runes.IsFull) return Runes.TryAdd(rune, SkillForNewRow());
            StoreRune(new StoredRune(rune));
            return true;
        }

        private void StoreRune(StoredRune rune)
        {
            if (rune == null || RuneInventory.TryAdd(rune)) return;
            _overflowRunes.Add(rune);
            InventoryOverflow?.Invoke();
        }

        private void AfterOverflowResolved()
        {
            if (PendingItem == null && PendingRune == null) CheckShards();
        }
    }
}
