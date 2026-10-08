using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>A-05: Skills als eigene Exemplare, getrennt von Runen (Wann) und Ausrüstung (Werte und Passive).</summary>
    public class SkillCollectionTests
    {
        private static readonly EquipmentCatalog Items = EquipmentCatalog.CreateDefault();
        private static readonly SkillCatalog Catalog = SkillCatalog.CreateDefault();

        private static KnightKit Kit(string id) => KnightKit.Defaults.Single(k => k.Id == id);

        /// <summary>
        /// Session mit Start-Kit. Beim Schildritter sitzt hier Schildschlag an der Start-Rune (seit A-12 startet dort Bohrstoß),
        /// damit die Tests weiter einen Skill mit Betäubung an Zeile 1 haben; Bohrstoß und Schildwall liegen frei.
        /// </summary>
        private static OverworldSession Session(string kit = "shield", int seed = 3)
        {
            OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = seed }, kit: Kit(kit));
            if (kit == "shield") s.PlaceSkill(s.Skills.All.Single(i => i.SkillId == SkillIds.ShieldBash).InstanceId, 0);
            return s;
        }

        /// <summary>Session mit drei Zeilen: Start-Rune plus zwei weitere.</summary>
        private static OverworldSession ThreeRows()
        {
            OverworldSession s = Session();
            s.Runes.TryAdd(s.RuneCatalog.Get("hp_low"), SkillInstance.BasicAttack());
            s.Runes.TryAdd(s.RuneCatalog.Get("enemy_charging"), SkillInstance.BasicAttack());
            return s;
        }

        // ------------------------------------------------------------------ Ein Exemplar, ein Ort

        [Test]
        public void ASkillSitsInExactlyOnePlace()
        {
            OverworldSession s = ThreeRows();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            Assert.AreSame(s.Runes.Rows[0], bash.Holder);

            Assert.IsTrue(s.PlaceSkill(bash.InstanceId, 2));
            Assert.AreSame(bash, s.Runes.Rows[2].Skill);
            Assert.AreSame(s.Runes.Rows[2], bash.Holder);
            Assert.IsNull(s.Runes.Rows[0].Skill, "Der alte Ort ist leer");
            Assert.AreEqual(1, s.Runes.Rows.Count(r => r.Skill == bash));
        }

        [Test]
        public void PlacingOverAnotherSkillFreesIt()
        {
            OverworldSession s = ThreeRows();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            SkillInstance wall = s.Skills.All.Single(i => i.SkillId == SkillIds.ShieldWall);
            Assert.IsTrue(wall.IsFree, "Zweiter Start-Skill liegt frei in der Sammlung");

            Assert.IsTrue(s.PlaceSkill(wall.InstanceId, 0));
            Assert.AreSame(wall, s.Runes.Rows[0].Skill);
            Assert.IsTrue(bash.IsFree);
            CollectionAssert.Contains(s.Skills.Free.ToList(), bash);
        }

        [Test]
        public void SkillsMoveBetweenRows()
        {
            OverworldSession s = ThreeRows();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            SkillInstance wall = s.Skills.All.Single(i => i.SkillId == SkillIds.ShieldWall);
            s.PlaceSkill(wall.InstanceId, 1);

            Assert.IsTrue(s.SwapSkills(0, 1));
            Assert.AreSame(wall, s.Runes.Rows[0].Skill);
            Assert.AreSame(bash, s.Runes.Rows[1].Skill);
            Assert.AreSame(s.Runes.Rows[0], wall.Holder);
            Assert.AreSame(s.Runes.Rows[1], bash.Holder);

            // Rune und Skill sind getrennt: Zeilen verschieben nimmt den Skill mit, Runen tauschen lässt ihn stehen.
            Assert.IsTrue(s.MoveRow(1, 2));
            Assert.AreSame(bash, s.Runes.Rows[2].Skill);
        }

        [Test]
        public void OrphanedRowsOnlyWhenASkillIsTakenOut()
        {
            OverworldSession s = ThreeRows();
            Assert.IsTrue(s.Runes.Rows.All(r => r.Skill != null), "Neue Zeilen bekommen einen Skill oder den Basisangriff");

            SkillInstance bash = s.Runes.Rows[0].Skill;
            Assert.IsTrue(s.RemoveSkill(0));
            Assert.IsNull(s.Runes.Rows[0].Skill);
            Assert.IsTrue(bash.IsFree);
            Assert.AreEqual(3, s.Skills.Count, "Das Exemplar bleibt in der Sammlung");
        }

        [Test]
        public void RemovingARowFreesItsSkill()
        {
            OverworldSession s = Session();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            Assert.IsTrue(s.UnequipRune(0));
            Assert.IsTrue(bash.IsFree);
            Assert.IsTrue(s.Skills.Contains(bash));
        }

        // ------------------------------------------------------------------ Duplikate und Stufen

        [Test]
        public void DuplicateRaisesTheLevelOrStaysAsSecondCopy()
        {
            OverworldSession s = Session();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            var messages = new List<string>();
            s.BuildImproved += messages.Add;

            // Begründet angepasst (A-08): ein Duplikat gibt +5 Wachstum (= Stufe 1); Schildschlag wächst in der Betäubung.
            int stunBefore = s.DescribeSkill(bash).Effects.First(e => e.DurationTicks > 0).DurationTicks;
            Assert.AreSame(bash, s.GainSkill(SkillIds.ShieldBash, SkillDuplicateChoice.Upgrade));
            Assert.AreEqual(1, bash.Level);
            Assert.AreEqual(5, bash.Growth);
            Assert.AreEqual(3, s.Skills.Count, "kein neues Exemplar");
            StringAssert.Contains("Shield Bash → Shield Bash +5", messages.Last());
            Assert.AreEqual(stunBefore + 5 * Ticks.FromTenths(1), s.DescribeSkill(bash).Effects.First(e => e.DurationTicks > 0).DurationTicks);

            SkillInstance copy = s.GainSkill(SkillIds.ShieldBash, SkillDuplicateChoice.KeepCopy);
            Assert.AreNotSame(bash, copy);
            Assert.AreEqual(0, copy.Level);
            Assert.AreEqual(2, s.Skills.OfSkill(SkillIds.ShieldBash).Count);
            Assert.AreNotEqual(bash.InstanceId, copy.InstanceId);

            // Zwei Exemplare: derselbe Skill an zwei Zeilen.
            s.Runes.TryAdd(s.RuneCatalog.Get("hp_low"), SkillInstance.BasicAttack());
            Assert.IsTrue(s.PlaceSkill(copy.InstanceId, 1));
            Assert.AreEqual(2, s.Runes.Rows.Count(r => r.SkillId == SkillIds.ShieldBash));
        }

        [Test]
        public void SkillsStopAtTheMaximumLevel()
        {
            OverworldSession s = Session();
            SkillInstance bash = s.Runes.Rows[0].Skill;
            // Begründet angepasst (A-08): je Duplikat +5 Wachstum, Stufe 3 liegt bei 30.
            int duplicates = 0;
            while (s.CanUpgradeSkill(SkillIds.ShieldBash) && duplicates < 20)
            {
                s.GainSkill(SkillIds.ShieldBash);
                duplicates++;
            }

            Assert.AreEqual(6, duplicates);
            Assert.AreEqual(30, bash.Growth);
            Assert.AreEqual(s.Progression.MaxSkillLevel, bash.Level);
            Assert.IsFalse(s.CanUpgradeSkill(SkillIds.ShieldBash));

            SkillInstance copy = s.GainSkill(SkillIds.ShieldBash);
            Assert.AreEqual(s.Progression.MaxSkillLevel, bash.Level);
            Assert.AreNotSame(bash, copy, "Bei voller Stufe kommt ein zweites Exemplar");
            Assert.AreEqual(2, s.Skills.OfSkill(SkillIds.ShieldBash).Count);
            Assert.IsTrue(s.CanUpgradeSkill(SkillIds.ShieldBash), "Das zweite Exemplar kann wieder steigen");
        }

        [Test]
        public void SkillGrowthChangesTheNumbersAndTheFight()
        {
            // Begründet angepasst (A-08): Duplikate geben Wachstum, Rüstungsbruch wächst um +1 Schaden pro Punkt.
            OverworldSession s = Session("blade");
            SkillInstance breaker = s.Runes.Rows[0].Skill;
            int before = s.DescribeSkill(breaker).Effects.First(e => e.IsDamage).Amount;

            s.GainSkill(SkillIds.ArmorBreak);
            s.GainSkill(SkillIds.ArmorBreak);
            SkillInfo info = s.DescribeSkill(breaker);
            Assert.AreEqual(before + 10, info.Effects.First(e => e.IsDamage).Amount);

            // Der Kampf rechnet mit demselben Wachstum wie die Anzeige.
            var loadout = new RuneLoadout();
            var copy = new SkillCollection();
            SkillInstance grown = copy.Add(SkillIds.ArmorBreak);
            copy.Grow(grown, breaker.Growth);
            loadout.TryAdd(s.RuneCatalog.Get("always"), grown);
            BattleSetup setup = new ArenaCombatResolver().CreateSetup(
                new CombatRequest(CellContent.Enemy, 0, new PlayerStats(30, 0), loadout, s.Gear, null, s.Progression.SkillLevels),
                new List<CombatantSetup> { new CombatantSetup { Name = "Sandsack", Stats = new CombatStats(9999, 0) } }, 1);
            setup.MaxTicks = Ticks.FromSeconds(3);
            BattleResult r = CombatSimulation.Run(setup);
            BattleEvent hit = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Detail == SkillIds.ArmorBreak);
            Assert.AreEqual(info.Effects.First(e => e.IsDamage).Amount, hit.Amount);
        }

        // ------------------------------------------------------------------ Ausrüstung entkoppelt

        [Test]
        public void UnequippingAnItemNoLongerTakesASkill()
        {
            OverworldSession s = Session();
            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Shield));
            Assert.AreEqual(SkillIds.ShieldBash, s.Runes.Rows[0].SkillId);

            BattleSetup setup = new ArenaCombatResolver().CreateSetup(
                new CombatRequest(CellContent.Enemy, 0, s.Stats, s.Runes, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);
            Assert.IsFalse(setup.Player.Board.Rows[0].IsOrphaned);
            Assert.AreEqual(SkillIds.ShieldBash, setup.Player.Board.Rows[0].Skill.Id);
        }

        [Test]
        public void TagPassivesOnlyAffectMatchingSkills()
        {
            var gear = new Equipment();
            gear.Equip(Items.Get("shock_dagger")); // Schock-Skills +20 % Wirkung
            gear.Equip(Items.Get("tower_shield")); // Schild-Skills −1 s Cooldown

            SkillDefinition stab = gear.Boost(Catalog.Get(SkillIds.ShockStab));
            SkillDefinition breaker = gear.Boost(Catalog.Get(SkillIds.ArmorBreak));
            SkillDefinition bash = gear.Boost(Catalog.Get(SkillIds.ShieldBash));
            SkillDefinition ignite = gear.Boost(Catalog.Get(SkillIds.Ignite));

            Assert.AreEqual(BasisPoints.Percent(96), ((DamageEffect)stab.Effects[0]).DamageBp, "80 % + 20 %");
            Assert.AreEqual(BasisPoints.Percent(190), ((DamageEffect)breaker.Effects[0]).DamageBp, "Klinge ohne Schock: unverändert");
            Assert.AreEqual(Ticks.FromSeconds(5), bash.CooldownTicks, "6 s − 1 s");
            Assert.AreEqual(Catalog.Get(SkillIds.Ignite).CooldownTicks, ignite.CooldownTicks);
            Assert.AreSame(Catalog.Get(SkillIds.Ignite), ignite, "Ohne passenden Tag derselbe Skill");
            Assert.AreSame(SkillDefinition.BasicAttack, gear.Boost(SkillDefinition.BasicAttack), "Basisangriff hat keine Tags");
        }

        [Test]
        public void TagPassivesWorkInTheFight()
        {
            var gear = new Equipment();
            gear.Equip(Items.Get("short_sword"));
            gear.Equip(Items.Get("tower_shield"));
            var loadout = new RuneLoadout();
            loadout.TryAdd(RuneCatalog.CreateDefault().Get("always"), SkillIds.ShieldBash);

            BattleSetup setup = new ArenaCombatResolver().CreateSetup(
                new CombatRequest(CellContent.Enemy, 0, new PlayerStats(30, 0), loadout, gear),
                new List<CombatantSetup> { new CombatantSetup { Name = "Sandsack", Stats = new CombatStats(9999, 0) } }, 1);
            setup.MaxTicks = Ticks.FromSeconds(12);
            BattleResult r = CombatSimulation.Run(setup);

            List<int> bashes = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ShieldBash).Select(e => e.Tick).ToList();
            Assert.GreaterOrEqual(bashes.Count, 2);
            // Jeder Treffer des Basisangriffs dazwischen verkürzt den laufenden Cooldown um 0,25 s (A-12).
            int hits = r.Events.Count(e => e.Kind == BattleEventKind.Hit && e.Detail == SkillIds.BasicAttack && e.Tick > bashes[0] && e.Tick < bashes[1]);
            Assert.Greater(hits, 0);
            Assert.AreEqual(Ticks.FromSeconds(5) - hits * SkillBudgetConfig.Default.BasicAttackCooldownCutTicks, bashes[1] - bashes[0],
                "Cooldown 5 s statt 6 s, abzüglich der Basisangriff-Treffer");
        }

        [Test]
        public void EquipmentNoLongerDeliversSkills()
        {
            foreach (EquipmentDefinition item in Items.All)
                foreach (SkillPassive p in item.Passives)
                    Assert.AreNotEqual(SkillKind.None, p.Target, item.Id);

            foreach (SkillDefinition skill in Catalog.All.Where(s => !s.IsBasicAttack))
                Assert.AreNotEqual(SkillKind.None, skill.Kinds, $"{skill.Id} braucht eine Art");
        }

        // ------------------------------------------------------------------ Kits und Erhalt

        [Test]
        public void KitsStartWithSkillsInTheirCollection()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: kit);
                Assert.That(s.Skills.Count, Is.InRange(2, 3), kit.Id);
                CollectionAssert.AreEquivalent(kit.StartSkillIds, s.Skills.All.Select(i => i.SkillId), kit.Id);
                Assert.AreEqual(kit.StartSkillId, s.Runes.Rows[0].SkillId, kit.Id);
                Assert.AreSame(s.Runes.Rows[0].Skill, s.Skills.All[0], "Das Exemplar an der Tafel gehört zur Sammlung");
            }
        }

        [Test]
        public void FightRewardsCanOfferSkills()
        {
            OverworldSession s = Session();
            s.Progression.SkillOfferChanceVictory = 100;
            RuneOffer offer = s.OfferRunes(RewardSources.Victory);
            Assert.AreEqual(1, offer.SkillIds.Count);
            Assert.GreaterOrEqual(offer.Options.Count, 1, "mindestens eine Rune bleibt");

            string id = offer.SkillIds[0];
            int before = s.Skills.Count;
            bool owned = s.OwnsSkill(id);
            Assert.IsTrue(s.TakeSkill(0, SkillDuplicateChoice.KeepCopy));
            Assert.AreEqual(before + 1, s.Skills.Count, owned ? "zweites Exemplar" : "neuer Skill");
            Assert.IsNull(s.PendingRuneOffer);
        }

        [Test]
        public void ShopsSellSkills()
        {
            OverworldSession s = Session();
            s.Stats.AddGold(100);
            var shop = new Betaknight.Core.Shop.ShopInventory(new RuneDefinition[0], null, new[] { SkillIds.Anchor });
            typeof(OverworldSession).GetProperty(nameof(OverworldSession.PendingShop))
                .SetValue(s, new ShopVisit(s.CurrentCell, shop));

            int gold = s.Stats.Gold;
            Assert.IsTrue(s.BuyShopSkill(0));
            Assert.AreEqual(gold - s.ShopPrices.Skill, s.Stats.Gold);
            Assert.IsTrue(s.OwnsSkill(SkillIds.Anchor));
            Assert.IsEmpty(shop.SkillIds);
        }

        [Test]
        public void OffersPreferSkillsThatFitTheBuild()
        {
            OverworldSession s = Session(); // Schildritter: Schild-Skills, Schild-Rune
            int shield = s.SkillOfferWeight(Catalog.Get(SkillIds.Anchor));
            int fire = s.SkillOfferWeight(Catalog.Get(SkillIds.Ignite));
            Assert.Greater(shield, fire);

            Assert.IsTrue(s.IsImprovementSkill(SkillIds.ShieldBash), "Stufe für einen eigenen Skill");
            Assert.IsTrue(s.IsImprovementSkill(SkillIds.Anchor), "neuer Skill mit passendem Tag");
            Assert.IsFalse(s.IsImprovementSkill(SkillIds.Ignite));
        }

        [Test]
        public void SkillsTravelIntoTheNextAct()
        {
            OverworldSession s = Session();
            s.GainSkill(SkillIds.Drill);
            OverworldSession next = OverworldSession.CreateNextAct(new MapGenerationConfig { Radius = 4, Seed = 3 }, s);
            Assert.AreSame(s.Skills, next.Skills);
            Assert.IsTrue(next.OwnsSkill(SkillIds.Drill));
            Assert.IsTrue(next.Skills.Contains(next.Runes.Rows[0].Skill), "Das Exemplar an der Tafel reist mit");
        }
    }
}
