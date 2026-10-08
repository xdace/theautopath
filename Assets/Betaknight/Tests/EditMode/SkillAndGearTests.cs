using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class SkillAndGearTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly EquipmentCatalog Items = EquipmentCatalog.CreateDefault();

        private static LogicRow Row(string runeId, string skillId, int parameter = 0) =>
            new LogicRow(Conditions.Create(runeId, parameter), Skills.Get(skillId), runeId);

        private static LogicBoard Board(params LogicRow[] rows) => new LogicBoard(rows);

        private static CombatantSetup With(CombatantSetup s, StatKind kind, int value)
        {
            s.Stats[kind] = value;
            return s;
        }

        private static IEnumerable<BattleEvent> On(BattleResult r, string target, BattleEventKind kind) =>
            r.Events.Where(e => e.Kind == kind && e.Target != null && e.Target.Name == target);

        private static BattleResult LongDuel(CombatantSetup a, CombatantSetup b, int seconds = 80, int seed = 3)
        {
            BattleSetup setup = Duel(a, b, seed);
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            return CombatSimulation.Run(setup);
        }

        // ------------------------------------------------------------------ Ausweichen, Block, Krit

        [Test]
        public void DodgeUsesPseudoRandomAroundBaseChance()
        {
            BattleResult r = LongDuel(Fighter("A", 100000, 1, 2), With(Fighter("B", 100000, 0, 1000), StatKind.Dodge, BasisPoints.Percent(30)));

            int dodged = On(r, "B", BattleEventKind.Dodged).Count();
            int hit = On(r, "B", BattleEventKind.Hit).Count();
            double rate = (double)dodged / (dodged + hit);
            Assert.Greater(dodged + hit, 500);
            Assert.That(rate, Is.InRange(0.3, 0.5), "Pseudo-Zufall hebt die Rate leicht über die Grundchance.");

            // Keine langen Pechsträhnen: nach wenigen Fehlversuchen steigt die Chance bis zur Obergrenze.
            int streak = 0, longest = 0;
            foreach (BattleEvent e in r.Events.Where(e => e.Target?.Name == "B" && (e.Kind == BattleEventKind.Dodged || e.Kind == BattleEventKind.Hit)))
            {
                streak = e.Kind == BattleEventKind.Hit ? streak + 1 : 0;
                longest = System.Math.Max(longest, streak);
            }
            Assert.LessOrEqual(longest, 12);
        }

        [Test]
        public void DodgeIsCappedAndAccuracyCountersIt()
        {
            BattleResult r = LongDuel(Fighter("A", 100000, 1, 2), With(Fighter("B", 100000, 0, 1000), StatKind.Dodge, BasisPoints.Full));
            int dodged = On(r, "B", BattleEventKind.Dodged).Count();
            int hit = On(r, "B", BattleEventKind.Hit).Count();
            Assert.That((double)dodged / (dodged + hit), Is.InRange(0.5, 0.7), "Obergrenze 60 %.");

            r = LongDuel(With(Fighter("A", 100000, 1, 2), StatKind.Accuracy, BasisPoints.Percent(30)),
                With(Fighter("B", 100000, 0, 1000), StatKind.Dodge, BasisPoints.Percent(30)));
            Assert.IsEmpty(On(r, "B", BattleEventKind.Dodged));
        }

        [Test]
        public void BlockCutsDamageToAQuarterAndIsCapped()
        {
            BattleResult r = LongDuel(Fighter("A", 100000, 8, 2), With(Fighter("B", 100000, 0, 1000), StatKind.Block, BasisPoints.Full));

            List<BattleEvent> blocked = On(r, "B", BattleEventKind.Blocked).ToList();
            int hits = On(r, "B", BattleEventKind.Hit).Count();
            Assert.That((double)blocked.Count / hits, Is.InRange(0.65, 0.85), "Obergrenze 75 %.");
            Assert.IsTrue(blocked.All(e => e.Amount == 2));
        }

        [Test]
        public void CritDoublesDamage()
        {
            BattleResult r = LongDuel(With(Fighter("A", 100000, 7, 10), StatKind.Crit, BasisPoints.Full), Fighter("B", 100000, 0, 1000), 10);
            List<BattleEvent> hits = On(r, "B", BattleEventKind.Hit).ToList();
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits.All(e => e.Amount == 14));
            Assert.AreEqual(hits.Count, r.Events.Count(e => e.Kind == BattleEventKind.Crit));
        }

        [Test]
        public void DodgeStreakNeedsConsecutiveDodges()
        {
            var ping = new SkillDefinition("ping", "ping", 1, 0, 0, new ISkillEffect[0]);
            CombatantSetup a = With(Fighter("A", 100000, 1, 1000, board: Board(new LogicRow(Conditions.Create("dodge_streak", 2), ping))),
                StatKind.Dodge, BasisPoints.Percent(50));
            BattleResult r = LongDuel(a, Fighter("B", 100000, 1, 5), 30);

            List<BattleEvent> onA = r.Events.Where(e => e.Target?.Name == "A" && (e.Kind == BattleEventKind.Dodged || e.Kind == BattleEventKind.Hit)).ToList();
            List<int> pings = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "ping").Select(e => e.Tick).ToList();
            Assert.IsNotEmpty(pings);

            int previous = 0;
            foreach (int t in pings)
            {
                int streak = 0;
                foreach (BattleEvent e in onA.Where(e => e.Tick >= previous && e.Tick < t))
                    streak = e.Kind == BattleEventKind.Dodged ? streak + 1 : 0;
                Assert.GreaterOrEqual(streak, 2, $"Ping bei Tick {t}");
                previous = t;
            }
        }

        // ------------------------------------------------------------------ Skills

        [Test]
        public void EverySkillRunsAgainstAGroup()
        {
            foreach (SkillDefinition skill in Skills.All)
            {
                var setup = new BattleSetup
                {
                    Player = Fighter("A", 200, 4, board: Board(new LogicRow(AlwaysCondition.Instance, skill))),
                    Enemies = new List<CombatantSetup> { Fighter("B", 60, 2), Fighter("C", 60, 2) },
                    Seed = 5,
                };
                BattleResult r = CombatSimulation.Run(setup);
                Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome, skill.Id);
                Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == skill.Id), skill.Id);
            }
        }

        [Test]
        public void ArmorBreakHalvesArmor()
        {
            BattleResult r = LongDuel(Fighter("A", 1000, 20, board: Board(Row("battle_start", SkillIds.ArmorBreak))),
                Fighter("B", 100000, 0, 1000, armor: 100), 6);

            List<BattleEvent> basics = On(r, "B", BattleEventKind.Damage).Where(e => e.Detail == SkillIds.BasicAttack).ToList();
            BattleEvent broken = On(r, "B", BattleEventKind.Damage).First(e => e.Detail == SkillIds.ArmorBreak);
            Assert.AreEqual(19, broken.Amount, "Der Bruch-Treffer selbst (190 %) trifft noch volle Rüstung.");
            Assert.IsTrue(basics.Where(e => e.Tick <= broken.Tick + Ticks.FromSeconds(6)).All(e => e.Amount == 13));
            Assert.IsTrue(basics.Where(e => e.Tick > broken.Tick + Ticks.FromSeconds(6)).All(e => e.Amount == 10));
        }

        [Test]
        public void IgniteBurnsFiveSeconds()
        {
            BattleResult r = LongDuel(Fighter("A", 1000, 10, 1000, board: Board(Row("battle_start", SkillIds.Ignite))),
                Fighter("B", 100000, 0, 1000, armor: 50), 10);

            List<BattleEvent> burns = On(r, "B", BattleEventKind.Damage).Where(e => e.Detail == StatusIds.Burn).ToList();
            Assert.AreEqual(5, burns.Count);
            Assert.IsTrue(burns.All(e => e.Amount == 7), "70 % Waffenschaden, Rüstung zählt nicht.");
            Assert.IsEmpty(r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Detail == StatusIds.Burn), "Brennen ist kein Treffer.");
        }

        [Test]
        public void ThrustersMakeTheNextHitMiss()
        {
            // B holt 13 Ticks aus, die Schubdüsen (Cast 0,4 s) stehen vorher.
            BattleResult r = LongDuel(Fighter("A", 1000, 1, 1000, board: Board(Row("battle_start", SkillIds.Thrusters))),
                Fighter("B", 100000, 3, 20), 5);

            List<BattleEvent> onA = r.Events.Where(e => e.Target?.Name == "A" && (e.Kind == BattleEventKind.Dodged || e.Kind == BattleEventKind.Hit)).ToList();
            Assert.AreEqual(BattleEventKind.Dodged, onA[0].Kind);
            Assert.AreEqual(1, onA.Count(e => e.Kind == BattleEventKind.Dodged));
        }

        [Test]
        public void AnchorDoublesArmorAndPreventsDodging()
        {
            CombatantSetup a = With(Fighter("A", 1000, 1, 1000, armor: 100, board: Board(Row("battle_start", SkillIds.Anchor))),
                StatKind.Dodge, BasisPoints.Percent(60));
            BattleResult r = LongDuel(a, Fighter("B", 100000, 20, 5), 3);

            int anchored = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Anchor).Tick;
            var during = r.Events.Where(e => e.Target?.Name == "A" && e.Tick > anchored && e.Tick < anchored + Ticks.FromSeconds(3)).ToList();
            Assert.IsEmpty(during.Where(e => e.Kind == BattleEventKind.Dodged));
            Assert.IsTrue(during.Where(e => e.Kind == BattleEventKind.Damage).All(e => e.Amount == 6), "20 × 100 / (100 + 200)");
            Assert.IsNotEmpty(during.Where(e => e.Kind == BattleEventKind.Damage));
        }

        [Test]
        public void EchoRepeatsTheLastSkillButNeverItself()
        {
            BattleResult r = LongDuel(Fighter("A", 1000, 20, 1000, board: Board(Row("battle_start", SkillIds.ArmorBreak), Row("always", SkillIds.Echo))),
                Fighter("B", 100000, 0, 1000), 8);

            // Bis zum zweiten Echo (12 s Cooldown): ein Rüstungsbruch aus seiner Zeile, einer als Wiederholung mit eigener Cast-Zeit.
            int window = Ticks.FromSeconds(10);
            Assert.AreEqual(1, r.Events.Count(e => e.Tick < window && e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak && !e.IsRepeat));
            BattleEvent repeat = r.Events.Single(e => e.Tick < window && e.Kind == BattleEventKind.ActionStarted && e.IsRepeat);
            Assert.AreEqual(SkillIds.ArmorBreak, repeat.Detail);
            BattleEvent repeated = r.Events.First(e => e.Tick > repeat.Tick && e.Kind == BattleEventKind.ActionExecuted && e.Detail == SkillIds.ArmorBreak);
            Assert.AreEqual(CastTime.Medium, repeated.Tick - repeat.Tick, "Die Wiederholung braucht die Cast-Zeit des Rüstungsbruchs.");
            Assert.AreEqual(2, On(r, "B", BattleEventKind.Damage).Count(e => e.Tick < window && e.Detail == SkillIds.ArmorBreak), "Echo wiederholt den Rüstungsbruch.");

            r = LongDuel(Fighter("A", 1000, 20, board: Board(Row("always", SkillIds.Echo))), Fighter("B", 200, 1), 20);
            Assert.AreEqual(BattleOutcome.Victory, r.Outcome, "Echo ohne Vorlage tut nichts, der Kampf läuft weiter.");
        }

        [Test]
        public void EmpBashStunsEveryEnemy()
        {
            var setup = new BattleSetup
            {
                Player = Fighter("A", 1000, 1, 1000, board: Board(Row("battle_start", SkillIds.EmpBash))),
                Enemies = new List<CombatantSetup> { Fighter("B", 1000, 1), Fighter("C", 1000, 1) },
                Seed = 2,
                TimeLimitTicks = 100,
            };
            BattleResult r = CombatSimulation.Run(setup);
            Assert.AreEqual(2, r.Events.Count(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun));
        }

        // ------------------------------------------------------------------ Ausrüstung

        [Test]
        public void TwoHandedWeaponLocksTheShield()
        {
            var gear = new Equipment();
            gear.Equip(Items.Get("round_shield"));
            List<EquipmentDefinition> removed = gear.Equip(Items.Get("plasma_drill"));

            CollectionAssert.AreEqual(new[] { "round_shield" }, removed.Select(i => i.Id));
            Assert.IsNull(gear.Get(EquipmentSlot.Shield));
            Assert.IsFalse(gear.CanEquip(Items.Get("tower_shield"), out string reason));
            Assert.IsNotEmpty(reason);
            Assert.IsNull(gear.Equip(Items.Get("tower_shield")));

            removed = gear.Equip(Items.Get("short_blade"));
            CollectionAssert.AreEqual(new[] { "plasma_drill" }, removed.Select(i => i.Id));
            Assert.IsNotNull(gear.Equip(Items.Get("tower_shield")));
        }

        [Test]
        public void EquipmentAddsStatsPassivesAndSetPieces()
        {
            var gear = new Equipment();
            gear.Equip(Items.Get("holo_barrier"));
            gear.Equip(Items.Get("shock_absorber"));
            gear.Equip(Items.Get("iron_greaves"));

            CombatStats stats = gear.ApplyTo(new CombatStats(30, 4, 20, 1));
            Assert.AreEqual(1 + 3 + 2, stats[StatKind.Armor]);
            Assert.AreEqual(BasisPoints.Percent(20), stats[StatKind.Block]);
            Assert.AreEqual(2, gear.SetPieces(SetIds.Aegis));
            Assert.AreEqual(2, gear.Passives.Count, "Schock-Absorber und Holo-Barriere geben je einen passiven Effekt.");
            Assert.IsTrue(gear.BoostsKind(SkillKind.Shield));
            Assert.IsTrue(gear.BoostsKind(SkillKind.Shock));
        }

        [Test]
        public void RowsWithoutASkillAreOrphanedAndSkipped()
        {
            // Begründet angepasst (A-05): Ein abgelegtes Teil nimmt keinen Skill mehr weg. Verwaist ist nur eine Zeile
            // ohne Skill (bewusst herausgenommen) oder mit unbekannter Rune.
            var factory = BoardFactory.CreateDefault();
            var gear = new Equipment();
            gear.Equip(Items.Get("round_shield"));
            var rows = new[] { new BoardRowSpec("battle_start", SkillIds.ShieldBash), new BoardRowSpec("no_such_rune", SkillIds.ShieldBash) };

            LogicBoard board = factory.Create(rows, gear);
            Assert.IsFalse(board.Rows[0].IsOrphaned);
            Assert.IsTrue(board.Rows[1].IsOrphaned, "Unbekannte Rune bleibt als leere Zeile stehen.");

            gear.Unequip(EquipmentSlot.Shield);
            Assert.IsFalse(factory.Create(rows, gear).Rows[0].IsOrphaned, "Teil ablegen nimmt keinen Skill weg.");

            board = factory.Create(new[] { new BoardRowSpec("battle_start", null) }, gear);
            Assert.IsTrue(board.Rows[0].IsOrphaned);

            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 5, board: board), Fighter("B", 20, 1)));
            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.IsFalse(r.Events.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A" && e.Detail != SkillIds.BasicAttack));
        }

        [Test]
        public void CatalogsFitTogether()
        {
            foreach (EquipmentDefinition item in Items.All)
                foreach (SkillPassive passive in item.Passives)
                    Assert.IsTrue(Skills.All.Any(s => passive.Affects(s)), $"{item.Id}: {passive.Text} wirkt auf mindestens einen Skill");

            foreach (string set in new[] { SetIds.Overload, SetIds.Aegis, SetIds.Scrap, SetIds.Phantom })
            {
                IReadOnlyList<EquipmentDefinition> pieces = Items.SetItems(set);
                Assert.AreEqual(3, pieces.Count, set);
                Assert.AreEqual(3, pieces.Select(p => p.Slot).Distinct().Count(), $"{set}: alle Teile zusammen tragbar");
            }
        }

        [Test]
        public void KitsStartWithAWeaponForTheirStartRune()
        {
            var factory = BoardFactory.CreateDefault();
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                var gear = new Equipment();
                foreach (string id in kit.StartItemIds) Assert.IsNotNull(gear.Equip(Items.Get(id)), $"{kit.Id}: {id}");

                Assert.IsNotNull(gear.Get(EquipmentSlot.Weapon), kit.Id);
                LogicBoard board = factory.Create(new[] { new BoardRowSpec(kit.StartRuneId, kit.StartSkillId) }, gear);
                Assert.IsFalse(board.Rows[0].IsOrphaned, kit.Id);
            }
        }
    }
}
