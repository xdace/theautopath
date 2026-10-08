namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Wie stark ein Skill pro Stufe seines Ausrüstungsteils wird (+1, +2, +3). Werte stehen in der
    /// <see cref="Run.ProgressionConfig"/>; Effekte, die stärker werden können, setzen <see cref="ILevelableEffect"/> um.
    /// </summary>
    public sealed class SkillLevelRules
    {
        /// <summary>Direkter Schaden: so viele Basispunkte Waffenschaden pro Stufe (1500 = 120 % → 135 %).</summary>
        public int DamageBpPerLevel = 1500;

        /// <summary>Schaden über Zeit pro Sekunde: so viele Basispunkte pro Stufe.</summary>
        public int DamageOverTimeBpPerLevel = 1000;

        /// <summary>Heilung: so viele Basispunkte Max-HP pro Stufe.</summary>
        public int HealBpPerLevel = 500;
    }

    /// <summary>Eine Wirkung, die mit der Stufe des Ausrüstungsteils stärker wird.</summary>
    public interface ILevelableEffect
    {
        /// <summary>Dieselbe Wirkung auf Stufe <paramref name="level"/> (0 = unverändert).</summary>
        ISkillEffect AtLevel(int level, SkillLevelRules rules);
    }
}
