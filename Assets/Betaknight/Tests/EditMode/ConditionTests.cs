using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Runes;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class ConditionTests
    {
        private static readonly ConditionRegistry Registry = ConditionRegistry.CreateDefault();

        private static LogicRow Row(string runeId, SkillDefinition skill, int parameter = 0) =>
            new LogicRow(Registry.Create(runeId, parameter), skill, runeId);

        /// <summary>Skill ohne Schaden, zählt nicht als Angriff. Zum Beobachten, wann ein Relais auslöst.</summary>
        private static SkillDefinition Ping(string id = "ping") =>
            new SkillDefinition(id, id, 1, 0, new ISkillEffect[0]);

        private static List<int> Starts(BattleResult r, string skillId) =>
            r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).Select(e => e.Tick).ToList();

        private static BattleResult Run(LogicBoard board, int enemyHp = 60, int enemyDamage = 1, int enemyInterval = 1000,
            System.Action<BattleSetup> tweak = null)
        {
            BattleSetup setup = Duel(Fighter("A", 200, 1, board: board), Fighter("B", enemyHp, enemyDamage, enemyInterval));
            tweak?.Invoke(setup);
            return CombatSimulation.Run(setup);
        }

        [Test]
        public void EveryRuneInTheCatalogHasACondition()
        {
            foreach (RuneDefinition rune in RuneCatalog.CreateDefault().All)
                for (int level = 0; level <= rune.MaxLevel; level++)
                    Assert.IsTrue(Registry.TryCreate(rune.Id, rune.ParameterAt(level), out _), $"{rune.Id} Stufe {level}");
        }

        [Test]
        public void EveryThirdAttack()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("every_3rd", Ping(), 3) }));

            List<int> attacks = r.Events.Where(e => e.Kind == BattleEventKind.ActionExecuted && e.Source.Name == "A" && e.Amount == 1)
                .Select(e => e.Tick).ToList();
            List<int> pings = Starts(r, "ping");

            Assert.Greater(pings.Count, 3);
            // Vor jedem Ping liegen genau 3 Angriffe mehr als vor dem vorherigen.
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(3 * (i + 1), attacks.Count(t => t < pings[i]));
        }

        [Test]
        public void ClockFiresEveryNSeconds()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("every_5s", Ping(), 5) }), enemyHp: 1000, tweak: s => s.TimeLimitTicks = 400);

            // Der Takt selbst läuft genau alle 5 s, unabhängig davon, wann der Ping drankommt.
            List<int> ticks = r.Events.Where(e => e.Kind == BattleEventKind.RelayTriggered && e.Source.Name == "A").Select(e => e.Tick).ToList();
            Assert.GreaterOrEqual(ticks.Count, 3);
            for (int i = 0; i < ticks.Count; i++) Assert.AreEqual(Ticks.FromSeconds(5) * (i + 1), ticks[i]);

            // Jedes Ticken startet genau einen Ping, sobald der Ritter frei ist (fällig bleibt fällig, er wartet in der Warteschlange).
            List<int> pings = Starts(r, "ping");
            Assert.That(pings.Count, Is.InRange(ticks.Count - 1, ticks.Count));
            for (int i = 0; i < pings.Count; i++)
                Assert.That(pings[i] - ticks[i], Is.InRange(0, Ticks.PerSecond), "Fällig bleibt fällig, feuert sobald frei.");
        }

        [Test]
        public void DueClockIsNotSwallowedByHigherRow()
        {
            // Eine frühere Komponente hält den Ritter beschäftigt (Takt 2 s, Cast 1,5 s); der fällige Takt geht trotzdem nicht verloren.
            var busy = new SkillDefinition("busy", "busy", 30, 0, new ISkillEffect[0]);
            BattleResult r = Run(new LogicBoard(new[] { Row("clock", busy, 2), Row("every_5s", Ping(), 5) }),
                enemyHp: 1000, tweak: s => s.TimeLimitTicks = 600);

            List<int> pings = Starts(r, "ping");
            Assert.IsNotEmpty(pings);
            Assert.GreaterOrEqual(pings[0], Ticks.FromSeconds(5));
        }

        [Test]
        public void OnHitFiresAfterHitsAndIsConsumed()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("on_hit", Ping()) }));

            List<int> hits = r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").Select(e => e.Tick).ToList();
            List<int> pings = Starts(r, "ping");

            Assert.IsNotEmpty(pings);
            Assert.LessOrEqual(pings.Count, hits.Count);
            foreach (int ping in pings) Assert.IsTrue(hits.Any(h => ping > h && ping - h <= EventCondition.DefaultWindow));
        }

        [Test]
        public void HealLoopDoesNotRecurseWithinATick()
        {
            var heal = new SkillDefinition("heal", "heal", 1, 0, new ISkillEffect[] { new HealEffect(100) });
            var setup = Duel(Fighter("A", 1000, 1, board: new LogicBoard(new[] { Row("after_heal", heal), Row("hp_low", heal, 99) })),
                Fighter("B", 100000, 3, 5));
            BattleResult r = CombatSimulation.Run(setup);

            Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome, "Kampf endet trotz Heil-Schleife.");
            var perTick = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted).GroupBy(e => (e.Tick, e.Source));
            Assert.IsTrue(perTick.All(g => g.Count() == 1), "Höchstens eine Aktion pro Kämpfer und Tick.");
        }

        [Test]
        public void ChainBuildsCombos()
        {
            BattleResult r = Run(new LogicBoard(new[]
            {
                Row("every_5s", Ping("a"), 5),
                Row("chain", Ping("b")),
                Row("chain", Ping("c")),
            }), enemyHp: 1000, tweak: s => s.TimeLimitTicks = 300);

            // Ein Takt startet die Kombo: a, dann b und c in Lesereihenfolge. Danach ketten sich b und c gegenseitig weiter
            // («Chain» löst nach jeder anderen Komponente aus), bis der Takt wieder a dazwischenschiebt.
            List<string> order = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A")
                .Select(e => e.Detail).Where(d => d != SkillDefinition.BasicAttackId).ToList();
            Assert.AreEqual(new[] { "a", "b", "c" }, order.Take(3).ToArray());

            // Jede fertige andere Komponente löst eine «Chain» genau einmal aus, nicht in mehreren Ticks hintereinander.
            List<BattleEvent> own = r.Events.Where(e => e.Source?.Name == "A").ToList();
            for (int relay = 1; relay <= 2; relay++)
            {
                int triggers = own.Count(e => e.Kind == BattleEventKind.RelayTriggered && e.Relay == relay);
                int others = own.Count(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail != SkillDefinition.BasicAttackId && e.RowIndex != relay);
                Assert.LessOrEqual(triggers, others, $"Relais {relay + 1}");
            }
        }

        [Test]
        public void ChainNeedsAnotherComponent()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("chain", Ping()) }));
            Assert.IsEmpty(Starts(r, "ping"));
        }

        [Test]
        public void BattleStartFiresOnce()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("battle_start", Ping()) }));
            Assert.AreEqual(new[] { 1 }, Starts(r, "ping").ToArray());
        }

        [Test]
        public void WhenHitTargetsTheAttacker()
        {
            var setup = Duel(Fighter("A", 200, 1, board: new LogicBoard(new[] { Row("when_hit", Ping()) })), Fighter("Idle", 100, 1, 1000));
            setup.Enemies.Add(Fighter("Attacker", 100, 1, 20));
            BattleResult r = CombatSimulation.Run(setup);

            BattleEvent ping = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "ping");
            Assert.AreEqual("Attacker", ping.Target.Name);
        }

        [Test]
        public void EveryNthHitTaken()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("every_nth_hit_taken", Ping(), 3) }), enemyHp: 500, enemyInterval: 10,
                tweak: s => s.TimeLimitTicks = 200);

            List<int> hitsTaken = r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Target.Name == "A").Select(e => e.Tick).ToList();
            List<int> pings = Starts(r, "ping");
            Assert.AreEqual(3, hitsTaken.Count(t => t < pings[0]));
        }

        [Test]
        public void AfterOwnSkillIgnoresBasicAttacks()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("every_5s", Ping("a"), 5), Row("after_own_skill", Ping("b")) }),
                enemyHp: 1000, tweak: s => s.TimeLimitTicks = 300);

            Assert.AreEqual(Starts(r, "a").Count, Starts(r, "b").Count);
        }

        [Test]
        public void ResourceRunesReadCounters()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("tempo_stacks", Ping(), 10) }),
                tweak: s => s.Player.Resources[ResourceIds.Tempo] = 10);
            Assert.IsNotEmpty(Starts(r, "ping"));

            r = Run(new LogicBoard(new[] { Row("tempo_stacks", Ping(), 10) }), tweak: s => s.Player.Resources[ResourceIds.Tempo] = 9);
            Assert.IsEmpty(Starts(r, "ping"));
        }

        [Test]
        public void BigHitTaken()
        {
            BattleResult small = Run(new LogicBoard(new[] { Row("big_hit_taken", Ping(), 15) }), enemyDamage: 20, enemyInterval: 20);
            BattleResult big = Run(new LogicBoard(new[] { Row("big_hit_taken", Ping(), 15) }), enemyDamage: 40, enemyInterval: 20);

            Assert.IsEmpty(Starts(small, "ping"), "20 von 200 HP = 10 %.");
            Assert.IsNotEmpty(Starts(big, "ping"), "40 von 200 HP = 20 %.");
        }

        [Test]
        public void EnemyDiesFiresAfterAKill()
        {
            var setup = Duel(Fighter("A", 200, 5, board: new LogicBoard(new[] { Row("enemy_dies", Ping()) })), Fighter("B", 5, 1, 1000));
            setup.Enemies.Add(Fighter("C", 100, 1, 1000));
            BattleResult r = CombatSimulation.Run(setup);

            int death = r.Events.First(e => e.Kind == BattleEventKind.Death).Tick;
            Assert.IsTrue(Starts(r, "ping").Any(t => t > death && t - death <= EventCondition.DefaultWindow));
        }
    }
}
