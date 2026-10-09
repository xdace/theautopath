using System;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
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
    /// <summary>Inventar: sammeln und wechseln statt ersetzen, mit allen Ausrüstungs- und Platinenregeln.</summary>
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
                : new EquipmentDefinition(i.Id, i.Name, i.Slot, i.Stats.ToDictionary(p => p.Key, p => p.Value), i.Passives, i.SetId, i.TwoHanded, 0)));

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
            s.OfferRunes("Treasure Chest");
            Assert.IsTrue(s.TakeItem(0));
            Assert.AreEqual("thermo_blade", s.Gear.Get(EquipmentSlot.Weapon).Id, "Platz war frei");

            OverworldSession t = Session(OnlyOffering("thermo_blade"));
            Wear(t, "short_sword");
            t.OfferRunes("Treasure Chest");
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
            s.OfferRunes("Treasure Chest");

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

            s.OfferRunes("Treasure Chest");
            Assert.IsTrue(s.TakeItem(0, ItemPlacement.Inventory));
            Assert.AreEqual("thermo_blade", s.PendingItem.Id);
            Assert.IsTrue(s.IsBusy, "Bewegung gesperrt, bis entschieden ist");
            Assert.AreEqual(1, overflows);

            Assert.IsTrue(s.RejectPendingItem());
            Assert.IsNull(s.PendingItem);
            Assert.IsFalse(s.IsBusy);
            Assert.IsFalse(s.Inventory.Contains("thermo_blade"));

            s.OfferRunes("Treasure Chest");
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
            s.Board.AddRelay(s.RuneCatalog.Get("hp_low"), new Cell(2, 0));
            Assert.IsTrue(s.Board.Upgrade(0));
            Assert.AreEqual("HP Below 25 %", s.Board.Relays[0].Name);

            Assert.IsTrue(s.UnequipRune(0));
            Assert.AreEqual(0, s.Board.Relays.Count);
            Assert.AreEqual(1, s.RuneInventory.Runes.Single().Level);
            Assert.AreEqual("HP Below 25 %", s.RuneInventory.Runes.Single().Name);

            Assert.IsTrue(s.EquipRuneFromInventory(0, new Cell(3, 2)));
            Assert.AreEqual(1, s.Board.Relays[0].Level);
            Assert.AreEqual(new Cell(3, 2), s.Board.Relays[0].Position);
            Assert.AreEqual(0, s.RuneInventory.Count);
        }

        [Test]
        public void SwappingARuneKeepsTheRelayInPlace()
        {
            // A-19: Der Tausch wechselt nur die Rune des Relais; Lage und die versorgte Komponente bleiben.
            OverworldSession s = Session();
            Wear(s, "short_blade");
            s.Board.AddRelay(s.RuneCatalog.Get("on_hit"), new Cell(2, 0));
            Assert.IsTrue(s.PlaceSkill(s.Skills.Add(SkillIds.ShockStab).InstanceId, new Cell(3, 0)));
            s.RuneInventory.TryAdd(new StoredRune(s.RuneCatalog.Get("hp_low"), 1));

            Assert.IsTrue(s.SwapRune(0, 0));

            Assert.AreEqual("hp_low", s.Board.Relays[0].Rune.Id);
            Assert.AreEqual(1, s.Board.Relays[0].Level);
            Assert.AreEqual(new Cell(2, 0), s.Board.Relays[0].Position);
            Assert.AreEqual(SkillIds.ShockStab, s.Board.ComponentsTouching(s.Board.Relays[0]).Single().Skill.SkillId);
            Assert.AreEqual("on_hit", s.RuneInventory.Runes.Single().Rune.Id);
        }

        [Test]
        public void OffersSkipRunesThatAreAlreadyInTheInventory()
        {
            OverworldSession s = Session();
            foreach (RuneDefinition r in s.RuneCatalog.All.Where(r => r.Weight > 0 && !r.IsExclusive).Skip(3))
                if (!s.RuneInventory.TryAdd(new StoredRune(r))) s.Board.AddRelay(r);

            RuneOffer offer = s.OfferRunes("Test");
            Assert.IsNotNull(offer);
            foreach (RuneDefinition r in offer.Options)
            {
                Assert.IsFalse(s.RuneInventory.Contains(r), r.Id);
                Assert.IsFalse(s.Board.Contains(r), r.Id);
            }
        }

        [Test]
        public void FullRuneInventoryAsksToDiscardOrReject()
        {
            var catalog = RuneCatalog.CreateDefault();
            OverworldSession s = Session();
            var free = catalog.All.Where(r => r.Weight > 0 && !r.IsExclusive).ToList();
            // Platine voll mit Relais (kein Platz für ein weiteres), Runen-Inventar voll.
            int onBoard = 0;
            while (!s.Board.IsFull) Assert.IsNotNull(s.Board.AddRelay(free[onBoard++]));
            for (int i = 0; i < s.RuneInventory.Capacity; i++) s.RuneInventory.TryAdd(new StoredRune(free[onBoard + i]));

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
            s.Board.AddRelay(chargeFull, new Cell(1, 0));
            Assert.IsTrue(s.PlaceSkill(s.Skills.Add(SkillIds.EmpBash).InstanceId, new Cell(2, 0)), "2×2 rechts am Relais");

            // A-19: Eine gesperrte Set-Rune ergibt ein totes Relais (löst nie aus); die Komponente bleibt liegen.
            LogicBoard Compiled() => BoardFactory.CreateDefault().Create(s.Board.ToSpec(), s.Gear);
            bool RelayDead() => Compiled().Relays[0].Condition is NotCondition not && not.Inner is AlwaysCondition;

            Assert.IsFalse(s.IsRuneUnlocked(chargeFull));
            Assert.IsTrue(RelayDead(), "Set-Rune braucht 2 Aegis-Teile");
            Assert.IsFalse(Compiled().Rows[0].IsOrphaned);

            Assert.IsTrue(s.EquipFromInventory(0));
            Assert.AreEqual(2, s.WornSets().Single().pieces);
            Assert.IsTrue(s.IsRuneUnlocked(chargeFull));
            Assert.IsFalse(RelayDead());

            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Shield));
            Assert.IsFalse(s.IsRuneUnlocked(chargeFull));
            Assert.IsTrue(RelayDead(), "Ohne Schild fehlt das Set");
            Assert.AreEqual(SkillIds.EmpBash, Compiled().Rows[0].Skill.Id, "A-05: Der Skill gehört der Komponente, nicht dem Schild");
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
