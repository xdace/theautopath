using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class ArenaSimulationTests
    {
        internal static CombatantSetup Fighter(string name, int hp, int damage, int interval = 20, int armor = 0, LogicBoard board = null) =>
            new CombatantSetup { Name = name, Stats = new CombatStats(hp, damage, interval, armor), Board = board };

        internal static BattleSetup Duel(CombatantSetup player, CombatantSetup enemy, int seed = 1) =>
            new BattleSetup { Player = player, Enemies = new List<CombatantSetup> { enemy }, Seed = seed };

        [Test]
        public void SameSetupAndSeedGiveIdenticalLog()
        {
            string Log() => string.Join("\n", CombatSimulation.Run(Duel(Fighter("A", 40, 5), Fighter("B", 35, 4, 17), 9)).Events);

            Assert.AreEqual(Log(), Log());
        }

        [Test]
        public void BasicAttacksFollowAttackInterval()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 5), Fighter("B", 20, 1, 1000)));

            List<BattleEvent> hits = r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").ToList();
            Assert.AreEqual(4, hits.Count);
            // Ausholen 13 Ticks (2/3 von 20), danach alle 20 Ticks ein Treffer.
            Assert.AreEqual(14, hits[0].Tick);
            Assert.AreEqual(20, hits[1].Tick - hits[0].Tick);
            Assert.IsTrue(r.IsVictory);
            Assert.AreEqual(hits[3].Tick, r.EndTick);
        }

        [Test]
        public void AttackSpeedShortensInterval()
        {
            CombatantSetup fast = Fighter("A", 100, 1);
            fast.Stats[StatKind.AttackSpeed] = BasisPoints.Full; // +100 %
            BattleResult r = CombatSimulation.Run(Duel(fast, Fighter("B", 5, 1, 1000)));

            List<int> ticks = r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").Select(e => e.Tick).ToList();
            Assert.AreEqual(10, ticks[1] - ticks[0]);
        }

        [Test]
        public void AttackSpeedNeverGoesBelowOneTick()
        {
            CombatantSetup absurd = Fighter("A", 100, 1);
            absurd.Stats[StatKind.AttackSpeed] = 10_000_000;
            BattleResult r = CombatSimulation.Run(Duel(absurd, Fighter("B", 50, 1, 1000)));

            List<int> ticks = r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").Select(e => e.Tick).ToList();
            Assert.IsTrue(r.IsVictory);
            Assert.IsTrue(ticks.Zip(ticks.Skip(1), (a, b) => b - a).All(d => d >= 1));
            Assert.AreEqual(ticks.Count, ticks.Distinct().Count(), "Höchstens ein Angriff pro Tick.");
        }

        [Test]
        public void ArmorFormula()
        {
            Assert.AreEqual(10, Defense.ApplyArmor(10, 0));
            Assert.AreEqual(5, Defense.ApplyArmor(10, 100));
            Assert.AreEqual(6, Defense.ApplyArmor(10, 50));
            Assert.AreEqual(1, Defense.ApplyArmor(1, 1000), "Nie unter 1.");
        }

        [Test]
        public void ArmorReducesHits()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 10), Fighter("B", 10, 1, 1000, armor: 100)));
            Assert.IsTrue(r.Events.Where(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").All(e => e.Amount == 5));
        }

        [Test]
        public void StalemateEndsThroughOverheat()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 5000, 1, armor: 1000), Fighter("B", 5000, 1, armor: 1000)));

            Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome);
            Assert.Greater(r.EndTick, Ticks.FromSeconds(90));
            Assert.Less(r.EndTick, 10000);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.Overheat));
            Assert.IsFalse(r.Events.Any(e => e.Kind == BattleEventKind.Overheat && e.Tick <= Ticks.FromSeconds(90)));
        }

        [Test]
        public void DefeatWhenPlayerDies()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 5, 1), Fighter("B", 100, 10)));
            Assert.AreEqual(BattleOutcome.Defeat, r.Outcome);
            Assert.AreEqual(0, r.PlayerHp);
        }

        [Test]
        public void PlayerActsFirstWithinATick()
        {
            // Beide würden sich im selben Tick töten. Der Spieler ist zuerst dran, der Gegner kommt nicht mehr zum Zug.
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 5, 10), Fighter("B", 5, 10)));
            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.AreEqual(5, r.PlayerHp);
        }

        [Test]
        public void DeathFromOverheatOnBothSidesIsADefeat()
        {
            BattleResult r = CombatSimulation.Run(Duel(Fighter("A", 100, 0), Fighter("B", 100, 0)));
            Assert.AreEqual(BattleOutcome.Defeat, r.Outcome);
        }

        [Test]
        public void StartHpIsUsed()
        {
            CombatantSetup hurt = Fighter("A", 50, 5);
            hurt.StartHp = 7;
            var battle = new Battle(Duel(hurt, Fighter("B", 10, 1)));
            Assert.AreEqual(7, battle.Player.Hp);
            Assert.AreEqual(50, battle.Player.MaxHp);
        }

        [Test]
        public void AllEnemiesMustFall()
        {
            var setup = Duel(Fighter("A", 100, 5), Fighter("B", 10, 1));
            setup.Enemies.Add(Fighter("C", 10, 1));

            BattleResult r = CombatSimulation.Run(setup);

            Assert.IsTrue(r.IsVictory);
            Assert.AreEqual(2, r.EnemiesDefeated);
        }

        private sealed class BurnOnHit : BattleModifier
        {
            public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
            {
                if (e.Kind == BattleEventKind.Hit && e.Source == owner)
                    battle.ResolveHit(HitInfo.SelfDamage(owner, 1, "test"));
            }
        }

        [Test]
        public void ModifiersReactOneTickLater()
        {
            CombatantSetup player = Fighter("A", 100, 1);
            player.Modifiers.Add(new BurnOnHit());

            BattleResult r = CombatSimulation.Run(Duel(player, Fighter("B", 3, 1, 1000)));

            int firstHit = r.Events.First(e => e.Kind == BattleEventKind.Hit && e.Source.Name == "A").Tick;
            int firstSelf = r.Events.First(e => e.Kind == BattleEventKind.SelfDamage).Tick;
            Assert.AreEqual(firstHit + 1, firstSelf);
        }
    }
}
