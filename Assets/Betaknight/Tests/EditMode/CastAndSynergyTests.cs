using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;
using static Betaknight.Tests.EditMode.ArenaSimulationTests;

namespace Betaknight.Tests.EditMode
{
    public class CastAndSynergyTests
    {
        private static readonly ConditionRegistry Conditions = ConditionRegistry.CreateDefault();
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();
        private static readonly SynergyRegistry Synergies = SynergyRegistry.CreateDefault();

        private static LogicRow Row(string runeId, SkillDefinition skill, int parameter = 0) =>
            new LogicRow(Conditions.Create(runeId, parameter), skill, runeId);

        private static BattleResult Run(BattleSetup setup, int seconds = 20)
        {
            setup.TimeLimitTicks = Ticks.FromSeconds(seconds);
            setup.MaxTicks = Ticks.FromSeconds(seconds + 5);
            return CombatSimulation.Run(setup);
        }

        /// <summary>Ein Teil pro Platz mit den angegebenen Tags.</summary>
        private static Equipment Wearing(params string[][] tagsPerItem)
        {
            var gear = new Equipment();
            EquipmentSlot[] slots = { EquipmentSlot.Helmet, EquipmentSlot.Gloves, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Boots, EquipmentSlot.Weapon, EquipmentSlot.Shield };
            for (int i = 0; i < tagsPerItem.Length; i++)
                gear.Equip(new EquipmentDefinition($"t{i}", $"Teil {i}", slots[i], tags: tagsPerItem[i]));
            return gear;
        }

        private static string[] T(params string[] tags) => tags;

        /// <summary>Jede Ausführung eines Kämpfers hat davor ihren Start, mindestens <paramref name="minCast"/> Ticks früher.</summary>
        private static void AssertEveryExecutionHasACast(BattleResult r, string name, int minCast)
        {
            int started = -1;
            int executions = 0;
            foreach (BattleEvent e in r.Events.Where(e => e.Source?.Name == name))
            {
                if (e.Kind == BattleEventKind.ActionStarted) started = e.Tick;
                if (e.Kind != BattleEventKind.ActionExecuted) continue;
                executions++;
                Assert.GreaterOrEqual(started, 0, $"Ausführung ohne Start bei Tick {e.Tick}");
                Assert.GreaterOrEqual(e.Tick - started, minCast, $"{e.Detail} bei Tick {e.Tick} ohne volle Cast-Zeit");
                started = -1;
            }
            Assert.Greater(executions, 0);
        }

        // ------------------------------------------------------------------ Cast-Zeit

        [Test]
        public void CastTimesAreStaggeredFastMediumHeavy()
        {
            Assert.AreEqual(Ticks.FromTenths(4), Skills.Get(SkillIds.ShockStab).CastTicks());
            Assert.AreEqual(Ticks.FromTenths(8), Skills.Get(SkillIds.ArmorBreak).CastTicks());
            Assert.AreEqual(Ticks.FromTenths(15), Skills.Get(SkillIds.Drill).CastTicks());
            Assert.IsTrue(Skills.All.Where(s => !s.IsBasicAttack).All(s => s.WindupTicks >= CastTime.Fast));
        }

        [Test]
        public void TheFloorHoldsForStackedReductions()
        {
            Assert.AreEqual(2, CastTime.DefaultMinTicks, "0,1 s");
            Assert.AreEqual(2, CastTime.Apply(8, -1000));
            Assert.AreEqual(4, CastTime.Apply(8, -1000, minTicks: 4), "Untergrenze ist einstellbar");

            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, 0, -50).WithBonus(0, 0, -60);
            Assert.AreEqual(-110, stab.CastBonusPercent, "Reduktionen addieren sich");
            Assert.AreEqual(2, stab.CastTicks());
            Assert.AreEqual(8, stab.WindupTicks, "Grundwert bleibt");

            SkillInfo info = SkillInfo.Create(stab, new SkillUserStats(10));
            Assert.AreEqual("Cast 0,1 s (Grund 0,4 s)", info.CastText);
            Assert.AreEqual(SkillPassive.Cast(SkillKind.Shock, -20).Text, "Schock-Skills −20 % Cast-Zeit");
            Assert.AreEqual(SkillPassive.Cast(SkillKinds.Every, -15).Text, "Alle Skills −15 % Cast-Zeit");
        }

        [Test]
        public void EveryExecutionNeedsItsCastEvenRepeats()
        {
            // Schockstich ohne Cast-Zeit und fast ohne Cooldown, Echo ohne Cooldown: alles läuft so schnell es geht.
            SkillDefinition stab = Skills.Get(SkillIds.ShockStab).WithBonus(0, -Ticks.FromSeconds(3) + 10, -1000);
            SkillDefinition echo = Skills.Get(SkillIds.Echo).WithBonus(0, -Ticks.FromSeconds(100), -1000);
            var board = new LogicBoard(new[] { Row("always", stab), Row("always", echo) });

            foreach (int min in new[] { CastTime.DefaultMinTicks, 5 })
            {
                BattleSetup setup = Duel(Fighter("A", 1000, 1, 20, board: board), Fighter("B", 1000000, 0, 1000));
                setup.MinCastTicks = min;
                BattleResult r = Run(setup, 10);

                AssertEveryExecutionHasACast(r, "A", min);
                Assert.Greater(r.Events.Count(e => e.Kind == BattleEventKind.ActionExecuted && e.IsRepeat), 0, "Echo wiederholt");
                Assert.IsTrue(r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Source.Name == "A").All(e => e.Amount >= min));
            }
        }

        [Test]
        public void AbsurdValuesStayStable()
        {
            SkillDefinition slow = Skills.Get(SkillIds.Drill).WithBonus(1000000, 100000000, 100000000);
            SkillDefinition fast = Skills.Get(SkillIds.ShockStab).WithBonus(-1000, -100000000, -100000000);
            SkillDefinition echo = Skills.Get(SkillIds.Echo).WithBonus(0, -100000000, -100000000);
            var board = new LogicBoard(new[] { Row("always", fast), Row("always", echo), Row("always", slow) });

            CombatantSetup a = Fighter("A", 1000, int.MaxValue / 4, 0, board: board);
            a.Stats[StatKind.AttackSpeed] = int.MaxValue / 2;
            a.Stats[StatKind.Dodge] = BasisPoints.Full * 10;
            CombatantSetup b = Fighter("B", int.MaxValue / 2, 1, 1, armor: int.MaxValue / 2);
            BattleResult r = null;
            Assert.DoesNotThrow(() => r = Run(Duel(a, b), 30));
            AssertEveryExecutionHasACast(r, "A", CastTime.DefaultMinTicks);
            AssertEveryExecutionHasACast(r, "B", CastTime.DefaultMinTicks);
            Assert.AreEqual(int.MaxValue / 2, CastTime.Apply(int.MaxValue, int.MaxValue), "Riesige Cast-Zeiten laufen nicht über");
        }

        [Test]
        public void CastPassivesSpeedUpTheFight()
        {
            Equipment gear = Wearing(T(SynergyTagIds.Tempo), T(SynergyTagIds.Tempo), T(SynergyTagIds.Tempo), T(SynergyTagIds.Tempo));
            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 5, 20), gear,
                new[] { new BoardRowSpec("always", SkillIds.ShockStab) });
            BattleResult r = Run(Duel(knight, Fighter("B", 100000, 0, 1000)), 5);

            BattleEvent start = r.Events.First(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ShockStab);
            Assert.AreEqual(CastTime.Apply(CastTime.Fast, -15), start.Amount, "Takt 4: alle Skills −15 % Cast-Zeit");
        }

        // ------------------------------------------------------------------ Tags und Schwellen

        [Test]
        public void TagsCountOnlyWornItems()
        {
            Equipment gear = Wearing(T(SynergyTagIds.Charge), T(SynergyTagIds.Charge, SynergyTagIds.Heat), T(SynergyTagIds.Charge));
            Assert.AreEqual(3, gear.TagCount(SynergyTagIds.Charge));
            Assert.AreEqual(1, gear.TagCount(SynergyTagIds.Heat));
            Assert.AreEqual("Ladung 3/4", Synergies.Counters(gear).Single(c => c.Tag.Id == SynergyTagIds.Charge).Text);

            gear.Unequip(EquipmentSlot.Gloves);
            Assert.AreEqual(2, gear.TagCount(SynergyTagIds.Charge), "Abgelegte Teile zählen nicht");
            Assert.AreEqual(0, gear.TagCount(SynergyTagIds.Heat));

            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: KnightKit.Defaults.Single(k => k.Id == "shield"));
            int before = s.Gear.TagCount(SynergyTagIds.Charge);
            s.Inventory.TryAdd(s.Items.Get("holo_barrier"));
            Assert.AreEqual(before, s.Gear.TagCount(SynergyTagIds.Charge), "Teile im Inventar zählen nicht");
        }

        [Test]
        public void ThresholdsUnlockTiersTogether()
        {
            Assert.AreEqual(0, SynergyRegistry.ReachedThreshold(1));
            Assert.AreEqual(2, SynergyRegistry.ReachedThreshold(3));
            Assert.AreEqual(6, SynergyRegistry.ReachedThreshold(7));
            Assert.AreEqual(4, SynergyRegistry.NextThreshold(2));
            Assert.AreEqual(0, SynergyRegistry.NextThreshold(6));

            Equipment one = Wearing(T(SynergyTagIds.Charge));
            Assert.IsEmpty(Synergies.ActiveTiers(one));

            Equipment four = Wearing(T(SynergyTagIds.Charge), T(SynergyTagIds.Charge), T(SynergyTagIds.Charge), T(SynergyTagIds.Charge));
            CollectionAssert.AreEqual(new[] { 2, 4 }, Synergies.ActiveTiers(four).Select(t => t.pieces));
            Assert.IsTrue(Synergies.Passives(four).Any(p => p.Effect == SkillPassiveEffect.CastPercent && p.Target == SkillKind.Shock));

            // Ladung 2: +10 % Block im Kampf.
            CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 5, 20), four, null);
            var battle = new Battle(Duel(knight, Fighter("B", 10, 0)));
            Assert.AreEqual(BasisPoints.Percent(10), battle.Player.GetStat(StatKind.Block));

            Equipment six = Wearing(T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap),
                T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap));
            Assert.AreEqual(3, Synergies.CreateModifiers(six).Count, "Schrott 2, 4 und 6 gelten zusammen");
        }

        [Test]
        public void ScrapSixIgnoresArmorInTheFight()
        {
            Equipment six = Wearing(T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap),
                T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap));
            Equipment five = Wearing(T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap), T(SynergyTagIds.Scrap),
                T(SynergyTagIds.Scrap));
            int FirstHit(Equipment gear)
            {
                CombatantSetup knight = PlayerLoadout.CreateCombatant("Ritter", new CombatStats(100, 10, 20), gear, null);
                BattleResult r = Run(Duel(knight, Fighter("B", 100000, 0, 1000, armor: 100)), 3);
                return r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Source?.Name == "Ritter").Amount;
            }
            // A-12: Der Basisangriff des Ritters macht 60 % Waffenschaden.
            Assert.AreEqual(6, FirstHit(six));
            Assert.Less(FirstHit(five), 6);
        }

        [Test]
        public void DuoNeedsBothTagsAtFour()
        {
            SynergyDuo duo = Synergies.Duos.Single(d => d.Uses(SynergyTagIds.Heat) && d.Uses(SynergyTagIds.Tempo));
            Equipment three = Wearing(T(SynergyTagIds.Heat, SynergyTagIds.Tempo), T(SynergyTagIds.Heat, SynergyTagIds.Tempo),
                T(SynergyTagIds.Heat, SynergyTagIds.Tempo), T(SynergyTagIds.Heat), T(SynergyTagIds.Heat), T(SynergyTagIds.Heat));
            Assert.AreEqual(6, three.TagCount(SynergyTagIds.Heat));
            Assert.IsFalse(Synergies.IsDuoActive(duo, three), "Takt nur 3");

            Equipment both = Wearing(T(SynergyTagIds.Heat, SynergyTagIds.Tempo), T(SynergyTagIds.Heat, SynergyTagIds.Tempo),
                T(SynergyTagIds.Heat, SynergyTagIds.Tempo), T(SynergyTagIds.Heat, SynergyTagIds.Tempo));
            Assert.IsTrue(Synergies.IsDuoActive(duo, both));
            CollectionAssert.AreEqual(new[] { duo }, Synergies.ActiveDuos(both));
            Assert.AreEqual(6, Synergies.Duos.Count);
            Assert.AreEqual(Synergies.Duos.Count, Synergies.Duos.Select(d => d.Id).Distinct().Count());
        }

        [Test]
        public void PreviewShowsThresholdsAndHiddenDuos()
        {
            Equipment gear = Wearing(T(SynergyTagIds.Charge), T(SynergyTagIds.Charge), T(SynergyTagIds.Charge));
            var item = new EquipmentDefinition("x", "X", EquipmentSlot.Boots, tags: T(SynergyTagIds.Charge));
            CollectionAssert.AreEqual(new[] { "→ Ladung 4/6: Schwelle!" }, Synergies.Preview(gear, item));

            var sameSlot = new EquipmentDefinition("y", "Y", EquipmentSlot.Helmet, tags: T(SynergyTagIds.Charge));
            CollectionAssert.AreEqual(new[] { "→ Ladung 3/4" }, Synergies.Preview(gear, sameSlot), "Verdrängtes Teil zählt nicht mehr");

            Equipment almost = Wearing(T(SynergyTagIds.Heat, SynergyTagIds.Tempo), T(SynergyTagIds.Heat, SynergyTagIds.Tempo),
                T(SynergyTagIds.Heat, SynergyTagIds.Tempo));
            var fourth = new EquipmentDefinition("z", "Z", EquipmentSlot.Legs, tags: T(SynergyTagIds.Heat, SynergyTagIds.Tempo));
            List<string> preview = Synergies.Preview(almost, fourth, d => "???");
            CollectionAssert.Contains(preview, "→ Duo frei: ???");
        }

        [Test]
        public void DuosAreSilhouettesUntilTriggered()
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: KnightKit.Defaults.Single(k => k.Id == "blade"));
            SynergyDuo duo = s.Synergies.Duos.Single(d => d.Id == "ember_rhythm");
            Assert.AreEqual("???", s.DuoName(duo));

            EquipmentSlot[] slots = { EquipmentSlot.Helmet, EquipmentSlot.Gloves, EquipmentSlot.Chest, EquipmentSlot.Legs };
            for (int i = 0; i < slots.Length; i++)
                s.Gear.Equip(new EquipmentDefinition($"d{i}", "D", slots[i], tags: T(SynergyTagIds.Heat, SynergyTagIds.Tempo)));
            CollectionAssert.Contains(s.ActiveDuos(), duo);
            Assert.AreEqual("???", s.DuoName(duo), "Aktiv, aber noch nicht ausgelöst");

            var messages = new List<string>();
            s.BuildImproved += messages.Add;
            typeof(OverworldSession).GetMethod("DiscoverDuos", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(s, null);
            Assert.AreEqual("Glutrhythmus", s.DuoName(duo));
            Assert.IsTrue(messages.Any(m => m.Contains("Duo entdeckt: Glutrhythmus")));

            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Radius = 4, Seed = 3 }, s);
            Assert.IsTrue(next.IsDuoDiscovered(duo.Id), "Das Rezeptbuch wandert durch die Akte mit");
        }

        [Test]
        public void CatalogItemsCarryOneOrTwoKnownTags()
        {
            foreach (EquipmentDefinition item in EquipmentCatalog.CreateDefault().All)
            {
                Assert.That(item.Tags.Count, Is.InRange(1, 2), item.Id);
                foreach (string tag in item.Tags) Assert.IsTrue(Synergies.TryGetTag(tag, out _), $"{item.Id}: {tag}");
            }
            foreach (SynergyTag tag in Synergies.Tags)
                CollectionAssert.AreEquivalent(SynergyRegistry.Thresholds, tag.Tiers.Keys, tag.Id);
            Assert.AreEqual(6, Synergies.Tags.Count);
        }

        // ------------------------------------------------------------------ Anzeige der Tag-Wirkungen

        [Test]
        public void EveryTagDescribesAllItsTiers()
        {
            SynergyRegistry registry = SynergyRegistry.CreateDefault();
            Assert.IsTrue(registry.TryGetTag(SynergyTagIds.Toxin, out SynergyTag toxin));
            string text = toxin.Describe(2);
            StringAssert.StartsWith("Toxin 2 Teile", text);
            StringAssert.Contains("● 2 Teile: Eigene Skill-Treffer vergiften", text);
            StringAssert.Contains("○ 4 Teile: Eigene Angriffe +1 Schaden je Gift-Stapel", text);
            StringAssert.Contains("○ 6 Teile: Auch Basisangriffe vergiften.", text);
            Assert.AreEqual(string.Empty, toxin.ActiveText(1));
            foreach (SynergyTag tag in registry.Tags)
            {
                Assert.AreEqual(3, tag.Tiers.Count, tag.Name);
                Assert.IsTrue(tag.Tiers.Values.All(t => t.Text.Length > 0), tag.Name);
            }
        }

        [Test]
        public void TheSessionPreviewsTagCountsAndActiveEffects()
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 });
            EquipmentCatalog items = EquipmentCatalog.CreateDefault();
            EquipmentDefinition toxic = items.All.First(i => i.Tags.Contains(SynergyTagIds.Toxin) && i.Slot != EquipmentSlot.Weapon);
            EquipmentDefinition second = items.All.First(i => i.Tags.Contains(SynergyTagIds.Toxin) && i.Slot != toxic.Slot && i.Slot != EquipmentSlot.Weapon);

            Assert.AreEqual(1, s.TagCountWith(toxic, SynergyTagIds.Toxin));
            Assert.AreEqual(string.Empty, s.ActiveSynergyText());
            s.Gear.Equip(toxic);
            Assert.AreEqual(1, s.TagCountWith(toxic, SynergyTagIds.Toxin), "schon getragen");
            Assert.AreEqual(2, s.TagCountWith(second, SynergyTagIds.Toxin));
            s.Gear.Equip(second);
            StringAssert.Contains("Toxin 2\n2 Teile: Eigene Skill-Treffer vergiften", s.ActiveSynergyText());
        }
    }
}
