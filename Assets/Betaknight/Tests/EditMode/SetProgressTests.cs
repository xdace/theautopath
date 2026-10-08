using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
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
    /// <summary>Sets im Run: freigeschaltete Runen, bevorzugte Set-Teile, Ausrüstung im Shop, Anzeige-Daten.</summary>
    public class SetProgressTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        private static OverworldSession Session(int gold = 100)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(30, gold));
        }

        private static void Wear(OverworldSession s, params string[] ids)
        {
            foreach (string id in ids) Assert.IsNotNull(s.Gear.Equip(s.Items.Get(id)), id);
        }

        [Test]
        public void EverySetHasTextsForItsBonuses()
        {
            var sets = SetBonusRegistry.CreateDefault();
            foreach (string setId in EquipmentCatalog.CreateDefault().All.Where(i => i.SetId != null).Select(i => i.SetId).Distinct())
            {
                Assert.IsTrue(sets.TryGet(setId, out SetDefinition set), setId);
                CollectionAssert.AreEquivalent(new[] { 2, 3 }, set.Bonuses.Keys, setId);
                Assert.IsTrue(set.Bonuses.Values.All(t => t.Length > 0), setId);
            }
        }

        [Test]
        public void AegisUnlocksChargeFullInOffers()
        {
            OverworldSession s = Session();
            RuneDefinition chargeFull = s.RuneCatalog.Get("charge_full");

            for (int i = 0; i < 20; i++)
            {
                RuneOffer offer = s.OfferRunes("Test");
                CollectionAssert.DoesNotContain(offer.Options, chargeFull);
                s.SkipRuneOffer();
            }

            Wear(s, "holo_barrier", "shock_absorber");
            Assert.IsTrue(s.IsRuneUnlocked(chargeFull));
            Assert.AreSame(chargeFull, s.OfferRunes("Test").Options[0], "Freigeschaltete Rune kommt garantiert.");
        }

        [Test]
        public void SetRunesOnlyWorkWhileTheSetIsWorn()
        {
            var factory = BoardFactory.CreateDefault();
            var gear = new Equipment();
            gear.Equip(EquipmentCatalog.CreateDefault().Get("holo_barrier"));
            var row = new[] { new BoardRowSpec("charge_full", SkillIds.EmpBash) };

            Assert.IsTrue(factory.Create(row, gear).Rows[0].IsOrphaned, "1 Teil reicht nicht.");
            gear.Equip(EquipmentCatalog.CreateDefault().Get("mag_anchors"));
            Assert.IsFalse(factory.Create(row, gear).Rows[0].IsOrphaned);
        }

        [Test]
        public void StartedSetsAreOfferedMoreOften()
        {
            OverworldSession s = Session();
            EquipmentDefinition visor = s.Items.Get("warning_visor");
            int before = s.OfferWeight(visor);
            Wear(s, "thermo_blade");
            Assert.AreEqual(before * OverworldSession.StartedSetWeightFactor, s.OfferWeight(visor));
            Assert.AreEqual(s.Items.Get("echo_helm").Weight, s.OfferWeight(s.Items.Get("echo_helm")));
        }

        [Test]
        public void ShopSellsEquipment()
        {
            OverworldSession s = Session(gold: 30);
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.AreEqual(OverworldSession.ShopItemCount, s.PendingShop.Inventory.ItemIds.Count);
            Assert.AreNotEqual(s.PendingShop.Inventory.ItemIds[0], s.PendingShop.Inventory.ItemIds[1]);

            string id = s.PendingShop.Inventory.ItemIds[0];
            EquipmentDefinition item = s.Items.Get(id);
            Assert.IsTrue(s.BuyShopItem(0));
            Assert.AreSame(item, s.Gear.Get(item.Slot));
            Assert.AreEqual(30 - s.ShopPrices.Item, s.Stats.Gold);
            CollectionAssert.DoesNotContain(s.PendingShop.Inventory.ItemIds, id);
        }

        [Test]
        public void ShopRefusesAShieldNextToATwoHander()
        {
            OverworldSession s = Session();
            Wear(s, "plasma_drill");
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsTrue(s.PendingShop.Inventory.ItemIds.All(id => s.Items.Get(id).Slot != EquipmentSlot.Shield),
                "Nicht anlegbare Teile kommen gar nicht ins Angebot.");
        }
    }
}
