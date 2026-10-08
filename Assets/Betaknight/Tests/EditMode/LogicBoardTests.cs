using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Runes;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class LogicBoardTests
    {
        private static readonly ConditionRegistry Registry = ConditionRegistry.CreateDefault();

        private static ICondition Rune(string id, int parameter = 0) => Registry.Create(id, parameter);

        internal static readonly SkillDefinition Repair = new SkillDefinition("repair", "Not-Reparatur", 4, 4, Ticks.FromSeconds(15),
            new ISkillEffect[] { new HealEffect(BasisPoints.Percent(25)) });

        internal static readonly SkillDefinition ShieldBash = new SkillDefinition("shield_bash", "Schildschlag", 4, 6, Ticks.FromSeconds(6),
            new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(80)), new StunEffect(Ticks.FromTenths(15)) }, countsAsAttack: true);

        /// <summary>Gegner-Skill mit sichtbarer Aufladung von 2 Sekunden.</summary>
        internal static readonly SkillDefinition HeavySmash = new SkillDefinition("heavy_smash", "Wuchtschlag", Ticks.FromSeconds(2), 10, Ticks.FromSeconds(5),
            new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(400)) }, countsAsAttack: true);

        private static LogicBoard Board(params LogicRow[] rows) => new LogicBoard(rows);

        private static LogicBoard SmashingEnemy() => Board(new LogicRow(AlwaysCondition.Instance, HeavySmash, "Immer"));

        [Test]
        public void MarcsExample_ShieldBashInterruptsChargeAndHealIsSkipped()
        {
            // Zeile 1: [HP < 30 %] -> Reparatur, Zeile 2: [Gegner lädt auf] -> Schildschlag, darunter Basisangriff.
            LogicBoard player = Board(
                new LogicRow(Rune("hp_low", 30), Repair, "HP Below 30 %"),
                new LogicRow(Rune("enemy_charging"), ShieldBash, "Enemy Charging"));

            BattleResult r = CombatSimulation.Run(Duel(
                Fighter("Ritter", 100, 5, board: player),
                Fighter("Brecher", 200, 3, board: SmashingEnemy())));

            var events = r.Events.ToList();
            BattleEvent charge = events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "heavy_smash");
            BattleEvent bash = events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "shield_bash");
            BattleEvent interrupted = events.First(e => e.Kind == BattleEventKind.ActionInterrupted && e.Detail == "heavy_smash");

            Assert.AreEqual(1, bash.RowIndex, "Zeile 2 feuert.");
            Assert.Greater(bash.Tick, charge.Tick);
            Assert.Less(interrupted.Tick, charge.Tick + Ticks.FromSeconds(2), "Abgebrochen, bevor der Wuchtschlag trifft.");
            Assert.IsFalse(events.Any(e => e.Detail == "repair" && e.Tick <= bash.Tick), "HP über 30 %, Zeile 1 wird übersprungen.");

            // Nach dem Schildschlag: 6 s Cooldown, dazwischen nur Basisangriffe.
            BattleEvent nextBash = events.FirstOrDefault(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "shield_bash" && e.Tick > bash.Tick);
            if (nextBash != null) Assert.GreaterOrEqual(nextBash.Tick - bash.Tick, Ticks.FromSeconds(6));
            Assert.IsTrue(events.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillDefinition.BasicAttackId
                                          && e.Source.Name == "Ritter" && e.Tick > bash.Tick));
        }

        [Test]
        public void WithoutTheBoardTheSmashLands()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("Ritter", 100, 5), Fighter("Brecher", 200, 3, board: SmashingEnemy())));
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.Hit && e.Detail == "heavy_smash"));
        }

        [Test]
        public void HigherRowsWinWhenBothAreMet()
        {
            var second = new SkillDefinition("second", "Zweite", 2, 2, Ticks.FromSeconds(1), new ISkillEffect[] { new DamageEffect(BasisPoints.Full) });
            var first = new SkillDefinition("first", "Erste", 2, 2, Ticks.FromSeconds(1), new ISkillEffect[] { new DamageEffect(BasisPoints.Full) });
            LogicBoard board = Board(new LogicRow(AlwaysCondition.Instance, first), new LogicRow(AlwaysCondition.Instance, second));

            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 1, board: board), Fighter("B", 30, 1, 1000)));

            var started = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A").ToList();
            Assert.AreEqual("first", started[0].Detail);
            // Während "first" auf Cooldown ist, darf "second" feuern.
            Assert.AreEqual("second", started[1].Detail);
        }

        [Test]
        public void SharedCooldownAcrossRows()
        {
            LogicBoard board = Board(new LogicRow(AlwaysCondition.Instance, Repair), new LogicRow(Rune("hp_full"), Repair));
            var p = Fighter("A", 100, 1, board: board);
            p.StartHp = 50;

            BattleResult r = CombatSimulation.Run(Duel(p, Fighter("B", 30, 1, 1000)));

            var repairs = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "repair").Select(e => e.Tick).ToList();
            for (int i = 1; i < repairs.Count; i++) Assert.GreaterOrEqual(repairs[i] - repairs[i - 1], Ticks.FromSeconds(15));
        }

        [Test]
        public void OrphanedRowsAreSkipped()
        {
            LogicBoard board = Board(new LogicRow(AlwaysCondition.Instance, null, "verwaist"));
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 5, board: board), Fighter("B", 10, 1, 1000)));

            Assert.IsTrue(r.IsVictory);
            Assert.IsTrue(r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A").All(e => e.RowIndex == 1));
        }

        [Test]
        public void HealRowFiresWhenLow()
        {
            LogicBoard board = Board(new LogicRow(Rune("hp_low", 30), Repair));
            var p = Fighter("A", 100, 1, board: board);
            p.StartHp = 20;

            BattleResult r = CombatSimulation.Run(Duel(p, Fighter("B", 5, 1, 1000)));

            BattleEvent heal = r.Events.First(e => e.Kind == BattleEventKind.Healed);
            Assert.AreEqual(25, heal.Amount);
        }

        [Test]
        public void StunCancelsAndBlocksActions()
        {
            LogicBoard board = Board(new LogicRow(AlwaysCondition.Instance, ShieldBash));
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 1, board: board), Fighter("B", 1000, 1)));

            BattleEvent stun = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun);
            Assert.IsFalse(r.Events.Any(e => e.Source?.Name == "B" && e.Kind == BattleEventKind.ActionStarted
                                             && e.Tick > stun.Tick && e.Tick < stun.Tick + Ticks.FromTenths(15)));
        }

        [Test]
        public void ChargingTargetIsTheChargingEnemy()
        {
            LogicBoard board = Board(new LogicRow(Rune("enemy_charging"), ShieldBash));
            var setup = Duel(Fighter("A", 300, 1, board: board), Fighter("Idle", 500, 1, 1000));
            setup.Enemies.Add(Fighter("Charger", 500, 1, board: SmashingEnemy()));

            BattleResult r = CombatSimulation.Run(setup);

            BattleEvent bash = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "shield_bash");
            Assert.AreEqual("Charger", bash.Target.Name);
        }

        [Test]
        public void StateConditionsRead()
        {
            var setup = Duel(Fighter("A", 100, 1), Fighter("B", 100, 1, armor: 10));
            setup.Context.OnGoldMine = true;
            var battle = new Battle(setup);
            var ctx = new ConditionContext(battle, battle.Player, battle.RowState(battle.Player, 0));

            Assert.IsTrue(Rune("enemy_armored").IsMet(ctx, out Combatant armored));
            Assert.AreSame(battle.Enemies[0], armored);
            Assert.IsTrue(Rune("hp_full").IsMet(ctx, out _));
            Assert.IsFalse(Rune("hp_low", 30).IsMet(ctx, out _));
            Assert.IsTrue(Rune("on_goldmine").IsMet(ctx, out _));
            Assert.IsFalse(Rune("vs_boss").IsMet(ctx, out _));
            Assert.IsTrue(Rune("last_enemy").IsMet(ctx, out _));
            Assert.IsFalse(Rune("outnumbered", 3).IsMet(ctx, out _));
            Assert.IsFalse(Rune("overheat").IsMet(ctx, out _));
        }

        [Test]
        public void RuneLevelsChangeParameters()
        {
            RuneDefinition hpLow = RuneCatalog.CreateDefault().Get("hp_low");
            Assert.AreEqual(30, hpLow.ParameterAt(0));
            Assert.AreEqual(50, hpLow.ParameterAt(2));
            Assert.AreEqual(50, hpLow.ParameterAt(9), "Über der Höchststufe bleibt es bei der Höchststufe.");
            Assert.AreEqual("HP Below 40 %", hpLow.NameAt(1));
        }

        [Test]
        public void ExclusiveRunesAreNeverOffered()
        {
            var catalog = RuneCatalog.CreateDefault();
            for (int seed = 0; seed < 200; seed++)
            {
                RuneOffer offer = RuneOffer.Create("Test", catalog, new RuneLoadout(), new System.Random(seed));
                Assert.IsFalse(offer.Options.Any(o => o.IsExclusive));
            }
        }
    }
}
