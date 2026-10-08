using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    public enum SkillPassiveEffect
    {
        /// <summary>Wirkung (Schaden, Brennen, Heilung) in Prozent stärker.</summary>
        PowerPercent,

        /// <summary>Cooldown in Ticks kürzer (negativ) oder länger.</summary>
        CooldownTicks,
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

        public bool Affects(SkillDefinition skill) => skill != null && !skill.IsBasicAttack && skill.Kinds.Overlaps(Target);

        /// <summary>«Schock-Skills +20 % Wirkung» oder «Heilung-Skills −1 s Cooldown».</summary>
        public string Text
        {
            get
            {
                string who = $"{SkillKinds.Names(Target)}-Skills";
                if (Effect == SkillPassiveEffect.PowerPercent) return $"{who} {(Value >= 0 ? "+" : "−")}{System.Math.Abs(Value)} % Wirkung";
                return $"{who} {(Value <= 0 ? "−" : "+")}{SkillInfo.Seconds(System.Math.Abs(Value))} Cooldown";
            }
        }

        public override string ToString() => Text;
    }
}
