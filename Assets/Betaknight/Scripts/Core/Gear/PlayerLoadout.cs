using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Baut aus Grundwerten, Ausrüstung und Runen-Zeilen den Spieler für einen Kampf.</summary>
    public static class PlayerLoadout
    {
        public static CombatantSetup CreateCombatant(string name, CombatStats baseStats, Equipment equipment,
            IEnumerable<BoardRowSpec> rows, int startHp = 0, BoardFactory boards = null, SetBonusRegistry sets = null,
            SkillLevelRules skillLevels = null)
        {
            equipment = equipment ?? new Equipment();
            boards = boards ?? BoardFactory.CreateDefault();
            sets = sets ?? SetBonusRegistry.CreateDefault();

            return new CombatantSetup
            {
                Name = name,
                Stats = equipment.ApplyTo(baseStats),
                StartHp = startHp,
                Board = boards.Create(rows, equipment, skillLevels),
                Modifiers = sets.CreateModifiers(equipment),
            };
        }
    }
}
