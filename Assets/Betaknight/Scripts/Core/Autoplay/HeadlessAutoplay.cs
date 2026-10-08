using System;
using System.Diagnostics;
using Betaknight.Core.Map;
using Betaknight.Core.Run;

namespace Betaknight.Core.Autoplay
{
    /// <summary>
    /// Lässt den Testspieler ohne Darstellung direkt gegen die <see cref="OverworldSession"/> spielen: für den
    /// EditMode-Test und für schnelle Läufe. Akte wechseln wie im Spiel über <see cref="OverworldSession.ActCompleted"/>.
    /// </summary>
    public static class HeadlessAutoplay
    {
        /// <summary>So viele Aktionen ohne Fortschritt (kein neuer Zug, kein Aktwechsel) gelten als Hänger.</summary>
        public const int HangActions = 300;

        /// <summary>Sicherheitsgrenze für Züge pro Run.</summary>
        public const int MaxTurns = 600;

        public static AutoplayReport Run(int seed, int targetAct = AutoplayOptions.DefaultTargetAct, KnightKit kit = null,
            Func<int, MapGenerationConfig> config = null)
        {
            config = config ?? (s => new MapGenerationConfig { Radius = 6, Seed = s });
            kit = kit ?? AutoplayBot.ChooseKit(KnightKit.Defaults, seed);
            var report = new AutoplayReport { Seed = seed, Kit = kit?.Name ?? "keins" };
            var recorder = new AutoplayRecorder(report);
            var bot = new AutoplayBot(seed);
            var watch = Stopwatch.StartNew();

            MapGenerationConfig cfg = config(seed);
            OverworldSession session = null;
            OverworldSession next = null;
            void OnAct(OverworldSession previous) => next = OverworldSession.CreateNextAct(cfg, previous);

            try
            {
                session = OverworldSession.Create(cfg, kit: kit);
                session.ActCompleted += OnAct;
                recorder.Attach(session);
            }
            catch (Exception e)
            {
                report.AddException(e.ToString());
                report.EndReason = "Exception beim Start";
                return report;
            }

            int idle = 0;
            int lastTurn = session.Turns.CurrentTurn;
            int lastAct = session.Act;
            string reason;
            while (true)
            {
                if (session.IsGameOver) { reason = "Game Over"; break; }
                if (session.Act >= targetAct) { reason = $"Akt {targetAct} erreicht"; break; }
                if (session.Turns.CurrentTurn >= MaxTurns) { reason = "Zuglimit"; break; }
                if (report.ExceptionCount >= 10) { reason = "zu viele Exceptions"; break; }

                BotAction action;
                try
                {
                    action = bot.Decide(session);
                }
                catch (Exception e)
                {
                    report.AddException($"Entscheidung: {e}");
                    idle++;
                    if (idle > HangActions) { reason = Hang(report, session, "Entscheidung wirft wiederholt"); break; }
                    continue;
                }

                if (action.Kind == BotActionKind.None)
                {
                    reason = Hang(report, session, action.Description);
                    break;
                }

                bool ok;
                try
                {
                    ok = action.Execute();
                }
                catch (Exception e)
                {
                    report.AddException($"{action}: {e}");
                    ok = false;
                }
                if (!ok) bot.MarkFailed(session, action);
                recorder.Record(action, ok);

                if (next != null)
                {
                    session.ActCompleted -= OnAct;
                    session = next;
                    next = null;
                    session.ActCompleted += OnAct;
                    recorder.Attach(session);
                }

                if (session.Turns.CurrentTurn != lastTurn || session.Act != lastAct)
                {
                    idle = 0;
                    lastTurn = session.Turns.CurrentTurn;
                    lastAct = session.Act;
                }
                else if (++idle > HangActions)
                {
                    reason = Hang(report, session, $"{HangActions} Aktionen ohne neuen Zug, zuletzt «{action.Description}»");
                    break;
                }
            }

            session.ActCompleted -= OnAct;
            report.DurationSeconds = watch.Elapsed.TotalSeconds;
            recorder.Finish(reason);
            return report;
        }

        private static string Hang(AutoplayReport report, OverworldSession s, string what)
        {
            report.AddHang($"Akt {s.Act}, Zug {s.Turns.CurrentTurn}: {what}");
            return "Hänger";
        }
    }
}
