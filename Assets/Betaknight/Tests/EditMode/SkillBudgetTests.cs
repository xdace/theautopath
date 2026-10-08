using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Autoplay;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>A-12: Skills sind der Hauptschaden, der Basisangriff ist Füller und Motor.</summary>
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

        [Test]
        public void EveryDamageSkillMeetsTheBudget()
        {
            List<SkillDefinition> damage = Skills.All.Where(SkillBudgetConfig.IsDamageSkill).ToList();
            Assert.GreaterOrEqual(damage.Count, 6, string.Join(", ", damage.Select(s => s.Name)));
            foreach (SkillDefinition skill in damage)
                Assert.GreaterOrEqual(SkillBudgetConfig.DamagePerTargetBp(skill), Budget.RequiredDamageBp(skill), Budget.Explain(skill));
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
            // Bohrstoß: Fläche, 1,8 s Aktion, 5 s Cooldown: 60 %/s × 1,5 × 1,10 × 1,8 s.
            SkillDefinition drill = Skills.Get(SkillIds.Drill);
            Assert.IsTrue(SkillBudgetConfig.IsArea(drill));
            Assert.AreEqual(Ticks.FromTenths(18), SkillBudgetConfig.ActionTicks(drill));
            Assert.AreEqual(17820, Budget.RequiredDamageBp(drill));

            // Längerer Cooldown verlangt mehr Wirkung.
            var quick = new SkillDefinition("q", "q", 10, 0, Ticks.FromSeconds(3), new ISkillEffect[] { new DamageEffect(1) }, kinds: SkillKind.Attack);
            var slow = new SkillDefinition("s", "s", 10, 0, Ticks.FromSeconds(10), new ISkillEffect[] { new DamageEffect(1) }, kinds: SkillKind.Attack);
            Assert.Greater(Budget.RequiredDamageBp(slow), Budget.RequiredDamageBp(quick));

            // Strengere Konfiguration: der Katalog fällt durch.
            var strict = new SkillBudgetConfig { SingleTargetFactorPercent = 1000, AreaFactorPercent = 1000 };
            Assert.IsTrue(Skills.All.Where(SkillBudgetConfig.IsDamageSkill).Any(s => SkillBudgetConfig.DamagePerTargetBp(s) < strict.RequiredDamageBp(s)));
        }

        // ------------------------------------------------------------------ Basisangriff

        [Test]
        public void TheKnightsBasicAttackDealsSixtyPercent()
        {
            SkillDefinition basic = Skills.Get(SkillIds.BasicAttack);
            Assert.AreEqual(BasisPoints.Percent(60), ((DamageEffect)basic.Effects[0]).DamageBp);
            Assert.AreEqual(Ticks.PerSecond / 4, basic.CooldownCutOnHitTicks);
            Assert.AreSame(basic.GetType(), SkillDefinition.BasicAttack.GetType());
            Assert.AreEqual(BasisPoints.Full, ((DamageEffect)SkillDefinition.BasicAttack.Effects[0]).DamageBp, "Gegner behalten 100 %");

            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 10, 20), null, null);
            Assert.AreEqual(BasisPoints.Percent(60), ((DamageEffect)knight.Board.Fallback.Skill.Effects[0]).DamageBp);
            BattleResult r = Run(Duel(knight, Fighter("B", 100000, 0, 1000)), 2);
            Assert.AreEqual(6, r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "Ritter").Amount);
        }

        [Test]
        public void BasicAttackHitsShortenRunningCooldowns()
        {
            // Rüstungsbruch einmal zu Beginn, danach füllt der Basisangriff. Mit Kürzung kommt der zweite Bruch früher.
            int SecondStart(int cut, out int hits)
            {
                SkillDefinition basic = SkillDefinition.CreateBasicAttack(BasisPoints.Percent(60), cut);
                var board = new LogicBoard(new[] { new LogicRow(AlwaysCondition.Instance, Skills.Get(SkillIds.ArmorBreak), "Immer") }, basic);
                BattleResult r = Run(Duel(Fighter("A", 1000, 10, 20, board: board), Fighter("B", 100000, 0, 1000)), 20);
                List<int> starts = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak).Select(e => e.Tick).ToList();
                hits = r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Detail == SkillIds.BasicAttack && e.Tick > starts[0] && e.Tick < starts[1]);
                return starts[1] - starts[0];
            }
            int plain = SecondStart(0, out _);
            int cut = SecondStart(Ticks.PerSecond / 4, out int hits);
            Assert.Greater(hits, 0);
            Assert.Less(cut, plain);
            // Höchstens 0,25 s pro Treffer; Treffer nach Ablauf des Cooldowns kürzen nichts mehr.
            Assert.GreaterOrEqual(cut, plain - hits * (Ticks.PerSecond / 4));
        }

        [Test]
        public void AMissDoesNotShortenCooldowns()
        {
            SkillDefinition basic = SkillDefinition.CreateBasicAttack(BasisPoints.Percent(60), Ticks.PerSecond);
            var board = new LogicBoard(new[] { new LogicRow(AlwaysCondition.Instance, Skills.Get(SkillIds.ArmorBreak), "Immer") }, basic);
            CombatantSetup ghost = Fighter("B", 100000, 0, 1000);
            ghost.Stats[StatKind.Dodge] = BasisPoints.Percent(100);
            BattleResult r = Run(Duel(Fighter("A", 1000, 10, 20, board: board), ghost), 12);
            List<int> starts = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak).Select(e => e.Tick).ToList();
            int hits = r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Detail == SkillIds.BasicAttack && e.Tick > starts[0] && e.Tick < starts[1]);
            int dodged = r.Events.Count(e => e.Kind == BattleEventKind.Dodged && e.Tick > starts[0] && e.Tick < starts[1]);
            Assert.Greater(dodged, 0);
            // Nur Treffer kürzen (1 s pro Treffer in diesem Test), Ausweicher nicht.
            Assert.GreaterOrEqual(starts[1] - starts[0], Skills.Get(SkillIds.ArmorBreak).CooldownTicks - hits * Ticks.PerSecond);
        }

        // ------------------------------------------------------------------ Anteil im Kampf

        [Test]
        public void WithThreeDamageSkillsTheBasicAttackDealsAtMostThirtyFivePercent()
        {
            var rows = new[]
            {
                new BoardRowSpec("always", SkillIds.ArmorBreak),
                new BoardRowSpec("always", SkillIds.Drill),
                new BoardRowSpec("always", SkillIds.ShockStab),
            };
            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 10, 20), null, rows);
            BattleResult r = Run(Duel(knight, Fighter("Sandsack", 100000, 0, 1000)), 30);
            BattleReport report = BattleReport.Create(r);

            Assert.Greater(report.TotalDamage, 0);
            Assert.LessOrEqual(report.BasicAttackShareBp, BasisPoints.Percent(35), report.DamageSplitText);
            StringAssert.StartsWith("Basisangriff ", report.DamageSplitText);
            StringAssert.Contains(" · Skills ", report.DamageSplitText);
        }

        [Test]
        public void EveryStartKitHasADamageSkillOnItsStartRune()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
                Assert.IsTrue(SkillBudgetConfig.IsDamageSkill(Skills.Get(kit.StartSkillId)), $"{kit.Name}: {kit.StartSkillId}");
            Assert.AreEqual(SkillIds.Drill, KnightKit.Defaults.Single(k => k.Id == "shield").StartSkillId);
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
                    BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 0, s.Stats, s.Runes, s.Gear), rat.Create(), seed);
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
                    CombatResult r = resolver.Resolve(new CombatRequest(CellContent.Boss, 0, s.Stats, s.Runes, s.Gear), new System.Random(seed));
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
            StringAssert.Contains("Basisangriff ", report.Summary());
        }
    }
}
