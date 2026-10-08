using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Skill-Kennzahlen für den Tafel-Editor: aus den Effekten abgeleitet und gegen das geprüft,
    /// was der Simulator wirklich austeilt.
    /// </summary>
    public class SkillInfoTests
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly EquipmentCatalog Items = EquipmentCatalog.CreateDefault();
        private static readonly RuneCatalog Runes = RuneCatalog.CreateDefault();

        private static Equipment Gear(params string[] ids)
        {
            var gear = new Equipment();
            foreach (string id in ids) Assert.IsNotNull(gear.Equip(Items.Get(id)), id);
            return gear;
        }

        /// <summary>Tafel mit einer Zeile [Immer] → Skill; ohne Skill nur der Basisangriff.</summary>
        private static RuneLoadout Board(string skillId)
        {
            var loadout = new RuneLoadout(4);
            if (skillId != null) loadout.TryAdd(Runes.Get("always"), skillId);
            return loadout;
        }

        private static SkillInfo Info(string skillId, Equipment gear, BattleContext context = null)
        {
            SkillUserStats stats = new ArenaCombatResolver().PreviewStats(new PlayerStats(30, 0), Board(skillId), gear, context);
            // Wie im Kampf: passive Effekte der Ausrüstung auf passende Tags (A-05) sind eingerechnet.
            return SkillInfo.Create(gear.Boost(Skills.Get(skillId ?? SkillIds.BasicAttack)), stats);
        }

        /// <summary>Gegner, die nur einstecken: viel Leben, keine Rüstung, kein Schaden, kein Ausweichen.</summary>
        private static BattleResult Simulate(string skillId, Equipment gear, int enemies = 1, int seconds = 12, BattleContext context = null)
        {
            var resolver = new ArenaCombatResolver();
            var request = new CombatRequest(Betaknight.Core.Map.CellContent.Enemy, 0, new PlayerStats(30, 0), Board(skillId), gear, context);
            var dummies = new List<CombatantSetup>();
            for (int i = 0; i < enemies; i++)
                dummies.Add(new CombatantSetup { Name = $"Sandsack {i + 1}", Stats = new CombatStats(99999, 0) });
            BattleSetup setup = resolver.CreateSetup(request, dummies, 5);
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds);
            return CombatSimulation.Run(setup);
        }

        private static List<BattleEvent> PlayerDamage(BattleResult r, string detail) =>
            r.Events.Where(e => e.Kind == BattleEventKind.Damage && e.Detail == detail && e.Source != null && e.Source.Side == Side.Player).ToList();

        private static int WindupInBattle(BattleResult r, string skillId)
        {
            BattleEvent start = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == skillId && e.Source.Side == Side.Player);
            BattleEvent done = r.Events.First(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == skillId && e.Source.Side == Side.Player);
            return done.Tick - start.Tick;
        }

        // ------------------------------------------------------------------ Kennzahlen

        [Test]
        public void ArmorBreakShowsWeaponDamageDebuffAndTimes()
        {
            SkillInfo info = Info(SkillIds.ArmorBreak, Gear("short_blade"));

            Assert.AreEqual(7, info.Stats.WeaponDamage);
            EffectInfo hit = info.Effects.Single(e => e.Kind == EffectInfoKind.Damage);
            Assert.AreEqual(BasisPoints.Percent(190), hit.DamageBp);
            Assert.AreEqual(13, hit.Amount);
            Assert.AreEqual("190 % Waffenschaden ≈ 13", info.DamageText);
            Assert.AreEqual(Ticks.FromSeconds(6), info.Effects.Single(e => e.Kind == EffectInfoKind.StatChange).DurationTicks);
            StringAssert.Contains("Gegner Rüstung −50 % für 6 s", info.OtherEffectsText);
            Assert.AreEqual("CD 8 s · Cast 0,8 s · Erholung 0,2 s", info.TimingText);
        }

        [Test]
        public void IgniteShowsBurnPerSecondAndTotal()
        {
            SkillInfo info = Info(SkillIds.Ignite, Gear("spark_staff"));

            EffectInfo burn = info.Effects.Single();
            Assert.AreEqual(EffectInfoKind.DamageOverTime, burn.Kind);
            Assert.AreEqual(4, burn.Amount, "70 % von 6 Waffenschaden pro Sekunde");
            Assert.AreEqual(20, burn.Total, "5 s Brennen");
            Assert.AreEqual(Ticks.FromSeconds(5), burn.DurationTicks);
            Assert.AreEqual("Brennen 70 % Waffenschaden/s ≈ 4/s, 20 über 5 s", info.DamageText);
            Assert.IsTrue(info.DealsDamage);
        }

        [Test]
        public void DrillHitsAllEnemiesForHundredEightyPercent()
        {
            SkillInfo plain = Info(SkillIds.Drill, Gear("short_sword"));
            Assert.AreEqual("180 % Waffenschaden ≈ 10 an allen Gegnern", plain.DamageText);

            // Der Plasma-Bohrer gibt Klinge-Skills +25 % Wirkung (A-05: passive Effekte statt Skills).
            SkillInfo info = Info(SkillIds.Drill, Gear("plasma_drill"));
            Assert.AreEqual(9, info.Stats.WeaponDamage);
            Assert.AreEqual("225 % Waffenschaden ≈ 20 an allen Gegnern", info.DamageText);
            Assert.IsTrue(info.Effects.Single().AllEnemies);
            Assert.AreEqual("CD 5 s · Cast 1,5 s · Erholung 0,3 s", info.TimingText);
        }

        [Test]
        public void ActiveSetBonusesChangeTheConcreteValue()
        {
            // Schrott-Ernter mit 3 Teilen: +50 % Flächenschaden auf Goldminen.
            Equipment gear = Gear("plasma_drill", "crawler_tracks", "resource_compactor");
            SkillInfo onMine = Info(SkillIds.Drill, gear, new BattleContext { OnGoldMine = true });
            SkillInfo elsewhere = Info(SkillIds.Drill, gear);

            // 225 % durch den Plasma-Bohrer (Klinge +25 %), auf der Mine noch einmal +50 % Flächenschaden.
            Assert.AreEqual(20, elsewhere.Effects.Single().Amount);
            Assert.AreEqual(30, onMine.Effects.Single().Amount);
            StringAssert.StartsWith("225 % Waffenschaden ≈ 30", onMine.DamageText);
        }

        [Test]
        public void ShieldBashShowsDamageStunAndInterrupt()
        {
            SkillInfo info = Info(SkillIds.ShieldBash, Gear("short_sword", "round_shield"));

            Assert.AreEqual("70 % Waffenschaden ≈ 4", info.DamageText);
            Assert.AreEqual(Ticks.FromSeconds(2), info.Effects.Single(e => e.Kind == EffectInfoKind.Stun).DurationTicks);
            Assert.AreEqual("bricht Aufladung ab, betäubt 2 s", info.OtherEffectsText);
        }

        [Test]
        public void BasicAttackUsesWeaponDamageAndAttackTempo()
        {
            SkillInfo info = Info(null, Gear("short_blade"));

            Assert.AreEqual("60 % Waffenschaden ≈ 4", info.DamageText, "A-12: Basisangriff des Ritters 60 %");
            Assert.AreEqual(18, info.WindupTicks + info.RecoveryTicks, "Intervall 20 − 2 der Kurzklinge");
            Assert.AreEqual("Takt 0,9 s · Cast 0,6 s · Erholung 0,3 s", info.TimingText);
        }

        [Test]
        public void SkillsWithoutDamageSayKeinSchaden()
        {
            SkillInfo flash = Info(SkillIds.Flashbang, Gear("short_sword", "holo_projector"));
            SkillInfo repair = Info(SkillIds.Repair, Gear("short_sword"));

            Assert.IsFalse(flash.DealsDamage);
            Assert.AreEqual("kein Schaden", flash.DamageText);
            StringAssert.Contains("Gegner Präzision −40 % für 4 s", flash.OtherEffectsText);
            Assert.AreEqual("kein Schaden", repair.DamageText);
            Assert.AreEqual("heilt 25 % Max-HP ≈ 7", repair.OtherEffectsText);
            StringAssert.Contains("kein Schaden", repair.Details);
        }

        [Test]
        public void ChanceEffectsShowTheirPercent()
        {
            SkillInfo info = Info(SkillIds.ShockStab, Gear("shock_dagger"));

            EffectInfo stun = info.Effects.Single(e => e.Kind == EffectInfoKind.Stun);
            Assert.AreEqual(BasisPoints.Percent(20), stun.ChanceBp);
            Assert.AreEqual("20 % Chance: betäubt 1 s", stun.Text);
        }

        [Test]
        public void EverySkillInTheCatalogCanBeDescribed()
        {
            var stats = new SkillUserStats(6);
            foreach (SkillDefinition skill in Skills.All)
            {
                SkillInfo info = SkillInfo.Create(skill, stats);
                Assert.IsNotEmpty(info.Effects, skill.Id);
                Assert.IsFalse(string.IsNullOrEmpty(info.Summary), skill.Id);
            }
        }

        [Test]
        public void SecondsAndPercentAreFormattedGerman()
        {
            Assert.AreEqual("0,5 s", SkillInfo.Seconds(10));
            Assert.AreEqual("1,5 s", SkillInfo.Seconds(30));
            Assert.AreEqual("0,35 s", SkillInfo.Seconds(7));
            Assert.AreEqual("12 s", SkillInfo.Seconds(240));
            Assert.AreEqual("120 %", SkillInfo.Percent(12000));
            Assert.AreEqual("−40 %", SkillInfo.Percent(-4000));
            Assert.AreEqual("12,5 %", SkillInfo.Percent(1250));
        }

        // ------------------------------------------------------------------ Abgleich mit dem Simulator

        [Test]
        public void ArmorBreakDealsWhatTheInfoSays()
        {
            Equipment gear = Gear("short_blade");
            SkillInfo info = Info(SkillIds.ArmorBreak, gear);
            BattleResult r = Simulate(SkillIds.ArmorBreak, gear);

            Assert.AreEqual(info.Effects.Single(e => e.IsDamage).Amount, PlayerDamage(r, SkillIds.ArmorBreak).First().Amount);
            Assert.AreEqual(info.WindupTicks, WindupInBattle(r, SkillIds.ArmorBreak));
        }

        [Test]
        public void IgniteBurnsForTheShownTotal()
        {
            Equipment gear = Gear("spark_staff");
            SkillInfo info = Info(SkillIds.Ignite, gear);
            BattleResult r = Simulate(SkillIds.Ignite, gear, seconds: 6);

            List<BattleEvent> burns = PlayerDamage(r, StatusIds.Burn);
            Assert.IsTrue(burns.All(b => b.Amount == info.Effects.Single().Amount));
            Assert.AreEqual(info.Effects.Single().Total, burns.Sum(b => b.Amount));
            Assert.AreEqual(info.WindupTicks, WindupInBattle(r, SkillIds.Ignite));
        }

        [Test]
        public void DrillHitsEveryEnemyForTheShownAmount()
        {
            Equipment gear = Gear("plasma_drill");
            SkillInfo info = Info(SkillIds.Drill, gear);
            BattleResult r = Simulate(SkillIds.Drill, gear, enemies: 3);

            List<BattleEvent> first = PlayerDamage(r, SkillIds.Drill).Take(3).ToList();
            Assert.AreEqual(3, first.Select(e => e.Target).Distinct().Count());
            Assert.IsTrue(first.All(e => e.Amount == info.Effects.Single().Amount));
        }

        [Test]
        public void SetBonusOnMineMatchesTheSimulator()
        {
            Equipment gear = Gear("plasma_drill", "crawler_tracks", "resource_compactor");
            var mine = new BattleContext { OnGoldMine = true };
            SkillInfo info = Info(SkillIds.Drill, gear, mine);
            BattleResult r = Simulate(SkillIds.Drill, gear, enemies: 2, context: mine);

            Assert.AreEqual(info.Effects.Single().Amount, PlayerDamage(r, SkillIds.Drill).First().Amount);
        }

        [Test]
        public void ShieldBashDealsWhatTheInfoSays()
        {
            Equipment gear = Gear("short_sword", "round_shield");
            SkillInfo info = Info(SkillIds.ShieldBash, gear);
            BattleResult r = Simulate(SkillIds.ShieldBash, gear);

            Assert.AreEqual(info.Effects.Single(e => e.IsDamage).Amount, PlayerDamage(r, SkillIds.ShieldBash).First().Amount);
            BattleEvent stun = r.Events.First(e => e.Kind == BattleEventKind.StatusApplied && e.Detail == StatusIds.Stun);
            Assert.AreEqual(info.Effects.Single(e => e.Kind == EffectInfoKind.Stun).DurationTicks, stun.Amount);
        }

        [Test]
        public void BasicAttackDealsWhatTheInfoSaysInTheShownRhythm()
        {
            Equipment gear = Gear("short_blade");
            SkillInfo info = Info(null, gear);
            BattleResult r = Simulate(null, gear);

            List<BattleEvent> hits = PlayerDamage(r, SkillIds.BasicAttack);
            Assert.IsTrue(hits.All(h => h.Amount == info.Effects.Single().Amount));
            Assert.AreEqual(info.WindupTicks, WindupInBattle(r, SkillIds.BasicAttack));
            Assert.AreEqual(info.WindupTicks + info.RecoveryTicks, hits[1].Tick - hits[0].Tick);
        }

        [Test]
        public void FlashbangDealsNoDamage()
        {
            Equipment gear = Gear("short_sword", "holo_projector");
            BattleResult r = Simulate(SkillIds.Flashbang, gear);

            Assert.IsEmpty(PlayerDamage(r, SkillIds.Flashbang));
            Assert.IsNotEmpty(r.Events.Where(e => e.Kind == BattleEventKind.ActionExecuted && e.Detail == SkillIds.Flashbang));
        }
    }
}
