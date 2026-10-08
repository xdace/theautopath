namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Zeitlich begrenzter Zustand auf einem Kämpfer (Betäubung, Brennen, Buffs, Debuffs).
    /// Neue Zustände sind Unterklassen. Ein Zustand mit gleicher Id ersetzt den alten, ausser er stapelt.
    /// </summary>
    public abstract class StatusEffect
    {
        public string Id { get; }
        public int TicksLeft { get; internal set; }

        /// <summary>Wer den Zustand verursacht hat (für Schaden über Zeit).</summary>
        public Combatant Source { get; internal set; }

        protected StatusEffect(string id, int ticks)
        {
            Id = id;
            TicksLeft = ticks;
        }

        /// <summary>Verhindert Aktionen und bricht laufende ab.</summary>
        public virtual bool Stuns => false;

        public virtual int StatBonus(StatKind kind) => 0;

        /// <summary>Jeden Tick in Phase 1, bevor die Restzeit sinkt.</summary>
        public virtual void OnTick(Battle battle, Combatant owner) { }

        /// <summary>Wird ein gleichartiger Zustand neu angewendet: true = beide behalten, false = ersetzen.</summary>
        public virtual bool Stacks => false;
    }
}
