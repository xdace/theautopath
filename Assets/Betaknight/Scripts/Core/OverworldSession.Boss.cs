using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Map;
using Betaknight.Core.Turns;

namespace Betaknight.Core
{
    /// <summary>
    /// Boss alle 25 Züge: Er taucht beim Ritter auf und ist unbesiegbar. Der Ritter muss überleben,
    /// bis sich das Fluchtportal öffnet (Ausweichen, Betäuben, Heilen; Phantom-Signal ist dafür gebaut).
    /// </summary>
    public sealed partial class OverworldSession
    {
        public const int BossInterval = 25;
        public const int BossEscapeGold = 8;
        public const int BossEscapeShards = 2;

        private bool _bossPending;

        /// <summary>Züge bis zum nächsten Boss (1 = der nächste Schritt ruft ihn).</summary>
        public int TurnsUntilBoss => BossInterval - Turns.CurrentTurn % BossInterval;

        public event Action<CombatResult> BossEncountered;

        private void ScheduleBoss(int turn)
        {
            if (TurnSystem.IsIntervalTurn(turn, BossInterval)) _bossPending = true;
        }

        /// <summary>Nach den Feld-Events eines Schritts: steht ein Boss an, greift er jetzt an.</summary>
        private void TriggerBossIfDue()
        {
            if (!_bossPending || IsGameOver) return;
            _bossPending = false;

            HexCell cell = CurrentCell;
            int tier = TierAt(cell.Coord);
            var context = new BattleContext { VsBoss = true, Turn = Turns.CurrentTurn };
            CombatResult result = RunCombat(CellContent.Boss, tier, context);
            BossEncountered?.Invoke(result);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(result.EnemyName)) lines.Add(result.EnemyName);
            if (!ApplyCombat(cell, result, "Boss", lines)) return;

            Stats.AddGold(BossEscapeGold);
            Stats.AddShards(BossEscapeShards);
            lines.Add($"+{BossEscapeGold} Gold");
            lines.Add($"+{BossEscapeShards} Runensplitter");
            int before = Runes.Slots;
            if (ExpandBoard(Progression.BoardRowsOnBossEscape)) lines.Add($"Tafel-Erweiterung: {before} → {Runes.Slots} Zeilen");
            string module = GrantBossModule();
            if (module != null) lines.Add(module);
            lines.AddRange(EvolveAfterBoss());
            lines.Add($"Portal zu Akt {Act + 1} offen");
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, result.Escaped ? "Durchs Portal entkommen" : "Boss besiegt", lines));
            CheckShards();
            OpenPortal();
        }
    }
}
