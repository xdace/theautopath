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
    /// <summary>
    /// A-20: Pins, Leiterbahnen, Pulse und Logik-Chips. Feuernde Komponenten schicken Pulse über Pins und Leiterbahnen; Ziele
    /// zählen als versorgt vom ursprünglichen Relais. Gatter verknüpfen Relais, Kondensator, Diode und Sicherung.
    /// </summary>
    public class CircuitPulseTests
    {
        private static readonly BoardFactory Factory = BoardFactory.CreateDefault();

        private static RelaySpec Relay(string runeId, int x, int y, int level = 0) => new RelaySpec(runeId, new Cell(x, y), level);

        private static ComponentSpec Part(string skillId, int x, int y, bool rotated = false) => new ComponentSpec(skillId, new Cell(x, y), rotated);

        private static ChipSpec Chip(string chipId, int x, int y, int turns = 0) => new ChipSpec(chipId, new Cell(x, y), turns);

        private static CircuitSpec Spec(IEnumerable<RelaySpec> relays, IEnumerable<ComponentSpec> components, IEnumerable<ChipSpec> chips = null)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.AddRange(relays);
            spec.Components.AddRange(components);
            if (chips != null) spec.Chips.AddRange(chips);
            return spec;
        }

        private static LogicBoard Compile(CircuitSpec spec, BoardFactory factory = null) => (factory ?? Factory).Create(spec, null);

        /// <summary>Ritter mit viel HP gegen einen zähen Gegner, der alle <paramref name="enemyInterval"/> Ticks leicht trifft.</summary>
        private static BattleResult Fight(LogicBoard board, int enemyInterval = 1000, int seconds = 10, int seed = 1)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: board), Fighter("B", 100000, 1, enemyInterval), seed);
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds);
            return CombatSimulation.Run(setup);
        }

        private static IEnumerable<BattleEvent> Own(BattleResult r) => r.Events.Where(e => e.Source?.Name == "A");

        private static List<BattleEvent> Starts(BattleResult r, string skillId) =>
            Own(r).Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId).ToList();

        private static PulseLink Link(LogicBoard board, int from, int to) =>
            board.Links.FirstOrDefault(l => l.From.Equals(PulseNode.Component(from)) && l.To.Equals(PulseNode.Component(to)));

        // ------------------------------------------------------------------ Pins

        [Test]
        public void TouchingPinsConnectComponentsBothWays()
        {
            // Schockstich (Pin rechts) neben Schubdüsen (Pin links): verbunden, eine Verbindung = ein Tick.
            LogicBoard board = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 2, 0) }));
            Assert.AreEqual(1, Link(board, 0, 1)?.Delay);
            Assert.AreEqual(1, Link(board, 1, 0)?.Delay);

            // Ladungsspule hat Pins nur oben und unten: neben dem Schockstich keine Verbindung.
            board = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.ChargeCoil, 2, 0) }));
            Assert.IsEmpty(board.Links);
        }

        [Test]
        public void PinsTurnWithTheComponent()
        {
            var pins = PinCatalog.CreateDefault();
            // Schockstich ungedreht: links und rechts. Gedreht (90° im Uhrzeigersinn): oben und unten.
            List<PlacedPin> flat = pins.Place(SkillIds.ShockStab, new Shape(1, 1), new Cell(2, 2), false);
            List<PlacedPin> turned = pins.Place(SkillIds.ShockStab, new Shape(1, 1), new Cell(2, 2), true);
            CollectionAssert.AreEquivalent(new[] { Edge.Left, Edge.Right }, flat.Select(p => p.Side));
            CollectionAssert.AreEquivalent(new[] { Edge.Up, Edge.Down }, turned.Select(p => p.Side));

            // Kühlmittel 1×2: Pin unten bei (0, 1) wird gedreht zu Pin links bei (0, 0) der 2×1-Form.
            PlacedPin bottom = pins.Place(SkillIds.Coolant, new Shape(1, 2), new Cell(0, 0), true).Single(p => p.IsTyped);
            Assert.AreEqual(Edge.Left, bottom.Side);
            Assert.AreEqual(new Cell(0, 0), bottom.Cell);
        }

        [Test]
        public void ATypedPinWithAMatchingNeighbourGivesThePinBonus()
        {
            // Schockstich hat rechts einen Schock-Pin: ein zweiter Schockstich daneben (Schock) erfüllt ihn, Schubdüsen nicht.
            LogicBoard shock = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.ShockStab, 2, 0) }));
            Assert.AreEqual(1, shock.Rows[0].MatchedPins);
            Assert.AreEqual(0, shock.Rows[1].MatchedPins, "der rechte Pin des zweiten zeigt ins Leere");
            Assert.AreEqual(PinConfig.Default.TypedPinBonusPercent, shock.Rows[0].Skill.PowerBonusPercent - shock.Rows[1].Skill.PowerBonusPercent);

            LogicBoard other = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 2, 0) }));
            Assert.AreEqual(0, other.Rows[0].MatchedPins, "Schubdüsen sind kein Schock-Skill");
        }

        // ------------------------------------------------------------------ Leiterbahnen und Diode

        [Test]
        public void TracesConnectDistantPinsOneConnectionPerTick()
        {
            // Gerade: Schockstich – Bahn – Bahn – Schubdüsen: drei Verbindungen.
            LogicBoard straight = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.Thrusters, 3, 0) },
                new[] { Chip(ChipIds.Trace, 1, 0), Chip(ChipIds.Trace, 2, 0) }));
            PulseLink link = Link(straight, 0, 1);
            Assert.IsNotNull(link);
            Assert.AreEqual(3, link.Delay);
            CollectionAssert.AreEqual(new[] { new Cell(1, 0), new Cell(2, 0) }, link.Path);

            // Ecke und senkrechte Bahn: vom rechten Pin des Schockstichs nach unten zum oberen Pin der Ladungsspule.
            LogicBoard corner = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.ChargeCoil, 1, 2) },
                new[] { Chip(ChipIds.TraceCorner, 1, 0, 2), Chip(ChipIds.Trace, 1, 1, 1) }));
            Assert.AreEqual(3, Link(corner, 0, 1)?.Delay);

            // Falsch gedrehte Bahn leitet nicht.
            LogicBoard broken = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.Thrusters, 2, 0) },
                new[] { Chip(ChipIds.Trace, 1, 0, 1) }));
            Assert.IsEmpty(broken.Links);
        }

        [Test]
        public void ADiodeLetsPulsesPassOneWayOnly()
        {
            LogicBoard forward = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.Thrusters, 2, 0) },
                new[] { Chip(ChipIds.Diode, 1, 0) }));
            Assert.AreEqual(2, Link(forward, 0, 1)?.Delay);
            Assert.IsNull(Link(forward, 1, 0));

            LogicBoard backward = Compile(Spec(new RelaySpec[0], new[] { Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.Thrusters, 2, 0) },
                new[] { Chip(ChipIds.Diode, 1, 0, 2) }));
            Assert.IsNull(Link(backward, 0, 1));
            Assert.AreEqual(2, Link(backward, 1, 0)?.Delay);
        }

        // ------------------------------------------------------------------ Pulse im Kampf

        /// <summary>Kampfbeginn versorgt den Schockstich; über eine Diode geht ein Puls zu den Schubdüsen (ohne Rückweg).</summary>
        private static LogicBoard PulseChain(string targetSkill = SkillIds.Thrusters, string rune = "battle_start") =>
            Compile(Spec(new[] { Relay(rune, 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(targetSkill, 3, 0) },
                new[] { Chip(ChipIds.Diode, 2, 0) }));

        [Test]
        public void APulseQueuesItsTargetWhichCastsNormally()
        {
            LogicBoard board = PulseChain();
            Assert.IsFalse(board.Rows[1].IsPowered, "die Schubdüsen berühren kein Relais");
            BattleResult r = Fight(board);

            BattleEvent stab = Own(r).First(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == SkillIds.ShockStab);
            BattleEvent sent = Own(r).First(e => e.Kind == BattleEventKind.PulseSent);
            Assert.AreEqual(stab.Tick, sent.Tick);
            Assert.AreEqual(2, sent.Amount, "Diode + Pin: zwei Verbindungen");

            BattleEvent queued = Own(r).First(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == 1);
            Assert.AreEqual(stab.Tick + 2, queued.Tick);
            Assert.IsTrue(queued.IsPulse);
            Assert.AreEqual(0, queued.CauseRow);
            Assert.AreEqual(0, queued.Relay, "zählt als versorgt vom ursprünglichen Relais");

            List<BattleEvent> thrusters = Starts(r, SkillIds.Thrusters);
            Assert.AreEqual(1, thrusters.Count);
            Assert.Greater(thrusters[0].Amount, 0, "mit Cast-Zeit");
            Assert.AreEqual(1, BattleReport.Create(r).Rows[1].FromPulse);
        }

        [Test]
        public void APulseInheritsTheLimitAndBonusOfTheOriginalRelay()
        {
            // Kampfbeginn (◇, 1 Zelle): die 2×1-Kryogranate ist für das ursprüngliche Relais zu gross.
            BattleResult easy = Fight(PulseChain(SkillIds.CryoGrenade));
            Assert.IsEmpty(Starts(easy, SkillIds.CryoGrenade));
            BattleEvent missed = Own(easy).First(e => e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == 1);
            Assert.AreEqual(MissReason.TooLarge, (MissReason)missed.Amount);

            // «HP Full» (◆, bis 2 Zellen): sie passt und läuft mit dessen Bonus-Stufe, nicht gestapelt.
            BattleResult medium = Fight(PulseChain(SkillIds.CryoGrenade, "hp_full"));
            BattleEvent cryo = Starts(medium, SkillIds.CryoGrenade).First();
            Assert.AreEqual(1, cryo.Tier);
            Assert.IsTrue(cryo.IsPulse);
        }

        [Test]
        public void ACircleOfPulsesStaysStableAndIsLimitedByCastTime()
        {
            // Schockstich und Schubdüsen berühren sich mit Pins: ein Kreis. Ein Kampfbeginn startet ihn.
            LogicBoard board = Compile(Spec(new[] { Relay("battle_start", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 2, 0) }));
            BattleResult r = Fight(board, seconds: 10);

            int stabs = Starts(r, SkillIds.ShockStab).Count;
            int thrusters = Starts(r, SkillIds.Thrusters).Count;
            Assert.Greater(stabs, 3);
            Assert.That(thrusters, Is.InRange(stabs - 1, stabs), "der Kreis wechselt sich ab");

            // Jede Ausführung braucht ihre Cast-Zeit: nie zwei Starts im selben Tick, höchstens so viele wie Ticks / Mindest-Cast.
            List<int> ticks = Own(r).Where(e => e.Kind == BattleEventKind.ActionStarted).Select(e => e.Tick).ToList();
            Assert.AreEqual(ticks.Count, ticks.Distinct().Count());
            Assert.LessOrEqual(stabs + thrusters, Ticks.FromSeconds(10) / CastTime.DefaultMinTicks);
        }

        [Test]
        public void TheSameSeedGivesTheSamePulses()
        {
            CircuitSpec spec = Spec(new[] { Relay("clock", 0, 0), Relay("when_hit", 2, 1) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 4, 0), Part(SkillIds.CryoGrenade, 0, 2) },
                new[] { Chip(ChipIds.Capacitor, 2, 0), Chip(ChipIds.Trace, 3, 0) });
            string Log() => string.Join("\n", Fight(Compile(spec), enemyInterval: 20, seed: 4).Events.Select(e => BattleLogText.Describe(e)));
            Assert.AreEqual(Log(), Log());
        }

        // ------------------------------------------------------------------ Gatter

        private static LogicRelay Gate(LogicBoard board) => board.Relays.Single(r => r.Gate != null);

        private static int Fires(BattleResult r, LogicRelay relay) =>
            Own(r).Count(e => e.Kind == BattleEventKind.RelayTriggered && e.Relay == relay.Index);

        [Test]
        public void AndTriggersWhenBothRelaysAreOnAndIsHarder()
        {
            // «HP Full» (◆, Zustand) und Kampfbeginn (◇, Ereignis) an einem UND; der Schockstich liegt unter dem Gatter.
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0), Relay("battle_start", 2, 0) }, new[] { Part(SkillIds.ShockStab, 1, 1) },
                new[] { Chip(ChipIds.And, 1, 0) }));
            LogicRelay and = Gate(board);
            Assert.AreEqual(ChipKind.And, and.Gate);
            Assert.AreEqual(2, and.Inputs.Count);
            Assert.AreEqual(2, and.Difficulty, "höhere + 1");
            CollectionAssert.AreEqual(new[] { 0 }, and.Powered);

            BattleResult r = Fight(board);
            Assert.AreEqual(1, Fires(r, and));
            Assert.AreEqual(2, Starts(r, SkillIds.ShockStab).Single().Tier);

            // Fehlt der Kampfbeginn (nur ein Eingang), löst das UND nie aus.
            LogicBoard alone = Compile(Spec(new[] { Relay("hp_full", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 1) }, new[] { Chip(ChipIds.And, 1, 0) }));
            Assert.IsFalse(Gate(alone).IsGateReady);
            Assert.IsEmpty(Starts(Fight(alone), SkillIds.ShockStab));
        }

        [Test]
        public void OrTriggersOnEitherRelayAndTakesTheLowerDifficulty()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0), Relay("clock", 2, 0) }, new[] { Part(SkillIds.ShockStab, 1, 1) },
                new[] { Chip(ChipIds.Or, 1, 0) }));
            LogicRelay or = Gate(board);
            Assert.AreEqual(0, or.Difficulty, "niedrigere");

            BattleResult r = Fight(board);
            LogicRelay clock = board.Relays.Single(x => x.RuneId == "clock");
            Assert.AreEqual(1 + Fires(r, clock), Fires(r, or), "einmal «HP Full», dann jeder Takt");
        }

        [Test]
        public void NotTriggersWhenItsRelayTurnsOff()
        {
            // NICHT «HP Full»: der erste Treffer des Gegners. Schwierigkeit = umgekehrter Wert der Rune.
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 1) }, new[] { Chip(ChipIds.Not, 1, 0) }));
            LogicRelay not = Gate(board);
            Assert.AreEqual(board.Relays.Single(x => x.Gate == null).InvertedDifficulty, not.Difficulty);

            BattleResult r = Fight(board, enemyInterval: 20);
            int firstHit = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "B").Tick;
            List<BattleEvent> fires = Own(r).Where(e => e.Kind == BattleEventKind.RelayTriggered && e.Relay == not.Index).ToList();
            Assert.AreEqual(1, fires.Count, "die HP werden nie wieder voll");
            Assert.GreaterOrEqual(fires[0].Tick, firstHit, "im Tick des Treffers oder danach");
        }

        [Test]
        public void AFuseTriggersOnceAsAVeryHardRelay()
        {
            // Takt (alle 2 s) an einer Sicherung; der Bohrer (2×2) ist nur für ◆◆+ klein genug.
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0) }, new[] { Part(SkillIds.Drill, 2, 0) }, new[] { Chip(ChipIds.Fuse, 1, 0) }));
            LogicRelay fuse = Gate(board);
            Assert.AreEqual(3, fuse.Difficulty);
            Assert.IsTrue(fuse.OncePerFight);
            CollectionAssert.AreEqual(new[] { 0 }, fuse.Powered);

            BattleResult r = Fight(board);
            Assert.Greater(Fires(r, board.Relays.Single(x => x.RuneId == "clock")), 2);
            Assert.AreEqual(1, Fires(r, fuse));
            Assert.AreEqual(1, Own(r).Count(e => e.Kind == BattleEventKind.FuseBlown));
            Assert.AreEqual(3, Starts(r, SkillIds.Drill).Single().Tier);
        }

        [Test]
        public void GateDifficultyIsData()
        {
            var config = new ChipConfig { AndDifficultyBonus = 0 };
            var easy = new LogicRelay(AlwaysCondition.Instance, "a", 1);
            var hard = new LogicRelay(AlwaysCondition.Instance, "b", 3);
            Assert.AreEqual(3, LogicBoard.GateDifficulty(ChipKind.And, new[] { easy, hard }, ChipConfig.Default), "höchstens 3");
            Assert.AreEqual(1, LogicBoard.GateDifficulty(ChipKind.And, new[] { easy, easy }, config));
            Assert.AreEqual(1, LogicBoard.GateDifficulty(ChipKind.Or, new[] { easy, hard }, config));
            Assert.AreEqual(3, LogicBoard.GateDifficulty(ChipKind.Fuse, new LogicRelay[0], config));
        }

        // ------------------------------------------------------------------ Kondensator

        [Test]
        public void ACapacitorStoresPulsesAndReleasesThemAfterItsTime()
        {
            // Takt → Schockstich → Kondensator → Schubdüsen. Ohne Relais am Kondensator gibt er nach 3 s ab.
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 3, 0) },
                new[] { Chip(ChipIds.Capacitor, 2, 0) }));
            BattleResult r = Fight(board);

            BattleEvent stored = Own(r).First(e => e.Kind == BattleEventKind.CapacitorStored);
            BattleEvent released = Own(r).First(e => e.Kind == BattleEventKind.CapacitorReleased);
            Assert.AreEqual(stored.Tick + ChipConfig.Default.CapacitorReleaseTicks, released.Tick);
            Assert.GreaterOrEqual(released.Amount, 1);

            BattleEvent thrusters = Starts(r, SkillIds.Thrusters).First();
            Assert.Greater(thrusters.Tick, released.Tick, "erst nach dem Entladen");
            Assert.IsTrue(thrusters.IsPulse);
            Assert.AreEqual(0, thrusters.CauseRow, "der Weg führt zurück zum Schockstich");
        }

        [Test]
        public void ACapacitorReleasesWhenATouchingRelayTriggers()
        {
            // «When Hit» unter dem Kondensator: jeder Treffer des Gegners (jede Sekunde) entlädt ihn.
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0), Relay("when_hit", 2, 1) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 3, 0) }, new[] { Chip(ChipIds.Capacitor, 2, 0) }));
            BattleResult r = Fight(board, enemyInterval: 20);
            BattleEvent stored = Own(r).First(e => e.Kind == BattleEventKind.CapacitorStored);
            BattleEvent released = Own(r).First(e => e.Kind == BattleEventKind.CapacitorReleased);
            Assert.Less(released.Tick - stored.Tick, ChipConfig.Default.CapacitorReleaseTicks);
        }

        [Test]
        public void AFullCapacitorLosesFurtherPulses()
        {
            var factory = new BoardFactory(null, null, null, chips: ChipCatalog.CreateDefault(new ChipConfig { CapacitorCapacity = 1, CapacitorReleaseTicks = Ticks.FromSeconds(30) }));
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0, level: 1) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 3, 0) },
                new[] { Chip(ChipIds.Capacitor, 2, 0) }), factory);
            BattleResult r = Fight(board);
            Assert.AreEqual(1, Own(r).Count(e => e.Kind == BattleEventKind.CapacitorStored));
            Assert.Greater(Own(r).Count(e => e.Kind == BattleEventKind.PulseLost), 0);
            Assert.IsEmpty(Starts(r, SkillIds.Thrusters), "nichts entlädt ihn");
        }

        // ------------------------------------------------------------------ Graph, Anzeige, Session

        [Test]
        public void TheLogicGraphHoldsRelaysComponentsChipsPinsAndConnections()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("hp_full", 0, 0), Relay("battle_start", 2, 0) },
                new[] { Part(SkillIds.ShockStab, 1, 1), Part(SkillIds.Thrusters, 2, 1) }, new[] { Chip(ChipIds.And, 1, 0) }));
            LogicGraph g = board.Graph;
            Assert.AreEqual(2, g.OfKind(GraphEdgeKind.And).Count());
            Assert.IsTrue(g.OfKind(GraphEdgeKind.Power).Any(e => e.From.Equals(GraphNode.Block(Gate(board).Index)) && e.To.Equals(GraphNode.Skill(0))));
            Assert.IsTrue(g.OfKind(GraphEdgeKind.IsChip).Any(e => e.To.Equals(GraphNode.Chip(0))));
            Assert.AreEqual(board.Rows.Sum(r => r.Pins.Count), g.OfKind(GraphEdgeKind.PinOf).Count());
            Assert.IsTrue(g.OfKind(GraphEdgeKind.Pulse).Any(e => e.From.Equals(GraphNode.Skill(0)) && e.To.Equals(GraphNode.Skill(1)) && e.Delay == 1));
        }

        [Test]
        public void PlaybackShowsPulsesGatesAndCapacitors()
        {
            LogicBoard board = Compile(Spec(new[] { Relay("clock", 0, 0) }, new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 3, 0) },
                new[] { Chip(ChipIds.Capacitor, 2, 0) }));
            BattleResult r = Fight(board);
            BattleEvent sent = r.Events.First(e => e.Kind == BattleEventKind.PulseSent && e.Source.Side == Side.Player);
            BattleEvent stored = r.Events.First(e => e.Kind == BattleEventKind.CapacitorStored && e.Source.Side == Side.Player);

            var playback = new BattlePlayback(r);
            playback.Advance(sent.Tick);
            Assert.IsNotEmpty(playback.Pulses);
            Assert.AreEqual(sent.Extra, playback.Pulses[0].Link);
            playback.Advance(stored.Tick - playback.Tick);
            Assert.AreEqual(1, playback.CapacitorCharge(0));
            Assert.IsTrue(playback.Entries.Any(e => e.Text.Contains("pulse")));
        }

        // ------------------------------------------------------------------ Beispiele aus der README

        /// <summary>Wie im Spiel: Kern bei (1, 1) mit +10 %.</summary>
        private static CircuitSpec WithCore(CircuitSpec spec)
        {
            spec.Core = new Cell(1, 1);
            spec.CoreBonusPercent = 10;
            return spec;
        }

        [Test]
        public void ReadmeExampleOpeningComboChainsByPulse()
        {
            // Battle Start → Shock Stab → Diode → Thrusters: die Schubdüsen feuern 2 Ticks nach dem Stich, versorgt vom Kampfbeginn.
            BattleResult r = Fight(Compile(WithCore(Spec(new[] { Relay("battle_start", 0, 0) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.Thrusters, 3, 0) }, new[] { Chip(ChipIds.Diode, 2, 0) }))));
            BattleEvent stab = Own(r).First(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == SkillIds.ShockStab);
            BattleEvent queued = Own(r).First(e => e.Kind == BattleEventKind.RowQueued && e.RowIndex == 1);
            Assert.AreEqual(stab.Tick + 2, queued.Tick);
            Assert.AreEqual(0, queued.Relay);
            Assert.AreEqual(1, Starts(r, SkillIds.ShockStab).Count, "die Diode verhindert den Kreis");
        }

        [Test]
        public void ReadmeExampleAndGateMakesAHarderOpener()
        {
            // HP Full (◆) UND Battle Start (◇) → ◆◆: der Schockstich neben dem Gatter (und am Kern) läuft mit Stufe 2.
            LogicBoard board = Compile(WithCore(Spec(new[] { Relay("hp_full", 3, 0), Relay("battle_start", 3, 2) }, new[] { Part(SkillIds.ShockStab, 2, 1) },
                new[] { Chip(ChipIds.And, 3, 1) })));
            Assert.IsTrue(board.Rows[0].TouchesCore);
            BattleEvent start = Starts(Fight(board), SkillIds.ShockStab).First();
            Assert.AreEqual(2, start.Tier);
            Assert.AreEqual(Gate(board).Index, start.Relay);
        }

        [Test]
        public void ReadmeExampleCapacitorSavesPulsesForACounter()
        {
            // Clock → Shock Stab → Diode → Capacitor → Trace → Lightning Lance; When Hit berührt den Kondensator.
            LogicBoard board = Compile(WithCore(Spec(new[] { Relay("clock", 0, 0), Relay("when_hit", 3, 1) },
                new[] { Part(SkillIds.ShockStab, 1, 0), Part(SkillIds.LightningLance, 5, 0) },
                new[] { Chip(ChipIds.Diode, 2, 0), Chip(ChipIds.Capacitor, 3, 0), Chip(ChipIds.Trace, 4, 0) })));
            Assert.IsFalse(board.Rows[1].IsPowered, "die Lanze berührt kein Relais");
            Assert.AreEqual(2, board.Links.Single(l => l.From.Equals(PulseNode.Component(0))).Delay);
            Assert.IsFalse(board.Links.Any(l => l.From.IsCapacitor && l.To.Equals(PulseNode.Component(0))), "die Diode sperrt den Rückweg");
            CollectionAssert.AreEqual(new[] { 1 }, board.Chips.Single(c => c.Kind == ChipKind.Capacitor).ReleaseRelays);

            BattleResult r = Fight(board, enemyInterval: 70);
            Assert.IsTrue(Own(r).Any(e => e.Kind == BattleEventKind.CapacitorStored));
            Assert.IsTrue(Own(r).Any(e => e.Kind == BattleEventKind.CapacitorReleased));
            BattleEvent lance = Starts(r, SkillIds.LightningLance).First();
            Assert.IsTrue(lance.IsPulse);
            Assert.AreEqual(0, lance.Relay, "versorgt von der Clock");
        }

        [Test]
        public void TheSessionPlacesTurnsMovesAndRemovesChips()
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Seed = 3 }, 1, KnightKit.Defaults[0]);
            Assert.IsNotNull(s.GrantChip(ChipIds.Trace));
            Assert.IsNull(s.GrantChip("nope"));
            Assert.AreEqual(1, s.ChipInventory.Count);

            Assert.IsFalse(s.PlaceChip(0, s.Board.Core), "nicht auf den Kern");
            Assert.IsTrue(s.PlaceChip(0, new Cell(3, 2)));
            Assert.IsEmpty(s.ChipInventory);
            Assert.IsTrue(s.RotateChip(0));
            Assert.AreEqual(1, s.Board.Chips[0].Turns);
            Assert.IsTrue(s.MoveChip(0, new Cell(3, 1)));

            ChipSpec spec = s.Board.ToSpec().Chips.Single();
            Assert.AreEqual(ChipIds.Trace, spec.ChipId);
            Assert.AreEqual(new Cell(3, 1), spec.Position);
            Assert.AreEqual(1, spec.Turns);
            Assert.AreEqual(1, s.CompileBoard().Chips.Count);

            Assert.IsTrue(s.RemoveChip(0));
            Assert.IsEmpty(s.Board.Chips);
            Assert.AreEqual(1, s.ChipInventory.Count);
        }

        [Test]
        public void ChipsAreRareRewards()
        {
            var progression = new ProgressionConfig();
            Assert.Greater(progression.ChipChance(RewardSources.Elite), 0);
            Assert.Greater(progression.ChipChance(RewardSources.Treasure), 0);
            Assert.AreEqual(0, progression.ChipChance(RewardSources.Victory), "normale Siege geben keine Chips");
            Assert.AreEqual(1, progression.ChipsOnBossEscape);
            Assert.Greater(progression.ShopChipChance, 0);

            ChipCatalog catalog = ChipCatalog.CreateDefault();
            var rolled = Enumerable.Range(0, 200).Select(i => catalog.Roll(new System.Random(i)).Id).ToList();
            CollectionAssert.AreEquivalent(catalog.All.Select(c => c.Id), rolled.Distinct(), "jeder Chip kann fallen");
            Assert.Greater(rolled.Count(id => id == ChipIds.Trace), rolled.Count(id => id == ChipIds.Fuse), "Leiterbahnen häufiger als Sicherungen");
        }
    }
}
