using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class ModuleTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly ModuleCatalog Catalog = ModuleCatalog.CreateDefault();

        /// <summary>Nie erfüllt: solche Zeilen feuern nur über Auslöser.</summary>
        private const string Never = "hp_low";

        private static LogicRow Row(string runeId, SkillDefinition skill, int parameter = 0) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId);

        private static SkillDefinition With(string skillId, string moduleId, int level = 0) =>
            ModuleRules.ApplyToSkill(Skills.Get(skillId), new ModuleSpec(moduleId, level), Catalog.Get(moduleId).Name);

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static BattleSetup VersusTwo(LogicBoard board, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 1000, 10, 20, board: board), Fighter("B", 100000, 0, 1000), seed);
            setup.Enemies.Add(Fighter("C", 100000, 0, 1000));
            return setup;
        }

        private static int DamageOn(BattleResult r, string target) =>
            r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.Target?.Name == target).Sum(e => e.Amount);

        private static List<BattleEvent> Executions(BattleResult r, string skillId) =>
            r.Events.Where(e => e.Kind == BattleEventKind.ActionExecuted && e.Source?.Name == "A" && e.Detail == skillId).ToList();

        /// <summary>Jede Ausführung eines Skills hat davor ihren eigenen Start, mindestens <paramref name="cast"/> Ticks früher.</summary>
        private static void AssertCastBeforeEachExecution(BattleResult r, string skillId, int cast)
        {
            int started = -1;
            foreach (BattleEvent e in r.Events.Where(e => e.Source?.Name == "A"))
            {
                if (e.Kind == BattleEventKind.ActionStarted) started = e.Detail == skillId ? e.Tick : -1;
                if (e.Kind != BattleEventKind.ActionExecuted || e.Detail != skillId) continue;
                Assert.GreaterOrEqual(started, 0, $"Ausführung ohne eigenen Start bei Tick {e.Tick}");
                Assert.GreaterOrEqual(e.Tick - started, cast, $"Ausführung bei Tick {e.Tick} ohne volle Cast-Zeit");
                started = -1;
            }
        }

        private static string Fingerprint(BattleResult r) => string.Join("\n", r.Events);

        // ------------------------------------------------------------------ Skill-Module

        [Test]
        public void MulticastRepeatsTheEffectAfterANewCast()
        {
            SkillDefinition stab = With(SkillIds.ShockStab, ModuleIds.Multicast);
            Assert.AreEqual(1, stab.ExtraCasts);
            Assert.AreEqual(Skills.Get(SkillIds.ShockStab).CooldownTicks, stab.CooldownTicks, "Wiederholung kostet keinen Cooldown extra");

            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: new LogicBoard(new[] { Row("always", stab) })), Fighter("B", 100000, 0, 1000)), 10);
            List<BattleEvent> runs = Executions(r, SkillIds.ShockStab);
            int board = runs.Count(e => e.Cause == ActionCause.Board);
            Assert.Greater(board, 0);
            Assert.AreEqual(board, runs.Count(e => e.IsRepeat), "Jede Ausführung wird genau einmal wiederholt");
            AssertCastBeforeEachExecution(r, SkillIds.ShockStab, stab.CastTicks());
        }

        [Test]
        public void AreaHitsEveryEnemy()
        {
            SkillDefinition plain = Skills.Get(SkillIds.ShockStab);
            SkillDefinition area = With(SkillIds.ShockStab, ModuleIds.Area);
            Assert.IsTrue(area.Modules.Contains(Catalog.Get(ModuleIds.Area).Name));

            Assert.AreEqual(0, DamageOn(Run(VersusTwo(new LogicBoard(new[] { Row("always", plain) })), 10), "C"));
            BattleResult r = Run(VersusTwo(new LogicBoard(new[] { Row("always", area) })), 10);
            Assert.Greater(DamageOn(r, "B"), 0);
            Assert.Greater(DamageOn(r, "C"), 0, "Fläche trifft auch den zweiten Gegner");
        }

        [Test]
        public void ChainHitsASecondTarget()
        {
            SkillDefinition chain = With(SkillIds.ShockStab, ModuleIds.Chain);
            Assert.AreEqual(1, chain.ExtraTargets);

            BattleResult r = Run(VersusTwo(new LogicBoard(new[] { Row("always", chain) })), 10);
            Assert.Greater(DamageOn(r, "B"), 0);
            Assert.Greater(DamageOn(r, "C"), 0, "Kette springt auf das zweite Ziel");
        }

        [Test]
        public void BloodCostReplacesTheCooldownWithHp()
        {
            SkillDefinition stab = With(SkillIds.ShockStab, ModuleIds.BloodCost);
            Assert.AreEqual(0, stab.CooldownTicks);
            Assert.AreEqual(BasisPoints.Percent(ModuleRules.BloodCostPercent), stab.HpCostBp);

            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: new LogicBoard(new[] { Row("always", stab) })), Fighter("B", 100000, 0, 1000)), 5);
            List<BattleEvent> costs = r.Events.Where(e => e.Kind == BattleEventKind.SelfDamage && e.Detail == "hp_cost").ToList();
            Assert.AreEqual(Executions(r, SkillIds.ShockStab).Count(e => !e.IsRepeat), costs.Count, "Jeder Cast kostet HP");
            Assert.IsTrue(costs.All(e => e.Amount == 50), "5 % von 1000 HP");
        }

        [Test]
        public void QuickcastIsFasterButCoolsDownLonger()
        {
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            SkillDefinition quick = With(SkillIds.Drill, ModuleIds.Quickcast);
            Assert.AreEqual(drill.CastTicks() * 70 / 100, quick.CastTicks());
            Assert.AreEqual(drill.CooldownTicks * 130 / 100, quick.CooldownTicks);
        }

        [Test]
        public void SkillModulesLeaveTheBasicAttackAlone()
        {
            SkillDefinition basic = Skills.Get(SkillIds.BasicAttack);
            Assert.AreSame(basic, ModuleRules.ApplyToSkill(basic, new ModuleSpec(ModuleIds.Multicast), "Mehrfach"));
        }

        // ------------------------------------------------------------------ Baustein-Module

        /// <summary>Nur in den ersten 10 Ticks erfüllt.</summary>
        private sealed class EarlyWindow : ICondition
        {
            public bool IsMet(in ConditionContext context, out Combatant target)
            {
                target = null;
                return context.Tick < 10;
            }
        }

        [Test]
        public void InvertTurnsTheConditionAround()
        {
            var factory = BoardFactory.CreateDefault();
            LogicRow row = factory.CreateRow(new BoardRowSpec(Never, SkillIds.ShockStab, blockModules: new[] { new ModuleSpec(ModuleIds.Invert) }), null);
            StringAssert.StartsWith("NICHT ", row.Label);

            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: new LogicBoard(new[] { row })), Fighter("B", 100000, 0, 1000)), 5);
            Assert.Greater(Executions(r, SkillIds.ShockStab).Count, 0, "«NICHT HP unter 30 %» ist bei voller HP erfüllt");
        }

        [Test]
        public void ExtendHoldsTheConditionOneSecondLonger()
        {
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -Ticks.FromSeconds(100), 0);
            BattleResult Fight(ICondition condition) =>
                Run(Duel(Fighter("A", 1000, 0, 20, board: new LogicBoard(new[] { new LogicRow(condition, stab, "Fenster") })), Fighter("B", 100000, 0, 1000)), 5);

            int LastStart(BattleResult r) => r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A" && e.Detail == SkillIds.ShockStab)
                .Select(e => e.Tick).DefaultIfEmpty(-1).Max();

            int plain = LastStart(Fight(new EarlyWindow()));
            int extended = LastStart(Fight(ModuleRules.ApplyToCondition(new EarlyWindow(), new ModuleSpec(ModuleIds.Extend))));
            Assert.Less(plain, 10);
            Assert.Greater(extended, 10, "Nach dem Fenster wird noch gecastet");
            Assert.LessOrEqual(extended, 9 + ModuleRules.ExtendTicks, "Aber höchstens 1 s länger");
        }

        [Test]
        public void ThresholdRaisesPercentRunesOnly()
        {
            RuneCatalog runes = RuneCatalog.CreateDefault();
            var threshold = new[] { new ModuleSpec(ModuleIds.Threshold) };
            Assert.AreEqual(40, ModuleRules.ApplyToParameter(runes.Get("hp_low"), 30, threshold));
            Assert.AreEqual(100, ModuleRules.ApplyToParameter(runes.Get("hp_low"), 95, threshold), "Nie über 100 %");
            Assert.AreEqual(0, ModuleRules.ApplyToParameter(runes.Get("always"), 0, threshold), "Ohne Prozent-Schwelle wirkungslos");

            LogicRow row = BoardFactory.CreateDefault().CreateRow(new BoardRowSpec("hp_low", SkillIds.Repair, blockModules: threshold), null);
            Assert.AreEqual("HP unter 40 %", row.Label);
        }

        // ------------------------------------------------------------------ Auslöser

        private static LogicBoard TriggerBoard(SkillDefinition first, SkillDefinition second, bool circle = false)
        {
            var edges = new List<GraphEdge> { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) };
            if (circle) edges.Add(new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)));
            return new LogicBoard(new[] { Row("always", first), Row(Never, second) }, null, new LogicGraph(edges));
        }

        [Test]
        public void TriggerBuildsAnEdgeToTheTargetRow()
        {
            var specs = new[]
            {
                new BoardRowSpec("always", SkillIds.ShockStab, skillModules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 1) }),
                new BoardRowSpec(Never, SkillIds.Drill, blockModules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 0) }),
                new BoardRowSpec(Never, SkillIds.Repair, skillModules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 7), new ModuleSpec(ModuleIds.Trigger) }),
            };
            LogicBoard board = BoardFactory.CreateDefault().Create(specs, null);

            Assert.AreEqual(new[] { GraphNode.Skill(1) }, board.Graph.From(GraphNode.Skill(0)).Select(e => e.To).ToArray());
            Assert.AreEqual(new[] { GraphNode.Skill(0) }, board.Graph.From(GraphNode.Block(1)).Select(e => e.To).ToArray());
            Assert.IsFalse(board.Graph.HasEdgesFrom(GraphNode.Skill(2)), "Ziele ausserhalb der Tafel oder ohne Ziel ergeben keine Kante");
        }

        [Test]
        public void ATriggeredSkillCastsWithItsCastTime()
        {
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: TriggerBoard(Skills.Get(SkillIds.ShockStab), drill)), Fighter("B", 100000, 0, 1000)), 20);

            List<BattleEvent> drills = Executions(r, SkillIds.Drill);
            Assert.Greater(drills.Count, 0, "Die Bedingung der Zielzeile ist nie erfüllt, nur der Auslöser startet sie");
            Assert.IsTrue(drills.All(e => e.IsTriggered && e.CauseRow == 0 && e.RowIndex == 1));
            AssertCastBeforeEachExecution(r, SkillIds.Drill, drill.CastTicks());
        }

        [Test]
        public void ATriggerOnCooldownExpires()
        {
            // Schockstich fast ohne Cooldown, Bohrstoß mit langem: die meisten Auslöser verfallen.
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -Ticks.FromSeconds(100), 0);
            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: TriggerBoard(stab, Skills.Get(SkillIds.Drill))), Fighter("B", 100000, 0, 1000)), 20);

            List<BattleEvent> expired = r.Events.Where(e => e.Kind == BattleEventKind.TriggerExpired).ToList();
            Assert.Greater(expired.Count, 0);
            Assert.IsTrue(expired.All(e => e.Detail == SkillIds.Drill && e.Amount == 0 && e.RowIndex == 1));
            Assert.Greater(Executions(r, SkillIds.Drill).Count, 0, "Wenn bereit, löst er aus");
        }

        [Test]
        public void ABlockTriggerFiresWhenTheConditionBecomesTrue()
        {
            // «Immer» wird nur einmal wahr (beim Start): der Auslöser feuert genau einmal, obwohl der Bohrstoß sofort wieder bereit wäre.
            SkillDefinition drill = Skills.Get(SkillIds.Drill).WithBonus(0, -Ticks.FromSeconds(100), 0);
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Block(0), GraphNode.Skill(1)) });
            var board = new LogicBoard(new[] { Row("always", Skills.Get(SkillIds.ShockStab)), Row(Never, drill) }, null, graph);
            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: board), Fighter("B", 100000, 0, 1000)), 10);

            List<BattleEvent> drills = Executions(r, SkillIds.Drill);
            Assert.AreEqual(1, drills.Count);
            Assert.IsTrue(drills[0].IsTriggered && drills[0].CauseRow == 0);
        }

        [Test]
        public void ACircleOfTwoTriggersRunsAndStaysStable()
        {
            // Beide fast ohne Cooldown und Cast: der Kreis feuert so oft es geht, bleibt aber endlich und deterministisch.
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -Ticks.FromSeconds(100), -1000);
            SkillDefinition bash = Skills.Get(SkillIds.ShieldBash).WithBonus(0, -Ticks.FromSeconds(100), -1000);
            BattleSetup Setup() => Duel(Fighter("A", 1000, 0, 20, board: TriggerBoard(stab, bash, circle: true)), Fighter("B", 100000, 0, 1000));

            BattleResult r = null;
            Assert.DoesNotThrow(() => r = Run(Setup(), 10));
            Assert.Greater(Executions(r, SkillIds.ShieldBash).Count(e => e.IsTriggered && e.CauseRow == 0), 0);
            Assert.Greater(Executions(r, SkillIds.ShockStab).Count(e => e.IsTriggered && e.CauseRow == 1), 0, "Der Kreis schliesst sich");
            Assert.LessOrEqual(r.Events.Count(e => e.Kind == BattleEventKind.ActionExecuted && e.Source.Name == "A"),
                Ticks.FromSeconds(10) / CastTime.DefaultMinTicks + 1, "Höchstens eine Ausführung pro Mindest-Cast");
            Assert.AreEqual(Fingerprint(r), Fingerprint(Run(Setup(), 10)));
        }

        [Test]
        public void SameSeedsGiveTheSameResultWithModules()
        {
            SkillDefinition multi = With(SkillIds.ShockStab, ModuleIds.Multicast);
            SkillDefinition chain = With(SkillIds.Drill, ModuleIds.Chain);
            BattleSetup Setup(int seed) => VersusTwo(TriggerBoard(multi, chain, circle: true), seed);

            foreach (int seed in new[] { 1, 7, 42 })
                Assert.AreEqual(Fingerprint(Run(Setup(seed))), Fingerprint(Run(Setup(seed))));
        }

        [Test]
        public void TriggeredExecutionsAreTraceableInTheLog()
        {
            BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: TriggerBoard(Skills.Get(SkillIds.ShockStab), Skills.Get(SkillIds.Drill))), Fighter("B", 100000, 0, 1000)), 20);
            BattleEvent started = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.Drill);
            Assert.IsTrue(started.IsTriggered);
            StringAssert.Contains("ausgelöst von Zeile 1", BattleLogText.Describe(started, r));

            List<BattleEvent> expired = r.Events.Where(e => e.Kind == BattleEventKind.TriggerExpired).ToList();
            if (expired.Count > 0) StringAssert.Contains("Auslöser von Zeile 1 verfällt", BattleLogText.Describe(expired[0], r));
        }

        // ------------------------------------------------------------------ Sammlung und Tafel

        private static OverworldSession NewSession() =>
            OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: KnightKit.Defaults[0]);

        [Test]
        public void AModuleSitsInExactlyOnePlace()
        {
            OverworldSession s = NewSession();
            s.Runes.TryAdd(s.RuneCatalog.Get("always"), s.Skills.Add(SkillIds.Repair));
            Assert.GreaterOrEqual(s.Runes.Rows.Count, 2);
            int first = s.Runes.Rows[0].Skill.InstanceId, second = s.Runes.Rows[1].Skill.InstanceId;

            ModuleInstance multi = s.GainModule(ModuleIds.Multicast);
            Assert.IsTrue(multi.IsFree);
            Assert.IsTrue(s.PlaceModuleOnSkill(multi.InstanceId, first));
            Assert.IsTrue(s.PlaceModuleOnSkill(multi.InstanceId, second));
            Assert.AreEqual(0, s.Skills.Get(first).Modules.Count, "Der alte Ort ist wieder leer");
            Assert.AreSame(multi, s.Skills.Get(second).Modules.Single());

            ModuleInstance area = s.GainModule(ModuleIds.Area);
            Assert.IsFalse(s.PlaceModuleOnSkill(area.InstanceId, second), "Nur ein Platz am Start");
            Assert.IsFalse(s.PlaceModuleOnRow(area.InstanceId, 0), "Skill-Module passen nicht an Bausteine");
            ModuleInstance invert = s.GainModule(ModuleIds.Invert);
            Assert.IsFalse(s.PlaceModuleOnSkill(invert.InstanceId, first), "Baustein-Module passen nicht an Skills");
            Assert.IsTrue(s.PlaceModuleOnRow(invert.InstanceId, 0));

            Assert.IsTrue(s.TakeOffModule(multi.InstanceId));
            Assert.IsTrue(multi.IsFree);
        }

        [Test]
        public void TriggerTargetsFollowStableIdsNotRowNumbers()
        {
            OverworldSession s = NewSession();
            s.Runes.TryAdd(s.RuneCatalog.Get("always"), s.Skills.Add(SkillIds.Repair));
            s.Runes.TryAdd(s.RuneCatalog.Get("hp_full"), s.Skills.Add(SkillIds.Drill));
            RuneSlot source = s.Runes.Rows[0];
            RuneSlot targetRow = s.Runes.Rows[2];

            ModuleInstance trigger = s.GainModule(ModuleIds.Trigger);
            Assert.IsTrue(s.PlaceModuleOnSkill(trigger.InstanceId, source.Skill.InstanceId));
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Skill(targetRow.Skill.InstanceId)));
            Assert.AreEqual(2, RuneLoadoutBoard.TargetRow(s.Runes, trigger.Target));

            Assert.IsTrue(s.Runes.Move(2, 1));
            Assert.AreEqual(1, RuneLoadoutBoard.TargetRow(s.Runes, trigger.Target), "Das Ziel wandert mit");
            Assert.AreEqual(1, s.Runes.ToBoardSpecs()[0].SkillModules.Single().TargetRow);

            // Zeilen-Ziel: bleibt an der Zeile, egal welcher Skill dort sitzt.
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Row(targetRow.RowId)));
            Assert.IsTrue(s.Runes.Move(1, 2));
            Assert.AreEqual(2, RuneLoadoutBoard.TargetRow(s.Runes, trigger.Target));

            // Ziel nicht mehr an der Tafel: kein Auslöser.
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Skill(9999)));
            Assert.AreEqual(-1, s.Runes.ToBoardSpecs()[0].SkillModules.Single().TargetRow);
        }

        [Test]
        public void ModulesOfARemovedRowBecomeFree()
        {
            OverworldSession s = NewSession();
            s.Runes.TryAdd(s.RuneCatalog.Get("always"), s.Skills.Add(SkillIds.Repair));
            ModuleInstance extend = s.GainModule(ModuleIds.Extend);
            int last = s.Runes.Rows.Count - 1;
            Assert.IsTrue(s.PlaceModuleOnRow(extend.InstanceId, last));

            s.Runes.RemoveAt(last);
            Assert.IsTrue(extend.IsFree);
        }

        [Test]
        public void ADuplicateModuleUpgradesOrStaysASecondCopy()
        {
            OverworldSession s = NewSession();
            ModuleInstance a = s.GainModule(ModuleIds.Chain);
            Assert.AreSame(a, s.GainModule(ModuleIds.Chain));
            Assert.AreEqual(1, a.Level);
            Assert.AreEqual("Kette +1", a.NameFrom(s.ModuleCatalog));

            ModuleInstance b = s.GainModule(ModuleIds.Chain);
            Assert.AreNotSame(a, b, "Auf Maximalstufe kommt ein weiteres Exemplar");
            Assert.AreEqual(2, s.Modules.OfModule(ModuleIds.Chain).Count);
        }

        [Test]
        public void ModulesAreRare()
        {
            var config = new ProgressionConfig();
            Assert.Greater(config.ModuleOfferChance(RewardSources.Elite), 0);
            Assert.Less(config.ModuleOfferChance(RewardSources.Elite), 50);
            Assert.AreEqual(0, config.ModuleOfferChance(RewardSources.Victory), "Normale Kämpfe geben keine Module");
            var prices = new ShopPrices();
            Assert.Greater(prices.Module, prices.Skill, "Teurer Shop-Platz");
        }
    }
}
