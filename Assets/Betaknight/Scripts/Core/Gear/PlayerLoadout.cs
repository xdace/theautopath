using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Baut aus Grundwerten, Ausrüstung (Werte, passive Effekte, Sets, Synergie-Tags) und Runen-Zeilen den Spieler für einen Kampf.</summary>
    public static class PlayerLoadout
    {
        public static CombatantSetup CreateCombatant(string name, CombatStats baseStats, Equipment equipment,
            IEnumerable<BoardRowSpec> rows, int startHp = 0, BoardFactory boards = null, SetBonusRegistry sets = null,
            SkillLevelRules skillLevels = null, SynergyRegistry synergies = null, ReliefCatalog reliefCatalog = null)
        {
            equipment = equipment ?? new Equipment();
            boards = boards ?? BoardFactory.CreateDefault();
            sets = sets ?? SetBonusRegistry.CreateDefault();
            synergies = synergies ?? SynergyRegistry.CreateDefault();

            List<BattleModifier> modifiers = sets.CreateModifiers(equipment);
            modifiers.AddRange(synergies.CreateModifiers(equipment));

            // Erleichterer aus getragenen Teilen und eingesetzten Modulen.
            var rowList = rows == null ? new List<BoardRowSpec>() : new List<BoardRowSpec>(rows);
            Dictionary<string, int> reliefs = (reliefCatalog ?? ReliefCatalog.Default).Collect(ReliefCarriers(equipment, rowList));
            var resources = new Dictionary<string, int>();
            if (reliefs.TryGetValue(ReliefIds.ChargeStart, out int charge))
                resources[ResourceIds.Charge] = System.Math.Min(SkillCatalog.ChargeMax, charge);
            if (reliefs.TryGetValue(ReliefIds.CritAfterBlock, out int crit)) modifiers.Add(new CritAfterBlockModifier(crit));

            return new CombatantSetup
            {
                Name = name,
                Stats = equipment.ApplyTo(baseStats),
                StartHp = startHp,
                Board = boards.Create(rowList, equipment, skillLevels, synergies.Passives(equipment)),
                Modifiers = modifiers,
                Resources = resources,
                Reliefs = reliefs,
            };
        }

        /// <summary>Ids aller Teile und Module, die Erleichterer tragen könnten.</summary>
        private static IEnumerable<string> ReliefCarriers(Equipment equipment, List<BoardRowSpec> rows)
        {
            foreach (EquipmentDefinition item in equipment.Items) yield return item.Id;
            foreach (BoardRowSpec row in rows)
            {
                foreach (Modules.ModuleSpec m in row.BlockModules) yield return m.ModuleId;
                foreach (Modules.ModuleSpec m in row.SkillModules) yield return m.ModuleId;
            }
        }
    }
}
