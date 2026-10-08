using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;

namespace Betaknight.Overworld.UI
{
    /// <summary>Kurztexte für Ausrüstung in Fenstern: Skills, Werte, Besonderheiten.</summary>
    public static class ItemText
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        public static string Describe(EquipmentDefinition item)
        {
            var parts = new List<string>();
            foreach (string id in item.SkillIds)
                parts.Add(Skills.TryGet(id, out SkillDefinition skill) ? $"Skill: {skill.Name}" : id);
            foreach (KeyValuePair<StatKind, int> stat in item.Stats)
                if (stat.Value != 0) parts.Add(Stat(stat.Key, stat.Value));
            if (item.TwoHanded) parts.Add("zweihändig, sperrt Schild");
            return parts.Count > 0 ? string.Join(", ", parts) : item.Description;
        }

        public static string Stat(StatKind kind, int value)
        {
            string sign = value > 0 ? "+" : "−";
            int abs = value < 0 ? -value : value;
            switch (kind)
            {
                case StatKind.MaxHp: return $"{sign}{abs} Max-HP";
                case StatKind.Damage: return $"{sign}{abs} Schaden";
                case StatKind.Armor: return $"{sign}{abs} Rüstung";
                // Weniger Ticks zwischen Angriffen = schneller, daher umgekehrtes Vorzeichen.
                case StatKind.AttackInterval: return value < 0 ? $"schneller ({abs} Ticks)" : $"langsamer ({abs} Ticks)";
                case StatKind.Dodge: return $"{sign}{abs / 100} % Ausweichen";
                case StatKind.Block: return $"{sign}{abs / 100} % Block";
                case StatKind.Crit: return $"{sign}{abs / 100} % Krit";
                default: return $"{kind} {sign}{abs}";
            }
        }
    }
}
