using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Überlast-Protokoll. 2 Teile: jeder eigene Basisangriff gibt 1 Tempo-Stapel (+10 % Angriffstempo) und
    /// 2 % Max-HP Selbstschaden (Hitze). 3 Teile: Hitze-Schaden höchstens 1, solange eine Heil-Zeile bereit ist.
    /// </summary>
    public sealed class OverloadSet : BattleModifier
    {
        public const string HeatDetail = "heat";
        public const int TempoPerStackBp = 1000;
        public const int SelfDamageBp = 200;

        public bool Full { get; }
        public OverloadSet(bool full) => Full = full;

        public override string Name => "Overload Protocol";

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind != BattleEventKind.ActionExecuted || e.Source != owner || e.Detail != SkillDefinition.BasicAttackId) return;
            if (!owner.IsAlive) return;

            battle.ChangeResource(owner, ResourceIds.Tempo, 1);
            battle.ChangeResource(owner, ResourceIds.Heat, 1);
            battle.ResolveHit(HitInfo.SelfDamage(owner, System.Math.Max(1, BasisPoints.Of(owner.MaxHp, SelfDamageBp)), HeatDetail));
        }

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind) =>
            kind == StatKind.AttackSpeed ? owner.GetResource(ResourceIds.Tempo) * TempoPerStackBp : 0;

        public override void ModifyFinalDamage(Battle battle, Combatant owner, HitInfo hit)
        {
            if (!Full || !hit.IsSelfDamage || hit.Target != owner || hit.SkillId != HeatDetail) return;
            if (HasReadyHealRow(owner)) hit.Final = System.Math.Min(hit.Final, 1);
        }

        private static bool HasReadyHealRow(Combatant c)
        {
            foreach (LogicRow row in c.Board.Rows)
            {
                if (row.IsOrphaned || !c.IsReady(row.Skill)) continue;
                foreach (ISkillEffect effect in row.Skill.Effects)
                    if (effect is HealEffect) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Aegis-Firewall. 2 Teile: jeder Block gibt 1 Ladung (max. 5), bei 5 Ladung Rüstung ×2.
    /// 3 Teile: Ein Skill aus einer Zeile mit Ladungs-Bedingung entlädt: Ladung auf 0, Schaden = 5 × Rüstung.
    /// </summary>
    public sealed class AegisSet : BattleModifier
    {
        public const int MaxCharge = 5;
        public const int DischargeArmorFactor = 5;
        public const string DischargeDetail = "discharge";

        public bool Full { get; }
        public AegisSet(bool full) => Full = full;

        public override string Name => "Aegis Firewall";

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind == BattleEventKind.Blocked && e.Target == owner && owner.IsAlive)
                battle.ChangeResource(owner, ResourceIds.Charge, 1, MaxCharge);
        }

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind) =>
            kind == StatKind.ArmorMultiplier && owner.GetResource(ResourceIds.Charge) >= MaxCharge ? BasisPoints.Full : 0;

        public override void OnActionStarted(Battle battle, Combatant owner, SkillDefinition skill, int rowIndex)
        {
            if (!Full || rowIndex < 0 || rowIndex >= owner.Board.Rows.Count) return;
            if (!(owner.Board.Rows[rowIndex].Condition is ResourceAtLeastCondition r) || r.ResourceId != ResourceIds.Charge) return;
            if (owner.GetResource(ResourceIds.Charge) <= 0) return;

            int damage = DischargeArmorFactor * owner.EffectiveArmor;
            battle.ChangeResource(owner, ResourceIds.Charge, -owner.GetResource(ResourceIds.Charge));
            Combatant target = owner.Action?.Target ?? battle.DefaultTarget(owner);
            if (damage > 0 && target != null)
            {
                battle.ResolveHit(new HitInfo
                {
                    Source = owner,
                    Target = target,
                    Amount = damage,
                    SkillId = DischargeDetail,
                    IsAttack = true,
                    CanBeDodged = false,
                    CanCrit = false,
                });
            }
        }
    }

    /// <summary>
    /// Schrott-Ernter. 2 Teile: +2 Gold je besiegtem Gegner. 3 Teile auf Goldminen: +50 % Flächenschaden,
    /// eigene Angriffe ignorieren Rüstung.
    /// </summary>
    public sealed class ScrapHarvesterSet : BattleModifier
    {
        public const int GoldPerKill = 2;
        public const int MineAreaBonusBp = 5000;

        public bool Full { get; }
        public ScrapHarvesterSet(bool full) => Full = full;

        public override string Name => "Scrap Harvester";

        private bool MineActive(Battle battle) => Full && battle.Context.OnGoldMine;

        public override void OnBattleEnd(Battle battle, Combatant owner, BattleOutcome outcome)
        {
            if (owner.Side != Side.Player) return;
            foreach (Combatant enemy in battle.Enemies)
                if (!enemy.IsAlive) battle.AddBonusGold(GoldPerKill);
        }

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind) =>
            kind == StatKind.AreaDamage && MineActive(battle) ? MineAreaBonusBp : 0;

        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source == owner && hit.IsAttack && MineActive(battle)) hit.IgnoreArmor = true;
        }
    }

    /// <summary>
    /// Phantom-Signal. 2 Teile: +20 % Ausweichen. 3 Teile: jedes Ausweichen senkt alle eigenen Cooldowns um 1 s,
    /// Ausweich-Obergrenze 75 %.
    /// </summary>
    public sealed class PhantomSet : BattleModifier
    {
        public const int DodgeBonusBp = 2000;
        public const int CapBonusBp = 1500;

        public bool Full { get; }
        public PhantomSet(bool full) => Full = full;

        public override string Name => "Phantom Signal";

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind)
        {
            if (kind == StatKind.Dodge) return DodgeBonusBp;
            if (kind == StatKind.DodgeCap && Full) return CapBonusBp;
            return 0;
        }

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (Full && e.Kind == BattleEventKind.Dodged && e.Target == owner) owner.ReduceCooldowns(Ticks.PerSecond);
        }
    }
}
