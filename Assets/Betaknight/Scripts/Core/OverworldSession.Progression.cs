using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;

namespace Betaknight.Core
{
    /// <summary>Bekannte Quellen für Belohnungsangebote.</summary>
    public static class RewardSources
    {
        public const string Victory = "Victory";
        public const string Elite = "Elite Victory";
        public const string MineDefended = "Mine Defended";
        public const string Treasure = "Treasure Chest";
        public const string Shards = "Rune Shards";

        /// <summary>Belohnungen nach Kämpfen: sie bieten immer mindestens eine Verbesserung des Builds an.</summary>
        public static bool IsFight(string source) => source == Victory || source == Elite || source == MineDefended;
    }

    /// <summary>
    /// Aufbauen statt austauschen: Tafel-Erweiterungen, Stufen für doppelte Runen und Teile,
    /// Angebote mit mindestens einer Verbesserung und Meldungen, was besser geworden ist.
    /// Alle Werte in <see cref="ProgressionConfig"/>.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public ProgressionConfig Progression { get; private set; } = new ProgressionConfig();

        /// <summary>Etwas am Build ist besser geworden, z. B. «Bohrstoß 120 % → 135 %» oder «Tafel 4 → 5 Zeilen».</summary>
        public event Action<string> BuildImproved;

        /// <summary>Ersetzt die Fortschritts-Konfiguration (z. B. für Tests oder Balance-Einstellungen).</summary>
        public void UseProgression(ProgressionConfig config) => Progression = config ?? new ProgressionConfig();

        // ------------------------------------------------------------------ Tafel-Erweiterung

        public bool CanExpandBoard => Runes.Slots < Progression.MaxBoardRows;

        /// <summary>Fügt bis zur Obergrenze Zeilen hinzu. True, wenn mindestens eine dazukam.</summary>
        public bool ExpandBoard(int rows)
        {
            int before = Runes.Slots;
            for (int i = 0; i < rows && CanExpandBoard; i++) Runes.AddSlot();
            if (Runes.Slots == before) return false;
            BuildImproved?.Invoke(SessionTexts.BoardGrown(before, Runes.Slots));
            return true;
        }

        /// <summary>Nimmt «Tafel-Erweiterung: +1 Zeile» aus dem wartenden Angebot.</summary>
        public bool TakeBoardExpansion()
        {
            if (PendingRuneOffer == null || !PendingRuneOffer.BoardExpansion || !CanExpandBoard) return false;
            PendingRuneOffer = null;
            ExpandBoard(1);
            CheckShards();
            return true;
        }

        // ------------------------------------------------------------------ Stufen

        /// <summary>Besitzt der Spieler dieses Teil schon (angelegt oder im Inventar) unterhalb der höchsten Stufe?</summary>
        public bool CanUpgradeItem(string itemId)
        {
            EquipmentDefinition owned = OwnedItem(itemId);
            return owned != null && owned.Level < Progression.MaxItemLevel;
        }

        /// <summary>Liegt diese Rune schon auf der Tafel oder im Inventar und kann noch steigen?</summary>
        public bool CanUpgradeRune(RuneDefinition rune)
        {
            int row = Runes.IndexOf(rune);
            if (row >= 0) return Runes.Rows[row].CanUpgrade;
            int stored = RuneInventory.IndexOf(rune);
            return stored >= 0 && RuneInventory[stored].Level < rune.MaxLevel;
        }

        public bool OwnsRune(RuneDefinition rune) => Runes.Contains(rune) || RuneInventory.Contains(rune);

        private EquipmentDefinition OwnedItem(string itemId)
        {
            if (!Items.TryGet(itemId, out EquipmentDefinition definition)) return null;
            EquipmentDefinition worn = Gear.Get(definition.Slot);
            if (worn != null && worn.Id == itemId) return worn;
            int index = Inventory.IndexOf(itemId);
            return index >= 0 ? Inventory[index] : null;
        }

        /// <summary>Ein doppeltes Teil wertet das vorhandene eine Stufe auf (Werte und Skill-Stärke).</summary>
        private bool UpgradeItem(string itemId)
        {
            if (!CanUpgradeItem(itemId)) return false;
            EquipmentDefinition old = OwnedItem(itemId);
            EquipmentDefinition upgraded = old.AtLevel(old.Level + 1, Progression.ItemStatPercentPerLevel);

            bool worn = Gear.Get(old.Slot) == old;

            if (worn) Gear.Equip(upgraded);
            else Inventory.ReplaceAt(Inventory.IndexOf(itemId), upgraded);

            var parts = new List<string> { SessionTexts.Upgrade(old.Name, upgraded.Name) };
            foreach (KeyValuePair<StatKind, int> stat in upgraded.Stats)
            {
                int before = old.StatBonus(stat.Key);
                if (before != stat.Value) parts.Add($"{SkillInfo.StatName(stat.Key)} {StatText(stat.Key, before)} → {StatText(stat.Key, stat.Value)}");
            }
            BuildImproved?.Invoke(string.Join(", ", parts));
            return true;
        }

        private static string StatText(StatKind kind, int value) =>
            SkillInfo.IsPercentStat(kind) ? SkillInfo.Percent(value) : value.ToString();

        /// <summary>«Bohrstoß 120 % → 135 %»: erste Schadens- oder Heilwirkung vorher und nachher.</summary>
        internal static string PowerChange(SkillInfo before, SkillInfo after)
        {
            if (before == null || after == null) return null;
            for (int i = 0; i < before.Effects.Count && i < after.Effects.Count; i++)
            {
                EffectInfo a = before.Effects[i];
                EffectInfo b = after.Effects[i];
                if (a.IsDamage && a.DamageBp != b.DamageBp)
                    return $"{after.Skill.Name} {SkillInfo.Percent(a.DamageBp)} → {SkillInfo.Percent(b.DamageBp)}";
                if (a.Kind == EffectInfoKind.Heal && a.Amount != b.Amount)
                    return SessionTexts.HealChange(after.Skill.Name, a.Amount, b.Amount);
            }
            return null;
        }

        /// <summary>Eine doppelte Rune hebt die vorhandene eine Stufe an (wie das Lagerfeuer).</summary>
        private bool UpgradeOwnedRune(RuneDefinition rune)
        {
            if (!CanUpgradeRune(rune)) return false;
            int row = Runes.IndexOf(rune);
            string before;
            string after;
            if (row >= 0)
            {
                before = Runes.Rows[row].Name;
                Runes.Upgrade(row);
                after = Runes.Rows[row].Name;
            }
            else
            {
                int index = RuneInventory.IndexOf(rune);
                before = RuneInventory[index].Name;
                RuneInventory.Upgrade(index);
                after = RuneInventory[index].Name;
            }
            BuildImproved?.Invoke(SessionTexts.RuneLevelUp(before, after));
            return true;
        }

        // ------------------------------------------------------------------ Angebote

        /// <summary>Verbessert diese Rune den aktuellen Build (Stufe für eine vorhandene oder passender Tag)?</summary>
        public bool IsImprovement(RuneDefinition rune) =>
            rune != null && (OwnsRune(rune) ? CanUpgradeRune(rune) : Runes.HasTag(rune.Tag));

        /// <summary>Verbessert dieses Teil den Build (Stufe für ein vorhandenes Teil oder fehlendes Set-Teil)?</summary>
        public bool IsImprovement(EquipmentDefinition item)
        {
            if (item == null) return false;
            if (OwnedItem(item.Id) != null) return CanUpgradeItem(item.Id);
            return item.SetId != null && Gear.SetPieces(item.SetId) > 0;
        }

        /// <summary>
        /// Ergänzt ein Angebot nach Kämpfen so, dass mindestens eine Option den Build verbessert, und hängt bei Elite
        /// und seltenen Truhen die Tafel-Erweiterung an.
        /// </summary>
        private RuneOffer ShapeOffer(RuneOffer offer)
        {
            string source = offer.Source;
            if (RewardSources.IsFight(source) && !HasImprovement(offer)) offer = AddImprovement(offer);

            int expansionChance = source == RewardSources.Elite ? Progression.EliteBoardExpansionChance
                : source == RewardSources.Treasure ? Progression.TreasureBoardExpansionChance : 0;
            if (expansionChance > 0 && CanExpandBoard && _random.Next(100) < expansionChance) offer = offer.WithBoardExpansion();
            return offer;
        }

        private bool HasImprovement(RuneOffer offer)
        {
            foreach (RuneDefinition r in offer.Options) if (IsImprovement(r)) return true;
            foreach (string id in offer.ItemIds) if (Items.TryGet(id, out EquipmentDefinition item) && IsImprovement(item)) return true;
            foreach (string id in offer.SkillIds) if (IsImprovementSkill(id)) return true;
            return false;
        }

        /// <summary>
        /// Ersetzt die letzte Option durch eine Verbesserung: zuerst eine Stufe für ein getragenes Teil, dann ein fehlendes
        /// Set-Teil, dann eine Stufe für einen eigenen Skill, dann eine Stufe für eine vorhandene Rune, zuletzt eine Rune
        /// zu einem vorhandenen Tag.
        /// </summary>
        private RuneOffer AddImprovement(RuneOffer offer)
        {
            var runes = offer.Options.ToList();
            var items = offer.ItemIds.ToList();

            string item = PickImprovementItem();
            if (item != null)
            {
                if (items.Count > 0) items[items.Count - 1] = item;
                else if (runes.Count > 1) { runes.RemoveAt(runes.Count - 1); items.Add(item); }
                else items.Add(item);
                return offer.With(runes, items);
            }

            var skills = offer.SkillIds.ToList();
            string skill = PickImprovementSkill(skills);
            if (skill != null)
            {
                if (skills.Count > 0) skills[skills.Count - 1] = skill;
                else if (runes.Count > 1) { runes.RemoveAt(runes.Count - 1); skills.Add(skill); }
                else skills.Add(skill);
                return offer.With(runes, items, skills);
            }

            RuneDefinition rune = PickImprovementRune(runes);
            if (rune == null) return offer;
            if (runes.Count > 0) runes[runes.Count - 1] = rune;
            else runes.Add(rune);
            return offer.With(runes, items);
        }

        private string PickImprovementItem()
        {
            var wornUpgrades = Gear.Items.Where(i => i.Level < Progression.MaxItemLevel && Items.TryGet(i.Id, out _))
                .Select(i => i.Id).ToList();
            if (wornUpgrades.Count > 0) return wornUpgrades[_random.Next(wornUpgrades.Count)];

            var setPieces = Items.All.Where(i => i.Weight > 0 && i.SetId != null && Gear.SetPieces(i.SetId) > 0 && OwnedItem(i.Id) == null)
                .Select(i => i.Id).ToList();
            return setPieces.Count > 0 ? setPieces[_random.Next(setPieces.Count)] : null;
        }

        private RuneDefinition PickImprovementRune(List<RuneDefinition> already)
        {
            var upgrades = Runes.Rows.Where(r => r.CanUpgrade).Select(r => r.Rune)
                .Concat(RuneInventory.Runes.Where(r => r.Level < r.Rune.MaxLevel).Select(r => r.Rune))
                .Where(r => !already.Contains(r)).ToList();
            if (upgrades.Count > 0) return upgrades[_random.Next(upgrades.Count)];

            var matching = RuneCatalog.All.Where(r => r.Weight > 0 && !r.IsExclusive && !OwnsRune(r) && Runes.HasTag(r.Tag) && !already.Contains(r)).ToList();
            return matching.Count > 0 ? matching[_random.Next(matching.Count)] : null;
        }
    }
}
