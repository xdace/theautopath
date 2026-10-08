namespace Betaknight.Core.Arena
{
    /// <summary>Abwehrformeln: Rüstung, Ausweichen, Block, Krit.</summary>
    public static class Defense
    {
        /// <summary>Geblockte Treffer verursachen nur noch diesen Anteil.</summary>
        public const int BlockedDamageBp = 2500;

        /// <summary>Schaden × 100 / (100 + Rüstung), abgerundet, mindestens 1.</summary>
        public static int ApplyArmor(int damage, int armor)
        {
            if (damage <= 0) return 0;
            if (armor <= 0) return damage;
            return System.Math.Max(1, (int)((long)damage * 100 / (100 + armor)));
        }

        public static bool RollDodge(Battle battle, Combatant attacker, Combatant target) => false;

        public static bool RollBlock(Battle battle, Combatant target) => false;

        public static bool RollCrit(Battle battle, Combatant attacker) => false;
    }
}
