using System.Linq;
using Betaknight.Core.Arena;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Arena-Steuerung: zur nächsten Aktion springen; Warteschlange in Auslöse-Reihenfolge angezeigt.</summary>
    public class PlaybackControlTests
    {
        private static SkillDefinition Skill(string id, int cast) =>
            new SkillDefinition(id, id, cast, 1, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(100)) });

        private static BattleResult Run(LogicBoard board, int seconds)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: board), Fighter("B", 100000, 1, 1000));
            setup.MaxTicks = Ticks.FromSeconds(seconds);
            setup.TimeLimitTicks = setup.MaxTicks + 1;
            return CombatSimulation.Run(setup);
        }

        [Test]
        public void NextTickWhereFindsTheNextOwnAction()
        {
            BattleResult r = Run(new LogicBoard(new[] { new LogicRow(new ClockCondition(Ticks.FromSeconds(1)), Skill("ping", 2)) }), 4);
            var p = new BattlePlayback(r);
            int first = p.NextTickWhere(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "ping");
            Assert.AreEqual(r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "ping").Tick, first);
            p.Advance(first);
            int second = p.NextTickWhere(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == "ping" && e.Tick > first);
            Assert.Greater(second, first);
        }

        [Test]
        public void TheQueueIsShownInTriggerOrder()
        {
            // «b» (nach 1 s) wird vor «a» (nach 1,5 s) eingereiht, beide warten hinter dem 3-s-Cast «long»; die Anzeige folgt dem Einreihen.
            BattleResult r = Run(new LogicBoard(new[]
            {
                new LogicRow(new ClockCondition(Ticks.FromTenths(15)), Skill("a", 2)),
                new LogicRow(new ClockCondition(Ticks.FromSeconds(1)), Skill("b", 2)),
                new LogicRow(new BattleStartCondition(), Skill("long", Ticks.FromSeconds(3))),
            }), 4);
            var p = new BattlePlayback(r);
            p.Advance(Ticks.FromSeconds(2));
            Assert.GreaterOrEqual(p.Queue.Count, 2, "«b» und «a» warten hinter «long»");
            Assert.That(p.Queue.Select(q => q.SinceTick).ToList(), Is.Ordered, "in Auslöse-Reihenfolge");
            Assert.AreEqual(1, p.Queue[0].Row, "«b» (Zeile 2) kam zuerst, steht trotz späterer Lesereihenfolge vorne");
        }
    }
}
