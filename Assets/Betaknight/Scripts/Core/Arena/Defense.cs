namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Abwehrformeln: Rüstung, Ausweichen, Block, Krit.
    /// Würfe nutzen Pseudo-Zufall: jeder Fehlversuch erhöht die Chance um einen Teil der Grundchance,
    /// ein Erfolg setzt sie zurück. Das glättet Pechsträhnen, ohne den Mittelwert stark zu verschieben.
    /// </summary>
    public static class Defense
    {
        /// <summary>Geblockte Treffer verursachen nur noch diesen Anteil.</summary>
        public const int BlockedDamageBp = 2500;

        /// <summary>Krit-Treffer verursachen diesen Anteil.</summary>
        public const int CritDamageBp = 20000;

        /// <summary>Pro Fehlversuch steigt die Chance um diesen Anteil der Grundchance.</summary>
        public const int PseudoRandomStepBp = 5000;

        public const string DodgeRoll = "dodge";
        public const string BlockRoll = "block";
        public const string CritRoll = "crit";

        /// <summary>Schaden × 100 / (100 + Rüstung), abgerundet, mindestens 1.</summary>
        public static int ApplyArmor(int damage, int armor)
        {
            if (damage <= 0) return 0;
            if (armor <= 0) return damage;
            return System.Math.Max(1, (int)((long)damage * 100 / (100 + armor)));
        }

        /// <summary>Ausweichchance des Ziels minus Präzision des Angreifers, gedeckelt durch die Ausweich-Obergrenze.</summary>
        public static int DodgeChance(Combatant attacker, Combatant target)
        {
            int accuracy = attacker != null ? attacker.GetStat(StatKind.Accuracy) : 0;
            return target.GetStat(StatKind.Dodge) - accuracy;
        }

        public static bool RollDodge(Battle battle, Combatant attacker, Combatant target) =>
            Roll(battle, target, DodgeRoll, DodgeChance(attacker, target), target.GetStat(StatKind.DodgeCap));

        public static bool RollBlock(Battle battle, Combatant target) =>
            Roll(battle, target, BlockRoll, target.GetStat(StatKind.Block), target.GetStat(StatKind.BlockCap));

        public static bool RollCrit(Battle battle, Combatant attacker) =>
            Roll(battle, attacker, CritRoll, attacker.GetStat(StatKind.Crit), BasisPoints.Full);

        /// <summary>
        /// Pseudo-Zufallswurf. Ohne Grundchance wird nicht gewürfelt, damit Kämpfe ohne Ausweichen, Block
        /// und Krit denselben Zufallsstrom behalten.
        /// </summary>
        public static bool Roll(Battle battle, Combatant owner, string key, int baseChanceBp, int capBp)
        {
            if (baseChanceBp <= 0 || capBp <= 0) return false;

            int failures = owner.RollFailures(key);
            long chance = baseChanceBp + (long)baseChanceBp * failures * PseudoRandomStepBp / BasisPoints.Full;
            chance = System.Math.Min(chance, System.Math.Min(capBp, BasisPoints.Full));

            bool success = chance >= BasisPoints.Full || battle.Random.Next(BasisPoints.Full) < chance;
            owner.SetRollFailures(key, success ? 0 : failures + 1);
            return success;
        }
    }
}
