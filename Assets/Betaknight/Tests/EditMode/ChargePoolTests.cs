using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Skills;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Ladung als Topf: ein Relais verteilt seine Grenze auf alle berührten Komponenten. Kleine zuerst, Gleichstand zufällig
    /// (wenn nur einer feuern kann) bzw. gleichmässig (wenn keiner feuern kann), Rest an die wartenden, Überladung +10 % je Feld.
    /// </summary>
    public class ChargePoolTests
    {
        private static readonly BoardFactory Factory = BoardFactory.CreateDefault();

        private static LogicBoard Board(RelaySpec relay, params ComponentSpec[] components)
        {
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(relay);
            spec.Components.AddRange(components);
            return Factory.Create(spec, null);
        }

        private static RelaySpec Relay(string runeId, int x, int y) => new RelaySpec(runeId, new Cell(x, y));

        private static ComponentSpec Part(string skillId, int x, int y, params ModuleSpec[] modules) =>
            new ComponentSpec(skillId, new Cell(x, y), modules: modules);

        private static BattleResult Fight(LogicBoard board, int seconds = 3, bool vsBoss = false)
        {
            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: board), Fighter("B", 100000, 1, 1000));
            setup.Context.VsBoss = vsBoss;
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        private static List<BattleEvent> Queued(BattleResult r, int row) =>
            r.Events.Where(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.RowQueued && e.RowIndex == row).ToList();

        private static int Charging(BattleResult r, int row, int after = -1) =>
            r.Events.Count(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.Charged && e.RowIndex == row && e.Tick > after);

        private static int Row(LogicBoard board, string skillId, int nth = 0) =>
            board.Rows.Where(x => x.Skill?.Id == skillId).Skip(nth).First().Index;

        [Test]
        public void SixChargeFiresATwoAndAFourCellSkillTogether()
        {
            // «Vs. Boss» (●●●, 6 Zellen) löst einmal aus: 2 + 4 = 6, beide laufen, nichts bleibt übrig.
            LogicBoard board = Board(Relay("vs_boss", 1, 0), Part(SkillIds.Ignite, 0, 0), Part(SkillIds.ArmorBreak, 2, 0));
            Assert.AreEqual(6, board.Relays[0].MaxCells);
            BattleResult r = Fight(board, vsBoss: true);
            Assert.AreEqual(0, Queued(r, Row(board, SkillIds.Ignite)).Single().Power);
            Assert.AreEqual(0, Queued(r, Row(board, SkillIds.ArmorBreak)).Single().Power);
        }

        [Test]
        public void OverchargeGivesTenPercentPerCellWithoutCap()
        {
            // 6 Ladung auf 1 Zelle: 5 Felder Überladung = +50 %.
            LogicBoard board = Board(Relay("vs_boss", 0, 0), Part(SkillIds.ShockStab, 1, 0));
            BattleResult r = Fight(board, vsBoss: true);
            Assert.AreEqual(5 * CircuitEffectConfig.Default.OverchargePowerPercentPerCell, Queued(r, 0).Single().Power);
        }

        [Test]
        public void TheSmallestNeedIsFilledFirstAndTheRestGoesToTheOthers()
        {
            // «HP Full» (●, 2 Ladung): der Schockstich braucht 1 und läuft, der Rüstungsbrecher (4) bekommt den Rest 1 und lädt.
            LogicBoard board = Board(Relay("hp_full", 1, 0), Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.ArmorBreak, 2, 0));
            BattleResult r = Fight(board, seconds: 1);
            // Nur das erste Auslösen (Tick 1) zählt, danach pulst «HP Full» alle 2 s.
            Assert.AreEqual(1, Queued(r, Row(board, SkillIds.ShockStab)).Count(e => e.Tick == 1));
            Assert.IsFalse(Queued(r, Row(board, SkillIds.ArmorBreak)).Any(e => e.Tick == 1));
            Assert.AreEqual(1, Charging(r, Row(board, SkillIds.ArmorBreak)) - Charging(r, Row(board, SkillIds.ArmorBreak), after: 1));
        }

        [Test]
        public void ATieThatCannotFireIsSplitEvenly()
        {
            // «Opening 5 s» (●, 2 Ladung) löst jede Sekunde aus; zwei Rüstungsbrecher (je 4) bekommen erst je 1 (keiner kann
            // laufen), nach zwei Auslösungen brauchen beide noch 2: Gleichstand, nur einer kann laufen, zufällig gewählt.
            // Die beiden berühren sich nicht (sonst schicken sie sich Pulse über ihre Pins).
            LogicBoard board = Board(new RelaySpec("opening", new Cell(2, 2), level: 2), Part(SkillIds.ArmorBreak, 0, 1), Part(SkillIds.ArmorBreak, 3, 1));
            BattleResult r = Fight(board, seconds: 6);
            int first = Row(board, SkillIds.ArmorBreak), second = Row(board, SkillIds.ArmorBreak, 1);
            List<int> triggers = r.Events.Where(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.RelayTriggered).Select(e => e.Tick).ToList();
            Assert.GreaterOrEqual(triggers.Count, 4);

            // Die ersten beiden Auslösungen laden nur, beide gleich viel.
            Assert.IsFalse(Queued(r, first).Concat(Queued(r, second)).Any(e => e.Tick <= triggers[1]));
            Assert.AreEqual(2, Charging(r, first) - Charging(r, first, after: triggers[1]));
            Assert.AreEqual(2, Charging(r, second) - Charging(r, second, after: triggers[1]));

            // Beim 3. Auslösen läuft genau einer, beim 4. der andere: 4 × 2 Ladung = zweimal 4 Zellen.
            Assert.AreEqual(1, Queued(r, first).Concat(Queued(r, second)).Count(e => e.Tick == triggers[2]));
            Assert.AreEqual(1, Queued(r, first).Count(e => e.Tick <= triggers[3]));
            Assert.AreEqual(1, Queued(r, second).Count(e => e.Tick <= triggers[3]));
        }

        [Test]
        public void ATieWhereOnlyOneCanFirePicksOne()
        {
            // «HP Full» (2 Ladung), zwei Ignite (je 2 Zellen): genau einer läuft, der andere lädt nicht (kein Rest).
            LogicBoard board = Board(Relay("hp_full", 1, 0), Part(SkillIds.Ignite, 0, 0), Part(SkillIds.Ignite, 2, 0));
            BattleResult r = Fight(board, seconds: 1);
            // Nur das erste Auslösen zählt (danach pulst «HP Full» alle 2 s).
            int a = Queued(r, Row(board, SkillIds.Ignite)).Count(e => e.Tick == 1), b = Queued(r, Row(board, SkillIds.Ignite, 1)).Count(e => e.Tick == 1);
            Assert.AreEqual(1, a + b);
        }

        [Test]
        public void TheChargeBelongsToTheComponentAndAllRelaysFillIt()
        {
            // Rail Cannon (6 Zellen) an zwei Relais: «Opening 5 s» (●, 2 pro Sekunde) und «HP Full» (●, 2 zu Kampfbeginn).
            // Beide füllen denselben Speicher: 2 + 2 im ersten Tick, nach der nächsten Sekunde 6 → die Kanone läuft.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(new RelaySpec("opening", new Cell(0, 0), level: 2));
            spec.Relays.Add(new RelaySpec("hp_full", new Cell(0, 2)));
            spec.Components.Add(Part(SkillIds.RailCannon, 1, 0));
            LogicBoard board = Factory.Create(spec, null);
            Assert.AreEqual(6, board.Rows[0].Cells);
            Assert.AreEqual(2, board.Relays.Count(x => x.TooLarge.Contains(0)));

            BattleResult r = Fight(board, seconds: 4);
            BattleEvent first = Queued(r, 0).First();
            Assert.AreEqual(Ticks.PerSecond + 1, first.Tick, "ohne gemeinsamen Speicher erst nach 3 Opening-Auslösungen (2 s)");
        }

        [Test]
        public void ChargeIsKeptWhileQueuedAndRequeuesAfterTheCast()
        {
            // «Vs. Boss» und «On Gold Mine» (je ●●●, 6 Ladung) lösen zu Kampfbeginn im selben Tick aus: 12 Ladung für die
            // Rail Cannon (6 Zellen). Die erste Ladung reiht sie ein, die zweite bleibt gespeichert (sie steht ja schon in der
            // Warteschlange) und reiht sie direkt nach der ersten Ausführung wieder ein.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(new RelaySpec("vs_boss", new Cell(0, 0)));
            spec.Relays.Add(new RelaySpec("on_goldmine", new Cell(0, 2)));
            spec.Components.Add(Part(SkillIds.RailCannon, 1, 0));
            LogicBoard board = Factory.Create(spec, null);

            BattleSetup setup = Duel(Fighter("A", 100000, 10, 20, board: board), Fighter("B", 100000, 1, 1000));
            setup.Context.VsBoss = true;
            setup.Context.OnGoldMine = true;
            setup.TimeLimitTicks = Ticks.FromSeconds(4);
            setup.MaxTicks = Ticks.FromSeconds(9);
            BattleResult r = CombatSimulation.Run(setup);

            List<BattleEvent> queued = Queued(r, 0);
            Assert.AreEqual(2, queued.Count, "zweimal geladen, zweimal gelaufen");
            int firstDone = r.Events.First(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.ActionExecuted && e.RowIndex == 0).Tick;
            Assert.AreEqual(firstDone, queued[1].Tick, "gleich nach der ersten Ausführung wieder eingereiht");
        }

        [Test]
        public void ChargeCoilChargesEveryTouchingComponent()
        {
            // «Clock 2 s» lässt die Charge Coil laufen; sie gibt Schockstich (1 Zelle) und Rüstungsbruch (4 Zellen, kein Relais)
            // je +2: der Schockstich (1 Zelle) läuft damit zweimal (die zweite Ladung reiht ihn nach dem Cast erneut ein), der
            // Rüstungsbruch jedes 2. Mal.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(new RelaySpec("clock", new Cell(0, 0)));
            spec.Components.Add(Part(SkillIds.ChargeCoil, 1, 0));
            spec.Components.Add(Part(SkillIds.ShockStab, 1, 1));
            spec.Components.Add(Part(SkillIds.ArmorBreak, 2, 0));
            LogicBoard board = Factory.Create(spec, null);
            int coil = Row(board, SkillIds.ChargeCoil), stab = Row(board, SkillIds.ShockStab), breaker = Row(board, SkillIds.ArmorBreak);
            Assert.IsFalse(board.Rows[stab].IsPowered);
            Assert.IsFalse(board.Rows[breaker].IsPowered);

            BattleResult r = Fight(board, seconds: 12);
            int coils = r.Events.Count(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.ActionExecuted && e.RowIndex == coil);
            Assert.GreaterOrEqual(coils, 4);
            Assert.That(Queued(r, stab).Count, Is.InRange(2 * coils - 1, 2 * coils));
            Assert.AreEqual(coils / 2, Queued(r, breaker).Count);
        }

        [Test]
        public void AComponentThatKeepsRequeuingCannotStarveTheOthers()
        {
            // Der Rüstungsbruch (#1, oben links) wird über «Opening» und gespeicherte Ladung ständig neu eingereiht. Die
            // Warteschlange läuft in Auslöse-Reihenfolge: er stellt sich hinten an, der Schockstich (#2) kommt dran.
            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Relays.Add(new RelaySpec("vs_boss", new Cell(2, 0)));
            spec.Relays.Add(new RelaySpec("opening", new Cell(2, 1), level: 2));
            spec.Components.Add(Part(SkillIds.ArmorBreak, 0, 0));
            spec.Relays.Add(new RelaySpec("hp_full", new Cell(0, 3)));
            spec.Components.Add(Part(SkillIds.ShockStab, 1, 3));
            LogicBoard board = Factory.Create(spec, null);
            int stab = Row(board, SkillIds.ShockStab);
            Assert.Greater(stab, Row(board, SkillIds.ArmorBreak), "später in der Lesereihenfolge");

            BattleResult r = Fight(board, seconds: 6, vsBoss: true);
            BattleEvent start = r.Events.FirstOrDefault(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.ActionStarted && e.RowIndex == stab);
            Assert.IsNotNull(start, "der Schockstich kommt dran");
            Assert.LessOrEqual(start.Tick, Ticks.FromSeconds(3));
        }

        [Test]
        public void SpilloverPassesTheOverchargeToTheNeighbours()
        {
            // 6 Ladung auf den Schockstich (1 Zelle) mit Spillover: 5 gehen an den Rüstungsbrecher daneben (4 Zellen, berührt
            // kein Relais). Er läuft, 1 Feld bleibt als Überladung (+10 %). Der Schockstich selbst bekommt keinen Bonus.
            ModuleSpec spill = new ModuleSpec(CircuitEffectIds.Spillover);
            LogicBoard board = Board(Relay("vs_boss", 0, 0), Part(SkillIds.ShockStab, 1, 0, spill), Part(SkillIds.ArmorBreak, 2, 0));
            int stab = Row(board, SkillIds.ShockStab), breaker = Row(board, SkillIds.ArmorBreak);
            Assert.IsFalse(board.Rows[breaker].IsPowered);

            BattleResult r = Fight(board, vsBoss: true);
            // Danach schicken sich die beiden über ihre Pins Pulse; hier zählt nur das erste Auslösen.
            Assert.AreEqual(0, Queued(r, stab).First().Power);
            Assert.AreEqual(5, r.Events.Single(e => e.Kind == BattleEventKind.Spillover).Amount);
            Assert.AreEqual(CircuitEffectConfig.Default.OverchargePowerPercentPerCell, Queued(r, breaker).First().Power);
        }
    }
}
