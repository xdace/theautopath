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

        /// <summary>Kurzer Anzeigetext für Tafel und Tooltips. Standard: der Name aus dem Kampfprotokoll.</summary>
        public virtual string Summary => BattleLogText.StatusName(Id);

        /// <summary>Verhindert Aktionen und bricht laufende ab.</summary>
        public virtual bool Stuns => false;

        public virtual int StatBonus(StatKind kind) => 0;

        /// <summary>Jeden Tick in Phase 1, bevor die Restzeit sinkt.</summary>
        public virtual void OnTick(Battle battle, Combatant owner) { }

        /// <summary>Wird ein gleichartiger Zustand neu angewendet: true = beide behalten, false = ersetzen.</summary>
        public virtual bool Stacks => false;

        /// <summary>Ein Treffer auf den Träger, vor Ausweichen, Krit, Block und Rüstung.</summary>
        public virtual void ModifyIncomingHit(Battle battle, Combatant owner, HitInfo hit) { }

        public bool IsActive => TicksLeft > 0;

        /// <summary>Verbraucht den Zustand; er wirkt ab sofort nicht mehr und verschwindet im nächsten Tick.</summary>
        protected void Consume() => TicksLeft = 0;
    }
}
