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

        public void Apply(in SkillContext context)
        {
            int amount = BasisPoints.Of(context.User.GetStat(StatKind.Damage), DamageBp);

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
