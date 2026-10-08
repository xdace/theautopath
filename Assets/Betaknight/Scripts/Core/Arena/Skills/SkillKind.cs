using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Skill-Arten. Ein Skill kann mehrere haben (z. B. Schockstich = Angriff + Schock).
    /// Sie sind nur Ziel für passive Effekte der Ausrüstung («Feuer-Skills +20 %») und steuern, welche Skills angeboten werden.
    /// </summary>
    [Flags]
    public enum SkillKind
    {
        None = 0,
        Attack = 1 << 0,
        Shield = 1 << 1,
        Fire = 1 << 2,
        Shock = 1 << 3,
        Healing = 1 << 4,
        Movement = 1 << 5,
    }

    public static class SkillKinds
    {
        /// <summary>Alle einzelnen Arten in Anzeige-Reihenfolge.</summary>
        public static IReadOnlyList<SkillKind> All { get; } = new[]
        {
            SkillKind.Attack, SkillKind.Shield, SkillKind.Fire, SkillKind.Shock, SkillKind.Healing, SkillKind.Movement,
        };

        public static string DisplayName(SkillKind kind)
        {
            switch (kind)
            {
                case SkillKind.Attack: return "Angriff";
                case SkillKind.Shield: return "Schild";
                case SkillKind.Fire: return "Feuer";
                case SkillKind.Shock: return "Schock";
                case SkillKind.Healing: return "Heilung";
                case SkillKind.Movement: return "Bewegung";
                default: return kind.ToString();
            }
        }

        /// <summary>«Angriff, Schock» oder leer.</summary>
        public static string Names(SkillKind kinds)
        {
            var names = new List<string>();
            foreach (SkillKind kind in All) if ((kinds & kind) != 0) names.Add(DisplayName(kind));
            return string.Join(", ", names);
        }

        /// <summary>Alle Arten zusammen: Ziel für Effekte auf jeden Skill («Alle Skills −15 % Cast-Zeit»).</summary>
        public const SkillKind Every = SkillKind.Attack | SkillKind.Shield | SkillKind.Fire | SkillKind.Shock | SkillKind.Healing | SkillKind.Movement;

        public static bool Overlaps(this SkillKind a, SkillKind b) => (a & b) != 0;
    }
}
