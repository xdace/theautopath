using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>A-13: Erfüllte Zeilen warten, bis sie dran sind – höhere Zeilen zuerst.</summary>
    public class QueueTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();

        /// <summary>Nie erfüllt: solche Zeilen feuern nur über Auslöser.</summary>
        private const string Never = "hp_low";

        private static SkillDefinition Skill(string id, int cast, int cooldown, int recovery = 0) =>
            new SkillDefinition(id, id, cast, recovery, cooldown, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) },
                countsAsAttack: true, kinds: SkillKind.Attack);

        private static LogicRow Row(string runeId, SkillDefinition skill, int difficulty = 0, int parameter = 0) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId, difficulty);

        /// <summary>Ritter mit viel HP gegen einen zähen Gegner, der alle <paramref name="enemyInterval"/> Ticks leicht trifft.</summary>
        private static BattleSetup Setup(LogicBoard board, int enemyInterval = 1000, int knightInterval = 20, RowQueueConfig queue = null, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, knightInterval, board: board), Fighter("B", 100000, 1, enemyInterval), seed);
            setup.Queue = queue ?? RowQueueConfig.Default;
            return setup;
        }

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static IEnumerable<BattleEvent> Own(BattleResult r) => r.Events.Where(e => e.Source?.Name == "A");

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            Own(r).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).ToList();

        private static List<BattleEvent> Queued(BattleResult r, int row) =>
            Own(r).Where(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == row).ToList();

        /// <summary>Höchstens <paramref name="max"/> Einträge pro Zeile: zwischen zwei Starts aus der Warteschlange stehen nie mehr Einreihungen.</summary>
        private static int MostWaitingAtOnce(BattleResult r, int row)
        {
            int waiting = 0, most = 0;
            foreach (BattleEvent e in Own(r).Where(e => e.RowIndex == row))
            {
                if (e.Kind == BattleEventKind.RowQueued) most = System.Math.Max(most, ++waiting);
                else if (e.Kind == BattleEventKind.ActionStarted && e.FromQueue) waiting--;
            }
            return most;
        }

        private static string Fingerprint(BattleResult r) => string.Join("\n", r.Events);

        // ------------------------------------------------------------------ Einreihen und Ausführen

        [Test]
        public void ARowOnCooldownIsQueuedAndRunsLater()
        {
            // «Wenn getroffen» kommt alle 0,5 s, der Skill hat 3 s Cooldown: Treffer während des Cooldowns reihen ihn ein.
            SkillDefinition a = Skill("a", 2, Ticks.FromSeconds(3));
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", a) }), enemyInterval: 10));

            List<BattleEvent> queued = Queued(r, 0);
            Assert.Greater(queued.Count, 0);
            Assert.IsTrue(queued.Any(e => e.Amount > 0), "eingereiht mit Rest-Cooldown");
            List<BattleEvent> starts = Starts(r, "a");
            List<BattleEvent> fromQueue = starts.Where(e => e.FromQueue).ToList();
            Assert.Greater(fromQueue.Count, 0);
            Assert.IsTrue(fromQueue.All(e => e.QueuedTicks > 0));
            // Startet, sobald der Cooldown abläuft; höchstens die Erholung eines Basisangriffs liegt dazwischen.
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i].Tick - starts[i - 1].Tick, Is.InRange(a.CooldownTicks, a.CooldownTicks + 20), $"Start {i}");
        }

        [Test]
        public void ALastingConditionFiresAfterEveryCooldown()
        {
            // «Immer» bleibt erfüllt: die Zeile reiht sich im Cooldown wieder ein und startet, sobald der Skill bereit ist.
            SkillDefinition a = Skill("a", 2, Ticks.FromSeconds(3));
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("always", a) })), 13);
            List<BattleEvent> starts = Starts(r, "a");
            Assert.GreaterOrEqual(starts.Count, 4);
            Assert.IsTrue(starts.Skip(1).All(e => e.FromQueue), "jede weitere Ausführung kommt aus der Warteschlange");
            Assert.IsTrue(Queued(r, 0).Any(e => e.Amount > 0), "eingereiht schon im Cooldown");
            for (int i = 1; i < starts.Count; i++)
                Assert.That(starts[i].Tick - starts[i - 1].Tick, Is.InRange(a.CooldownTicks, a.CooldownTicks + 20), $"Start {i}");

            // Als Daten abschaltbar: dann reiht im Cooldown nur ein neues Erfüllen ein (bereit und nur beschäftigt reiht weiter ein).
            BattleResult fresh = Run(Setup(new LogicBoard(new[] { Row("always", a) }), queue: new RowQueueConfig { OnlyNewFulfilmentDuringCooldown = true }), 13);
            Assert.IsTrue(Queued(fresh, 0).All(e => e.Amount == 0));
        }

        [Test]
        public void AnEventIsNotLostDuringALongCast()
        {
            // Der Ritter castet fast ununterbrochen (3 s); «Wenn getroffen» trifft ihn mitten im Cast.
            BattleResult Fight(RowQueueConfig queue) => Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, 0)),
                Row("always", Skill("long", Ticks.FromSeconds(3), 0)),
            }), enemyInterval: 30, queue: queue));

            BattleResult r = Fight(null);
            List<BattleEvent> starts = Starts(r, "a");
            Assert.Greater(starts.Count, 0);
            Assert.IsTrue(starts.All(e => e.FromQueue && e.Cause == ActionCause.Board));
            Assert.IsTrue(Queued(r, 0).All(e => e.Amount == 0), "bereit, nur der Ritter war beschäftigt");

            // Ohne Warteschlange ging der Treffer verloren.
            Assert.Less(Starts(Fight(RowQueueConfig.Off), "a").Count, starts.Count);
        }

        [Test]
        public void PriorityDecidesBetweenTwoReadyQueuedRows()
        {
            // Zeile 2 (jede Sekunde) wartet länger als Zeile 1 (getroffen), trotzdem startet Zeile 1 zuerst.
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, Ticks.FromSeconds(10))),
                Row("every_5s", Skill("b", 2, Ticks.FromSeconds(10)), parameter: 1),
                Row("battle_start", Skill("long", Ticks.FromSeconds(3), 0)),
            }), enemyInterval: 30), 6);

            Assert.Less(Queued(r, 1)[0].Tick, Queued(r, 0)[0].Tick, "Zeile 2 wurde früher eingereiht");
            BattleEvent a = Starts(r, "a")[0], b = Starts(r, "b")[0];
            Assert.IsTrue(a.FromQueue && b.FromQueue);
            Assert.Less(a.Tick, b.Tick, "höhere Zeile zuerst");
            Assert.Greater(b.QueuedTicks, a.QueuedTicks);
        }

        [Test]
        public void ATriggerOnABusyTargetIsQueued()
        {
            // Der Auslöser kommt bei der Ausführung des Stichs; danach erholt sich der Ritter noch, der Bohrer wartet.
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) });
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("always", Skill("stab", 4, Ticks.FromSeconds(3), recovery: 10)),
                Row(Never, Skill("drill", 4, 0)),
            }, null, graph)), 10);

            Assert.IsEmpty(r.Events.Where(e => e.Kind == BattleEventKind.TriggerExpired));
            List<BattleEvent> queued = Queued(r, 1);
            Assert.Greater(queued.Count, 0);
            Assert.IsTrue(queued.All(e => e.IsTriggered && e.CauseRow == 0 && e.Amount == 0));
            List<BattleEvent> drills = Starts(r, "drill");
            Assert.AreEqual(queued.Count, drills.Count);
            Assert.IsTrue(drills.All(e => e.FromQueue && e.IsTriggered && e.CauseRow == 0));
        }

        [Test]
        public void EachRowIsQueuedAtMostOnce()
        {
            // Viele Treffer während eines langen Casts: die Zeile steht trotzdem nur einmal in der Warteschlange.
            LogicBoard Board() => new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, Ticks.FromSeconds(2))),
                Row("always", Skill("long", Ticks.FromSeconds(3), 0)),
            });
            BattleResult r = Run(Setup(Board(), enemyInterval: 7));
            Assert.Greater(Queued(r, 0).Count, 1);
            Assert.AreEqual(1, MostWaitingAtOnce(r, 0));

            // Als Daten: mit zwei erlaubten Einträgen kann sie doppelt warten.
            BattleResult twice = Run(Setup(Board(), enemyInterval: 7, queue: new RowQueueConfig { MaxEntriesPerRow = 2 }));
            Assert.AreEqual(2, MostWaitingAtOnce(twice, 0));
        }

        [Test]
        public void TheBasicAttackFillsTheGapAndIsInterrupted()
        {
            // Langsamer Basisangriff (3 s) füllt den Cooldown; wird der eingereihte Skill bereit, bricht er ab.
            SkillDefinition a = Skill("a", 2, Ticks.FromSeconds(2));
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", a) }), enemyInterval: 7, knightInterval: 60));

            List<BattleEvent> own = Own(r).ToList();
            int interrupted = 0;
            for (int i = 0; i < own.Count; i++)
            {
                BattleEvent e = own[i];
                if (e.Kind != BattleEventKind.ActionStarted || e.Detail != "a" || !e.FromQueue) continue;
                BattleEvent before = own.Take(i).LastOrDefault(x => x.Kind == BattleEventKind.ActionStarted);
                if (before == null || before.Detail != SkillDefinition.BasicAttackId) continue;
                Assert.IsTrue(own.Any(x => x.Kind == BattleEventKind.ActionInterrupted && x.Tick == e.Tick), $"Abbruch bei Tick {e.Tick}");
                interrupted++;
            }
            Assert.Greater(interrupted, 0, "Basisangriff hat gefüllt und wurde abgebrochen");
            Assert.Greater(own.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillDefinition.BasicAttackId), 0);
        }

        // ------------------------------------------------------------------ Bonus und Auslöser-Ursprung

        [Test]
        public void TheBonusOfTheEarningRowIsKept()
        {
            // Eine schwere Zeile (◆◆◆) behält ihren Bonus, auch wenn sie aus der Warteschlange startet.
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, Ticks.FromSeconds(3)), difficulty: 3),
            }), enemyInterval: 10));
            List<BattleEvent> fromQueue = Starts(r, "a").Where(e => e.FromQueue).ToList();
            Assert.Greater(fromQueue.Count, 0);
            Assert.IsTrue(fromQueue.All(e => e.Tier == 3));

            // Über einen Auslöser: die schwere auslösende Zeile gibt ihre Stufe der leichten Zielzeile mit.
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) });
            BattleResult t = Run(Setup(new LogicBoard(new[]
            {
                Row("always", Skill("stab", 4, Ticks.FromSeconds(3), recovery: 10), difficulty: 3),
                Row(Never, Skill("drill", 4, Ticks.FromSeconds(1))),
            }, null, graph)), 10);
            List<BattleEvent> drills = Starts(t, "drill");
            Assert.Greater(drills.Count, 0);
            Assert.IsTrue(drills.All(e => e.FromQueue && e.Tier == 3));
        }

        // ------------------------------------------------------------------ Stabilität

        [Test]
        public void SameSeedsGiveTheSameFights()
        {
            SkillCatalog skills = SkillCatalog.CreateDefault();
            BattleSetup Fight(int seed) => Setup(new LogicBoard(new[]
            {
                Row("when_hit", skills.Get(SkillIds.ShockStab)),
                Row("always", skills.Get(SkillIds.Drill)),
            }), enemyInterval: 13, seed: seed);

            BattleResult r = Run(Fight(4));
            Assert.Greater(r.Events.Count(e => e.Kind == BattleEventKind.RowQueued), 0);
            Assert.AreEqual(Fingerprint(r), Fingerprint(Run(Fight(4))));
        }

        [Test]
        public void ACircleOfTriggersStaysStable()
        {
            // Stich löst Schlag aus, Schlag löst Stich aus: jede Zeile wartet höchstens einmal, der Kampf bleibt endlich.
            LogicBoard Circle()
            {
                var edges = new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)), new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)) };
                return new LogicBoard(new[]
                {
                    Row("battle_start", Skill("stab", 4, Ticks.FromSeconds(3), recovery: 4)),
                    Row(Never, Skill("bash", 6, Ticks.FromSeconds(2), recovery: 4)),
                }, null, new LogicGraph(edges));
            }

            BattleResult r = Run(Setup(Circle()), 30);
            Assert.AreEqual(BattleEventKind.BattleEnd, r.Events.Last().Kind);
            Assert.AreEqual(1, MostWaitingAtOnce(r, 0));
            Assert.AreEqual(1, MostWaitingAtOnce(r, 1));
            List<BattleEvent> stabs = Starts(r, "stab");
            for (int i = 1; i < stabs.Count; i++)
                Assert.GreaterOrEqual(stabs[i].Tick - stabs[i - 1].Tick, Ticks.FromSeconds(3), "nicht schneller als der Cooldown");
            Assert.Greater(Starts(r, "bash").Count, 5, "der Kreis läuft weiter");
            Assert.AreEqual(Fingerprint(r), Fingerprint(Run(Setup(Circle()), 30)));
        }

        [Test]
        public void TheQueueIsEmptiedAtTheEndOfTheFight()
        {
            BattleSetup setup = Setup(new LogicBoard(new[] { Row("when_hit", Skill("a", 2, Ticks.FromSeconds(30))) }), enemyInterval: 10);
            setup.TimeLimitTicks = Ticks.FromSeconds(5);
            setup.MaxTicks = Ticks.FromSeconds(10);
            var battle = new Battle(setup);
            BattleResult r = battle.Run();

            Assert.Greater(Queued(r, 0).Count, 0, "am Ende wartete noch etwas");
            Assert.IsEmpty(battle.Player.Queue);
            Assert.IsTrue(battle.Enemies.All(e => e.Queue.Count == 0));
        }

        [Test]
        public void TheReportCountsQueuedRowsAndTheirWait()
        {
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", Skill("a", 2, Ticks.FromSeconds(3))) }), enemyInterval: 10));
            RowReport row = BattleReport.Create(r).Rows[0];

            Assert.AreEqual(Queued(r, 0).Count, row.Queued);
            Assert.Greater(row.StartedFromQueue, 0);
            Assert.Greater(row.AverageWaitTicks, 0);
            StringAssert.Contains("× eingereiht, Ø ", row.QueueText);
            StringAssert.EndsWith(" gewartet", row.QueueText);
        }

        [Test]
        public void ThePlaybackShowsTheQueueNextToTheBoard()
        {
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, Ticks.FromSeconds(3))),
                Row("always", Skill("long", Ticks.FromSeconds(3), 0)),
            }), enemyInterval: 30), 10);
            BattleEvent queued = Queued(r, 0)[0];

            var playback = new BattlePlayback(r);
            playback.Advance(queued.Tick);
            Assert.IsTrue(playback.IsRowQueued(0));
            Assert.AreEqual(RowDisplay.Queued, playback.RowStateAt(0));
            StringAssert.StartsWith("Wartet: 1. a ", playback.QueueText());
            Assert.IsTrue(playback.Lines.Any(l => l.Contains("Zeile 1 (a) eingereiht")), string.Join("\n", playback.Lines));

            BattleEvent start = Starts(r, "a").First(e => e.FromQueue);
            playback.Advance(start.Tick - playback.Tick);
            Assert.IsFalse(playback.IsRowQueued(0));
            Assert.IsTrue(playback.Lines.Last(l => l.Contains("] → a")).Contains("aus der Warteschlange nach"));

            playback.SkipToEnd();
            Assert.AreEqual(string.Empty, playback.QueueText());
        }

        [Test]
        public void TheDecisionLogSaysQueuedInsteadOfSkipped()
        {
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2, Ticks.FromSeconds(1))),
                Row("always", Skill("long", Ticks.FromSeconds(3), 0)),
            }), enemyInterval: 30), 10);

            Assert.IsTrue(r.Decisions.Any(d => d.Queued(0)), "wartende Zeile steht als eingereiht im Protokoll");
            foreach (BattleDecision d in r.Decisions.Where(d => d.Queued(0)))
            {
                Assert.IsFalse(d.IsBusy ? false : d.ChosenRow == 0);
                StringAssert.StartsWith("queued", RowStateText.Reason(d.Rows[0]));
            }
        }
    }
}
