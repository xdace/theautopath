using System.Linq;
using Betaknight.Core.Arena;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class PlaybackTests
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        private static BattleResult Fight()
        {
            var board = new LogicBoard(new[]
            {
                new LogicRow(ConditionRegistry.CreateDefault().Create("enemy_charging", 0), Skills.Get(SkillIds.ShieldBash), "Gegner lädt auf"),
            });
            CombatantSetup golem = SetTests.Golem();
            return CombatSimulation.Run(Duel(Fighter("Ritter", 40, 4, board: board), golem, 3));
        }

        [Test]
        public void PlaybackEndsInTheSimulatedState()
        {
            BattleResult r = Fight();
            var stepwise = new BattlePlayback(r);
            while (!stepwise.IsFinished) stepwise.Advance(3);

            var skipped = new BattlePlayback(r);
            skipped.SkipToEnd();

            Assert.AreEqual(r.PlayerHp, stepwise.Player.Hp);
            Assert.AreEqual(r.PlayerHp, skipped.Player.Hp);
            Assert.AreEqual(stepwise.Fighters[1].Hp, skipped.Fighters[1].Hp);
            Assert.AreEqual(r.IsVictory, !stepwise.Fighters[1].Alive);
            CollectionAssert.AreEqual(stepwise.Lines, skipped.Lines);
            StringAssert.EndsWith(BattleLogText.OutcomeText(r.Outcome), stepwise.Lines.Last());
        }

        [Test]
        public void FiringRowLightsUpAndIsNamed()
        {
            BattleResult r = Fight();
            int bash = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ShieldBash).Tick;
            var p = new BattlePlayback(r);

            p.Advance(bash);
            Assert.IsTrue(p.IsRowHighlighted(0));
            Assert.IsTrue(p.Lines.Any(l => l.Contains("[Gegner lädt auf] → Schildschlag")));

            p.Advance(BattlePlayback.RowHighlightTicks);
            Assert.IsFalse(p.IsRowHighlighted(0));
        }

        [Test]
        public void ResultCarriesFightersAndRowSkills()
        {
            BattleResult r = Fight();
            Assert.AreEqual(2, r.Fighters.Count);
            Assert.AreEqual(Side.Player, r.Fighters[0].Side);
            Assert.AreEqual(40, r.Fighters[0].StartHp);
            CollectionAssert.AreEqual(new[] { "Schildschlag", "Basisangriff" }, r.PlayerRowSkills);
        }
    }
}
