namespace Betaknight.Core.Arena
{
    /// <summary>Bekannte Zustands-Ids, damit Bedingungen und Effekte dieselben Schlüssel nutzen.</summary>
    public static class StatusIds
    {
        public const string Stun = "stun";
        public const string Burn = "burn";
        public const string ArmorBreak = "armor_break";
        public const string ShieldWall = "shield_wall";
        public const string Blinded = "blinded";
        public const string Anchor = "anchor";
        public const string Thrusters = "thrusters";
    }

    /// <summary>Betäubt: keine Aktionen, laufende Aktion wird abgebrochen.</summary>
    public sealed class StunStatus : StatusEffect
    {
        public StunStatus(int ticks) : base(StatusIds.Stun, ticks) { }
        public override bool Stuns => true;
    }

    /// <summary>Zeitlicher Wertebonus oder -malus, z. B. +Block, -Rüstung, +Ausweichen.</summary>
    public sealed class StatModifierStatus : StatusEffect
    {
        public StatKind Stat { get; }
        public int Amount { get; }

        public StatModifierStatus(string id, StatKind stat, int amount, int ticks) : base(id, ticks)
        {
            Stat = stat;
            Amount = amount;
        }

        public override int StatBonus(StatKind kind) => kind == Stat ? Amount : 0;
    }

    /// <summary>Brennen: Schaden jede Sekunde, ignoriert Abwehr und Rüstung. Neues Brennen ersetzt das alte.</summary>
    public sealed class BurnStatus : StatusEffect
    {
        public int DamagePerSecond { get; }
        private int _elapsed;

        public BurnStatus(int ticks, int damagePerSecond) : base(StatusIds.Burn, ticks)
        {
            DamagePerSecond = System.Math.Max(1, damagePerSecond);
        }

        public override void OnTick(Battle battle, Combatant owner)
        {
            _elapsed++;
            if (_elapsed % Ticks.PerSecond == 0)
                battle.ResolveHit(HitInfo.OverTime(Source, owner, DamagePerSecond, StatusIds.Burn));
        }
    }

    /// <summary>Bodenanker: Rüstung verdoppelt, aber kein Ausweichen.</summary>
    public sealed class AnchorStatus : StatusEffect
    {
        public AnchorStatus(int ticks) : base(StatusIds.Anchor, ticks) { }

        public override int StatBonus(StatKind kind) => kind == StatKind.ArmorMultiplier ? BasisPoints.Full : 0;

        public override void ModifyIncomingHit(Battle battle, Combatant owner, HitInfo hit) => hit.CanBeDodged = false;
    }

    /// <summary>Schubdüsen: der nächste ausweichbare gegnerische Treffer verfehlt sicher.</summary>
    public sealed class ThrustersStatus : StatusEffect
    {
        public ThrustersStatus(int ticks) : base(StatusIds.Thrusters, ticks) { }

        public override void ModifyIncomingHit(Battle battle, Combatant owner, HitInfo hit)
        {
            if (hit.Source == null || hit.Source.Side == owner.Side || !hit.CanBeDodged) return;
            hit.ForceDodge = true;
            Consume();
        }
    }
}
