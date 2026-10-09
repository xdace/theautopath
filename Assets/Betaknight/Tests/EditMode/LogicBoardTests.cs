using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Runes;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Platinen im Kampf (A-19): Relais lösen aus, Komponenten feuern in Lesereihenfolge, keine Cooldowns.</summary>
    public class LogicBoardTests
    {
        private static readonly ConditionRegistry Registry = ConditionRegistry.CreateDefault();

        private static ICondition Rune(string id, int parameter = 0) => Registry.Create(id, parameter);

        internal static readonly SkillDefinition Repair = new SkillDefinition("repair", "Not-Reparatur", 4, 4,
            new ISkillEffect[] { new HealEffect(BasisPoints.Percent(25)) });

        internal static readonly SkillDefinition ShieldBash = new SkillDefinition("shield_bash", "Schildschlag", 4, 6,
            new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(80)), new StunEffect(Ticks.FromTenths(15)) }, countsAsAttack: true);

        /// <summary>Gegner-Skill mit sichtbarer Aufladung von 2 Sekunden.</summary>
        internal static readonly SkillDefinition HeavySmash = new SkillDefinition("heavy_smash", "Wuchtschlag", Ticks.FromSeconds(2), 10,
            new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(400)) }, countsAsAttack: true);

        private static LogicBoard Board(params LogicRow[] rows) => new LogicBoard(rows);

        /// <summary>Gegner, der alle 5 s den Wuchtschlag auflädt (Takt-Relais statt «Immer» mit Cooldown).</summary>
        private static LogicBoard SmashingEnemy() => EnemyBoard.Build(new EnemyPart(new ClockCondition(Ticks.FromSeconds(5)), "Every 5 s", HeavySmash));

        [Test]
        public void MarcsExample_ShieldBashInterruptsChargeAndHealIsSkipped()
        {
            // Komponente 1: [HP < 30 %] -> Reparatur, Komponente 2: [Gegner lädt auf] -> Schildschlag, dazu der Basisangriff.
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

            Assert.AreEqual(1, bash.RowIndex, "Komponente 2 feuert.");
            Assert.Greater(bash.Tick, charge.Tick);
            Assert.Less(interrupted.Tick, charge.Tick + Ticks.FromSeconds(2), "Abgebrochen, bevor der Wuchtschlag trifft.");
            Assert.IsFalse(events.Any(e => e.Detail == "repair" && e.Tick <= bash.Tick), "HP über 30 %, Komponente 1 löst nicht aus.");

            // Kein Cooldown, aber ein Zustand löst nur bei der steigenden Flanke aus: der nächste Schildschlag kommt erst,
            // wenn der Gegner erneut auflädt; dazwischen nur Basisangriffe.
            BattleEvent nextBash = events.FirstOrDefault(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "shield_bash" && e.Tick > bash.Tick);
            if (nextBash != null)
                Assert.IsTrue(events.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "heavy_smash" && e.Tick > bash.Tick && e.Tick <= nextBash.Tick),
                    "erst eine neue Aufladung löst wieder aus");
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
        public void EarlierComponentsWinWhenBothAreTriggered()
        {
            var second = new SkillDefinition("second", "Zweite", 2, 2, new ISkillEffect[] { new DamageEffect(BasisPoints.Full) });
            var first = new SkillDefinition("first", "Erste", 2, 2, new ISkillEffect[] { new DamageEffect(BasisPoints.Full) });
            LogicBoard board = Board(new LogicRow(Rune("battle_start"), first), new LogicRow(Rune("battle_start"), second));

            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 1, board: board), Fighter("B", 30, 1, 1000)));

            var started = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A").ToList();
            Assert.AreEqual("first", started[0].Detail, "beide lösen im selben Tick aus, die frühere in Lesereihenfolge startet zuerst");
            // Die zweite wartet in der Warteschlange und startet direkt danach, ohne dass der Basisangriff dazwischenkommt.
            Assert.AreEqual("second", started[1].Detail);
            Assert.IsTrue(started[1].FromQueue && started[1].QueuedTicks > 0);
        }

        [Test]
        public void AComponentPoweredByTwoRelaysIsQueuedOnlyOnce()
        {
            // Zwei Relais links und rechts derselben Komponente lösen im selben Tick aus: sie wartet trotzdem nur einmal.
            var mend = new SkillDefinition("mend", "Flicken", 4, 4, new ISkillEffect[] { new HealEffect(BasisPoints.Percent(10)) });
            LogicBoard board = LogicBoard.Compile(new BoardLayout(3, 1, null),
                new[] { new LogicRow(mend, new CellRect(1, 0)) },
                new[] { new LogicRelay(Rune("battle_start"), "Links", rect: new CellRect(0, 0)), new LogicRelay(Rune("battle_start"), "Rechts", rect: new CellRect(2, 0)) });
            Assert.AreEqual(2, board.Rows[0].Relays.Count);

            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 1, board: board), Fighter("B", 30, 1, 1000)));
            List<BattleEvent> own = r.Events.Where(e => e.Source?.Name == "A").ToList();
            Assert.AreEqual(2, own.Count(e => e.Kind == BattleEventKind.RelayTriggered));
            Assert.AreEqual(1, own.Count(e => e.Kind == BattleEventKind.RowQueued));
            BattleEvent missed = own.Single(e => e.Kind == BattleEventKind.TriggerMissed);
            Assert.AreEqual((int)MissReason.AlreadyQueued, missed.Amount);
            Assert.AreEqual(1, missed.Relay, "das zweite Relais verpasst");
            Assert.AreEqual(1, own.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "mend"));
        }

        [Test]
        public void OrphanedComponentsNeverFire()
        {
            LogicBoard board = Board(new LogicRow(Rune("battle_start"), null, "verwaist"));
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 5, board: board), Fighter("B", 10, 1, 1000)));

            Assert.IsTrue(r.IsVictory);
            Assert.IsTrue(r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A").All(e => e.RowIndex == 1));
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.TriggerMissed && e.Amount == (int)MissReason.Orphaned), "das Auslösen verpufft");
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
            LogicBoard board = Board(new LogicRow(Rune("battle_start"), ShieldBash));
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
            var ctx = new ConditionContext(battle, battle.Player, new RowRuntime(0));

            Assert.IsTrue(Rune("enemy_armored").IsMet(ctx, out Combatant armored));
            Assert.AreSame(battle.Enemies[0], armored);
            Assert.IsTrue(Rune("hp_full").IsMet(ctx, out _));
            Assert.IsFalse(Rune("hp_low", 30).IsMet(ctx, out _));
            Assert.IsTrue(Rune("on_goldmine").IsMet(ctx, out _));
            Assert.IsFalse(Rune("vs_boss").IsMet(ctx, out _));
            Assert.IsTrue(Rune("last_enemy").IsMet(ctx, out _));
            Assert.IsFalse(Rune("outnumbered", 3).IsMet(ctx, out _));
            Assert.IsFalse(Rune("overtime", 4).IsMet(ctx, out _));
            Assert.IsTrue(Rune("opening", 3).IsMet(ctx, out _));
            Assert.IsFalse(Rune("backlog", 2).IsMet(ctx, out _));
        }

        [Test]
        public void RuneLevelsChangeParameters()
        {
            RuneDefinition hpLow = RuneCatalog.CreateDefault().Get("hp_low");
            Assert.AreEqual(20, hpLow.ParameterAt(0));
            Assert.AreEqual(25, hpLow.ParameterAt(1));
            Assert.AreEqual(25, hpLow.ParameterAt(9), "Über der Höchststufe bleibt es bei der Höchststufe.");
            Assert.AreEqual("HP Below 25 %", hpLow.NameAt(1));
        }

        [Test]
        public void ExclusiveRunesAreNeverOffered()
        {
            var catalog = RuneCatalog.CreateDefault();
            for (int seed = 0; seed < 200; seed++)
            {
                RuneOffer offer = RuneOffer.Create("Test", catalog, new CircuitBoard(), new System.Random(seed));
                Assert.IsFalse(offer.Options.Any(o => o.IsExclusive));
            }
        }
    }
}
