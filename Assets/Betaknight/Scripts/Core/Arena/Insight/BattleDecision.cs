using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Zustand einer Tafel-Zeile bei einer Entscheidung.</summary>
    public enum RowCheckState
    {
        /// <summary>Bedingung erfüllt und Skill bereit (die gewählte Zeile oder eine darunter, die nicht mehr dran kam).</summary>
        Ready,

        /// <summary>Bedingung nicht erfüllt.</summary>
        ConditionFalse,

        /// <summary>Skill im Cooldown.</summary>
        Cooldown,

        /// <summary>Verwaist: die Zeile hat keinen Skill.</summary>
        Orphaned,

        /// <summary>Bedingung erfüllt und Skill bereit, aber eine andere Aktion lief und liess sich nicht abbrechen.</summary>
        ActionRunning,

        /// <summary>Eingereiht (A-13): die Zeile wartet, bis sie dran ist (Aktion läuft oder Skill im Cooldown).</summary>
        Queued,
    }

    /// <summary>Eine Zeile bei einer Entscheidung: Zustand und Cooldown des Skills danach.</summary>
    public readonly struct RowCheck
    {
        public readonly RowCheckState State;

        /// <summary>Rest-Cooldown des Skills in Ticks, nachdem die Entscheidung gefallen ist (0 = bereit).</summary>
        public readonly int CooldownLeft;

        /// <summary>Volle Abklingzeit des Skills in Ticks (für den Restzeit-Balken).</summary>
        public readonly int CooldownTotal;

        /// <summary>
        /// Ist die Bedingung nach der Entscheidung erfüllt (auch wenn der Skill im Cooldown ist)? Nur für die Anzeige
        /// ✔/✖; eine gerade gefeuerte Ereignis-Bedingung ist danach verbraucht.
        /// </summary>
        public readonly bool ConditionMet;

        public RowCheck(RowCheckState state, int cooldownLeft, int cooldownTotal, bool conditionMet = false)
        {
            State = state;
            CooldownLeft = cooldownLeft;
            CooldownTotal = cooldownTotal;
            ConditionMet = conditionMet;
        }
    }

    /// <summary>
    /// Eine Entscheidung des Spielers: welche Zeile eine neue Aktion gestartet hat und warum die Zeilen darüber nicht.
    /// Wird nur bei Entscheidungen festgehalten, nicht jeden Tick. Zeilen unter der gewählten sind zur Anzeige mitgeprüft.
    /// Ist <see cref="ChosenRow"/> -1, lief eine Aktion weiter, während Zeilen bereit wurden (<see cref="RowCheckState.ActionRunning"/>).
    /// </summary>
    public sealed class BattleDecision
    {
        public int Tick { get; }

        /// <summary>Zeile, die die neue Aktion gestartet hat (Fallback = Zeilenzahl), oder -1 bei «Aktion läuft».</summary>
        public int ChosenRow { get; }

        /// <summary>Zeile der laufenden Aktion bei «Aktion läuft», sonst -1.</summary>
        public int RunningRow { get; }

        /// <summary>Alle Zeilen inklusive Fallback.</summary>
        public IReadOnlyList<RowCheck> Rows { get; }

        public BattleDecision(int tick, int chosenRow, IReadOnlyList<RowCheck> rows, int runningRow = -1)
        {
            Tick = tick;
            ChosenRow = chosenRow;
            Rows = rows;
            RunningRow = runningRow;
        }

        public bool IsBusy => ChosenRow < 0;

        /// <summary>Wurde die Zeile bei dieser Entscheidung übersprungen? Dann steht der Grund in <see cref="Rows"/>.</summary>
        public bool Skipped(int row) =>
            IsBusy ? Rows[row].State == RowCheckState.ActionRunning || Rows[row].State == RowCheckState.Queued : row < ChosenRow;

        /// <summary>Stand die Zeile bei dieser Entscheidung in der Warteschlange? Dann ist sie nicht übersprungen, sondern wartet.</summary>
        public bool Queued(int row) => Rows[row].State == RowCheckState.Queued;
    }

    /// <summary>Lesbare Gründe für die Arena-Ansicht und die Auswertung.</summary>
    public static class RowStateText
    {
        public static string Reason(RowCheckState state) => Reason(new RowCheck(state, 0, 0));

        public static string Reason(RowCheck check)
        {
            switch (check.State)
            {
                case RowCheckState.ConditionFalse: return ArenaTexts.ReasonMissedTrigger;
                case RowCheckState.Cooldown: return check.CooldownLeft > 0 ? ArenaTexts.ReasonCooldownLeft(Seconds(check.CooldownLeft)) : ArenaTexts.ReasonCooldown;
                case RowCheckState.Orphaned: return ArenaTexts.ReasonOrphaned;
                case RowCheckState.ActionRunning: return ArenaTexts.ReasonActionRunning;
                case RowCheckState.Queued:
                    return check.CooldownLeft > 0 ? ArenaTexts.ReasonQueuedCooldown(Seconds(check.CooldownLeft)) : ArenaTexts.ReasonQueuedRunning;
                default: return ArenaTexts.Ready;
            }
        }

        /// <summary>«1.5 s» mit einer Nachkommastelle.</summary>
        public static string Seconds(int ticks) => ArenaTexts.SecondsOneDecimal(ticks);
    }
}
