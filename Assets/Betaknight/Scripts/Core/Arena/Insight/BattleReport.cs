using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Auswertung einer Tafel-Zeile nach dem Kampf.</summary>
    public sealed class RowReport
    {
        public int Index { get; }
        public string Label { get; }
        public string Skill { get; }
        public bool IsFallback { get; }

        /// <summary>Wie oft die Zeile eine Aktion gestartet hat (inklusive ausgelöster und wiederholter).</summary>
        public int Fired { get; internal set; }

        /// <summary>Davon durch Auslöser gestartet.</summary>
        public int Triggered { get; internal set; }

        /// <summary>Davon Wiederholungen (Echo, Modul «Mehrfach»).</summary>
        public int Repeated { get; internal set; }

        /// <summary>Auslöser auf diese Zeile, die verfallen sind (Skill nicht bereit).</summary>
        public int TriggersExpired { get; internal set; }

        /// <summary>Welche Zeilen diese ausgelöst haben (Index, Anzahl), für die Kette in der Auswertung.</summary>
        internal readonly SortedDictionary<int, int> TriggeredBy = new SortedDictionary<int, int>();

        /// <summary>«Zeile 1 ×3, Zeile 2 ×1» oder leer.</summary>
        public string TriggeredByText
        {
            get
            {
                var parts = new List<string>();
                foreach (KeyValuePair<int, int> p in TriggeredBy) parts.Add($"Zeile {p.Key + 1} ×{p.Value}");
                return string.Join(", ", parts);
            }
        }

        /// <summary>Schaden an Gegnern aus Aktionen dieser Zeile (inklusive Brennen, das sie gesetzt hat).</summary>
        public int Damage { get; internal set; }
        public int Healing { get; internal set; }

        /// <summary>Anteil am Gesamtschaden in Basispunkten.</summary>
        public int DamageShareBp { get; internal set; }

        /// <summary>Wie oft die Zeile eingereiht wurde (A-13: erfüllt, aber gerade nicht startbar).</summary>
        public int Queued { get; internal set; }

        /// <summary>Starts aus der Warteschlange und ihre gesamte Wartezeit in Ticks.</summary>
        public int StartedFromQueue { get; internal set; }
        public int WaitTicksTotal { get; internal set; }

        /// <summary>Mittlere Wartezeit der Starts aus der Warteschlange in Ticks (0 ohne solche Starts).</summary>
        public int AverageWaitTicks => StartedFromQueue > 0 ? WaitTicksTotal / StartedFromQueue : 0;

        /// <summary>«4× eingereiht, Ø 1,2 s gewartet» oder leer.</summary>
        public string QueueText => Queued == 0 ? string.Empty
            : StartedFromQueue > 0 ? $"{Queued}× eingereiht, Ø {SkillInfo.Seconds(AverageWaitTicks)} gewartet" : $"{Queued}× eingereiht";

        /// <summary>Grundschwierigkeit des Bausteins (0–3); bestimmt den Bonus.</summary>
        public int Difficulty { get; internal set; }

        /// <summary>Wie oft die Bedingung erfüllt wurde (Wechsel von «nicht erfüllt» zu «erfüllt»), -1 wenn unbekannt.</summary>
        public int ConditionMet { get; internal set; } = -1;

        /// <summary>Ausführungen mit Schwierigkeits-Bonus (eigener oder über einen Auslöser mitgebrachter).</summary>
        public int BonusExecutions { get; internal set; }

        /// <summary>Schaden bzw. Heilung, die der Bonus dazugegeben hat.</summary>
        public int BonusDamage { get; internal set; }
        public int BonusHealing { get; internal set; }

        /// <summary>Cooldown, den der Bonus eingespart hat, in Ticks.</summary>
        public int CooldownSavedTicks { get; internal set; }

        /// <summary>«Bedingung 12× erfüllt · Bonus: 8 Ausführungen, +140 Schaden, 6,0 s Cooldown gespart» oder leer.</summary>
        public string DifficultyText
        {
            get
            {
                if (IsFallback) return string.Empty;
                var parts = new List<string>();
                if (ConditionMet >= 0) parts.Add($"Bedingung {ConditionMet}× erfüllt");
                if (BonusExecutions > 0)
                {
                    var bonus = new List<string> { $"{BonusExecutions} Ausführungen" };
                    if (BonusDamage > 0) bonus.Add($"+{BonusDamage} Schaden");
                    if (BonusHealing > 0) bonus.Add($"+{BonusHealing} Heilung");
                    if (CooldownSavedTicks > 0) bonus.Add($"{SkillInfo.Seconds(CooldownSavedTicks)} Cooldown gespart");
                    parts.Add("Bonus: " + string.Join(", ", bonus));
                }
                return string.Join(" · ", parts);
            }
        }

        /// <summary>Wie oft die Zeile übersprungen wurde (über der gewählten Zeile oder «Aktion läuft»).</summary>
        public int Skipped { get; internal set; }

        /// <summary>Häufigster Grund beim Überspringen, null wenn nie übersprungen.</summary>
        public RowCheckState? MainReason { get; internal set; }

        internal readonly Dictionary<RowCheckState, int> SkipReasons = new Dictionary<RowCheckState, int>();
        internal readonly Dictionary<RowCheckState, int> AllStates = new Dictionary<RowCheckState, int>();

        public int SkipCount(RowCheckState reason) => SkipReasons.TryGetValue(reason, out int n) ? n : 0;

        /// <summary>«Zeile 3» oder «Basisangriff» für die Fallback-Zeile.</summary>
        public string Name => IsFallback ? "Basisangriff-Zeile" : $"Zeile {Index + 1}";

        internal RowReport(int index, string label, string skill, bool fallback)
        {
            Index = index;
            Label = label;
            Skill = skill;
            IsFallback = fallback;
        }
    }

    /// <summary>
    /// Auswertung nach dem Kampf aus Protokoll und Entscheidungen: pro Zeile Feuern, Schaden, Heilung, Überspringen
    /// und Hinweise wie «Zeile 3 hat nie gefeuert: Bedingung nie erfüllt». Rechnet nichts neu.
    /// </summary>
    public sealed class BattleReport
    {
        private readonly List<RowReport> _rows = new List<RowReport>();
        private readonly List<string> _hints = new List<string>();

        public IReadOnlyList<RowReport> Rows => _rows;
        public IReadOnlyList<string> Hints => _hints;

        /// <summary>Gesamtschaden des Spielers an Gegnern laut Protokoll.</summary>
        public int TotalDamage { get; private set; }

        /// <summary>Schaden ohne Zeile, z. B. aus Set-Boni oder Rückschlag.</summary>
        public int OtherDamage { get; private set; }
        public int TotalHealing { get; private set; }

        /// <summary>Schaden des Basisangriffs (aus allen Zeilen, auch der Basisangriff-Zeile).</summary>
        public int BasicAttackDamage { get; private set; }

        /// <summary>Anteil des Basisangriffs am Gesamtschaden in Basispunkten; der Rest kommt aus Skills.</summary>
        public int BasicAttackShareBp => TotalDamage > 0 ? (int)((long)BasicAttackDamage * BasisPoints.Full / TotalDamage) : 0;

        /// <summary>«Basisangriff 28 % · Skills 72 %», leer ohne Schaden.</summary>
        public string DamageSplitText => TotalDamage > 0
            ? $"Basisangriff {SkillInfo.Percent(BasicAttackShareBp)} · Skills {SkillInfo.Percent(BasisPoints.Full - BasicAttackShareBp)}"
            : string.Empty;

        /// <summary>Schaden, den Schwierigkeits-Boni insgesamt dazugegeben haben.</summary>
        public int BonusDamage { get; private set; }
        public int Decisions { get; private set; }

        public static BattleReport Create(BattleResult result)
        {
            var report = new BattleReport();
            report.Build(result);
            return report;
        }

        private void Build(BattleResult result)
        {
            int count = result.PlayerRowLabels.Count;
            for (int i = 0; i < count; i++)
            {
                string skill = i < result.PlayerRowSkills.Count ? result.PlayerRowSkills[i] : "?";
                var r = new RowReport(i, result.PlayerRowLabels[i], skill, i == count - 1);
                if (result.PlayerRowDifficulty != null && i < result.PlayerRowDifficulty.Count) r.Difficulty = result.PlayerRowDifficulty[i];
                if (result.PlayerRowMet != null && i < result.PlayerRowMet.Count) r.ConditionMet = result.PlayerRowMet[i];
                _rows.Add(r);
            }

            Combatant player = result.Fighters.Count > 0 ? result.Fighters[0].Combatant : null;
            foreach (BattleEvent e in result.Events)
            {
                if (player == null || e.Source != player) continue;
                RowReport row = e.RowIndex >= 0 && e.RowIndex < count ? _rows[e.RowIndex] : null;
                switch (e.Kind)
                {
                    case BattleEventKind.ActionStarted:
                        if (row == null) break;
                        row.Fired++;
                        if (e.FromQueue)
                        {
                            row.StartedFromQueue++;
                            row.WaitTicksTotal += e.QueuedTicks;
                        }
                        if (e.IsRepeat) row.Repeated++;
                        if (e.Tier > 0)
                        {
                            row.BonusExecutions++;
                            row.CooldownSavedTicks += e.Bonus;
                        }
                        if (e.IsTriggered)
                        {
                            row.Triggered++;
                            row.TriggeredBy[e.CauseRow] = (row.TriggeredBy.TryGetValue(e.CauseRow, out int n) ? n : 0) + 1;
                        }
                        break;
                    case BattleEventKind.RowQueued:
                        if (row != null) row.Queued++;
                        break;
                    case BattleEventKind.TriggerExpired:
                        if (row != null) row.TriggersExpired++;
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

            foreach (BattleDecision d in result.Decisions)
            {
                Decisions++;
                for (int i = 0; i < count && i < d.Rows.Count; i++)
                {
                    RowCheckState state = d.Rows[i].State;
                    Add(_rows[i].AllStates, state);
                    // Eingereiht ist nicht übersprungen: die Zeile wartet und kommt dran (A-13).
                    if (!d.Skipped(i) || d.Queued(i)) continue;
                    _rows[i].Skipped++;
                    Add(_rows[i].SkipReasons, state);
                }
            }

            foreach (RowReport row in _rows)
            {
                row.DamageShareBp = TotalDamage > 0 ? (int)((long)row.Damage * BasisPoints.Full / TotalDamage) : 0;
                row.MainReason = Most(row.SkipReasons);
            }

            BuildHints();
        }

        private void BuildHints()
        {
            foreach (RowReport row in _rows)
            {
                if (row.IsFallback || row.Fired > 0) continue;
                _hints.Add($"{row.Name} hat nie gefeuert: {NeverFiredReason(row)}.");
            }

            // Wer trägt den Schaden? Nur sinnvoll, wenn mehrere Zeilen Schaden machen.
            RowReport best = null;
            int dealing = 0;
            foreach (RowReport row in _rows)
            {
                if (row.Damage <= 0) continue;
                dealing++;
                if (best == null || row.Damage > best.Damage) best = row;
            }
            if (best != null && dealing > 1)
                _hints.Add($"{best.Name} ({best.Skill}) macht {SkillInfo.Percent(best.DamageShareBp)} des Schadens.");

            foreach (RowReport row in _rows)
            {
                if (row.Fired == 0 || row.Skipped < 3) continue;
                if (row.MainReason == RowCheckState.ActionRunning && row.SkipCount(RowCheckState.ActionRunning) * 2 >= row.Skipped)
                    _hints.Add($"{row.Name} war {row.SkipCount(RowCheckState.ActionRunning)}× bereit, während eine andere Aktion lief. Kürzere Aktionen darunter helfen.");
            }

            // Auslöser-Ketten: wer löst wen aus, und wie viele verfallen, weil das Ziel nicht bereit war.
            foreach (RowReport row in _rows)
            {
                if (row.Triggered > 0) _hints.Add($"{row.Name} ({row.Skill}) wurde {row.Triggered}× ausgelöst, von {row.TriggeredByText}.");
                if (row.TriggersExpired > 0)
                    _hints.Add($"{row.TriggersExpired} Auslöser auf {row.Name} verfielen: der Skill war nicht bereit (Cooldown oder Aktion lief).");
            }

            // Schwierigkeits-Bonus: was hat er ausgemacht, und welche schweren Bausteine kamen nie zum Zug?
            foreach (RowReport row in _rows)
            {
                if (row.IsFallback || row.Difficulty == 0) continue;
                if (row.BonusDamage > 0 && row.Damage > 0)
                    _hints.Add($"{row.Name}: Bonus {DifficultyText.Symbol(row.Difficulty)} brachte +{row.BonusDamage} Schaden "
                        + $"({SkillInfo.Percent((int)((long)row.BonusDamage * BasisPoints.Full / row.Damage))} des Zeilenschadens).");
                else if (row.ConditionMet == 0 && row.Difficulty >= 2)
                    _hints.Add($"{row.Name}: schwerer Baustein ({DifficultyText.Name(row.Difficulty)}) nie erfüllt. Erleichterer helfen, ohne den Bonus zu senken.");
            }

            // Warteschlange: welche Zeile wartet am längsten?
            RowReport waiting = null;
            foreach (RowReport row in _rows)
                if (row.StartedFromQueue > 0 && (waiting == null || row.AverageWaitTicks > waiting.AverageWaitTicks)) waiting = row;
            if (waiting != null && waiting.AverageWaitTicks >= Ticks.PerSecond)
                _hints.Add($"{waiting.Name} ({waiting.Skill}) wartete im Schnitt {SkillInfo.Seconds(waiting.AverageWaitTicks)} in der Warteschlange. "
                    + "Höhere Zeilen oder lange Casts halten sie auf.");

            if (OtherDamage > 0) _hints.Add($"{OtherDamage} Schaden kam ohne Zeile (Set-Boni, Rückschlag).");
        }

        private static string NeverFiredReason(RowReport row)
        {
            if (row.AllStates.Count == 0) return "es gab keine Entscheidung";
            int Count(RowCheckState s) => row.AllStates.TryGetValue(s, out int n) ? n : 0;
            int total = 0;
            foreach (int n in row.AllStates.Values) total += n;

            if (Count(RowCheckState.Orphaned) == total) return "verwaist, kein Skill zugeordnet";
            if (Count(RowCheckState.ConditionFalse) == total) return "Bedingung nie erfüllt";
            if (Count(RowCheckState.Cooldown) == total) return "Skill war immer im Cooldown";
            if (Count(RowCheckState.ActionRunning) > 0) return "Bedingung war erfüllt, aber es lief jedes Mal eine andere Aktion";
            if (Count(RowCheckState.Ready) > 0) return "Bedingung war erfüllt, aber höhere Zeilen hatten Vorrang";
            return Count(RowCheckState.ConditionFalse) >= Count(RowCheckState.Cooldown)
                ? "Bedingung nie erfüllt, wenn der Skill bereit war"
                : "Skill meist im Cooldown";
        }

        private static void Add(Dictionary<RowCheckState, int> counts, RowCheckState state) =>
            counts[state] = (counts.TryGetValue(state, out int n) ? n : 0) + 1;

        private static RowCheckState? Most(Dictionary<RowCheckState, int> counts)
        {
            RowCheckState? best = null;
            int max = 0;
            // Feste Reihenfolge, damit Gleichstände immer gleich ausgehen.
            foreach (RowCheckState s in new[] { RowCheckState.ConditionFalse, RowCheckState.Cooldown, RowCheckState.Orphaned, RowCheckState.ActionRunning, RowCheckState.Queued, RowCheckState.Ready })
            {
                if (counts.TryGetValue(s, out int n) && n > max)
                {
                    max = n;
                    best = s;
                }
            }
            return best;
        }
    }
}
