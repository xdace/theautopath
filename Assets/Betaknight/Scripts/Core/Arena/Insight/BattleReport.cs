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
                _rows.Add(new RowReport(i, result.PlayerRowLabels[i], skill, i == count - 1));
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
                        if (e.IsRepeat) row.Repeated++;
                        if (e.IsTriggered)
                        {
                            row.Triggered++;
                            row.TriggeredBy[e.CauseRow] = (row.TriggeredBy.TryGetValue(e.CauseRow, out int n) ? n : 0) + 1;
                        }
                        break;
                    case BattleEventKind.TriggerExpired:
                        if (row != null) row.TriggersExpired++;
                        break;
                    case BattleEventKind.Damage:
                        if (e.Target == null || e.Target.Side == player.Side) break;
                        TotalDamage += e.Amount;
                        if (row != null) row.Damage += e.Amount;
                        else OtherDamage += e.Amount;
                        break;
                    case BattleEventKind.Healed:
                        TotalHealing += e.Amount;
                        if (row != null) row.Healing += e.Amount;
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
                    if (!d.Skipped(i)) continue;
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
            foreach (RowCheckState s in new[] { RowCheckState.ConditionFalse, RowCheckState.Cooldown, RowCheckState.Orphaned, RowCheckState.ActionRunning, RowCheckState.Ready })
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
