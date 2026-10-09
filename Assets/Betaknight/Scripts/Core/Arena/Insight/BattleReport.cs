using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Auswertung einer Komponente der Platine nach dem Kampf (A-19).</summary>
    public sealed class RowReport
    {
        public int Index { get; }

        /// <summary>Versorgende Relais, z. B. «On Hit».</summary>
        public string Label { get; }
        public string Skill { get; }
        public bool IsFallback { get; }

        /// <summary>Wie oft die Komponente eine Aktion gestartet hat (inklusive ausgelöster und wiederholter).</summary>
        public int Fired { get; internal set; }

        /// <summary>Wie oft ein Relais oder Auslöser sie erreicht hat (eingereiht, aufgeladen oder verpasst).</summary>
        public int Triggered => Queued + Charged + Missed;

        /// <summary>Auslösen, die nur Ladung gespeichert haben (sie läuft, sobald die Ladung reicht). Kein «Missed Trigger».</summary>
        public int Charged { get; internal set; }

        /// <summary>Ladung, die diese Auslösen insgesamt gebracht haben.</summary>
        public int ChargeGained { get; internal set; }

        /// <summary>Gespeicherte Ladung am Kampfende und Grösse der Komponente.</summary>
        public int ChargeLeft { get; internal set; }
        public int Cells { get; internal set; }

        /// <summary>«2× (+4) · 2/4 left» oder leer.</summary>
        public string ChargeText => Charged == 0 && ChargeLeft == 0 ? string.Empty : ArenaTexts.ChargeStats(Charged, ChargeGained, ChargeLeft, Cells);

        /// <summary>Davon durch Auslöser-Module gestartet.</summary>
        public int FromTriggerModule { get; internal set; }

        /// <summary>Davon über Pulse gestartet (A-20).</summary>
        public int FromPulse { get; internal set; }

        /// <summary>Wie viele Pulse diese Komponente geschickt hat.</summary>
        public int PulsesSent { get; internal set; }

        /// <summary>Welche Komponenten diese über Pulse gestartet haben (Index, Anzahl).</summary>
        internal readonly SortedDictionary<int, int> PulsedBy = new SortedDictionary<int, int>();

        /// <summary>«#1 ×3» oder leer.</summary>
        public string PulsedByText
        {
            get
            {
                var parts = new List<string>();
                foreach (KeyValuePair<int, int> p in PulsedBy) parts.Add(ArenaTexts.TriggeredByPart(p.Key, p.Value));
                return string.Join(", ", parts);
            }
        }

        /// <summary>Davon Wiederholungen (Echo, Modul «Multicast»).</summary>
        public int Repeated { get; internal set; }

        /// <summary>Welche Komponenten diese über ein Auslöser-Modul gestartet haben (Index, Anzahl).</summary>
        internal readonly SortedDictionary<int, int> TriggeredBy = new SortedDictionary<int, int>();

        /// <summary>«#1 ×3, #2 ×1» oder leer.</summary>
        public string TriggeredByText
        {
            get
            {
                var parts = new List<string>();
                foreach (KeyValuePair<int, int> p in TriggeredBy) parts.Add(ArenaTexts.TriggeredByPart(p.Key, p.Value));
                return string.Join(", ", parts);
            }
        }

        /// <summary>Schaden an Gegnern aus Aktionen dieser Komponente (inklusive Brennen, das sie gesetzt hat).</summary>
        public int Damage { get; internal set; }
        public int Healing { get; internal set; }

        /// <summary>Anteil am Gesamtschaden in Basispunkten.</summary>
        public int DamageShareBp { get; internal set; }

        /// <summary>Wie oft die Komponente eingereiht wurde.</summary>
        public int Queued { get; internal set; }

        /// <summary>Starts aus der Warteschlange, davon mit Wartezeit, und die gesamte Wartezeit in Ticks.</summary>
        public int StartedFromQueue { get; internal set; }
        public int Waited { get; internal set; }
        public int WaitTicksTotal { get; internal set; }

        /// <summary>Mittlere Wartezeit der Starts, die warten mussten, in Ticks (0 ohne solche Starts).</summary>
        public int AverageWaitTicks => Waited > 0 ? WaitTicksTotal / Waited : 0;

        /// <summary>«4× queued, Ø 1.2 s wait» oder leer.</summary>
        public string QueueText => Queued == 0 ? string.Empty
            : Waited > 0 ? ArenaTexts.QueueStats(Queued, SkillInfo.Seconds(AverageWaitTicks)) : ArenaTexts.QueueStats(Queued);

        /// <summary>«Missed Trigger»: Auslösen, das nichts bewirkt hat (schon eingereiht, zu gross, eingefroren).</summary>
        public int Missed { get; internal set; }

        internal readonly Dictionary<MissReason, int> MissReasons = new Dictionary<MissReason, int>();

        public int MissCount(MissReason reason) => MissReasons.TryGetValue(reason, out int n) ? n : 0;

        /// <summary>Häufigster Grund für Missed Triggers, null ohne.</summary>
        public MissReason? MainMissReason { get; internal set; }

        /// <summary>Wird die Komponente von einem Relais versorgt? (Ohne Versorgung feuert sie nie.)</summary>
        public bool IsPowered { get; internal set; }

        /// <summary>Berührt sie ein Relais, für das sie zu gross ist?</summary>
        public bool IsTooLargeSomewhere { get; internal set; }

        /// <summary>Höchste Schwierigkeit der versorgenden Relais (0–3).</summary>
        public int Difficulty { get; internal set; }

        /// <summary>Ausführungen mit Schwierigkeits-Bonus.</summary>
        public int BonusExecutions { get; internal set; }

        /// <summary>Schaden bzw. Heilung, die der Bonus dazugegeben hat.</summary>
        public int BonusDamage { get; internal set; }
        public int BonusHealing { get; internal set; }

        /// <summary>Cast-Zeit, die der Bonus eingespart hat, in Ticks.</summary>
        public int CastSavedTicks { get; internal set; }

        /// <summary>«Triggered 12× · Bonus: 8 executions, +140 damage, 1.5 s cast saved» oder leer.</summary>
        public string DifficultyText
        {
            get
            {
                if (IsFallback) return string.Empty;
                var parts = new List<string> { ArenaTexts.TriggeredCount(Triggered) };
                if (BonusExecutions > 0)
                {
                    var bonus = new List<string> { ArenaTexts.BonusExecutions(BonusExecutions) };
                    if (BonusDamage > 0) bonus.Add(ArenaTexts.BonusDamage(BonusDamage));
                    if (BonusHealing > 0) bonus.Add(ArenaTexts.BonusHealing(BonusHealing));
                    if (CastSavedTicks > 0) bonus.Add(ArenaTexts.CastSaved(SkillInfo.Seconds(CastSavedTicks)));
                    parts.Add(ArenaTexts.BonusPrefix + string.Join(", ", bonus));
                }
                return string.Join(" · ", parts);
            }
        }

        /// <summary>«#2 Shield Bash» oder «Basic Attack».</summary>
        public string Name => IsFallback ? ArenaTexts.BasicAttack : ArenaTexts.ComponentName(Index, Skill);

        internal RowReport(int index, string label, string skill, bool fallback)
        {
            Index = index;
            Label = label;
            Skill = skill;
            IsFallback = fallback;
        }
    }

    /// <summary>
    /// Auswertung nach dem Kampf aus dem Protokoll: pro Komponente Feuern, Einreihen, Missed Triggers, Schaden, Heilung
    /// und Hinweise wie «#3 Drill Strike never fired: not powered». Rechnet nichts neu.
    /// </summary>
    public sealed class BattleReport
    {
        private readonly List<RowReport> _rows = new List<RowReport>();
        private readonly List<string> _hints = new List<string>();

        public IReadOnlyList<RowReport> Rows => _rows;
        public IReadOnlyList<string> Hints => _hints;

        /// <summary>Gesamtschaden des Spielers an Gegnern laut Protokoll.</summary>
        public int TotalDamage { get; private set; }

        /// <summary>Schaden ohne Komponente, z. B. aus Set-Boni oder Rückschlag.</summary>
        public int OtherDamage { get; private set; }
        public int TotalHealing { get; private set; }

        /// <summary>Schaden des Basisangriffs.</summary>
        public int BasicAttackDamage { get; private set; }

        /// <summary>Anteil des Basisangriffs am Gesamtschaden in Basispunkten; der Rest kommt aus Skills.</summary>
        public int BasicAttackShareBp => TotalDamage > 0 ? (int)((long)BasicAttackDamage * BasisPoints.Full / TotalDamage) : 0;

        /// <summary>«Basic Attack 28 % · Skills 72 %», leer ohne Schaden.</summary>
        public string DamageSplitText => TotalDamage > 0
            ? ArenaTexts.DamageSplit(SkillInfo.Percent(BasicAttackShareBp), SkillInfo.Percent(BasisPoints.Full - BasicAttackShareBp))
            : string.Empty;

        /// <summary>Schaden, den Schwierigkeits-Boni insgesamt dazugegeben haben.</summary>
        public int BonusDamage { get; private set; }

        /// <summary>Wie oft Relais des Spielers ausgelöst haben.</summary>
        public int RelayTriggers { get; private set; }

        /// <summary>Gibt es Missed Triggers? Nur dann zeigt die Auswertung die Spalte.</summary>
        public bool HasMissedTriggers { get; private set; }

        /// <summary>Bis zu so vielen Auslösungen gilt eine Komponente als selten ausgelöst (Hinweis).</summary>
        public const int RarelyTriggered = 1;

        public static BattleReport Create(BattleResult result)
        {
            var report = new BattleReport();
            report.Build(result);
            return report;
        }

        private void Build(BattleResult result)
        {
            int count = result.PlayerRowLabels.Count;
            LogicBoard board = result.PlayerBoard;
            for (int i = 0; i < count; i++)
            {
                string skill = i < result.PlayerRowSkills.Count ? result.PlayerRowSkills[i] : "?";
                var r = new RowReport(i, result.PlayerRowLabels[i], skill, i == count - 1);
                if (result.PlayerRowDifficulty != null && i < result.PlayerRowDifficulty.Count) r.Difficulty = result.PlayerRowDifficulty[i];
                if (board != null && i < board.Rows.Count)
                {
                    r.IsPowered = board.Rows[i].IsPowered;
                    r.IsTooLargeSomewhere = board.Rows[i].TooLargeFor.Count > 0;
                }
                else r.IsPowered = true;
                _rows.Add(r);
            }

            Combatant player = result.Fighters.Count > 0 ? result.Fighters[0].Combatant : null;
            foreach (BattleEvent e in result.Events)
            {
                if (player == null || e.Source != player) continue;
                RowReport row = e.RowIndex >= 0 && e.RowIndex < count ? _rows[e.RowIndex] : null;
                switch (e.Kind)
                {
                    case BattleEventKind.RelayTriggered:
                        RelayTriggers++;
                        break;
                    case BattleEventKind.ActionStarted:
                        if (row == null) break;
                        row.Fired++;
                        if (e.FromQueue)
                        {
                            row.StartedFromQueue++;
                            if (e.QueuedTicks > 0)
                            {
                                row.Waited++;
                                row.WaitTicksTotal += e.QueuedTicks;
                            }
                        }
                        if (e.IsRepeat) row.Repeated++;
                        if (e.Tier > 0)
                        {
                            row.BonusExecutions++;
                            row.CastSavedTicks += e.Bonus;
                        }
                        if (e.IsTriggered && e.CauseRow >= 0)
                        {
                            row.FromTriggerModule++;
                            row.TriggeredBy[e.CauseRow] = (row.TriggeredBy.TryGetValue(e.CauseRow, out int n) ? n : 0) + 1;
                        }
                        if (e.IsPulse && e.CauseRow >= 0)
                        {
                            row.FromPulse++;
                            row.PulsedBy[e.CauseRow] = (row.PulsedBy.TryGetValue(e.CauseRow, out int p) ? p : 0) + 1;
                        }
                        break;
                    case BattleEventKind.PulseSent:
                        if (row != null) row.PulsesSent++;
                        break;
                    case BattleEventKind.RowQueued:
                        if (row != null) row.Queued++;
                        break;
                    case BattleEventKind.Charged:
                        if (row == null) break;
                        row.Charged++;
                        row.ChargeGained += Math.Max(0, e.Power);
                        row.ChargeLeft = e.Amount;
                        row.Cells = e.Extra;
                        break;
                    case BattleEventKind.ChargeSpent:
                        if (row == null) break;
                        row.ChargeLeft = e.Amount;
                        row.Cells = e.Extra;
                        break;
                    case BattleEventKind.TriggerMissed:
                        if (row == null) break;
                        row.Missed++;
                        var reason = (MissReason)e.Amount;
                        row.MissReasons[reason] = row.MissCount(reason) + 1;
                        HasMissedTriggers = true;
                        break;
                    case BattleEventKind.Damage:
                        if (e.Target == null || e.Target.Side == player.Side) break;
                        TotalDamage += e.Amount;
                        if (e.Detail == SkillDefinition.BasicAttackId) BasicAttackDamage += e.Amount;
                        if (row != null)
                        {
                            row.Damage += e.Amount;
                            row.BonusDamage += e.Bonus;
                            BonusDamage += e.Bonus;
                        }
                        else OtherDamage += e.Amount;
                        break;
                    case BattleEventKind.Healed:
                        TotalHealing += e.Amount;
                        if (row != null)
                        {
                            row.Healing += e.Amount;
                            row.BonusHealing += e.Bonus;
                        }
                        break;
                }
            }

            foreach (RowReport row in _rows)
            {
                row.DamageShareBp = TotalDamage > 0 ? (int)((long)row.Damage * BasisPoints.Full / TotalDamage) : 0;
                row.MainMissReason = MostMissed(row);
            }

            BuildHints();
        }

        private void BuildHints()
        {
            foreach (RowReport row in _rows)
            {
                if (row.IsFallback || row.Fired > 0) continue;
                if (row.Skill == "—") _hints.Add(ArenaTexts.HintNeverFired(row.Name, ArenaTexts.NeverFiredOrphaned));
                else if (!row.IsPowered)
                    _hints.Add(ArenaTexts.HintNeverFired(row.Name, row.IsTooLargeSomewhere ? ArenaTexts.NeverFiredTooLarge : ArenaTexts.NeverFiredUnpowered));
                else if (row.Triggered <= RarelyTriggered)
                    _hints.Add(ArenaTexts.HintRarelyTriggered(row.Name, row.Triggered, row.Difficulty > 0 ? DifficultyText.Name(row.Difficulty) : null));
                else _hints.Add(ArenaTexts.HintNeverFired(row.Name, ArenaTexts.NeverFiredOther));
            }

            // Wer trägt den Schaden? Nur sinnvoll, wenn mehrere Komponenten Schaden machen.
            RowReport best = null;
            int dealing = 0;
            foreach (RowReport row in _rows)
            {
                if (row.Damage <= 0) continue;
                dealing++;
                if (best == null || row.Damage > best.Damage) best = row;
            }
            if (best != null && dealing > 1)
                _hints.Add(ArenaTexts.HintTopDamage(best.Name, SkillInfo.Percent(best.DamageShareBp)));

            // Viele Missed Triggers, weil die Komponente schon wartete: das Relais löst schneller aus, als sie feuern kann.
            foreach (RowReport row in _rows)
            {
                if (row.IsFallback) continue;
                int queued = row.MissCount(MissReason.AlreadyQueued);
                if (queued >= 3 && queued * 2 >= row.Triggered) _hints.Add(ArenaTexts.HintMissedQueued(row.Name, queued));
            }

            // Auslöser-Ketten und Pulse: wer startet wen über ein Modul oder eine Verbindung.
            foreach (RowReport row in _rows)
            {
                if (row.FromTriggerModule > 0) _hints.Add(ArenaTexts.HintTriggered(row.Name, row.FromTriggerModule, row.TriggeredByText));
                if (row.FromPulse > 0) _hints.Add(ArenaTexts.HintPulsed(row.Name, row.FromPulse, row.PulsedByText));
            }

            // Schwierigkeits-Bonus: was hat er ausgemacht?
            foreach (RowReport row in _rows)
            {
                if (row.IsFallback || row.BonusDamage <= 0 || row.Damage <= 0) continue;
                _hints.Add(ArenaTexts.HintBonus(row.Name, DifficultyText.Symbol(row.Difficulty), row.BonusDamage,
                    SkillInfo.Percent((int)((long)row.BonusDamage * BasisPoints.Full / row.Damage))));
            }

            // Warteschlange: welche Komponente wartet am längsten?
            RowReport waiting = null;
            foreach (RowReport row in _rows)
                if (row.Waited > 0 && (waiting == null || row.AverageWaitTicks > waiting.AverageWaitTicks)) waiting = row;
            if (waiting != null && waiting.AverageWaitTicks >= Ticks.PerSecond)
                _hints.Add(ArenaTexts.HintLongWait(waiting.Name, SkillInfo.Seconds(waiting.AverageWaitTicks)));

            if (OtherDamage > 0) _hints.Add(ArenaTexts.HintOtherDamage(OtherDamage));
        }

        private static MissReason? MostMissed(RowReport row)
        {
            MissReason? best = null;
            int max = 0;
            foreach (MissReason r in new[] { MissReason.AlreadyQueued, MissReason.TooLarge, MissReason.Frozen, MissReason.Orphaned })
            {
                int n = row.MissCount(r);
                if (n <= max) continue;
                max = n;
                best = r;
            }
            return best;
        }
    }

    /// <summary>Lesbare Gründe für Missed Triggers.</summary>
    public static class RowStateText
    {
        public static string Reason(MissReason reason)
        {
            switch (reason)
            {
                case MissReason.TooLarge: return ArenaTexts.MissTooLarge;
                case MissReason.Frozen: return ArenaTexts.MissFrozen;
                case MissReason.Orphaned: return ArenaTexts.MissOrphaned;
                case MissReason.Overflow: return ArenaTexts.MissOverflow;
                case MissReason.Overheated: return ArenaTexts.MissOverheated;
                default: return ArenaTexts.MissAlreadyQueued;
            }
        }

        /// <summary>«1.5 s» mit einer Nachkommastelle.</summary>
        public static string Seconds(int ticks) => ArenaTexts.SecondsOneDecimal(ticks);
    }
}
