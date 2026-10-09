using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Autoplay;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>A-12, A-19: Skills sind der Hauptschaden, ihre Wirkung folgt der Grösse; der Basisangriff füllt die Lücken.</summary>
    public class SkillBudgetTests
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly SkillBudgetConfig Budget = SkillBudgetConfig.Default;

        private static BattleResult Run(BattleSetup setup, int seconds)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        // ------------------------------------------------------------------ Budget

        [TestCase(1, 60)]
        [TestCase(2, 150)]
        [TestCase(4, 350)]
        [TestCase(6, 600)]
        [TestCase(3, 250)]
        [TestCase(5, 475)]
        [TestCase(7, 725)]
        public void PowerFollowsTheSizeTable(int cells, int percent)
        {
            // Werte der Tabelle direkt, Lücken linear, über 6 Zellen mit dem Schritt der letzten beiden Einträge.
            Assert.AreEqual(percent, Budget.PowerPercent(cells));
        }

        [Test]
        public void EveryDamageSkillMeetsTheBudgetOfItsSize()
        {
            List<SkillDefinition> damage = Skills.All.Where(SkillBudgetConfig.IsDamageSkill).ToList();
            Assert.GreaterOrEqual(damage.Count, 6, string.Join(", ", damage.Select(s => s.Name)));
            foreach (SkillDefinition skill in damage)
            {
                Assert.GreaterOrEqual(SkillBudgetConfig.DamagePerTargetBp(skill) * 100, Budget.RequiredDamageBp(skill) * Budget.MinOfBudgetPercent,
                    Budget.Explain(skill));
                if (!skill.IsEvolution) Assert.IsTrue(Budget.IsWithinBudget(skill), Budget.Explain(skill));
            }
        }

        [Test]
        public void UtilitySkillsDealLittleDamage()
        {
            foreach (SkillDefinition skill in Skills.All.Where(s => !s.IsBasicAttack && !SkillBudgetConfig.IsDamageSkill(s)))
            {
                int budget = Budget.RequiredDamageBp(skill);
                Assert.LessOrEqual(SkillBudgetConfig.DamagePerTargetBp(skill), budget * Budget.UtilityMaxShareOfBudgetPercent / 100, Budget.Explain(skill));
            }
            Assert.IsFalse(SkillBudgetConfig.IsDamageSkill(Skills.Get(SkillIds.ShieldBash)), "Betäubung ist Nutzen");
            Assert.IsFalse(SkillBudgetConfig.IsDamageSkill(Skills.Get(SkillIds.Flashbang)), "Blendung ist Nutzen");
        }

        [Test]
        public void TheBudgetIsTheRuleFromTheConfig()
        {
            // Bohrstoß: Fläche, 2×2 = 4 Zellen: 350 % × 60 % pro Ziel.
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            Assert.IsTrue(SkillBudgetConfig.IsArea(drill));
            Assert.AreEqual(4, drill.Shape.Cells);
            Assert.AreEqual(BasisPoints.Percent(210), Budget.RequiredDamageBp(drill));
            StringAssert.Contains("2×2", Budget.Explain(drill));

            // Grössere Komponente verlangt mehr Wirkung; die Cast-Zeit zählt nicht mehr.
            var small = new SkillDefinition("q", "q", 10, 0, new ISkillEffect[] { new DamageEffect(1) }, kinds: SkillKind.Attack, shape: new Shape(1, 1));
            var large = new SkillDefinition("s", "s", 10, 0, new ISkillEffect[] { new DamageEffect(1) }, kinds: SkillKind.Attack, shape: new Shape(2, 2));
            var slowSmall = new SkillDefinition("t", "t", 60, 0, new ISkillEffect[] { new DamageEffect(1) }, kinds: SkillKind.Attack, shape: new Shape(1, 1));
            Assert.Greater(Budget.RequiredDamageBp(large), Budget.RequiredDamageBp(small));
            Assert.AreEqual(Budget.RequiredDamageBp(small), Budget.RequiredDamageBp(slowSmall));

            // Strengere Konfiguration: der Katalog fällt durch.
            var strict = new SkillBudgetConfig { PowerPercentByCells = new Dictionary<int, int> { { 1, 600 }, { 2, 1500 }, { 4, 3500 }, { 6, 6000 } } };
            Assert.IsTrue(Skills.All.Where(SkillBudgetConfig.IsDamageSkill).Any(s => !strict.IsWithinBudget(s)));
        }

        // ------------------------------------------------------------------ Basisangriff

        [Test]
        public void TheKnightsBasicAttackDealsSixtyPercent()
        {
            SkillDefinition basic = Skills.Get(SkillIds.BasicAttack);
            Assert.AreEqual(BasisPoints.Percent(60), ((DamageEffect)basic.Effects[0]).DamageBp);
            Assert.AreSame(basic.GetType(), SkillDefinition.BasicAttack.GetType());
            Assert.AreEqual(BasisPoints.Full, ((DamageEffect)SkillDefinition.BasicAttack.Effects[0]).DamageBp, "Gegner behalten 100 %");

            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 10, 20), null, null);
            Assert.AreEqual(BasisPoints.Percent(60), ((DamageEffect)knight.Board.Fallback.Skill.Effects[0]).DamageBp);
            BattleResult r = Run(Duel(knight, Fighter("B", 100000, 0, 1000)), 2);
            Assert.AreEqual(6, r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "Ritter").Amount);
        }

        [Test]
        public void TheBasicAttackFillsTheGapsAndYieldsToQueuedComponents()
        {
            // Früher kürzten Treffer des Basisangriffs laufende Cooldowns. Jetzt füllt er nur die Lücken der Warteschlange:
            // zwischen zwei Rüstungsbrüchen (Clock 3 s) trifft er, und eine eingereihte Komponente wartet höchstens seine Erholung ab.
            SkillDefinition basic = SkillDefinition.CreateBasicAttack(BasisPoints.Percent(60));
            var board = new LogicBoard(new[] { new LogicRow(new ClockCondition(Ticks.FromSeconds(3)), Skills.Get(SkillIds.ArmorBreak), "Clock") }, basic);
            BattleResult r = Run(Duel(Fighter("A", 1000, 10, 20, board: board), Fighter("B", 100000, 0, 1000)), 12);

            List<BattleEvent> starts = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak).ToList();
            Assert.GreaterOrEqual(starts.Count, 3);
            Assert.AreEqual(Ticks.FromSeconds(3), starts[1].Tick - starts[0].Tick, "keine Verzögerung über die Cast-Zeit hinaus");
            int hits = r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Detail == SkillIds.BasicAttack && e.Tick > starts[0].Tick && e.Tick < starts[1].Tick);
            Assert.Greater(hits, 0, "der Basisangriff füllt die Lücke");
            Battle.ActionTiming(basic, 20, out _, out int recovery);
            Assert.IsTrue(starts.All(e => e.FromQueue && e.QueuedTicks <= recovery), string.Join(", ", starts.Select(e => e.QueuedTicks)));
        }

        // ------------------------------------------------------------------ Anteil im Kampf

        [Test]
        public void WithThreeDamageSkillsTheBasicAttackDealsAtMostThirtyFivePercent()
        {
            // «Enemy Stunned» ●● → Rüstungsbruch (2×2), «Every 20 s» ●●● → Bohrstoß (2×2), «On Hit» ● → Schockstoss.
            // Die grossen Komponenten liegen oben (Vorrang in Lesereihenfolge), sonst verdrängt der Schockstoss sie:
            // er löst sein eigenes «On Hit» immer wieder aus.
            var circuit = new CircuitSpec { Width = 6, Height = 6 };
            circuit.Relays.AddRange(new[]
            {
                new RelaySpec("enemy_stunned", new Cell(0, 0)),
                new RelaySpec("every_20s", new Cell(0, 2)),
                new RelaySpec("on_hit", new Cell(0, 4)),
            });
            circuit.Components.AddRange(new[]
            {
                new ComponentSpec(SkillIds.ArmorBreak, new Cell(1, 0)),
                new ComponentSpec(SkillIds.Drill, new Cell(1, 2)),
                new ComponentSpec(SkillIds.ShockStab, new Cell(1, 4)),
            });
            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 10, 20), null, circuit);
            Assert.IsTrue(knight.Board.Rows.All(row => row.IsPowered));
            BattleResult r = Run(Duel(knight, Fighter("Sandsack", 100000, 0, 1000)), 30);
            BattleReport report = BattleReport.Create(r);

            Assert.Greater(report.TotalDamage, 0);
            Assert.IsTrue(report.Rows.Where(row => !row.IsFallback).All(row => row.Fired > 0), "alle drei Komponenten feuern");
            Assert.LessOrEqual(report.BasicAttackShareBp, BasisPoints.Percent(35), report.DamageSplitText);
            StringAssert.StartsWith("Basic Attack ", report.DamageSplitText);
            StringAssert.Contains(" · Skills ", report.DamageSplitText);
        }

        [Test]
        public void EveryStartKitHasADamagingComponentPoweredByItsStartRelay()
        {
            BoardFactory factory = BoardFactory.CreateDefault();
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                SkillDefinition skill = Skills.Get(kit.StartSkillId);
                Assert.Greater(SkillBudgetConfig.DamagePerTargetBp(skill), 0, $"{kit.Name}: {kit.StartSkillId}");
                Assert.LessOrEqual(skill.Shape.Cells, factory.MaxCellsOf(new RelaySpec(kit.StartRuneId, new Cell(0, 0))), $"{kit.Name}: passt in die Grenze");
            }
            Assert.AreEqual(SkillIds.ShieldBash, KnightKit.Defaults.Single(k => k.Id == "shield").StartSkillId);
        }

        [Test]
        public void EveryStartKitBeatsTheEasiestEnemy()
        {
            EnemyDefinition rat = EnemyCatalog.CreateDefault().All.Single(e => e.Id == "scrap_rat");
            var resolver = new ArenaCombatResolver();
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: kit);
                for (int seed = 1; seed <= 10; seed++)
                {
                    BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 0, s.Stats, s.Board, s.Gear), rat.Create(), seed);
                    BattleResult r = CombatSimulation.Run(setup);
                    Assert.IsTrue(r.IsVictory, $"{kit.Name}, Seed {seed}: {r.Outcome}");
                    Assert.Greater(r.PlayerHp * 2, r.PlayerMaxHp, $"{kit.Name}, Seed {seed}: gut schaffbar, mehr als halbe HP übrig");
                }
            }
        }

        [Test]
        public void EveryStartKitCanSurviveTheBoss()
        {
            var resolver = new ArenaCombatResolver();
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: kit);
                int survived = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    CombatResult r = resolver.Resolve(new CombatRequest(CellContent.Boss, 0, s.Stats, s.Board, s.Gear), new System.Random(seed));
                    if (r.Victory) survived++;
                }
                Assert.GreaterOrEqual(survived, 4, $"{kit.Name} überlebt den Boss bis zum Portal");
            }
        }

        [Test]
        public void TheBotReportsTheBasicAttackShare()
        {
            AutoplayReport report = HeadlessAutoplay.Run(1, targetAct: 2);
            Assert.Greater(report.TotalDamage, 0);
            Assert.IsTrue(report.BasicAttackSharePercent.HasValue);
            Assert.That(report.BasicAttackSharePercent.Value, Is.InRange(0.0, 100.0));
            StringAssert.Contains("\"basicAttackSharePercent\"", report.ToJson());
            StringAssert.Contains("Basic Attack ", report.Summary());
        }
    }
}
