using System.Collections.Generic;
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
            // «Charge Full» (●●●) versorgt den EMP-Schildschlag (2×2) rechts daneben; die Ladung ist von Anfang an voll.
            var spec = new CircuitSpec();
            spec.Relays.Add(new RelaySpec("charge_full", new Betaknight.Core.Circuit.Cell(0, 0)));
            spec.Components.Add(new ComponentSpec(SkillIds.EmpBash, new Betaknight.Core.Circuit.Cell(1, 0)));
            int Bashes()
            {
                LogicBoard board = factory.Create(spec, gear);
                Assert.IsTrue(board.Rows[0].IsPowered);
                CombatantSetup knight = ArenaSimulationTests.Fighter("A", 1000, 1, board: board);
                knight.Resources[ResourceIds.Charge] = 5;
                BattleResult r = CombatSimulation.Run(ArenaSimulationTests.Duel(knight, ArenaSimulationTests.Fighter("B", 100000, 0, 1000)));
                return r.Events.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.EmpBash);
            }

            Assert.AreEqual(0, Bashes(), "1 Teil reicht nicht: das Relais löst nie aus.");
            gear.Equip(EquipmentCatalog.CreateDefault().Get("mag_anchors"));
            Assert.Greater(Bashes(), 0);
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
        public void ShopShieldsNextToATwoHanderOnlyGoToTheInventory()
        {
            // Seit dem Inventar (A-02) dürfen Schilde trotz Zweihandwaffe ins Angebot, anlegen geht aber nicht.
            OverworldSession s = Session();
            Wear(s, "plasma_drill");
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            IReadOnlyList<string> offered = s.PendingShop.Inventory.ItemIds;
            for (int i = 0; i < offered.Count; i++)
                if (s.Items.Get(offered[i]).Slot == EquipmentSlot.Shield)
                    Assert.IsFalse(s.CanBuyShopItem(i, ItemPlacement.Equip), "Die Zweihandwaffe sperrt den Schild.");
        }
    }
}
