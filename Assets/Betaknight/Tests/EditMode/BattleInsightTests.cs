using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Kampf-Lesbarkeit: Relais-Auslösungen und Missed Triggers, Auswertung pro Komponente und Live-Anzeige der Wiedergabe.</summary>
    public class BattleInsightTests
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();

        private static LogicRow Row(string condition, string skill, int param = 0, string label = null) =>
            new LogicRow(Conditions.Create(condition, param), skill != null ? Skills.Get(skill) : null, label ?? condition);

        private static BattleResult Run(LogicBoard board, CombatantSetup enemy = null, int seed = 1, int hp = 200) =>
            CombatSimulation.Run(Duel(Fighter("Ritter", hp, 4, board: board), enemy ?? Fighter("Puppe", 400, 1, 40), seed));

        // ------------------------------------------------------------------ Gründe

        /// <summary>Platine aus dem Katalog: «When Hit» oben links, rechts daneben der Schockstich, darunter der zu grosse
        /// Rüstungsbrecher (2×2 an einem ◆-Relais), unten rechts die Schubdüsen ohne Relais.</summary>
        private static LogicBoard MixedBoard()
        {
            var spec = new CircuitSpec { Width = 4, Height = 3 };
            spec.Relays.Add(new RelaySpec("when_hit", new Cell(0, 0)));
            spec.Components.Add(new ComponentSpec(SkillIds.ShockStab, new Cell(1, 0)));
            spec.Components.Add(new ComponentSpec(SkillIds.ArmorBreak, new Cell(0, 1)));
            spec.Components.Add(new ComponentSpec(SkillIds.Thrusters, new Cell(3, 2)));
            return BoardFactory.CreateDefault().Create(spec, null);
        }

        [Test]
        public void TooLargeAndUnpoweredComponentsAreExplained()
        {
            LogicBoard board = MixedBoard();
            CollectionAssert.AreEqual(new[] { SkillIds.ShockStab, SkillIds.ArmorBreak, SkillIds.Thrusters }, board.Rows.Select(x => x.Skill.Id).ToList());
            BattleResult r = Run(board);
            BattleReport report = BattleReport.Create(r);

            RowReport stab = report.Rows[0], breaker = report.Rows[1], thrusters = report.Rows[2];
            Assert.Greater(stab.Fired, 0);
            Assert.AreEqual(stab.Queued, stab.Triggered, "der Schockstich verpasst nichts");

            // Der Rüstungsbrecher berührt das Relais, ist aber zu gross: jedes Auslösen ist ein «Missed Trigger».
            Assert.IsFalse(breaker.IsPowered);
            Assert.IsTrue(breaker.IsTooLargeSomewhere);
            Assert.AreEqual(0, breaker.Fired);
            Assert.AreEqual(report.RelayTriggers, breaker.MissCount(MissReason.TooLarge));
            Assert.AreEqual(MissReason.TooLarge, breaker.MainMissReason);
            Assert.IsTrue(report.HasMissedTriggers);
            Assert.AreEqual("too large for the relay", RowStateText.Reason(MissReason.TooLarge));
            CollectionAssert.Contains(report.Hints, "#2 Armor Break never fired: too large for every touching relay.");

            // Die Schubdüsen berührt kein Relais: nie ausgelöst, kein Missed Trigger.
            Assert.IsFalse(thrusters.IsPowered);
            Assert.IsFalse(thrusters.IsTooLargeSomewhere);
            Assert.AreEqual(0, thrusters.Triggered);
            CollectionAssert.Contains(report.Hints, "#3 Thrusters never fired: no relay touches it.");
        }

        [Test]
        public void ARelayThatNeverTriggersIsExplained()
        {
            // Volles Leben gegen eine harmlose Puppe: «HP unter 30 %» wird nie wahr, das Relais löst nie aus.
            BattleResult r = Run(new LogicBoard(new[] { Row("hp_low", SkillIds.Repair, 30, "HP unter 30 %") }), Fighter("Puppe", 60, 0, 1000));

            BattleReport report = BattleReport.Create(r);
            Assert.AreEqual(0, report.RelayTriggers);
            Assert.AreEqual(0, report.Rows[0].Fired);
            Assert.AreEqual(0, report.Rows[0].Triggered, "Triggered");
            Assert.AreEqual(0, report.Rows[0].Missed, "nie ausgelöst ist kein verpasster Auslöser");
            Assert.IsNull(report.Rows[0].MainMissReason);
            Assert.IsFalse(report.HasMissedTriggers);
            CollectionAssert.Contains(report.Hints, "#1 Emergency Repair: never triggered – try an easer or a different relay.");
        }

        [Test]
        public void ARarelyTriggeredComponentGetsAHint()
        {
            // «Kampfbeginn» löst genau einmal aus; davor steht eine Komponente, deren Cast länger dauert als der Kampf. Die
            // Reparatur wartet bis zum Ende und feuert nie: «triggered only 1×».
            var endless = new SkillDefinition("endless", "Endless", Ticks.FromSeconds(1000), 0, new ISkillEffect[0]);
            BattleResult r = Run(new LogicBoard(new[]
            {
                new LogicRow(Conditions.Create("battle_start", 0), endless, "Kampfbeginn"),
                Row("battle_start", SkillIds.Repair, 0, "Kampfbeginn"),
            }), Fighter("Puppe", 60, 0, 1000));
            BattleReport report = BattleReport.Create(r);
            Assert.AreEqual(0, report.Rows[1].Fired);
            Assert.AreEqual(1, report.Rows[1].Triggered);
            Assert.AreEqual(1, report.Rows[1].Queued);
            CollectionAssert.Contains(report.Hints, "#2 Emergency Repair: triggered only 1× – try an easer or a different relay.");
        }

        [Test]
        public void OrphanedComponentsMissAsOrphaned()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("battle_start", null, label: "Kampfbeginn") }), Fighter("Puppe", 30, 0, 1000));
            BattleReport report = BattleReport.Create(r);
            CollectionAssert.Contains(report.Hints, "#1 — never fired: no skill placed.");
            Assert.AreEqual("no skill", RowStateText.Reason(MissReason.Orphaned));
            Assert.AreEqual(1, report.Rows[0].MissCount(MissReason.Orphaned));
            Assert.AreEqual(MissReason.Orphaned, report.Rows[0].MainMissReason);
            Assert.AreEqual(0, report.Rows[0].MissCount(MissReason.AlreadyQueued));
        }

        [Test]
        public void ATriggerDuringARunningActionWaitsInTheQueue()
        {
            // Schildschlag holt aus, die schnelle Puppe trifft: «HP unter 100 %» löst aus, während die Aktion läuft.
            var board = new LogicBoard(new[]
            {
                Row("hp_low", SkillIds.Repair, 100, "Verletzt"),
                Row("battle_start", SkillIds.ShieldBash, label: "Kampfbeginn"),
            });
            BattleResult r = Run(board, Fighter("Puppe", 400, 1, 3));
            Combatant player = r.Fighters[0].Combatant;

            BattleEvent bash = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ShieldBash);
            BattleEvent queued = r.Events.First(e => e.Kind == BattleEventKind.RowQueued && e.Source == player && e.RowIndex == 0);
            Assert.Less(bash.Tick, queued.Tick);
            Assert.Less(queued.Tick, bash.Tick + bash.Amount, "eingereiht, während der Schildschlag noch ausholt");
            Assert.IsFalse(r.Events.Any(e => e.Kind == BattleEventKind.ActionInterrupted && e.Detail == SkillIds.ShieldBash), "nichts bricht den Schildschlag ab");

            BattleEvent repair = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.Repair);
            Assert.IsTrue(repair.FromQueue);
            Assert.Greater(repair.Tick, bash.Tick + bash.Amount - 1, "startet erst nach dem Schildschlag");
            Assert.AreEqual(repair.Tick - queued.Tick, repair.QueuedTicks);
            Assert.Greater(BattleReport.Create(r).Rows[0].AverageWaitTicks, 0);
        }

        [Test]
        public void EveryRelayTriggerReachesEachComponentItTouches()
        {
            // Jedes Auslösen nennt, wie viele Komponenten es versorgt; jede berührte Komponente wird eingereiht oder verpasst.
            LogicBoard board = MixedBoard();
            BattleResult r = Run(board);
            Combatant player = r.Fighters[0].Combatant;
            List<BattleEvent> own = r.Events.Where(e => e.Source == player).ToList();
            List<BattleEvent> triggers = own.Where(e => e.Kind == BattleEventKind.RelayTriggered).ToList();
            Assert.IsNotEmpty(triggers);
            Assert.Less(triggers.Count, r.EndTick / 4, "deutlich weniger Einträge als Ticks");
            foreach (BattleEvent t in triggers)
            {
                Assert.AreEqual(1, t.Extra, "versorgt nur den Schockstich");
                List<BattleEvent> reached = own.Where(e => e.Tick == t.Tick && e.Relay == t.Relay
                    && (e.Kind == BattleEventKind.RowQueued || e.Kind == BattleEventKind.TriggerMissed)).ToList();
                CollectionAssert.AreEquivalent(new[] { 0, 1 }, reached.Select(e => e.RowIndex).ToList(), $"Tick {t.Tick}");
            }
        }

        // ------------------------------------------------------------------ Auswertung

        [Test]
        public void ReportSumsExactlyTheDamageFromTheLog()
        {
            var board = new LogicBoard(new[]
            {
                Row("enemy_charging", SkillIds.ShieldBash, label: "Enemy Charging"),
                Row("every_5s", SkillIds.Ignite, 5, "Alle 5 s"),
                Row("enemy_armored", SkillIds.ArmorBreak, label: "Gegner gepanzert"),
            });
            BattleResult r = CombatSimulation.Run(Duel(Fighter("Ritter", 120, 4, board: board), SetTests.Golem(), 3));
            Combatant player = r.Fighters[0].Combatant;
            BattleReport report = BattleReport.Create(r);

            int logged = r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.Source == player && e.Target.Side == Side.Enemy).Sum(e => e.Amount);
            Assert.Greater(logged, 0);
            Assert.AreEqual(logged, report.TotalDamage);
            Assert.AreEqual(logged, report.Rows.Sum(row => row.Damage) + report.OtherDamage);

            int started = r.Events.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Source == player);
            Assert.AreEqual(started, report.Rows.Sum(row => row.Fired));

            int shares = report.Rows.Sum(row => row.DamageShareBp);
            Assert.LessOrEqual(shares, BasisPoints.Full);
            if (report.OtherDamage == 0) Assert.GreaterOrEqual(shares, BasisPoints.Full - report.Rows.Count, "nur Rundung");
        }

        [Test]
        public void BurnDamageCountsForTheComponentThatIgnited()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("battle_start", SkillIds.Ignite, label: "Kampfbeginn") }), Fighter("Puppe", 400, 0, 1000));
            Combatant player = r.Fighters[0].Combatant;
            List<BattleEvent> burns = r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.Source == player && e.Detail == StatusIds.Burn).ToList();

            Assert.IsNotEmpty(burns);
            Assert.IsTrue(burns.All(e => e.RowIndex == 0));
            Assert.GreaterOrEqual(BattleReport.Create(r).Rows[0].Damage, burns.Sum(e => e.Amount));
        }

        // ------------------------------------------------------------------ Gleiche Ergebnisse wie vorher

        /// <summary>
        /// Fingerabdrücke aller Kämpfe aus Bot-Runs. Das Protokoll darf keinen Kampf verändern: gleiche Seeds ergeben
        /// dieselben Ereignisse und damit dieselben Entscheidungen. Neu aufgenommen mit A-06, weil sich Kämpfe dort bewusst
        /// ändern (neue Cast-Zeiten, Echo mit eigener Cast-Zeit, Synergie-Tags) und Angebote anders würfeln (Skills seit A-05).
        /// Mit A-07 erneut aufgenommen: Belohnungen und Shops würfeln zusätzlich seltene Module, die Kämpfe selbst sind unverändert.
        /// Mit A-08 erneut: Skills wachsen aus Kämpfen (z. B. +1 Schaden pro Kill), spätere Kämpfe eines Runs ändern sich daher.
        /// Mit A-11 erneut: Schwierigkeits-Bonus der Bausteine und neue Erleichterer in den Angeboten.
        /// Mit A-12 erneut: Skills neu eingestellt, Basisangriff 60 % und verkürzt Cooldowns, Gegner-HP 65 %.
        /// Mit A-13 erneut: Warteschlange, erfüllte Zeilen warten statt übersprungen zu werden; durchgehend erfüllte feuern nach jedem Cooldown.
        /// Schild mit Bohrstoß an der Start-Rune: Seed 5 des Schildritters erneut aufgenommen.
        /// Mit A-19 erneut: Platine statt Tafel, keine Cooldowns, Relais lösen bei Ereignissen bzw. steigender Flanke aus,
        /// Skills nach Grösse neu eingestellt, Gegner mit Takt-Relais.
        /// Mit A-20 erneut: Relais und Gatter melden «an/aus» (neue Ereignisse), Pins verbinden berührende Komponenten mit Pulsen.
        /// Mit A-21 erneut: Thermal Throttling ab 30 s statt Überhitzungsschaden, Gegner mit Hacks.
        /// Mit Gegner-Platinen erneut: Gegner tragen Skills, Module und Chips (fest je Feld), nach dem Sieg wird geborgen.
        /// </summary>
        [TestCase("blade", 5, "4 Kämpfe, 419 Ereignisse, EBDB181DD7CC1806")]
        [TestCase("blade", 21, "2 Kämpfe, 220 Ereignisse, 05E7CAB027B90EED")]
        [TestCase("shield", 5, "6 Kämpfe, 863 Ereignisse, E12064B83239FCDF")]
        [TestCase("shield", 21, "3 Kämpfe, 391 Ereignisse, 33013BFDE6756A92")]
        [TestCase("spark", 5, "2 Kämpfe, 206 Ereignisse, 0A1771818AC715E0")]
        [TestCase("spark", 21, "2 Kämpfe, 283 Ereignisse, 8258D2EDF7A6B0E6")]
        public void SameSeedsGiveTheSameFightsAsBefore(string kit, int seed, string fingerprint)
        {
            Assert.AreEqual(fingerprint, Fingerprint(BotBattles(KnightKit.Defaults.Single(k => k.Id == kit), seed)));
        }

        [Test]
        public void SameSeedsGiveTheSameQueues()
        {
            KnightKit kit = KnightKit.Defaults.Single(k => k.Id == "shield");
            List<BattleResult> a = BotBattles(kit, 21);
            List<BattleResult> b = BotBattles(kit, 21);
            Assert.AreEqual(a.Count, b.Count);
            Assert.IsNotEmpty(a);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(Describe(a[i]), Describe(b[i]));

                // Jede Komponente startet aus der Warteschlange; nur der Basisangriff und Wiederholungen nicht.
                Combatant player = a[i].Fighters[0].Combatant;
                List<BattleEvent> starts = a[i].Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source == player && !e.IsRepeat).ToList();
                Assert.IsTrue(starts.Where(e => e.Detail != SkillIds.BasicAttack).All(e => e.FromQueue));
                Assert.IsTrue(starts.Where(e => e.Detail == SkillIds.BasicAttack).All(e => !e.FromQueue));
                Assert.LessOrEqual(starts.Count(e => e.FromQueue), a[i].Events.Count(e => e.Kind == BattleEventKind.RowQueued && e.Source == player));
            }
        }

        private static string Describe(BattleResult r) => string.Join(";", r.Events
            .Where(e => e.Kind == BattleEventKind.RelayTriggered || e.Kind == BattleEventKind.RowQueued || e.Kind == BattleEventKind.TriggerMissed
                        || e.Kind == BattleEventKind.ActionStarted)
            .Select(e => $"{e.Tick}:{e.Kind}:{e.Source?.Index}:{e.RowIndex}:{e.Relay}:{e.QueuedTicks}:{e.Amount}"));

        internal static string Fingerprint(IEnumerable<BattleResult> battles)
        {
            ulong hash = 1469598103934665603UL;
            int fights = 0;
            int events = 0;
            foreach (BattleResult r in battles)
            {
                fights++;
                foreach (BattleEvent e in r.Events)
                {
                    events++;
                    bool action = e.Kind == BattleEventKind.ActionStarted || e.Kind == BattleEventKind.ActionExecuted || e.Kind == BattleEventKind.ActionInterrupted;
                    string s = $"{e.Tick}|{(int)e.Kind}|{e.Source?.Index ?? -1}|{e.Target?.Index ?? -1}|{e.Amount}|{e.Detail}|{(action ? e.RowIndex : 0)};";
                    foreach (char ch in s)
                    {
                        hash ^= ch;
                        hash *= 1099511628211UL;
                    }
                }
            }
            return $"{fights} Kämpfe, {events} Ereignisse, {hash:X16}";
        }

        /// <summary>Zufalls-Bot auf einer erzeugten Karte; sammelt alle Arena-Kämpfe.</summary>
        internal static List<BattleResult> BotBattles(KnightKit kit, int seed)
        {
            var battles = new List<BattleResult>();
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 6, Seed = seed }, kit: kit);
            s.CombatFinished += r => { if (r.Battle != null) battles.Add(r.Battle); };
            var random = new Random(seed);
            for (int step = 0; step < 80 && !s.IsGameOver; step++)
            {
                int guard = 0;
                while (s.IsBusy && guard++ < 50)
                {
                    if (s.PendingEncounter != null) s.ChooseEncounterOption(s.PendingEncounter.Definition.Options.Count - 1);
                    else if (s.PendingItem != null) s.RejectPendingItem();
                    else if (s.PendingSalvage != null) s.TakeSalvage(0);
                    else if (s.PendingRune != null) s.RejectPendingRune();
                    else if (s.PendingRuneOffer != null) { if (!s.TakeRune(0, 0)) s.SkipRuneOffer(); }
                    else if (s.PendingShop != null) s.LeaveShop();
                    else if (s.CanEnterPortal) s.EnterPortal();
                }
                var options = s.Map.GetNeighbors(s.Player.Position).Where(c => s.CanStepTo(c.Coord)).ToList();
                if (options.Count == 0) break;
                s.TryStep(options[random.Next(options.Count)].Coord);
            }
            return battles;
        }

        // ------------------------------------------------------------------ Wiedergabe

        [Test]
        public void PlaybackShowsComponentStatesRelaysAndMissReasons()
        {
            BattleResult r = Run(MixedBoard());
            var p = new BattlePlayback(r);
            Assert.AreEqual(RowDisplay.Idle, p.RowStateAt(0));
            Assert.AreEqual(RowDisplay.TooLarge, p.RowStateAt(1));
            Assert.AreEqual(RowDisplay.Unpowered, p.RowStateAt(2));
            Assert.AreEqual(0, p.RelayCount(0));

            BattleEvent trigger = r.Events.First(e => e.Kind == BattleEventKind.RelayTriggered && e.Source == r.Fighters[0].Combatant);
            p.Advance(trigger.Tick);
            Assert.IsTrue(p.IsRelayLit(0));
            Assert.AreEqual(1, p.RelayCount(0));
            Assert.AreEqual(RowDisplay.Firing, p.RowStateAt(0), "der Schockstich startet im selben Tick");
            StringAssert.Contains("too large for the relay", p.LastSkipReason(1));
            Assert.IsNull(p.LastSkipReason(0), "der Schockstich hat nichts verpasst");
            Assert.IsNull(p.LastSkipReason(3), "der Basisangriff wird nie ausgelöst");

            p.Advance(BattlePlayback.RowHighlightTicks);
            Assert.IsFalse(p.IsRelayLit(0), "das Leuchten vergeht");
        }

        [Test]
        public void PlaybackTracksStatusesResourcesAndPopups()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("battle_start", SkillIds.Ignite, label: "Kampfbeginn") }), Fighter("Puppe", 400, 0, 1000));
            int ignite = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Burn).Tick;
            var p = new BattlePlayback(r);
            p.Advance(ignite);

            StatusView burn = p.Fighters[1].Statuses.Single(st => st.Id == StatusIds.Burn);
            Assert.AreEqual(1, burn.Stacks);
            Assert.Greater(burn.TicksLeft(p.Tick), 0);

            p.Advance(Ticks.PerSecond * 2);
            List<Popup> popups = p.TakePopups();
            Assert.IsTrue(popups.Any(x => x.TargetIndex == 1 && x.FromPlayer && x.Row == 0 && x.Amount > 0));
            Assert.IsEmpty(p.TakePopups(), "jede Zahl nur einmal");
        }

        [Test]
        public void LogFilterSeparatesOwnActionsAndDamage()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("clock", SkillIds.ShieldBash, 2, "Clock 2 s") }), Fighter("Puppe", 60, 2, 25));
            var p = new BattlePlayback(r);
            p.SkipToEnd();

            List<LogEntry> mine = p.Entries.Where(e => e.Matches(LogFilter.Mine)).ToList();
            List<LogEntry> damage = p.Entries.Where(e => e.Matches(LogFilter.Damage)).ToList();
            Assert.IsNotEmpty(mine);
            Assert.IsNotEmpty(damage);
            Assert.IsTrue(mine.Any(e => e.Text.Contains("Shield Bash")));
            Assert.IsTrue(damage.All(e => e.Text.Contains("−") || e.Text.Contains("Crit") || e.Text.Contains("blocks") || e.Text.Contains("dodges")));
            Assert.AreEqual(p.Entries.Count, p.Entries.Count(e => e.Matches(LogFilter.All)));
        }
    }
}
