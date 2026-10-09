using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Movement;
using Betaknight.Core.Exploration;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using Betaknight.Core.Turns;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Verwerfen im Build-Fenster: Skills, Module, Relais und Chips verschwinden endgültig.</summary>
    public class DiscardTests
    {
        private static OverworldSession Session()
        {
            var map = new HexMap(HexCoord.Zero, 4, 11);
            foreach (HexCoord c in HexCoord.Spiral(HexCoord.Zero, 4)) map.AddCell(new HexCell(c, CellContent.Empty));
            return new OverworldSession(map, new PlayerModel(HexCoord.Zero), new TurnSystem(), new ExplorationService(map),
                new PlayerStats(30, 50), null, null, null, null, null, null);
        }

        [Test]
        public void ASkillOnTheBoardIsTakenOffAndDiscardedAndItsModulesBecomeFree()
        {
            OverworldSession s = Session();
            s.Board.AddRelay(s.RuneCatalog.Get("on_hit"), new Cell(0, 0));
            SkillInstance stab = s.Skills.Add(SkillIds.ShockStab);
            Assert.IsTrue(s.PlaceSkill(stab.InstanceId, new Cell(1, 0)));
            ModuleInstance multicast = s.GainModule(ModuleIds.Multicast);
            Assert.IsTrue(s.PlaceModuleOnSkill(multicast.InstanceId, stab.InstanceId));

            Assert.IsTrue(s.DiscardSkill(stab.InstanceId));
            Assert.IsNull(s.Skills.Get(stab.InstanceId));
            Assert.IsEmpty(s.Board.Components);
            Assert.IsTrue(multicast.IsFree, "das Modul bleibt und ist frei");
        }

        [Test]
        public void ModulesRelaysAndChipsCanBeDiscarded()
        {
            OverworldSession s = Session();
            ModuleInstance m = s.GainModule(ModuleIds.Extend);
            s.Board.AddRelay(s.RuneCatalog.Get("on_hit"), new Cell(0, 0));
            Assert.IsTrue(s.PlaceModuleOnRelay(m.InstanceId, 0));

            Assert.IsTrue(s.DiscardModule(m.InstanceId));
            Assert.IsNull(s.Modules.Get(m.InstanceId));
            Assert.IsEmpty(s.Board.Relays[0].Modules);

            Assert.IsTrue(s.DiscardRelay(0));
            Assert.IsEmpty(s.Board.Relays);
            Assert.AreEqual(0, s.RuneInventory.Count, "die Rune kommt nicht ins Inventar");

            s.GrantChip(ChipIds.And);
            s.GrantChip(ChipIds.Fuse);
            Assert.IsTrue(s.PlaceChip(1, new Cell(2, 2)));
            Assert.IsTrue(s.DiscardChip(0));
            Assert.IsEmpty(s.ChipInventory);
            Assert.IsTrue(s.DiscardBoardChip(0));
            Assert.IsEmpty(s.Board.Chips);
            Assert.IsEmpty(s.ChipInventory, "verworfen, nicht zurück ins Inventar");
        }
    }
}
