using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// A-13/A-19: Ausgelöste Komponenten warten, bis sie dran sind – in Lesereihenfolge (oben links zuerst). Jede Komponente
    /// steht höchstens einmal in der Warteschlange; weitere Auslöser sind «Missed Triggers». Keine Cooldowns.
    /// </summary>
    public class QueueTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();

        /// <summary>Nie erfüllt: solche Komponenten feuern nur über Auslöser-Module.</summary>
        private const string Never = "hp_low";

        private static SkillDefinition Skill(string id, int cast, int recovery = 0) =>
            new SkillDefinition(id, id, cast, recovery, new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(10)) },
                countsAsAttack: true, kinds: SkillKind.Attack);

        private static LogicRow Row(string runeId, SkillDefinition skill, int difficulty = 0, int parameter = 0, bool repeatWhileTrue = false) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId, difficulty, repeatWhileTrue);

        /// <summary>Takt-Relais: löst alle <paramref name="seconds"/> s aus.</summary>
        private static LogicRow Clock(int seconds, SkillDefinition skill, int difficulty = 0) => Row("clock", skill, difficulty, seconds);

        /// <summary>Ritter mit viel HP gegen einen zähen Gegner, der alle <paramref name="enemyInterval"/> Ticks leicht trifft.</summary>
        private static BattleSetup Setup(LogicBoard board, int enemyInterval = 1000, int knightInterval = 20, QueueConfig queue = null, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, knightInterval, board: board), Fighter("B", 100000, 1, enemyInterval), seed);
            setup.Queue = queue ?? QueueConfig.Default;
            return setup;
        }

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            // Ohne Thermal Throttling (A-21): die Tests messen reine Cast-Zeiten.
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            setup.TimeLimitTicks = setup.MaxTicks + 1;
            return CombatSimulation.Run(setup);
        }

        private static IEnumerable<BattleEvent> Own(BattleResult r) => r.Events.Where(e => e.Source?.Name == "A");

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            Own(r).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).ToList();

        private static List<BattleEvent> Queued(BattleResult r, int row) =>
            Own(r).Where(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == row).ToList();

        private static List<BattleEvent> Missed(BattleResult r, int row, MissReason reason) =>
            Own(r).Where(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == row && e.Amount == (int)reason).ToList();

        private static int Hits(BattleResult r) => r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Source?.Name == "B");

        /// <summary>Höchstens so viele Einträge pro Komponente: zwischen zwei Starts aus der Warteschlange stehen nie mehr Einreihungen.</summary>
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

        /// <summary>Zustand, der nur in den angegebenen Tick-Fenstern [von, bis) gilt – für Flanken-Tests.</summary>
        private sealed class WindowCondition : ICondition
        {
            private readonly (int from, int to)[] _windows;

            public WindowCondition(params (int from, int to)[] windows) => _windows = windows;

            public bool IsMet(in ConditionContext context, out Combatant target)
            {
                target = null;
                int tick = context.Tick;
                return _windows.Any(w => tick >= w.from && tick < w.to);
            }
        }

        // ------------------------------------------------------------------ Einreihen und Ausführen

        [Test]
        public void ATriggeredComponentIsQueuedAndRunsFromTheQueue()
        {
            // «When Hit» kommt jede Sekunde: jeder Treffer reiht den Skill über das Relais ein, er startet aus der Warteschlange.
            SkillDefinition a = Skill("a", 2);
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", a) }), enemyInterval: 20));

            List<BattleEvent> queued = Queued(r, 0);
            Assert.Greater(queued.Count, 5);
            Assert.IsTrue(queued.All(e => e.Relay == 0 && e.Cause == ActionCause.Board), "eingereiht vom eigenen Relais");
            List<BattleEvent> starts = Starts(r, "a");
            Assert.IsTrue(starts.All(e => e.FromQueue && e.Relay == 0));
            Assert.That(starts.Count, Is.InRange(queued.Count - 1, queued.Count), "jede Einreihung führt zu genau einem Start");

            // Kein Cooldown: der Skill feuert bei jedem Treffer, nichts geht verloren.
            Assert.That(starts.Count, Is.InRange(Hits(r) - 1, Hits(r)));
            Assert.IsEmpty(Own(r).Where(e => e.Kind == BattleEventKind.TriggerMissed));
        }

        [Test]
        public void ALastingStateTriggersOnceButRepeatWhileTrueQueuesAgain()
        {
            // «HP Full» bleibt erfüllt (der Gegner trifft nie): ohne Modul genau eine Einreihung (steigende Flanke).
            SkillDefinition a = Skill("a", 10, 2);
            BattleResult once = Run(Setup(new LogicBoard(new[] { Row("hp_full", a) })), 10);
            Assert.AreEqual(1, Queued(once, 0).Count);
            Assert.AreEqual(1, Starts(once, "a").Count);

            // Mit «Repeat while true» reiht sich die Komponente nach jeder Ausführung wieder ein und startet ohne Wartezeit.
            BattleResult repeat = Run(Setup(new LogicBoard(new[] { Row("hp_full", a, repeatWhileTrue: true) })), 10);
            List<BattleEvent> starts = Starts(repeat, "a");
            Assert.Greater(starts.Count, 10);
            Assert.AreEqual(1, Own(repeat).Count(e => e.Kind == BattleEventKind.RelayTriggered), "das Relais selbst löst nur einmal aus");
            Assert.IsTrue(starts.All(e => e.FromQueue));
            for (int i = 1; i < starts.Count; i++)
                Assert.AreEqual(a.WindupTicks + a.RecoveryTicks, starts[i].Tick - starts[i - 1].Tick, $"Start {i}: nur die Cast-Zeit trennt die Ausführungen");
        }

        [Test]
        public void AStateTriggersAgainAfterItWasFalse()
        {
            // Zwei Fenster, in denen der Zustand gilt: genau zwei Auslösungen (je steigende Flanke), nicht eine pro Tick.
            var board = new LogicBoard(new[] { new LogicRow(new WindowCondition((20, 60), (100, 140)), Skill("a", 2), "window") });
            BattleResult r = Run(Setup(board), 10);
            List<BattleEvent> relay = Own(r).Where(e => e.Kind == BattleEventKind.RelayTriggered).ToList();
            CollectionAssert.AreEqual(new[] { 20, 100 }, relay.Select(e => e.Tick).ToList());
            Assert.AreEqual(2, Starts(r, "a").Count);
            Assert.IsEmpty(Own(r).Where(e => e.Kind == BattleEventKind.TriggerMissed), "kein Auslösen, solange der Zustand nur anhält");
        }

        [Test]
        public void AnEventIsNotLostDuringALongCast()
        {
            // Der Ritter castet fast ununterbrochen (Takt 3 s, Cast 3 s); «When Hit» trifft ihn mitten im Cast.
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2)),
                Clock(3, Skill("long", Ticks.FromSeconds(3))),
            }), enemyInterval: 30));

            List<BattleEvent> starts = Starts(r, "a");
            Assert.Greater(starts.Count, 0);
            Assert.IsTrue(starts.All(e => e.FromQueue && e.Cause == ActionCause.Board));
            Assert.IsTrue(starts.Any(e => e.QueuedTicks > 0), "wartete auf das Ende des langen Casts");

            // Jeder Treffer kommt an: eingereiht oder als «schon eingereiht» verpasst – keiner verschwindet still.
            int reached = Queued(r, 0).Count + Missed(r, 0, MissReason.AlreadyQueued).Count;
            Assert.That(reached, Is.InRange(Hits(r) - 1, Hits(r)));
        }

        [Test]
        public void TheQueueRunsInTheOrderOfTriggering()
        {
            // Komponente 2 (jede Sekunde) wird vor Komponente 1 (getroffen) eingereiht, beide warten hinter dem 3-s-Cast:
            // wer zuerst ausgelöst wurde, läuft zuerst, egal wo er auf der Platine liegt.
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2)),
                Clock(1, Skill("b", 2)),
                Row("battle_start", Skill("long", Ticks.FromSeconds(3))),
            }), enemyInterval: 30), 6);

            Assert.Less(Queued(r, 1)[0].Tick, Queued(r, 0)[0].Tick, "Komponente 2 wurde früher eingereiht");
            BattleEvent a = Starts(r, "a")[0], b = Starts(r, "b")[0];
            Assert.IsTrue(a.FromQueue && b.FromQueue);
            Assert.Less(b.Tick, a.Tick, "zuerst ausgelöst, zuerst gelaufen");
        }

        [Test]
        public void ATriggerOnABusyTargetIsQueued()
        {
            // Der Auslöser kommt bei der Ausführung des Stichs; danach erholt sich der Ritter noch, der Bohrer wartet.
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) });
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Clock(3, Skill("stab", 4, recovery: 10)),
                Row(Never, Skill("drill", 4)),
            }, null, graph)), 10);

            Assert.IsEmpty(Own(r).Where(e => e.Kind == BattleEventKind.TriggerMissed), "kein Auslösen geht verloren");
            List<BattleEvent> queued = Queued(r, 1);
            Assert.Greater(queued.Count, 0);
            Assert.IsTrue(queued.All(e => e.IsTriggered && e.CauseRow == 0));
            List<BattleEvent> drills = Starts(r, "drill");
            Assert.AreEqual(queued.Count, drills.Count);
            Assert.IsTrue(drills.All(e => e.FromQueue && e.IsTriggered && e.CauseRow == 0 && e.QueuedTicks >= 10));
        }

        [Test]
        public void EachComponentIsQueuedAtMostOnce()
        {
            // Viele Treffer während eines langen Casts: die Komponente steht trotzdem nur einmal in der Warteschlange.
            LogicBoard Board() => new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2)),
                Clock(3, Skill("long", Ticks.FromSeconds(3))),
            });
            BattleResult r = Run(Setup(Board(), enemyInterval: 7));
            Assert.Greater(Queued(r, 0).Count, 1);
            Assert.AreEqual(1, MostWaitingAtOnce(r, 0));
            Assert.Greater(Missed(r, 0, MissReason.AlreadyQueued).Count, 0, "weitere Treffer sind «Missed Triggers»");
            Assert.AreEqual(1, QueueConfig.Default.MaxEntriesPerComponent);

            // Als Daten: mit zwei erlaubten Einträgen kann sie doppelt warten.
            BattleResult twice = Run(Setup(Board(), enemyInterval: 7, queue: new QueueConfig { MaxEntriesPerComponent = 2 }));
            Assert.AreEqual(2, MostWaitingAtOnce(twice, 0));
        }

        [Test]
        public void TheBasicAttackFillsTheGapAndIsInterrupted()
        {
            // Langsamer Basisangriff (3 s) füllt die Lücke; wird die Komponente eingereiht, bricht er im Ausholen ab.
            SkillDefinition a = Skill("a", 2);
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", a) }), enemyInterval: 30, knightInterval: 60));

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
        public void TheBonusOfTheTriggeringRelayIsKept()
        {
            // Ein schweres Relais (◆◆◆) gibt seinen Bonus mit, auch wenn die Komponente aus der Warteschlange startet.
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", Ticks.FromSeconds(1)), difficulty: 3),
            }), enemyInterval: 10));
            List<BattleEvent> fromQueue = Starts(r, "a").Where(e => e.FromQueue).ToList();
            Assert.Greater(fromQueue.Count, 0);
            Assert.IsTrue(fromQueue.All(e => e.Tier == 3));

            // Über einen Auslöser: die schwere auslösende Komponente gibt ihre Stufe der leichten Ziel-Komponente mit.
            var graph = new LogicGraph(new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)) });
            BattleResult t = Run(Setup(new LogicBoard(new[]
            {
                Clock(3, Skill("stab", 4, recovery: 10), difficulty: 3),
                Row(Never, Skill("drill", 4)),
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
                Clock(2, skills.Get(SkillIds.Drill)),
            }), enemyInterval: 13, seed: seed);

            BattleResult r = Run(Fight(4));
            Assert.Greater(r.Events.Count(e => e.Kind == BattleEventKind.RowQueued), 0);
            Assert.AreEqual(Fingerprint(r), Fingerprint(Run(Fight(4))));
        }

        [Test]
        public void ACircleOfTriggersStaysStable()
        {
            // Stich löst Schlag aus, Schlag löst Stich aus: jede Komponente wartet höchstens einmal, der Kampf bleibt endlich.
            SkillDefinition stab = Skill("stab", 4, recovery: 4), bash = Skill("bash", 6, recovery: 4);
            LogicBoard Circle()
            {
                var edges = new[] { new GraphEdge(GraphNode.Skill(0), GraphNode.Skill(1)), new GraphEdge(GraphNode.Skill(1), GraphNode.Skill(0)) };
                return new LogicBoard(new[] { Row("battle_start", stab), Row(Never, bash) }, null, new LogicGraph(edges));
            }

            BattleResult r = Run(Setup(Circle()), 30);
            Assert.AreEqual(BattleEventKind.BattleEnd, r.Events.Last().Kind);
            Assert.AreEqual(1, MostWaitingAtOnce(r, 0));
            Assert.AreEqual(1, MostWaitingAtOnce(r, 1));
            List<BattleEvent> stabs = Starts(r, "stab");
            int loop = stab.WindupTicks + stab.RecoveryTicks + bash.WindupTicks + bash.RecoveryTicks;
            for (int i = 1; i < stabs.Count; i++)
                Assert.GreaterOrEqual(stabs[i].Tick - stabs[i - 1].Tick, loop, "nicht schneller als die Cast-Zeiten beider Komponenten");
            Assert.Greater(Starts(r, "bash").Count, 5, "der Kreis läuft weiter");
            Assert.AreEqual(Fingerprint(r), Fingerprint(Run(Setup(Circle()), 30)));
        }

        [Test]
        public void TheQueueIsEmptiedAtTheEndOfTheFight()
        {
            // Langer Cast, Treffer alle 0,5 s: am Ende wartet immer noch eine Ausführung.
            BattleSetup setup = Setup(new LogicBoard(new[] { Row("when_hit", Skill("a", Ticks.FromSeconds(3))) }), enemyInterval: 10);
            setup.TimeLimitTicks = Ticks.FromSeconds(5);
            setup.MaxTicks = Ticks.FromSeconds(10);
            var battle = new Battle(setup);
            BattleResult r = battle.Run();

            Assert.Greater(Queued(r, 0).Count, Starts(r, "a").Count - 1, "am Ende wartete noch etwas");
            Assert.IsEmpty(battle.Player.Queue);
            Assert.IsTrue(battle.Enemies.All(e => e.Queue.Count == 0));
        }

        [Test]
        public void TheReportCountsQueuedComponentsTheirWaitAndMissedTriggers()
        {
            // Cast 1,5 s, Treffer alle 0,5 s: die Komponente wartet und verpasst Auslöser, weil sie schon eingereiht ist.
            BattleResult r = Run(Setup(new LogicBoard(new[] { Row("when_hit", Skill("a", 30)) }), enemyInterval: 10));
            RowReport row = BattleReport.Create(r).Rows[0];

            Assert.AreEqual(Queued(r, 0).Count, row.Queued);
            Assert.Greater(row.StartedFromQueue, 0);
            Assert.Greater(row.AverageWaitTicks, 0);
            StringAssert.Contains("× queued, Ø ", row.QueueText);
            StringAssert.EndsWith(" wait", row.QueueText);

            Assert.AreEqual(Missed(r, 0, MissReason.AlreadyQueued).Count, row.MissCount(MissReason.AlreadyQueued));
            Assert.Greater(row.Missed, 0);
            Assert.AreEqual(MissReason.AlreadyQueued, row.MainMissReason);
            Assert.AreEqual(row.Queued + row.Missed, row.Triggered);
        }

        [Test]
        public void ThePlaybackShowsTheQueueNextToTheBoard()
        {
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2)),
                Clock(3, Skill("long", Ticks.FromSeconds(3))),
            }), enemyInterval: 30), 10);
            BattleEvent start = Starts(r, "a").First(e => e.FromQueue && e.QueuedTicks > 0);
            int queuedAt = start.Tick - start.QueuedTicks;
            Assert.IsTrue(Queued(r, 0).Any(e => e.Tick == queuedAt));

            var playback = new BattlePlayback(r);
            playback.Advance(queuedAt);
            Assert.IsTrue(playback.IsRowQueued(0));
            Assert.AreEqual(RowDisplay.Queued, playback.RowStateAt(0));
            StringAssert.StartsWith("Waiting: #1 a", playback.QueueText());
            Assert.IsTrue(playback.Entries.Any(l => l.Text.Contains("#1 a queued")), string.Join("\n", playback.Entries.Select(l => l.Text)));

            playback.Advance(start.Tick - playback.Tick);
            Assert.IsFalse(playback.IsRowQueued(0));
            Assert.IsTrue(playback.Lines.Last(l => l.Contains("] → #1 a")).Contains("from the queue after"));

            playback.SkipToEnd();
            Assert.AreEqual(string.Empty, playback.QueueText());
        }

        [Test]
        public void TheLogExplainsMissedTriggers()
        {
            BattleResult r = Run(Setup(new LogicBoard(new[]
            {
                Row("when_hit", Skill("a", 2)),
                Clock(3, Skill("long", Ticks.FromSeconds(3))),
            }), enemyInterval: 7), 10);

            BattleEvent missed = Missed(r, 0, MissReason.AlreadyQueued).First();
            Assert.AreEqual(0, missed.Relay, "das auslösende Relais steht am Missed Trigger");
            var playback = new BattlePlayback(r);
            playback.Advance(missed.Tick);
            StringAssert.Contains(RowStateText.Reason(MissReason.AlreadyQueued), playback.LastSkipReason(0));
            Assert.IsTrue(playback.Lines.Any(l => l.Contains("missed trigger on #1 a (already queued)")), string.Join("\n", playback.Lines));
            Assert.IsNull(playback.LastSkipReason(1), "die lange Komponente hat nichts verpasst");
        }
    }
}
