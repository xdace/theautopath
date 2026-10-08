using System;

namespace Betaknight.Core.Turns
{
    /// <summary>
    /// Zählt Oberwelt-Züge. Jeder Schritt auf ein Nachbarfeld kostet einen Zug.
    /// Spätere Systeme (Goldminen-Einkommen, Boss-Spawn alle ~25 Züge, Gegneralarme)
    /// hängen sich an <see cref="TurnEnded"/>.
    /// </summary>
    public sealed class TurnSystem
    {
        public int CurrentTurn { get; private set; }

        /// <summary>Parameter: Nummer des gerade beendeten Zuges (beginnend bei 1).</summary>
        public event Action<int> TurnEnded;

        public TurnSystem(int startTurn = 0)
        {
            if (startTurn < 0) throw new ArgumentOutOfRangeException(nameof(startTurn));
            CurrentTurn = startTurn;
        }

        public void EndTurn()
        {
            CurrentTurn++;
            TurnEnded?.Invoke(CurrentTurn);
        }

        /// <summary>Hilfsfunktion für Intervall-Ereignisse, z. B. IsIntervalTurn(turn, 25) für den Boss.</summary>
        public static bool IsIntervalTurn(int turn, int interval) => interval > 0 && turn > 0 && turn % interval == 0;
    }
}
