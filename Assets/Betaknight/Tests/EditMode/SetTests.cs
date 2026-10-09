using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
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

        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        /// <summary>Ein Relais mit der Komponente, die es versorgt (früher eine Zeile der Tafel).</summary>
        private readonly struct Line
        {
            public readonly string Rune, Skill;
            public readonly int Level;

            public Line(string rune, string skill, int level)
            {
                Rune = rune;
                Skill = skill;
                Level = level;
            }
        }

        private static Line R(string rune, string skill, int level = 0) => new Line(rune, skill, level);

        /// <summary>
        /// Platine aus Paaren: links das Relais, rechts daneben die Komponente, Paar für Paar untereinander. So versorgt jedes
        /// Relais nur seine Komponente, und die Lesereihenfolge bleibt die Reihenfolge der Paare. Seit A-20 liegt eine leere Zeile
        /// zwischen den Paaren, damit sich keine Pins berühren (sonst schicken sich die Komponenten Pulse).
        /// </summary>
        private static CircuitSpec Circuit(params Line[] lines)
        {
            var spec = new CircuitSpec { Width = 3 };
            int y = 0;
            foreach (Line l in lines)
            {
                spec.Relays.Add(new RelaySpec(l.Rune, new Cell(0, y), l.Level));
                spec.Components.Add(new ComponentSpec(l.Skill, new Cell(1, y)));
                y += (Skills.TryGet(l.Skill, out SkillDefinition s) ? s.Shape.Height : 1) + 1;
            }
            spec.Height = System.Math.Max(1, y - 1);
            return spec;
        }

        private static CombatantSetup Knight(Equipment gear, params Line[] lines) => Knight(gear, Circuit(lines));

        private static CombatantSetup Knight(Equipment gear, CircuitSpec circuit) =>
            PlayerLoadout.CreateCombatant("Ritter", new CombatStats(36, 4, 20, 0), gear, circuit);

        /// <summary>Referenzgegner mit sichtbarer Aufladung, etwa Stufe eines frühen Elite-Kampfs. Takt-Relais statt «Immer» mit Cooldown.</summary>
        internal static CombatantSetup Golem()
        {
            var slam = new SkillDefinition("slam", "Hammerschlag", 30, 6,
                new ISkillEffect[] { new DamageEffect(BasisPoints.Percent(300)) }, countsAsAttack: true);
            LogicBoard board = EnemyBoard.Build(new EnemyPart(new ClockCondition(Ticks.FromSeconds(6)), "Every 6 s", slam));
            return new CombatantSetup { Name = "Golem", Stats = new CombatStats(80, 2, 20, 2), Board = board };
        }

        private static BattleResult Fight(CombatantSetup player, CombatantSetup enemy = null, BattleContext context = null, int seed = 7)
        {
            BattleSetup setup = Duel(player, enemy ?? Golem(), seed);
            // Thermal Throttling (A-21) erst spät: die Set-Tests vergleichen Cast-Zeiten und Schaden ohne Aufheizen.
            setup.TimeLimitTicks = Ticks.FromSeconds(90);
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
            // Ein Relais «Every 5 Seconds» (●●●, 6 Zellen Ladung) reicht für den Schildschlag (gedreht, 2×1) und den Rüstungsbrecher (2×2)
            // zusammen, beide laufen in Lesereihenfolge; «Chain» (●, bis 2 Zellen) hängt Ignite an. Ein 2×2 an «Chain» wäre zu gross.
            var circuit = new CircuitSpec { Width = 4, Height = 4 };
            circuit.Relays.Add(new RelaySpec("every_20s", new Cell(0, 0)));
            circuit.Relays.Add(new RelaySpec("chain", new Cell(3, 3)));
            circuit.Components.Add(new ComponentSpec(SkillIds.ShieldBash, new Cell(1, 0), rotated: true));
            circuit.Components.Add(new ComponentSpec(SkillIds.ArmorBreak, new Cell(0, 1)));
            circuit.Components.Add(new ComponentSpec(SkillIds.Ignite, new Cell(2, 2)));
            BattleResult r = Fight(Knight(Wear("short_blade", "round_shield", "incendiary_gloves"), circuit));

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
            // Seit A-12 (Basisangriff 60 %, Skills tragen den Schaden) gewinnt eine Platine nur mit der Blendgranate den
            // Referenzkampf nicht mehr; geprüft wird weiter, was das Set bricht. Seit A-19 (keine Cooldowns) gibt jedes
            // Ausweichen Haste: die Granate an «After Dodge» castet schneller, solange der Ritter ausweicht.
            List<int> Casts(bool full)
            {
                CombatantSetup knight = Knight(Wear("gyro_thrusters", "holo_projector", "shock_dagger"), R("after_dodge", SkillIds.Flashbang));
                // Vergleich: dieselben Teile, aber nur der 2-Teile-Bonus (Ausweichen ohne Haste).
                if (!full) knight.Modifiers = knight.Modifiers.Select(m => m is PhantomSet ? new PhantomSet(false) : m).ToList();
                BattleResult r = Fight(knight);
                Assert.AreEqual(full, r.Events.Any(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Haste && e.Target.Side == Side.Player),
                    "nur das volle Set gibt Haste beim Ausweichen");
                return r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.Flashbang && e.Source.Side == Side.Player)
                    .Select(e => e.Amount).ToList();
            }

            List<int> hasted = Casts(true), normal = Casts(false);
            Assert.Greater(hasted.Count, 1);
            Assert.Greater(normal.Count, 0);
            Assert.Less(hasted.Max(), normal.Min(), "mit Haste castet die Granate schneller");
        }

        [Test]
        public void BrokenAegisDischarge()
        {
            BattleResult r = Fight(Knight(Wear("short_blade", "holo_barrier", "shock_absorber", "mag_anchors"),
                R("charge_full", SkillIds.EmpBash, 0), R("enemy_stunned", SkillIds.ArmorBreak), R("enemy_charging", SkillIds.ShieldWall)));

            Assert.AreEqual(BattleOutcome.Victory, r.Outcome);
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.Damage && e.Detail == AegisSet.DischargeDetail), "Entladung");
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.ResourceChanged && e.Detail == ResourceIds.Charge && e.Amount == 0), "Ladung auf 0");

            // Gegen einen zähen Gegner zeigt sich die zweite Komponente: nach dem EMP-Schlag bricht der Ritter die Rüstung.
            r = Fight(Knight(Wear("short_blade", "holo_barrier", "shock_absorber", "mag_anchors"),
                R("charge_full", SkillIds.EmpBash, 0), R("enemy_stunned", SkillIds.ArmorBreak)), Fighter("Sack", 2000, 2, 5, armor: 10));
            int emp = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.EmpBash).Tick;
            Assert.IsTrue(r.Events.Any(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ArmorBreak && e.Tick > emp && e.Tick < emp + Ticks.FromSeconds(4)));
        }

        [Test]
        public void ScrapHarvesterPaysPerKillAndIgnoresArmorOnMines()
        {
            // Der Bohrer (2×2) braucht ein Relais ab ●●; «Every 5 Hits Taken» ersetzt das frühere «Immer».
            var setup = new BattleSetup
            {
                Player = Knight(Wear("plasma_drill", "crawler_tracks", "resource_compactor"), R("every_nth_hit_taken", SkillIds.Drill)),
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
                // «Every 5 Seconds» (●) ersetzt das frühere «Immer» mit Cooldown.
                BattleResult r = Fight(Knight(Wear("thermo_blade", "warning_visor", "overload_chassis", "shock_absorber"),
                    R("after_self_damage", SkillIds.Coolant), R("every_5s", SkillIds.ShieldWall)), seed: seed);
                Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome);
            }
        }

        // ------------------------------------------------------------------ Anzeige der Set-Boni

        [Test]
        public void SetBonusesAreDescribedWithTheirPieces()
        {
            SetBonusRegistry sets = SetBonusRegistry.CreateDefault();
            Assert.IsTrue(sets.TryGet(SetIds.Aegis, out SetDefinition aegis));
            string text = aegis.Describe(2);
            StringAssert.StartsWith("Aegis Firewall 2/3", text);
            StringAssert.Contains("● 2 pieces: Every Block: +1 Static", text);
            StringAssert.Contains("○ 3 pieces: Components powered by \"Static Full\" discharge", text);
            Assert.AreEqual(string.Empty, aegis.ActiveText(1));
            StringAssert.StartsWith("2 pieces: ", aegis.ActiveText(2));
            foreach (SetDefinition set in sets.All)
                Assert.AreEqual(2, set.Bonuses.Count, $"{set.Name}: Boni für 2 und 3 Teile");
        }

        [Test]
        public void TheSessionPreviewsSetPiecesAndActiveBonuses()
        {
            Betaknight.Core.OverworldSession s = Betaknight.Core.OverworldSession.Create(new Betaknight.Core.Map.MapGenerationConfig { Radius = 4, Seed = 3 });
            Assert.AreEqual(string.Empty, s.ActiveSetBonusText());
            EquipmentDefinition barrier = Items.Get("holo_barrier"), absorber = Items.Get("shock_absorber");
            Assert.AreEqual(1, s.SetPiecesWith(barrier), "mit diesem Teil 1/3");

            s.Gear.Equip(barrier);
            Assert.AreEqual(1, s.SetPiecesWith(barrier), "schon getragen");
            Assert.AreEqual(2, s.SetPiecesWith(absorber));
            Assert.AreEqual(string.Empty, s.ActiveSetBonusText(), "ein Teil: noch kein Bonus");

            s.Gear.Equip(absorber);
            StringAssert.Contains("Aegis Firewall 2/3", s.ActiveSetBonusText());
            StringAssert.Contains("2 pieces: Every Block", s.ActiveSetBonusText());
        }
    }
}
