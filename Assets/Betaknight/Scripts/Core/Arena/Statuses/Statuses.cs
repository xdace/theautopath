namespace Betaknight.Core.Arena
{
    /// <summary>Bekannte Zustands-Ids, damit Bedingungen und Effekte dieselben Schlüssel nutzen.</summary>
    public static class StatusIds
    {
        public const string Stun = "stun";
        public const string Burn = "burn";
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
}
