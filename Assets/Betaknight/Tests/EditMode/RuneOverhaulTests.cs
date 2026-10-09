using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Überarbeitete Auslöser: Kämpfe dauern nur wenige Sekunden, also feuern Zeit- und Zustands-Runen oft genug, pulsierende
    /// Zustände laden zu grosse Komponenten jede Sekunde auf, neue Runen und das Modul «Charge Link».
    /// </summary>
    public class RuneOverhaulTests
    {
        private static readonly BoardFactory Factory = BoardFactory.CreateDefault();

        private static LogicBoard Board(RelaySpec relay, params ComponentSpec[] components)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(relay);
            spec.Components.AddRange(components);
            return Factory.Create(spec, null);
        }

        private static RelaySpec Relay(string runeId, int x = 0, int y = 0, int level = 0, params ModuleSpec[] modules) =>
            new RelaySpec(runeId, new Cell(x, y), level, modules);

        private static ComponentSpec Part(string skillId, int x, int y, params ModuleSpec[] modules) =>
            new ComponentSpec(skillId, new Cell(x, y), modules: modules);

        private static BattleResult Fight(LogicBoard board, int seconds = 10, int startHp = 0, int enemyInterval = 1000)
        {
            CombatantSetup a = Fighter("A", 100000, 10, 20, board: board);
            if (startHp > 0) a.StartHp = startHp;
            BattleSetup setup = Duel(a, Fighter("B", 100000, 1, enemyInterval));
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static List<BattleEvent> Own(BattleResult r) => r.Events.Where(e => e.Source?.Name == "A").ToList();

        private static List<int> TriggerTicks(BattleResult r, int relay = 0) =>
            Own(r).Where(e => e.Kind == BattleEventKind.RelayTriggered && e.Relay == relay).Select(e => e.Tick).ToList();

        private static int Queued(BattleResult r, int row) => Own(r).Count(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == row);

        // ------------------------------------------------------------------ Katalog

        [Test]
        public void HeavyHitAndHpCriticalAreGoneAndHpBelowIsTwentyOrTwentyFivePercent()
        {
            RuneCatalog runes = RuneCatalog.CreateDefault();
            Assert.IsFalse(runes.TryGet("big_hit_taken", out _));
            Assert.IsFalse(runes.TryGet("hp_critical", out _));
            Assert.IsFalse(runes.TryGet("overheat", out _));

            RuneDefinition hpLow = runes.Get("hp_low");
            CollectionAssert.AreEqual(new[] { 20, 25 }, hpLow.Levels);
            Assert.AreEqual(1, hpLow.Difficulty, "Grenze 2 Zellen: jede Sekunde 2 Ladung");
            Assert.AreEqual(1, hpLow.PulseSeconds);
        }

        [Test]
        public void TimeRunesFitIntoShortFights()
        {
            RuneCatalog runes = RuneCatalog.CreateDefault();
            Assert.That(runes.All.Where(r => r.Kind == ConditionKind.Clock).SelectMany(r => r.Levels), Is.All.LessThanOrEqualTo(5),
                "Ein Kampf dauert nur wenige Sekunden");
        }

        // ------------------------------------------------------------------ Pulsierende Zustände

        [Test]
        public void HpBelowFiresEverySecondWhileTheHpStayLow()
        {
            // 15 von 100 000 HP ist weit unter 20 %: das Relais löst sofort und danach jede Sekunde erneut aus.
            BattleResult r = Fight(Board(Relay("hp_low"), Part(SkillIds.ShockStab, 1, 0)), seconds: 5, startHp: 15000);
            List<int> ticks = TriggerTicks(r);
            Assert.GreaterOrEqual(ticks.Count, 4);
            for (int i = 1; i < ticks.Count; i++) Assert.AreEqual(Ticks.PerSecond, ticks[i] - ticks[i - 1], "Takt 1 s");
        }

        [Test]
        public void HpBelowChargesATooLargeComponentByTwoEverySecond()
        {
            // Rüstungsbruch ist 2×2 (4 Zellen, heilt nicht), das Relais versorgt 2: jedes 2. Pulsieren lässt sie laufen.
            BattleResult r = Fight(Board(Relay("hp_low"), Part(SkillIds.ArmorBreak, 1, 0)), seconds: 6, startHp: 15000);
            int triggers = TriggerTicks(r).Count;
            Assert.GreaterOrEqual(triggers, 4);
            int busy = Own(r).Count(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 0 && e.Amount == (int)MissReason.AlreadyQueued);
            Assert.AreEqual(triggers / 2, Queued(r, 0) + busy);
        }

        [Test]
        public void HpBelowDoesNotFireWithFullHp()
        {
            BattleResult r = Fight(Board(Relay("hp_low"), Part(SkillIds.ShockStab, 1, 0)), seconds: 5);
            Assert.IsEmpty(TriggerTicks(r));
        }

        // ------------------------------------------------------------------ Neue Runen

        [Test]
        public void OpeningFiresEverySecondAtTheStartOnly()
        {
            BattleResult r = Fight(Board(Relay("opening"), Part(SkillIds.ShockStab, 1, 0)), seconds: 8);
            List<int> ticks = TriggerTicks(r);
            Assert.GreaterOrEqual(ticks.Count, 3);
            Assert.That(ticks, Is.All.LessThanOrEqualTo(Ticks.FromSeconds(3)), "Opening 3 s");
        }

        [Test]
        public void OvertimeFiresEverySecondFromItsStartTime()
        {
            BattleResult r = Fight(Board(Relay("overtime"), Part(SkillIds.ShockStab, 1, 0)), seconds: 9);
            List<int> ticks = TriggerTicks(r);
            Assert.GreaterOrEqual(ticks.Count, 4);
            Assert.That(ticks, Is.All.GreaterThanOrEqualTo(Ticks.FromSeconds(4)), "Overtime 4 s");
        }

        [Test]
        public void IdleCycleFiresAfterBasicAttacks()
        {
            BattleResult r = Fight(Board(Relay("idle_cycle"), Part(SkillIds.ShockStab, 1, 0)), seconds: 6);
            Assert.GreaterOrEqual(TriggerTicks(r).Count, 3);
            Assert.Greater(Queued(r, 0), 0);
        }

        [Test]
        public void StatusAppliedFiresAfterAnOwnStatusOnTheEnemy()
        {
            // «Every 4 Seconds» (Grenze 2) zündet den Gegner an (Ignite, 2 Zellen), «Status Applied» lässt danach den Schockstich laufen.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(Relay("every_5s", 0, 0));
            spec.Components.Add(Part(SkillIds.Ignite, 1, 0));
            spec.Relays.Add(Relay("status_applied", 4, 0));
            spec.Components.Add(Part(SkillIds.ShockStab, 5, 0));
            LogicBoard board = Factory.Create(spec, null);
            int statusRelay = board.Relays.ToList().FindIndex(x => x.RuneId == "status_applied");

            BattleResult r = Fight(board, seconds: 8);
            Assert.IsNotEmpty(TriggerTicks(r, statusRelay));
        }

        // ------------------------------------------------------------------ Charge Link

        [Test]
        public void ChargeLinkChargesItsTargetByTheSizeOfItsComponent()
        {
            // Clock 2 s lässt den Schockstich (1 Zelle) laufen; sein Charge Link lädt die Reparatur (4 Zellen) jedes Mal um 1:
            // jede 4. Ausführung des Schockstichs startet die Reparatur.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(Relay("clock", 0, 0));
            spec.Components.Add(Part(SkillIds.ShockStab, 1, 0, new ModuleSpec(ModuleIds.ChargeLink, targetRow: 1)));
            spec.Components.Add(Part(SkillIds.Repair, 3, 2));
            LogicBoard board = Factory.Create(spec, null);
            Assert.AreEqual(SkillIds.Repair, board.Rows[1].Skill.Id);
            Assert.IsFalse(board.Rows[1].IsPowered, "die Reparatur berührt kein Relais");

            BattleResult r = Fight(board, seconds: 30);
            int stabs = Own(r).Count(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == SkillIds.ShockStab);
            int busy = Own(r).Count(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 1 && e.Amount == (int)MissReason.AlreadyQueued);
            Assert.GreaterOrEqual(stabs, 8);
            Assert.AreEqual(stabs / 4, Queued(r, 1) + busy);
        }

        [Test]
        public void ChargeLinkIsATargetedModule()
        {
            ModuleDefinition link = ModuleCatalog.CreateDefault().Get(ModuleIds.ChargeLink);
            Assert.AreEqual(ModuleKind.Trigger, link.Kind);
            Assert.IsTrue(ModuleRules.IsTargeted(ModuleIds.ChargeLink));
            Assert.IsTrue(ModuleRules.IsTargeted(ModuleIds.Trigger));
            Assert.IsFalse(ModuleRules.IsTargeted(ModuleIds.Extend));
        }
    }
}
