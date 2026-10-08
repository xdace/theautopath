using System;
using System.Collections.Generic;
using System.Globalization;

namespace Betaknight.Core.Autoplay
{
    /// <summary>
    /// Kommandozeile des Testspielers: <c>-autoplay [-seed N] [-runs N] [-speed N] [-report pfad.json] [-quit]</c>.
    /// Ohne <c>-autoplay</c> ist <see cref="Enabled"/> false und das Spiel startet normal.
    /// </summary>
    public sealed class AutoplayOptions
    {
        /// <summary>Ab diesem Akt endet ein Run als «geschafft».</summary>
        public const int DefaultTargetAct = 3;

        public bool Enabled { get; private set; }

        /// <summary>Seed des ersten Runs; weitere Runs zählen hoch. Null = zufällig.</summary>
        public int? Seed { get; private set; }

        public int Runs { get; private set; } = 1;

        /// <summary>Tempo: Bot-Takt, Schritt-Animation und Arena-Wiedergabe laufen so viel schneller.</summary>
        public float Speed { get; private set; } = 1f;

        public string ReportPath { get; private set; }

        /// <summary>Nach dem letzten Run das Spiel beenden (Exit-Code 0 oder 1). Ohne bleibt das Fenster offen.</summary>
        public bool Quit { get; private set; }

        public int TargetAct { get; private set; } = DefaultTargetAct;

        /// <summary>Unbekannte oder fehlerhafte Argumente, als Warnung fürs Log.</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Seed von Run <paramref name="index"/> (0-basiert).</summary>
        public int SeedFor(int index, Func<int> random) => Seed.HasValue ? unchecked(Seed.Value + index) : random();

        public static AutoplayOptions Parse(IReadOnlyList<string> args)
        {
            var o = new AutoplayOptions();
            if (args == null) return o;
            for (int i = 0; i < args.Count; i++)
            {
                string a = args[i]?.Trim() ?? string.Empty;
                switch (a.ToLowerInvariant())
                {
                    case "-autoplay": o.Enabled = true; break;
                    case "-quit": o.Quit = true; break;
                    case "-seed": if (Int(args, ref i, o, a, out int seed)) o.Seed = seed; break;
                    case "-runs": if (Int(args, ref i, o, a, out int runs)) o.Runs = Math.Max(1, runs); break;
                    case "-act": if (Int(args, ref i, o, a, out int act)) o.TargetAct = Math.Max(2, act); break;
                    case "-speed":
                        if (i + 1 < args.Count && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) && speed > 0f)
                        {
                            o.Speed = Math.Min(100f, speed);
                            i++;
                        }
                        else o.Warnings.Add(AutoplayTexts.ExpectsPositiveNumber(a));
                        break;
                    case "-report":
                        if (i + 1 < args.Count && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
                        {
                            o.ReportPath = args[i + 1];
                            i++;
                        }
                        else o.Warnings.Add(AutoplayTexts.ExpectsPath(a));
                        break;
                }
            }
            return o;
        }

        private static bool Int(IReadOnlyList<string> args, ref int i, AutoplayOptions o, string name, out int value)
        {
            value = 0;
            if (i + 1 < args.Count && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                i++;
                return true;
            }
            o.Warnings.Add(AutoplayTexts.ExpectsInteger(name));
            return false;
        }
    }
}
