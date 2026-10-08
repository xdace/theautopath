using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;

namespace Betaknight.Core
{
    /// <summary>Ausrüstung: getragene Teile, Ausrüstung als Belohnung und die Zuordnung von Skills zu Runen-Zeilen.</summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Chance in Prozent, dass ein Sieg eine Ausrüstung statt der dritten Rune anbietet.</summary>
        public const int VictoryItemChance = 50;

        public Equipment Gear { get; }
        public EquipmentCatalog Items { get; }

        /// <summary>Ein Teil wurde aus einem Angebot angelegt; abgelegte Teile gehen verloren.</summary>
        public event Action<EquipmentDefinition, IReadOnlyList<EquipmentDefinition>> ItemTaken;

        /// <summary>
        /// Nimmt ein Ausrüstungsteil aus dem wartenden Angebot und legt es an. Verwaiste Zeilen bekommen
        /// den ersten neuen Skill. False, wenn das Teil nicht angelegt werden kann (z. B. Schild bei Zweihand).
        /// </summary>
        public bool TakeItem(int itemIndex)
        {
            RuneOffer offer = PendingRuneOffer;
            if (offer == null || itemIndex < 0 || itemIndex >= offer.ItemIds.Count) return false;
            if (!Items.TryGet(offer.ItemIds[itemIndex], out EquipmentDefinition item)) return false;

            List<EquipmentDefinition> removed = Gear.Equip(item);
            if (removed == null) return false;

            if (item.SkillIds.Count > 0)
            {
                for (int i = 0; i < Runes.Rows.Count; i++)
                    if (!Gear.ProvidesSkill(Runes.Rows[i].SkillId)) Runes.AssignSkill(i, item.SkillIds[0]);
            }

            PendingRuneOffer = null;
            ItemTaken?.Invoke(item, removed);
            CheckShards();
            return true;
        }

        public bool CanTakeItem(int itemIndex) =>
            PendingRuneOffer != null && itemIndex >= 0 && itemIndex < PendingRuneOffer.ItemIds.Count
            && Items.TryGet(PendingRuneOffer.ItemIds[itemIndex], out EquipmentDefinition item) && Gear.CanEquip(item, out _);

        /// <summary>Ordnet einer Zeile einen Skill zu. Erlaubt sind getragene Skills, der Basisangriff oder null (leer).</summary>
        public bool AssignSkill(int row, string skillId)
        {
            if (skillId != null && !Gear.ProvidesSkill(skillId)) return false;
            return Runes.AssignSkill(row, skillId);
        }

        /// <summary>Verschiebt eine Zeile der Logik-Tafel (Priorität).</summary>
        public bool MoveRow(int from, int to) => Runes.Move(from, to);

        /// <summary>Skill für eine neue Zeile: der erste getragene Skill, den noch keine Zeile nutzt, sonst der erste überhaupt.</summary>
        private string DefaultSkillForNewRow()
        {
            IReadOnlyList<string> skills = Gear.SkillIds;
            if (skills.Count == 0) return SkillDefinition.BasicAttackId;

            foreach (string id in skills)
            {
                bool used = false;
                foreach (RuneSlot row in Runes.Rows) if (row.SkillId == id) used = true;
                if (!used) return id;
            }
            return skills[0];
        }

        private List<string> RollRewardItems(string source)
        {
            bool offerItem = source == "Schatztruhe" || (source == "Sieg" && _random.Next(100) < VictoryItemChance);
            var result = new List<string>();
            if (!offerItem) return result;

            var pool = new List<EquipmentDefinition>();
            int total = 0;
            foreach (EquipmentDefinition item in Items.All)
            {
                if (item.Weight <= 0 || Gear.Get(item.Slot) == item || !Gear.CanEquip(item, out _)) continue;
                pool.Add(item);
                total += item.Weight;
            }
            if (total == 0) return result;

            int roll = _random.Next(total);
            foreach (EquipmentDefinition item in pool)
            {
                roll -= item.Weight;
                if (roll >= 0) continue;
                result.Add(item.Id);
                break;
            }
            return result;
        }
    }
}
