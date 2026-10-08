using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Die sieben Ausrüstungsplätze. Ein Teil pro Platz.</summary>
    public enum EquipmentSlot
    {
        Helmet,
        Gloves,
        Chest,
        Legs,
        Weapon,
        Shield,
        Boots,
    }

    /// <summary>Bekannte Set-Ids.</summary>
    public static class SetIds
    {
        public const string Overload = "overload";
        public const string Aegis = "aegis";
        public const string Scrap = "scrap";
        public const string Phantom = "phantom";
    }

    public static class EquipmentSlotExtensions
    {
        public static string DisplayName(this EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Helmet: return CatalogTexts.SlotHelmet;
                case EquipmentSlot.Gloves: return CatalogTexts.SlotGloves;
                case EquipmentSlot.Chest: return CatalogTexts.SlotChest;
                case EquipmentSlot.Legs: return CatalogTexts.SlotLegs;
                case EquipmentSlot.Weapon: return CatalogTexts.SlotWeapon;
                case EquipmentSlot.Shield: return CatalogTexts.SlotShield;
                case EquipmentSlot.Boots: return CatalogTexts.SlotBoots;
                default: return slot.ToString();
            }
        }
    }

    /// <summary>
    /// Ein Ausrüstungsteil: Platz, Wertebonus, passive Effekte auf Skill-Arten, Synergie-Tags und optional ein Set. Skills liefert es
    /// nicht mehr, die sind eigene Exemplare in der Skill-Sammlung. Die Waffe bestimmt Waffenschaden und Basisangriff.
    /// Reine Daten; neue Teile sind neue Katalog-Einträge.
    /// </summary>
    public sealed class EquipmentDefinition : IInventoryItem
    {
        private readonly Dictionary<StatKind, int> _stats;
        private readonly Dictionary<StatKind, int> _baseStats;

        public string Id { get; }

        /// <summary>Name ohne Stufe.</summary>
        public string BaseName { get; }

        /// <summary>Name mit Stufe, z. B. "Kurzklinge +2".</summary>
        public string Name => Level > 0 ? $"{BaseName} +{Level}" : BaseName;

        /// <summary>Stufe 0 bis +3. Ein doppeltes Teil wertet das vorhandene auf.</summary>
        public int Level { get; private set; }

        public InventoryItemKind ItemKind => InventoryItemKind.Equipment;
        public EquipmentSlot Slot { get; }
        public string Description { get; }
        /// <summary>Passive Effekte auf Skills bestimmter Arten, z. B. «Schock-Skills +20 % Wirkung».</summary>
        public IReadOnlyList<SkillPassive> Passives { get; }
        public IReadOnlyDictionary<StatKind, int> Stats => _stats;

        /// <summary>Synergie-Tags (1–2 Ids aus <see cref="SynergyTagIds"/>), gezählt über alle getragenen Teile.</summary>
        public IReadOnlyList<string> Tags { get; }

        /// <summary>Set-Zugehörigkeit oder null.</summary>
        public string SetId { get; }

        /// <summary>Zweihandwaffe: sperrt den Schild-Platz.</summary>
        public bool TwoHanded { get; }

        /// <summary>Gewicht für Angebote. 0 = nie angeboten (z. B. Startausrüstung).</summary>
        public int Weight { get; }

        public EquipmentDefinition(string id, string name, EquipmentSlot slot, IDictionary<StatKind, int> stats = null,
            IEnumerable<SkillPassive> passives = null, string setId = null, bool twoHanded = false, int weight = 10,
            string description = null, IEnumerable<string> tags = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (twoHanded && slot != EquipmentSlot.Weapon) throw new ArgumentException("Nur Waffen sind zweihändig.", nameof(twoHanded));

            Id = id;
            BaseName = name ?? id;
            Slot = slot;
            _stats = stats != null ? new Dictionary<StatKind, int>(stats) : new Dictionary<StatKind, int>();
            _baseStats = new Dictionary<StatKind, int>(_stats);
            Passives = new List<SkillPassive>(passives ?? Array.Empty<SkillPassive>());
            SetId = setId;
            TwoHanded = twoHanded;
            Weight = Math.Max(0, weight);
            Description = description ?? string.Empty;
            Tags = new List<string>(tags ?? Array.Empty<string>());
        }

        public int StatBonus(StatKind kind) => _stats.TryGetValue(kind, out int v) ? v : 0;

        /// <summary>Werte auf Stufe 0.</summary>
        public IReadOnlyDictionary<StatKind, int> BaseStats => _baseStats;

        /// <summary>
        /// Dasselbe Teil auf einer anderen Stufe. Jede Stufe gibt <paramref name="statPercentPerLevel"/> % der Grundwerte dazu,
        /// aber nur bei vorteilhaften Werten (mindestens 1); Nachteile wie langsameres Tempo wachsen nicht mit.
        /// </summary>
        public EquipmentDefinition AtLevel(int level, int statPercentPerLevel)
        {
            level = Math.Max(0, level);
            var stats = new Dictionary<StatKind, int>();
            foreach (KeyValuePair<StatKind, int> stat in _baseStats)
            {
                bool good = stat.Key == StatKind.AttackInterval ? stat.Value < 0 : stat.Value > 0;
                int bonus = 0;
                if (good && level > 0)
                {
                    int per = Math.Max(1, Math.Abs(stat.Value) * statPercentPerLevel / 100);
                    bonus = per * level * Math.Sign(stat.Value);
                }
                stats[stat.Key] = stat.Value + bonus;
            }

            var copy = new EquipmentDefinition(Id, BaseName, Slot, stats, Passives, SetId, TwoHanded, Weight, Description, Tags);
            copy._baseStats.Clear();
            foreach (KeyValuePair<StatKind, int> stat in _baseStats) copy._baseStats[stat.Key] = stat.Value;
            copy.Level = level;
            return copy;
        }

        public override string ToString() => $"{Name} ({Slot.DisplayName()})";
    }
}
