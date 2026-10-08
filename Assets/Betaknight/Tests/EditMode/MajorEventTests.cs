using System;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Combat;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class MajorEventTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        /// <summary>Kampf mit festem Ergebnis, damit Tests nicht vom Würfel abhängen.</summary>
        private sealed class FixedCombat : ICombatResolver
        {
            public int Damage;
            public bool Victory = true;
            public int Gold = 5;
            public CombatRequest LastRequest;

            public CombatResult Resolve(CombatRequest request, Random random)
            {
                LastRequest = request;
                return new CombatResult(Victory, Damage, Gold);
            }
        }

        private static OverworldSession Session(PlayerStats stats = null, ICombatResolver combat = null, RuneLoadout runes = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats, null, runes, null, combat);
        }

        [Test]
        public void WinningAFightGivesGoldAndARuneOffer()
        {
            var combat = new FixedCombat { Damage = 4, Gold = 6 };
            OverworldSession s = Session(new PlayerStats(maxHp: 20, gold: 0), combat);
            s.Map.SetContent(East, CellContent.Enemy);
            MajorEventOutcome outcome = null;
            s.MajorEventResolved += o => outcome = o;

            s.TryStep(East);

            Assert.AreEqual(16, s.Stats.Hp);
            Assert.AreEqual(6, s.Stats.Gold);
            Assert.IsTrue(s.Map.GetCell(East).IsResolved);
            Assert.IsNotNull(s.PendingRuneOffer);
            Assert.AreEqual("Sieg", s.PendingRuneOffer.Source);
            Assert.AreEqual("Kampf gewonnen", outcome.Title);
            Assert.AreEqual(1, combat.LastRequest.Tier);
        }

        [Test]
        public void DefeatedEnemiesStayDefeated()
        {
            var combat = new FixedCombat { Damage = 1 };
            OverworldSession s = Session(combat: combat);
            s.Map.SetContent(East, CellContent.Enemy);
            s.TryStep(East);
            s.SkipRuneOffer();
            int hp = s.Stats.Hp;

            s.TryStep(HexCoord.Zero);
            s.TryStep(East);

            Assert.AreEqual(hp, s.Stats.Hp);
        }

        [Test]
        public void LosingAFightEndsTheRun()
        {
            var combat = new FixedCombat { Damage = 3, Victory = false };
            OverworldSession s = Session(new PlayerStats(maxHp: 20), combat);
            s.Map.SetContent(East, CellContent.Enemy);
            bool ended = false;
            s.RunEnded += () => ended = true;

            s.TryStep(East);

            Assert.IsTrue(ended);
            Assert.IsTrue(s.IsGameOver);
            Assert.AreEqual(0, s.Stats.Hp);
            Assert.AreEqual(MoveFailure.GameOver, s.TryStep(HexCoord.Zero).Failure);
            Assert.IsNull(s.PendingRuneOffer);
        }

        [Test]
        public void TreasureGivesGoldAndRuneOffer()
        {
            OverworldSession s = Session(new PlayerStats(gold: 0));
            s.Map.SetContent(East, CellContent.Treasure);

            s.TryStep(East);

            Assert.That(s.Stats.Gold, Is.InRange(6, 10));
            Assert.AreEqual("Schatztruhe", s.PendingRuneOffer.Source);
            Assert.IsTrue(s.Map.GetCell(East).IsResolved);
        }

        [Test]
        public void GoldMinePaysEveryFewTurns()
        {
            OverworldSession s = Session(new PlayerStats(gold: 0));
            s.Map.SetContent(East, CellContent.GoldMine);

            s.TryStep(East);                 // Zug 1: Mine erobert, +3 Gold
            Assert.AreEqual(1, s.ClaimedMines);
            Assert.AreEqual(3, s.Stats.Gold);

            s.TryStep(HexCoord.Zero);        // Zug 2
            s.TryStep(East);                 // Zug 3: Auszahlung

            Assert.AreEqual(3 + OverworldSession.MineIncomeGold, s.Stats.Gold);
            Assert.AreEqual(1, s.ClaimedMines, "Eine Mine zählt nur einmal.");
        }

        [Test]
        public void ShopOpensOnFirstVisitAndSellsRunes()
        {
            OverworldSession s = Session(new PlayerStats(gold: 30));
            s.Map.SetContent(East, CellContent.Shop);

            s.TryStep(East);

            Assert.IsNotNull(s.PendingShop);
            Assert.IsTrue(s.IsBusy);
            RuneDefinition rune = s.PendingShop.Inventory.Runes[0];

            Assert.IsTrue(s.BuyShopRune(0));

            Assert.AreEqual(30 - s.ShopPrices.Rune, s.Stats.Gold);
            Assert.IsTrue(s.Runes.Contains(rune));
            Assert.IsFalse(s.PendingShop.Inventory.Runes.Contains(rune));
        }

        [Test]
        public void ShopKeepsStockAndReopensOnDemand()
        {
            OverworldSession s = Session(new PlayerStats(gold: 30));
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);
            var stock = s.PendingShop.Inventory.Runes.ToList();
            s.LeaveShop();
            Assert.IsFalse(s.IsBusy);

            s.TryStep(HexCoord.Zero);
            s.TryStep(East);
            Assert.IsNull(s.PendingShop, "Beim erneuten Betreten öffnet der Shop nicht von selbst.");

            Assert.IsTrue(s.OpenShop());
            CollectionAssert.AreEqual(stock, s.PendingShop.Inventory.Runes.ToList());
        }

        [Test]
        public void ShopRejectsWhatPlayerCannotAfford()
        {
            OverworldSession s = Session(new PlayerStats(gold: 2));
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsFalse(s.BuyShopRune(0));
            Assert.IsFalse(s.BuyRuneSlot());
            Assert.IsFalse(s.RerollShop());
            Assert.IsFalse(s.BuyHeal(), "Volle HP und zu wenig Gold.");
            Assert.AreEqual(2, s.Stats.Gold);
        }

        [Test]
        public void ShopHealSlotAndReroll()
        {
            OverworldSession s = Session(new PlayerStats(maxHp: 30, gold: 100));
            s.Stats.Damage(20);
            s.Map.SetContent(East, CellContent.Shop);
            s.TryStep(East);

            Assert.IsTrue(s.BuyHeal());
            Assert.AreEqual(20, s.Stats.Hp);

            Assert.IsTrue(s.BuyRuneSlot());
            Assert.AreEqual(4, s.Runes.Slots);
            Assert.IsFalse(s.BuyRuneSlot(), "Nur ein Platz pro Shop.");

            Assert.IsTrue(s.RerollShop());
            Assert.AreEqual(3, s.PendingShop.Inventory.Runes.Count);
        }

        [Test]
        public void CannotOpenShopElsewhere()
        {
            OverworldSession s = Session();
            Assert.IsFalse(s.CanOpenShop);
            Assert.IsFalse(s.OpenShop());
        }

        [Test]
        public void PlaceholderCombatRewardsRunes()
        {
            var resolver = new PlaceholderCombatResolver();
            var none = new RuneLoadout();
            var synergy = new RuneLoadout();
            RuneCatalog catalog = RuneCatalog.CreateDefault();
            synergy.TryAdd(catalog.Get("on_hit"));
            synergy.TryAdd(catalog.Get("enemy_low"));

            Assert.AreEqual(0, resolver.Mitigation(none));
            Assert.AreEqual(2 * resolver.ReductionPerRune + resolver.SynergyReduction, resolver.Mitigation(synergy));
        }

        [Test]
        public void PlaceholderCombatLosesWhenDamageWouldKill()
        {
            var resolver = new PlaceholderCombatResolver();
            var stats = new PlayerStats(maxHp: 2);
            CombatResult result = resolver.Resolve(new CombatRequest(CellContent.Enemy, 6, stats, new RuneLoadout()), new Random(1));

            Assert.IsFalse(result.Victory);
            Assert.AreEqual(0, result.GoldReward);
        }

        [Test]
        public void FullRunOnGeneratedMapReachesFirstFights()
        {
            KnightKit kit = KnightKit.Defaults[0];
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 6, Seed = 21 }, kit: kit);
            var random = new Random(3);
            int fights = 0;
            s.MajorEventResolved += o => { if (o.Title.StartsWith("Kampf")) fights++; };

            // Zufälliger Spieler: wählt immer die letzte Option, nimmt die erste Rune, verlässt Shops.
            for (int step = 0; step < 60 && !s.IsGameOver; step++)
            {
                while (s.IsBusy)
                {
                    if (s.PendingEncounter != null) s.ChooseEncounterOption(s.PendingEncounter.Definition.Options.Count - 1);
                    else if (s.PendingRuneOffer != null) { if (!s.TakeRune(0, 0)) s.SkipRuneOffer(); }
                    else if (s.PendingShop != null) s.LeaveShop();
                    else if (s.CanEnterPortal) s.EnterPortal();
                }

                var options = s.Map.GetNeighbors(s.Player.Position).Where(c => s.CanStepTo(c.Coord)).ToList();
                s.TryStep(options[random.Next(options.Count)].Coord);
            }

            Assert.Greater(fights, 0);
        }
    }
}
