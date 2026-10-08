using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Schwierigkeits-Bonus der Logikbausteine (A-11): Stufen, Wandern über Auslöser, Erleichterer.</summary>
    public class DifficultyTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly DifficultyBonusConfig Config = DifficultyBonusConfig.Default;

        /// <summary>Nie erfüllt (volle HP): solche Zeilen feuern nur über Auslöser.</summary>
        private const string Never = "hp_low";

        private static LogicRow Row(string runeId, SkillDefinition skill, int difficulty = 0, int parameter = 30) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId, difficulty);

        /// <summary>100 % Waffenschaden plus 0,5 s Betäubung; Cast 10 Ticks, Cooldown 5 s.</summary>
        private static SkillDefinition Nuke(string id = "nuke") =>
            new SkillDefinition(id, id, 10, 0, Ticks.FromSeconds(5), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(100)),
                new StunEffect(Ticks.FromTenths(5)),
            });

        private static SkillDefinition Ping(string id = "ping", int cooldown = 0) =>
            new SkillDefinition(id, id, 1, 0, cooldown, new ISkillEffect[0]);

        private static SkillDefinition Stunner(int ticks = 10) =>
            new SkillDefinition("stunner", "stunner", 1, 0, Ticks.FromSeconds(50), new ISkillEffect[] { new StunEffect(ticks) });

        private static SkillDefinition Poisoner() =>
            new SkillDefinition("poisoner", "poisoner", 1, 0, Ticks.FromSeconds(50),
                new ISkillEffect[] { new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 1), onTarget: true) });

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static BattleResult Solo(LogicBoard board, int seconds = 20, System.Action<CombatantSetup> player = null,
            System.Action<CombatantSetup> enemy = null, RowQueueConfig queue = null)
        {
            CombatantSetup a = Fighter("A", 1000, 100, 1000, board: board);
            CombatantSetup b = Fighter("B", 100000, 0, 1000);
            player?.Invoke(a);
            enemy?.Invoke(b);
            BattleSetup setup = Duel(a, b);
            if (queue != null) setup.Queue = queue;
            return Run(setup, seconds);
        }

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source?.Name == "A" && e.Detail == skillId).ToList();

        private static int FirstDamage(BattleResult r, int row) =>
            r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.RowIndex == row).Amount;

        // ------------------------------------------------------------------ Daten

        [Test]
        public void EveryRuneHasADifficultyAndTheExamplesMatch()
        {
            var runes = RuneCatalog.CreateDefault();
            foreach (RuneDefinition rune in runes.All)
            {
                Assert.That(rune.Difficulty, Is.InRange(0, DifficultyBonusConfig.MaxTier), rune.Id);
                Assert.That(rune.InvertedDifficulty, Is.InRange(0, DifficultyBonusConfig.MaxTier), rune.Id);
            }
            Assert.AreEqual(0, runes.Get("always").Difficulty);
            Assert.AreEqual(1, runes.Get("on_hit").Difficulty);
            Assert.AreEqual(2, runes.Get("on_crit").Difficulty);
            Assert.AreEqual(2, runes.Get("enemy_stunned").Difficulty);
            Assert.AreEqual(3, runes.Get("charge_full").Difficulty);
            Assert.AreEqual(3, runes.Get("dodge_streak").Difficulty);
            Assert.AreEqual(3, runes.Get("hp_critical").Difficulty);
        }

        [Test]
        public void TheBonusTableHasTheStartValues()
        {
            Assert.IsTrue(Config[0].IsNone);
            Assert.AreEqual((15, 0, 0, 0), Tuple(Config[1]));
            Assert.AreEqual((30, 25, 0, 0), Tuple(Config[2]));
            Assert.AreEqual((50, 50, 30, Ticks.PerSecond), Tuple(Config[3]));
            Assert.AreEqual("−30 % Cooldown, +25 % Wirkung", Config[2].Text);
        }

        private static (int, int, int, int) Tuple(DifficultyBonus b) =>
            (b.CooldownReductionPercent, b.PowerPercent, b.CastReductionPercent, b.ExtraStatusTicks);

        // ------------------------------------------------------------------ Bonus je Stufe

        [TestCase(0, 100, 10, 0)]
        [TestCase(1, 100, 10, 0)]
        [TestCase(2, 125, 10, 0)]
        [TestCase(3, 150, 7, 20)]
        public void BonusPerTier(int tier, int damage, int cast, int extraStun)
        {
            SkillDefinition nuke = Nuke();
            BattleResult r = Solo(new LogicBoard(new[] { Row("always", nuke, tier) }), 3);

            BattleEvent start = Starts(r, "nuke").First();
            Assert.AreEqual(tier, start.Tier);
            Assert.AreEqual(cast, start.Amount, "Cast-Zeit");
            Assert.AreEqual(damage, FirstDamage(r, 0), "Wirkung");
            int cooldown = nuke.CooldownTicks * (100 - Config[tier].CooldownReductionPercent) / 100;
            Assert.AreEqual(nuke.CooldownTicks - cooldown, start.Bonus, "eingesparter Cooldown");
            BattleEvent stun = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun);
            Assert.AreEqual(Ticks.FromTenths(5) + extraStun, stun.Amount, "Dauer von Status-Wirkungen");
        }

        [Test]
        public void ShorterCooldownMeansMoreExecutions()
        {
            int Count(int tier) => Starts(Solo(new LogicBoard(new[] { Row("always", Nuke(), tier) }), 30), "nuke").Count;
            Assert.Less(Count(0), Count(1));
            Assert.Less(Count(1), Count(2));
            Assert.Less(Count(2), Count(3));
        }

        [Test]
        public void HealAndSelfBuffsAreBoostedToo()
        {
            var skill = new SkillDefinition("mend", "mend", 1, 0, Ticks.FromSeconds(5), new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(10)),
                new StatModifierEffect("guard", StatKind.Armor, 10, Ticks.FromSeconds(2)),
            });
            SkillDefinition boosted = Config.Apply(skill, 3);
            Assert.AreEqual(BasisPoints.Percent(15), ((HealEffect)boosted.Effects[0]).MaxHpBp);
            var guard = (StatModifierEffect)boosted.Effects[1];
            Assert.AreEqual(15, guard.Amount, "Schild/Buff +50 %");
            Assert.AreEqual(Ticks.FromSeconds(3), guard.Ticks, "+1 s Dauer");
        }

        [Test]
        public void TheBasicAttackNeverGetsABonus()
        {
            Assert.AreSame(SkillDefinition.BasicAttack, Config.Apply(SkillDefinition.BasicAttack, 3));
            BattleResult r = Solo(new LogicBoard(new[] { Row("always", SkillDefinition.BasicAttack, 3) }), 3);
            Assert.IsTrue(Starts(r, SkillDefinition.BasicAttackId).All(e => e.Tier == 0));
        }

        [Test]
        public void TheCastFloorStays()
        {
            var quick = new SkillDefinition("quick", "quick", CastTime.DefaultMinTicks, 0, 20, new ISkillEffect[0]);
            Assert.AreEqual(CastTime.DefaultMinTicks, Config.Apply(quick, 3).CastTicks());
            // Auch zusammen mit Ausrüstungs-Boni nicht unter 0,1 s.
            SkillDefinition stacked = Config.Apply(quick.WithBonus(0, 0, -60), 3);
            Assert.AreEqual(CastTime.DefaultMinTicks, stacked.CastTicks());
            BattleResult r = Solo(new LogicBoard(new[] { Row("always", quick, 3) }), 2);
            Assert.IsTrue(Starts(r, "quick").All(e => e.Amount >= CastTime.DefaultMinTicks));
        }

        // ------------------------------------------------------------------ Bonus bleibt bei Erleichterung

        [Test]
        public void TheBonusStaysWhenTheConditionIsEased()
        {
            var catalog = new EquipmentCatalog(EquipmentCatalog.CreateDefault().All);
            var gloves = new Equipment();
            gloves.Equip(catalog.Get(ReliefCarrierIds.NumbingGloves));
            var spec = new[] { new BoardRowSpec("enemy_stunned", SkillIds.Drill), new BoardRowSpec("always", SkillIds.ShockStab) };

            CombatantSetup plain = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), null, spec);
            CombatantSetup eased = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), gloves, spec);
            Assert.AreEqual(2, plain.Board.Rows[0].Difficulty);
            Assert.AreEqual(2, eased.Board.Rows[0].Difficulty, "Erleichterer ändern die Stufe nicht");
            Assert.Greater(eased.Reliefs[ReliefIds.StunLonger], 0);

            // Ein Modul am Baustein, das ihn erleichtert, ändert die Stufe ebenso wenig.
            var alarm = new BoardRowSpec("hp_low", SkillIds.Repair, blockModules: new[] { new ModuleSpec(ModuleIds.AlarmSensor) });
            CombatantSetup withModule = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), null, new[] { alarm });
            Assert.AreEqual(2, withModule.Board.Rows[0].Difficulty);
            Assert.AreEqual(10, withModule.Reliefs[ReliefIds.HpThresholdUp]);

            BattleResult r = Run(Duel(eased, Fighter("B", 100000, 0, 1000)), 30);
            List<BattleEvent> drills = Starts(r, SkillIds.Drill).Where(e => e.RowIndex == 0).ToList();
            Assert.Greater(drills.Count, 0);
            Assert.IsTrue(drills.All(e => e.Tier == 2), "volle Stufe trotz Erleichterung");
        }

        // ------------------------------------------------------------------ Bonus wandert über Auslöser

        private static BattleResult Chain(int sourceTier, int targetTier, bool fromBlock = false)
        {
            GraphNode from = fromBlock ? GraphNode.Block(0) : GraphNode.Skill(0);
            var graph = new LogicGraph(new[] { new GraphEdge(from, GraphNode.Skill(1)) });
            var board = new LogicBoard(new[] { Row("always", Nuke("source"), sourceTier), Row(Never, Nuke("target"), targetTier) }, null, graph);
            return Solo(board, 20);
        }

        [TestCase(3, 1, 3)]
        [TestCase(0, 2, 2)]
        [TestCase(2, 3, 3)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 0, 0)]
        public void TheBonusTravelsAlongTriggersAndTheHigherCounts(int source, int target, int expected)
        {
            BattleResult r = Chain(source, target);
            List<BattleEvent> triggered = Starts(r, "target");
            Assert.Greater(triggered.Count, 0);
            Assert.IsTrue(triggered.All(e => e.IsTriggered && e.Tier == expected), string.Join(", ", triggered.Select(e => e.Tier)));

            // Nicht stapelnd: genau die Werte der höheren Stufe.
            int cooldown = Nuke().CooldownTicks;
            Assert.AreEqual(cooldown - cooldown * (100 - Config[expected].CooldownReductionPercent) / 100, triggered[0].Bonus);
            int damage = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.RowIndex == 1).Amount;
            Assert.AreEqual(100 + Config[expected].PowerPercent, damage);
        }

        [Test]
        public void ABlockTriggerCarriesTheTierOfItsBlock()
        {
            List<BattleEvent> triggered = Starts(Chain(3, 0, fromBlock: true), "target");
            Assert.Greater(triggered.Count, 0);
            Assert.IsTrue(triggered.All(e => e.Tier == 3));
        }

        // ------------------------------------------------------------------ Umkehren

        [Test]
        public void InvertUsesItsOwnTier()
        {
            BoardFactory factory = BoardFactory.CreateDefault();
            ModuleSpec[] invert = { new ModuleSpec(ModuleIds.Invert) };

            Assert.AreEqual(0, factory.CreateRow(new BoardRowSpec("always", SkillIds.Drill), null).Difficulty);
            Assert.AreEqual(3, factory.CreateRow(new BoardRowSpec("always", SkillIds.Drill, blockModules: invert), null).Difficulty,
                "NICHT Immer ist sehr selten");
            Assert.AreEqual(3, factory.CreateRow(new BoardRowSpec("every_20s", SkillIds.Drill), null).Difficulty);
            Assert.AreEqual(0, factory.CreateRow(new BoardRowSpec("every_20s", SkillIds.Drill, blockModules: invert), null).Difficulty,
                "NICHT alle 20 s ist fast immer");
            Assert.AreEqual(1, factory.CreateRow(new BoardRowSpec("enemy_armored", SkillIds.Drill, blockModules: invert), null).Difficulty);
        }

        // ------------------------------------------------------------------ Erleichterer

        [Test]
        public void AtLeastEightReliefsAndEveryCarrierExists()
        {
            ReliefCatalog reliefs = ReliefCatalog.CreateDefault();
            var items = EquipmentCatalog.CreateDefault();
            var modules = ModuleCatalog.CreateDefault();
            var skills = SkillCatalog.CreateDefault();
            var runes = RuneCatalog.CreateDefault();

            Assert.GreaterOrEqual(reliefs.All.Count, 8);
            foreach (ReliefDefinition r in reliefs.All)
            {
                switch (r.Kind)
                {
                    case ReliefKind.Passive: Assert.IsTrue(items.TryGet(r.CarrierId, out _), r.CarrierId); break;
                    case ReliefKind.Module: Assert.IsTrue(modules.TryGet(r.CarrierId, out _), r.CarrierId); break;
                    case ReliefKind.Skill: Assert.IsTrue(skills.TryGet(r.CarrierId, out _), r.CarrierId); break;
                }
                Assert.IsNotEmpty(r.Text);
                Assert.IsNotEmpty(r.Tag, $"{r.CarrierId} braucht Tag bzw. Art");
                Assert.IsNotEmpty(r.EasedRuneIds, r.CarrierId);
                foreach (string rune in r.EasedRuneIds) Assert.IsTrue(runes.TryGet(rune, out _), rune);
                StringAssert.StartsWith("Erleichtert: «", reliefs.EasesText(r.CarrierId, runes));
            }
        }

        [Test]
        public void EveryPassiveAndModuleReachesTheFight()
        {
            ReliefCatalog reliefs = ReliefCatalog.CreateDefault();
            var items = EquipmentCatalog.CreateDefault();
            foreach (ReliefDefinition r in reliefs.All.Where(r => r.Kind == ReliefKind.Passive))
            {
                var gear = new Equipment();
                Assert.IsNotNull(gear.Equip(items.Get(r.CarrierId)), r.CarrierId);
                CombatantSetup setup = PlayerLoadout.CreateCombatant("A", new CombatStats(100, 10, 20), gear, null);
                Assert.AreEqual(r.Value, setup.Reliefs[r.ReliefId], r.CarrierId);
            }
            foreach (ReliefDefinition r in reliefs.All.Where(r => r.Kind == ReliefKind.Module))
            {
                var spec = new BoardRowSpec("always", SkillIds.Drill, blockModules: new[] { new ModuleSpec(r.CarrierId) });
                CombatantSetup setup = PlayerLoadout.CreateCombatant("A", new CombatStats(100, 10, 20), null, new[] { spec });
                Assert.AreEqual(r.Value, setup.Reliefs[r.ReliefId], r.CarrierId);
            }
        }

        private static BattleResult WithRelief(LogicBoard board, string relief, int value, int seconds = 10,
            System.Action<CombatantSetup> player = null, System.Action<CombatantSetup> enemy = null, RowQueueConfig queue = null) =>
            Solo(board, seconds, a =>
            {
                if (relief != null) a.Reliefs[relief] = value;
                player?.Invoke(a);
            }, enemy, queue);

        [Test]
        public void Relief_StunsLastLonger()
        {
            int Stun(string relief) => WithRelief(new LogicBoard(new[] { Row("always", Stunner(10)) }), relief, 20, 2)
                .Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun).Amount;
            Assert.AreEqual(10, Stun(null));
            Assert.AreEqual(30, Stun(ReliefIds.StunLonger));
        }

        [Test]
        public void Relief_StunAfterglow()
        {
            // Zeile 1 pingt, solange der Gegner als betäubt gilt; Zeile 2 betäubt ihn einmal.
            LogicBoard Board() => new LogicBoard(new[] { Row("enemy_stunned", Ping()), Row("always", Stunner(10)) });
            int PingsAfterStun(string relief)
            {
                // Ohne Warteschlange (A-13): gemessen wird nur, wie lange die Bedingung gilt.
                BattleResult r = WithRelief(Board(), relief, 10, 3, queue: RowQueueConfig.Off);
                int expired = r.Events.First(e => e.Kind == BattleEventKind.StatusExpired && e.Detail == StatusIds.Stun).Tick;
                return Starts(r, "ping").Count(e => e.Tick >= expired);
            }
            Assert.AreEqual(0, PingsAfterStun(null));
            Assert.Greater(PingsAfterStun(ReliefIds.StunAfterglow), 0);
        }

        [Test]
        public void Relief_ChargeStartsHigher()
        {
            var items = EquipmentCatalog.CreateDefault();
            var gear = new Equipment();
            gear.Equip(items.Get(ReliefCarrierIds.PrechargedCell));
            CombatantSetup setup = PlayerLoadout.CreateCombatant("A", new CombatStats(100, 10, 20), gear, null);
            var battle = new Battle(Duel(setup, Fighter("B", 10, 0)));
            Assert.AreEqual(3, battle.Player.GetResource(ResourceIds.Charge));
        }

        [Test]
        public void Relief_DodgeStreakSurvivesAHit()
        {
            int Fires(string relief) => Starts(WithRelief(new LogicBoard(new[] { Row("dodge_streak", Ping(), parameter: 3) }), relief, 1, 60,
                a => a.Stats[StatKind.Dodge] = BasisPoints.Percent(50),
                b => { b.Stats[StatKind.Damage] = 1; b.Stats[StatKind.AttackInterval] = 5; }), "ping").Count;
            Assert.Greater(Fires(ReliefIds.DodgeTolerance), Fires(null));
        }

        [Test]
        public void Relief_CritAfterBlock()
        {
            var items = EquipmentCatalog.CreateDefault();
            var gear = new Equipment();
            gear.Equip(items.Get(ReliefCarrierIds.CounterShield));
            CombatantSetup knight = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 20), gear, null);
            knight.Stats = knight.Stats.Clone();
            knight.Stats[StatKind.Block] = BasisPoints.Percent(100);
            BattleResult r = Run(Duel(knight, Fighter("B", 100000, 1, 10)), 3);

            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.Blocked));
            BattleEvent buff = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == CritAfterBlockModifier.StatusId);
            Assert.AreEqual("A", buff.Target.Name);
            Assert.AreEqual(ReliefCatalog.CritAfterBlockTicks, buff.Amount);
        }

        [Test]
        public void Relief_BurnCountsPoison()
        {
            LogicBoard Board() => new LogicBoard(new[] { Row("enemy_burning", Ping()), Row("always", Poisoner()) });
            Assert.AreEqual(0, Starts(WithRelief(Board(), null, 1, 3), "ping").Count);
            Assert.Greater(Starts(WithRelief(Board(), ReliefIds.BurnCountsPoison, 1, 3), "ping").Count, 0);
        }

        [Test]
        public void Relief_BigHitCountsEarlier()
        {
            // 12 Schaden bei 100 Max-HP: kein schwerer Treffer ab 15 %, aber ab 10 %.
            int Fires(string relief) => Starts(WithRelief(new LogicBoard(new[] { Row("big_hit_taken", Ping(), parameter: 15) }), relief, 5, 5,
                a => a.Stats[StatKind.MaxHp] = 100,
                b => { b.Stats[StatKind.Damage] = 12; b.Stats[StatKind.AttackInterval] = 20; }), "ping").Count;
            Assert.AreEqual(0, Fires(null));
            Assert.Greater(Fires(ReliefIds.BigHitLower), 0);
        }

        [Test]
        public void Relief_HpThresholdsCountEarlier()
        {
            // Gezählt wird nur bis zum Zeitlimit; danach senkt die Überhitzung die HP ohnehin.
            int Fires(string relief) => Starts(WithRelief(new LogicBoard(new[] { Row("hp_low", Ping(), parameter: 30) }), relief, 10, 2,
                a => { a.Stats[StatKind.MaxHp] = 100; a.StartHp = 35; }), "ping").Count(e => e.Tick < Ticks.FromSeconds(2));
            Assert.AreEqual(0, Fires(null));
            Assert.Greater(Fires(ReliefIds.HpThresholdUp), 0);
        }

        [Test]
        public void Relief_EnemyLowCountsEarlier()
        {
            int Fires(string relief) => Starts(WithRelief(new LogicBoard(new[] { Row("enemy_low", Ping(), parameter: 25) }), relief, 10, 2,
                a => a.Stats[StatKind.Damage] = 0,
                b => { b.Stats[StatKind.MaxHp] = 100; b.StartHp = 30; }), "ping").Count(e => e.Tick < Ticks.FromSeconds(2));
            Assert.AreEqual(0, Fires(null));
            Assert.Greater(Fires(ReliefIds.EnemyLowUp), 0);
        }

        [Test]
        public void ReliefSkill_ChargeCoilFillsCharge()
        {
            var skills = SkillCatalog.CreateDefault();
            LogicBoard Board(SkillDefinition filler) =>
                new LogicBoard(new[] { Row("charge_full", Ping(), parameter: SkillCatalog.ChargeMax), Row("always", filler) });
            Assert.AreEqual(0, Starts(Solo(Board(Ping("idle", 20)), 20), "ping").Count);
            Assert.Greater(Starts(Solo(Board(skills.Get(SkillIds.ChargeCoil)), 20), "ping").Count, 0);
        }

        [Test]
        public void ReliefSkill_NumbingMistStunsEveryone()
        {
            var skills = SkillCatalog.CreateDefault();
            BattleSetup setup = Duel(Fighter("A", 1000, 10, 1000,
                board: new LogicBoard(new[] { Row("enemy_stunned", Ping()), Row("always", skills.Get(SkillIds.NumbingMist)) })), Fighter("B", 100000, 0, 1000));
            setup.Enemies.Add(Fighter("C", 100000, 0, 1000));
            BattleResult r = Run(setup, 5);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun && e.Target.Name == "B"));
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun && e.Target.Name == "C"));
            Assert.Greater(Starts(r, "ping").Count, 0);
        }

        // ------------------------------------------------------------------ Auswertung und Anzeige

        [Test]
        public void TheReportShowsHowOftenTheConditionWasMetAndWhatTheBonusDid()
        {
            BattleResult r = Solo(new LogicBoard(new[] { Row("enemy_stunned", Nuke("hard"), 2), Row("always", Stunner(40)) }), 10);
            BattleReport report = BattleReport.Create(r);
            RowReport hard = report.Rows[0];

            Assert.AreEqual(2, hard.Difficulty);
            Assert.GreaterOrEqual(hard.ConditionMet, 1);
            Assert.AreEqual(hard.Fired, hard.BonusExecutions);
            Assert.Greater(hard.BonusDamage, 0);
            Assert.AreEqual(hard.Damage - hard.Damage * 100 / 125, hard.BonusDamage, 1);
            Assert.Greater(hard.CooldownSavedTicks, 0);
            StringAssert.Contains("erfüllt", hard.DifficultyText);
            Assert.IsTrue(report.Hints.Any(h => h.Contains("Bonus")), string.Join("\n", report.Hints));
        }

        [Test]
        public void TheSkillInfoShowsTheValuesWithTheBonus()
        {
            SkillDefinition drill = SkillCatalog.CreateDefault().Get(SkillIds.Drill);
            var stats = new SkillUserStats(10);
            SkillInfo plain = SkillInfo.Create(drill, stats);
            SkillInfo hard = SkillInfo.Create(Config.Apply(drill, 3), stats);
            Assert.Less(hard.CooldownTicks, plain.CooldownTicks);
            Assert.Greater(hard.Effects[0].Total, plain.Effects[0].Total);
            StringAssert.Contains("◆◆◆", hard.Details);
            Assert.IsEmpty(plain.DifficultyLine);
        }

        // ------------------------------------------------------------------ Gleiche Seeds

        [Test]
        public void SameSeedsGiveTheSameFights()
        {
            string Log()
            {
                var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)) });
                var board = new LogicBoard(new[] { Row("on_crit", Nuke("a"), 2), Row("always", Nuke("b"), 1), Row("enemy_stunned", Stunner(), 2) }, null, graph);
                BattleResult r = WithRelief(board, ReliefIds.StunAfterglow, 10, 20,
                    a => a.Stats[StatKind.Crit] = BasisPoints.Percent(30), b => b.Stats[StatKind.Damage] = 3);
                return string.Join("\n", r.Events.Select(e => $"{e} T{e.Tier} B{e.Bonus}"));
            }
            Assert.AreEqual(Log(), Log());
        }
    }
}
