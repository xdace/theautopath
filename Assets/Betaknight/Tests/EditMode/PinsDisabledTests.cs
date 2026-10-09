using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Pins sind vorübergehend aus dem Spiel: keine Pins, keine Pulse zwischen Komponenten, keine Leiterbahn-Chips im Angebot.</summary>
    public class PinsDisabledTests
    {
        [Test]
        public void PinsAreOffByDefault()
        {
            Assert.IsFalse(PinConfig.Default.Enabled);
            Assert.IsEmpty(PinCatalog.CreateDefault().For(SkillIds.ShieldBash));

            var spec = new CircuitSpec { Width = 6, Height = 6 };
            spec.Components.Add(new ComponentSpec(SkillIds.ShockStab, new Cell(0, 0)));
            spec.Components.Add(new ComponentSpec(SkillIds.ShockStab, new Cell(1, 0)));
            var board = BoardFactory.CreateDefault().Create(spec, null);
            Assert.IsTrue(board.Rows.All(r => r.Pins.Count == 0));
            Assert.IsEmpty(board.Links);
        }

        [Test]
        public void ChipsThatOnlyConductAreNotOffered()
        {
            ChipCatalog chips = ChipCatalog.CreateDefault();
            var random = new System.Random(1);
            for (int i = 0; i < 500; i++) Assert.IsFalse(chips.Roll(random).Conducts);
        }
    }
}
