using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Die Sets und die gewollt kaputten Kombinationen aus dem Konzept: sie sollen stark sein, die Engine stabil.</summary>
    public class SetTests
    {
        private static readonly EquipmentCatalog Items = EquipmentCatalog.CreateDefault();

        private static Equipment Wear(params string[] ids)
        {
            var gear = new Equipment();
            foreach (string id in ids) Assert.IsNotNull(gear.Equip(Items.Get(id)), id);
            return gear;
        }

        private static BoardRowSpec R(string rune, string skill, int level = 0) => new BoardRowSpec(rune, skill, level);

        private static CombatantSetup Knight(Equipment gear, params BoardRowSpec[] rows) =>
            PlayerLoadout.CreateCombatant("Ritter", new CombatStats(36, 4, 20, 0), gear, rows);

        /// <summary>Referenzgegner mit sichtbarer Aufladung, etwa Stufe eines frühen Elite-Kampfs.</summary>
        internal static CombatantSetup Golem()
        {
            var slam = new SkillDefinition("slam", "Hammerschlag", 30, 6, Ticks.FromSeconds(6),
                new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(300)) }, countsAsAttack: true);
            var board = new LogicBoard(new[] { new LogicRow(AlwaysCondition.Instance, slam, "Immer") });
            return new CombatantSetup { Name = "Golem", Stats = new CombatStats(80, 2, 20, 2), Board = board };
        }

        private static BattleResult Fight(CombatantSetup player, CombatantSetup enemy = null, BattleContext context = null, int seed = 7)
        {
            BattleSetup setup = Duel(player, enemy ?? Golem(), seed);
            if (context != null) setup.Context = context;
            BattleResult r = CombatSimulation.Run(setup);
            TestContext.WriteLine($"{r.Outcome} nach {r.EndTick} Ticks, HP {r.PlayerHp}/{r.PlayerMaxHp}");
            return r;
        }

        private static int Starts(BattleResult r, string skill) => r.Events.Count(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skill && e.Source.Side == Side.Player);

        [Test]
        public void SetBonusesNeedTwoPieces()
        {
            var sets = SetBonusRegistry.CreateDefault();
            Assert.IsEmpty(sets.CreateModifiers(Wear("thermo_blade")));
            Assert.IsInstanceOf<OverloadSet>(sets.CreateModifiers(Wear("thermo_blade", "warning_visor")).Single());
            Assert.IsFalse(((OverloadSet)sets.CreateModifiers(Wear("thermo_blade", "warning_visor")).Single()).Full);
            Assert.IsTrue(((OverloadSet)sets.CreateModifiers(Wear("thermo_blade", "warning_visor", "overload_chassis")).Single()).Full);
        }

        [Test]
        public void ReferenceKnightWithoutSetsLoses()
        {
            BattleResult r = Fight(Knight(Wear("short_sword")));
            Assert.AreEqual(BattleOutcome.Defeat, r.Outcome, "Der Golem ist ohne Build ein echter Prüfstein.");
        }

        [Test]
        public void OverloadStacksTempoAndBurnsItself()
        {
            BattleResult r = Fight(Knight(Wear("thermo_blade", "warning_visor")), Fighter("Sack", 100000, 0, 1000));
            Assert.Greater(r.Events.Count(e => e.Kind == BattleEventKind.SelfDamage && e.Detail == OverloadSet.HeatDetail), 5);
            Assert.AreEqual(BattleOutcome.Defeat, r.Outcome, "Ohne Heilung bringt sich das halbe Set selbst um.");
        }

        [Test]
        public void BrokenOverloadCoolantLoop()
        {
            BattleResult r = Fight(Knight(Wear("thermo_blade", "warning_visor", "overload_chassis"),
                R("after_self_damage", SkillIds.Coolant), R("hp_low", SkillIds.Repair)));

            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            int tempo = r.Events.Where(e => e.Kind == BattleEventKind.ResourceChanged && e.Detail == ResourceIds.Tempo).Select(e => e.Amount).DefaultIfEmpty().Max();
            Assert.GreaterOrEqual(tempo, 10, "Tempo-Stapel wachsen ohne Grenze.");
            Assert.Greater(Starts(r, SkillIds.Coolant), 1);
        }

        [Test]
        public void BrokenTripleChain()
        {
            BattleResult r = Fight(Knight(Wear("short_blade", "round_shield", "incendiary_gloves"),
                R("enemy_charging", SkillIds.ShieldBash), R("chain", SkillIds.ArmorBreak), R("chain", SkillIds.Ignite)));

            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            List<BattleEvent> starts = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Side == Side.Player && e.Detail != SkillIds.BasicAttack).ToList();
            int bash = starts.FindIndex(e => e.Detail == SkillIds.ShieldBash);
            Assert.GreaterOrEqual(bash, 0);
            CollectionAssert.AreEqual(new[] { SkillIds.ShieldBash, SkillIds.ArmorBreak, SkillIds.Ignite },
                starts.Skip(bash).Take(3).Select(e => e.Detail), "Ein Auslöser, drei Skills.");
        }

        [Test]
        public void BrokenPhantomFlashbang()
        {
            // Seit A-12 (Basisangriff 60 %, Skills tragen den Schaden) gewinnt eine Tafel nur mit der Blendgranate den
            // Referenzkampf nicht mehr; geprüft wird weiter, was das Set bricht: die Granate läuft öfter als ihr Cooldown.
            BattleResult r = Fight(Knight(Wear("gyro_thrusters", "holo_projector", "shock_dagger"), R("always", SkillIds.Flashbang)));

            int seconds = r.EndTick / Ticks.PerSecond;
            Assert.Greater(Starts(r, SkillIds.Flashbang), seconds / 8 + 1, "Ausweichen senkt den Cooldown, die Granate läuft öfter als alle 8 s.");
        }

        [Test]
        public void BrokenAegisDischarge()
        {
            BattleResult r = Fight(Knight(Wear("short_blade", "holo_barrier", "shock_absorber", "mag_anchors"),
                R("charge_full", SkillIds.EmpBash, 0), R("enemy_stunned", SkillIds.ArmorBreak), R("enemy_charging", SkillIds.ShieldWall)));

            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.Damage && e.Detail == AegisSet.DischargeDetail), "Entladung");
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.ResourceChanged && e.Detail == ResourceIds.Charge && e.Amount == 0), "Ladung auf 0");

            // Gegen einen zähen Gegner zeigt sich die zweite Zeile: nach dem EMP-Schlag bricht der Ritter die Rüstung.
            r = Fight(Knight(Wear("short_blade", "holo_barrier", "shock_absorber", "mag_anchors"),
                R("charge_full", SkillIds.EmpBash, 0), R("enemy_stunned", SkillIds.ArmorBreak)), Fighter("Sack", 2000, 2, 5, armor: 10));
            int emp = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.EmpBash).Tick;
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak && e.Tick > emp && e.Tick < emp + Ticks.FromSeconds(4)));
        }

        [Test]
        public void ScrapHarvesterPaysPerKillAndIgnoresArmorOnMines()
        {
            var setup = new BattleSetup
            {
                Player = Knight(Wear("plasma_drill", "crawler_tracks", "resource_compactor"), R("always", SkillIds.Drill)),
                Enemies = new List<CombatantSetup> { Fighter("B", 30, 1, armor: 50), Fighter("C", 30, 1, armor: 50) },
                Seed = 4,
            };
            BattleResult off = CombatSimulation.Run(setup);
            setup.Context = new BattleContext { OnGoldMine = true };
            BattleResult mine = CombatSimulation.Run(setup);

            Assert.AreEqual(BattleOutcome.Victory, mine.Outcome);
            // Dazu +1 Gold je Gegner aus dem Tag «Schrott» (3 Teile, Stufe 2).
            Assert.AreEqual(2 * (ScrapHarvesterSet.GoldPerKill + 1), mine.BonusGold);
            Assert.Less(mine.EndTick, off.EndTick, "Auf der Mine räumt der Bohrer schneller ab.");
        }

        [Test]
        public void BrokenBuildsNeverHitTheSafetyNet()
        {
            foreach (int seed in Enumerable.Range(1, 30))
            {
                BattleResult r = Fight(Knight(Wear("thermo_blade", "warning_visor", "overload_chassis", "shock_absorber"),
                    R("after_self_damage", SkillIds.Coolant), R("always", SkillIds.ShieldWall)), seed: seed);
                Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome);
            }
        }
    }
}
