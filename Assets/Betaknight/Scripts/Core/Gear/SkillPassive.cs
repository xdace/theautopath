using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    public enum SkillPassiveEffect
    {
        /// <summary>Wirkung (Schaden, Brennen, Heilung) in Prozent stärker.</summary>
        PowerPercent,

        /// <summary>Cooldown in Ticks kürzer (negativ) oder länger.</summary>
        CooldownTicks,

        /// <summary>Cast-Zeit in Prozent kürzer (negativ) oder länger, nie unter die Untergrenze.</summary>
        CastPercent,
    }

    /// <summary>
    /// Passiver Effekt eines Ausrüstungsteils auf alle Skills einer Art, z. B. «Schock-Skills +20 % Wirkung»
    /// oder «Heilung-Skills −1 s Cooldown». Wirkt auf jedes eingesetzte Exemplar passender Art.
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

        public static SkillPassive Cooldown(SkillKind target, int ticks) => new SkillPassive(target, SkillPassiveEffect.CooldownTicks, ticks);

        public static SkillPassive Cast(SkillKind target, int percent) => new SkillPassive(target, SkillPassiveEffect.CastPercent, percent);

        /// <summary>Summe passiver Effekte für einen Skill (nur passende Arten).</summary>
        public static void Sum(SkillDefinition skill, IEnumerable<SkillPassive> passives, out int powerPercent, out int cooldownTicks, out int castPercent)
        {
            powerPercent = 0;
            cooldownTicks = 0;
            castPercent = 0;
            if (passives == null) return;
            foreach (SkillPassive p in passives)
            {
                if (!p.Affects(skill)) continue;
                switch (p.Effect)
                {
                    case SkillPassiveEffect.PowerPercent: powerPercent += p.Value; break;
                    case SkillPassiveEffect.CooldownTicks: cooldownTicks += p.Value; break;
                    case SkillPassiveEffect.CastPercent: castPercent += p.Value; break;
                }
            }
        }

        /// <summary>Der Skill mit allen passenden passiven Effekten.</summary>
        public static SkillDefinition Apply(SkillDefinition skill, IEnumerable<SkillPassive> passives)
        {
            if (skill == null) return null;
            Sum(skill, passives, out int power, out int cooldown, out int cast);
            return skill.WithBonus(power, cooldown, cast);
        }

        public bool Affects(SkillDefinition skill) => skill != null && !skill.IsBasicAttack && skill.Kinds.Overlaps(Target);

        /// <summary>«Schock-Skills +20 % Wirkung» oder «Heilung-Skills −1 s Cooldown».</summary>
        public string Text
        {
            get
            {
                string who = Target == SkillKinds.Every ? "Alle Skills" : $"{SkillKinds.Names(Target)}-Skills";
                if (Effect == SkillPassiveEffect.PowerPercent) return $"{who} {(Value >= 0 ? "+" : "−")}{System.Math.Abs(Value)} % Wirkung";
                if (Effect == SkillPassiveEffect.CastPercent) return $"{who} {(Value <= 0 ? "−" : "+")}{System.Math.Abs(Value)} % Cast-Zeit";
                return $"{who} {(Value <= 0 ? "−" : "+")}{SkillInfo.Seconds(System.Math.Abs(Value))} Cooldown";
            }
        }

        public override string ToString() => Text;
    }
}
