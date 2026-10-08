using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>Ausrüstung: getragene Teile (Werte und passive Skill-Boni) und Ausrüstung als Belohnung.</summary>
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
            (_combat as ArenaCombatResolver ?? new ArenaCombatResolver()).PreviewStats(Stats, Runes, Gear, null, Progression.SkillLevels);

        /// <summary>
        /// Skill auf einer Stufe mit den passiven Boni der getragenen Ausrüstung und der Tag-Stufen, so wie er im Kampf wirkt.
        /// Ohne <paramref name="level"/> gilt die höchste Stufe der eigenen Exemplare. Null bei unbekannter Id.
        /// </summary>
        public SkillDefinition LeveledSkill(string skillId, int level = -1)
        {
            if (!SkillCatalog.TryGet(skillId, out SkillDefinition skill)) return null;
            if (level < 0) level = HighestSkillLevel(skillId);
            return Gear.Boost(skill.AtLevel(level, Progression.SkillLevels), Synergies.Passives(Gear));
        }

        /// <summary>Kennzahlen eines Skills mit der aktuellen Ausrüstung. Ohne Stufe: höchste eigene Stufe. Null bei unbekannter Id.</summary>
        public SkillInfo DescribeSkill(string skillId, SkillUserStats stats = null, int level = -1)
        {
            SkillDefinition skill = LeveledSkill(skillId, level);
            return skill != null ? SkillInfo.Create(skill, stats ?? SkillUserStats()) : null;
        }

        /// <summary>Kennzahlen eines Skill-Exemplars (seine Stufe, passive Boni der Ausrüstung).</summary>
        public SkillInfo DescribeSkill(SkillInstance skill, SkillUserStats stats = null) =>
            skill != null ? DescribeSkill(skill.SkillId, stats, skill.Level) : null;

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

        /// <summary>
        /// Ein Teil wurde genommen. Zweiter Wert: Teile, die dabei abgelegt wurden (sie liegen jetzt im Inventar).
        /// Ist das Teil selbst ins Inventar gegangen, ist es nicht angelegt.
        /// </summary>
        public event Action<EquipmentDefinition, IReadOnlyList<EquipmentDefinition>> ItemTaken;

        /// <summary>
        /// Nimmt ein Ausrüstungsteil aus dem wartenden Angebot. <see cref="ItemPlacement.Auto"/> legt es an, wenn
        /// sein Platz frei ist, sonst kommt es ins Inventar. Abgelegte Teile wandern ins Inventar.
        /// </summary>
        public bool TakeItem(int itemIndex, ItemPlacement placement = ItemPlacement.Auto)
        {
            if (!CanTakeItem(itemIndex, placement)) return false;
            EquipmentDefinition item = Items.Get(PendingRuneOffer.ItemIds[itemIndex]);

            PendingRuneOffer = null;
            PlaceNewItem(item, placement);
            CheckShards();
            return true;
        }

        public bool CanTakeItem(int itemIndex, ItemPlacement placement = ItemPlacement.Auto) =>
            PendingRuneOffer != null && itemIndex >= 0 && itemIndex < PendingRuneOffer.ItemIds.Count
            && Items.TryGet(PendingRuneOffer.ItemIds[itemIndex], out EquipmentDefinition item)
            && (placement != ItemPlacement.Equip || Gear.CanEquip(item, out _) || CanUpgradeItem(item.Id));

        /// <summary>Verschiebt eine Zeile der Logik-Tafel (Priorität).</summary>
        public bool MoveRow(int from, int to) => Runes.Move(from, to);

        /// <summary>Angebotsgewicht: angefangene Sets werden bevorzugt, damit sie sich vervollständigen lassen.</summary>
        public int OfferWeight(EquipmentDefinition item)
        {
            if (item == null || item.Weight <= 0) return 0;
            bool started = item.SetId != null && Gear.SetPieces(item.SetId) > 0;
            return started ? item.Weight * StartedSetWeightFactor : item.Weight;
        }

        /// <summary>So viele Ausrüstungsteile liegen in jedem Shop.</summary>
        public const int ShopItemCount = 2;

        public bool CanBuyShopItem(int index, ItemPlacement placement = ItemPlacement.Auto) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.ItemIds.Count
            && Stats.Gold >= ShopPrices.Item
            && Items.TryGet(PendingShop.Inventory.ItemIds[index], out EquipmentDefinition item)
            && (placement != ItemPlacement.Equip || Gear.CanEquip(item, out _));

        /// <summary>Kauft ein Ausrüstungsteil: anlegen oder ins Inventar wie bei <see cref="TakeItem"/>.</summary>
        public bool BuyShopItem(int index, ItemPlacement placement = ItemPlacement.Auto)
        {
            if (!CanBuyShopItem(index, placement)) return false;
            EquipmentDefinition item = Items.Get(PendingShop.Inventory.ItemIds[index]);

            Stats.TrySpendGold(ShopPrices.Item);
            PendingShop.Inventory.RemoveItemAt(index);
            PlaceNewItem(item, placement);
            return true;
        }

        /// <summary>Legt an. Abgelegte Teile kommen zurück. Skills an der Tafel bleiben, wo sie sind.</summary>
        private List<EquipmentDefinition> EquipItem(EquipmentDefinition item) => Gear.Equip(item);

        private List<string> RollRewardItems(string source)
        {
            bool offerItem = source == RewardSources.Treasure
                || (RewardSources.IsFight(source) && _random.Next(100) < VictoryItemChance);
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
                // Schon getragene oder gelagerte Teile nicht noch einmal; Schilde trotz Zweihand gehen ins Inventar.
                if (item.Weight <= 0 || Gear.Get(item.Slot)?.Id == item.Id || Inventory.Contains(item.Id)) continue;
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
