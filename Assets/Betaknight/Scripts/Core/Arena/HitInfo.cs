namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Ein Treffer auf dem Weg durch die Schadensberechnung. Modifikatoren dürfen die Felder ändern,
    /// bevor Ausweichen, Krit, Block und Rüstung angewendet werden.
    /// </summary>
    public sealed class HitInfo
    {
        public Combatant Source;
        public Combatant Target;
        public int Amount;

        /// <summary>Skill, der den Treffer verursacht, oder null (Brennen, Überhitzung, ...).</summary>
        public string SkillId;

        /// <summary>Ein Angriff (Basisangriff oder Angriffs-Skill). Nur Angriffe lösen "Bei Treffer" aus.</summary>
        public bool IsAttack;
        public bool IsArea;
        public bool IgnoreArmor;
        public bool CanBeDodged = true;
        public bool CanBeBlocked = true;
        public bool CanCrit = true;

        /// <summary>Weicht sicher aus, sofern der Treffer ausweichbar ist (z. B. Schubdüsen).</summary>
        public bool ForceDodge;

        /// <summary>Selbstschaden: ignoriert Rüstung, Block und Ausweichen und kann töten.</summary>
        public bool IsSelfDamage;

        // Ergebnis, gesetzt von der Berechnung.
        public bool Dodged;
        public bool Blocked;
        public bool Crit;
        public int Final;

        public static HitInfo SelfDamage(Combatant target, int amount, string detail = null) => new HitInfo
        {
            Source = target,
            Target = target,
            Amount = amount,
            SkillId = detail,
            IsSelfDamage = true,
            IgnoreArmor = true,
            CanBeDodged = false,
            CanBeBlocked = false,
            CanCrit = false,
        };

        /// <summary>Schaden über Zeit (Brennen): hat einen Verursacher, ignoriert aber Abwehr und Rüstung.</summary>
        public static HitInfo OverTime(Combatant source, Combatant target, int amount, string detail) => new HitInfo
        {
            Source = source,
            Target = target,
            Amount = amount,
            SkillId = detail,
            IgnoreArmor = true,
            CanBeDodged = false,
            CanBeBlocked = false,
            CanCrit = false,
        };

        /// <summary>Schaden ohne Angreifer und ohne Abwehr, z. B. Überhitzung.</summary>
        public static HitInfo True(Combatant target, int amount, string detail) => new HitInfo
        {
            Source = null,
            Target = target,
            Amount = amount,
            SkillId = detail,
            IgnoreArmor = true,
            CanBeDodged = false,
            CanBeBlocked = false,
            CanCrit = false,
        };
    }
}
