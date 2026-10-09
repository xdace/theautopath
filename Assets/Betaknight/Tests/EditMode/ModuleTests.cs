using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using Betaknight.Core.Skills;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Module (A-19): Skill-Module an Komponenten, Relais-Module (Invert, Extend, Threshold, Repeat while true) und Auslöser.</summary>
    public class ModuleTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly ModuleCatalog Catalog = ModuleCatalog.CreateDefault();

        /// <summary>Nie erfüllt: solche Komponenten feuern nur über Auslöser.</summary>
        private const string Never = "hp_low";

        private static LogicRow Row(string runeId, SkillDefinition skill, int parameter = 0) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId);

        /// <summary>Ersatz für das frühere «Always»: ein Clock-Relais, das alle <paramref name="ticks"/> auslöst.</summary>
        private static LogicRow Clock(SkillDefinition skill, int ticks = Ticks.PerSecond) =>
            new LogicRow(new ClockCondition(ticks), skill, "clock");

        private static SkillDefinition With(string skillId, string moduleId, int level = 0) =>
            ModuleRules.ApplyToSkill(Skills.Get(skillId), new ModuleSpec(moduleId, level), Catalog.Get(moduleId).Name);

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static BattleSetup Solo(LogicBoard board, int damage = 0) =>
            Duel(Fighter("A", 1000, damage, 20, board: board), Fighter("B", 100000, 0, 1000));

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

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source?.Name == "A" && e.Detail == skillId).ToList();

        private static int FirstDamage(BattleResult r, string skillId) =>
            r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.Target?.Name == "B"
                && r.Events.Any(s => s.Kind == BattleEventKind.ActionExecuted && s.Tick == e.Tick && s.Detail == skillId)).Amount;

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

        private static CircuitSpec Circuit(IEnumerable<RelaySpec> relays, IEnumerable<ComponentSpec> components)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.AddRange(relays);
            spec.Components.AddRange(components);
            return spec;
        }

        // ------------------------------------------------------------------ Skill-Module

        [Test]
        public void MulticastRepeatsTheEffectAfterANewCast()
        {
            SkillDefinition stab = With(SkillIds.ShockStab, ModuleIds.Multicast);
            Assert.AreEqual(1, stab.ExtraCasts);

            BattleResult r = Run(Solo(new LogicBoard(new[] { Clock(stab, Ticks.FromSeconds(2)) })), 10);
            List<BattleEvent> runs = Executions(r, SkillIds.ShockStab);
            int board = runs.Count(e => e.Cause == ActionCause.Board);
            Assert.Greater(board, 0);
            Assert.AreEqual(board, runs.Count(e => e.IsRepeat), "Jede Ausführung wird genau einmal wiederholt");
            Assert.IsFalse(r.Events.Any(e => e.Kind == BattleEventKind.TriggerMissed), "Die Wiederholung belegt keinen Platz in der Warteschlange");
            AssertCastBeforeEachExecution(r, SkillIds.ShockStab, stab.CastTicks());
        }

        [Test]
        public void AreaHitsEveryEnemy()
        {
            SkillDefinition plain = Skills.Get(SkillIds.ShockStab);
            SkillDefinition area = With(SkillIds.ShockStab, ModuleIds.Area);
            Assert.IsTrue(area.Modules.Contains(Catalog.Get(ModuleIds.Area).Name));

            Assert.AreEqual(0, DamageOn(Run(VersusTwo(new LogicBoard(new[] { Clock(plain) }, SkillDefinition.CreateBasicAttack(0))), 10), "C"));
            BattleResult r = Run(VersusTwo(new LogicBoard(new[] { Clock(area) })), 10);
            Assert.Greater(DamageOn(r, "B"), 0);
            Assert.Greater(DamageOn(r, "C"), 0, "Fläche trifft auch den zweiten Gegner");
        }

        [Test]
        public void ChainHitsASecondTarget()
        {
            SkillDefinition chain = With(SkillIds.ShockStab, ModuleIds.Chain);
            Assert.AreEqual(1, chain.ExtraTargets);

            BattleResult r = Run(VersusTwo(new LogicBoard(new[] { Clock(chain) })), 10);
            Assert.Greater(DamageOn(r, "B"), 0);
            Assert.Greater(DamageOn(r, "C"), 0, "Kette springt auf das zweite Ziel");
        }

        [TestCase(0, 5, 40)]
        [TestCase(1, 4, 50)]
        public void BloodTollCostsHpPerCastAndAddsEffect(int level, int costPercent, int powerPercent)
        {
            SkillDefinition stab = With(SkillIds.ShockStab, ModuleIds.BloodCost, level);
            Assert.AreEqual(BasisPoints.Percent(costPercent), stab.HpCostBp);
            Assert.AreEqual(powerPercent, stab.PowerBonusPercent, "+40 % Wirkung, +10 % je Stufe");
            Assert.AreEqual(Skills.Get(SkillIds.ShockStab).CastTicks(), stab.CastTicks(), "Cast-Zeit bleibt");

            BattleResult r = Run(Solo(new LogicBoard(new[] { Clock(stab) }), damage: 100), 5);
            List<BattleEvent> costs = r.Events.Where(e => e.Kind == BattleEventKind.SelfDamage && e.Detail == "hp_cost").ToList();
            Assert.Greater(costs.Count, 0);
            Assert.AreEqual(Starts(r, SkillIds.ShockStab).Count(e => !e.IsRepeat), costs.Count, "Jeder Cast kostet HP (beim Start)");
            Assert.IsTrue(costs.All(e => e.Amount == 1000 * costPercent / 100), $"{costPercent} % von 1000 HP");
            Assert.AreEqual(60 * (100 + powerPercent) / 100, FirstDamage(r, SkillIds.ShockStab), "60 % Waffenschaden mit Bonus");
        }

        [TestCase(0, 30)]
        [TestCase(1, 40)]
        public void QuickcastIsFasterButWeaker(int level, int castPercent)
        {
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            SkillDefinition quick = With(SkillIds.Drill, ModuleIds.Quickcast, level);
            Assert.AreEqual(drill.CastTicks() * (100 - castPercent) / 100, quick.CastTicks(), "−30 % Cast-Zeit, −10 % je Stufe");
            Assert.AreEqual(-15, quick.PowerBonusPercent, "−15 % Wirkung");

            int plainDamage = FirstDamage(Run(Solo(new LogicBoard(new[] { Clock(drill) }), damage: 100), 3), SkillIds.Drill);
            int quickDamage = FirstDamage(Run(Solo(new LogicBoard(new[] { Clock(quick) }), damage: 100), 3), SkillIds.Drill);
            Assert.AreEqual(210, plainDamage);
            Assert.AreEqual(plainDamage * 85 / 100, quickDamage, 1);
        }

        [Test]
        public void SkillModulesLeaveTheBasicAttackAlone()
        {
            SkillDefinition basic = Skills.Get(SkillIds.BasicAttack);
            Assert.AreSame(basic, ModuleRules.ApplyToSkill(basic, new ModuleSpec(ModuleIds.Multicast), "Mehrfach"));
        }

        // ------------------------------------------------------------------ Relais-Module

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
            LogicRelay relay = factory.CreateRelay(new RelaySpec(Never, new Cell(0, 0), modules: new[] { new ModuleSpec(ModuleIds.Invert) }), null);
            StringAssert.StartsWith("NOT ", relay.Label);

            LogicBoard board = factory.Create(Circuit(new[] { new RelaySpec(Never, new Cell(0, 0), modules: new[] { new ModuleSpec(ModuleIds.Invert) }) },
                new[] { new ComponentSpec(SkillIds.ShockStab, new Cell(1, 0)) }), null);
            Assert.IsTrue(board.Rows.Single().IsPowered);
            BattleResult r = Run(Solo(board), 5);
            Assert.Greater(Executions(r, SkillIds.ShockStab).Count, 0, "«NICHT HP unter 30 %» ist bei voller HP erfüllt");
        }

        [Test]
        public void ExtendHoldsTheConditionOneSecondLonger()
        {
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -1000);
            // Mit «Repeat while true»: die Komponente läuft genau so lange wieder, wie die Bedingung gilt.
            BattleResult Fight(ICondition condition) =>
                Run(Solo(new LogicBoard(new[] { new LogicRow(condition, stab, "Fenster", repeatWhileTrue: true) })), 5);

            int LastStart(BattleResult r) => Starts(r, SkillIds.ShockStab).Select(e => e.Tick).DefaultIfEmpty(-1).Max();

            int plain = LastStart(Fight(new EarlyWindow()));
            int extended = LastStart(Fight(ModuleRules.ApplyToCondition(new EarlyWindow(), new ModuleSpec(ModuleIds.Extend))));
            Assert.Less(plain, 10);
            Assert.Greater(extended, 10, "Nach dem Fenster wird noch gecastet");
            // Geprüft wird nach der Ausführung; der neue Start folgt nach der Erholung.
            Assert.LessOrEqual(extended, 9 + ModuleRules.ExtendTicks + stab.RecoveryTicks, "Aber höchstens 1 s länger");
        }

        [Test]
        public void ThresholdRaisesPercentRunesOnly()
        {
            RuneCatalog runes = RuneCatalog.CreateDefault();
            var threshold = new[] { new ModuleSpec(ModuleIds.Threshold) };
            Assert.AreEqual(40, ModuleRules.ApplyToParameter(runes.Get("hp_low"), 30, threshold));
            Assert.AreEqual(100, ModuleRules.ApplyToParameter(runes.Get("hp_low"), 95, threshold), "Nie über 100 %");
            Assert.AreEqual(2, ModuleRules.ApplyToParameter(runes.Get("clock"), 2, threshold), "Ohne Prozent-Schwelle wirkungslos");

            LogicRelay relay = BoardFactory.CreateDefault().CreateRelay(new RelaySpec("hp_low", new Cell(0, 0), modules: threshold), null);
            Assert.AreEqual("HP Below 40 %", relay.Label);
        }

        [Test]
        public void RepeatWhileTrueIsARelayModuleThatRequeuesWhileTheStateHolds()
        {
            ModuleDefinition repeat = Catalog.Get(ModuleIds.RepeatWhileTrue);
            Assert.AreEqual("Repeat while true", repeat.Name);
            Assert.IsTrue(repeat.FitsBlock);
            Assert.IsFalse(repeat.FitsSkill);

            var factory = BoardFactory.CreateDefault();
            ModuleSpec[] modules = { new ModuleSpec(ModuleIds.RepeatWhileTrue) };
            Assert.IsTrue(factory.CreateRelay(new RelaySpec("hp_full", new Cell(0, 0), modules: modules), null).RepeatWhileTrue);
            Assert.IsFalse(factory.CreateRelay(new RelaySpec("hp_full", new Cell(0, 0)), null).RepeatWhileTrue);

            // «HP Full» gilt, bis der Gegner trifft (nach 3 s): solange läuft die Komponente immer wieder, danach nicht mehr.
            int Stabs(bool withModule, out int firstHit)
            {
                LogicBoard board = factory.Create(Circuit(new[] { new RelaySpec("hp_full", new Cell(0, 0), modules: withModule ? modules : null) },
                    new[] { new ComponentSpec(SkillIds.ShockStab, new Cell(1, 0)) }), null);
                BattleResult r = Run(Duel(Fighter("A", 1000, 0, 20, board: board), Fighter("B", 100000, 1, Ticks.FromSeconds(3))), 8);
                firstHit = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Target?.Name == "A").Tick;
                int hit = firstHit;
                Assert.AreEqual(1, r.Events.Count(e => e.Kind == BattleEventKind.RelayTriggered && e.Source?.Name == "A"), "das Relais löst einmal aus");
                Assert.IsFalse(r.Events.Any(e => e.Kind == BattleEventKind.RowQueued && e.Source?.Name == "A" && e.Tick > hit),
                    "nach dem Ende des Zustands keine Wiederholung mehr");
                return Starts(r, SkillIds.ShockStab).Count;
            }
            Assert.AreEqual(1, Stabs(false, out _));
            Assert.Greater(Stabs(true, out int until), 1);
            Assert.Greater(until, Ticks.FromSeconds(2));
        }

        // ------------------------------------------------------------------ Auslöser

        private static LogicBoard TriggerBoard(SkillDefinition first, SkillDefinition second, bool circle = false, bool fromBlock = false)
        {
            var edges = new List<GraphEdge> { new GraphEdge(fromBlock ? GraphNode.Block(0) : GraphNode.Skill(0), GraphNode.Skill(1)) };
            if (circle) edges.Add(new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)));
            return new LogicBoard(new[] { Clock(first), Row(Never, second) }, null, new LogicGraph(edges));
        }

        [Test]
        public void TriggerBuildsAnEdgeToTheTargetComponent()
        {
            // Komponenten in Lesereihenfolge: #1 Schockstoss (1,0), #2 Bohrer (1,2), #3 Reparatur (1,4). Relais: Clock (0,0), HP-Low (0,2).
            CircuitSpec spec = Circuit(
                new[]
                {
                    new RelaySpec("clock", new Cell(0, 0)),
                    new RelaySpec(Never, new Cell(0, 2), modules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 0) }),
                },
                new[]
                {
                    new ComponentSpec(SkillIds.Repair, new Cell(1, 4), modules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 7), new ModuleSpec(ModuleIds.Trigger) }),
                    new ComponentSpec(SkillIds.ShockStab, new Cell(1, 0), modules: new[] { new ModuleSpec(ModuleIds.Trigger, 0, 1) }),
                    new ComponentSpec(SkillIds.Drill, new Cell(1, 2)),
                });
            LogicBoard board = BoardFactory.CreateDefault().Create(spec, null);

            CollectionAssert.AreEqual(new[] { SkillIds.ShockStab, SkillIds.Drill, SkillIds.Repair }, board.Rows.Select(r => r.Skill.Id).ToArray());
            // Seit A-20 enthält der Graph auch Versorgung, Pins und Pulse; geprüft werden die Auslöser-Kanten.
            IEnumerable<GraphNode> Triggers(GraphNode from) => board.Graph.From(from).Where(e => e.Kind == GraphEdgeKind.Trigger).Select(e => e.To);
            Assert.AreEqual(new[] { GraphNode.Skill(1) }, Triggers(GraphNode.Skill(0)).ToArray());
            Assert.AreEqual(new[] { GraphNode.Skill(0) }, Triggers(GraphNode.Block(1)).ToArray());
            Assert.IsEmpty(Triggers(GraphNode.Skill(2)), "Ziele ausserhalb der Platine oder ohne Ziel ergeben keine Kante");
        }

        [Test]
        public void ATriggeredSkillCastsWithItsCastTime()
        {
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            BattleResult r = Run(Solo(TriggerBoard(Skills.Get(SkillIds.ShockStab), drill)), 20);

            List<BattleEvent> drills = Executions(r, SkillIds.Drill);
            Assert.Greater(drills.Count, 0, "Das eigene Relais der Ziel-Komponente löst nie aus, nur der Auslöser startet sie");
            Assert.IsTrue(drills.All(e => e.IsTriggered && e.CauseRow == 0 && e.RowIndex == 1));
            AssertCastBeforeEachExecution(r, SkillIds.Drill, drill.CastTicks());
        }

        [Test]
        public void ATriggerQueuesTheTargetAndIsMissedWhileItStillWaits()
        {
            // Früher: Auslöser im Cooldown verfallen oder werden eingereiht. Jetzt: jeder Auslöser reiht ein; wartet das
            // Ziel schon, ist es ein «Missed Trigger». Das Relais (alle 1 s) löst den langsamen Bohrer (6 s Cast) aus.
            SkillDefinition slow = Skills.Get(SkillIds.Drill).WithBonus(0, 300);
            BattleResult r = Run(Solo(TriggerBoard(Skills.Get(SkillIds.ShockStab), slow, fromBlock: true)), 20);

            List<BattleEvent> queued = r.Events.Where(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == 1).ToList();
            Assert.Greater(queued.Count, 0);
            Assert.IsTrue(queued.All(e => e.Cause == ActionCause.Trigger && e.CauseRow == -1));
            Assert.Greater(Starts(r, SkillIds.Drill).Count(e => e.FromQueue && e.IsTriggered), 0, "Gestartet wird aus der Warteschlange");
            List<BattleEvent> missed = r.Events.Where(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 1).ToList();
            Assert.Greater(missed.Count, 0);
            Assert.IsTrue(missed.All(e => e.Amount == (int)MissReason.AlreadyQueued && e.Detail == SkillIds.Drill));
        }

        [Test]
        public void ABlockTriggerFiresOnTheRisingEdgeOfAState()
        {
            // «HP Full» bleibt wahr (der Gegner trifft nie), löst aber nur einmal aus: der Auslöser feuert genau einmal.
            SkillDefinition drill = Skills.Get(SkillIds.Drill).WithBonus(0, -1000);
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Block(0), GraphNode.Skill(1)) });
            var board = new LogicBoard(new[] { Row("hp_full", Skills.Get(SkillIds.ShockStab)), Row(Never, drill) }, null, graph);
            BattleResult r = Run(Solo(board), 10);

            List<BattleEvent> drills = Executions(r, SkillIds.Drill);
            Assert.AreEqual(1, drills.Count);
            Assert.IsTrue(drills[0].IsTriggered);
        }

        [Test]
        public void ACircleOfTwoTriggersRunsAndStaysStable()
        {
            // Beide fast ohne Cast: der Kreis feuert so oft es geht, bleibt aber endlich und deterministisch.
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -1000);
            SkillDefinition bash = Skills.Get(SkillIds.ShieldBash).WithBonus(0, -1000);
            LogicBoard Circle()
            {
                var edges = new List<GraphEdge> { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)), new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)) };
                return new LogicBoard(new[] { Row("battle_start", stab), Row(Never, bash) }, null, new LogicGraph(edges));
            }
            BattleSetup Setup() => Solo(Circle());

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
            BattleResult r = Run(Solo(TriggerBoard(Skills.Get(SkillIds.ShockStab), Skills.Get(SkillIds.Drill))), 20);
            BattleEvent started = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.Drill);
            Assert.IsTrue(started.IsTriggered);
            StringAssert.Contains("triggered by #1", BattleLogText.Describe(started, r));

            List<BattleEvent> missed = r.Events.Where(e => e.Kind == BattleEventKind.TriggerMissed).ToList();
            if (missed.Count > 0) StringAssert.Contains("missed trigger on", BattleLogText.Describe(missed[0], r));
        }

        [Test]
        public void TheReportShowsTheChain()
        {
            BattleReport report = BattleReport.Create(Run(Solo(TriggerBoard(Skills.Get(SkillIds.ShockStab), Skills.Get(SkillIds.Drill)))));

            RowReport drill = report.Rows[1];
            Assert.Greater(drill.Triggered, 0);
            Assert.AreEqual(drill.Fired, drill.FromTriggerModule, "Die Komponente startet nur über den Auslöser");
            Assert.AreEqual(drill.Queued, drill.Fired, "Jede Einreihung führt zu einer Ausführung");
            Assert.AreEqual(drill.Queued + drill.Missed, drill.Triggered, "Wartet sie noch, ist ein Auslöser «missed»");
            Assert.AreEqual(drill.Missed, drill.MissCount(MissReason.AlreadyQueued));
            Assert.AreEqual($"#1 ×{drill.FromTriggerModule}", drill.TriggeredByText);
            Assert.IsTrue(report.Hints.Any(h => h.StartsWith("#2 ") && h.Contains(" by #1 ×")), string.Join("\n", report.Hints));
        }

        // ------------------------------------------------------------------ Sammlung und Platine

        private static OverworldSession NewSession() =>
            OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: KnightKit.Defaults[0]);

        /// <summary>Legt einen neuen Skill als Komponente auf eine freie Zelle.</summary>
        private static SkillInstance Place(OverworldSession s, string skillId, Cell at)
        {
            SkillInstance skill = s.Skills.Add(skillId);
            Assert.IsTrue(s.PlaceSkill(skill.InstanceId, at), $"{skillId} auf {at}");
            return skill;
        }

        [Test]
        public void AModuleSitsInExactlyOnePlace()
        {
            OverworldSession s = NewSession();
            Place(s, SkillIds.Thrusters, new Cell(3, 0));
            Assert.GreaterOrEqual(s.Board.Components.Count, 2);
            int first = s.Board.Components[0].Skill.InstanceId, second = s.Board.Components[1].Skill.InstanceId;

            ModuleInstance multi = s.GainModule(ModuleIds.Multicast);
            Assert.IsTrue(multi.IsFree);
            Assert.IsTrue(s.PlaceModuleOnSkill(multi.InstanceId, first));
            Assert.IsTrue(s.PlaceModuleOnSkill(multi.InstanceId, second));
            Assert.AreEqual(0, s.Skills.Get(first).Modules.Count, "Der alte Ort ist wieder leer");
            Assert.AreSame(multi, s.Skills.Get(second).Modules.Single());

            ModuleInstance area = s.GainModule(ModuleIds.Area);
            Assert.IsFalse(s.PlaceModuleOnSkill(area.InstanceId, second), "Nur ein Platz am Start");
            Assert.IsFalse(s.PlaceModuleOnRelay(area.InstanceId, 0), "Skill-Module passen nicht an Relais");
            ModuleInstance invert = s.GainModule(ModuleIds.Invert);
            Assert.IsFalse(s.PlaceModuleOnSkill(invert.InstanceId, first), "Relais-Module passen nicht an Skills");
            Assert.IsTrue(s.PlaceModuleOnRelay(invert.InstanceId, 0));

            Assert.IsTrue(s.TakeOffModule(multi.InstanceId));
            Assert.IsTrue(multi.IsFree);
        }

        [Test]
        public void RepeatWhileTrueSitsOnARelayAndReachesTheFight()
        {
            OverworldSession s = NewSession();
            ModuleInstance repeat = s.GainModule(ModuleIds.RepeatWhileTrue);
            Assert.IsFalse(s.PlaceModuleOnSkill(repeat.InstanceId, s.Board.Components[0].Skill.InstanceId), "Relais-Modul");
            Assert.IsFalse(s.CompileBoard().Relays[0].RepeatWhileTrue);
            Assert.IsTrue(s.PlaceModuleOnRelay(repeat.InstanceId, 0));
            Assert.AreEqual(ModuleIds.RepeatWhileTrue, s.Board.ToSpec().Relays[0].Modules.Single().ModuleId);
            Assert.IsTrue(s.CompileBoard().Relays[0].RepeatWhileTrue);
        }

        [Test]
        public void TriggerTargetsFollowTheComponentNotItsIndex()
        {
            OverworldSession s = NewSession();
            SkillInstance source = s.Board.Components[0].Skill;
            Place(s, SkillIds.Thrusters, new Cell(3, 0));
            SkillInstance target = Place(s, SkillIds.ChargeCoil, new Cell(0, 2));

            ModuleInstance trigger = s.GainModule(ModuleIds.Trigger);
            Assert.IsTrue(s.PlaceModuleOnSkill(trigger.InstanceId, source.InstanceId));
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Skill(target.InstanceId)));
            Assert.AreEqual(2, CircuitBoardSpec.TargetComponent(s.Board, trigger.Target));

            // Verschoben nach (2,0): in Lesereihenfolge jetzt #2, das Ziel wandert mit.
            int index = s.Board.IndexOf(s.Board.ComponentOf(target));
            Assert.IsTrue(s.MoveComponent(index, new Cell(2, 0), false));
            Assert.AreEqual(1, CircuitBoardSpec.TargetComponent(s.Board, trigger.Target), "Das Ziel wandert mit");
            Assert.AreEqual(1, s.Board.ToSpec().Components[0].Modules.Single().TargetRow);

            // Komponenten-Ziel: bleibt an der Komponente, auch wenn sie wieder verschoben wird.
            ComponentSlot slot = s.Board.ComponentOf(target);
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Row(slot.SlotId)));
            Assert.IsTrue(s.MoveComponent(s.Board.IndexOf(slot), new Cell(0, 2), false));
            Assert.AreEqual(2, CircuitBoardSpec.TargetComponent(s.Board, trigger.Target));

            // Ziel nicht mehr auf der Platine: kein Auslöser.
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Skill(9999)));
            Assert.AreEqual(-1, s.Board.ToSpec().Components[0].Modules.Single().TargetRow);
            Assert.AreEqual("After execution → target not on the board", s.DescribeTrigger(trigger));
        }

        [Test]
        public void TriggerTargetsCanBeCycledAndShowAsLinks()
        {
            OverworldSession s = NewSession();
            Place(s, SkillIds.Thrusters, new Cell(3, 0));
            ModuleInstance after = s.GainModule(ModuleIds.Trigger);
            ModuleInstance when = s.GainModule(ModuleIds.Trigger, SkillDuplicateChoice.KeepCopy);
            Assert.IsTrue(s.PlaceModuleOnSkill(after.InstanceId, s.Board.Components[0].Skill.InstanceId));
            Assert.IsTrue(s.PlaceModuleOnRelay(when.InstanceId, 0));
            Assert.AreEqual("After execution → no target", s.DescribeTrigger(after));
            Assert.IsEmpty(s.TriggerLinks());

            Assert.IsTrue(s.CycleTriggerTarget(after.InstanceId));
            Assert.AreEqual(0, CircuitBoardSpec.TargetComponent(s.Board, after.Target), "Erstes Ziel: Komponente #1 (auch sich selbst)");
            Assert.IsTrue(s.CycleTriggerTarget(after.InstanceId));
            Assert.AreEqual(1, CircuitBoardSpec.TargetComponent(s.Board, after.Target));
            Assert.IsTrue(s.CycleTriggerTarget(when.InstanceId));
            StringAssert.StartsWith("When triggered → ", s.DescribeTrigger(when));

            var links = s.TriggerLinks();
            Assert.AreEqual(2, links.Count, "Komponente #1 → #2, Relais 1 → #1");
            Assert.IsTrue(links.Any(l => l.From == 0 && l.To == 1 && !l.FromBlock));
            Assert.IsTrue(links.Any(l => l.From == 0 && l.To == 0 && l.FromBlock));

            for (int i = 0; i < s.Board.Components.Count - 1; i++) s.CycleTriggerTarget(after.InstanceId);
            Assert.IsNull(after.Target, "Nach dem letzten Ziel kommt «kein Ziel»");
        }

        [Test]
        public void ModulesOfARemovedRelayBecomeFree()
        {
            OverworldSession s = NewSession();
            ModuleInstance extend = s.GainModule(ModuleIds.Extend);
            int last = s.Board.Relays.Count - 1;
            Assert.IsTrue(s.PlaceModuleOnRelay(extend.InstanceId, last));

            Assert.IsTrue(s.UnequipRune(last));
            Assert.IsTrue(extend.IsFree);
        }

        [Test]
        public void ADuplicateModuleUpgradesOrStaysASecondCopy()
        {
            OverworldSession s = NewSession();
            ModuleInstance a = s.GainModule(ModuleIds.Chain);
            Assert.AreSame(a, s.GainModule(ModuleIds.Chain));
            Assert.AreEqual(1, a.Level);
            Assert.AreEqual("Chain +1", a.NameFrom(s.ModuleCatalog));

            ModuleInstance b = s.GainModule(ModuleIds.Chain);
            Assert.AreNotSame(a, b, "Auf Maximalstufe kommt ein weiteres Exemplar");
            Assert.AreEqual(2, s.Modules.OfModule(ModuleIds.Chain).Count);

            ModuleInstance invert = s.GainModule(ModuleIds.Invert);
            Assert.AreNotSame(invert, s.GainModule(ModuleIds.Invert), "Umkehren hat keine Stufen: gleich ein weiteres Exemplar");
            Assert.AreEqual(0, invert.Level);
        }

        [Test]
        public void ModulesAreRare()
        {
            var config = new ProgressionConfig();
            Assert.AreEqual(0, config.ModuleOfferChance(RewardSources.Elite), "Elite-Module kommen übers Bergen von der Gegner-Platine");
            Assert.Greater(config.ModuleOfferChance(RewardSources.Treasure), 0);
            Assert.Less(config.ModuleOfferChance(RewardSources.Treasure), 50);
            Assert.AreEqual(0, config.ModuleOfferChance(RewardSources.Victory), "Normale Kämpfe geben keine Module");
            var prices = new ShopPrices();
            Assert.Greater(prices.Module, prices.Skill, "Teurer Shop-Platz");
        }
    }
}
