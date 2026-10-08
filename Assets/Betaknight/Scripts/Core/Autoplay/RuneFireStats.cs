using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Autoplay
{
    /// <summary>Wie oft ein Baustein in Bot-Kämpfen erfüllt war und gefeuert hat.</summary>
    public sealed class RuneFireStat
    {
        public string RuneId;
        public string Name = string.Empty;

        /// <summary>Grundschwierigkeit laut RuneCatalog (ohne Umkehren).</summary>
        public int Difficulty;

        /// <summary>Kämpfe, in denen der Baustein auf der Tafel lag.</summary>
        public int Fights;

        /// <summary>Davon Kämpfe, in denen die Bedingung mindestens einmal erfüllt war.</summary>
        public int FightsMet;

        /// <summary>Wie oft die Bedingung insgesamt erfüllt wurde (Wechsel zu «erfüllt»).</summary>
        public int Met;

        /// <summary>Wie oft die Zeile eine Aktion gestartet hat (inklusive Auslöser und Wiederholungen).</summary>
        public int Fired;

        /// <summary>Kampfzeit in Ticks, in der der Baustein auf der Tafel lag.</summary>
        public long Ticks;

        /// <summary>Feuern pro Minute Kampfzeit.</summary>
        public double FiredPerMinute => Ticks > 0 ? Fired * 60.0 * Arena.Ticks.PerSecond / Ticks : 0;

        /// <summary>Anteil der Kämpfe mit erfüllter Bedingung in Prozent.</summary>
        public double MetPercent => Fights > 0 ? 100.0 * FightsMet / Fights : 0;

        internal void Add(RuneFireStat o)
        {
            Fights += o.Fights;
            FightsMet += o.FightsMet;
            Met += o.Met;
            Fired += o.Fired;
            Ticks += o.Ticks;
        }
    }

    /// <summary>
    /// Messung für die Einstufung der Grundschwierigkeit: pro Baustein, wie oft er in Bot-Kämpfen erfüllt war und
    /// gefeuert hat. Der Testspieler (A-10) sammelt das in jedem Run mit; <see cref="Table"/> fasst mehrere Runs zusammen.
    /// </summary>
    public static class RuneFireStats
    {
        /// <summary>Zählt einen Kampf: Zeile i der Tafel gehört zu <paramref name="runeIds"/>[i].</summary>
        public static void Record(IDictionary<string, RuneFireStat> stats, IReadOnlyList<(string id, string name, int difficulty)> runes, BattleResult battle,
            BattleReport report = null)
        {
            if (stats == null || runes == null || battle == null) return;
            report = report ?? BattleReport.Create(battle);
            for (int i = 0; i < runes.Count && i < report.Rows.Count; i++)
            {
                RowReport row = report.Rows[i];
                if (row.IsFallback || runes[i].id == null) continue;
                if (!stats.TryGetValue(runes[i].id, out RuneFireStat s))
                    stats[runes[i].id] = s = new RuneFireStat { RuneId = runes[i].id, Name = runes[i].name, Difficulty = runes[i].difficulty };
                s.Fights++;
                int met = Math.Max(0, row.ConditionMet);
                if (met > 0) s.FightsMet++;
                s.Met += met;
                s.Fired += row.Fired;
                s.Ticks += battle.EndTick;
            }
        }

        /// <summary>Summe über mehrere Runs, sortiert nach Schwierigkeit und Feuern pro Minute.</summary>
        public static List<RuneFireStat> Merge(IEnumerable<AutoplayReport> runs)
        {
            var total = new Dictionary<string, RuneFireStat>();
            foreach (AutoplayReport r in runs)
            {
                foreach (RuneFireStat s in r.RuneStats.Values)
                {
                    if (!total.TryGetValue(s.RuneId, out RuneFireStat t))
                        total[s.RuneId] = t = new RuneFireStat { RuneId = s.RuneId, Name = s.Name, Difficulty = s.Difficulty };
                    t.Add(s);
                }
            }
            return total.Values.OrderBy(s => s.Difficulty).ThenByDescending(s => s.FiredPerMinute).ThenBy(s => s.RuneId, StringComparer.Ordinal).ToList();
        }

        /// <summary>Textabelle: «◆◆ Gegner betäubt: 14 Kämpfe, erfüllt in 43 %, 2,1× pro Minute gefeuert».</summary>
        public static string Table(IEnumerable<AutoplayReport> runs)
        {
            var sb = new StringBuilder(AutoplayTexts.RuneTableHeader);
            foreach (RuneFireStat s in Merge(runs))
            {
                sb.Append('\n').Append(AutoplayTexts.RuneTableLine(DifficultyText.Symbol(s.Difficulty), s.Name, s.RuneId, s.Fights,
                    AutoplayReport.Num(s.MetPercent), s.Met, s.Fired, AutoplayReport.Num(s.FiredPerMinute)));
            }
            return sb.ToString();
        }

        internal static void Write(JsonWriter w, IEnumerable<RuneFireStat> stats)
        {
            w.Open('[');
            foreach (RuneFireStat s in stats)
            {
                w.Item();
                w.Open('{');
                w.Field("rune", s.RuneId);
                w.Field("difficulty", s.Difficulty);
                w.Field("fights", s.Fights);
                w.Field("fightsMet", s.FightsMet);
                w.Field("met", s.Met);
                w.Field("fired", s.Fired);
                w.Field("firedPerMinute", s.FiredPerMinute);
                w.Close('}');
            }
            w.Close(']');
        }
    }
}
