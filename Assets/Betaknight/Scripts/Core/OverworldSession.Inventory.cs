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

        /// <summary>Legt ein Teil aus dem Inventar an. Das bisherige Teil (bei Zweihand auch der Schild) kommt ins Inventar.</summary>
        public bool EquipFromInventory(int index)
        {
            if (!CanEquipFromInventory(index)) return false;
            EquipmentDefinition item = Inventory.RemoveAt(index);
            List<EquipmentDefinition> removed = EquipItem(item);
            foreach (EquipmentDefinition old in removed) StoreItem(old);
            ItemTaken?.Invoke(item, removed);
            return true;
        }

        public bool CanEquipFromInventory(int index) =>
            CanChangeLoadout && Inventory.IsValid(index) && Gear.CanEquip(Inventory[index], out _);

        /// <summary>Legt das Teil eines Platzes ab und ins Inventar. Braucht einen freien Inventarplatz.</summary>
        public bool UnequipToInventory(EquipmentSlot slot)
        {
            if (!CanUnequipToInventory(slot)) return false;
            Inventory.TryAdd(Gear.Unequip(slot));
            return true;
        }

        public bool CanUnequipToInventory(EquipmentSlot slot) => CanChangeLoadout && Gear.Get(slot) != null && !Inventory.IsFull;

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
            if (!Runes.SwapRune(row, stored.Rune, stored.Level, out RuneDefinition old, out int oldLevel))
            {
                RuneInventory.TryAdd(stored, inventoryIndex);
                return false;
            }
            RuneInventory.TryAdd(new StoredRune(old, oldLevel), inventoryIndex);
            return true;
        }

        /// <summary>Setzt eine Rune aus dem Inventar als neue Zeile ein (braucht eine freie Zeile).</summary>
        public bool EquipRuneFromInventory(int inventoryIndex)
        {
            if (!CanChangeLoadout || !RuneInventory.IsValid(inventoryIndex) || Runes.IsFull) return false;
            StoredRune stored = RuneInventory.RemoveAt(inventoryIndex);
            return Runes.TryAdd(stored.Rune, DefaultSkillForNewRow(), stored.Level);
        }

        /// <summary>Nimmt eine Zeile von der Tafel; die Rune kommt mit Stufe ins Inventar.</summary>
        public bool UnequipRune(int row)
        {
            if (!CanChangeLoadout || RuneInventory.IsFull || row < 0 || row >= Runes.Rows.Count) return false;
            RuneSlot removed = Runes.RemoveAt(row);
            return RuneInventory.TryAdd(new StoredRune(removed.Rune, removed.Level));
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
            if (rune == null || Runes.Contains(rune) || RuneInventory.Contains(rune)) return false;

            if (replaceSlot >= 0)
            {
                if (!Runes.SwapRune(replaceSlot, rune, 0, out RuneDefinition old, out int oldLevel)) return false;
                StoreRune(new StoredRune(old, oldLevel));
                return true;
            }

            if (!Runes.IsFull) return Runes.TryAdd(rune, DefaultSkillForNewRow());
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
