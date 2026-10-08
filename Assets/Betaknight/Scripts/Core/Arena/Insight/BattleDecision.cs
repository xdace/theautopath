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
        public bool Skipped(int row) => IsBusy ? Rows[row].State == RowCheckState.ActionRunning : row < ChosenRow;
    }

    /// <summary>Lesbare Gründe für die Arena-Ansicht und die Auswertung.</summary>
    public static class RowStateText
    {
        public static string Reason(RowCheckState state) => Reason(new RowCheck(state, 0, 0));

        public static string Reason(RowCheck check)
        {
            switch (check.State)
            {
                case RowCheckState.ConditionFalse: return "Bedingung nicht erfüllt";
                case RowCheckState.Cooldown: return check.CooldownLeft > 0 ? $"Skill im Cooldown (noch {Seconds(check.CooldownLeft)})" : "Skill im Cooldown";
                case RowCheckState.Orphaned: return "verwaist (kein Skill)";
                case RowCheckState.ActionRunning: return "Bedingung erfüllt, aber Aktion läuft";
                default: return "bereit";
            }
        }

        /// <summary>«1,5 s» mit einer Nachkommastelle.</summary>
        public static string Seconds(int ticks) => $"{ticks / Ticks.PerSecond},{ticks % Ticks.PerSecond * 10 / Ticks.PerSecond} s";
    }
}
