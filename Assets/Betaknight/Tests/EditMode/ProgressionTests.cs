using System;
using System.Collections.Generic;
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
    /// <summary>Build verbessern statt austauschen: Tafel-Erweiterung, Stufen, Angebote mit Verbesserung, Shop-Preis, Elite.</summary>
    public class ProgressionTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly EquipmentCatalog AllItems = EquipmentCatalog.CreateDefault();

        private sealed class RecordingCombat : ICombatResolver
        {
            public CombatRequest LastRequest;

            public CombatResult Resolve(CombatRequest request, Random random)
            {
                LastRequest = request;
                return new CombatResult(true, 1, 3);
            }
        }

        /// <summary>Katalog, in dem nur die genannten Teile angeboten werden (alle anderen Gewicht 0).</summary>
        private static EquipmentCatalog Offering(params string[] ids) =>
            new EquipmentCatalog(AllItems.All.Select(i => ids.Contains(i.Id) ? i
                : new EquipmentDefinition(i.Id, i.BaseName, i.Slot, i.Stats.ToDictionary(p => p.Key, p => p.Value), i.Passives, i.SetId, i.TwoHanded, 0)));

        private static OverworldSession Session(EquipmentCatalog items = null, RuneCatalog runes = null, ICombatResolver combat = null,
            int gold = 200, int seed = 11)
        {
            var map = new HexMap(HexCoord.Zero, 4, seed);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(40, gold), null, null, runes, combat ?? new RecordingCombat(), null, items ?? Offering());
        }

        private static List<string> Messages(OverworldSession s)
        {
            var list = new List<string>();
            s.BuildImproved += list.Add;
            return list;
        }

        // ------------------------------------------------------------------ Tafel-Erweiterung

        [Test]
        public void BoardExpandsUpToTheLimit()
        {
            OverworldSession s = Session();
            List<string> messages = Messages(s);
            Assert.AreEqual(3, s.Runes.Slots);

            while (s.ExpandBoard(1)) { }

            Assert.AreEqual(8, s.Runes.Slots);
            Assert.AreEqual(s.Progression.MaxBoardRows, s.Runes.Slots);
            Assert.IsFalse(s.CanExpandBoard);
            Assert.AreEqual(5, messages.Count);
            Assert.AreEqual("Board 3 → 4 rows", messages[0]);
        }

        [Test]
        public void BossEscapeAndNewActEachGrantARow()
        {
            OverworldSession s = Session();
            while (!s.PendingPortal && s.Turns.CurrentTurn < OverworldSession.BossInterval)
            {
                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.TryStep(s.Player.Position == HexCoord.Zero ? East : HexCoord.Zero);
            }
            Assert.AreEqual(4, s.Runes.Slots, "Boss-Flucht");

            Assert.IsTrue(s.EnterPortal());
            Assert.AreEqual(5, s.Runes.Slots, "Akt-Wechsel");
            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Seed = 2 }, s);
            Assert.AreEqual(5, next.Runes.Slots);
        }

        [Test]
        public void EliteFightsAreTougherAndCanOfferABoardExpansion()
        {
            var combat = new RecordingCombat();
            OverworldSession s = Session(combat: combat);
            s.UseProgression(new ProgressionConfig { EliteBoardExpansionChance = 100 });
            s.Map.SetContent(East, CellContent.Elite);

            s.TryStep(East);

            Assert.AreEqual(CellContent.Elite, combat.LastRequest.Enemy);
            Assert.AreEqual(1 + s.Progression.EliteTierBonus, combat.LastRequest.Tier);
            Assert.AreEqual(s.Progression.EliteHpPercent, combat.LastRequest.EnemyHpPercent);
            Assert.AreEqual(3 + s.Progression.EliteGoldBonus, s.Stats.Gold - 200);
            Assert.IsTrue(s.PendingRuneOffer.BoardExpansion);

            Assert.IsTrue(s.TakeBoardExpansion());
            Assert.AreEqual(4, s.Runes.Slots);
            Assert.IsNull(s.PendingRuneOffer);
        }

        [Test]
        public void ElitesAreScaledInTheArena()
        {
            var resolver = new ArenaCombatResolver();
            var normal = resolver.Resolve(new CombatRequest(CellContent.Enemy, 3, new PlayerStats(40, 0), new RuneLoadout()), new Random(4));
            var elite = resolver.Resolve(new CombatRequest(CellContent.Elite, 3, new PlayerStats(40, 0), new RuneLoadout(),
                enemyHpPercent: 150, enemyDamagePercent: 125), new Random(4));

            StringAssert.StartsWith("Elite: ", elite.EnemyName);
            Assert.AreEqual(normal.Battle.Fighters[1].MaxHp * 150 / 100, elite.Battle.Fighters[1].MaxHp);
        }

        [Test]
        public void EliteFieldsAppearFromRingThreeOnGeneratedMaps()
        {
            HexMap map = MapGenerator.Generate(new MapGenerationConfig { Seed = 3 });
            List<HexCell> elites = map.Cells.Where(c => c.Content == CellContent.Elite).ToList();

            Assert.IsNotEmpty(elites);
            Assert.IsTrue(elites.All(c => c.Coord.DistanceTo(map.Center) >= 3));
            Assert.IsTrue(CellContent.Elite.IsHostile());
        }

        // ------------------------------------------------------------------ Stufen

        [Test]
        public void DuplicateRuneRaisesItsLevel()
        {
            RuneCatalog runes = new RuneCatalog(new[] { RuneCatalog.CreateDefault().Get("hp_low") });
            OverworldSession s = Session(runes: runes);
            List<string> messages = Messages(s);
            s.Runes.TryAdd(runes.Get("hp_low"), SkillIds.BasicAttack);

            RuneOffer offer = s.OfferRunes(RewardSources.Victory);
            Assert.AreEqual("hp_low", offer.Options.Single().Id, "Die einzige Verbesserung ist die vorhandene Rune");
            Assert.IsTrue(s.TakeRune(0));

            Assert.AreEqual(1, s.Runes.Rows.Single().Level);
            Assert.AreEqual("HP Below 40 %", s.Runes.Rows.Single().Name);
            Assert.AreEqual("Rune HP Below 30 % → HP Below 40 %", messages.Last());
        }

        [Test]
        public void DuplicateRuneInTheInventoryRaisesItsLevelThere()
        {
            RuneCatalog runes = new RuneCatalog(new[] { RuneCatalog.CreateDefault().Get("hp_low") });
            OverworldSession s = Session(runes: runes);
            s.RuneInventory.TryAdd(new StoredRune(runes.Get("hp_low"), 1));

            s.OfferRunes(RewardSources.Victory);
            Assert.IsTrue(s.TakeRune(0));

            Assert.AreEqual(2, s.RuneInventory.Runes.Single().Level);
            Assert.AreEqual(0, s.Runes.Rows.Count);
        }

        [Test]
        public void DuplicateItemRaisesLevelAndStats()
        {
            // Nur eine Rune im Katalog, damit keine passende neue Rune die Verbesserung schon stellt.
            RuneCatalog runes = new RuneCatalog(new[] { RuneCatalog.CreateDefault().Get("always") });
            OverworldSession s = Session(runes: runes);
            List<string> messages = Messages(s);
            s.Gear.Equip(s.Items.Get("short_blade"));
            s.Runes.TryAdd(runes.Get("always"), SkillIds.ArmorBreak);

            RuneOffer offer = s.OfferRunes(RewardSources.Victory);
            Assert.Contains("short_blade", offer.ItemIds.ToList(), "Stufe für das getragene Teil wird angeboten");
            Assert.IsTrue(s.TakeItem(offer.ItemIds.ToList().IndexOf("short_blade")));

            EquipmentDefinition blade = s.Gear.Get(EquipmentSlot.Weapon);
            Assert.AreEqual(1, blade.Level);
            Assert.AreEqual("Short Blade +1", blade.Name);
            Assert.AreEqual(3, blade.StatBonus(StatKind.Damage), "2 + 50 %");
            Assert.AreEqual(-3, blade.StatBonus(StatKind.AttackInterval));
            Assert.AreEqual(0, s.Inventory.Count, "Kein zweites Exemplar");

            // A-05: Die Teilstufe hebt nur Werte; die Skill-Stufe gehört dem Skill-Exemplar.
            SkillInfo info = s.DescribeSkill(SkillIds.ArmorBreak);
            Assert.AreEqual(BasisPoints.Percent(190), info.Effects.First(e => e.IsDamage).DamageBp, "Rüstungsbruch seit A-12: 190 %");
            StringAssert.Contains("Short Blade → Short Blade +1", messages.Last());
            StringAssert.Contains("Weapon Damage 2 → 3", messages.Last());
        }

        [Test]
        public void GrownSkillsHitHarderInTheSimulator()
        {
            // Begründet angepasst (A-08): Stufen geben keinen eigenen Schaden mehr, die Wirkung kommt aus dem Wachstum
            // (Rüstungsbruch: +1 Schaden pro Kill, hier 7 Punkte).
            var gear = new Equipment();
            gear.Equip(AllItems.Get("short_blade").AtLevel(2, 50));
            var skills = new Betaknight.Core.Skills.SkillCollection();
            Betaknight.Core.Skills.SkillInstance breaker = skills.Add(SkillIds.ArmorBreak);
            skills.Grow(breaker, 7);
            var loadout = new RuneLoadout();
            loadout.TryAdd(RuneCatalog.CreateDefault().Get("always"), breaker);
            var rules = new SkillLevelRules();

            var request = new CombatRequest(CellContent.Enemy, 0, new PlayerStats(30, 0), loadout, gear, null, rules);
            var dummy = new CombatantSetup { Name = "Sandsack", Stats = new CombatStats(9999, 0) };
            BattleSetup setup = new ArenaCombatResolver().CreateSetup(request, new List<CombatantSetup> { dummy }, 1);
            setup.MaxTicks = Ticks.FromSeconds(3);
            BattleResult r = CombatSimulation.Run(setup);

            int weapon = 5 + 2 + 2 * 1;
            BattleEvent hit = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Detail == SkillIds.ArmorBreak);
            Assert.AreEqual(BasisPoints.Of(weapon, BasisPoints.Percent(190)) + 7, hit.Amount);
        }

        [Test]
        public void ItemsStopAtTheMaximumLevel()
        {
            OverworldSession s = Session();
            s.Gear.Equip(s.Items.Get("short_blade").AtLevel(s.Progression.MaxItemLevel, 50));

            Assert.IsFalse(s.CanUpgradeItem("short_blade"));
            Assert.IsFalse(s.IsImprovement(s.Items.Get("short_blade")));
        }

        // ------------------------------------------------------------------ Angebote

        [Test]
        public void FightRewardsAlwaysContainAnImprovement()
        {
            foreach (int seed in Enumerable.Range(1, 40))
            {
                OverworldSession s = Session(items: AllItems, seed: seed);
                RuneCatalog runes = s.RuneCatalog;
                if (seed % 3 == 0) s.Gear.Equip(s.Items.Get("thermo_blade"));
                if (seed % 2 == 0) s.Runes.TryAdd(runes.Get("hp_low"));
                else s.Runes.TryAdd(runes.Get("every_5s"));

                RuneOffer offer = s.OfferRunes(seed % 4 == 0 ? RewardSources.MineDefended : RewardSources.Victory);
                bool improves = offer.Options.Any(s.IsImprovement)
                    || offer.ItemIds.Any(id => s.IsImprovement(s.Items.Get(id)));
                Assert.IsTrue(improves, $"Seed {seed}");
            }
        }

        // ------------------------------------------------------------------ Shop

        [Test]
        public void ShopSlotPriceRisesPerRun()
        {
            OverworldSession s = Session(gold: 200);
            HexCoord west = new HexCoord(-1, 0);
            s.Map.SetContent(East, CellContent.Shop);
            s.Map.SetContent(west, CellContent.Shop);

            Assert.AreEqual(20, s.RuneSlotPrice);
            s.TryStep(East);
            Assert.IsTrue(s.BuyRuneSlot());
            Assert.AreEqual(180, s.Stats.Gold);
            Assert.AreEqual(35, s.RuneSlotPrice);
            Assert.IsFalse(s.CanBuyRuneSlot, "Ein Platz pro Shop");
            s.LeaveShop();

            s.TryStep(HexCoord.Zero);
            s.TryStep(west);
            Assert.IsTrue(s.BuyRuneSlot());
            Assert.AreEqual(145, s.Stats.Gold);
            Assert.AreEqual(50, s.RuneSlotPrice);
            Assert.AreEqual(5, s.Runes.Slots);
        }

        [Test]
        public void ShopSlotRespectsTheBoardLimit()
        {
            OverworldSession s = Session(gold: 500);
            while (s.ExpandBoard(1)) { }
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsFalse(s.CanBuyRuneSlot);
            Assert.IsFalse(s.BuyRuneSlot());
        }

        [Test]
        public void ProgressionValuesComeFromOneConfig()
        {
            var config = new ProgressionConfig { MaxBoardRows = 4, SlotPriceBase = 10, SlotPriceStep = 5 };
            OverworldSession s = Session();
            s.UseProgression(config);

            Assert.AreEqual(10, s.RuneSlotPrice);
            Assert.AreEqual(20, config.SlotPrice(2));
            Assert.IsTrue(s.ExpandBoard(5));
            Assert.AreEqual(4, s.Runes.Slots);
        }
    }
}
