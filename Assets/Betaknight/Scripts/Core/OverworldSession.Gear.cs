using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;

namespace Betaknight.Core
{
    /// <summary>Ausrüstung: getragene Teile, Ausrüstung als Belohnung und die Zuordnung von Skills zu Runen-Zeilen.</summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Chance in Prozent, dass ein Sieg eine Ausrüstung statt der dritten Rune anbietet.</summary>
        public const int VictoryItemChance = 50;

        /// <summary>Teile eines Sets, von dem schon etwas getragen wird, kommen so viel häufiger in Angebote.</summary>
        public const int StartedSetWeightFactor = 3;

        public Equipment Gear { get; }
        public EquipmentCatalog Items { get; }
        public SetBonusRegistry Sets { get; } = SetBonusRegistry.CreateDefault();

        /// <summary>Skill-Daten für Anzeigen wie den Tafel-Editor.</summary>
        public SkillCatalog SkillCatalog { get; } = SkillCatalog.CreateDefault();

        /// <summary>Werte des Ritters zu Kampfbeginn (Waffe, Werte, aktive Set-Boni), wie der Kampf sie nutzt.</summary>
        public SkillUserStats SkillUserStats() =>
            (_combat as ArenaCombatResolver ?? new ArenaCombatResolver()).PreviewStats(Stats, Runes, Gear);

        /// <summary>Kennzahlen eines Skills mit der aktuellen Ausrüstung. Null bei unbekannter Id.</summary>
        public SkillInfo DescribeSkill(string skillId, SkillUserStats stats = null) =>
            SkillCatalog.TryGet(skillId, out SkillDefinition skill) ? SkillInfo.Create(skill, stats ?? SkillUserStats()) : null;

        /// <summary>Sets, von denen mindestens ein Teil getragen wird, mit Teilezahl.</summary>
        public List<(SetDefinition set, int pieces)> WornSets()
        {
            var result = new List<(SetDefinition, int)>();
            foreach (SetDefinition set in Sets.All)
            {
                int pieces = Gear.SetPieces(set.Id);
                if (pieces > 0) result.Add((set, pieces));
            }
            return result;
        }

        /// <summary>Set-exklusive Runen sind frei, solange das Set mit mindestens 2 Teilen getragen wird.</summary>
        public bool IsRuneUnlocked(RuneDefinition rune) =>
            rune != null && rune.UnlockSetId != null && Gear.SetPieces(rune.UnlockSetId) >= SetDefinition.FirstBonusPieces;

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

            List<EquipmentDefinition> removed = EquipItem(item);
            if (removed == null) return false;

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

        /// <summary>Angebotsgewicht: angefangene Sets werden bevorzugt, damit sie sich vervollständigen lassen.</summary>
        public int OfferWeight(EquipmentDefinition item)
        {
            if (item == null || item.Weight <= 0) return 0;
            bool started = item.SetId != null && Gear.SetPieces(item.SetId) > 0;
            return started ? item.Weight * StartedSetWeightFactor : item.Weight;
        }

        /// <summary>So viele Ausrüstungsteile liegen in jedem Shop.</summary>
        public const int ShopItemCount = 2;

        public bool CanBuyShopItem(int index) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.ItemIds.Count
            && Stats.Gold >= ShopPrices.Item
            && Items.TryGet(PendingShop.Inventory.ItemIds[index], out EquipmentDefinition item) && Gear.CanEquip(item, out _);

        /// <summary>Kauft ein Ausrüstungsteil und legt es sofort an. Abgelegte Teile gehen verloren.</summary>
        public bool BuyShopItem(int index)
        {
            if (!CanBuyShopItem(index)) return false;
            EquipmentDefinition item = Items.Get(PendingShop.Inventory.ItemIds[index]);
            List<EquipmentDefinition> removed = EquipItem(item);
            if (removed == null) return false;

            Stats.TrySpendGold(ShopPrices.Item);
            PendingShop.Inventory.RemoveItemAt(index);
            ItemTaken?.Invoke(item, removed);
            return true;
        }

        /// <summary>Legt an und gibt verwaisten Zeilen den ersten neuen Skill.</summary>
        private List<EquipmentDefinition> EquipItem(EquipmentDefinition item)
        {
            List<EquipmentDefinition> removed = Gear.Equip(item);
            if (removed == null) return null;
            if (item.SkillIds.Count > 0)
            {
                for (int i = 0; i < Runes.Rows.Count; i++)
                    if (!Gear.ProvidesSkill(Runes.Rows[i].SkillId)) Runes.AssignSkill(i, item.SkillIds[0]);
            }
            return removed;
        }

        private List<string> RollRewardItems(string source)
        {
            bool offerItem = source == "Schatztruhe" || ((source == "Sieg" || source == "Mine verteidigt") && _random.Next(100) < VictoryItemChance);
            return offerItem ? PickItems(1) : new List<string>();
        }

        /// <summary>Würfelt verschiedene, anlegbare Teile nach Angebotsgewicht.</summary>
        private List<string> PickItems(int count)
        {
            var result = new List<string>();
            var pool = new List<EquipmentDefinition>();
            int total = 0;
            foreach (EquipmentDefinition item in Items.All)
            {
                if (item.Weight <= 0 || Gear.Get(item.Slot) == item || !Gear.CanEquip(item, out _)) continue;
                pool.Add(item);
                total += OfferWeight(item);
            }
            while (result.Count < count && total > 0)
            {
                int roll = _random.Next(total);
                foreach (EquipmentDefinition item in pool)
                {
                    roll -= OfferWeight(item);
                    if (roll >= 0) continue;
                    result.Add(item.Id);
                    pool.Remove(item);
                    total -= OfferWeight(item);
                    break;
                }
            }
            return result;
        }
    }
}
