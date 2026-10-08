using System;
using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Shop
{
    /// <summary>Preise im Shop. Werte als Felder, damit sie leicht anpassbar sind.</summary>
    public sealed class ShopPrices
    {
        public int Rune = 10;
        public int Heal = 5;
        public int HealAmount = 10;
        public int Slot = 20;
        public int Reroll = 3;
        public int Item = 12;

        /// <summary>Verkauf aus dem Inventar: halber Preis.</summary>
        public int SellItem => Item / 2;
        public int SellRune => Rune / 2;
    }

    /// <summary>Warenbestand eines Shops. Bleibt pro Shop-Feld erhalten, Gekauftes verschwindet.</summary>
    public sealed class ShopInventory
    {
        private readonly List<RuneDefinition> _runes;
        private readonly List<string> _items = new List<string>();

        public IReadOnlyList<RuneDefinition> Runes => _runes;

        /// <summary>Ausrüstung im Angebot (Ids aus dem Ausrüstungs-Katalog).</summary>
        public IReadOnlyList<string> ItemIds => _items;

        /// <summary>Der zusätzliche Runenplatz ist pro Shop nur einmal käuflich.</summary>
        public bool SlotSold { get; internal set; }

        public ShopInventory(IEnumerable<RuneDefinition> runes, IEnumerable<string> itemIds = null)
        {
            _runes = new List<RuneDefinition>(runes ?? throw new ArgumentNullException(nameof(runes)));
            if (itemIds != null) _items.AddRange(itemIds);
        }

        internal void RemoveItemAt(int index) => _items.RemoveAt(index);

        internal void ReplaceItems(IEnumerable<string> itemIds)
        {
            _items.Clear();
            _items.AddRange(itemIds);
        }

        internal void Remove(RuneDefinition rune) => _runes.Remove(rune);

        internal void Replace(IEnumerable<RuneDefinition> runes)
        {
            _runes.Clear();
            _runes.AddRange(runes);
        }
    }
}
