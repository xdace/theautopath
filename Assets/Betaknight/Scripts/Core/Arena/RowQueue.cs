using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Warteschlange für erfüllte Zeilen (A-13) als Daten: Ist die Bedingung einer Zeile erfüllt, der Skill kann aber
    /// gerade nicht starten (Aktion läuft, Cooldown, betäubt), wird die Zeile eingereiht statt übersprungen. Erfüllte
    /// Zeilen warten, bis sie dran sind – höhere Zeilen zuerst.
    /// </summary>
    public sealed class RowQueueConfig
    {
        /// <summary>Ohne Warteschlange gilt die alte Regel (überspringen, Auslöser verfallen).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Wie oft eine Zeile gleichzeitig in der Warteschlange stehen darf. 1 = erneutes Erfüllen ändert nichts.</summary>
        public int MaxEntriesPerRow { get; set; } = 1;

        /// <summary>Auslöser auf ein Ziel, das gerade nicht starten kann, reihen es ein statt zu verfallen.</summary>
        public bool QueueTriggers { get; set; } = true;

        /// <summary>Auch Gegner haben eine Warteschlange (sie sind Kämpfer wie der Ritter).</summary>
        public bool ForEnemies { get; set; } = true;

        /// <summary>
        /// Eine Bedingung, die seit dem letzten Feuern durchgehend erfüllt ist, reiht während des Cooldowns nicht erneut
        /// ein (sonst feuerte z. B. «HP unter 30 %» nach jedem Cooldown, auch wenn die HP längst wieder hoch sind). Neu
        /// erfüllt reiht sie ein; ist der Skill bereit und nur der Ritter beschäftigt, reiht sie immer ein.
        /// </summary>
        public bool OnlyNewFulfilmentDuringCooldown { get; set; } = true;

        public static RowQueueConfig Default { get; } = new RowQueueConfig();

        /// <summary>Ohne Warteschlange (alte Regel), z. B. für Vergleiche.</summary>
        public static RowQueueConfig Off { get; } = new RowQueueConfig { Enabled = false, QueueTriggers = false };

        public bool AppliesTo(Combatant c) => Enabled && c != null && (ForEnemies || c.Side == Side.Player);
    }

    /// <summary>Eine wartende Zeile: verdient zu <see cref="SinceTick"/>, startet ohne erneute Prüfung der Bedingung.</summary>
    public sealed class QueuedRow
    {
        public int Row { get; internal set; }

        /// <summary>Tick, an dem die Zeile eingereiht wurde.</summary>
        public int SinceTick { get; internal set; }

        /// <summary>Tafel (Bedingung erfüllt) oder Auslöser.</summary>
        public ActionCause Cause { get; internal set; }

        /// <summary>Bei Auslösern die auslösende Zeile, sonst -1.</summary>
        public int CauseRow { get; internal set; } = -1;

        /// <summary>Schwierigkeits-Stufe, die die Ausführung mitbringt (die Zeile, die sie verdient hat).</summary>
        public int BonusTier { get; internal set; }

        /// <summary>Ziel aus der Bedingung (falls es eins gab); ist es tot, nimmt die Ausführung das Standardziel.</summary>
        public Combatant Target { get; internal set; }

        public int WaitedTicks(int tick) => Math.Max(0, tick - SinceTick);
    }
}
