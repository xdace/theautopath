using System.Collections.Generic;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;

namespace Betaknight.Core
{
    /// <summary>
    /// Lock im Shop (A-21): ein gesperrtes Angebot bleibt beim Reroll stehen und erscheint beim nächsten Shop-Besuch wieder,
    /// auch in einem anderen Shop, bis es gekauft oder entsperrt wird. Höchstens <see cref="ShopPrices.LockSlots"/> zugleich.
    /// </summary>
    public sealed partial class OverworldSession
    {
        private List<ShopOffer> _lockedOffers = new List<ShopOffer>();

        public IReadOnlyList<ShopOffer> LockedOffers => _lockedOffers;

        public bool IsLocked(ShopOfferKind kind, string id) => _lockedOffers.Contains(new ShopOffer(kind, id));

        /// <summary>Angebot an dieser Stelle des offenen Shops gesperrt?</summary>
        public bool IsLockedAt(ShopOfferKind kind, int index) =>
            PendingShop != null && IsLocked(kind, PendingShop.Inventory.IdAt(kind, index));

        public bool CanLock(ShopOfferKind kind, int index)
        {
            if (PendingShop == null) return false;
            string id = PendingShop.Inventory.IdAt(kind, index);
            return id != null && (IsLocked(kind, id) || _lockedOffers.Count < ShopPrices.LockSlots);
        }

        /// <summary>Sperrt bzw. entsperrt ein Angebot des offenen Shops. Gibt zurück, ob es danach gesperrt ist.</summary>
        public bool ToggleLock(ShopOfferKind kind, int index)
        {
            if (!CanLock(kind, index)) return false;
            var offer = new ShopOffer(kind, PendingShop.Inventory.IdAt(kind, index));
            if (_lockedOffers.Remove(offer)) return false;
            _lockedOffers.Add(offer);
            return true;
        }

        private void Unlock(ShopOfferKind kind, string id) => _lockedOffers.Remove(new ShopOffer(kind, id));

        /// <summary>Gesperrte Angebote stehen in jedem Shop (vorne), auch nach einem Reroll.</summary>
        private void KeepLockedOffers(ShopInventory inventory)
        {
            for (int i = _lockedOffers.Count - 1; i >= 0; i--)
            {
                ShopOffer offer = _lockedOffers[i];
                RuneDefinition rune = offer.Kind == ShopOfferKind.Rune && RuneCatalog.TryGet(offer.Id, out RuneDefinition r) ? r : null;
                inventory.Keep(offer.Kind, offer.Id, rune);
            }
        }

        private void CarryShopLocks(OverworldSession previous) => _lockedOffers = previous._lockedOffers;
    }
}
