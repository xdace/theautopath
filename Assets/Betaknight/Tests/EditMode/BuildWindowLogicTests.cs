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
using Betaknight.Core.Skills;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Die Logik hinter den Drops der Fenster «Build» und «Inventar»: Item-Raster mit fester Reihenfolge, Anlegen auf
    /// den passenden Platz, Ablegen in eine Zelle, Skills und Runen an der Tafel, Sperre bei offenen Entscheidungen, Stat-Vorschau.
    /// </summary>
    public class BuildWindowLogicTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        private static OverworldSession Session()
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(30, 50));
        }

        private static EquipmentDefinition Item(OverworldSession s, string id) => s.Items.Get(id);

        private static string CellId(OverworldSession s, int cell) => s.Inventory.ItemAt(cell)?.Id;

        private sealed class TestConsumable : IInventoryItem
        {
            public string Id => "repair_kit";
            public string Name => "Reparatur-Set";
            public InventoryItemKind ItemKind => InventoryItemKind.Consumable;
        }

        // ------------------------------------------------------------------ Item-Raster

        [Test]
        public void TheGridMovesOntoEmptyCellsAndSwapsOnFullOnes()
        {
            OverworldSession s = Session();
            s.Inventory.TryAdd(Item(s, "padded_vest"));
            s.Inventory.TryAdd(Item(s, "round_shield"));

            Assert.IsTrue(s.MoveInventoryItem(0, 5), "auf leere Zelle verschieben");
            Assert.IsNull(CellId(s, 0));
            Assert.AreEqual("padded_vest", CellId(s, 5));

            Assert.IsTrue(s.MoveInventoryItem(1, 5), "auf volle Zelle tauschen");
            Assert.AreEqual("padded_vest", CellId(s, 1));
            Assert.AreEqual("round_shield", CellId(s, 5));

            Assert.IsFalse(s.MoveInventoryItem(0, 3), "leere Zelle lässt sich nicht ziehen");
            Assert.IsFalse(s.MoveInventoryItem(1, s.Inventory.Capacity), "ausserhalb des Rasters");
        }

        [Test]
        public void RemovingLeavesAGapAndNewItemsFillTheFirstFreeCell()
        {
            OverworldSession s = Session();
            s.Inventory.TryAdd(Item(s, "padded_vest"));
            s.Inventory.TryAdd(Item(s, "round_shield"));
            s.Inventory.TryAdd(Item(s, "light_boots"));

            s.Inventory.RemoveAt(1);
            Assert.AreEqual("light_boots", CellId(s, 2), "Die Reihenfolge bleibt, nichts rückt nach");
            Assert.AreEqual(new[] { "padded_vest", "light_boots" }, s.Inventory.Items.Select(i => i.Id).ToArray());

            s.Inventory.TryAdd(Item(s, "iron_greaves"));
            Assert.AreEqual("iron_greaves", CellId(s, 1));
        }

        [Test]
        public void TheGridTakesConsumablesButOnlyEquipmentCountsAsGear()
        {
            OverworldSession s = Session();
            Assert.IsTrue(s.Inventory.TryPlace(new TestConsumable(), 4));
            s.Inventory.TryAdd(Item(s, "padded_vest"));

            Assert.AreEqual(2, s.Inventory.Count);
            Assert.AreEqual(InventoryItemKind.Consumable, s.Inventory.ItemAt(4).ItemKind);
            Assert.IsNull(s.Inventory[4], "kein Ausrüstungsteil");
            Assert.IsFalse(s.EquipFromInventory(4));
            Assert.AreEqual(new[] { "padded_vest" }, s.Inventory.Items.Select(i => i.Id).ToArray());
        }

        [Test]
        public void TheGridOrderTravelsThroughTheActs()
        {
            OverworldSession s = Session();
            s.Inventory.TryPlace(Item(s, "padded_vest"), 7);
            s.Inventory.TryPlace(Item(s, "round_shield"), 2);

            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Radius = 4 }, s);
            Assert.AreEqual("padded_vest", CellId(next, 7));
            Assert.AreEqual("round_shield", CellId(next, 2));
        }

        // ------------------------------------------------------------------ Figur

        [Test]
        public void ItemsOnlyGoOnTheirOwnSlotAndTheOldPieceTakesTheCell()
        {
            OverworldSession s = Session();
            s.Gear.Equip(Item(s, "short_sword"));
            s.Inventory.TryPlace(Item(s, "thermo_blade"), 3);

            Assert.IsFalse(s.CanEquipTo(3, EquipmentSlot.Helmet), "falscher Platz");
            Assert.IsFalse(s.EquipFromInventoryTo(3, EquipmentSlot.Helmet));
            Assert.AreEqual("thermo_blade", CellId(s, 3));

            Assert.IsTrue(s.EquipFromInventoryTo(3, EquipmentSlot.Weapon));
            Assert.AreEqual("thermo_blade", s.Gear.Get(EquipmentSlot.Weapon).Id);
            Assert.AreEqual("short_sword", CellId(s, 3), "Das alte Teil landet in derselben Zelle");
        }

        [Test]
        public void ATwoHanderPutsTheShieldIntoTheNextFreeCell()
        {
            OverworldSession s = Session();
            s.Gear.Equip(Item(s, "short_sword"));
            s.Gear.Equip(Item(s, "round_shield"));
            s.Inventory.TryPlace(Item(s, "plasma_drill"), 4);

            Assert.IsTrue(s.EquipFromInventoryTo(4, EquipmentSlot.Weapon));
            Assert.AreEqual("short_sword", CellId(s, 4));
            Assert.AreEqual("round_shield", CellId(s, 0));
            Assert.IsNull(s.Gear.Get(EquipmentSlot.Shield));
        }

        [Test]
        public void UnequippingDropsIntoTheChosenCellOrSwapsWithAFittingPiece()
        {
            OverworldSession s = Session();
            s.Gear.Equip(Item(s, "padded_vest"));
            s.Gear.Equip(Item(s, "light_boots"));
            s.Inventory.TryPlace(Item(s, "bio_plating"), 1);
            s.Inventory.TryPlace(Item(s, "round_shield"), 2);

            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Boots, 6), "in eine leere Zelle");
            Assert.AreEqual("light_boots", CellId(s, 6));
            Assert.IsNull(s.Gear.Get(EquipmentSlot.Boots));

            Assert.IsFalse(s.CanUnequipToInventory(EquipmentSlot.Chest, 2), "dort liegt ein Schild: verweigert");
            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Chest, 1), "auf eine passende Brust: tauschen");
            Assert.AreEqual("bio_plating", s.Gear.Get(EquipmentSlot.Chest).Id);
            Assert.AreEqual("padded_vest", CellId(s, 1));
        }

        [Test]
        public void DraggingIsLockedWhileADecisionIsOpen()
        {
            OverworldSession s = Session();
            s.Inventory.TryAdd(Item(s, "padded_vest"));
            s.Inventory.TryAdd(Item(s, "round_shield"));
            s.OfferRunes(RewardSources.Elite);
            Assume.That(s.PendingRuneOffer, Is.Not.Null);

            Assert.IsFalse(s.CanChangeLoadout);
            Assert.IsFalse(s.MoveInventoryItem(0, 1));
            Assert.IsFalse(s.EquipFromInventoryTo(0, EquipmentSlot.Chest));
            Assert.IsFalse(s.UnequipToInventory(EquipmentSlot.Weapon, 5));

            s.SkipRuneOffer();
            Assert.IsTrue(s.MoveInventoryItem(0, 1));
        }

        // ------------------------------------------------------------------ Tafel

        [Test]
        public void SkillsGoIntoRowsSwapAndComeBackOut()
        {
            OverworldSession s = Session();
            SkillInstance drill = s.Skills.Add(SkillIds.Drill);
            SkillInstance bash = s.Skills.Add(SkillIds.ShieldBash);
            SkillInstance ignite = s.Skills.Add(SkillIds.Ignite);
            Assert.IsTrue(s.Runes.TryAdd(s.RuneCatalog.Get("always"), drill));
            Assert.IsTrue(s.Runes.TryAdd(s.RuneCatalog.Get("hp_low"), bash));

            Assert.IsTrue(s.PlaceSkill(ignite.InstanceId, 0), "auf eine besetzte Zeile: einsetzen, der alte wird frei");
            Assert.AreSame(ignite, s.Runes.Rows[0].Skill);
            Assert.IsTrue(drill.IsFree);

            Assert.IsTrue(s.SwapSkills(0, 1), "Zeile auf Zeile: tauschen");
            Assert.AreSame(bash, s.Runes.Rows[0].Skill);
            Assert.AreSame(ignite, s.Runes.Rows[1].Skill);

            Assert.IsTrue(s.RemoveSkill(1), "zurück ins Skill-Inventar");
            Assert.IsTrue(ignite.IsFree);
            Assert.IsNull(s.Runes.Rows[1].Skill);
        }

        [Test]
        public void RunesFromTheInventorySwapIntoRowsOrBecomeANewRowAtTheDropPosition()
        {
            OverworldSession s = Session();
            s.Runes.TryAdd(s.RuneCatalog.Get("always"), s.Skills.Add(SkillIds.Drill));
            s.Runes.TryAdd(s.RuneCatalog.Get("hp_low"), s.Skills.Add(SkillIds.Repair));
            s.ExpandBoard(2);
            s.RuneInventory.TryAdd(new StoredRune(s.RuneCatalog.Get("enemy_low"), 1));
            s.RuneInventory.TryAdd(new StoredRune(s.RuneCatalog.Get("hp_critical"), 0));

            Assert.IsTrue(s.SwapRune(0, 0), "auf eine Zeile: tauschen");
            Assert.AreEqual("enemy_low", s.Runes.Rows[0].Rune.Id);
            Assert.AreEqual(1, s.Runes.Rows[0].Level, "Stufe kommt mit");
            Assert.AreEqual("always", s.RuneInventory.Runes[0].Rune.Id);

            int hpCritical = s.RuneInventory.Runes.ToList().FindIndex(r => r.Rune.Id == "hp_critical");
            Assert.IsTrue(s.EquipRuneFromInventory(hpCritical, 1), "auf freien Platz zwischen Zeile 1 und 2");
            Assert.AreEqual(new[] { "enemy_low", "hp_critical", "hp_low" }, s.Runes.Rows.Select(r => r.Rune.Id).ToArray());

            Assert.IsTrue(s.MoveRow(2, 0), "Zeilen untereinander ziehen");
            Assert.AreEqual("hp_low", s.Runes.Rows[0].Rune.Id);
        }

        // ------------------------------------------------------------------ Stat-Leiste

        [Test]
        public void TheStatBarPreviewsWhatAPieceChanges()
        {
            OverworldSession s = Session();
            s.Gear.Equip(Item(s, "round_shield"));
            BuildStats now = s.StatsNow();
            Assert.AreEqual(30, now.Hp);

            var changes = s.PreviewEquip(Item(s, "tower_shield"));
            StatChange armor = changes.Single(c => c.Label == "Armor");
            Assert.AreEqual(1, armor.Sign, "mehr Rüstung ist besser");
            Assert.AreEqual(now.Armor.ToString(), armor.Before);
            Assert.Greater(int.Parse(armor.After), now.Armor);

            StatChange lost = s.PreviewUnequip(EquipmentSlot.Shield).Single(c => c.Label == "Armor");
            Assert.AreEqual(-1, lost.Sign);
            Assert.AreEqual(now.Armor, s.StatsNow().Armor, "Die Vorschau ändert die echte Ausrüstung nicht");
        }

        [Test]
        public void TheStatBarListsActiveSetsAndTags()
        {
            OverworldSession s = Session();
            foreach (EquipmentDefinition item in s.Items.All.Where(i => i.SetId != null).GroupBy(i => i.SetId).First().Take(2))
                s.Gear.Equip(item);
            Assert.IsTrue(s.StatsNow().Bonuses.Any(b => b.EndsWith("2/3")), string.Join(", ", s.StatsNow().Bonuses));
        }
    }
}
