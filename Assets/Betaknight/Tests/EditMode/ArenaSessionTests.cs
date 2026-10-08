using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Encounters;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Die Arena hinter der Oberwelt: Ausrüstung, Platine aus Relais und Komponenten, Belohnungen, Lagerfeuer.</summary>
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
            var skills = new SkillCollection();
            foreach (string id in kit.StartSkillIds) skills.Add(id);
            var board = new CircuitBoard();
            KnightKit.LayOut(board, runeCatalog.Get(kit.StartRuneId), skills.All.First(k => k.SkillId == kit.StartSkillId));

            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats ?? kit.CreateStats(), null, board, runeCatalog, combat, gear, items, skills: skills);
        }

        [Test]
        public void CreateEquipsTheKitAndWiresTheStartRune()
        {
            foreach (KnightKit kit in KnightKit.Defaults)
            {
                OverworldSession s = OverworldSession.Create(new MapGenerationConfig { Radius = 4, Seed = 3 }, kit: kit);
                CollectionAssert.AreEquivalent(kit.StartItemIds, s.Gear.Items.Select(i => i.Id), kit.Id);
                Assert.AreEqual(kit.StartSkillId, s.Board.Components.Single().Skill.SkillId, kit.Id);
                Assert.AreEqual(kit.StartRuneId, s.Board.Relays.Single().Rune.Id, kit.Id);
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
        public void TheBoardComesFromTheCircuitAndGear()
        {
            OverworldSession s = Session(Kit("shield"));
            var resolver = new ArenaCombatResolver();
            BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 1, s.Stats, s.Board, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);

            Assert.AreEqual(SkillIds.ShieldBash, setup.Player.Board.Rows[0].Skill.Id);
            Assert.IsTrue(setup.Player.Board.Rows[0].IsPowered, "«When Hit» versorgt den Start-Skill");
            Assert.AreEqual(s.Stats.MaxHp, setup.Player.Stats[StatKind.MaxHp]);
            Assert.AreEqual(2, setup.Player.Stats[StatKind.Armor], "Rundschild");

            s.Gear.Unequip(EquipmentSlot.Shield);
            setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, 1, s.Stats, s.Board, s.Gear), new List<CombatantSetup> { new CombatantSetup() }, 1);
            // A-05: Der Skill gehört der Komponente, nicht dem Schild. Ablegen kostet nur die Werte.
            Assert.AreEqual(SkillIds.ShieldBash, setup.Player.Board.Rows[0].Skill.Id);
            Assert.AreEqual(0, setup.Player.Stats[StatKind.Armor]);
        }

        [Test]
        public void TreasureOffersEquipmentWithoutTouchingTheBoard()
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
            Assert.AreEqual(Kit("shield").StartSkillId, s.Board.Components.Single().Skill.SkillId, "Ein neues Teil ändert keine Komponenten auf der Platine.");
        }

        [Test]
        public void NewRunesBecomeRelaysNextToUnpoweredComponents()
        {
            // A-19 (früher: neue Rune bekommt einen freien Skill oder den Basisangriff): Ein neues Relais landet neben einer
            // Komponente, die noch kein Relais hat; sonst auf der ersten freien Zelle.
            OverworldSession s = Session(Kit("blade"));
            SkillInstance bash = s.Skills.Add(SkillIds.ShieldBash);
            Assert.IsTrue(s.PlaceSkill(bash.InstanceId, new Cell(3, 0)));
            ComponentSlot bashSlot = s.Board.ComponentOf(bash);
            Assert.IsEmpty(s.Board.RelaysTouching(bashSlot));

            s.OfferRunes("Test");
            Assert.IsTrue(s.TakeRune(0));
            Assert.AreEqual(2, s.Board.Relays.Count);
            Assert.AreEqual(1, s.Board.RelaysTouching(bashSlot).Count, "Das neue Relais berührt die unversorgte Komponente.");

            Cell? free = s.Board.FreeCellFor(Shape.One);
            s.OfferRunes("Test");
            Assert.IsTrue(s.TakeRune(0));
            Assert.AreEqual(3, s.Board.Relays.Count);
            Assert.IsTrue(s.Board.Relays.Any(r => r.Position == free.Value), "Alle Komponenten haben ein Relais: erste freie Zelle.");
        }

        [Test]
        public void RelaysAndComponentsCanBeMovedAndRemoved()
        {
            // A-19 (früher: Zeilen umsortieren und neu verdrahten): Relais und Komponenten werden verschoben, die Lesereihenfolge folgt.
            OverworldSession s = Session(Kit("shield"));
            s.OfferRunes("Test");
            s.TakeRune(0);
            Assert.AreEqual(2, s.Board.Relays.Count);
            string second = s.Board.Relays[1].Rune.Id;

            Cell before = s.Board.Relays[0].Position;
            Assert.IsTrue(s.MoveRelay(0, new Cell(0, 2)));
            Assert.IsTrue(s.MoveRelay(s.Board.Relays.ToList().FindIndex(r => r.Rune.Id == second), before));
            Assert.AreEqual(second, s.Board.Relays[0].Rune.Id);
            Assert.IsFalse(s.PlaceSkill(999, new Cell(3, 2)), "Nur Exemplare aus der Sammlung.");

            SkillInstance skill = s.Board.Components[0].Skill;
            Assert.IsTrue(s.RemoveComponent(0));
            Assert.IsEmpty(s.Board.Components);
            Assert.IsTrue(skill.IsFree);
        }

        [Test]
        public void CampfireUpgradesTheWeakestRune()
        {
            OverworldSession s = Session(Kit("shield"));
            var hpLow = s.RuneCatalog.Get("hp_low");
            RelayChip low = s.Board.AddRelay(hpLow);
            Assert.IsTrue(s.Board.Upgrade(s.Board.IndexOf(low)));
            RelayChip whenHit = s.Board.Relays.Single(r => r != low);

            var resolver = new EncounterResolver(s.Map, s.Exploration, s.Stats, new System.Random(1), s.Board);
            List<string> lines = resolver.Apply(new EncounterOption("Rune verstärken", EncounterEffect.UpgradeRune()), HexCoord.Zero);

            Assert.AreEqual(0, whenHit.Level, "when_hit hat keine Stufen.");
            Assert.AreEqual(2, low.Level);
            Assert.AreEqual(hpLow.NameAt(2), low.Name);
            StringAssert.Contains("Rune upgraded", lines.Single());
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
                    BattleSetup setup = resolver.CreateSetup(new CombatRequest(CellContent.Enemy, enemy.MinTier, s.Stats, s.Board, s.Gear), enemy.Create(), seed);
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
