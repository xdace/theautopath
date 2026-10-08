using System;
using System.Linq;
using Betaknight.Core;
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
    public class RuneTests
    {
        private static readonly RuneCatalog Catalog = RuneCatalog.CreateDefault();

        private static OverworldSession Session(PlayerStats stats = null, RuneLoadout runes = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 3);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats, null, runes, Catalog);
        }

        [Test]
        public void OfferHasDistinctRunesNotYetOwned()
        {
            var loadout = new RuneLoadout();
            loadout.TryAdd(Catalog.Get("on_hit"));

            for (int seed = 0; seed < 100; seed++)
            {
                RuneOffer offer = RuneOffer.Create("Test", Catalog, loadout, new Random(seed));
                Assert.AreEqual(3, offer.Options.Count);
                Assert.AreEqual(3, offer.Options.Select(r => r.Id).Distinct().Count());
                Assert.IsFalse(offer.Options.Any(r => r.Id == "on_hit"));
            }
        }

        [Test]
        public void OfferAlwaysContainsAMatchingTag()
        {
            var loadout = new RuneLoadout();
            loadout.TryAdd(Catalog.Get("when_hit"));

            for (int seed = 0; seed < 100; seed++)
            {
                RuneOffer offer = RuneOffer.Create("Test", Catalog, loadout, new Random(seed));
                Assert.IsTrue(offer.Options.Any(r => r.Tag == RuneTag.Shield), $"Seed {seed}");
            }
        }

        [Test]
        public void ThreeShardsOpenARuneOffer()
        {
            OverworldSession s = Session();
            s.Map.SetEncounter(new HexCoord(1, 0), "shard");
            s.Map.SetEncounter(new HexCoord(2, 0), "shard");
            s.Map.SetEncounter(new HexCoord(3, 0), "shard");
            RuneOffer started = null;
            s.RuneOfferStarted += o => started = o;

            s.TryStep(new HexCoord(1, 0));
            s.TryStep(new HexCoord(2, 0));
            Assert.IsNull(s.PendingRuneOffer);
            Assert.AreEqual(2, s.Stats.Shards);

            s.TryStep(new HexCoord(3, 0));

            Assert.IsNotNull(started);
            Assert.AreSame(started, s.PendingRuneOffer);
            Assert.AreEqual(0, s.Stats.Shards);
            Assert.IsTrue(s.IsBusy);
            Assert.AreEqual(MoveFailure.Busy, s.TryStep(new HexCoord(2, 0)).Failure);
        }

        [Test]
        public void TakingARuneEquipsIt()
        {
            OverworldSession s = Session();
            RuneOffer offer = s.OfferRunes("Test");
            RuneDefinition chosen = offer.Options[1];

            Assert.IsTrue(s.TakeRune(1));

            Assert.AreSame(chosen, s.Runes.Runes.Single());
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void FullBoardSendsNewRunesToTheInventory()
        {
            var runes = new RuneLoadout(slots: 1);
            runes.TryAdd(Catalog.Get("on_hit"));
            OverworldSession s = Session(runes: runes);
            RuneOffer offer = s.OfferRunes("Test");

            Assert.IsTrue(s.TakeRune(0));
            Assert.AreEqual("on_hit", s.Runes.Runes.Single().Id, "Die Tafel bleibt unverändert.");
            Assert.AreSame(offer.Options[0], s.RuneInventory.Runes.Single().Rune);
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void ReplacingARowMovesTheOldRuneToTheInventory()
        {
            var runes = new RuneLoadout(slots: 1);
            runes.TryAdd(Catalog.Get("on_hit"));
            OverworldSession s = Session(runes: runes);
            RuneOffer offer = s.OfferRunes("Test");

            Assert.IsTrue(s.TakeRune(0, replaceSlot: 0));
            Assert.AreSame(offer.Options[0], s.Runes.Runes[0]);
            Assert.AreEqual("on_hit", s.RuneInventory.Runes.Single().Rune.Id);
        }

        [Test]
        public void SkippingGivesGold()
        {
            OverworldSession s = Session(new PlayerStats(gold: 0));
            s.OfferRunes("Test");

            Assert.IsTrue(s.SkipRuneOffer());

            Assert.AreEqual(OverworldSession.SkipRuneGold, s.Stats.Gold);
            Assert.IsFalse(s.IsBusy);
            Assert.IsFalse(s.SkipRuneOffer());
        }

        [Test]
        public void OnlyOneDecisionAtATime()
        {
            OverworldSession s = Session();
            Assert.IsNotNull(s.OfferRunes("A"));
            Assert.IsNull(s.OfferRunes("B"));
        }

        [Test]
        public void LeftoverShardsOpenTheNextOffer()
        {
            OverworldSession s = Session(new PlayerStats(shards: 0));
            s.Stats.AddShards(5);
            s.Map.SetEncounter(new HexCoord(1, 0), "shard");

            s.TryStep(new HexCoord(1, 0));
            Assert.IsNotNull(s.PendingRuneOffer);
            Assert.AreEqual(3, s.Stats.Shards);

            s.TakeRune(0);

            Assert.IsNotNull(s.PendingRuneOffer, "Zweite Wahl aus den restlichen Splittern.");
            Assert.AreEqual(0, s.Stats.Shards);
        }

        [Test]
        public void KitSetsStatsAndStartRune()
        {
            KnightKit kit = KnightKit.Defaults.First(k => k.Id == "shield");

            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 5, Seed = 4 }, kit: kit);

            Assert.AreSame(kit, s.Kit);
            Assert.AreEqual(kit.MaxHp, s.Stats.MaxHp);
            Assert.AreEqual(kit.Gold, s.Stats.Gold);
            Assert.AreEqual("when_hit", s.Runes.Runes.Single().Id);
        }

        [Test]
        public void EveryKitStartRuneExistsAndMatchesItsTag()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
                Assert.AreEqual(kit.Tag, Catalog.Get(kit.StartRuneId).Tag, kit.Id);
        }

        [Test]
        public void LoadoutRejectsDuplicates()
        {
            var loadout = new RuneLoadout();
            Assert.IsTrue(loadout.TryAdd(Catalog.Get("hp_low")));
            Assert.IsFalse(loadout.TryAdd(Catalog.Get("hp_low")));
            Assert.AreEqual(1, loadout.CountByTag()[RuneTag.Ember]);
        }
    }
}
