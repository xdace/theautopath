using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Kampf-Lesbarkeit: Entscheidungs-Protokoll, Auswertung pro Zeile und Live-Anzeige der Wiedergabe.</summary>
    public class BattleInsightTests
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();

        private static LogicRow Row(string condition, string skill, int param = 0, string label = null) =>
            new LogicRow(Conditions.Create(condition, param), skill != null ? Skills.Get(skill) : null, label ?? condition);

        private static BattleResult Run(LogicBoard board, CombatantSetup enemy = null, int seed = 1, int hp = 200) =>
            CombatSimulation.Run(Duel(Fighter("Ritter", hp, 4, board: board), enemy ?? Fighter("Puppe", 400, 1, 40), seed));

        // ------------------------------------------------------------------ Gründe

        [Test]
        public void CooldownIsTheReasonWhileTheSkillRecharges()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.ShieldBash, label: "Immer") }));

            BattleDecision first = r.Decisions[0];
            Assert.AreEqual(0, first.ChosenRow, "Schildschlag feuert zuerst");

            BattleDecision next = r.Decisions.First(d => d.ChosenRow == 1);
            RowCheck check = next.Rows[0];
            Assert.AreEqual(RowCheckState.Cooldown, check.State);
            Assert.IsTrue(next.Skipped(0));

            // Rest-Cooldown passt genau zur Zeit seit dem Schildschlag.
            int expected = Skills.Get(SkillIds.ShieldBash).CooldownTicks - (next.Tick - first.Tick);
            Assert.AreEqual(expected, check.CooldownLeft);
            Assert.AreEqual(Skills.Get(SkillIds.ShieldBash).CooldownTicks, check.CooldownTotal);
            StringAssert.StartsWith("Skill im Cooldown (noch ", RowStateText.Reason(check));
        }

        [Test]
        public void ConditionFalseIsTheReasonWhenItDoesNotHold()
        {
            // Volles Leben gegen eine harmlose Puppe: «HP unter 30 %» wird nie wahr.
            BattleResult r = Run(new LogicBoard(new[] { Row("hp_low", SkillIds.Repair, 30, "HP unter 30 %") }), Fighter("Puppe", 60, 0, 1000));

            Assert.IsNotEmpty(r.Decisions);
            foreach (BattleDecision d in r.Decisions)
            {
                Assert.AreEqual(1, d.ChosenRow);
                Assert.AreEqual(RowCheckState.ConditionFalse, d.Rows[0].State);
            }
            Assert.AreEqual("Bedingung nicht erfüllt", RowStateText.Reason(r.Decisions[0].Rows[0]));

            BattleReport report = BattleReport.Create(r);
            Assert.AreEqual(0, report.Rows[0].Fired);
            Assert.AreEqual(r.Decisions.Count, report.Rows[0].Skipped);
            Assert.AreEqual(RowCheckState.ConditionFalse, report.Rows[0].MainReason);
            CollectionAssert.Contains(report.Hints, "Zeile 1 hat nie gefeuert: Bedingung nie erfüllt.");
        }

        [Test]
        public void OrphanedRowsAreSkippedAsOrphaned()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", null, label: "Immer") }), Fighter("Puppe", 30, 0, 1000));
            Assert.AreEqual(RowCheckState.Orphaned, r.Decisions[0].Rows[0].State);
            CollectionAssert.Contains(BattleReport.Create(r).Hints, "Zeile 1 hat nie gefeuert: verwaist, kein Skill zugeordnet.");
        }

        [Test]
        public void ReadyRowsDuringAnUninterruptibleActionAreMarkedAsActionRunning()
        {
            // Schildschlag holt aus, die schnelle Puppe trifft: «HP unter 100 %» wird wahr, während die Aktion läuft.
            var board = new LogicBoard(new[]
            {
                Row("hp_low", SkillIds.Repair, 100, "Verletzt"),
                Row("always", SkillIds.ShieldBash, label: "Immer"),
            });
            BattleResult r = Run(board, Fighter("Puppe", 400, 1, 3));

            BattleDecision busy = r.Decisions.First(d => d.IsBusy);
            Assert.AreEqual(RowCheckState.ActionRunning, busy.Rows[0].State);
            Assert.AreEqual(1, busy.RunningRow);
            Assert.IsTrue(busy.Skipped(0));
            Assert.AreEqual("Bedingung erfüllt, aber Aktion läuft", RowStateText.Reason(busy.Rows[0]));

            // Pro laufender Aktion nur ein Eintrag je Zeile, nicht jeden Tick.
            var perAction = r.Decisions.Where(d => d.IsBusy).GroupBy(d => d.RunningRow + ":" + LastChoiceBefore(r, d.Tick));
            foreach (var group in perAction) Assert.LessOrEqual(group.Count(d => d.Rows[0].State == RowCheckState.ActionRunning), 1);
        }

        private static int LastChoiceBefore(BattleResult r, int tick) => r.Decisions.Last(d => !d.IsBusy && d.Tick <= tick).Tick;

        [Test]
        public void DecisionsAreOnlyRecordedWhenSomethingIsDecided()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.ShieldBash) }));
            int started = r.Events.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Side == Side.Player);
            Assert.AreEqual(started, r.Decisions.Count(d => !d.IsBusy));
            Assert.Less(r.Decisions.Count, r.EndTick / 4, "deutlich weniger Einträge als Ticks");
        }

        // ------------------------------------------------------------------ Auswertung

        [Test]
        public void ReportSumsExactlyTheDamageFromTheLog()
        {
            var board = new LogicBoard(new[]
            {
                Row("enemy_charging", SkillIds.ShieldBash, label: "Gegner lädt auf"),
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
        public void BurnDamageCountsForTheRowThatIgnited()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.Ignite, label: "Immer") }), Fighter("Puppe", 400, 0, 1000));
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
        /// </summary>
        [TestCase("blade", 5, "2 Kämpfe, 160 Ereignisse, ECDD1EF6E91402DD")]
        [TestCase("blade", 21, "3 Kämpfe, 168 Ereignisse, 2427B85FF7426862")]
        [TestCase("shield", 5, "4 Kämpfe, 336 Ereignisse, 23FF6E4CF91A9B38")]
        [TestCase("shield", 21, "2 Kämpfe, 200 Ereignisse, 3CF50B54B9F5F2B1")]
        [TestCase("spark", 5, "2 Kämpfe, 139 Ereignisse, FC176BE2532C6AA4")]
        [TestCase("spark", 21, "2 Kämpfe, 142 Ereignisse, F7164EE645981A14")]
        public void SameSeedsGiveTheSameFightsAsBefore(string kit, int seed, string fingerprint)
        {
            Assert.AreEqual(fingerprint, Fingerprint(BotBattles(KnightKit.Defaults.Single(k => k.Id == kit), seed)));
        }

        [Test]
        public void SameSeedsGiveTheSameDecisions()
        {
            KnightKit kit = KnightKit.Defaults.Single(k => k.Id == "shield");
            List<BattleResult> a = BotBattles(kit, 21);
            List<BattleResult> b = BotBattles(kit, 21);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(Describe(a[i]), Describe(b[i]));

                // Jede gewählte Zeile ist genau eine gestartete Aktion des Spielers.
                Combatant player = a[i].Fighters[0].Combatant;
                CollectionAssert.AreEqual(
                    a[i].Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source == player && !e.IsRepeat).Select(e => (e.Tick, e.RowIndex)),
                    a[i].Decisions.Where(d => !d.IsBusy).Select(d => (d.Tick, d.ChosenRow)));
            }
        }

        private static string Describe(BattleResult r) => string.Join(";", r.Decisions.Select(d =>
            $"{d.Tick}:{d.ChosenRow}:{d.RunningRow}:" + string.Join(",", d.Rows.Select(c => $"{c.State}/{c.CooldownLeft}/{c.ConditionMet}"))));

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
        public void PlaybackShowsRowStatesCooldownAndSkipReasons()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.ShieldBash, label: "Immer") }));
            var p = new BattlePlayback(r);
            Assert.AreEqual(RowDisplay.Unknown, p.RowStateAt(0));

            p.Advance(r.Decisions[0].Tick);
            Assert.AreEqual(RowDisplay.Cooldown, p.RowStateAt(0));
            Assert.AreEqual(1f, p.CooldownFraction(0), 0.001f);

            BattleDecision skip = r.Decisions.First(d => d.ChosenRow == 1);
            p.Advance(skip.Tick - p.Tick);
            Assert.Less(p.CooldownFraction(0), 1f);
            StringAssert.Contains("Skill im Cooldown (noch", p.LastSkipReason(0));
            Assert.IsNull(p.LastSkipReason(1), "Fallback wurde nie übersprungen");
        }

        [Test]
        public void PlaybackTracksStatusesResourcesAndPopups()
        {
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.Ignite, label: "Immer") }), Fighter("Puppe", 400, 0, 1000));
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
            BattleResult r = Run(new LogicBoard(new[] { Row("always", SkillIds.ShieldBash, label: "Immer") }), Fighter("Puppe", 60, 2, 25));
            var p = new BattlePlayback(r);
            p.SkipToEnd();

            List<LogEntry> mine = p.Entries.Where(e => e.Matches(LogFilter.Mine)).ToList();
            List<LogEntry> damage = p.Entries.Where(e => e.Matches(LogFilter.Damage)).ToList();
            Assert.IsNotEmpty(mine);
            Assert.IsNotEmpty(damage);
            Assert.IsTrue(mine.Any(e => e.Text.Contains("Schildschlag")));
            Assert.IsTrue(damage.All(e => e.Text.Contains("−") || e.Text.Contains("Krit") || e.Text.Contains("blockt") || e.Text.Contains("weicht")));
            Assert.AreEqual(p.Entries.Count, p.Entries.Count(e => e.Matches(LogFilter.All)));
        }
    }
}
