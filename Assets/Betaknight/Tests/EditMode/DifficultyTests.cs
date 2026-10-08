using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Schwierigkeits-Bonus der Relais (A-11, A-19): Stufen ohne Cooldown-Anteil, Grössen-Grenze je Stufe, Wandern über
    /// Auslöser, Erleichterer.
    /// </summary>
    public class DifficultyTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly DifficultyBonusConfig Config = DifficultyBonusConfig.Default;

        /// <summary>Nie erfüllt (volle HP): solche Komponenten feuern nur über Auslöser.</summary>
        private const string Never = "hp_low";

        private static LogicRow Row(string runeId, SkillDefinition skill, int difficulty = 0, int parameter = 30, bool repeatWhileTrue = false) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId, difficulty, repeatWhileTrue);

        /// <summary>Ersatz für das frühere «Always + Cooldown»: ein Clock-Relais, das alle <paramref name="ticks"/> auslöst.</summary>
        private static LogicRow Clock(SkillDefinition skill, int difficulty = 0, int ticks = Ticks.PerSecond) =>
            new LogicRow(new ClockCondition(ticks), skill, "clock", difficulty);

        /// <summary>100 % Waffenschaden plus 0,5 s Betäubung; Cast 10 Ticks.</summary>
        private static SkillDefinition Nuke(string id = "nuke") =>
            new SkillDefinition(id, id, 10, 0, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(100)),
                new StunEffect(Ticks.FromTenths(5)),
            });

        private static SkillDefinition Ping(string id = "ping") =>
            new SkillDefinition(id, id, 1, 0, new ISkillEffect[0]);

        private static SkillDefinition Stunner(int ticks = 10) =>
            new SkillDefinition("stunner", "stunner", 1, 0, new ISkillEffect[] { new StunEffect(ticks) });

        private static SkillDefinition Poisoner() =>
            new SkillDefinition("poisoner", "poisoner", 1, 0,
                new ISkillEffect[] { new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 1), onTarget: true) });

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static BattleResult Solo(LogicBoard board, int seconds = 20, System.Action<CombatantSetup> player = null,
            System.Action<CombatantSetup> enemy = null)
        {
            CombatantSetup a = Fighter("A", 1000, 100, 1000, board: board);
            CombatantSetup b = Fighter("B", 100000, 0, 1000);
            player?.Invoke(a);
            enemy?.Invoke(b);
            return Run(Duel(a, b), seconds);
        }

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source?.Name == "A" && e.Detail == skillId).ToList();

        private static int FirstDamage(BattleResult r, int row) =>
            r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.RowIndex == row).Amount;

        private static CircuitSpec Circuit(IEnumerable<RelaySpec> relays, IEnumerable<ComponentSpec> components)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.AddRange(relays);
            spec.Components.AddRange(components);
            return spec;
        }

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
            Assert.AreEqual(0, runes.Get("clock").Difficulty);
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
            Assert.AreEqual((0, 0, 0, 1), Tuple(Config[0]));
            Assert.AreEqual((15, 0, 0, 2), Tuple(Config[1]));
            Assert.AreEqual((30, 20, 0, 4), Tuple(Config[2]));
            Assert.AreEqual((60, 35, Ticks.PerSecond, 6), Tuple(Config[3]));
            Assert.AreEqual("+30 % power, −20 % Cast Time", Config[2].Text);
            Assert.AreEqual("no bonus", Config[0].Text);
            StringAssert.EndsWith("powers components up to 4 cells", DifficultyText.Tooltip(2));
        }

        private static (int, int, int, int) Tuple(DifficultyBonus b) =>
            (b.PowerPercent, b.CastReductionPercent, b.ExtraStatusTicks, b.MaxCells);

        // ------------------------------------------------------------------ Grössen-Grenze je Stufe

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 4)]
        [TestCase(3, 6)]
        public void TheSizeLimitGrowsWithTheTier(int tier, int cells)
        {
            Assert.AreEqual(cells, Config.MaxCells(tier));
            Assert.AreEqual(cells, Config[tier].MaxCells);
        }

        // Je Stufe eine Rune: die grösste erlaubte Form wird versorgt, die nächstgrössere ist «zu gross».
        [TestCase("battle_start", SkillIds.ShockStab, true)]   // ◇ 1 Zelle
        [TestCase("battle_start", SkillIds.ShieldBash, false)] // ◇ 1×2
        [TestCase("on_hit", SkillIds.ShieldBash, true)]        // ◆ 2 Zellen
        [TestCase("on_hit", SkillIds.ArmorBreak, false)]       // ◆ 2×2
        [TestCase("enemy_stunned", SkillIds.ArmorBreak, true)] // ◆◆ 4 Zellen
        [TestCase("enemy_stunned", SkillIds.RailCannon, false)]// ◆◆ 2×3
        [TestCase("every_20s", SkillIds.RailCannon, true)]     // ◆◆◆ 6 Zellen
        public void ARelayPowersComponentsUpToTheLimitOfItsTier(string runeId, string skillId, bool powered)
        {
            LogicBoard board = BoardFactory.CreateDefault().Create(Circuit(
                new[] { new RelaySpec(runeId, new Cell(0, 0)) }, new[] { new ComponentSpec(skillId, new Cell(1, 0)) }), null);
            LogicRow row = board.Rows.Single();
            LogicRelay relay = board.Relays.Single();
            Assert.AreEqual(Config.MaxCells(relay.Difficulty), relay.MaxCells);
            Assert.AreEqual(powered, row.IsPowered);
            Assert.AreEqual(!powered, row.TooLargeFor.Contains(relay), "berührt, aber zu gross");
        }

        // ------------------------------------------------------------------ Bonus je Stufe

        [TestCase(0, 100, 10, 0)]
        [TestCase(1, 115, 10, 0)]
        [TestCase(2, 130, 8, 0)]
        [TestCase(3, 160, 6, 20)]
        public void BonusPerTier(int tier, int damage, int cast, int extraStun)
        {
            BattleResult r = Solo(new LogicBoard(new[] { Row("battle_start", Nuke(), tier) }), 3);

            BattleEvent start = Starts(r, "nuke").First();
            Assert.AreEqual(tier, start.Tier);
            Assert.AreEqual(cast, start.Amount, "Cast-Zeit");
            Assert.AreEqual(10 - cast, start.Bonus, "eingesparte Cast-Zeit");
            Assert.AreEqual(damage, FirstDamage(r, 0), "Wirkung");
            BattleEvent stun = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun);
            Assert.AreEqual(Ticks.FromTenths(5) + extraStun, stun.Amount, "Dauer von Status-Wirkungen");
        }

        [Test]
        public void ShorterCastMeansMoreExecutions()
        {
            // Früher: kürzerer Cooldown. Jetzt: ein Relais, das jeden Tick auslöst, und nur die Cast-Zeit begrenzt die Schleife.
            var slow = new SkillDefinition("slow", "slow", 40, 0, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) });
            int Count(int tier) => Starts(Solo(new LogicBoard(new[] { Clock(slow, tier, 1) }), 30), "slow").Count;
            Assert.AreEqual(Count(0), Count(1), "Stufe 1 kürzt die Cast-Zeit nicht");
            Assert.Less(Count(1), Count(2));
            Assert.Less(Count(2), Count(3));
        }

        [Test]
        public void HealAndSelfBuffsAreBoostedToo()
        {
            var skill = new SkillDefinition("mend", "mend", 1, 0, new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(10)),
                new StatModifierEffect("guard", StatKind.Armor, 10, Ticks.FromSeconds(2)),
            });
            SkillDefinition boosted = Config.Apply(skill, 3);
            Assert.AreEqual(BasisPoints.Percent(16), ((HealEffect)boosted.Effects[0]).MaxHpBp);
            var guard = (StatModifierEffect)boosted.Effects[1];
            Assert.AreEqual(16, guard.Amount, "Schild/Buff +60 %");
            Assert.AreEqual(Ticks.FromSeconds(3), guard.Ticks, "+1 s Dauer");
        }

        [Test]
        public void TheBasicAttackNeverGetsABonus()
        {
            Assert.AreSame(SkillDefinition.BasicAttack, Config.Apply(SkillDefinition.BasicAttack, 3));
            BattleResult r = Solo(new LogicBoard(new[] { Clock(SkillDefinition.BasicAttack, 3) }), 3);
            Assert.IsTrue(Starts(r, SkillDefinition.BasicAttackId).All(e => e.Tier == 0));
        }

        [Test]
        public void TheCastFloorStays()
        {
            var quick = new SkillDefinition("quick", "quick", CastTime.DefaultMinTicks, 0, new ISkillEffect[0]);
            Assert.AreEqual(CastTime.DefaultMinTicks, Config.Apply(quick, 3).CastTicks());
            // Auch zusammen mit Ausrüstungs-Boni nicht unter 0,1 s.
            SkillDefinition stacked = Config.Apply(quick.WithBonus(0, -60), 3);
            Assert.AreEqual(CastTime.DefaultMinTicks, stacked.CastTicks());
            BattleResult r = Solo(new LogicBoard(new[] { Clock(quick, 3, 1) }), 2);
            Assert.Greater(Starts(r, "quick").Count, 0);
            Assert.IsTrue(Starts(r, "quick").All(e => e.Amount >= CastTime.DefaultMinTicks));
        }

        // ------------------------------------------------------------------ Bonus bleibt bei Erleichterung

        [Test]
        public void TheBonusStaysWhenTheConditionIsEased()
        {
            var catalog = new EquipmentCatalog(EquipmentCatalog.CreateDefault().All);
            var gloves = new Equipment();
            gloves.Equip(catalog.Get(ReliefCarrierIds.NumbingGloves));
            // «Enemy Stunned» ◆◆ versorgt den 2×2-Bohrer; ein Clock-Relais darunter den Schockstoss (20 % Betäubung).
            CircuitSpec spec = Circuit(
                new[] { new RelaySpec("enemy_stunned", new Cell(0, 0)), new RelaySpec("clock", new Cell(0, 3)) },
                new[] { new ComponentSpec(SkillIds.Drill, new Cell(1, 0)), new ComponentSpec(SkillIds.ShockStab, new Cell(1, 3)) });

            CombatantSetup plain = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), null, spec);
            CombatantSetup eased = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), gloves, spec);
            Assert.AreEqual(SkillIds.Drill, plain.Board.Rows[0].Skill.Id);
            Assert.AreEqual(2, plain.Board.Rows[0].Difficulty);
            Assert.AreEqual(2, eased.Board.Rows[0].Difficulty, "Erleichterer ändern die Stufe nicht");
            Assert.AreEqual(4, eased.Board.Relays[0].MaxCells, "… und auch nicht die Grössen-Grenze");
            Assert.Greater(eased.Reliefs[ReliefIds.StunLonger], 0);

            // Ein Modul am Relais, das es erleichtert, ändert die Stufe ebenso wenig.
            CircuitSpec alarm = Circuit(
                new[] { new RelaySpec("hp_low", new Cell(0, 0), modules: new[] { new ModuleSpec(ModuleIds.AlarmSensor) }) },
                new[] { new ComponentSpec(SkillIds.Repair, new Cell(1, 0)) });
            CombatantSetup withModule = PlayerLoadout.CreateCombatant("A", new CombatStats(1000, 10, 1000), null, alarm);
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
            var board = new LogicBoard(new[] { Clock(Nuke("source"), sourceTier), Row(Never, Nuke("target"), targetTier) }, null, graph);
            return Solo(board, 20);
        }

        // Ausgelöste Komponenten laufen mit Stufe (und Grenze) der auslösenden Ausführung; die Stufe ihres eigenen
        // Relais zählt nur, wenn dieses selbst auslöst.
        [TestCase(3, 1, 3)]
        [TestCase(0, 2, 0)]
        [TestCase(2, 3, 2)]
        [TestCase(1, 0, 1)]
        [TestCase(0, 0, 0)]
        public void TheBonusTravelsAlongTriggers(int source, int target, int expected)
        {
            BattleResult r = Chain(source, target);
            List<BattleEvent> triggered = Starts(r, "target");
            Assert.Greater(triggered.Count, 0);
            Assert.IsTrue(triggered.All(e => e.IsTriggered && e.Tier == expected), string.Join(", ", triggered.Select(e => e.Tier)));

            Assert.AreEqual(10 - CastTime.Apply(10, -Config[expected].CastReductionPercent), triggered[0].Bonus, "eingesparte Cast-Zeit");
            int damage = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "A" && e.RowIndex == 1).Amount;
            Assert.AreEqual(100 + Config[expected].PowerPercent, damage);
        }

        // Wartet die Komponente schon (eigenes Relais), hebt ein Auslöser mit höherer Stufe nur ihren Bonus: die höhere zählt.
        [TestCase(3, 0, 3)]
        [TestCase(0, 2, 2)]
        [TestCase(1, 3, 3)]
        public void WhenAlreadyQueuedTheHigherTierCountsNotStacked(int source, int target, int expected)
        {
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) });
            var board = new LogicBoard(new[] { Row("battle_start", Nuke("source"), source), Row("battle_start", Nuke("target"), target) }, null, graph);
            BattleResult r = Solo(board, 3);

            List<BattleEvent> starts = Starts(r, "target");
            Assert.AreEqual(1, starts.Count, "eine Ausführung, der zweite Auslöser ist ein Missed Trigger");
            Assert.AreEqual(expected, starts[0].Tier);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 1 && e.Amount == (int)MissReason.AlreadyQueued));
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
            LogicRelay Relay(string rune, bool inverted = false) =>
                factory.CreateRelay(new RelaySpec(rune, new Cell(0, 0), modules: inverted ? invert : null), null);

            Assert.AreEqual(0, Relay("clock").Difficulty);
            Assert.AreEqual(3, Relay("every_20s").Difficulty);
            Assert.AreEqual(0, Relay("every_20s", true).Difficulty, "NICHT alle 20 s ist fast immer");
            Assert.AreEqual(1, Relay("enemy_armored", true).Difficulty);
            Assert.AreEqual(2, Relay("hp_low").Difficulty);
            Assert.AreEqual(0, Relay("hp_low", true).Difficulty);

            // Die Grössen-Grenze folgt der eigenen Stufe des umgekehrten Relais.
            Assert.AreEqual(6, Relay("every_20s").MaxCells);
            Assert.AreEqual(1, Relay("every_20s", true).MaxCells);
            Assert.AreEqual(1, factory.MaxCellsOf(new RelaySpec("every_20s", new Cell(0, 0), modules: invert)));
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
                StringAssert.StartsWith("Eases: \"", reliefs.EasesText(r.CarrierId, runes));
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
                CircuitSpec spec = Circuit(new[] { new RelaySpec("clock", new Cell(0, 0), modules: new[] { new ModuleSpec(r.CarrierId) }) },
                    new[] { new ComponentSpec(SkillIds.Drill, new Cell(1, 0)) });
                CombatantSetup setup = PlayerLoadout.CreateCombatant("A", new CombatStats(100, 10, 20), null, spec);
                Assert.AreEqual(r.Value, setup.Reliefs[r.ReliefId], r.CarrierId);
            }
        }

        private static BattleResult WithRelief(LogicBoard board, string relief, int value, int seconds = 10,
            System.Action<CombatantSetup> player = null, System.Action<CombatantSetup> enemy = null) =>
            Solo(board, seconds, a =>
            {
                if (relief != null) a.Reliefs[relief] = value;
                player?.Invoke(a);
            }, enemy);

        [Test]
        public void Relief_StunsLastLonger()
        {
            int Stun(string relief) => WithRelief(new LogicBoard(new[] { Row("battle_start", Stunner(10)) }), relief, 20, 2)
                .Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun).Amount;
            Assert.AreEqual(10, Stun(null));
            Assert.AreEqual(30, Stun(ReliefIds.StunLonger));
        }

        [Test]
        public void Relief_StunAfterglow()
        {
            // Komponente 1 pingt mit «Repeat while true», solange der Gegner als betäubt gilt; Komponente 2 betäubt ihn einmal.
            LogicBoard Board() => new LogicBoard(new[] { Row("enemy_stunned", Ping(), repeatWhileTrue: true), Row("battle_start", Stunner(10)) });
            int PingsAfterStun(string relief)
            {
                // Gemessen wird, wie lange die Bedingung gilt: die Wiederholung läuft nur, solange sie wahr ist.
                BattleResult r = WithRelief(Board(), relief, 10, 3);
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
            LogicBoard Board() => new LogicBoard(new[] { Row("enemy_burning", Ping()), Row("battle_start", Poisoner()) });
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
                new LogicBoard(new[] { Row("charge_full", Ping(), parameter: SkillCatalog.ChargeMax), Clock(filler) });
            Assert.AreEqual(0, Starts(Solo(Board(Ping("idle")), 20), "ping").Count);
            Assert.Greater(Starts(Solo(Board(skills.Get(SkillIds.ChargeCoil)), 20), "ping").Count, 0);
        }

        [Test]
        public void ReliefSkill_NumbingMistStunsEveryone()
        {
            var skills = SkillCatalog.CreateDefault();
            BattleSetup setup = Duel(Fighter("A", 1000, 10, 1000,
                board: new LogicBoard(new[] { Row("enemy_stunned", Ping()), Row("battle_start", skills.Get(SkillIds.NumbingMist)) })), Fighter("B", 100000, 0, 1000));
            setup.Enemies.Add(Fighter("C", 100000, 0, 1000));
            BattleResult r = Run(setup, 5);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun && e.Target.Name == "B"));
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun && e.Target.Name == "C"));
            Assert.Greater(Starts(r, "ping").Count, 0);
        }

        // ------------------------------------------------------------------ Auswertung und Anzeige

        [Test]
        public void TheReportShowsHowOftenTheRelayTriggeredAndWhatTheBonusDid()
        {
            BattleResult r = Solo(new LogicBoard(new[] { Row("enemy_stunned", Nuke("hard"), 2), Row("battle_start", Stunner(40)) }), 10);
            BattleReport report = BattleReport.Create(r);
            RowReport hard = report.Rows[0];

            Assert.AreEqual(2, hard.Difficulty);
            Assert.GreaterOrEqual(hard.Triggered, 1);
            Assert.Greater(hard.Fired, 0);
            Assert.AreEqual(hard.Fired, hard.BonusExecutions);
            Assert.Greater(hard.BonusDamage, 0);
            Assert.AreEqual(hard.Damage - hard.Damage * 100 / 130, hard.BonusDamage, 1);
            Assert.AreEqual(hard.Fired * (10 - 8), hard.CastSavedTicks, "−20 % Cast-Zeit je Ausführung");
            StringAssert.Contains("Triggered", hard.DifficultyText);
            StringAssert.Contains("cast time saved", hard.DifficultyText);
            Assert.IsTrue(report.Hints.Any(h => h.Contains("Bonus")), string.Join("\n", report.Hints));
        }

        [Test]
        public void TheSkillInfoShowsTheValuesWithTheBonus()
        {
            SkillDefinition drill = SkillCatalog.CreateDefault().Get(SkillIds.Drill);
            var stats = new SkillUserStats(10);
            SkillInfo plain = SkillInfo.Create(drill, stats);
            SkillInfo hard = SkillInfo.Create(Config.Apply(drill, 3), stats);
            Assert.Less(hard.WindupTicks, plain.WindupTicks, "−35 % Cast-Zeit");
            Assert.AreEqual(plain.BaseCastTicks, hard.BaseCastTicks);
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
                var board = new LogicBoard(new[] { Row("on_crit", Nuke("a"), 2), Clock(Nuke("b"), 1), Row("enemy_stunned", Stunner(), 2) }, null, graph);
                BattleResult r = WithRelief(board, ReliefIds.StunAfterglow, 10, 20,
                    a => a.Stats[StatKind.Crit] = BasisPoints.Percent(30), b => b.Stats[StatKind.Damage] = 3);
                return string.Join("\n", r.Events.Select(e => $"{e} T{e.Tier} B{e.Bonus}"));
            }
            Assert.AreEqual(Log(), Log());
        }
    }
}
