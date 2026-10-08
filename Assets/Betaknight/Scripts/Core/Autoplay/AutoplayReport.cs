using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Betaknight.Core.Autoplay
{
    /// <summary>Bericht über einen Run des Testspielers. Als JSON (<see cref="ToJson"/>) und als kurze Log-Zusammenfassung.</summary>
    public sealed class AutoplayReport
    {
        /// <summary>So viele Texte (Exceptions, Fehler-Logs, Hänger) werden höchstens aufgehoben; gezählt wird alles.</summary>
        public const int MaxTexts = 40;

        public int Seed;
        public string Kit = string.Empty;
        public int Turns;
        public int Act = 1;
        public int Actions;
        public int FightsWon;
        public int FightsLost;
        public int ElitesWon;
        public int ElitesLost;
        public int Bosses;
        public int BossesSurvived;
        public readonly SortedDictionary<string, int> Rewards = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public readonly List<string> BoardRows = new List<string>();
        public readonly List<string> Modules = new List<string>();
        public int TriggersSet;
        public int TriggerLinks;
        public readonly List<string> Duos = new List<string>();
        public double DurationSeconds;

        /// <summary>Nur mit Darstellung; ohne bleibt es null.</summary>
        public double? FpsAverage;

        public double? FpsMin;
        public int ExceptionCount;
        public readonly List<string> Exceptions = new List<string>();
        public int ErrorLogCount;
        public readonly List<string> ErrorLogs = new List<string>();
        public int HangCount;
        public readonly List<string> Hangs = new List<string>();
        public string EndReason = string.Empty;
        public int Hp;
        public int MaxHp;
        public int Gold;

        /// <summary>Run ohne Exception und ohne Hänger (Exit-Code 0).</summary>
        public bool Ok => ExceptionCount == 0 && HangCount == 0;

        public void AddReward(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return;
            Rewards.TryGetValue(kind, out int n);
            Rewards[kind] = n + 1;
        }

        public void AddException(string text)
        {
            ExceptionCount++;
            if (Exceptions.Count < MaxTexts) Exceptions.Add(text ?? string.Empty);
        }

        public void AddErrorLog(string text)
        {
            ErrorLogCount++;
            if (ErrorLogs.Count < MaxTexts) ErrorLogs.Add(text ?? string.Empty);
        }

        public void AddHang(string text)
        {
            HangCount++;
            if (Hangs.Count < MaxTexts) Hangs.Add(text ?? string.Empty);
        }

        /// <summary>Kurze Zusammenfassung für das Log, eine Zeile.</summary>
        public string Summary()
        {
            string fps = FpsAverage.HasValue ? $", FPS Ø {Num(FpsAverage.Value)} / min {Num(FpsMin ?? 0)}" : string.Empty;
            return $"Seed {Seed}, {Kit}: {EndReason} in Akt {Act} nach {Turns} Zügen. Kämpfe {FightsWon}:{FightsLost}, " +
                $"Elite {ElitesWon}:{ElitesLost}, Boss {BossesSurvived}/{Bosses}, Tafel {BoardRows.Count} Zeilen, " +
                $"Module {Modules.Count}, Auslöser {TriggersSet}, Duos {Duos.Count}, {Num(DurationSeconds)} s{fps}. " +
                $"Exceptions {ExceptionCount}, Fehler-Logs {ErrorLogCount}, Hänger {HangCount} → {(Ok ? "OK" : "FEHLER")}";
        }

        public string ToJson(int indent = 2)
        {
            var w = new JsonWriter(indent);
            w.Open('{');
            Write(w);
            w.Close('}');
            return w.ToString();
        }

        internal void Write(JsonWriter w)
        {
            w.Field("seed", Seed);
            w.Field("kit", Kit);
            w.Field("ok", Ok);
            w.Field("endReason", EndReason);
            w.Field("act", Act);
            w.Field("turns", Turns);
            w.Field("actions", Actions);
            w.Field("hp", Hp);
            w.Field("maxHp", MaxHp);
            w.Field("gold", Gold);
            w.Field("fightsWon", FightsWon);
            w.Field("fightsLost", FightsLost);
            w.Field("elitesWon", ElitesWon);
            w.Field("elitesLost", ElitesLost);
            w.Field("bosses", Bosses);
            w.Field("bossesSurvived", BossesSurvived);
            w.Name("rewards");
            w.Open('{');
            foreach (KeyValuePair<string, int> r in Rewards) w.Field(r.Key, r.Value);
            w.Close('}');
            w.Field("boardRows", BoardRows);
            w.Field("modules", Modules);
            w.Field("triggersSet", TriggersSet);
            w.Field("triggerLinks", TriggerLinks);
            w.Field("duos", Duos);
            w.Field("durationSeconds", DurationSeconds);
            w.Field("fpsAverage", FpsAverage);
            w.Field("fpsMin", FpsMin);
            w.Field("exceptionCount", ExceptionCount);
            w.Field("exceptions", Exceptions);
            w.Field("errorLogCount", ErrorLogCount);
            w.Field("errorLogs", ErrorLogs);
            w.Field("hangCount", HangCount);
            w.Field("hangs", Hangs);
        }

        internal static string Num(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>Mehrere Runs: Bericht pro Run plus Summe.</summary>
    public sealed class AutoplaySummary
    {
        public readonly List<AutoplayReport> Runs = new List<AutoplayReport>();

        public bool Ok => Runs.All(r => r.Ok);

        /// <summary>0 = alle Runs ohne Exception und ohne Hänger, sonst 1.</summary>
        public int ExitCode => Ok ? 0 : 1;

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append($"Autoplay: {Runs.Count} Runs, {Runs.Count(r => r.Ok)} OK, höchster Akt {(Runs.Count > 0 ? Runs.Max(r => r.Act) : 0)}, ");
            sb.Append($"Kämpfe {Runs.Sum(r => r.FightsWon)}:{Runs.Sum(r => r.FightsLost)}, ");
            sb.Append($"Exceptions {Runs.Sum(r => r.ExceptionCount)}, Fehler-Logs {Runs.Sum(r => r.ErrorLogCount)}, Hänger {Runs.Sum(r => r.HangCount)}, ");
            sb.Append($"Exit-Code {ExitCode}");
            return sb.ToString();
        }

        public string ToJson()
        {
            var w = new JsonWriter(2);
            w.Open('{');
            w.Name("total");
            w.Open('{');
            w.Field("runs", Runs.Count);
            w.Field("ok", Ok);
            w.Field("exitCode", ExitCode);
            w.Field("runsOk", Runs.Count(r => r.Ok));
            w.Field("maxAct", Runs.Count > 0 ? Runs.Max(r => r.Act) : 0);
            w.Field("turns", Runs.Sum(r => r.Turns));
            w.Field("fightsWon", Runs.Sum(r => r.FightsWon));
            w.Field("fightsLost", Runs.Sum(r => r.FightsLost));
            w.Field("elitesWon", Runs.Sum(r => r.ElitesWon));
            w.Field("elitesLost", Runs.Sum(r => r.ElitesLost));
            w.Field("bosses", Runs.Sum(r => r.Bosses));
            w.Field("bossesSurvived", Runs.Sum(r => r.BossesSurvived));
            w.Name("rewards");
            w.Open('{');
            foreach (IGrouping<string, KeyValuePair<string, int>> g in Runs.SelectMany(r => r.Rewards).GroupBy(p => p.Key).OrderBy(g => g.Key, StringComparer.Ordinal))
                w.Field(g.Key, g.Sum(p => p.Value));
            w.Close('}');
            w.Field("triggersSet", Runs.Sum(r => r.TriggersSet));
            w.Field("durationSeconds", Runs.Sum(r => r.DurationSeconds));
            List<AutoplayReport> withFps = Runs.Where(r => r.FpsAverage.HasValue).ToList();
            w.Field("fpsAverage", withFps.Count > 0 ? withFps.Average(r => r.FpsAverage.Value) : (double?)null);
            w.Field("fpsMin", withFps.Count > 0 ? withFps.Min(r => r.FpsMin ?? 0) : (double?)null);
            w.Field("exceptionCount", Runs.Sum(r => r.ExceptionCount));
            w.Field("errorLogCount", Runs.Sum(r => r.ErrorLogCount));
            w.Field("hangCount", Runs.Sum(r => r.HangCount));
            w.Field("endReasons", Runs.Select(r => r.EndReason).ToList());
            w.Close('}');
            w.Name("runs");
            w.Open('[');
            foreach (AutoplayReport r in Runs)
            {
                w.Item();
                w.Open('{');
                r.Write(w);
                w.Close('}');
            }
            w.Close(']');
            w.Close('}');
            return w.ToString();
        }
    }

    /// <summary>Kleiner JSON-Schreiber ohne Abhängigkeiten (Core bleibt Unity-frei).</summary>
    internal sealed class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly int _indent;
        private readonly Stack<bool> _first = new Stack<bool>();
        private bool _afterName;

        public JsonWriter(int indent) => _indent = indent;

        public void Open(char bracket)
        {
            if (!_afterName) Separator();
            _afterName = false;
            _sb.Append(bracket);
            _first.Push(true);
        }

        public void Close(char bracket)
        {
            bool empty = _first.Pop();
            if (!empty) NewLine();
            _sb.Append(bracket);
        }

        /// <summary>Vor einem Array-Element, das selbst ein Objekt ist.</summary>
        public void Item()
        {
            Separator();
            _afterName = true;
        }

        public void Name(string name)
        {
            Separator();
            _sb.Append(Quote(name)).Append(_indent > 0 ? ": " : ":");
            _afterName = true;
        }

        public void Field(string name, string value)
        {
            Name(name);
            _afterName = false;
            _sb.Append(value == null ? "null" : Quote(value));
        }

        public void Field(string name, int value)
        {
            Name(name);
            _afterName = false;
            _sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        public void Field(string name, bool value)
        {
            Name(name);
            _afterName = false;
            _sb.Append(value ? "true" : "false");
        }

        public void Field(string name, double? value)
        {
            Name(name);
            _afterName = false;
            _sb.Append(value.HasValue ? Math.Round(value.Value, 2).ToString("0.##", CultureInfo.InvariantCulture) : "null");
        }

        public void Field(string name, IEnumerable<string> values)
        {
            Name(name);
            _afterName = false;
            _sb.Append('[');
            _sb.Append(string.Join(", ", values.Select(Quote)));
            _sb.Append(']');
        }

        private void Separator()
        {
            if (_first.Count == 0) return;
            bool first = _first.Pop();
            if (!first) _sb.Append(',');
            _first.Push(false);
            NewLine();
        }

        private void NewLine()
        {
            if (_indent <= 0) return;
            _sb.Append('\n').Append(' ', _indent * _first.Count);
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        public override string ToString() => _sb.ToString();
    }
}
