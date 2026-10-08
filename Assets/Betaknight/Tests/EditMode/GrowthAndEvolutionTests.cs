using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
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
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
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

        private static SkillInstance AddRow(OverworldSession s, string runeId, string skillId)
        {
            SkillInstance skill = s.Skills.Add(skillId);
            Assert.IsTrue(s.Runes.TryAdd(s.RuneCatalog.Get(runeId), skill));
            return skill;
        }

        // ------------------------------------------------------------------ Wachsen

        [Test]
        public void TheTallyCountsKillsStunsAndWinsPerRow()
        {
            var board = new LogicBoard(new[]
            {
                new LogicRow(Conditions.Create("always", 0), Catalog.Get(SkillIds.ShieldBash), "Immer"),
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
        }

        [Test]
        public void GrowthComesFromFightsAndStaysAcrossFightsAndActs()
        {
            OverworldSession s = EmptySession();
            AddRow(s, "always", SkillIds.Drill);
            AddRow(s, "hp_low", SkillIds.Repair);
            var before = s.Runes.Rows.Select(r => (r.Skill?.Growth ?? 0, r.Growth)).ToList();

            int fights = 0;
            CombatResult? fought = null;
            s.CombatFinished += r => fought = r;
            HexCoord[] fresh = { new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1) };
            for (int i = 0; i < 3; i++)
            {
                var expectedSkill = new List<int>();
                var expectedRow = new List<int>();
                s.Map.SetContent(fresh[i], CellContent.Enemy);
                fought = null;
                s.TryStep(fresh[i]);
                fights++;
                Assert.IsTrue(fought.HasValue, $"Kampf {fights} hat stattgefunden");
                Assert.IsTrue(fought.Value.Victory);
                Dictionary<int, RowTally> tally = GrowthTally.Count(fought.Value.Battle);
                for (int row = 0; row < s.Runes.Rows.Count; row++)
                {
                    RowTally t = tally.TryGetValue(row, out RowTally x) ? x : new RowTally();
                    RuneSlot slot = s.Runes.Rows[row];
                    expectedSkill.Add(before[row].Item1 + t.PointsFor(s.SkillGrowthRule(slot.Skill).Trigger, true));
                    expectedRow.Add(before[row].Item2 + t.PointsFor(s.RowGrowthRule(slot).Trigger, true));
                }
                Assert.AreEqual(expectedSkill, s.Runes.Rows.Select(r => r.Skill.Growth).ToList(), $"Skills nach Kampf {fights}");
                Assert.AreEqual(expectedRow, s.Runes.Rows.Select(r => r.Growth).ToList(), $"Bausteine nach Kampf {fights}");
                before = s.Runes.Rows.Select(r => (r.Skill.Growth, r.Growth)).ToList();

                if (s.PendingRuneOffer != null) s.SkipRuneOffer();
                s.RejectPendingItem();
                s.RejectPendingRune();
                s.TryStep(HexCoord.Zero);
            }
            Assert.Greater(before.Sum(b => b.Item1 + b.Item2), 0, "Drei gewonnene Kämpfe lassen etwas wachsen");

            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Radius = 4 }, s);
            Assert.AreEqual(before, next.Runes.Rows.Select(r => (r.Skill.Growth, r.Growth)).ToList(), "Wachstum wandert durch die Akte");
        }

        [Test]
        public void KillGrowthAddsDamageInTheFight()
        {
            var skills = new SkillCollection();
            SkillInstance drill = skills.Add(SkillIds.Drill);
            skills.Grow(drill, 7);
            Assert.AreEqual("Bohrstoß +7", drill.NameFrom(Catalog));

            LogicBoard board = BoardFactory.CreateDefault().Create(new[] { new BoardRowSpec("always", SkillIds.Drill, skillGrowth: drill.Growth) }, null);
            DamageEffect damage = board.Rows[0].Skill.Effects.OfType<DamageEffect>().Single();
            Assert.AreEqual(7, damage.FlatBonus);

            skills.Grow(drill, 100);
            board = BoardFactory.CreateDefault().Create(new[] { new BoardRowSpec("always", SkillIds.Drill, skillGrowth: drill.Growth) }, null);
            Assert.AreEqual(30, board.Rows[0].Skill.Effects.OfType<DamageEffect>().Single().FlatBonus, "Obergrenze +30");
        }

        [Test]
        public void ThresholdGrowthStopsAtFiftyPercent()
        {
            BoardFactory factory = BoardFactory.CreateDefault();
            Assert.AreEqual("HP unter 45 %", factory.CreateRow(new BoardRowSpec("hp_low", SkillIds.Repair, blockGrowth: 15), null).Label);
            Assert.AreEqual("HP unter 50 %", factory.CreateRow(new BoardRowSpec("hp_low", SkillIds.Repair, blockGrowth: 40), null).Label);
            Assert.AreEqual("HP unter 50 %", factory.CreateRow(new BoardRowSpec("hp_low", SkillIds.Repair, level: 2, blockGrowth: 40), null).Label,
                "Ein höherer Grundwert bleibt");
            Assert.AreEqual("HP unter 60 %", factory.CreateRow(new BoardRowSpec("hp_low", SkillIds.Repair, blockGrowth: 40,
                blockModules: new[] { new ModuleSpec(ModuleIds.Threshold) }), null).Label, "Das Modul «Schwelle» kommt obendrauf");
        }

        [Test]
        public void ModuleSlotsOpenAtGrowthTenAndTwentyFive()
        {
            OverworldSession s = EmptySession();
            SkillInstance skill = AddRow(s, "always", SkillIds.ShockStab);
            RuneSlot row = s.Runes.Rows[0];

            s.Skills.Grow(skill, 9);
            s.Runes.Grow(row, 9);
            Assert.AreEqual(1, skill.ModuleSlots);
            Assert.AreEqual(1, row.ModuleSlots);

            ModuleInstance a = s.GainModule(ModuleIds.Chain);
            ModuleInstance b = s.GainModule(ModuleIds.Quickcast);
            Assert.IsTrue(s.PlaceModuleOnSkill(a.InstanceId, skill.InstanceId));
            Assert.IsFalse(s.PlaceModuleOnSkill(b.InstanceId, skill.InstanceId));

            s.Skills.Grow(skill, 1);
            s.Runes.Grow(row, 1);
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
            AddRow(s, "hp_low", SkillIds.Repair);
            s.Runes.Grow(s.Runes.Rows[0], 12);

            Assert.IsTrue(s.UnequipRune(0));
            Assert.AreEqual(12, s.RuneInventory.Runes[0].Growth);
            Assert.IsTrue(s.EquipRuneFromInventory(0));
            Assert.AreEqual(12, s.Runes.Rows[0].Growth);
        }

        // ------------------------------------------------------------------ Evolution

        private static (OverworldSession s, SkillInstance ignite) ReadyForInferno(bool withModule)
        {
            OverworldSession s = EmptySession();
            SkillInstance ignite = AddRow(s, "always", SkillIds.Ignite);
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
            StringAssert.Contains("bereit", s.EvolutionProgress(recipe));

            Walk(s, OverworldSession.BossInterval - 1);
            Assert.AreEqual(SkillIds.Ignite, ignite.SkillId, "Vor dem Boss passiert nichts");

            int id = ignite.InstanceId;
            int growth = ignite.Growth;
            ModuleInstance area = ignite.Modules.Single();
            RuneSlot row = s.Runes.Rows[0];
            Walk(s, 1);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(SkillIds.Inferno, ignite.SkillId, "Nach dem überlebten Boss");
            Assert.AreEqual(id, ignite.InstanceId);
            Assert.GreaterOrEqual(ignite.Growth, growth, "Wachstum bleibt (und wächst weiter)");
            Assert.AreSame(area, ignite.Modules.Single(), "Module bleiben");
            Assert.AreSame(row, ignite.Holder);
            Assert.IsTrue(s.RecipeBook.HasEvolution("evo_inferno"));
            Assert.AreEqual("Feuersturm", s.EvolutionName(recipe));
            StringAssert.StartsWith("Feuersturm +", ignite.NameFrom(s.SkillCatalog));
        }

        [Test]
        public void EvolutionNeedsEveryCondition()
        {
            (OverworldSession s, SkillInstance ignite) = ReadyForInferno(false);
            EvolutionRecipe recipe = s.EvolutionCatalog.Get("evo_inferno");
            CollectionAssert.AreEqual(new[] { "Modul Fläche" }, s.EvolutionMissing(recipe, ignite));
            Assert.AreEqual("Evolution ???: fehlt Modul Fläche", s.EvolutionProgress(recipe));

            Walk(s, OverworldSession.BossInterval);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(SkillIds.Ignite, ignite.SkillId, "Ohne Modul keine Evolution");
            Assert.IsFalse(s.RecipeBook.HasEvolution("evo_inferno"));

            // Ohne Höchststufe auch nicht.
            OverworldSession low = EmptySession();
            SkillInstance young = AddRow(low, "always", SkillIds.Ignite);
            low.Skills.Grow(young, 12);
            ModuleInstance area = low.GainModule(ModuleIds.Area);
            Assert.IsTrue(low.PlaceModuleOnSkill(area.InstanceId, young.InstanceId));
            CollectionAssert.AreEqual(new[] { "Stufe 3 (Wachstum 12/30)" }, low.EvolutionMissing(recipe, young));
        }

        [Test]
        public void TagAndBlockRecipesCheckTheirConditions()
        {
            OverworldSession s = EmptySession();
            SkillInstance stab = AddRow(s, "always", SkillIds.ShockStab);
            s.Skills.Grow(stab, 30);
            EvolutionRecipe lance = s.EvolutionCatalog.Get("evo_lance");
            CollectionAssert.AreEqual(new[] { "Tag Ladung 0/4" }, s.EvolutionMissing(lance, stab));

            SkillInstance drill = AddRow(s, "hp_full", SkillIds.Drill);
            s.Skills.Grow(drill, 30);
            EvolutionRecipe acid = s.EvolutionCatalog.Get("evo_acid");
            Assert.AreEqual(1, s.EvolutionMissing(acid, drill).Count);
            StringAssert.StartsWith("Baustein «Gegner unter", s.EvolutionMissing(acid, drill)[0]);

            // Bohrstoß in eine Zeile mit «Gegner unter x %»: bereit.
            Assert.IsTrue(s.Runes.TryAdd(s.RuneCatalog.Get("enemy_low"), SkillInstance.BasicAttack()));
            Assert.IsTrue(s.PlaceSkill(drill.InstanceId, 2));
            Assert.IsEmpty(s.EvolutionMissing(acid, drill));
        }

        [Test]
        public void ABlockEvolvesAfterTheBossAndKeepsItsModules()
        {
            OverworldSession s = EmptySession();
            AddRow(s, "always", SkillIds.ShieldBash);
            AddRow(s, "hp_low", SkillIds.Repair);
            RuneSlot row = s.Runes.Rows[1];
            s.Runes.Upgrade(1);
            ModuleInstance extend = s.GainModule(ModuleIds.Extend);
            Assert.IsTrue(s.PlaceModuleOnRow(extend.InstanceId, 1));
            EvolutionRecipe reflex = s.EvolutionCatalog.Get("evo_reflex");
            Assert.AreEqual(1, s.EvolutionMissing(reflex, row).Count, "Noch nicht auf Höchststufe");

            s.Runes.Upgrade(1);
            Assert.IsEmpty(s.EvolutionMissing(reflex, row));
            Walk(s, OverworldSession.BossInterval);
            Assert.IsFalse(s.IsGameOver);
            Assert.AreEqual(EvolvedRuneIds.PhantomReflex, row.Rune.Id);
            Assert.AreEqual(2, row.Level);
            Assert.AreSame(extend, row.Modules.Single());
            StringAssert.StartsWith("HP unter 50 % oder ausgewichen", row.Name);
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
            SkillInstance ignite = AddRow(s, "always", SkillIds.Ignite);
            s.Skills.Grow(ignite, 30);

            Assert.AreEqual(new[] { "Evolution ???: fehlt Modul Fläche" }, s.EvolutionHintsForSkill(SkillIds.Ignite));
            StringAssert.StartsWith("Evolution ???: fehlt Modul Fläche (Teil des Rezepts für Entzünden)", s.EvolutionHintsForModule(ModuleIds.Area).Single());
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
            Assert.AreEqual("Feuersturm", nextRun.EvolutionName(nextRun.EvolutionCatalog.Get("evo_inferno")));
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
