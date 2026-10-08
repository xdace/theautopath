using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    public enum SkillPassiveEffect
    {
        /// <summary>Wirkung (Schaden, Brennen, Heilung) in Prozent stärker.</summary>
        PowerPercent,

        /// <summary>Cast-Zeit in Prozent kürzer (negativ) oder länger, nie unter die Untergrenze.</summary>
        CastPercent,
    }

    /// <summary>
    /// Passiver Effekt eines Ausrüstungsteils auf alle Skills einer Art, z. B. «Schock-Skills +20 % Wirkung»
    /// oder «Heilung-Skills −15 % Cast-Zeit». Wirkt auf jedes eingesetzte Exemplar passender Art.
    /// </summary>
    public readonly struct SkillPassive
    {
        public readonly SkillKind Target;
        public readonly SkillPassiveEffect Effect;
        public readonly int Value;

        public SkillPassive(SkillKind target, SkillPassiveEffect effect, int value)
        {
            Target = target;
            Effect = effect;
            Value = value;
        }

        public static SkillPassive Power(SkillKind target, int percent) => new SkillPassive(target, SkillPassiveEffect.PowerPercent, percent);

        public static SkillPassive Cast(SkillKind target, int percent) => new SkillPassive(target, SkillPassiveEffect.CastPercent, percent);

        /// <summary>Summe passiver Effekte für einen Skill (nur passende Arten).</summary>
        public static void Sum(SkillDefinition skill, IEnumerable<SkillPassive> passives, out int powerPercent, out int castPercent)
        {
            powerPercent = 0;
            castPercent = 0;
            if (passives == null) return;
            foreach (SkillPassive p in passives)
            {
                if (!p.Affects(skill)) continue;
                switch (p.Effect)
                {
                    case SkillPassiveEffect.PowerPercent: powerPercent += p.Value; break;
                    case SkillPassiveEffect.CastPercent: castPercent += p.Value; break;
                }
            }
        }

        /// <summary>Der Skill mit allen passenden passiven Effekten.</summary>
        public static SkillDefinition Apply(SkillDefinition skill, IEnumerable<SkillPassive> passives)
        {
            if (skill == null) return null;
            Sum(skill, passives, out int power, out int cast);
            return skill.WithBonus(power, cast);
        }

        public bool Affects(SkillDefinition skill) => skill != null && !skill.IsBasicAttack && skill.Kinds.Overlaps(Target);

        /// <summary>«Shock Skills +20 % power» oder «Healing Skills −15 % Cast Time».</summary>
        public string Text
        {
            get
            {
                string who = Target == SkillKinds.Every ? CatalogTexts.AllSkills : CatalogTexts.KindSkills(SkillKinds.Names(Target));
                if (Effect == SkillPassiveEffect.PowerPercent) return CatalogTexts.PassivePower(who, Value >= 0 ? "+" : "−", System.Math.Abs(Value));
                return CatalogTexts.PassiveCast(who, Value <= 0 ? "−" : "+", System.Math.Abs(Value));
            }
        }

        public override string ToString() => Text;
    }
}
