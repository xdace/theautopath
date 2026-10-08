using System.Collections.Generic;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;

namespace Betaknight.Core
{
    /// <summary>
    /// Stat-Leiste der Fenster «Build» und «Inventar»: aktuelle Werte und die Vorschau, was ein Teil beim Anlegen
    /// oder Ablegen ändern würde. Rechnet mit einer Kopie der Ausrüstung; der echte Stand bleibt unberührt.
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Werte zu Kampfbeginn mit der getragenen Ausrüstung.</summary>
        public BuildStats StatsNow() => BuildStatsFor(Gear);

        /// <summary>Werte, wenn dieses Teil angelegt wäre (verdrängte Teile und bei Zweihand der Schild fallen weg).</summary>
        public BuildStats StatsWith(EquipmentDefinition item)
        {
            Equipment copy = CopyGear();
            if (item != null && copy.CanEquip(item, out _)) copy.Equip(item);
            return BuildStatsFor(copy);
        }

        /// <summary>Werte, wenn das Teil dieses Platzes abgelegt wäre.</summary>
        public BuildStats StatsWithout(EquipmentSlot slot)
        {
            Equipment copy = CopyGear();
            copy.Unequip(slot);
            return BuildStatsFor(copy);
        }

        /// <summary>Vorschau für ein Teil: «Rüstung 6 → 9» (grün/rot über <see cref="StatChange.Sign"/>).</summary>
        public List<StatChange> PreviewEquip(EquipmentDefinition item) => BuildStats.Compare(StatsNow(), StatsWith(item));

        public List<StatChange> PreviewUnequip(EquipmentSlot slot) => BuildStats.Compare(StatsNow(), StatsWithout(slot));

        private Equipment CopyGear()
        {
            var copy = new Equipment();
            foreach (EquipmentDefinition item in Gear.Items) copy.Equip(item);
            return copy;
        }

        private BuildStats BuildStatsFor(Equipment equipment)
        {
            var resolver = _combat as ArenaCombatResolver ?? new ArenaCombatResolver();
            return BuildStats.From(resolver.PreviewCombatant(Stats, Runes, equipment, null, Progression.SkillLevels), Stats.Hp,
                BonusesFor(equipment));
        }

        /// <summary>Aktive Set-Boni (ab 2 Teilen), erreichte Tag-Stufen und aktive Duos.</summary>
        private List<string> BonusesFor(Equipment equipment)
        {
            var bonuses = new List<string>();
            foreach (SetDefinition set in Sets.All)
            {
                int pieces = equipment.SetPieces(set.Id);
                if (pieces >= SetDefinition.FirstBonusPieces) bonuses.Add($"{set.Name} {pieces}/{set.MaxPieces}");
            }
            foreach (SynergyCounter c in Synergies.Counters(equipment))
                if (c.Reached > 0) bonuses.Add($"{c.Tag.Name} {c.Reached}");
            foreach (SynergyDuo duo in Synergies.ActiveDuos(equipment)) bonuses.Add($"Duo {DuoName(duo)}");
            return bonuses;
        }
    }
}
