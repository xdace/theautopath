using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// A-05: Skills als eigene Exemplare, getrennt von Runen (Wann) und Ausrüstung (Werte und Passive). Seit A-19 liegen sie
    /// als Komponenten auf der Platine, Runen als Relais daneben.
    /// </summary>
    public class SkillCollectionTests
    {
        private static readonly EquipmentCatalog Items = EquipmentCatalog.CreateDefault();
        private static readonly SkillCatalog Catalog = SkillCatalog.CreateDefault();

        private static KnightKit Kit(string id) => KnightKit.Defaults.Single(k => k.Id == id);

        /// <summary>
        /// Session mit Start-Kit. Beim Schildritter liegt Schildschlag (Betäubung) als Komponente am Start-Relais «When Hit»
        /// auf (0, 0); Bohrstoß und Schildwall liegen frei in der Sammlung.
        /// </summary>
        private static OverworldSession Session(string kit = "shield", int seed = 3) =>
            OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = seed }, kit: Kit(kit));

        private static SkillInstance First(OverworldSession s) => s.Board.Components[0].Skill;

        private static BattleSetup Setup(CircuitBoard board, Equipment gear, PlayerStats stats = null, SkillLevelRules levels = null) =>
            new ArenaCombatResolver().CreateSetup(
                new CombatRequest(CellContent.Enemy, 0, stats ?? new PlayerStats(30, 0), board, gear, null, levels),
                new List<CombatantSetup> { new CombatantSetup { Name = "Sandsack", Stats = new CombatStats(9999, 0) } }, 1);

        // ------------------------------------------------------------------ Ein Exemplar, ein Ort

        [Test]
        public void ASkillSitsInExactlyOnePlace()
        {
            OverworldSession s = Session();
            SkillInstance bash = First(s);
            ComponentSlot slot = s.Board.ComponentOf(bash);
            Assert.AreSame(slot, bash.Holder);
            Cell old = slot.Origin;

            // Erneutes Legen verschiebt die Komponente, statt das Exemplar zu verdoppeln.
            Assert.IsTrue(s.PlaceSkill(bash.InstanceId, new Cell(3, 1)));
            Assert.AreSame(slot, bash.Holder);
            Assert.AreEqual(new Cell(3, 1), slot.Origin);
            Assert.IsNull(s.Board.At(old), "Der alte Ort ist leer");
            Assert.AreEqual(1, s.Board.Components.Count(c => c.Skill == bash));
        }

        [Test]
        public void PlacingOnAnOccupiedSpotIsRejected()
        {
            // A-19: nichts überlappt. Früher verdrängte ein Skill den anderen aus der Zeile, jetzt wird das Legen abgelehnt.
            OverworldSession s = Session();
            SkillInstance bash = First(s);
            SkillInstance wall = s.Skills.All.Single(i => i.SkillId == SkillIds.ShieldWall);
            Assert.IsTrue(wall.IsFree, "Zweiter Start-Skill liegt frei in der Sammlung");

            Assert.IsFalse(s.CanPlaceSkill(wall.InstanceId, s.Board.ComponentOf(bash).Origin));
            Assert.IsFalse(s.PlaceSkill(wall.InstanceId, s.Board.ComponentOf(bash).Origin));
            Assert.IsTrue(wall.IsFree);
            Assert.IsFalse(bash.IsFree);
            Assert.AreSame(bash, s.Board.Components.Single().Skill);
        }

        [Test]
        public void ComponentsAndRelaysMoveIndependently()
        {
            OverworldSession s = Session();
            SkillInstance bash = First(s);
            SkillInstance wall = s.Skills.All.Single(i => i.SkillId == SkillIds.ShieldWall);
            Assert.IsTrue(s.PlaceSkill(wall.InstanceId, new Cell(3, 0)));
            ComponentSlot bashSlot = s.Board.ComponentOf(bash);
            ComponentSlot wallSlot = s.Board.ComponentOf(wall);

            // Verschieben nimmt das Exemplar mit: dieselbe Komponente, neuer Ort.
            Assert.IsTrue(s.MoveComponent(s.Board.IndexOf(bashSlot), new Cell(0, 1), false));
            Assert.AreSame(bashSlot, bash.Holder);
            Assert.AreSame(wallSlot, wall.Holder);
            Assert.AreEqual(new Cell(0, 1), bashSlot.Origin);
            Assert.IsTrue(s.IsPowered(bashSlot), "Unter dem Relais auf (0, 0)");

            // Relais und Skill sind getrennt: das Relais verschieben lässt die Komponenten liegen.
            Assert.IsTrue(s.MoveRelay(0, new Cell(2, 2)));
            Assert.AreEqual(new Cell(0, 1), bashSlot.Origin);
            Assert.AreEqual(new Cell(3, 0), wallSlot.Origin);
            Assert.IsFalse(s.IsPowered(bashSlot), "Das Relais berührt die Komponente nicht mehr");
        }

        [Test]
        public void RemovingAComponentKeepsTheSkillInTheCollection()
        {
            OverworldSession s = Session();
            SkillInstance bash = First(s);
            Assert.IsTrue(s.RemoveComponent(0));
            Assert.IsEmpty(s.Board.Components);
            Assert.IsTrue(bash.IsFree);
            Assert.AreEqual(1, s.Board.Relays.Count, "Das Relais bleibt liegen");
            Assert.AreEqual(3, s.Skills.Count, "Das Exemplar bleibt in der Sammlung");
        }

        [Test]
        public void RemovingARelayLeavesItsComponentUnpowered()
        {
            // A-19: Relais und Komponente sind getrennte Teile; das Relais ins Inventar nehmen lässt die Komponente liegen.
            OverworldSession s = Session();
            SkillInstance bash = First(s);
            Assert.IsTrue(s.UnequipRune(0));
            Assert.IsFalse(bash.IsFree);
            Assert.IsTrue(s.Skills.Contains(bash));
            Assert.IsFalse(s.IsPowered(s.Board.ComponentOf(bash)));
        }

        // ------------------------------------------------------------------ Duplikate und Stufen

        [Test]
        public void DuplicateRaisesTheLevelOrStaysAsSecondCopy()
        {
            OverworldSession s = Session();
            SkillInstance bash = First(s);
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

            // Zwei Exemplare: derselbe Skill als zwei Komponenten auf der Platine.
            Assert.IsTrue(s.PlaceSkill(copy.InstanceId, new Cell(3, 1)));
            Assert.AreEqual(2, s.Board.Components.Count(c => c.Skill.SkillId == SkillIds.ShieldBash));
        }

        [Test]
        public void SkillsStopAtTheMaximumLevel()
        {
            OverworldSession s = Session();
            SkillInstance bash = First(s);
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
            // Begründet angepasst (A-08): Duplikate geben Wachstum, Schockstich wächst um +1 Schaden pro Punkt (Kills).
            // A-19: Start-Komponente des Klingenritters ist jetzt Schockstich (früher Rüstungsbruch, beide wachsen gleich).
            OverworldSession s = Session("blade");
            SkillInstance stab = First(s);
            Assert.AreEqual(SkillIds.ShockStab, stab.SkillId);
            int before = s.DescribeSkill(stab).Effects.First(e => e.IsDamage).Amount;

            s.GainSkill(SkillIds.ShockStab);
            s.GainSkill(SkillIds.ShockStab);
            SkillInfo info = s.DescribeSkill(stab);
            Assert.AreEqual(before + 10, info.Effects.First(e => e.IsDamage).Amount);

            // Der Kampf rechnet mit demselben Wachstum wie die Anzeige. Relais «Clock» (Schwierigkeit 0, kein Bonus) und
            // Komponente abseits des Kerns, damit nur das Wachstum zählt.
            var board = new CircuitBoard();
            var copy = new SkillCollection();
            SkillInstance grown = copy.Add(SkillIds.ShockStab);
            copy.Grow(grown, stab.Growth);
            Assert.IsNotNull(board.AddRelay(s.RuneCatalog.Get("clock"), new Cell(2, 0)));
            ComponentSlot slot = board.Place(grown, new Cell(3, 0));
            Assert.IsNotNull(slot);
            Assert.IsFalse(board.TouchesCore(slot));
            BattleSetup setup = Setup(board, s.Gear, levels: s.Progression.SkillLevels);
            setup.MaxTicks = Ticks.FromSeconds(4);
            BattleResult r = CombatSimulation.Run(setup);
            BattleEvent hit = r.Events.First(e => e.Kind == BattleEventKind.Damage && e.Detail == SkillIds.ShockStab);
            Assert.AreEqual(info.Effects.First(e => e.IsDamage).Amount, hit.Amount);
        }

        // ------------------------------------------------------------------ Ausrüstung entkoppelt

        [Test]
        public void UnequippingAnItemNoLongerTakesASkill()
        {
            OverworldSession s = Session();
            Assert.IsTrue(s.UnequipToInventory(EquipmentSlot.Shield));
            Assert.AreEqual(SkillIds.ShieldBash, First(s).SkillId);

            BattleSetup setup = new ArenaCombatResolver().CreateSetup(
                new CombatRequest(CellContent.Enemy, 0, s.Stats, s.Board, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);
            Assert.IsFalse(setup.Player.Board.Rows[0].IsOrphaned);
            Assert.IsTrue(setup.Player.Board.Rows[0].IsPowered);
            Assert.AreEqual(SkillIds.ShieldBash, setup.Player.Board.Rows[0].Skill.Id);
        }

        [Test]
        public void TagPassivesOnlyAffectMatchingSkills()
        {
            var gear = new Equipment();
            gear.Equip(Items.Get("shock_dagger")); // Schock-Skills +20 % Wirkung
            gear.Equip(Items.Get("tower_shield")); // Schild-Skills −15 % Cast-Zeit

            SkillDefinition stab = gear.Boost(Catalog.Get(SkillIds.ShockStab));
            SkillDefinition breaker = gear.Boost(Catalog.Get(SkillIds.ArmorBreak));
            SkillDefinition bash = gear.Boost(Catalog.Get(SkillIds.ShieldBash));
            SkillDefinition ignite = gear.Boost(Catalog.Get(SkillIds.Ignite));

            Assert.AreEqual(BasisPoints.Percent(72), ((DamageEffect)stab.Effects[0]).DamageBp, "60 % + 20 %");
            Assert.AreEqual(BasisPoints.Percent(300), ((DamageEffect)breaker.Effects[0]).DamageBp, "Klinge ohne Schock: unverändert");
            Assert.AreEqual(-15, bash.CastBonusPercent);
            Assert.AreEqual(CastTime.Medium * 85 / 100, bash.CastTicks(), "0,8 s − 15 %");
            Assert.AreEqual(Catalog.Get(SkillIds.Ignite).CastTicks(), ignite.CastTicks());
            Assert.AreSame(Catalog.Get(SkillIds.Ignite), ignite, "Ohne passenden Tag derselbe Skill");
            Assert.AreSame(SkillDefinition.BasicAttack, gear.Boost(SkillDefinition.BasicAttack), "Basisangriff hat keine Tags");
        }

        [Test]
        public void TagPassivesWorkInTheFight()
        {
            // A-19: keine Cooldowns mehr; der Turmschild verkürzt die Cast-Zeit von Schild-Skills um 15 %, auch im Kampf.
            int CastInFight(params string[] items)
            {
                var gear = new Equipment();
                foreach (string id in items) gear.Equip(Items.Get(id));
                var board = new CircuitBoard();
                board.AddRelay(RuneCatalog.CreateDefault().Get("on_hit"), new Cell(0, 0));
                Assert.IsNotNull(board.Place(new SkillInstance(SkillIds.ShieldBash), new Cell(0, 1)));
                BattleSetup setup = Setup(board, gear);
                setup.MaxTicks = Ticks.FromSeconds(6);
                BattleResult r = CombatSimulation.Run(setup);
                List<BattleEvent> bashes = r.Events.Where(e => e.Kind == BattleEventKind.ActionStarted && e.Detail == SkillIds.ShieldBash).ToList();
                Assert.GreaterOrEqual(bashes.Count, 1, "«On Hit» versorgt den Schildschlag");
                return bashes[0].Amount;
            }

            Assert.AreEqual(CastTime.Medium, CastInFight("short_sword"));
            Assert.AreEqual(CastTime.Medium * 85 / 100, CastInFight("short_sword", "tower_shield"), "−15 % Cast-Zeit");
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
                Assert.AreEqual(kit.StartSkillId, First(s).SkillId, kit.Id);
                Assert.AreSame(First(s), s.Skills.All[0], "Das Exemplar auf der Platine gehört zur Sammlung");
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
            Assert.IsTrue(next.Skills.Contains(First(next)), "Das Exemplar auf der Platine reist mit");
        }
    }
}
