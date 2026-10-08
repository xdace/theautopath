namespace Betaknight.Core.Arena
{
    public enum BattleEventKind
    {
        BattleStart,

        /// <summary>Ein Kämpfer beginnt eine Aktion (Ausholen). Bei langen Aufladungen sichtbar für "Gegner lädt auf".</summary>
        ActionStarted,

        /// <summary>Die Wirkung einer Aktion tritt ein. Amount = 1, wenn sie als Angriff zählt.</summary>
        ActionExecuted,

        /// <summary>Eine Aktion wurde abgebrochen (Betäubung oder eine höhere Zeile).</summary>
        ActionInterrupted,

        /// <summary>Ein Angriff trifft (Quelle = Angreifer, Ziel = Getroffener).</summary>
        Hit,

        /// <summary>Ziel verliert Leben. Amount = tatsächlicher Schaden.</summary>
        Damage,
        Dodged,
        Blocked,
        Crit,

        /// <summary>Selbstschaden (Hitze, Opfer). Quelle = Ziel.</summary>
        SelfDamage,
        Healed,
        StatusApplied,
        StatusExpired,

        /// <summary>Ein Ressourcen-Zähler (Ladung, Tempo, ...) hat sich geändert. Detail = Ressourcen-Id.</summary>
        ResourceChanged,
        Death,

        /// <summary>Zeitlimit überschritten, Überhitzung beginnt bzw. steigt.</summary>
        Overheat,
        BattleEnd,

        /// <summary>
        /// Ein Auslösen hat bei einer Komponente nichts bewirkt («Missed Trigger»): RowIndex = Komponente, Detail = Skill,
        /// Amount = <see cref="MissReason"/>, <see cref="BattleEvent.Relay"/> = auslösendes Relais (bei Auslöser-Modulen das Relais der Quelle, sonst -1).
        /// </summary>
        TriggerMissed,

        /// <summary>
        /// Eine Komponente wurde eingereiht (ihr Relais hat ausgelöst). RowIndex = Komponente, Detail = Skill,
        /// <see cref="BattleEvent.Relay"/> = auslösendes Relais.
        /// </summary>
        RowQueued,

        /// <summary>Ein Relais hat ausgelöst (A-19). Amount = Index des Relais, Extra = Zahl der versorgten Komponenten.</summary>
        RelayTriggered,

        /// <summary>Eine Komponente wurde eingefroren (Freeze). RowIndex = Komponente des Ziels, Amount = Dauer in Ticks.</summary>
        Frozen,
    }

    /// <summary>Ein Eintrag im Kampfprotokoll. Bedingungen und Set-Boni lesen dieselben Einträge.</summary>
    public sealed class BattleEvent
    {
        public int Tick { get; }
        public BattleEventKind Kind { get; }
        public Combatant Source { get; }
        public Combatant Target { get; }
        public int Amount { get; }

        /// <summary>Skill-, Status- oder Ressourcen-Id, je nach Art.</summary>
        public string Detail { get; }

        /// <summary>
        /// Komponente der Platine, deren Aktion es ist, sonst -1. Schaden, Heilung und Zustände einer Aktion (auch späteres
        /// Brennen) tragen die Komponente ebenfalls, damit Auswertung und Anzeige sie zuordnen können.
        /// </summary>
        public int RowIndex { get; internal set; }

        /// <summary>Bei Aktionen: Relais, Wiederholung (Echo, Multicast) oder Auslöser-Modul.</summary>
        public ActionCause Cause { get; internal set; }

        /// <summary>Bei Auslöser-Modulen: Komponente, die ausgelöst hat, sonst -1.</summary>
        public int CauseRow { get; internal set; } = -1;

        /// <summary>Bei Aktionen, Einreihen und Missed Triggers: das auslösende Relais (Index), sonst -1.</summary>
        public int Relay { get; internal set; } = -1;

        /// <summary>Wiederholung (Echo, Multicast): eigene Cast-Zeit.</summary>
        public bool IsRepeat => Cause == ActionCause.Repeat;

        /// <summary>Durch ein Auslöser-Modul gestartet.</summary>
        public bool IsTriggered => Cause == ActionCause.Trigger;

        /// <summary>Zusatzwert nur für die Anzeige: Stapel bei Zuständen, Obergrenze bei Ressourcen (0 = offen).</summary>
        public int Extra { get; internal set; }

        /// <summary>Bei <see cref="BattleEventKind.ActionStarted"/>: Schwierigkeits-Stufe der Ausführung (0 = kein Bonus).</summary>
        public int Tier { get; internal set; }

        /// <summary>
        /// Anteil des Schwierigkeits-Bonus: bei Schaden und Heilung die Menge, die er dazugegeben hat; bei
        /// <see cref="BattleEventKind.ActionStarted"/> die gesparte Cast-Zeit in Ticks.
        /// </summary>
        public int Bonus { get; internal set; }

        /// <summary>Bei <see cref="BattleEventKind.ActionStarted"/>: Wartezeit in der Warteschlange in Ticks, -1 = nicht eingereiht.</summary>
        public int QueuedTicks { get; internal set; } = -1;

        /// <summary>Kam die Aktion aus der Warteschlange?</summary>
        public bool FromQueue => QueuedTicks >= 0;

        public BattleEvent(int tick, BattleEventKind kind, Combatant source, Combatant target, int amount = 0, string detail = null, int rowIndex = -1)
        {
            Tick = tick;
            Kind = kind;
            Source = source;
            Target = target;
            Amount = amount;
            Detail = detail;
            RowIndex = rowIndex;
        }

        public override string ToString()
        {
            string time = $"{Tick / Ticks.PerSecond}.{Tick % Ticks.PerSecond * 100 / Ticks.PerSecond:00}s";
            return $"{time} {Kind} {Source?.Name} -> {Target?.Name} {Amount} {Detail}".TrimEnd();
        }
    }
}
