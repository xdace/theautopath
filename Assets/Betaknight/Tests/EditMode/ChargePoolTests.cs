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
            r.Events.Count(e => e.Source?.Name == "A" && e.Kind == BattleEventKind.TriggerMissed && e.RowIndex == row && e.Amount == (int)MissReason.TooLarge
                && e.Tick > after);

        private static int Row(LogicBoard board, string skillId, int nth = 0) =>
            board.Rows.Where(x => x.Skill?.Id == skillId).Skip(nth).First().Index;

        [Test]
        public void SixChargeFiresATwoAndAFourCellSkillTogether()
        {
            // «Vs. Boss» (◆◆◆, 6 Zellen) löst einmal aus: 2 + 4 = 6, beide laufen, nichts bleibt übrig.
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
            // «HP Full» (◆, 2 Ladung): der Schockstich braucht 1 und läuft, der Rüstungsbrecher (4) bekommt den Rest 1 und lädt.
            LogicBoard board = Board(Relay("hp_full", 1, 0), Part(SkillIds.ShockStab, 0, 0), Part(SkillIds.ArmorBreak, 2, 0));
            BattleResult r = Fight(board);
            Assert.AreEqual(1, Queued(r, Row(board, SkillIds.ShockStab)).Count);
            Assert.IsEmpty(Queued(r, Row(board, SkillIds.ArmorBreak)));
            Assert.AreEqual(1, Charging(r, Row(board, SkillIds.ArmorBreak)));
        }

        [Test]
        public void ATieThatCannotFireIsSplitEvenly()
        {
            // «Opening 5 s» (◆, 2 Ladung) löst jede Sekunde aus; zwei Rüstungsbrecher (je 4) bekommen erst je 1 (keiner kann
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
            BattleResult r = Fight(board);
            int a = Queued(r, Row(board, SkillIds.Ignite)).Count, b = Queued(r, Row(board, SkillIds.Ignite, 1)).Count;
            Assert.AreEqual(1, a + b);
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
