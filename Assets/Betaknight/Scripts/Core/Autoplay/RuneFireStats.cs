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

        /// <summary>Wie oft das Relais ausgelöst hat (Ereignis, steigende Flanke oder Takt).</summary>
        public int Met;

        /// <summary>Wie viele Aktionen das Relais verdient hat (versorgte Komponenten, inklusive Wiederholungen).</summary>
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
        /// <summary>
        /// Zählt einen Kampf je Relais der Platine (A-19): «Met» = wie oft das Relais ausgelöst hat, «Fired» = wie viele
        /// Aktionen es verdient hat. Name und Schwierigkeit liefert <paramref name="describe"/> zur Runen-Id.
        /// </summary>
        public static void Record(IDictionary<string, RuneFireStat> stats, BattleResult battle, Func<string, (string name, int difficulty)> describe)
        {
            LogicBoard board = battle?.PlayerBoard;
            if (stats == null || board == null) return;
            int relays = board.Relays.Count;
            var triggered = new int[relays];
            var fired = new int[relays];
            foreach (BattleEvent e in battle.Events)
            {
                if (e.Source == null || e.Source.Side != Side.Player || e.Relay < 0 || e.Relay >= relays) continue;
                if (e.Kind == BattleEventKind.RelayTriggered) triggered[e.Relay]++;
                else if (e.Kind == BattleEventKind.ActionStarted) fired[e.Relay]++;
            }
            for (int i = 0; i < relays; i++)
            {
                string id = board.Relays[i].RuneId;
                if (id == null) continue;
                if (!stats.TryGetValue(id, out RuneFireStat s))
                {
                    (string name, int difficulty) info = describe != null ? describe(id) : (id, 0);
                    stats[id] = s = new RuneFireStat { RuneId = id, Name = info.name ?? id, Difficulty = info.difficulty };
                }
                s.Fights++;
                if (triggered[i] > 0) s.FightsMet++;
                s.Met += triggered[i];
                s.Fired += fired[i];
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

        /// <summary>Textabelle: «●● Gegner betäubt: 14 Kämpfe, erfüllt in 43 %, 2,1× pro Minute gefeuert».</summary>
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
