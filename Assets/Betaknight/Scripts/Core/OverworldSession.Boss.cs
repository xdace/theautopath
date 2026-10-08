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
            if (!ApplyCombat(cell, result, SessionTexts.FightBoss, lines)) return;

            Stats.AddGold(BossEscapeGold);
            Stats.AddShards(BossEscapeShards);
            lines.Add(SessionTexts.GoldGain(BossEscapeGold));
            lines.Add(SessionTexts.ShardGain(BossEscapeShards));
            string before = BoardSize;
            if (ExpandBoard(Progression.BoardExpansionsOnBossEscape)) lines.Add(SessionTexts.BoardExpansion(before, BoardSize));
            string module = GrantBossModule();
            if (module != null) lines.Add(module);
            for (int i = 0; i < Progression.ChipsOnBossEscape; i++)
            {
                string chip = GrantChip(RollChipId());
                if (chip != null) lines.Add(chip);
            }
            lines.AddRange(EvolveAfterBoss());
            lines.Add(SessionTexts.PortalOpen(Act + 1));
            MajorEventResolved?.Invoke(new MajorEventOutcome(cell, result.Escaped ? SessionTexts.EscapedThroughPortal : SessionTexts.BossDefeated, lines));
            CheckShards();
            OpenPortal();
        }
    }
}
