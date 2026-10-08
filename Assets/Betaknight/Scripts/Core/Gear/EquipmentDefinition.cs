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
                case EquipmentSlot.Helmet: return "Helm";
                case EquipmentSlot.Gloves: return "Handschuhe";
                case EquipmentSlot.Chest: return "Brust";
                case EquipmentSlot.Legs: return "Beinschienen";
                case EquipmentSlot.Weapon: return "Waffe";
                case EquipmentSlot.Shield: return "Schild";
                case EquipmentSlot.Boots: return "Stiefel";
                default: return slot.ToString();
            }
        }
    }

    /// <summary>
    /// Ein Ausrüstungsteil: Platz, Wertebonus, Skills (das "Was" für die Logik-Tafel) und optional ein Set.
    /// Reine Daten; neue Teile sind neue Katalog-Einträge.
    /// </summary>
    public sealed class EquipmentDefinition
    {
        private readonly Dictionary<StatKind, int> _stats;

        public string Id { get; }
        public string Name { get; }
        public EquipmentSlot Slot { get; }
        public string Description { get; }
        public IReadOnlyList<string> SkillIds { get; }
        public IReadOnlyDictionary<StatKind, int> Stats => _stats;

        /// <summary>Set-Zugehörigkeit oder null.</summary>
        public string SetId { get; }

        /// <summary>Zweihandwaffe: sperrt den Schild-Platz.</summary>
        public bool TwoHanded { get; }

        /// <summary>Gewicht für Angebote. 0 = nie angeboten (z. B. Startausrüstung).</summary>
        public int Weight { get; }

        public EquipmentDefinition(string id, string name, EquipmentSlot slot, IDictionary<StatKind, int> stats = null,
            IEnumerable<string> skillIds = null, string setId = null, bool twoHanded = false, int weight = 10,
            string description = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (twoHanded && slot != EquipmentSlot.Weapon) throw new ArgumentException("Nur Waffen sind zweihändig.", nameof(twoHanded));

            Id = id;
            Name = name ?? id;
            Slot = slot;
            _stats = stats != null ? new Dictionary<StatKind, int>(stats) : new Dictionary<StatKind, int>();
            SkillIds = new List<string>(skillIds ?? Array.Empty<string>());
            SetId = setId;
            TwoHanded = twoHanded;
            Weight = Math.Max(0, weight);
            Description = description ?? string.Empty;
        }

        public int StatBonus(StatKind kind) => _stats.TryGetValue(kind, out int v) ? v : 0;

        public override string ToString() => $"{Name} ({Slot.DisplayName()})";
    }
}
