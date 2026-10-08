using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Encounters;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class EncounterTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);

        /// <summary>Testkarte ohne Zufall: alles leer, einzelne Felder werden gezielt belegt.</summary>
        private static OverworldSession Session(PlayerStats stats = null, int radius = 4, int seed = 1)
        {
            var map = new HexMap(HexCoord.Zero, radius, seed);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, radius))
                map.AddCell(new HexCell(c, CellContent.Empty));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map), stats);
        }

        [Test]
        public void MinorEventResolvesImmediately()
        {
            OverworldSession s = Session();
            s.Map.SetEncounter(East, "coins");
            EncounterOutcome seen = null;
            s.EncounterResolved += o => seen = o;
            int goldBefore = s.Stats.Gold;

            s.TryStep(East);

            Assert.IsNotNull(seen);
            Assert.AreEqual("coins", seen.Definition.Id);
            Assert.That(s.Stats.Gold - goldBefore, Is.InRange(2, 4));
            Assert.IsTrue(s.Map.GetCell(East).IsResolved);
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void EventsOnlyTriggerOnce()
        {
            OverworldSession s = Session();
            s.Map.SetEncounter(East, "coins");
            s.TryStep(East);
            int gold = s.Stats.Gold;

            s.TryStep(HexCoord.Zero);
            s.TryStep(East);

            Assert.AreEqual(gold, s.Stats.Gold);
        }

        [Test]
        public void MediumEventWaitsForChoiceAndBlocksMovement()
        {
            OverworldSession s = Session(new PlayerStats(maxHp: 30));
            s.Stats.Damage(15);
            s.Map.SetEncounter(East, "campfire");
            EncounterPrompt started = null;
            s.EncounterStarted += p => started = p;

            s.TryStep(East);

            Assert.IsNotNull(started);
            Assert.IsTrue(s.IsBusy);
            Assert.IsFalse(s.Map.GetCell(East).IsResolved);
            Assert.IsFalse(s.CanStepTo(HexCoord.Zero));
            Assert.AreEqual(MoveFailure.Busy, s.TryStep(HexCoord.Zero).Failure);

            EncounterOutcome outcome = s.ChooseEncounterOption(0);

            Assert.IsNotNull(outcome);
            Assert.AreEqual(25, s.Stats.Hp);
            Assert.IsFalse(s.IsBusy);
            Assert.IsTrue(s.Map.GetCell(East).IsResolved);
            Assert.IsTrue(s.TryStep(HexCoord.Zero).Success);
        }

        [Test]
        public void UnaffordableOptionIsRejected()
        {
            OverworldSession s = Session(new PlayerStats(gold: 2));
            s.Map.SetEncounter(East, "wanderer");
            s.TryStep(East);

            Assert.IsFalse(s.PendingEncounter.Definition.Options[0].IsAvailable(s.Stats));
            Assert.IsNull(s.ChooseEncounterOption(0));
            Assert.IsTrue(s.IsBusy);
            Assert.AreEqual(2, s.Stats.Gold);

            Assert.IsNotNull(s.ChooseEncounterOption(1));
            Assert.IsFalse(s.IsBusy);
        }

        [Test]
        public void PaidOptionCostsGold()
        {
            OverworldSession s = Session(new PlayerStats(gold: 7));
            s.Map.SetEncounter(East, "wanderer");
            s.TryStep(East);

            EncounterOutcome outcome = s.ChooseEncounterOption(0);

            Assert.AreEqual(2, s.Stats.Gold);
            Assert.AreEqual(1, s.Stats.Shards);
            CollectionAssert.Contains(outcome.Lines.ToList(), "-5 Gold");
        }

        [Test]
        public void InvalidChoicesAreIgnored()
        {
            OverworldSession s = Session();
            Assert.IsNull(s.ChooseEncounterOption(0));

            s.Map.SetEncounter(East, "shrine");
            s.TryStep(East);
            Assert.IsNull(s.ChooseEncounterOption(5));
            Assert.IsNull(s.ChooseEncounterOption(-1));
            Assert.IsTrue(s.IsBusy);
        }

        [Test]
        public void EventDamageNeverKills()
        {
            OverworldSession s = Session(new PlayerStats(maxHp: 4));
            s.Map.SetEncounter(East, "shrine");
            s.TryStep(East);

            s.ChooseEncounterOption(0);

            Assert.AreEqual(1, s.Stats.Hp);
            Assert.IsFalse(s.Stats.IsDead);
            Assert.AreEqual(2, s.Stats.Shards);
        }

        [Test]
        public void SignpostScoutsWithoutCreatingRoutes()
        {
            OverworldSession s = Session();
            s.Map.SetEncounter(East, "signpost");
            HexCoord far = new HexCoord(3, 0);

            s.TryStep(East);

            HexCell farCell = s.Map.GetCell(far);
            Assert.IsTrue(farCell.IsScouted);
            Assert.AreEqual(CellVisibility.Unexplored, farCell.Visibility);
            Assert.IsTrue(farCell.IsContentKnown);
            Assert.IsFalse(s.Map.GetCell(new HexCoord(4, 0)).IsScouted);
            Assert.IsNull(s.PlanRoute(far), "Ausgekundschaftete Felder sind keine bekannten Routen.");
        }

        [Test]
        public void TracksRevealNearestShop()
        {
            OverworldSession s = Session();
            s.Map.SetEncounter(East, "tracks");
            HexCoord nearShop = new HexCoord(3, 0);
            HexCoord farShop = new HexCoord(-4, 0);
            s.Map.SetContent(nearShop, CellContent.Shop);
            s.Map.SetContent(farShop, CellContent.Shop);

            s.TryStep(East);

            Assert.IsTrue(s.Map.GetCell(nearShop).IsScouted);
            Assert.IsFalse(s.Map.GetCell(farShop).IsScouted);
        }

        [Test]
        public void SameSeedGivesSameLoot()
        {
            int Loot(int seed)
            {
                OverworldSession s = Session(seed: seed);
                s.Map.SetEncounter(East, "coins");
                s.Map.SetEncounter(new HexCoord(2, 0), "coins");
                s.TryStep(East);
                s.TryStep(new HexCoord(2, 0));
                return s.Stats.Gold;
            }

            Assert.AreEqual(Loot(9), Loot(9));
        }

        [Test]
        public void EveryCatalogEventCanBeResolved()
        {
            foreach (EncounterDefinition def in EncounterCatalog.CreateDefault().All)
            {
                OverworldSession s = Session(new PlayerStats(gold: 100));
                s.Map.SetEncounter(East, def.Id);
                s.TryStep(East);

                if (def.Size == EncounterSize.Medium)
                {
                    Assert.IsTrue(s.IsBusy, def.Id);
                    Assert.IsNotNull(s.ChooseEncounterOption(def.Options.Count - 1), def.Id);
                }

                Assert.IsFalse(s.IsBusy, def.Id);
                Assert.IsTrue(s.Map.GetCell(East).IsResolved, def.Id);
            }
        }

        [Test]
        public void GeneratedMapIsPlayable()
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 6, Seed = 5 });
            int resolved = 0;
            s.EncounterResolved += _ => resolved++;

            // Erkundet Ring 1 komplett: dort liegen nur kleine Events.
            foreach (HexCoord n in HexCoord.Zero.Neighbors().ToList())
            {
                Assert.IsTrue(s.TryStep(n).Success);
                Assert.IsFalse(s.IsBusy);
                Assert.IsTrue(s.TryStep(HexCoord.Zero).Success);
            }

            Assert.AreEqual(6, resolved);
        }
    }

    public class PlayerStatsTests
    {
        [Test]
        public void HealIsCappedAtMaxHp()
        {
            var stats = new PlayerStats(maxHp: 10);
            stats.Damage(3);
            Assert.AreEqual(3, stats.Heal(10));
            Assert.AreEqual(10, stats.Hp);
        }

        [Test]
        public void LethalDamageKills()
        {
            var stats = new PlayerStats(maxHp: 10);
            stats.Damage(50);
            Assert.IsTrue(stats.IsDead);
            Assert.AreEqual(0, stats.Hp);
        }

        [Test]
        public void SpendingFailsWithoutEnoughGold()
        {
            var stats = new PlayerStats(gold: 3);
            Assert.IsFalse(stats.TrySpendGold(4));
            Assert.IsTrue(stats.TrySpendGold(3));
            Assert.AreEqual(0, stats.Gold);
        }

        [Test]
        public void ChangedFiresOnlyOnRealChanges()
        {
            var stats = new PlayerStats(maxHp: 10);
            int changes = 0;
            stats.Changed += () => changes++;

            stats.Heal(5);
            stats.AddGold(0);
            stats.AddGold(2);
            stats.Damage(1);

            Assert.AreEqual(2, changes);
        }
    }
}
