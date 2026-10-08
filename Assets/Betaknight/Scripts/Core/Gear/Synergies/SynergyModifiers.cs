using System;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    // Bausteine für Tag-Stufen und Duos. Jede Regel ist ein BattleModifier, der Simulator bleibt unverändert.
    // Ereignisse kommen wie bei allen Modifikatoren einen Tick verzögert an.

    /// <summary>Fester Wertebonus für den ganzen Kampf.</summary>
    public sealed class SynergyStatBonus : BattleModifier
    {
        private readonly StatKind _kind;
        private readonly int _amount;

        public SynergyStatBonus(StatKind kind, int amount)
        {
            _kind = kind;
            _amount = amount;
        }

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind) => kind == _kind ? _amount : 0;
    }

    /// <summary>Gold je besiegtem Gegner nach einem Kampf.</summary>
    public sealed class SynergyGoldPerKill : BattleModifier
    {
        private readonly int _gold;
        public SynergyGoldPerKill(int gold) => _gold = gold;

        public override void OnBattleEnd(Battle battle, Combatant owner, BattleOutcome outcome)
        {
            if (owner.Side != Side.Player) return;
            foreach (Combatant enemy in battle.Enemies)
                if (!enemy.IsAlive) battle.AddBonusGold(_gold);
        }
    }

    /// <summary>Jeder Block bzw. jedes Ausweichen des Trägers senkt alle eigenen Cooldowns.</summary>
    public sealed class SynergyCooldownOnDefense : BattleModifier
    {
        private readonly BattleEventKind _kind;
        private readonly int _ticks;

        public SynergyCooldownOnDefense(BattleEventKind kind, int ticks)
        {
            _kind = kind;
            _ticks = ticks;
        }

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind == _kind && e.Target == owner && owner.IsAlive) owner.ReduceCooldowns(_ticks);
        }
    }

    /// <summary>Eigene Angriffe gegen Ziele mit einem Zustand machen mehr Schaden (Prozent).</summary>
    public sealed class SynergyBonusVsStatus : BattleModifier
    {
        private readonly string _statusId;
        private readonly int _percent;

        public SynergyBonusVsStatus(string statusId, int percent)
        {
            _statusId = statusId;
            _percent = percent;
        }

        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source != owner || !hit.IsAttack || hit.Target == null || !hit.Target.HasStatus(_statusId)) return;
            hit.Amount = (int)Math.Min(int.MaxValue, (long)hit.Amount * (100 + _percent) / 100);
        }
    }

    /// <summary>Schaden über Zeit (z. B. Brennen) gegen Ziele mit einem weiteren Zustand (z. B. Gift) wird stärker.</summary>
    public sealed class SynergyDotBoostVsStatus : BattleModifier
    {
        private readonly string _dotId;
        private readonly string _statusId;
        private readonly int _percent;

        public SynergyDotBoostVsStatus(string dotId, string statusId, int percent)
        {
            _dotId = dotId;
            _statusId = statusId;
            _percent = percent;
        }

        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source != owner || hit.SkillId != _dotId || hit.Target == null || !hit.Target.HasStatus(_statusId)) return;
            hit.Amount = (int)Math.Min(int.MaxValue, (long)hit.Amount * (100 + _percent) / 100);
        }
    }

    /// <summary>Eigene Angriffe ignorieren Rüstung, optional nur gegen Ziele mit einem Zustand.</summary>
    public sealed class SynergyIgnoreArmor : BattleModifier
    {
        private readonly string _requiredStatus;
        public SynergyIgnoreArmor(string requiredStatus = null) => _requiredStatus = requiredStatus;

        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source != owner || !hit.IsAttack) return;
            if (_requiredStatus != null && (hit.Target == null || !hit.Target.HasStatus(_requiredStatus))) return;
            hit.IgnoreArmor = true;
        }
    }

    /// <summary>Eigene Treffer vergiften (Stapel bis zur Obergrenze). Wahlweise nur Skills oder auch Basisangriffe.</summary>
    public sealed class SynergyPoisonOnHit : BattleModifier
    {
        public const int MaxStacks = 5;
        public static readonly int Duration = Ticks.FromSeconds(4);

        private readonly bool _basicAttacks;
        public SynergyPoisonOnHit(bool basicAttacks) => _basicAttacks = basicAttacks;

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind != BattleEventKind.Hit || e.Source != owner || e.Target == null || !e.Target.IsAlive) return;
            bool basic = e.Detail == SkillDefinition.BasicAttackId;
            if (basic != _basicAttacks) return;
            if (PoisonStatus.StacksOn(e.Target) >= MaxStacks) return;
            battle.ApplyStatus(e.Target, new PoisonStatus(Duration), owner);
        }
    }

    /// <summary>Eigene Angriffe: +1 Schaden je Gift-Stapel des Ziels.</summary>
    public sealed class SynergyDamagePerPoison : BattleModifier
    {
        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source != owner || !hit.IsAttack || hit.Target == null) return;
            hit.Amount += PoisonStatus.StacksOn(hit.Target);
        }
    }

    /// <summary>Eigene Basisangriffe setzen Brennen, wenn das Ziel noch nicht brennt (überschreibt kein stärkeres Brennen).</summary>
    public sealed class SynergyBurnOnBasic : BattleModifier
    {
        public static readonly int Duration = Ticks.FromSeconds(3);
        public const int WeaponDamageBp = 2500;

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind != BattleEventKind.Hit || e.Source != owner || e.Detail != SkillDefinition.BasicAttackId) return;
            if (e.Target == null || !e.Target.IsAlive || e.Target.HasStatus(StatusIds.Burn)) return;
            int dps = Math.Max(1, BasisPoints.Of(owner.GetStat(StatKind.Damage), WeaponDamageBp));
            battle.ApplyStatus(e.Target, new BurnStatus(Duration, dps), owner);
        }
    }

    /// <summary>Eigene Basisangriffe gegen Ziele mit einem Zustand senken alle eigenen Cooldowns.</summary>
    public sealed class SynergyCooldownOnBasicVsStatus : BattleModifier
    {
        private readonly string _statusId;
        private readonly int _ticks;

        public SynergyCooldownOnBasicVsStatus(string statusId, int ticks)
        {
            _statusId = statusId;
            _ticks = ticks;
        }

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind != BattleEventKind.Hit || e.Source != owner || e.Detail != SkillDefinition.BasicAttackId) return;
            if (e.Target != null && e.Target.HasStatus(_statusId)) owner.ReduceCooldowns(_ticks);
        }
    }

    /// <summary>Jedes Ereignis einer Art (Block, Ausweichen) auf den Träger gibt einen Stapel Wertebonus bis Kampfende.</summary>
    public sealed class SynergyStackOnDefense : BattleModifier
    {
        private readonly BattleEventKind _kind;
        private readonly StatKind _stat;
        private readonly int _perStack;
        private readonly int _maxStacks;
        private int _stacks;

        public SynergyStackOnDefense(BattleEventKind kind, StatKind stat, int perStack, int maxStacks)
        {
            _kind = kind;
            _stat = stat;
            _perStack = perStack;
            _maxStacks = maxStacks;
        }

        public int Stacks => _stacks;

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind == _kind && e.Target == owner && owner.IsAlive && _stacks < _maxStacks) _stacks++;
        }

        public override int StatBonus(Battle battle, Combatant owner, StatKind kind) => kind == _stat ? _stacks * _perStack : 0;
    }

    /// <summary>Nach jedem Ausweichen trifft der nächste eigene Angriff stärker.</summary>
    public sealed class SynergyRiposte : BattleModifier
    {
        private readonly int _percent;
        private bool _armed;

        public SynergyRiposte(int percent) => _percent = percent;

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind == BattleEventKind.Dodged && e.Target == owner) _armed = true;
        }

        public override void ModifyHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (!_armed || hit.Source != owner || !hit.IsAttack) return;
            _armed = false;
            hit.Amount = (int)Math.Min(int.MaxValue, (long)hit.Amount * (100 + _percent) / 100);
        }
    }
}
