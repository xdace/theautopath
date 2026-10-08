using System;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Inventar: sammeln und wechseln statt ersetzen, mit allen Ausrüstungs- und Tafelregeln.</summary>
    public class InventoryTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly EquipmentCatalog AllItems = EquipmentCatalog.CreateDefault();

        private static OverworldSession Session(EquipmentCatalog items = null, ICombatResolver combat = null, PlayerStats stats = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats ?? new PlayerStats(30, 50), null, null, null, combat, null, items);
        }

        /// <summary>Katalog mit genau einem Teil im Angebot, damit Belohnungen vorhersehbar sind.</summary>
        private static EquipmentCatalog OnlyOffering(string id) =>
            new EquipmentCatalog(AllItems.All.Select(i => i.Id == id ? i
                : new EquipmentDefinition(i.Id, i.Name, i.Slot, i.Stats.ToDictionary(p => p.Key, p => p.Value), i.SkillIds, i.SetId, i.TwoHanded, 0)));

        private static void Put(OverworldSession s, params string[] ids)
        {
            foreach (string id in ids) Assert.IsTrue(s.Inventory.TryAdd(s.Items.Get(id)), id);
        }

        private static void Wear(OverworldSession s, params string[] ids)
        {
            foreach (string id in ids) Assert.IsNotNull(s.Gear.Equip(s.Items.Get(id)), id);
        }

        private static string[] InventoryIds(OverworldSession s) => s.Inventory.Items.Select(i => i.Id).ToArray();

        // ------------------------------------------------------------------ Verdrängen

        [Test]
        public void EquippingARewardMovesTheOldPieceToTheInventory()
        {
            OverworldSession s = Session(OnlyOffering("thermo_blade"));
            Wear(s, "short_sword");
            s.Map.SetContent(East, CellContent.Treasure);
            s.TryStep(East);
            Assert.AreEqual("thermo_blade", s.PendingRuneOffer.ItemIds.Single());

            Assert.IsTrue(s.TakeItem(0, ItemPlacement.Equip));

            Assert.AreEqual("thermo_blade", s.Gear.Get(EquipmentSlot.Weapon).Id);
            CollectionAssert.AreEqual(new[] { "short_sword" }, InventoryIds(s));
        }

        [Test]
        public void AutoPlacementEquipsIntoFreeSlotsAndStoresTheRest()
        {
            OverworldSession s = Session(OnlyOffering("thermo_blade"));
            s.OfferRunes("Schatztruhe");
            Assert.IsTrue(s.TakeItem(0));
            Assert.AreEqual("thermo_blade", s.Gear.Get(EquipmentSlot.Weapon).Id, "Platz war frei");

            OverworldSession t = Session(OnlyOffering("thermo_blade"));
            Wear(t, "short_sword");
            t.OfferRunes("Schatztruhe");
            Assert.IsTrue(t.TakeItem(0, ItemPlacement.Inventory));
            Assert.AreEqual("short_sword", t.Gear.Get(EquipmentSlot.Weapon).Id);
            CollectionAssert.AreEqual(new[] { "thermo_blade" }, InventoryIds(t));
        }

        [Test]
        public void SwappingFromTheInventoryKeepsBothPieces()
        {
            OverworldSession s = Session();
            Wear(s, "short_sword");
            Put(s, "thermo_blade");

            Assert.IsTrue(s.EquipFromInventory(0));
            Assert.AreEqual("thermo_blade", s.Gear.Get(EquipmentSlot.Weapon).Id);
            CollectionAssert.AreEqual(new[] { "short_sword" }, InventoryIds(s));

            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Weapon));
            Assert.IsNull(s.Gear.Get(EquipmentSlot.Weapon));
            CollectionAssert.AreEquivalent(new[] { "short_sword", "thermo_blade" }, InventoryIds(s));
        }

        [Test]
        public void TwoHandedWeaponPushesTheShieldIntoTheInventory()
        {
            OverworldSession s = Session();
            Wear(s, "short_sword", "round_shield");
            Put(s, "plasma_drill", "tower_shield");

            Assert.IsTrue(s.EquipFromInventory(0));

            Assert.AreEqual("plasma_drill", s.Gear.Get(EquipmentSlot.Weapon).Id);
            Assert.IsNull(s.Gear.Get(EquipmentSlot.Shield));
            CollectionAssert.AreEquivalent(new[] { "tower_shield", "short_sword", "round_shield" }, InventoryIds(s));

            int shield = Array.IndexOf(InventoryIds(s), "tower_shield");
            Assert.IsFalse(s.CanEquipFromInventory(shield), "Zweihand sperrt den Schild");
            Assert.IsFalse(s.EquipFromInventory(shield));
        }

        [Test]
        public void ShieldsAreOfferedEvenWithATwoHanderAndGoToTheInventory()
        {
            OverworldSession s = Session(OnlyOffering("tower_shield"));
            Wear(s, "plasma_drill");
            s.OfferRunes("Schatztruhe");

            Assert.IsFalse(s.CanTakeItem(0, ItemPlacement.Equip));
            Assert.IsTrue(s.TakeItem(0));
            CollectionAssert.AreEqual(new[] { "tower_shield" }, InventoryIds(s));
        }

        // ------------------------------------------------------------------ Volles Inventar

        [Test]
        public void FullInventoryAsksToDiscardOrReject()
        {
            OverworldSession s = Session(OnlyOffering("thermo_blade"));
            for (int i = 0; i < s.Inventory.Capacity; i++) Assert.IsTrue(s.Inventory.TryAdd(s.Items.Get("padded_vest")));
            int overflows = 0;
            s.InventoryOverflow += () => overflows++;

            s.OfferRunes("Schatztruhe");
            Assert.IsTrue(s.TakeItem(0, ItemPlacement.Inventory));
            Assert.AreEqual("thermo_blade", s.PendingItem.Id);
            Assert.IsTrue(s.IsBusy, "Bewegung gesperrt, bis entschieden ist");
            Assert.AreEqual(1, overflows);

            Assert.IsTrue(s.RejectPendingItem());
            Assert.IsNull(s.PendingItem);
            Assert.IsFalse(s.IsBusy);
            Assert.IsFalse(s.Inventory.Contains("thermo_blade"));

            s.OfferRunes("Schatztruhe");
            s.TakeItem(0, ItemPlacement.Inventory);
            Assert.IsTrue(s.DiscardItem(0));
            Assert.IsNull(s.PendingItem);
            Assert.IsTrue(s.Inventory.Contains("thermo_blade"));
            Assert.AreEqual(s.Inventory.Capacity, s.Inventory.Count);
        }

        [Test]
        public void UnequipNeedsAFreeInventorySlot()
        {
            OverworldSession s = Session();
            Wear(s, "short_sword");
            for (int i = 0; i < s.Inventory.Capacity; i++) s.Inventory.TryAdd(s.Items.Get("padded_vest"));

            Assert.IsFalse(s.UnequipToInventory(EquipmentSlot.Weapon));
            Assert.AreEqual("short_sword", s.Gear.Get(EquipmentSlot.Weapon).Id);
        }

        // ------------------------------------------------------------------ Runen

        [Test]
        public void RuneKeepsItsLevelWhenStoredAndReinserted()
        {
            OverworldSession s = Session();
            s.Runes.TryAdd(s.RuneCatalog.Get("hp_low"), SkillIds.BasicAttack);
            Assert.IsTrue(s.Runes.Upgrade(0));
            Assert.AreEqual("HP unter 40 %", s.Runes.Rows[0].Name);

            Assert.IsTrue(s.UnequipRune(0));
            Assert.AreEqual(0, s.Runes.Rows.Count);
            Assert.AreEqual(1, s.RuneInventory.Runes.Single().Level);
            Assert.AreEqual("HP unter 40 %", s.RuneInventory.Runes.Single().Name);

            Assert.IsTrue(s.EquipRuneFromInventory(0));
            Assert.AreEqual(1, s.Runes.Rows[0].Level);
            Assert.AreEqual(0, s.RuneInventory.Count);
        }

        [Test]
        public void SwappingARuneKeepsTheSkillOnTheRow()
        {
            OverworldSession s = Session();
            Wear(s, "short_blade");
            s.Runes.TryAdd(s.RuneCatalog.Get("on_hit"), SkillIds.ArmorBreak);
            s.RuneInventory.TryAdd(new StoredRune(s.RuneCatalog.Get("hp_low"), 2));

            Assert.IsTrue(s.SwapRune(0, 0));

            Assert.AreEqual("hp_low", s.Runes.Rows[0].Rune.Id);
            Assert.AreEqual(2, s.Runes.Rows[0].Level);
            Assert.AreEqual(SkillIds.ArmorBreak, s.Runes.Rows[0].SkillId);
            Assert.AreEqual("on_hit", s.RuneInventory.Runes.Single().Rune.Id);
        }

        [Test]
        public void OffersSkipRunesThatAreAlreadyInTheInventory()
        {
            OverworldSession s = Session();
            foreach (RuneDefinition r in s.RuneCatalog.All.Where(r => r.Weight > 0 && !r.IsExclusive).Skip(3))
                if (!s.RuneInventory.TryAdd(new StoredRune(r))) s.Runes.TryAdd(r);

            RuneOffer offer = s.OfferRunes("Test");
            Assert.IsNotNull(offer);
            foreach (RuneDefinition r in offer.Options)
            {
                Assert.IsFalse(s.RuneInventory.Contains(r), r.Id);
                Assert.IsFalse(s.Runes.Contains(r), r.Id);
            }
        }

        [Test]
        public void FullRuneInventoryAsksToDiscardOrReject()
        {
            var catalog = RuneCatalog.CreateDefault();
            OverworldSession s = Session();
            var free = catalog.All.Where(r => r.Weight > 0 && !r.IsExclusive).ToList();
            for (int i = 0; i < s.Runes.Slots; i++) s.Runes.TryAdd(free[i]);
            for (int i = 0; i < s.RuneInventory.Capacity; i++) s.RuneInventory.TryAdd(new StoredRune(free[s.Runes.Slots + i]));

            RuneOffer offer = s.OfferRunes("Test");
            Assert.IsTrue(s.TakeRune(0));
            Assert.AreSame(offer.Options[0], s.PendingRune.Rune);
            Assert.IsTrue(s.IsBusy);

            Assert.IsTrue(s.DiscardRune(0));
            Assert.IsNull(s.PendingRune);
            Assert.IsTrue(s.RuneInventory.Contains(offer.Options[0]));
        }

        // ------------------------------------------------------------------ Regeln

        [Test]
        public void SetBonusesAndSetRunesFollowTheSwap()
        {
            OverworldSession s = Session();
            Wear(s, "holo_barrier");
            Put(s, "shock_absorber");
            RuneDefinition chargeFull = s.RuneCatalog.Get("charge_full");
            s.Runes.TryAdd(chargeFull, SkillIds.EmpBash);
            LogicRow Row() => BoardFactory.CreateDefault().Create(s.Runes.ToBoardSpecs(), s.Gear).Rows[0];

            Assert.IsFalse(s.IsRuneUnlocked(chargeFull));
            Assert.IsTrue(Row().IsOrphaned, "Set-Rune braucht 2 Aegis-Teile");

            Assert.IsTrue(s.EquipFromInventory(0));
            Assert.AreEqual(2, s.WornSets().Single().pieces);
            Assert.IsTrue(s.IsRuneUnlocked(chargeFull));
            Assert.IsFalse(Row().IsOrphaned);

            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Shield));
            Assert.IsFalse(s.IsRuneUnlocked(chargeFull));
            Assert.IsTrue(Row().IsOrphaned, "Ohne Schild fehlt auch der Skill");
        }

        [Test]
        public void SwappingIsLockedDuringCombatAndOpenWindows()
        {
            var combat = new MeddlingCombat();
            OverworldSession s = Session(combat: combat);
            combat.Session = s;
            Wear(s, "short_sword");
            Put(s, "thermo_blade");
            s.Map.SetContent(East, CellContent.Enemy);

            s.TryStep(East);
            Assert.IsTrue(combat.Called);
            Assert.IsFalse(combat.EquipWorked, "Im Kampf gesperrt");
            Assert.AreEqual("short_sword", s.Gear.Get(EquipmentSlot.Weapon).Id);

            Assert.IsNotNull(s.PendingRuneOffer, "Siegbelohnung offen");
            Assert.IsFalse(s.EquipFromInventory(0), "Bei offenem Fenster gesperrt");
            s.SkipRuneOffer();
            Assert.IsTrue(s.EquipFromInventory(0));
        }

        private sealed class MeddlingCombat : ICombatResolver
        {
            public OverworldSession Session;
            public bool Called;
            public bool EquipWorked;

            public CombatResult Resolve(CombatRequest request, Random random)
            {
                Called = true;
                EquipWorked = Session.EquipFromInventory(0) | Session.UnequipToInventory(EquipmentSlot.Weapon);
                return new CombatResult(true, 0, 3);
            }
        }

        [Test]
        public void ShopBuysFromTheInventoryForHalfPrice()
        {
            OverworldSession s = Session(stats: new PlayerStats(30, 0));
            Put(s, "thermo_blade");
            s.RuneInventory.TryAdd(new StoredRune(s.RuneCatalog.Get("hp_low")));
            Assert.IsFalse(s.SellItem(0), "Nur im Shop");

            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsTrue(s.SellItem(0));
            Assert.AreEqual(s.ShopPrices.Item / 2, s.Stats.Gold);
            Assert.IsTrue(s.SellRune(0));
            Assert.AreEqual(s.ShopPrices.Item / 2 + s.ShopPrices.Rune / 2, s.Stats.Gold);
            Assert.AreEqual(0, s.Inventory.Count);
            Assert.AreEqual(0, s.RuneInventory.Count);
        }

        [Test]
        public void BoughtItemsGoToTheInventoryWhenTheSlotIsTaken()
        {
            OverworldSession s = Session(OnlyOffering("thermo_blade"));
            Wear(s, "short_sword");
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsTrue(s.BuyShopItem(0));
            Assert.AreEqual("short_sword", s.Gear.Get(EquipmentSlot.Weapon).Id);
            CollectionAssert.AreEqual(new[] { "thermo_blade" }, InventoryIds(s));
        }

        [Test]
        public void InventoryTravelsToTheNextAct()
        {
            OverworldSession first = OverworldSession.Create(new MapGenerationConfig { Seed = 4 }, kit: KnightKit.Defaults[0]);
            Put(first, "thermo_blade");
            first.RuneInventory.TryAdd(new StoredRune(first.RuneCatalog.Get("hp_low"), 1));

            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Seed = 4 }, first);

            Assert.AreSame(first.Inventory, next.Inventory);
            Assert.AreSame(first.RuneInventory, next.RuneInventory);
            Assert.AreEqual(1, next.RuneInventory.Runes.Single().Level);
        }
    }
}
