using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Gear
{
    public static class RuneLoadoutBoard
    {
        /// <summary>Die Zeilen der Runen-Plätze als Bauplan für die Logik-Tafel.</summary>
        public static List<BoardRowSpec> ToBoardSpecs(this RuneLoadout loadout)
        {
            var specs = new List<BoardRowSpec>();
            if (loadout == null) return specs;
            foreach (RuneSlot row in loadout.Rows) specs.Add(new BoardRowSpec(row.Rune.Id, row.SkillId, row.Level, row.SkillLevel));
            return specs;
        }
    }
}
