using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Baut aus Grundwerten, Ausrüstung (Werte, passive Effekte, Sets, Synergie-Tags) und Runen-Zeilen den Spieler für einen Kampf.</summary>
    public static class PlayerLoadout
    {
        public static CombatantSetup CreateCombatant(string name, CombatStats baseStats, Equipment equipment,
            IEnumerable<BoardRowSpec> rows, int startHp = 0, BoardFactory boards = null, SetBonusRegistry sets = null,
            SkillLevelRules skillLevels = null, SynergyRegistry synergies = null)
        {
            equipment = equipment ?? new Equipment();
            boards = boards ?? BoardFactory.CreateDefault();
            sets = sets ?? SetBonusRegistry.CreateDefault();
            synergies = synergies ?? SynergyRegistry.CreateDefault();

            List<BattleModifier> modifiers = sets.CreateModifiers(equipment);
            modifiers.AddRange(synergies.CreateModifiers(equipment));

            return new CombatantSetup
            {
                Name = name,
                Stats = equipment.ApplyTo(baseStats),
                StartHp = startHp,
                Board = boards.Create(rows, equipment, skillLevels, synergies.Passives(equipment)),
                Modifiers = modifiers,
            };
        }
    }
}
