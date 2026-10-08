using System;
using System.Collections.Generic;
using Betaknight.Core.Map;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Combat
{
    /// <summary>
    /// PLATZHALTER bis zur Kampfarena: würfelt Schaden nach Gegnerstufe und zieht pro Rune etwas ab,
    /// damit sich ein Build schon jetzt lohnt. Stirbt der Spieler dabei, ist der Kampf verloren.
    /// </summary>
    public sealed class PlaceholderCombatResolver : ICombatResolver
    {
        /// <summary>Schadensreduktion pro ausgerüsteter Rune.</summary>
        public int ReductionPerRune = 1;

        /// <summary>Zusätzliche Reduktion pro Tag, der mindestens zweimal ausgerüstet ist (Synergie).</summary>
        public int SynergyReduction = 2;

        public CombatResult Resolve(CombatRequest request, Random random)
        {
            (int min, int max) = BaseDamage(request.Enemy, request.Tier);
            int damage = random.Next(min, max + 1) - Mitigation(request.Board);
            damage = Math.Max(1, damage);

            bool victory = damage < request.Stats.Hp;
            int gold = victory ? random.Next(2, 5) + request.Tier / 2 : 0;
            return new CombatResult(victory, damage, gold);
        }

        public int Mitigation(Circuit.CircuitBoard runes)
        {
            int mitigation = runes.Runes.Count * ReductionPerRune;
            foreach (KeyValuePair<RuneTag, int> tag in runes.CountByTag())
            {
                if (tag.Value >= 2) mitigation += SynergyReduction;
            }
            return mitigation;
        }

        private static (int min, int max) BaseDamage(CellContent enemy, int tier)
        {
            if (enemy == CellContent.Boss) return (18, 26);
            if (enemy == CellContent.Elite) return (8, 14);
            if (tier <= 2) return (3, 6);
            if (tier == 3) return (5, 9);
            return (7, 12);
        }
    }
}
