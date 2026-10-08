using System;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Warteschlange als Daten (A-13, A-19): Löst ein Relais aus, kommen die versorgten Komponenten in die Warteschlange.
    /// Gestartet wird nach Lesereihenfolge der Platine (oben links zuerst), sobald keine Aktion läuft; jede Ausführung
    /// braucht ihre volle Cast-Zeit. Ist die Warteschlange leer, füllt der Basisangriff die Lücke.
    /// </summary>
    public sealed class QueueConfig
    {
        /// <summary>Die Regel in einem Satz, für Tooltips.</summary>
        public const string RuleText = ArenaTexts.QueueRule;

        /// <summary>
        /// Wie oft eine Komponente gleichzeitig in der Warteschlange stehen darf. Löst ihr Relais aus, während sie schon so
        /// oft wartet, ist das ein «Missed Trigger».
        /// </summary>
        public int MaxEntriesPerComponent { get; set; } = 1;

        public static QueueConfig Default { get; } = new QueueConfig();
    }

    /// <summary>Warum ein Auslösen bei einer Komponente nichts bewirkt hat.</summary>
    public enum MissReason
    {
        /// <summary>Die Komponente stand schon (so oft wie erlaubt) in der Warteschlange.</summary>
        AlreadyQueued,

        /// <summary>Für die Grenze des auslösenden Relais zu gross.</summary>
        TooLarge,

        /// <summary>Eingefroren (Freeze): kann eine Weile nicht feuern.</summary>
        Frozen,

        /// <summary>Kein Skill (verwaist).</summary>
        Orphaned,
    }

    /// <summary>Eine wartende Komponente: eingereiht zu <see cref="SinceTick"/>, startet ohne erneute Prüfung.</summary>
    public sealed class QueuedRow
    {
        /// <summary>Komponente (Index in <see cref="LogicBoard.Rows"/>).</summary>
        public int Row { get; internal set; }

        /// <summary>Tick, an dem die Komponente eingereiht wurde.</summary>
        public int SinceTick { get; internal set; }

        /// <summary>Relais (Platine), Auslöser-Modul oder «Repeat while true».</summary>
        public ActionCause Cause { get; internal set; }

        /// <summary>Bei Auslöser-Modulen die auslösende Komponente, sonst -1.</summary>
        public int CauseRow { get; internal set; } = -1;

        /// <summary>Relais, das die Ausführung verdient hat (Index in <see cref="LogicBoard.Relays"/>), -1 ohne.</summary>
        public int Relay { get; internal set; } = -1;

        /// <summary>Schwierigkeits-Stufe, die die Ausführung mitbringt (das auslösende Relais).</summary>
        public int BonusTier { get; internal set; }

        /// <summary>Ziel aus der Bedingung (falls es eins gab); ist es tot, nimmt die Ausführung das Standardziel.</summary>
        public Combatant Target { get; internal set; }

        public int WaitedTicks(int tick) => Math.Max(0, tick - SinceTick);
    }
}
