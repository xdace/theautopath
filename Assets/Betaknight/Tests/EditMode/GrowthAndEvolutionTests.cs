using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Evolution;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Growth;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using Betaknight.Core.Turns;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class GrowthAndEvolutionTests
    {
        // Evolutionen sind im Spiel vorübergehend aus; die Regeln bleiben hier geprüft, bis sie neu gedacht sind.
        [SetUp] public void EnableEvolutions() => Betaknight.Core.Evolution.EvolutionCatalog.Enabled = true;
        [TearDown] public void DisableEvolutions() => Betaknight.Core.Evolution.EvolutionCatalog.Enabled = false;

        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly SkillCatalog Catalog = SkillCatalog.CreateDefault();

        /// <summary>Leere Karte: auf ihr passiert nur, was der Test hinstellt (und alle 25 Züge der Boss).</summary>
        private static OverworldSession EmptySession(int hp = 3000)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(hp, 0));
        }

        private static void Walk(OverworldSession s, int steps)
        {
            for (int i = 0; i < steps && !s.IsGameOver; i++)
            {
                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.TryStep(s.Player.Position == HexCoord.Zero ? East : HexCoord.Zero);
            }
        }

        /// <summary>
        /// Legt ein Relais mit <paramref name="runeId"/> auf <paramref name="relayAt"/> und ein neues Exemplar als Komponente auf
        /// <paramref name="at"/>, so dass das Relais die Komponente berührt (früher: Rune und Skill in einer Zeile).
        /// </summary>
        private static SkillInstance AddPair(OverworldSession s, string runeId, Cell relayAt, string skillId, Cell at, bool rotated = false)
        {
            Assert.IsNotNull(s.Board.AddRelay(s.RuneCatalog.Get(runeId), relayAt), runeId);
            SkillInstance skill = s.Skills.Add(skillId);
            Assert.IsTrue(s.PlaceSkill(skill.InstanceId, at, rotated), skillId);
            Assert.IsTrue(s.Board.RelaysTouching(s.Board.ComponentOf(skill)).Any(r => r.Rune.Id == runeId), $"{runeId} berührt {skillId}");
            return skill;
        }

        /// <summary>Linkes Paar auf der Start-Platine (4×3, Kern auf (1, 1)): Relais (0, 0), Komponente darunter ab (0, 1).</summary>
        private static SkillInstance AddLeft(OverworldSession s, string runeId, string skillId) =>
            AddPair(s, runeId, new Cell(0, 0), skillId, new Cell(0, 1));

        /// <summary>Rechtes Paar: Relais (3, 0), eine 2×2-Komponente darunter auf (2, 1).</summary>
        private static SkillInstance AddRight(OverworldSession s, string runeId, string skillId) =>
            AddPair(s, runeId, new Cell(3, 0), skillId, new Cell(2, 1));

        // ------------------------------------------------------------------ Wachsen

        [Test]
        public void TheTallyCountsKillsStunsAndWinsPerComponentAndRelay()
        {
            // A-19: kein «Immer» mehr; ein Takt-Relais jede Sekunde versorgt Schildschlag.
            var board = new LogicBoard(new[]
            {
                new LogicRow(new ClockCondition(Ticks.PerSecond), Catalog.Get(SkillIds.ShieldBash), "Clock 1 s"),
            });
            BattleSetup setup = Duel(Fighter("A", 1000, 10, 20, board: board), Fighter("B", 30, 0, 1000));
            setup.Enemies.Add(Fighter("C", 30, 0, 1000));
            BattleResult r = CombatSimulation.Run(setup);
            Assert.IsTrue(r.IsVictory);

            Dictionary<int, RowTally> tally = GrowthTally.Count(r);
            int deathsByRow0 = r.Events.Count(e => e.Kind == BattleEventKind.Death && e.RowIndex == 0);
            int stunsByRow0 = r.Events.Count(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun && e.RowIndex == 0);
            Assert.Greater(stunsByRow0, 0);
            Assert.AreEqual(deathsByRow0, tally[0].Kills);
            Assert.AreEqual(stunsByRow0, tally[0].Stuns);
            Assert.AreEqual(1, tally[0].PointsFor(GrowthTrigger.Win, true), "Ein Sieg zählt einmal");
            Assert.AreEqual(0, tally[0].PointsFor(GrowthTrigger.Win, false));

            // Pro Relais: was die Aktionen bewirkt haben, die das Relais gestartet hat. Hier startet Relais 0 jeden Schildschlag.
            Dictionary<int, RowTally> relays = GrowthTally.CountRelays(r);
            int killsByRelay0 = r.Events.Count(e => e.Kind == BattleEventKind.Death && e.RowIndex == 0);
            Assert.AreEqual(killsByRelay0, relays[0].Kills);
            Assert.AreEqual(stunsByRow0, relays[0].Stuns);
            Assert.AreEqual(tally[0].Fired, relays[0].Fired);
            Assert.AreEqual(1, relays[0].PointsFor(GrowthTrigger.Win, true));
        }

        [Test]
        public void GrowthComesFromFightsAndStaysAcrossFightsAndActs()
        {
            OverworldSession s = EmptySession();
            AddLeft(s, "on_hit", SkillIds.ShockStab);
            AddRight(s, "hp_low", SkillIds.Repair);
            List<int> SkillGrowth(OverworldSession x) => x.Board.Components.Select(c => c.Skill.Growth).ToList();
            List<int> RelayGrowth(OverworldSession x) => x.Board.Relays.Select(r => r.Growth).ToList();
            List<int> skillsBefore = SkillGrowth(s);
            List<int> relaysBefore = RelayGrowth(s);

            int fights = 0;
            CombatResult? fought = null;
            s.CombatFinished += r => fought = r;
            HexCoord[] fresh = { new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1) };
            for (int i = 0; i < 3; i++)
            {
                var expectedSkill = new List<int>();
                var expectedRelay = new List<int>();
                s.Map.SetContent(fresh[i], CellContent.Enemy);
                fought = null;
                s.TryStep(fresh[i]);
                fights++;
                Assert.IsTrue(fought.HasValue, $"Kampf {fights} hat stattgefunden");
                Assert.IsTrue(fought.Value.Victory);

                // Wachstum pro Komponente (ihr Skill-Exemplar) und pro Relais (was seine Auslösungen bewirkt haben).
                Dictionary<int, RowTally> tally = GrowthTally.Count(fought.Value.Battle);
                Dictionary<int, RowTally> relayTally = GrowthTally.CountRelays(fought.Value.Battle);
                for (int c = 0; c < s.Board.Components.Count; c++)
                {
                    RowTally t = tally.TryGetValue(c, out RowTally x) ? x : new RowTally();
                    expectedSkill.Add(skillsBefore[c] + t.PointsFor(s.SkillGrowthRule(s.Board.Components[c].Skill).Trigger, true));
                }
                for (int r = 0; r < s.Board.Relays.Count; r++)
                {
                    RowTally t = relayTally.TryGetValue(r, out RowTally x) ? x : new RowTally();
                    expectedRelay.Add(relaysBefore[r] + t.PointsFor(s.RelayGrowthRule(s.Board.Relays[r]).Trigger, true));
                }
                Assert.AreEqual(expectedSkill, SkillGrowth(s), $"Komponenten nach Kampf {fights}");
                Assert.AreEqual(expectedRelay, RelayGrowth(s), $"Relais nach Kampf {fights}");
                skillsBefore = SkillGrowth(s);
                relaysBefore = RelayGrowth(s);

                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.RejectPendingItem();
                s.RejectPendingRune();
                s.TryStep(HexCoord.Zero);
            }
            Assert.Greater(skillsBefore.Sum() + relaysBefore.Sum(), 0, "Drei gewonnene Kämpfe lassen etwas wachsen");

            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Radius = 4 }, s);
            Assert.AreEqual(skillsBefore, SkillGrowth(next), "Wachstum der Komponenten wandert durch die Akte");
            Assert.AreEqual(relaysBefore, RelayGrowth(next), "Wachstum der Relais wandert durch die Akte");
        }

        /// <summary>Platine mit «Gegner unter x %» (◆◆, 4 Zellen) und Bohrstoß (2×2) daneben, mit Wachstum.</summary>
        private static CircuitSpec DrillSpec(int growth)
        {
            var spec = new CircuitSpec();
            spec.Relays.Add(new RelaySpec("enemy_low", new Cell(0, 0)));
            spec.Components.Add(new ComponentSpec(SkillIds.Drill, new Cell(1, 0), growth: growth));
            return spec;
        }

        [Test]
        public void KillGrowthAddsDamageInTheFight()
        {
            var skills = new SkillCollection();
            SkillInstance drill = skills.Add(SkillIds.Drill);
            skills.Grow(drill, 7);
            Assert.AreEqual("Drill Strike +7", drill.NameFrom(Catalog));

            LogicBoard board = BoardFactory.CreateDefault().Create(DrillSpec(drill.Growth), null);
            DamageEffect damage = board.Rows[0].Skill.Effects.OfType<DamageEffect>().Single();
            Assert.IsTrue(board.Rows[0].IsPowered);
            Assert.AreEqual(7, damage.FlatBonus);

            skills.Grow(drill, 100);
            board = BoardFactory.CreateDefault().Create(DrillSpec(drill.Growth), null);
            Assert.AreEqual(30, board.Rows[0].Skill.Effects.OfType<DamageEffect>().Single().FlatBonus, "Obergrenze +30");
        }

        [Test]
        public void ThresholdGrowthStopsAtFiftyPercent()
        {
            BoardFactory factory = BoardFactory.CreateDefault();
            var at = new Cell(0, 0);
            Assert.AreEqual("HP Below 35 %", factory.CreateRelay(new RelaySpec("hp_low", at, growth: 15), null).Label);
            Assert.AreEqual("HP Below 50 %", factory.CreateRelay(new RelaySpec("hp_low", at, growth: 40), null).Label);
            // Das Stufen-Abzeichen «▲2» kommt aus dem Runen-Stufen-Hotfix.
            Assert.AreEqual("HP Below 50 % ▲2", factory.CreateRelay(new RelaySpec("hp_low", at, level: 2, growth: 40), null).Label,
                "Ein höherer Grundwert bleibt");
            Assert.AreEqual("HP Below 60 %", factory.CreateRelay(new RelaySpec("hp_low", at, growth: 40,
                modules: new[] { new ModuleSpec(ModuleIds.Threshold) }), null).Label, "Das Modul «Schwelle» kommt obendrauf");
        }

        [Test]
        public void ModuleSlotsOpenAtGrowthTenAndTwentyFive()
        {
            OverworldSession s = EmptySession();
            SkillInstance skill = AddLeft(s, "clock", SkillIds.ShockStab);
            RelayChip row = s.Board.Relays[0];

            s.Skills.Grow(skill, 9);
            s.Board.Grow(row, 9);
            Assert.AreEqual(1, skill.ModuleSlots);
            Assert.AreEqual(1, row.ModuleSlots);

            ModuleInstance a = s.GainModule(ModuleIds.Chain);
            ModuleInstance b = s.GainModule(ModuleIds.Quickcast);
            Assert.IsTrue(s.PlaceModuleOnSkill(a.InstanceId, skill.InstanceId));
            Assert.IsFalse(s.PlaceModuleOnSkill(b.InstanceId, skill.InstanceId));

            s.Skills.Grow(skill, 1);
            s.Board.Grow(row, 1);
            Assert.AreEqual(2, skill.ModuleSlots);
            Assert.AreEqual(2, row.ModuleSlots);
            Assert.IsTrue(s.PlaceModuleOnSkill(b.InstanceId, skill.InstanceId));

            s.Skills.Grow(skill, 15);
            Assert.AreEqual(3, skill.ModuleSlots);
            Assert.AreEqual(2, skill.Level);
        }

        [Test]
        public void BlockGrowthTravelsWithTheRuneIntoTheInventory()
        {
            OverworldSession s = EmptySession();
            AddRight(s, "hp_low", SkillIds.Repair);
            s.Board.Grow(s.Board.Relays[0], 12);

            Assert.IsTrue(s.UnequipRune(0));
            Assert.AreEqual(12, s.RuneInventory.Runes[0].Growth);
            Assert.IsTrue(s.EquipRuneFromInventory(0));
            Assert.AreEqual(12, s.Board.Relays[0].Growth);
            Assert.IsTrue(s.Board.RelaysTouching(s.Board.Components[0]).Any(), "Zurück neben die unversorgte Komponente");
        }

        // ------------------------------------------------------------------ Evolution

        private static (OverworldSession s, SkillInstance ignite) ReadyForInferno(bool withModule)
        {
            OverworldSession s = EmptySession();
            SkillInstance ignite = AddLeft(s, "on_hit", SkillIds.Ignite);
            s.Skills.Grow(ignite, 30);
            if (withModule)
            {
                ModuleInstance area = s.GainModule(ModuleIds.Area);
                Assert.IsTrue(s.PlaceModuleOnSkill(area.InstanceId, ignite.InstanceId));
            }
            return (s, ignite);
        }

        [Test]
        public void EvolutionWaitsForTheBoss()
        {
            (OverworldSession s, SkillInstance ignite) = ReadyForInferno(true);
            EvolutionRecipe recipe = s.EvolutionCatalog.Get("evo_inferno");
            Assert.IsEmpty(s.EvolutionMissing(recipe, ignite));
            StringAssert.Contains("ready", s.EvolutionProgress(recipe));

            Walk(s, OverworldSession.BossInterval - 1);
            Assert.AreEqual(SkillIds.Ignite, ignite.SkillId, "Vor dem Boss passiert nichts");

            int id = ignite.InstanceId;
            int growth = ignite.Growth;
            ModuleInstance area = ignite.Modules.Single();
            ComponentSlot component = s.Board.ComponentOf(ignite);
            Walk(s, 1);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(SkillIds.Inferno, ignite.SkillId, "Nach dem überlebten Boss");
            Assert.AreEqual(id, ignite.InstanceId);
            Assert.GreaterOrEqual(ignite.Growth, growth, "Wachstum bleibt (und wächst weiter)");
            Assert.AreSame(area, ignite.Modules.Single(), "Module bleiben");
            Assert.AreSame(component, ignite.Holder, "Die Komponente bleibt liegen");
            Assert.AreEqual(new Cell(0, 1), component.Origin);
            Assert.IsTrue(s.RecipeBook.HasEvolution("evo_inferno"));
            Assert.AreEqual("Firestorm", s.EvolutionName(recipe));
            StringAssert.StartsWith("Firestorm +", ignite.NameFrom(s.SkillCatalog));
        }

        [Test]
        public void EachComponentShowsItsOwnEvolutionProgress()
        {
            OverworldSession s = EmptySession();
            SkillInstance ignite = AddLeft(s, "on_hit", SkillIds.Ignite);
            Assert.AreEqual(new[] { "Evolution ???: missing Level 3 (Growth 0/30), Module Area" }, s.EvolutionProgressFor(ignite));
            Assert.IsFalse(s.IsEvolutionReady(ignite));

            (OverworldSession ready, SkillInstance readyIgnite) = ReadyForInferno(true);
            Assert.IsTrue(ready.IsEvolutionReady(readyIgnite));
            StringAssert.Contains("ready", ready.EvolutionProgressFor(ready.Board.Components[0].Skill).Single());
        }

        [Test]
        public void EvolutionNeedsEveryCondition()
        {
            (OverworldSession s, SkillInstance ignite) = ReadyForInferno(false);
            EvolutionRecipe recipe = s.EvolutionCatalog.Get("evo_inferno");
            CollectionAssert.AreEqual(new[] { "Module Area" }, s.EvolutionMissing(recipe, ignite));
            Assert.AreEqual("Evolution ???: missing Module Area", s.EvolutionProgress(recipe));

            Walk(s, OverworldSession.BossInterval);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(SkillIds.Ignite, ignite.SkillId, "Ohne Modul keine Evolution");
            Assert.IsFalse(s.RecipeBook.HasEvolution("evo_inferno"));

            // Ohne Höchststufe auch nicht.
            OverworldSession low = EmptySession();
            SkillInstance young = AddLeft(low, "on_hit", SkillIds.Ignite);
            low.Skills.Grow(young, 12);
            ModuleInstance area = low.GainModule(ModuleIds.Area);
            Assert.IsTrue(low.PlaceModuleOnSkill(area.InstanceId, young.InstanceId));
            CollectionAssert.AreEqual(new[] { "Level 3 (Growth 12/30)" }, low.EvolutionMissing(recipe, young));
        }

        [Test]
        public void TagAndBlockRecipesCheckTheirConditions()
        {
            OverworldSession s = EmptySession();
            SkillInstance stab = AddLeft(s, "clock", SkillIds.ShockStab);
            s.Skills.Grow(stab, 30);
            EvolutionRecipe lance = s.EvolutionCatalog.Get("evo_lance");
            CollectionAssert.AreEqual(new[] { "Tag Static 0/4" }, s.EvolutionMissing(lance, stab));

            // Bohrstoß neben «HP Full»: die verlangte Rune berührt ihn nicht.
            SkillInstance drill = AddRight(s, "hp_full", SkillIds.Drill);
            s.Skills.Grow(drill, 30);
            EvolutionRecipe acid = s.EvolutionCatalog.Get("evo_acid");
            Assert.AreEqual(1, s.EvolutionMissing(acid, drill).Count);
            StringAssert.StartsWith("powered by the relay \"Enemy Below", s.EvolutionMissing(acid, drill)[0]);

            // A-19: «in derselben Zeile» heisst jetzt: ein Relais mit «Gegner unter x %» berührt die Komponente.
            // Ein solches Relais irgendwo auf der Platine reicht nicht.
            Assert.IsTrue(s.ExpandBoard(1), "4×4");
            Assert.IsNotNull(s.Board.AddRelay(s.RuneCatalog.Get("enemy_low"), new Cell(1, 3)));
            Assert.AreEqual(1, s.EvolutionMissing(acid, drill).Count, "Relais liegt woanders");

            // Bohrstoß an das Relais schieben: bereit.
            Assert.IsTrue(s.PlaceSkill(drill.InstanceId, new Cell(2, 2)));
            Assert.IsTrue(s.Board.RelaysTouching(s.Board.ComponentOf(drill)).Any(r => r.Rune.Id == "enemy_low"));
            Assert.IsEmpty(s.EvolutionMissing(acid, drill));
        }

        [Test]
        public void ABlockEvolvesAfterTheBossAndKeepsItsModules()
        {
            OverworldSession s = EmptySession();
            AddLeft(s, "on_hit", SkillIds.ShieldBash);
            AddRight(s, "hp_low", SkillIds.Repair);
            RelayChip row = s.Board.Relays[1];
            Assert.AreEqual("hp_low", row.Rune.Id);
            ModuleInstance extend = s.GainModule(ModuleIds.Extend);
            Assert.IsTrue(s.PlaceModuleOnRelay(extend.InstanceId, 1));
            EvolutionRecipe reflex = s.EvolutionCatalog.Get("evo_reflex");
            Assert.AreEqual(1, s.EvolutionMissing(reflex, row).Count, "Noch nicht auf Höchststufe");

            Assert.IsTrue(s.Board.Upgrade(1));
            Assert.IsEmpty(s.EvolutionMissing(reflex, row));
            Assert.IsTrue(s.IsEvolutionReady(row));
            Walk(s, OverworldSession.BossInterval);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(EvolvedRuneIds.PhantomReflex, row.Rune.Id);
            Assert.AreEqual(1, row.Level);
            Assert.AreSame(extend, row.Modules.Single());
            Assert.AreEqual(new Cell(3, 0), row.Position, "Das Relais bleibt liegen");
            StringAssert.StartsWith("HP Below 25 % or Dodged", row.Name);
        }

        [Test]
        public void EvolutionsAreNeverOffered()
        {
            OverworldSession s = EmptySession();
            for (int i = 0; i < 40; i++)
            {
                s.OfferRunes(RewardSources.Elite);
                RuneOffer offer = s.PendingRuneOffer;
                if (offer == null) continue;
                Assert.IsFalse(offer.SkillIds.Any(id => s.SkillCatalog.Get(id).IsEvolution));
                Assert.IsFalse(offer.Options.Any(r => r.Id == EvolvedRuneIds.PhantomReflex));
                s.SkipRuneOffer();
            }
        }

        [Test]
        public void OffersShowEvolutionProgress()
        {
            OverworldSession s = EmptySession();
            SkillInstance ignite = AddLeft(s, "on_hit", SkillIds.Ignite);
            s.Skills.Grow(ignite, 30);

            Assert.AreEqual(new[] { "Evolution ???: missing Module Area" }, s.EvolutionHintsForSkill(SkillIds.Ignite));
            StringAssert.StartsWith("Evolution ???: missing Module Area (part of the recipe for Ignite)", s.EvolutionHintsForModule(ModuleIds.Area).Single());
            Assert.IsEmpty(s.EvolutionHintsForModule(ModuleIds.Multicast), "Echo fehlt: kein Hinweis");
        }

        // ------------------------------------------------------------------ Rezeptbuch

        [Test]
        public void TheRecipeBookIsSavedAcrossRuns()
        {
            var store = new MemoryRecipeBookStore();
            (OverworldSession s, SkillInstance _) = ReadyForInferno(true);
            s.UseRecipeStore(store);
            s.RecipeBook.DiscoverDuo("glutrhythmus");
            Walk(s, OverworldSession.BossInterval);
            StringAssert.Contains("evo:evo_inferno", store.Data);
            StringAssert.Contains("duo:glutrhythmus", store.Data);

            OverworldSession nextRun = EmptySession();
            Assert.IsFalse(nextRun.RecipeBook.HasEvolution("evo_inferno"));
            nextRun.UseRecipeStore(store);
            Assert.IsTrue(nextRun.RecipeBook.HasEvolution("evo_inferno"));
            Assert.IsTrue(nextRun.IsDuoDiscovered("glutrhythmus"));
            Assert.AreEqual("Firestorm", nextRun.EvolutionName(nextRun.EvolutionCatalog.Get("evo_inferno")));
            Assert.AreEqual(0, nextRun.Skills.Count, "Nur Wissen, keine Werte");
        }

        [Test]
        public void TheSavedTextIsOnlyKnowledge()
        {
            RecipeBook book = RecipeBook.Parse("duo:a\nevo:b\nmüll\n\nevo:\ngold:999\n");
            CollectionAssert.AreEquivalent(new[] { "a" }, book.Duos);
            CollectionAssert.AreEquivalent(new[] { "b" }, book.Evolutions);
            Assert.AreEqual("duo:a\nevo:b\n", book.Serialize());
            Assert.AreEqual(book.Serialize(), RecipeBook.Parse(book.Serialize()).Serialize());
        }
    }
}
