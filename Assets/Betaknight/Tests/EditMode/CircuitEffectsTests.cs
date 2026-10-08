using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// A-21: eigene Effekte der Platine (Overclock, Interrupt, Parallel Thread, Buffer, Recursion, Amplifier, Watchdog,
    /// Overflow), Hacks gegen die gegnerische Platine (Bit Flip, Jam, Hijack, Short Circuit, Latency, Firewall) und Thermal
    /// Throttling. Die Form jedes Effekts (Modul, Chip, Skill) steht in den Daten.
    /// </summary>
    public class CircuitEffectsTests
    {
        private static readonly BoardFactory Factory = BoardFactory.CreateDefault();
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly CircuitEffectConfig Fx = CircuitEffectConfig.Default;

        // ------------------------------------------------------------------ Helfer

        private static RelaySpec Relay(string runeId, int x, int y) => new RelaySpec(runeId, new Cell(x, y));

        private static ComponentSpec Part(string skillId, int x, int y, params string[] modules) =>
            new ComponentSpec(skillId, new Cell(x, y), false, modules.Select(m => new ModuleSpec(m)).ToList());

        private static ChipSpec Chip(string chipId, int x, int y, int turns = 0) => new ChipSpec(chipId, new Cell(x, y), turns);

        private static CircuitSpec Spec(IEnumerable<RelaySpec> relays, IEnumerable<ComponentSpec> components, IEnumerable<ChipSpec> chips = null)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.AddRange(relays);
            spec.Components.AddRange(components);
            if (chips != null) spec.Chips.AddRange(chips);
            return spec;
        }

        private static LogicBoard Compile(CircuitSpec spec, BoardFactory factory = null) => (factory ?? Factory).Create(spec, null);

        private static ICondition When(string runeId) => Conditions.Create(runeId, 0);

        private static SkillDefinition Skill(string id, int windup, int damagePercent = 100) =>
            new SkillDefinition(id, id, windup, 2, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(damagePercent)) });

        private static LogicBoard Wired(params LogicRow[] rows) => new LogicBoard(rows);

        /// <summary>Ein Kampf ohne Thermal Throttling: der Gegner ist zäh und tut nichts, wenn er kein Board hat.</summary>
        private static BattleResult Fight(LogicBoard player, CombatantSetup enemy = null, int seconds = 10, int playerDamage = 10, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, playerDamage, 20, board: player), enemy ?? Fighter("B", 100000, 0, 1000), seed);
            setup.MaxTicks = Ticks.FromSeconds(seconds);
            setup.TimeLimitTicks = setup.MaxTicks + 1;
            return CombatSimulation.Run(setup);
        }

        private static CombatantSetup Enemy(int damage, params EnemyPart[] parts) =>
            new CombatantSetup { Name = "B", Stats = new CombatStats(100000, damage, 1000, 0), Board = EnemyBoard.Build(parts) };

        private static IEnumerable<BattleEvent> Of(BattleResult r, string name) => r.Events.Where(e => e.Source?.Name == name);
        private static IEnumerable<BattleEvent> Own(BattleResult r) => Of(r, "A");

        private static List<BattleEvent> Starts(BattleResult r, string skillId, string who = "A") =>
            Of(r, who).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).ToList();

        private static List<BattleEvent> Executes(BattleResult r, string skillId, string who = "A") =>
            Of(r, who).Where(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == skillId).ToList();

        private static List<BattleEvent> Kind(BattleResult r, BattleEventKind kind) => r.Events.Where(e => e.Kind == kind).ToList();

        // ------------------------------------------------------------------ Form als Daten

        [Test]
        public void EveryEffectHasExactlyOneFormDefinedInData()
        {
            CircuitEffectCatalog effects = CircuitEffectCatalog.CreateDefault();
            ModuleCatalog modules = ModuleCatalog.CreateDefault(effects);
            ChipCatalog chips = ChipCatalog.CreateDefault(null, effects);
            SkillCatalog skills = SkillCatalog.CreateDefault(effects);
            Assert.AreEqual(14, effects.All.Count);
            foreach (CircuitEffectDefinition e in effects.All)
            {
                int forms = (modules.Contains(e.Id) ? 1 : 0) + (chips.Contains(e.Id) ? 1 : 0) + (skills.Contains(e.Id) ? 1 : 0);
                Assert.AreEqual(1, forms, e.Id);
                Assert.IsNotEmpty(e.Colour, e.Id);
                Assert.IsNotEmpty(e.Icon, e.Id);
            }
            Assert.IsTrue(modules.Contains(CircuitEffectIds.Overclock));
            Assert.IsTrue(chips.Contains(CircuitEffectIds.Amplifier));
            Assert.IsTrue(skills.Contains(CircuitEffectIds.Hijack));
        }

        [Test]
        public void AnEffectWorksTheSameInAnotherForm()
        {
            // Overclock als Chip statt als Modul: der berührte Schockstich hat den Effekt und castet halb so lange.
            CircuitEffectCatalog effects = CircuitEffectCatalog.CreateDefault().WithForm(CircuitEffectIds.Overclock, CircuitEffectForm.Chip);
            var factory = new BoardFactory(null, null, null, effects: effects);
            Assert.IsFalse(ModuleCatalog.CreateDefault(effects).Contains(CircuitEffectIds.Overclock));

            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 1) }, new[] { Part(SkillIds.ShockStab, 0, 0) },
                new[] { Chip(CircuitEffectIds.Overclock, 1, 0) }), factory);
            Assert.IsTrue(board.Rows[0].Has(CircuitEffectIds.Overclock));
            Assert.AreEqual(Starts(Fight(Compile(Spec(new[] { Relay("clock", 0, 1) }, new[] { Part(SkillIds.ShockStab, 0, 0) }))), SkillIds.ShockStab)[0].Amount / 2,
                Starts(Fight(board), SkillIds.ShockStab)[0].Amount);
        }

        // ------------------------------------------------------------------ Eigene Komponenten

        [Test]
        public void OverclockHalvesCastTimeAndHeatsNeighboursUntilTheySkip()
        {
            // Schockstich mit Overclock neben der Ladungsspule (keine Pins zueinander), beide an einer eigenen Clock.
            CircuitSpec Board(bool overclock) => Spec(new[] { Relay("clock", 0, 1), Relay("clock", 1, 1) },
                new[] { overclock ? Part(SkillIds.ShockStab, 0, 0, CircuitEffectIds.Overclock) : Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.ChargeCoil, 1, 0) });
            BattleResult plain = Fight(Compile(Board(false)), seconds: 15);
            BattleResult hot = Fight(Compile(Board(true)), seconds: 15);
            Assert.AreEqual(Starts(plain, SkillIds.ShockStab)[0].Amount * (100 + Fx.OverclockCastPercent) / 100, Starts(hot, SkillIds.ShockStab)[0].Amount);

            List<int> heat = Own(hot).Where(e => e.Kind == BattleEventKind.HeatChanged && e.RowIndex == 1).Select(e => e.Amount).ToList();
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 0 }, heat.Take(6), "jede Ausführung 1 Hitze, bei 5 aussetzen");
            BattleEvent skip = Own(hot).Single(e => e.Kind == BattleEventKind.HeatSkip);
            Assert.AreEqual(1, skip.RowIndex);
            Assert.AreEqual(Fx.HeatSkipAt, skip.Amount);
            Assert.AreEqual(Starts(plain, SkillIds.ChargeCoil).Count - 1, Starts(hot, SkillIds.ChargeCoil).Count, "genau eine Ausführung fällt aus");
            Assert.IsFalse(Own(hot).Any(e => e.Kind == BattleEventKind.HeatChanged && e.RowIndex == 0), "Overclock heizt nur die Nachbarn");
        }

        [Test]
        public void InterruptJumpsToTheFrontOfTheQueue()
        {
            // Kampfbeginn reiht beide ein; ohne Interrupt käme der Schockstich (oben) zuerst, mit Interrupt die Ladungsspule.
            CircuitSpec Board(bool interrupt) => Spec(new[] { Relay("battle_start", 0, 0) },
                new[] { Part(SkillIds.ShockStab, 1, 0), interrupt ? Part(SkillIds.ChargeCoil, 0, 1, CircuitEffectIds.Interrupt) : Part(SkillIds.ChargeCoil, 0, 1) });
            string First(BattleResult r) => Own(r).First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail != SkillIds.BasicAttack).Detail;
            Assert.AreEqual(SkillIds.ShockStab, First(Fight(Compile(Board(false)))));
            BattleResult r2 = Fight(Compile(Board(true)));
            Assert.AreEqual(SkillIds.ChargeCoil, First(r2));
            Assert.IsTrue(Own(r2).Any(e => e.Kind == BattleEventKind.QueueJump && e.RowIndex == 1));
        }

        [Test]
        public void AParallelThreadRunsBesideTheCurrentExecution()
        {
            SkillDefinition slow = Skill("slow", 40), quick = Skill("quick", 4);
            BattleResult Run(bool parallel) => Fight(Wired(new LogicRow(When("battle_start"), slow),
                new LogicRow(new ClockCondition(10), parallel ? quick.WithCircuitEffect(CircuitEffectIds.ParallelThread) : quick)), seconds: 3);

            BattleResult serial = Run(false);
            Assert.Greater(Executes(serial, "quick")[0].Tick, Executes(serial, "slow")[0].Tick, "ohne: wartet in der Warteschlange");

            BattleResult r = Run(true);
            BattleEvent thread = Own(r).First(e => e.Kind == BattleEventKind.ParallelThread);
            Assert.AreEqual(10, thread.Tick);
            Assert.AreEqual(14, Executes(r, "quick")[0].Tick, "läuft sofort mit eigener Cast-Zeit");
            Assert.AreEqual(41, Executes(r, "slow")[0].Tick, "die Hauptaktion läuft ungestört weiter");
            Assert.IsFalse(Own(r).Any(e => e.Kind == BattleEventKind.ActionInterrupted && e.Detail == "slow"));
        }

        [Test]
        public void ABufferMayStandInTheQueueThreeTimes()
        {
            SkillDefinition slow = Skill("slow", 60), quick = Skill("quick", 4);
            int QueuedDuringSlow(bool buffer)
            {
                BattleResult r = Fight(Wired(new LogicRow(When("battle_start"), slow),
                    new LogicRow(new ClockCondition(10), buffer ? quick.WithCircuitEffect(CircuitEffectIds.Buffer) : quick)), seconds: 5);
                int end = Executes(r, "slow")[0].Tick;
                return Own(r).Count(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == 1 && e.Tick < end);
            }
            Assert.AreEqual(1, QueuedDuringSlow(false));
            Assert.AreEqual(Fx.BufferEntries, QueuedDuringSlow(true));
        }

        [Test]
        public void RecursionCallsItselfWithMoreEffectPerDepth()
        {
            // «HP Full» gilt, solange der Gegner nie trifft: nach jeder Ausführung ruft sich die Komponente erneut auf.
            BattleResult r = Fight(Wired(new LogicRow(When("hp_full"), Skill("rec", 6).WithCircuitEffect(CircuitEffectIds.Recursion))), seconds: 5);
            CollectionAssert.AreEqual(Enumerable.Range(1, Fx.RecursionMaxDepth), Kind(r, BattleEventKind.RecursionCall).Select(e => e.Amount));
            Assert.AreEqual(Fx.RecursionMaxDepth, Kind(r, BattleEventKind.RecursionLimit).Single().Amount);

            List<BattleEvent> starts = Starts(r, "rec");
            Assert.AreEqual(Fx.RecursionMaxDepth + 1, starts.Count, "danach endet die Rekursion");
            CollectionAssert.AreEqual(Enumerable.Range(0, Fx.RecursionMaxDepth + 1).Select(d => d * Fx.RecursionPowerPercentPerDepth), starts.Select(e => e.Power));
            List<int> damage = Own(r).Where(e => e.Kind == BattleEventKind.Damage && e.Detail == "rec").Select(e => e.Amount).ToList();
            Assert.AreEqual(10, damage[0]);
            Assert.AreEqual(10 * (100 + Fx.RecursionMaxDepth * Fx.RecursionPowerPercentPerDepth) / 100, damage.Last(), "+20 % je Tiefe");
        }

        [Test]
        public void AnAmplifierStrengthensPulsesPassingThrough()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.ShockStab, 3, 0) },
                new[] { Chip(CircuitEffectIds.Amplifier, 2, 0) }));
            PulseLink link = board.Links.Single(l => l.From.Equals(PulseNode.Component(0)));
            Assert.AreEqual(1, link.Amplifiers);
            Assert.AreEqual(2, link.Delay);

            BattleResult r = Fight(board, seconds: 2, playerDamage: 100);
            Assert.AreEqual(Fx.AmplifierPowerPercent, Own(r).First(e => e.Kind == BattleEventKind.PulseSent).Power);
            BattleEvent second = Own(r).First(e => e.Kind == BattleEventKind.ActionStarted && e.RowIndex == 1);
            Assert.AreEqual(Fx.AmplifierPowerPercent, second.Power);
            List<BattleEvent> hits = Own(r).Where(e => e.Kind == BattleEventKind.Damage && e.Detail == SkillIds.ShockStab).ToList();
            Assert.Greater(hits.First(e => e.RowIndex == 1).Amount, hits.First(e => e.RowIndex == 0).Amount);
        }

        [Test]
        public void AWatchdogTriggersTheLargestTouchingComponentWhenNothingFired()
        {
            // Watchdog berührt Schockstich (1 Zelle) und Entzünden (2 Zellen): nur Entzünden wird versorgt.
            LogicBoard board = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Ignite, 2, 1) },
                new[] { Chip(CircuitEffectIds.Watchdog, 1, 1) }));
            LogicRelay watchdog = board.Relays.Single();
            Assert.AreEqual(ChipKind.Watchdog, watchdog.Gate);
            CollectionAssert.AreEqual(new[] { 1 }, watchdog.Powered);

            BattleResult r = Fight(board, seconds: 10);
            List<BattleEvent> fires = Own(r).Where(e => e.Kind == BattleEventKind.RelayTriggered).ToList();
            Assert.AreEqual(Fx.WatchdogIdleTicks, fires[0].Tick, "nach 2 s Stillstand");
            Assert.Greater(fires.Count, 2);
            Assert.IsNotEmpty(Starts(r, SkillIds.Ignite));
            Assert.IsEmpty(Starts(r, SkillIds.ShockStab));
            int firstIgnite = Executes(r, SkillIds.Ignite)[0].Tick;
            Assert.AreEqual(firstIgnite + Fx.WatchdogIdleTicks, fires[1].Tick, "die Zeit läuft ab der letzten Ausführung");
        }

        [Test]
        public void OverflowTurnsFurtherEntriesIntoAShock()
        {
            // Kampfbeginn versorgt vier Schockstiche: drei passen in die Warteschlange, der vierte wird zum Schock.
            CircuitSpec Board(bool overflow) => Spec(new[] { Relay("battle_start", 2, 1) },
                new[] { Part(SkillIds.ShockStab, 2, 0), Part(SkillIds.ShockStab, 1, 1), Part(SkillIds.ShockStab, 3, 1), Part(SkillIds.ShockStab, 2, 2) },
                overflow ? new[] { Chip(CircuitEffectIds.Overflow, 5, 5) } : null);
            Assert.IsFalse(Kind(Fight(Compile(Board(false)), seconds: 2), BattleEventKind.OverflowShock).Any());

            LogicBoard board = Compile(Board(true));
            Assert.IsTrue(board.HasBoardEffect(CircuitEffectIds.Overflow));
            BattleResult r = Fight(board, seconds: 2);
            BattleEvent shock = Kind(r, BattleEventKind.OverflowShock).First();
            Assert.AreEqual(3, shock.RowIndex);
            Assert.AreEqual(Fx.OverflowQueueLimit, shock.Amount);
            Assert.IsTrue(Own(r).Any(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 3 && (MissReason)e.Amount == MissReason.Overflow));
            BattleEvent damage = Own(r).First(e => e.Kind == BattleEventKind.Damage && e.Detail == "overflow");
            Assert.AreEqual(10 * SkillBudgetConfig.Default.PowerPercent(1) * Fx.OverflowDamagePercent / 10000, damage.Amount, "nach Grösse");
        }

        // ------------------------------------------------------------------ Hacks

        private static LogicBoard Hacker(string hack) => Wired(new LogicRow(When("battle_start"), Skills.Get(hack)));

        [Test]
        public void JamMakesAnEnemyRelayIgnoreItsNextTriggers()
        {
            BattleResult r = Fight(Hacker(CircuitEffectIds.Jam), Enemy(1, new EnemyPart(new ClockCondition(20), "Every 1 s", Skill("zap", 4))), playerDamage: 0);
            BattleEvent hack = Kind(r, BattleEventKind.Hacked).Single();
            Assert.AreEqual(0, hack.Extra);
            CollectionAssert.AreEqual(new[] { 1, 0 }, Of(r, "B").Where(e => e.Kind == BattleEventKind.RelayJammed).Select(e => e.Amount));
            Assert.AreEqual(60, Of(r, "B").First(e => e.Kind == BattleEventKind.RelayTriggered).Tick, "die ersten beiden Takte verfallen");
        }

        [Test]
        public void BitFlipInvertsAnEnemyStateRelay()
        {
            // «HP Full» des Gegners gilt (der Ritter macht keinen Schaden): umgekehrt gilt es nicht mehr; danach löst es wieder aus.
            BattleResult r = Fight(Hacker(CircuitEffectIds.BitFlip), Enemy(1, new EnemyPart(When("hp_full"), "HP Full", Skill("guard", 4))), playerDamage: 0);
            BattleEvent hack = Kind(r, BattleEventKind.Hacked).Single();
            Assert.AreEqual(Fx.BitFlipTicks, hack.Amount);
            BattleEvent off = Of(r, "B").First(e => e.Kind == BattleEventKind.RelayState && e.Amount == 0);
            Assert.AreEqual(hack.Tick, off.Tick, "umgekehrt gilt der Zustand nicht");
            int end = hack.Tick + Fx.BitFlipTicks;
            Assert.AreEqual(end, Kind(r, BattleEventKind.FlipEnded).Single().Tick);
            CollectionAssert.AreEqual(new[] { 1, end }, Of(r, "B").Where(e => e.Kind == BattleEventKind.RelayTriggered).Select(e => e.Tick));
        }

        [Test]
        public void BitFlipCannotFlipEventRelays()
        {
            BattleResult r = Fight(Hacker(CircuitEffectIds.BitFlip), Enemy(1, new EnemyPart(new ClockCondition(20), "Every 1 s", Skill("zap", 4))), playerDamage: 0);
            Assert.IsEmpty(Kind(r, BattleEventKind.Hacked));
            Assert.AreEqual(CircuitEffectIds.BitFlip, Kind(r, BattleEventKind.HackFailed).Single().Detail);
        }

        [Test]
        public void HijackExecutesTheEnemyComponentForThePlayer()
        {
            BattleResult r = Fight(Hacker(CircuitEffectIds.Hijack), Enemy(5, new EnemyPart(new ClockCondition(40), "Every 2 s", Skill("slam", 10, 300))));
            BattleEvent hijacked = Kind(r, BattleEventKind.HijackedExecution).Single();
            Assert.AreEqual("A", hijacked.Source.Name);
            List<BattleEvent> slams = r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.Detail == "slam").ToList();
            Assert.AreEqual("B", slams[0].Target.Name, "die erste Ausführung trifft den Gegner selbst");
            Assert.AreEqual("A", slams[0].Source.Name);
            Assert.AreEqual(30, slams[0].Amount, "mit dem Waffenschaden des Ritters");
            Assert.AreEqual("A", slams[1].Target.Name, "danach wieder normal");
        }

        [Test]
        public void ShortCircuitFiresAnEnemyComponentAtItsOwnSide()
        {
            BattleResult r = Fight(Hacker(CircuitEffectIds.ShortCircuit), Enemy(5, new EnemyPart(new ClockCondition(100), "Every 5 s", Skill("slam", 10, 300))));
            BattleEvent shorted = Kind(r, BattleEventKind.ShortCircuit).Single();
            Assert.AreEqual(Executes(r, CircuitEffectIds.ShortCircuit)[0].Tick, shorted.Tick, "sofort, ohne Cast-Zeit");
            BattleEvent self = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Detail == "slam");
            Assert.AreEqual("B", self.Source.Name);
            Assert.AreEqual("B", self.Target.Name);
            Assert.AreEqual(15, self.Amount);
        }

        [Test]
        public void LatencyRaisesEnemyComputingTime()
        {
            BattleResult r = Fight(Hacker(CircuitEffectIds.Latency), Enemy(1, new EnemyPart(new ClockCondition(20), "Every 1 s", Skill("zap", 10))), playerDamage: 0);
            List<int> windups = Starts(r, "zap", "B").Select(e => e.Amount).ToList();
            Assert.AreEqual(10 * (100 + Fx.LatencyCastPercent) / 100, windups[0]);
            Assert.AreEqual(10, windups.Last(), "nach der Dauer wieder normal");
        }

        [Test]
        public void AFirewallBlocksTheNextEnemyHack()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0) },
                new[] { Chip(CircuitEffectIds.Firewall, 5, 5) }));
            Assert.AreEqual(1, board.BoardEffectCount(CircuitEffectIds.Firewall));
            BattleResult r = Fight(board, Enemy(1, new EnemyPart(new ClockCondition(20), "Every 1 s", Skills.Get(CircuitEffectIds.Jam))), seconds: 4);
            List<BattleEvent> hacks = r.Events.Where(e => e.Kind == BattleEventKind.HackBlocked || e.Kind == BattleEventKind.Hacked).ToList();
            Assert.AreEqual(BattleEventKind.HackBlocked, hacks[0].Kind);
            Assert.AreEqual(0, hacks[0].Amount, "keine Ladung mehr");
            Assert.AreEqual(BattleEventKind.Hacked, hacks[1].Kind, "der nächste Hack kommt durch");
            Assert.AreEqual("A", hacks[1].Target.Name);
        }

        [Test]
        public void SomeEnemiesHack()
        {
            var hackers = EnemyCatalog.CreateDefault().All.Where(e => e.Create().Any(s =>
                s.Board.Rows.Any(row => row.Skill.CircuitEffects.Any(id => CircuitEffectCatalog.Shared.TryGet(id, out CircuitEffectDefinition d) && d.IsHack))))
                .Select(e => e.Id).ToList();
            Assert.GreaterOrEqual(hackers.Count, 3, string.Join(", ", hackers));
        }

        // ------------------------------------------------------------------ Thermal Throttling

        [Test]
        public void ThermalThrottlingHeatsBothBoardsInSteps()
        {
            ThermalConfig thermal = ThermalConfig.Default;
            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: Wired(new LogicRow(new ClockCondition(Ticks.FromSeconds(2)), Skill("tick", 10)))),
                Fighter("B", 100000, 0, 1000));
            setup.MaxTicks = thermal.StartTicks + 3 * thermal.StepTicks;
            BattleResult r = CombatSimulation.Run(setup);

            List<BattleEvent> steps = Kind(r, BattleEventKind.Overheat);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, steps.Select(e => e.Amount));
            CollectionAssert.AreEqual(Enumerable.Range(0, 4).Select(i => thermal.StartTicks + i * thermal.StepTicks), steps.Select(e => e.Tick));

            int Windup(int tick) => Starts(r, "tick").Last(e => e.Tick < tick).Amount;
            int Hit(int tick) => Own(r).Last(e => e.Kind == BattleEventKind.Damage && e.Detail == "tick" && e.Tick < tick).Amount;
            Assert.AreEqual(10, Windup(thermal.StartTicks));
            Assert.AreEqual(10, Hit(thermal.StartTicks));
            int late = setup.MaxTicks;
            Assert.Greater(Windup(late), 10, "längere Cast-Zeit");
            Assert.Greater(Hit(late), 10, "mehr Schaden");
        }

        // ------------------------------------------------------------------ Determinismus

        [Test]
        public void TheSameSeedGivesTheSameFightWithEveryEffect()
        {
            CircuitSpec spec = Spec(new[] { Relay("clock", 0, 1), Relay("hp_full", 2, 1) },
                new[]
                {
                    Part(SkillIds.ShockStab, 0, 0, CircuitEffectIds.Overclock), Part(SkillIds.ShockStab, 1, 0, CircuitEffectIds.Recursion),
                    Part(SkillIds.ChargeCoil, 2, 0, CircuitEffectIds.Buffer), Part(CircuitEffectIds.Jam, 2, 2),
                },
                new[] { Chip(CircuitEffectIds.Amplifier, 3, 0), Chip(CircuitEffectIds.Firewall, 5, 5), Chip(CircuitEffectIds.Watchdog, 0, 2) });
            CombatantSetup Foe() => Enemy(2, new EnemyPart(new ClockCondition(30), "Every 1.5 s", Skills.Get(CircuitEffectIds.BitFlip)),
                new EnemyPart(new ClockCondition(40), "Every 2 s", Skill("slam", 10, 200)));
            string Log() => string.Join("\n", Fight(Compile(spec), Foe(), seconds: 40, seed: 4).Events.Select(e => BattleLogText.Describe(e)));
            Assert.AreEqual(Log(), Log());
        }
    }
}
