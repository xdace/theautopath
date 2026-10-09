using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// Gegner-Platinen: Gegner rüsten sich mit Skills, Runen, Modulen und Chips aus den Katalogen des Ritters aus (Elite mehr),
    /// kämpfen damit wie der Ritter, die Karte zeigt vorher genau diese Platine, und nach dem Sieg lässt sich davon bergen.
    /// </summary>
    public class EnemyBoardTests
    {
        private static readonly EnemyCatalog Enemies = EnemyCatalog.CreateDefault();
        private static readonly EnemyLoadout Loadout = EnemyLoadout.Default;
        private static readonly HexCoord East = new HexCoord(1, 0);

        private static EnemyEncounter Roll(int tier, CellContent content, int seed) =>
            EnemyEncounter.Roll(Enemies, Loadout, tier, content, new Random(seed));

        /// <summary>Ausrüstungs-Komponenten: an einem Runen-Relais (feste Teile haben eigene Bedingungen).</summary>
        private static IEnumerable<LogicRow> GearRows(EnemyEncounter e) =>
            e.Fighters[0].Board.Rows.Where(r => r.Skill != null && r.Relays.Any(x => x.RuneId != null));

        // ------------------------------------------------------------------ Ausrüstung

        [Test]
        public void EarlyEnemiesFightWithTheirOwnPartsOnly()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                EnemyEncounter e = Roll(0, CellContent.Enemy, seed);
                Assert.IsEmpty(GearRows(e), e.Name);
                Assert.IsEmpty(e.Loot, e.Name);
                Assert.AreEqual(0, e.LootPicks);
            }
        }

        [Test]
        public void ElitesCarryBetterBoardsThanNormalEnemies()
        {
            int normalGear = 0, eliteGear = 0, normalModules = 0, eliteModules = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                EnemyEncounter normal = Roll(4, CellContent.Enemy, seed);
                EnemyEncounter elite = Roll(4, CellContent.Elite, seed);
                normalGear += GearRows(normal).Count();
                eliteGear += GearRows(elite).Count();
                normalModules += normal.Fighters[0].Board.Rows.Sum(r => r.Skill?.Modules.Count ?? 0);
                int modules = elite.Fighters[0].Board.Rows.Sum(r => r.Skill?.Modules.Count ?? 0);
                eliteModules += modules;

                Assert.GreaterOrEqual(modules, 1, $"Seed {seed}: Elite trägt mindestens ein Modul");
                Assert.AreEqual(EnemyLoadoutConfig.Default.EliteChips, elite.Fighters[0].Board.Chips.Count, $"Seed {seed}");
                Assert.AreEqual(EnemyLoadoutConfig.Default.EliteLootPicks, elite.LootPicks);
                Assert.AreEqual(normal.Loot.Count > 0 ? EnemyLoadoutConfig.Default.LootPicks : 0, normal.LootPicks);
            }
            Assert.Greater(eliteGear, normalGear);
            Assert.Greater(eliteModules, normalModules);
        }

        [Test]
        public void EnemyGearIsBuiltLikeThePlayersBoard()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                EnemyEncounter e = Roll(8, CellContent.Elite, seed);
                LogicBoard board = e.Fighters[0].Board;
                foreach (LogicRow row in GearRows(e))
                {
                    int index = board.Rows.ToList().IndexOf(row);
                    Assert.IsNotEmpty(row.Relays, $"Seed {seed}: {row.Skill.Name} hat ein Relais");
                    Assert.IsTrue(row.Relays.Where(r => r.RuneId != null).All(r => r.MaxCells >= row.Skill.Shape.Cells),
                        $"Seed {seed}: {row.Skill.Name} wird versorgt (nicht zu gross)");
                    Assert.AreEqual(Loadout.Factory.CreateComponent(new Betaknight.Core.Gear.ComponentSpec(row.Skill.Id, row.Rect.Value.Origin), null).Pins.Count,
                        row.Pins.Count, "Pins wie beim Ritter");
                    Assert.GreaterOrEqual(index, 0);
                }
                foreach (LogicRow a in board.Rows)
                foreach (LogicRow b in board.Rows)
                    if (a != b) Assert.IsFalse(a.Rect.Value.Overlaps(b.Rect.Value), $"Seed {seed}: Komponenten überlappen nicht");
                foreach (LogicChip chip in board.Chips)
                    Assert.IsFalse(board.Rows.Any(r => r.Rect.Value.Contains(chip.Rect.Origin)), $"Seed {seed}: Chip liegt frei");
            }
        }

        [Test]
        public void EnemiesFightWithTheirModules()
        {
            // Ein Gegner mit Schockstich an «Battle Start», daran Multicast: im Kampf castet er zweimal.
            var fighter = new EnemyFighter("B", new CombatStats(100000, 10, 1000, 0), new EnemyPart[0]);
            var gear = new EnemyGear();
            var stab = new EnemyGearComponent("battle_start", SkillIds.ShockStab);
            stab.Modules.Add(ModuleIds.Multicast);
            gear.Components.Add(stab);
            LogicBoard board = EnemyBoard.Build(fighter.Parts, gear, Loadout);
            LogicRow row = board.Rows.Single(r => r.Skill?.Id == SkillIds.ShockStab);
            CollectionAssert.Contains(row.Skill.Modules, Loadout.ModuleName(ModuleIds.Multicast));

            BattleSetup setup = ArenaSimulationTests.Duel(ArenaSimulationTests.Fighter("A", 100000, 0, 1000), fighter.ToSetup(board), 1);
            setup.MaxTicks = Ticks.FromSeconds(5);
            BattleResult r = CombatSimulation.Run(setup);
            int hits = r.Events.Count(e => e.Source?.Name == "B" && e.Kind == BattleEventKind.Damage && e.Detail == SkillIds.ShockStab);
            Assert.AreEqual(2, hits, "Multicast wirkt beim Gegner wie beim Ritter");
        }

        [Test]
        public void EliteModulesAlsoSitOnTheEnemysOwnSkills()
        {
            bool found = false;
            for (int seed = 0; seed < 40 && !found; seed++)
            {
                EnemyEncounter e = Roll(4, CellContent.Elite, seed);
                found = e.Fighters[0].Board.Rows.Any(r => r.Skill != null && r.Relays.All(x => x.RuneId == null) && r.Skill.Modules.Count > 0);
            }
            Assert.IsTrue(found);
        }

        [Test]
        public void TheSameSeedGivesTheSameEnemyAndBoard()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                EnemyEncounter a = Roll(6, CellContent.Elite, seed), b = Roll(6, CellContent.Elite, seed);
                Assert.AreEqual(a.Name, b.Name);
                CollectionAssert.AreEqual(EnemyBoard.Lines(a.Fighters[0].Board), EnemyBoard.Lines(b.Fighters[0].Board));
                CollectionAssert.AreEqual(a.Loot, b.Loot);
            }
        }

        // ------------------------------------------------------------------ Beute

        [Test]
        public void LootIsExactlyWhatTheEnemyUsed()
        {
            for (int seed = 0; seed < 30; seed++)
            {
                EnemyEncounter e = Roll(7, CellContent.Elite, seed);
                LogicBoard board = e.Fighters[0].Board;
                var used = new HashSet<EnemyLoot>();
                foreach (LogicRow row in GearRows(e))
                {
                    used.Add(new EnemyLoot(EnemyLootKind.Skill, row.Skill.Id, null));
                    foreach (LogicRelay relay in row.Relays.Where(x => x.RuneId != null)) used.Add(new EnemyLoot(EnemyLootKind.Rune, relay.RuneId, null));
                }
                foreach (LogicRow row in board.Rows)
                    if (row.Skill != null && Loadout.Skills.TryGet(row.Skill.Id, out SkillDefinition own) && !own.IsBasicAttack)
                        used.Add(new EnemyLoot(EnemyLootKind.Skill, row.Skill.Id, null));
                foreach (LogicChip chip in board.Chips) used.Add(new EnemyLoot(EnemyLootKind.Chip, chip.Definition.Id, null));
                var moduleNames = board.Rows.Where(r => r.Skill != null).SelectMany(r => r.Skill.Modules).ToList();

                foreach (EnemyLoot l in e.Loot)
                {
                    if (l.Kind == EnemyLootKind.Module) CollectionAssert.Contains(moduleNames, l.Name, $"Seed {seed}");
                    else Assert.IsTrue(used.Contains(l), $"Seed {seed}: {l}");
                }
                Assert.IsTrue(used.All(e.Loot.Contains), $"Seed {seed}: alles Benutzte ist Beute");
            }
        }

        [Test]
        public void TheBossCarriesNoGearAndDropsNothing()
        {
            EnemyEncounter boss = Roll(20, CellContent.Boss, 1);
            Assert.IsEmpty(boss.Loot);
            Assert.IsEmpty(boss.Fighters[0].Board.Chips);
        }

        // ------------------------------------------------------------------ Session: Karte und Bergen

        private sealed class LootCombat : ICombatResolver
        {
            public List<EnemyLoot> Loot = new List<EnemyLoot>();
            public int Picks = 1;

            public CombatResult Resolve(CombatRequest request, Random random) =>
                new CombatResult(true, 0, 5, null, "Thief", Loot, Picks);
        }

        private static OverworldSession Session(ICombatResolver combat, PlayerStats stats = null)
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4))
                map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                stats ?? new PlayerStats(maxHp: 30), null, null, null, combat);
        }

        [Test]
        public void TheMapShowsExactlyTheEnemyWaitingThere()
        {
            OverworldSession s = Session(new ArenaCombatResolver(), new PlayerStats(maxHp: 100000));
            s.Map.SetContent(East, CellContent.Elite);
            List<EnemyBoardPreview> previews = s.EnemyBoardsAt(East);
            Assert.AreEqual(1, previews.Count);
            EnemyBoardPreview preview = previews[0];
            Assert.IsTrue(preview.IsExact);
            Assert.IsNotEmpty(preview.Fighters);
            Assert.AreEqual(previews[0].Name, s.EnemyBoardsAt(East)[0].Name, "gleich bei jedem Hover");

            s.TryStep(East);
            CombatResult fought = s.LastCombat.Value;
            Assert.AreEqual(preview.Name, fought.EnemyName);
            CollectionAssert.AreEqual(preview.Fighters.SelectMany(f => EnemyBoard.Lines(f.Board)).ToList(),
                fought.Battle.Fighters.Where(f => f.Side == Side.Enemy).SelectMany(f => EnemyBoard.Lines(f.Combatant.Board)).ToList(),
                "gekämpft wird gegen genau die gezeigte Platine");
            if (fought.Victory) CollectionAssert.AreEqual(preview.Loot, fought.Loot);
        }

        [Test]
        public void AfterAVictoryYouSalvagePartsThenChooseTheReward()
        {
            var combat = new LootCombat { Picks = 2 };
            combat.Loot.Add(new EnemyLoot(EnemyLootKind.Skill, SkillIds.Drill, "Drill"));
            combat.Loot.Add(new EnemyLoot(EnemyLootKind.Module, ModuleIds.Multicast, "Multicast"));
            combat.Loot.Add(new EnemyLoot(EnemyLootKind.Chip, CircuitEffectIds.Firewall, "Firewall"));
            OverworldSession s = Session(combat);
            s.Map.SetContent(East, CellContent.Enemy);
            s.TryStep(East);

            SalvageOffer offer = s.PendingSalvage;
            Assert.IsNotNull(offer);
            Assert.IsTrue(s.IsBusy);
            Assert.IsNull(s.PendingRuneOffer, "erst bergen, dann die Belohnung");
            Assert.AreEqual(2, offer.PicksLeft);

            Assert.IsTrue(s.TakeSalvage(0));
            Assert.IsTrue(s.OwnsSkill(SkillIds.Drill));
            Assert.AreEqual(1, s.PendingSalvage.PicksLeft);
            Assert.AreEqual(2, s.PendingSalvage.Parts.Count);

            int chips = s.ChipInventory.Count;
            Assert.IsTrue(s.TakeSalvage(1));
            Assert.AreEqual(chips + 1, s.ChipInventory.Count);
            Assert.IsNull(s.PendingSalvage, "alle Wahlen verbraucht");
            Assert.IsNotNull(s.PendingRuneOffer, "danach die gewohnte Belohnungswahl");
        }

        [Test]
        public void SalvageCanBeLeftAndIsSkippedWithoutLoot()
        {
            var combat = new LootCombat();
            combat.Loot.Add(new EnemyLoot(EnemyLootKind.Module, ModuleIds.Multicast, "Multicast"));
            OverworldSession s = Session(combat);
            s.Map.SetContent(East, CellContent.Enemy);
            s.TryStep(East);
            Assert.IsNotNull(s.PendingSalvage);
            Assert.IsTrue(s.SkipSalvage());
            Assert.IsNull(s.PendingSalvage);
            Assert.IsFalse(s.Modules.Owns(ModuleIds.Multicast));
            Assert.IsNotNull(s.PendingRuneOffer);

            OverworldSession plain = Session(new LootCombat());
            plain.Map.SetContent(East, CellContent.Enemy);
            plain.TryStep(East);
            Assert.IsNull(plain.PendingSalvage, "ohne Beute direkt zur Belohnung");
            Assert.IsNotNull(plain.PendingRuneOffer);
        }

        [Test]
        public void EnemyBoardLinesNameModulesAndChips()
        {
            EnemyEncounter e = Enumerable.Range(0, 40).Select(seed => Roll(5, CellContent.Elite, seed))
                .First(x => x.Fighters[0].Board.Rows.Any(r => r.Skill?.Modules.Count > 0));
            IReadOnlyList<string> lines = EnemyBoard.Lines(e.Fighters[0].Board);
            Assert.IsTrue(lines.Any(l => l.Contains("[+ ")), string.Join("\n", lines));
            Assert.IsTrue(lines.Any(l => l.StartsWith("Chips: ")), string.Join("\n", lines));
        }
    }
}
