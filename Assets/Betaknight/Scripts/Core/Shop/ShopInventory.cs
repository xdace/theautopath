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
    }

    /// <summary>Warenbestand eines Shops. Bleibt pro Shop-Feld erhalten, Gekauftes verschwindet.</summary>
    public sealed class ShopInventory
    {
        private readonly List<RuneDefinition> _runes;

        public IReadOnlyList<RuneDefinition> Runes => _runes;

        /// <summary>Der zusätzliche Runenplatz ist pro Shop nur einmal käuflich.</summary>
        public bool SlotSold { get; internal set; }

        public ShopInventory(IEnumerable<RuneDefinition> runes)
        {
            _runes = new List<RuneDefinition>(runes ?? throw new ArgumentNullException(nameof(runes)));
        }

        internal void Remove(RuneDefinition rune) => _runes.Remove(rune);

        internal void Replace(IEnumerable<RuneDefinition> runes)
        {
            _runes.Clear();
            _runes.AddRange(runes);
        }
    }
}
