namespace Betaknight.Core.Arena
{
    /// <summary>Schaden in Prozent des Waffenschadens, auf das Ziel oder alle Gegner.</summary>
    public sealed class DamageEffect : ISkillEffect
    {
        public int DamageBp { get; }
        public bool AllEnemies { get; }
        public bool IgnoreArmor { get; }

        public DamageEffect(int damageBp, bool allEnemies = false, bool ignoreArmor = false)
        {
            DamageBp = damageBp;
            AllEnemies = allEnemies;
            IgnoreArmor = ignoreArmor;
        }

        /// <summary>Schaden pro Ziel vor Abwehr, aus Waffenschaden und Flächenbonus.</summary>
        public int AmountFor(int weaponDamage, int areaDamageBp)
        {
            int amount = BasisPoints.Of(weaponDamage, DamageBp);
            if (!AllEnemies) return amount;
            int bonus = System.Math.Max(-BasisPoints.Full, areaDamageBp);
            return BasisPoints.Of(amount, BasisPoints.Full + bonus);
        }

        public void Apply(in SkillContext context)
        {
            int amount = AmountFor(context.User.GetStat(StatKind.Damage), context.User.GetStat(StatKind.AreaDamage));

            if (AllEnemies)
            {
                foreach (Combatant enemy in context.Battle.OpponentsOf(context.User))
                    context.Battle.ResolveHit(Hit(context, enemy, amount, true));
            }
            else if (context.Target != null && context.Target.IsAlive)
            {
                context.Battle.ResolveHit(Hit(context, context.Target, amount, false));
            }
        }

        public void Describe(SkillInfoBuilder info)
        {
            int amount = AmountFor(info.Stats.WeaponDamage, info.Stats.AreaDamageBp);
            string text = $"{SkillInfo.Percent(DamageBp)} Waffenschaden ≈ {amount}{(AllEnemies ? " an allen Gegnern" : string.Empty)}"
                + (IgnoreArmor ? " (ignoriert Rüstung)" : string.Empty);
            info.Add(new EffectInfo(EffectInfoKind.Damage, text, DamageBp, amount, allEnemies: AllEnemies));
        }

        private HitInfo Hit(in SkillContext context, Combatant target, int amount, bool area) => new HitInfo
        {
            Source = context.User,
            Target = target,
            Amount = amount,
            SkillId = context.Skill.Id,
            IsAttack = true,
            IsArea = area,
            IgnoreArmor = IgnoreArmor,
        };
    }
}
