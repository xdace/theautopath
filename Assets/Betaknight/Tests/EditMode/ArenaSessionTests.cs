using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Encounters;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Die Arena hinter der Oberwelt: Ausrüstung, Tafel aus Runen-Plätzen, Belohnungen, Lagerfeuer.</summary>
    public class ArenaSessionTests
    {
        private static readonly HexCoord East = new HexCoord(1, 0);
        private static KnightKit Kit(string id) => KnightKit.Defaults.Single(k => k.Id == id);

        private static OverworldSession Session(KnightKit kit, PlayerStats stats = null, ICombatResolver combat = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));

            var items = EquipmentCatalog.CreateDefault();
            var gear = new Equipment();
            foreach (string id in kit.StartItemIds) gear.Equip(items.Get(id));
            var runeCatalog = RuneCatalog.CreateDefault();
            var runes = new RuneLoadout();
            runes.TryAdd(runeCatalog.Get(kit.StartRuneId), kit.StartSkillId);

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats ?? kit.CreateStats(), null, runes, runeCatalog, combat, gear, items);
        }

        [Test]
        public void CreateEquipsTheKitAndWiresTheStartRune()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: kit);
                CollectionAssert.AreEquivalent(kit.StartItemIds, s.Gear.Items.Select(i => i.Id), kit.Id);
                Assert.AreEqual(kit.StartSkillId, s.Runes.Rows.Single().SkillId, kit.Id);
            }
        }

        [Test]
        public void FightsRunInTheArenaAndKeepTheLog()
        {
            OverworldSession s = Session(Kit("shield"));
            s.Map.SetContent(East, CellContent.Enemy);
            CombatResult? fought = null;
            s.CombatFinished += r => fought = r;
            int hp = s.Stats.Hp;

            s.TryStep(East);

            Assert.IsTrue(fought.HasValue);
            Assert.IsNotNull(fought.Value.Battle);
            Assert.IsNotEmpty(fought.Value.EnemyName);
            Assert.AreEqual(BattleOutcome.Victory, fought.Value.Outcome, "Stufe 1 schafft der Startritter.");
            Assert.AreEqual(hp - fought.Value.DamageTaken, s.Stats.Hp);
            Assert.AreEqual(fought.Value.Battle.PlayerHp, s.Stats.Hp);
        }

        [Test]
        public void TheBoardComesFromRuneRowsAndGear()
        {
            OverworldSession s = Session(Kit("shield"));
            var resolver = new ArenaCombatResolver();
            BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 1, s.Stats, s.Runes, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);

            Assert.AreEqual(SkillIds.Drill, setup.Player.Board.Rows[0].Skill.Id);
            Assert.AreEqual(s.Stats.MaxHp, setup.Player.Stats[StatKind.MaxHp]);
            Assert.AreEqual(2, setup.Player.Stats[StatKind.Armor], "Rundschild");

            s.Gear.Unequip(EquipmentSlot.Shield);
            setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 1, s.Stats, s.Runes, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);
            // A-05: Der Skill gehört der Zeile, nicht dem Schild. Ablegen kostet nur die Werte.
            Assert.AreEqual(SkillIds.Drill, setup.Player.Board.Rows[0].Skill.Id);
            Assert.AreEqual(0, setup.Player.Stats[StatKind.Armor]);
        }

        [Test]
        public void TreasureOffersEquipmentThatRewiresOrphanedRows()
        {
            OverworldSession s = Session(Kit("shield"));
            s.Gear.Unequip(EquipmentSlot.Shield);
            s.Map.SetContent(East, CellContent.Treasure);
            s.TryStep(East);

            RuneOffer offer = s.PendingRuneOffer;
            Assert.AreEqual(1, offer.ItemIds.Count);
            Assert.AreEqual(3, offer.Count - offer.ModuleIds.Count, "Ein seltenes Modul darf dazukommen (A-07).");

            EquipmentDefinition item = s.Items.Get(offer.ItemIds[0]);
            Assert.IsTrue(s.TakeItem(0));
            Assert.AreSame(item, s.Gear.Get(item.Slot));
            Assert.IsNull(s.PendingRuneOffer);
            Assert.AreEqual(Kit("shield").StartSkillId, s.Runes.Rows[0].SkillId, "Ein neues Teil ändert keine Skills an der Tafel.");
        }

        [Test]
        public void NewRunesGetAFreeSkillOrTheBasicAttack()
        {
            // Begründet angepasst (A-05): Skills kommen nicht mehr aus der Ausrüstung, sondern aus der Sammlung.
            OverworldSession s = Session(Kit("blade"));
            s.Skills.Add(SkillIds.ShieldBash);
            s.OfferRunes("Test");
            Assert.IsTrue(s.TakeRune(0));

            Assert.AreEqual(SkillIds.ArmorBreak, s.Runes.Rows[0].SkillId);
            Assert.AreEqual(SkillIds.ShieldBash, s.Runes.Rows[1].SkillId);

            s.OfferRunes("Test");
            Assert.IsTrue(s.TakeRune(0));
            Assert.AreEqual(SkillIds.BasicAttack, s.Runes.Rows[2].SkillId, "Kein freier Skill mehr: Basisangriff statt verwaist.");
        }

        [Test]
        public void RowsCanBeReorderedAndRewired()
        {
            OverworldSession s = Session(Kit("shield"));
            s.OfferRunes("Test");
            s.TakeRune(0);
            string second = s.Runes.Rows[1].Rune.Id;

            Assert.IsTrue(s.MoveRow(1, 0));
            Assert.AreEqual(second, s.Runes.Rows[0].Rune.Id);
            Assert.IsFalse(s.PlaceSkill(999, 0), "Nur Exemplare aus der Sammlung.");
            Assert.IsTrue(s.PlaceBasicAttack(0));
            Assert.IsTrue(s.RemoveSkill(0));
            Assert.IsTrue(s.Runes.Rows[0].Skill == null);
        }

        [Test]
        public void CampfireUpgradesTheWeakestRune()
        {
            OverworldSession s = Session(Kit("shield"));
            var hpLow = s.RuneCatalog.Get("hp_low");
            s.Runes.TryAdd(hpLow, SkillIds.ShieldBash);
            s.Runes.Upgrade(1);

            var resolver = new EncounterResolver(s.Map, s.Exploration, s.Stats, new System.Random(1), s.Runes);
            List<string> lines = resolver.Apply(new EncounterOption("Rune verstärken", EncounterEffect.UpgradeRune()), HexCoord.Zero);

            Assert.AreEqual(0, s.Runes.Rows[0].Level, "when_hit hat keine Stufen.");
            Assert.AreEqual(2, s.Runes.Rows[1].Level);
            Assert.AreEqual(hpLow.NameAt(2), s.Runes.Rows[1].Name);
            StringAssert.Contains("Rune verstärkt", lines.Single());
        }

        [Test]
        public void EveryEnemyIsBeatableAndNeverTimesOut()
        {
            EnemyCatalog enemies = EnemyCatalog.CreateDefault();
            var resolver = new ArenaCombatResolver();
            OverworldSession s = Session(Kit("shield"));
            foreach (EnemyDefinition enemy in enemies.All)
            {
                int wins = 0;
                for (int seed = 1; seed <= 10; seed++)
                {
                    BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, enemy.MinTier, s.Stats, s.Runes, s.Gear), enemy.Create(), seed);
                    BattleResult r = CombatSimulation.Run(setup);
                    Assert.AreNotEqual(BattleOutcome.Timeout, r.Outcome, enemy.Id);
                    if (r.IsVictory) wins++;
                }
                if (!enemy.IsBoss) Assert.Greater(wins, 0, $"{enemy.Id}: Startritter mit vollem Leben gewinnt manchmal.");
            }
        }

        [Test]
        public void EnemyPickRespectsTiersAndBosses()
        {
            EnemyCatalog enemies = EnemyCatalog.CreateDefault();
            var random = new System.Random(5);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsTrue(enemies.Pick(1, false, random).FitsTier(1));
                Assert.IsTrue(enemies.Pick(9, false, random).FitsTier(9));
                Assert.IsTrue(enemies.Pick(3, true, random).IsBoss);
            }
        }
    }
}
