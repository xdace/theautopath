using System;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Circuit;
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

        private static OverworldSession Session(PlayerStats stats = null, CircuitBoard runes = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 3);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats, null, runes, Catalog);
        }

        /// <summary>Platine mit genau einer freien Zelle (2×1, Kern rechts): ein Relais darauf, und sie ist voll.</summary>
        private static CircuitBoard OneCellBoard() =>
            new CircuitBoard(new CircuitConfig { Sizes = new[] { new Shape(2, 1) }, Core = new Cell(1, 0) });

        [Test]
        public void OfferHasDistinctRunesNotYetOwned()
        {
            var loadout = new CircuitBoard();
            loadout.AddRelay(Catalog.Get("on_hit"));

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
            var loadout = new CircuitBoard();
            loadout.AddRelay(Catalog.Get("when_hit"));

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

            Assert.AreSame(chosen, s.Board.Runes.Single());
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void FullBoardSendsNewRunesToTheInventory()
        {
            // Volle Platine: keine freie Zelle für ein weiteres Relais.
            CircuitBoard runes = OneCellBoard();
            Assert.IsNotNull(runes.AddRelay(Catalog.Get("on_hit")));
            Assert.IsTrue(runes.IsFull);
            OverworldSession s = Session(runes: runes);
            RuneOffer offer = s.OfferRunes("Test");

            Assert.IsTrue(s.TakeRune(0));
            Assert.AreEqual("on_hit", s.Board.Runes.Single().Id, "Die Platine bleibt unverändert.");
            Assert.AreSame(offer.Options[0], s.RuneInventory.Runes.Single().Rune);
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void ReplacingARelaysRuneMovesTheOldRuneToTheInventory()
        {
            CircuitBoard runes = OneCellBoard();
            RelayChip relay = runes.AddRelay(Catalog.Get("on_hit"));
            OverworldSession s = Session(runes: runes);
            RuneOffer offer = s.OfferRunes("Test");

            // Das Relais bleibt liegen, nur seine Rune wechselt.
            Assert.IsTrue(s.TakeRune(0, replaceSlot: 0));
            Assert.AreSame(offer.Options[0], s.Board.Runes[0]);
            Assert.AreSame(relay, s.Board.Relays.Single());
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
            Assert.AreEqual("when_hit", s.Board.Runes.Single().Id);
        }

        [Test]
        public void EveryKitStartRuneExistsAndMatchesItsTag()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
                Assert.AreEqual(kit.Tag, Catalog.Get(kit.StartRuneId).Tag, kit.Id);
        }

        [Test]
        public void BoardRejectsDuplicateRunes()
        {
            var loadout = new CircuitBoard();
            Assert.IsNotNull(loadout.AddRelay(Catalog.Get("hp_low")));
            Assert.IsNull(loadout.AddRelay(Catalog.Get("hp_low")));
            Assert.AreEqual(1, loadout.CountByTag()[RuneTag.Ember]);
        }
    }
}
