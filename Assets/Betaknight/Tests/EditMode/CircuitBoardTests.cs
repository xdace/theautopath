using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Run;
using Betaknight.Core.Skills;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// A-19: Die Platine. Komponenten mit Formen, Relais versorgen nur berührte Komponenten bis zu ihrer Grenze, keine
    /// Cooldowns, Ereignis- vs. Zustands-Auslöser, Warteschlange in Lesereihenfolge, Gegner-Platinen.
    /// </summary>
    public class CircuitBoardTests
    {
        private static readonly BoardFactory Factory = BoardFactory.CreateDefault();

        private static CircuitBoard NewBoard() => new CircuitBoard();

        private static SkillInstance Skill(string id) => new SkillInstance(id);

        private static RelaySpec Relay(string runeId, int x, int y, params ModuleSpec[] modules) =>
            new RelaySpec(runeId, new Cell(x, y), modules: modules);

        private static ComponentSpec Part(string skillId, int x, int y, bool rotated = false) => new ComponentSpec(skillId, new Cell(x, y), rotated);

        private static CircuitSpec Spec(IEnumerable<RelaySpec> relays, IEnumerable<ComponentSpec> components, bool core = false, int width = 6, int height = 6)
        {
            var spec = new CircuitSpec { Width = width, Height = height, Core = core ? new Cell(1, 1) : (Cell?)null, CoreBonusPercent = core ? 10 : 0 };
            spec.Relays.AddRange(relays);
            spec.Components.AddRange(components);
            return spec;
        }

        private static LogicBoard Compile(CircuitSpec spec) => Factory.Create(spec, null);

        /// <summary>Ritter mit viel HP gegen einen zähen Gegner, der alle <paramref name="enemyInterval"/> Ticks leicht trifft.</summary>
        private static BattleResult Fight(LogicBoard board, int enemyInterval = 1000, int seconds = 10, LogicBoard enemyBoard = null, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: board), Fighter("B", 100000, 1, enemyInterval, board: enemyBoard), seed);
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static IEnumerable<BattleEvent> Own(BattleResult r, string who = "A") => r.Events.Where(e => e.Source?.Name == who);

        private static List<BattleEvent> Starts(BattleResult r, string skillId, string who = "A") =>
            Own(r, who).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).ToList();

        private static int RelayTriggers(BattleResult r, int relay = 0) =>
            Own(r).Count(e => e.Kind == BattleEventKind.RelayTriggered && e.Relay == relay);

        // ------------------------------------------------------------------ Raster, Formen, Drehen

        [Test]
        public void TheBoardStartsAt4x3WithACoreAndGrowsTo6x6()
        {
            CircuitBoard board = NewBoard();
            Assert.AreEqual(4, board.Width);
            Assert.AreEqual(3, board.Height);
            Assert.AreEqual(new Cell(1, 1), board.Core);

            var sizes = new List<string> { $"{board.Width}×{board.Height}" };
            while (board.Expand()) sizes.Add($"{board.Width}×{board.Height}");
            CollectionAssert.AreEqual(new[] { "4×3", "4×4", "5×4", "5×5", "6×5", "6×6" }, sizes);
            Assert.IsFalse(board.CanExpand);
        }

        [Test]
        public void SkillsHaveShapesAsData()
        {
            SkillCatalog skills = SkillCatalog.CreateDefault();
            Assert.AreEqual(new Shape(1, 1), skills.Get(SkillIds.ShockStab).Shape);
            Assert.AreEqual(new Shape(1, 2), skills.Get(SkillIds.ShieldBash).Shape);
            Assert.AreEqual(new Shape(2, 1), skills.Get(SkillIds.Echo).Shape);
            Assert.AreEqual(new Shape(2, 2), skills.Get(SkillIds.ArmorBreak).Shape);
            Assert.AreEqual(new Shape(2, 3), skills.Get(SkillIds.RailCannon).Shape);
        }

        [Test]
        public void ComponentsCannotOverlapEachOtherRelaysOrTheCore()
        {
            CircuitBoard board = NewBoard();
            Assert.IsNotNull(board.Place(Skill(SkillIds.ArmorBreak), new Cell(2, 1)), "2×2 unten rechts");
            Assert.IsNull(board.Place(Skill(SkillIds.ShockStab), new Cell(3, 2)), "überlappt die 2×2");
            Assert.IsNull(board.Place(Skill(SkillIds.Echo), new Cell(0, 1)), "2×1 auf dem Kern");
            Assert.IsNull(board.Place(Skill(SkillIds.Echo), new Cell(3, 0)), "ragt rechts hinaus");
            Assert.IsNotNull(board.AddRelay(Runes.Get("on_hit"), new Cell(0, 0)));
            Assert.IsNull(board.Place(Skill(SkillIds.ShockStab), new Cell(0, 0)), "auf dem Relais");
            Assert.IsNull(board.AddRelay(Runes.Get("when_hit"), new Cell(1, 1)), "Relais auf dem Kern");
            Assert.IsNotNull(board.Place(Skill(SkillIds.ShockStab), new Cell(1, 0)));
            Assert.AreEqual(2, board.Components.Count);
        }

        [Test]
        public void RightClickRotatesWhenThereIsRoom()
        {
            CircuitBoard board = NewBoard();
            ComponentSlot bash = board.Place(Skill(SkillIds.ShieldBash), new Cell(0, 0));
            Assert.AreEqual(new Shape(1, 2), bash.Shape);

            Assert.IsTrue(board.Rotate(bash));
            Assert.AreEqual(new Shape(2, 1), bash.Shape);
            Assert.IsTrue(bash.Rotated);

            Assert.IsTrue(board.Rotate(bash), "zurück auf 1×2");
            board.Place(Skill(SkillIds.ShockStab), new Cell(1, 0));
            Assert.IsFalse(board.Rotate(bash), "kein Platz für 2×1");
            Assert.AreEqual(new Shape(1, 2), bash.Shape);
        }

        [Test]
        public void MovingKeepsTheComponentAndChecksRoom()
        {
            CircuitBoard board = NewBoard();
            SkillInstance stab = Skill(SkillIds.ShockStab);
            ComponentSlot slot = board.Place(stab, new Cell(0, 0));
            Assert.IsTrue(board.Move(slot, new Cell(3, 2), false));
            Assert.AreSame(slot, board.ComponentOf(stab));
            Assert.IsFalse(board.Move(slot, new Cell(1, 1), false), "Kern");

            // Erneutes Legen desselben Exemplars verschiebt es statt es zu verdoppeln.
            board.Place(stab, new Cell(2, 0));
            Assert.AreEqual(1, board.Components.Count);
            Assert.AreEqual(new Cell(2, 0), slot.Origin);
        }

        [Test]
        public void ComponentsAndRelaysAreListedInReadingOrder()
        {
            CircuitBoard board = NewBoard();
            board.Place(Skill(SkillIds.ShockStab), new Cell(3, 2));
            board.Place(Skill(SkillIds.Thrusters), new Cell(0, 2));
            board.Place(Skill(SkillIds.ChargeCoil), new Cell(2, 0));
            CollectionAssert.AreEqual(new[] { SkillIds.ChargeCoil, SkillIds.Thrusters, SkillIds.ShockStab },
                board.Components.Select(c => c.Skill.SkillId).ToList());
        }

        // ------------------------------------------------------------------ Versorgung

        [Test]
        public void ARelayPowersOnlyTouchingComponentsWithinItsSizeLimit()
        {
            // «On Hit» ist ◆ (Grenze 2 Zellen). Rechts ein 1×1 (versorgt), darunter ein 2×2 (zu gross), diagonal ein 1×1 (berührt nicht).
            LogicBoard board = Compile(Spec(
                new[] { Relay("on_hit", 0, 0) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.ArmorBreak, 0, 1), Part(SkillIds.Thrusters, 1, 1) }));

            LogicRelay relay = board.Relays.Single();
            Assert.AreEqual(2, relay.MaxCells);
            LogicRow stab = board.Rows.Single(r => r.Skill.Id == SkillIds.ShockStab);
            LogicRow breaker = board.Rows.Single(r => r.Skill.Id == SkillIds.ArmorBreak);
            LogicRow thrusters = board.Rows.Single(r => r.Skill.Id == SkillIds.Thrusters);

            Assert.IsTrue(stab.IsPowered);
            Assert.IsFalse(breaker.IsPowered);
            CollectionAssert.Contains(breaker.TooLargeFor, relay, "berührt, aber zu gross");
            Assert.IsFalse(thrusters.IsPowered, "nur an der Ecke");
            Assert.IsEmpty(thrusters.TooLargeFor);
        }

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 6)]
        public void TheSizeLimitFollowsTheDifficulty(int tier, int cells)
        {
            Assert.AreEqual(cells, DifficultyBonusConfig.Default.MaxCells(tier));
        }

        [Test]
        public void AnUnpoweredComponentNeverFires()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) }, new[] { Part(SkillIds.ShockStab, 3, 3) }));
            BattleResult r = Fight(board);
            Assert.IsEmpty(Starts(r, SkillIds.ShockStab));
            Assert.Greater(Starts(r, SkillIds.BasicAttack).Count, 0, "der Basisangriff füllt die Lücken");
        }

        [Test]
        public void ATooLargeComponentNeverFiresAndCountsAsMissed()
        {
            // «Battle Start» ist ohne Raute: Grenze 1 Zelle, der 2×2 ist zu gross.
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) }, new[] { Part(SkillIds.ArmorBreak, 1, 0) }));
            BattleResult r = Fight(board);
            Assert.IsEmpty(Starts(r, SkillIds.ArmorBreak));
            BattleEvent missed = Own(r).Single(e => e.Kind == BattleEventKind.TriggerMissed);
            Assert.AreEqual((int)MissReason.TooLarge, missed.Amount);
            Assert.AreEqual(ArenaTexts.NotPoweredTooLarge, "not powered (too large)");
        }

        [Test]
        public void TheCoreBoostsTouchingComponents()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("on_hit", 0, 0) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 0, 1), Part(SkillIds.ChargeCoil, 3, 3) }, core: true));
            LogicRow stab = board.Rows.Single(r => r.Skill.Id == SkillIds.ShockStab);
            LogicRow coil = board.Rows.Single(r => r.Skill.Id == SkillIds.ChargeCoil);
            Assert.IsTrue(stab.TouchesCore);
            Assert.AreEqual(10, stab.Skill.PowerBonusPercent);
            Assert.IsFalse(coil.TouchesCore);
            Assert.AreEqual(0, coil.Skill.PowerBonusPercent);
        }

        // ------------------------------------------------------------------ Auslösen

        [Test]
        public void AnEventRelayTriggersOnEveryEvent()
        {
            // «When Hit»: jeder Treffer des Gegners löst aus (alle 1 s).
            LogicBoard board = Compile(Spec(new[] { Relay("when_hit", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board, enemyInterval: 20);
            int hits = r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Source?.Name == "B");
            Assert.Greater(hits, 5);
            Assert.That(RelayTriggers(r), Is.InRange(hits - 1, hits));
        }

        [Test]
        public void OnHitIgnoresHitsOfItsOwnComponents()
        {
            // Schockstich an «On Hit»: nur Basisangriffe lösen aus, die eigenen Treffer halten ihn nicht endlos in der Warteschlange.
            LogicBoard board = Compile(Spec(new[] { Relay("on_hit", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board);
            int basicHits = Own(r).Count(e => e.Kind == BattleEventKind.Hit && e.Detail == SkillDefinition.BasicAttackId);
            Assert.Greater(basicHits, 3);
            Assert.That(RelayTriggers(r), Is.InRange(basicHits - 1, basicHits));
            Assert.Greater(Starts(r, SkillDefinition.BasicAttackId).Count, Starts(r, SkillIds.ShockStab).Count / 2,
                "Der Basisangriff kommt weiter zum Zug");
        }

        [Test]
        public void AStateRelayTriggersOnlyOnTheRisingEdge()
        {
            // «HP Full» bleibt den ganzen Kampf wahr (der Gegner trifft nie): genau ein Auslösen.
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board);
            Assert.AreEqual(1, RelayTriggers(r));
            Assert.AreEqual(1, Starts(r, SkillIds.ShockStab).Count);
        }

        [Test]
        public void RepeatWhileTrueQueuesAgainWhileTheStateHolds()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0, new ModuleSpec(ModuleIds.RepeatWhileTrue)) },
                new[] { Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board, seconds: 5);
            Assert.AreEqual(1, RelayTriggers(r), "das Relais löst nur einmal aus");
            Assert.Greater(Starts(r, SkillIds.ShockStab).Count, 5, "die Komponente läuft weiter, solange die Bedingung gilt");
        }

        [Test]
        public void TheClockRelayTicksEveryInterval()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board, seconds: 10);
            List<int> ticks = Own(r).Where(e => e.Kind == BattleEventKind.RelayTriggered).Select(e => e.Tick).ToList();
            Assert.AreEqual(Ticks.FromSeconds(2), ticks[0], "erstes Ticken nach 2 s");
            Assert.AreEqual(5, ticks.Count(t => t <= Ticks.FromSeconds(10)), "Clock 2 s über 10 s");
            for (int i = 1; i < ticks.Count; i++) Assert.AreEqual(Ticks.FromSeconds(2), ticks[i] - ticks[i - 1]);
        }

        [Test]
        public void ThereIsNoAlwaysRuneAnymore()
        {
            Assert.IsFalse(Runes.TryGet("always", out _));
            Assert.IsTrue(Runes.TryGet("clock", out _));
            Assert.AreEqual(0, Runes.Get("clock").Difficulty);
        }

        // ------------------------------------------------------------------ Warteschlange

        [Test]
        public void TheQueueRunsInReadingOrder()
        {
            // Ein Relais versorgt zwei Komponenten: die obere links startet zuerst, egal in welcher Reihenfolge sie gebaut wurden.
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) },
                new[] { Part(SkillIds.Thrusters, 0, 1), Part(SkillIds.ShockStab, 1, 0) }));
            BattleResult r = Fight(board, seconds: 3);
            List<string> started = Own(r).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail != SkillIds.BasicAttack)
                .Select(e => e.Detail).ToList();
            CollectionAssert.AreEqual(new[] { SkillIds.ShockStab, SkillIds.Thrusters }, started);
            Assert.AreEqual(0, board.Rows.Single(x => x.Skill.Id == SkillIds.ShockStab).Index);
        }

        [Test]
        public void AComponentStillQueuedMissesTheNextTrigger()
        {
            // Lange Cast-Zeit, Relais löst jede Sekunde aus: einige Auslöser treffen eine Komponente, die schon wartet.
            var slow = new SkillDefinition("slow", "Slow", Ticks.FromSeconds(3), 0, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) });
            var board = new LogicBoard(new[] { new LogicRow(new ClockCondition(Ticks.PerSecond), slow, "clock") });
            BattleResult r = Fight(board, seconds: 10);
            Assert.IsTrue(Own(r).Any(e => e.Kind == BattleEventKind.TriggerMissed && e.Amount == (int)MissReason.AlreadyQueued));
        }

        // ------------------------------------------------------------------ Freeze, Haste, Slow

        [Test]
        public void FreezeStopsTheLargestEnemyComponentFromFiring()
        {
            // «On Hit» ◆ versorgt die 2×1-Granate.
            LogicBoard knight = Compile(Spec(new[] { Relay("on_hit", 0, 0) }, new[] { Part(SkillIds.CryoGrenade, 1, 0) }));
            var hammer = new SkillDefinition("hammer", "Hammer", 4, 0, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) },
                shape: new Shape(2, 2));
            LogicBoard enemy = EnemyBoard.Build(new EnemyPart(new ClockCondition(Ticks.PerSecond / 2), "Every 0.5 s", hammer));
            BattleResult r = Fight(knight, enemyBoard: enemy, seconds: 6);

            BattleEvent frozen = r.Events.First(e => e.Kind == BattleEventKind.Frozen);
            Assert.AreEqual(Ticks.FromSeconds(3), frozen.Amount);
            List<BattleEvent> misses = Own(r, "B").Where(e => e.Kind == BattleEventKind.TriggerMissed && e.Amount == (int)MissReason.Frozen).ToList();
            Assert.Greater(misses.Count, 0);
            Assert.IsTrue(misses.Any(e => e.Tick >= frozen.Tick && e.Tick < frozen.Tick + frozen.Amount));
            Assert.IsFalse(Starts(r, "hammer", "B").Any(e => e.Tick > frozen.Tick && e.Tick < frozen.Tick + frozen.Amount));
        }

        [Test]
        public void HasteAndSlowChangeTheCastTime()
        {
            var cast = new SkillDefinition("c", "C", 20, 0, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) });
            var board = new LogicBoard(new[] { new LogicRow(new ClockCondition(Ticks.PerSecond * 2), cast, "clock") });
            CombatantSetup a = Fighter("A", 100000, 10, 20, board: board);
            a.Stats[StatKind.CastPercent] = -50;
            BattleSetup setup = Duel(a, Fighter("B", 100000, 1, 1000));
            setup.TimeLimitTicks = Ticks.FromSeconds(3);
            BattleResult hasted = CombatSimulation.Run(setup);
            Assert.AreEqual(10, Starts(hasted, "c")[0].Amount, "−50 % Cast-Zeit");

            a = Fighter("A", 100000, 10, 20, board: new LogicBoard(new[] { new LogicRow(new ClockCondition(Ticks.PerSecond * 2), cast, "clock") }));
            a.Stats[StatKind.CastPercent] = 30;
            setup = Duel(a, Fighter("B", 100000, 1, 1000));
            setup.TimeLimitTicks = Ticks.FromSeconds(3);
            Assert.AreEqual(26, Starts(CombatSimulation.Run(setup), "c")[0].Amount, "+30 % Cast-Zeit (Slow)");
        }

        // ------------------------------------------------------------------ Bonus und Budget

        [Test]
        public void TheDifficultyBonusHasNoCooldownPart()
        {
            DifficultyBonusConfig c = DifficultyBonusConfig.Default;
            Assert.AreEqual(15, c[1].PowerPercent);
            Assert.AreEqual(0, c[1].CastReductionPercent);
            Assert.AreEqual(30, c[2].PowerPercent);
            Assert.AreEqual(20, c[2].CastReductionPercent);
            Assert.AreEqual(60, c[3].PowerPercent);
            Assert.AreEqual(35, c[3].CastReductionPercent);
            Assert.AreEqual(Ticks.PerSecond, c[3].ExtraStatusTicks);
        }

        [TestCase(1, 60)]
        [TestCase(2, 150)]
        [TestCase(4, 350)]
        [TestCase(6, 600)]
        public void SkillPowerGrowsWithSize(int cells, int percent)
        {
            Assert.AreEqual(percent, SkillBudgetConfig.Default.PowerPercent(cells));
        }

        [Test]
        public void DamageSkillsMatchTheBudgetOfTheirSize()
        {
            SkillBudgetConfig budget = SkillBudgetConfig.Default;
            foreach (SkillDefinition s in SkillCatalog.CreateDefault().All.Where(s => !s.IsEvolution && SkillBudgetConfig.IsDamageSkill(s)))
                Assert.IsTrue(budget.IsWithinBudget(s), budget.Explain(s));
        }

        [Test]
        public void EvolutionsKeepTheShapeOfTheirBaseSkill()
        {
            SkillCatalog skills = SkillCatalog.CreateDefault();
            foreach (Betaknight.Core.Evolution.EvolutionRecipe r in Betaknight.Core.Evolution.EvolutionCatalog.CreateDefault().All
                         .Where(r => r.Subject == Betaknight.Core.Evolution.EvolutionSubject.Skill))
                Assert.AreEqual(skills.Get(r.FromId).Shape, skills.Get(r.ToId).Shape, r.Id);
        }

        // ------------------------------------------------------------------ Keine Cooldowns

        [Test]
        public void NothingInTheCoreHasACooldown()
        {
            Assembly core = typeof(Battle).Assembly;
            var found = new List<string>();
            foreach (System.Type t in core.GetTypes().Where(t => t.Namespace != null && t.Namespace.StartsWith("Betaknight.Core")))
            {
                if (t.Name.IndexOf("Cooldown", System.StringComparison.OrdinalIgnoreCase) >= 0) found.Add(t.FullName);
                foreach (MemberInfo m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    if (m.Name.IndexOf("Cooldown", System.StringComparison.OrdinalIgnoreCase) >= 0) found.Add($"{t.Name}.{m.Name}");
            }
            Assert.IsEmpty(found, string.Join(", ", found));
        }

        [Test]
        public void NoCatalogTextMentionsACooldown()
        {
            var texts = new List<string>();
            texts.AddRange(SkillCatalog.CreateDefault().All.Select(s => s.Description));
            texts.AddRange(Runes.All.Select(r => r.Description));
            texts.AddRange(ModuleCatalog.CreateDefault().All.SelectMany(m => m.Descriptions));
            texts.AddRange(EquipmentCatalog.CreateDefault().All.SelectMany(i => i.Passives.Select(p => p.Text)));
            texts.AddRange(SetBonusRegistry.CreateDefault().All.SelectMany(s => s.Bonuses.Values));
            texts.AddRange(SynergyRegistry.CreateDefault().Tags.SelectMany(t => t.Tiers.Values.Select(s => s.Text)));
            texts.AddRange(SynergyRegistry.CreateDefault().Duos.Select(d => d.Effect.Text));
            List<string> bad = texts.Where(t => t != null && t.IndexOf("cooldown", System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        // ------------------------------------------------------------------ Gegner-Platinen

        [Test]
        public void EnemyBoardsAreReadableData()
        {
            foreach (EnemyDefinition enemy in EnemyCatalog.CreateDefault().All)
            foreach (CombatantSetup fighter in enemy.Create())
            {
                LogicBoard board = fighter.Board ?? LogicBoard.FallbackOnly;
                Assert.IsTrue(board.Rows.All(r => r.IsPowered), enemy.Id);
                Assert.IsFalse(board.Relays.Any(r => r.Condition is AlwaysCondition), enemy.Id + ": kein «Always» mehr");
                IReadOnlyList<string> lines = EnemyBoard.Lines(board);
                Assert.IsNotEmpty(lines, enemy.Id);
                foreach (LogicRow row in board.Rows)
                {
                    Assert.IsNotNull(board.Layout, enemy.Id);
                    Assert.IsTrue(row.Rect.Value.FitsIn(board.Layout.Width, board.Layout.Height), enemy.Id);
                    Assert.IsTrue(lines.Any(l => l.Contains(row.Skill.Name)), $"{enemy.Id}: {row.Skill.Name}");
                }
            }
        }

        [Test]
        public void EnemyClockRelaysReplaceAlwaysWithCooldown()
        {
            CombatantSetup warden = EnemyCatalog.CreateDefault().All.Single(e => e.Id == "rust_warden").Create()[0];
            LogicRelay relay = warden.Board.Relays.Single();
            Assert.IsInstanceOf<ClockCondition>(relay.Condition);
            Assert.AreEqual("Every 7 s", relay.Label);
            StringAssert.StartsWith("Every 7 s → Ram (2×1)", EnemyBoard.Lines(warden.Board)[0]);
        }

        // ------------------------------------------------------------------ Determinismus

        [Test]
        public void TheSameBoardAndSeedGiveTheSameFight()
        {
            string Log()
            {
                LogicBoard board = Compile(Spec(new[] { Relay("when_hit", 0, 0), Relay("clock", 2, 2) },
                    new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.ShieldBash, 2, 0, rotated: true), Part(SkillIds.Thrusters, 2, 3) }, core: true));
                return string.Join("\n", Fight(board, enemyInterval: 17, seed: 5).Events);
            }

            Assert.AreEqual(Log(), Log());
        }

        // ------------------------------------------------------------------ Session

        private static OverworldSession Session(KnightKit kit = null) =>
            OverworldSession.Create(new MapGenerationConfig { Radius = 5, Seed = 7 }, kit: kit ?? KnightKit.Defaults[0]);

        [Test]
        public void EveryKitStartsWithAPoweredComponentAtTheCore()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = Session(kit);
                Assert.AreEqual(1, s.Board.Relays.Count, kit.Id);
                Assert.AreEqual(new Cell(0, 0), s.Board.Relays[0].Position, kit.Id);
                ComponentSlot first = s.Board.Components.Single();
                Assert.AreEqual(kit.StartSkillId, first.Skill.SkillId, kit.Id);
                Assert.IsTrue(s.IsPowered(first), kit.Id);
                Assert.IsTrue(s.Board.TouchesCore(first), kit.Id);
                Assert.IsTrue(s.Skills.Contains(first.Skill), kit.Id);
            }
        }

        [Test]
        public void TheSessionPlacesMovesRotatesAndRemovesComponents()
        {
            OverworldSession s = Session(KnightKit.Defaults[1]);
            SkillInstance wall = s.Skills.All.First(k => k.SkillId == SkillIds.ShieldWall);
            Assert.IsFalse(s.PlaceSkill(wall.InstanceId, new Cell(1, 1)), "Kern");
            Assert.IsTrue(s.PlaceSkill(wall.InstanceId, new Cell(3, 0)));
            int index = s.Board.IndexOf(s.Board.ComponentOf(wall));
            Assert.IsFalse(s.RotateComponent(index), "2×1 passt nicht an den rechten Rand");
            Assert.IsTrue(s.MoveComponent(index, new Cell(2, 2), true));
            Assert.AreEqual(new Shape(2, 1), s.Board.ComponentOf(wall).Shape);
            index = s.Board.IndexOf(s.Board.ComponentOf(wall));
            Assert.IsTrue(s.RemoveComponent(index));
            Assert.IsTrue(wall.IsFree);
            Assert.IsNull(s.Board.ComponentOf(wall));
        }

        [Test]
        public void TakingASkillThroughTheCollectionRemovesItsComponent()
        {
            OverworldSession s = Session();
            ComponentSlot first = s.Board.Components.Single();
            SkillInstance skill = first.Skill;
            s.Skills.TakeFrom(first);
            Assert.IsEmpty(s.Board.Components);
            Assert.IsTrue(skill.IsFree);
        }

        [Test]
        public void ATooLargeComponentShowsAsNotPowered()
        {
            OverworldSession s = Session(KnightKit.Defaults[0]); // «On Hit» ◆: Grenze 2 Zellen
            SkillInstance breaker = s.Skills.All.First(k => k.SkillId == SkillIds.ArmorBreak);
            Assert.IsTrue(s.MoveRelay(0, new Cell(2, 0)));
            Assert.IsTrue(s.PlaceSkill(breaker.InstanceId, new Cell(2, 1)), "2×2 unter dem Relais");
            ComponentSlot slot = s.Board.ComponentOf(breaker);
            Assert.IsFalse(s.IsPowered(slot));
            Assert.IsTrue(s.IsTooLarge(slot));

            LogicBoard compiled = s.CompileBoard();
            LogicRow row = compiled.Rows.Single(x => x.Skill.Id == SkillIds.ArmorBreak);
            Assert.IsFalse(row.IsPowered);
            Assert.AreEqual(1, row.TooLargeFor.Count);
        }

        [Test]
        public void TriggerTargetsFollowTheComponentWhenItMoves()
        {
            OverworldSession s = Session(KnightKit.Defaults[1]);
            SkillInstance bash = s.Board.Components.Single().Skill;
            SkillInstance wall = s.Skills.All.First(k => k.SkillId == SkillIds.ShieldWall);
            s.PlaceSkill(wall.InstanceId, new Cell(0, 2), true);
            ModuleInstance trigger = s.GainModule(ModuleIds.Trigger, SkillDuplicateChoice.KeepCopy);
            Assert.IsTrue(s.PlaceModuleOnSkill(trigger.InstanceId, bash.InstanceId));
            Assert.IsTrue(s.SetTriggerTarget(trigger.InstanceId, ModuleTarget.Skill(wall.InstanceId)));

            int before = CircuitBoardSpec.TargetComponent(s.Board, trigger.Target);
            Assert.IsTrue(s.PlaceSkill(wall.InstanceId, new Cell(3, 0), false) || s.PlaceSkill(wall.InstanceId, new Cell(2, 2), true));
            int after = CircuitBoardSpec.TargetComponent(s.Board, trigger.Target);
            Assert.GreaterOrEqual(before, 0);
            Assert.AreSame(wall, s.Board.Components[after].Skill);
            Assert.AreEqual(1, s.TriggerLinks().Count);
        }

        [Test]
        public void ExpansionsGrowTheBoardUpToItsMaximum()
        {
            OverworldSession s = Session();
            Assert.AreEqual("4×3", s.BoardSize);
            Assert.IsTrue(s.ExpandBoard(10));
            Assert.AreEqual("6×6", s.BoardSize);
            Assert.AreEqual(s.MaxBoardSize, s.BoardSize);
            Assert.IsFalse(s.CanExpandBoard);
        }

        private static Betaknight.Core.Runes.RuneCatalog Runes => Betaknight.Core.Runes.RuneCatalog.CreateDefault();
    }
}
