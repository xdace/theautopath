using System;
using System.Collections.Generic;
using System.IO;
using Betaknight.Core;
using Betaknight.Core.Autoplay;
using Betaknight.Core.Hex;
using Betaknight.Core.Run;
using Betaknight.Overworld.Controllers;
using Betaknight.Overworld.UI;
using UnityEngine;

namespace Betaknight.Overworld.Autoplay
{
    /// <summary>
    /// Testspieler im laufenden Spiel (<c>-autoplay</c>). Er bedient die echten Fenster: wählt das Kit im Kit-Fenster,
    /// lässt Kämpfe in der Arena abspielen und klickt «Weiter», öffnet «Build» und «Inventar» vor Tafel- und
    /// Ausrüstungs-Aktionen und läuft über den Controller (mit Animation). Entscheidungen trifft der
    /// <see cref="AutoplayBot"/>; ausgeführt wird über die Session-Methoden, die auch die UI nutzt.
    /// Gemessen werden FPS, Exceptions und Fehler-Logs (über das Unity-Log) und Hänger (keine Aktion &gt; 10 s).
    /// </summary>
    public sealed class AutoplayRunner : MonoBehaviour
    {
        /// <summary>Ohne Fortschritt so lange (Echtzeit) gilt der Bot als hängend.</summary>
        public const float HangSeconds = 10f;

        /// <summary>Nach so vielen Hängern in einem Run wird er abgebrochen.</summary>
        public const int MaxHangsPerRun = 3;

        /// <summary>Pause zwischen zwei Aktionen bei Tempo 1 (Echtzeit), damit man zuschauen kann.</summary>
        private const float ActionPause = 0.35f;

        /// <summary>Ein geöffnetes Fenster bleibt so lange sichtbar, bevor die Aktion darin läuft.</summary>
        private const float WindowPause = 0.4f;

        /// <summary>Game Over bzw. Akt-Ende bleibt so lange stehen, bevor der nächste Run beginnt.</summary>
        private const float BetweenRunsPause = 2f;

        /// <summary>Die ersten Frames nach einem Kartenaufbau zählen nicht zur FPS-Messung.</summary>
        private const int WarmupFrames = 10;

        private enum Phase { Idle, Running, BetweenRuns, Done }

        private OverworldBootstrapper _boot;
        private AutoplayOptions _options;
        private readonly AutoplaySummary _summary = new AutoplaySummary();
        private Phase _phase;
        private int _runIndex;
        private int _seed;
        private AutoplayReport _report;
        private AutoplayRecorder _recorder;
        private AutoplayBot _bot;
        private OverworldSession _attached;
        private float _runStart;
        private float _lastProgress;
        private float _nextActionAt;
        private float _nextRunAt;
        private int _lastTurn = -1;
        private int _lastArenaTick = -1;
        private int _hangsThisRun;
        private string _lastAction = string.Empty;
        private int _warmup;
        private int _frames;
        private double _frameTime;
        private float _maxFrame;
        private string _reportPath;

        private float Now => Time.realtimeSinceStartup;
        private float Speed => _options?.Speed ?? 1f;

        public void Initialize(OverworldBootstrapper boot, AutoplayOptions options)
        {
            _boot = boot;
            _options = options;
        }

        /// <summary>Startet den ersten Run (statt des normalen Spielstarts).</summary>
        public void Begin()
        {
            foreach (string warning in _options.Warnings) Debug.LogWarning($"[Autoplay] {warning}");
            Application.logMessageReceived += OnLog;
            Application.runInBackground = true;
            Time.timeScale = Mathf.Clamp(Speed, 0.1f, 100f);
            _boot.Arena.SpeedFactor = Speed;
            Debug.Log($"[Autoplay] Start: {_options.Runs} Run(s), Seed {(_options.Seed.HasValue ? _options.Seed.Value.ToString() : "zufällig")}, " +
                $"Tempo {Speed}×, bis Akt {_options.TargetAct}{(_options.Quit ? ", danach beenden" : string.Empty)}.");
            StartRun();
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            _recorder?.Detach();
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_report == null || _phase != Phase.Running) return;
            switch (type)
            {
                case LogType.Exception:
                    _report.AddException(string.IsNullOrEmpty(stackTrace) ? condition : $"{condition}\n{stackTrace}");
                    break;
                case LogType.Error:
                case LogType.Assert:
                    _report.AddErrorLog(condition);
                    break;
            }
        }

        // ------------------------------------------------------------------ Runs

        private void StartRun()
        {
            _seed = _options.SeedFor(_runIndex, () => UnityEngine.Random.Range(1, int.MaxValue));
            _report = new AutoplayReport { Seed = _seed };
            _recorder = new AutoplayRecorder(_report);
            _bot = new AutoplayBot(_seed);
            _attached = null;
            _hangsThisRun = 0;
            _lastTurn = -1;
            _lastArenaTick = -1;
            _frames = 0;
            _frameTime = 0;
            _maxFrame = 0f;
            _warmup = WarmupFrames;
            _runStart = Now;
            _phase = Phase.Running;
            Progress("Run startet");
            _nextActionAt = Now + WindowPause / Speed;

            Debug.Log($"[Autoplay] Run {_runIndex + 1}/{_options.Runs}, Seed {_seed}");
            _boot.SeedOverride = _seed;
            _boot.StartNewRun();
        }

        private void EndRun(string reason)
        {
            _report.DurationSeconds = Now - _runStart;
            if (_frames > 0)
            {
                _report.FpsAverage = _frames / Math.Max(0.0001, _frameTime);
                _report.FpsMin = _maxFrame > 0f ? 1.0 / _maxFrame : 0.0;
            }
            _recorder.Finish(reason);
            _summary.Runs.Add(_report);
            Debug.Log($"[Autoplay] {_report.Summary()}");

            _phase = Phase.BetweenRuns;
            _nextRunAt = Now + BetweenRunsPause / Speed;
            _runIndex++;
        }

        private void Finish()
        {
            _phase = Phase.Done;
            Application.logMessageReceived -= OnLog;
            Debug.Log($"[Autoplay] {_summary.Summary()}");
            Debug.Log($"[Autoplay] {RuneFireStats.Table(_summary.Runs)}");

            _reportPath = string.IsNullOrEmpty(_options.ReportPath)
                ? Path.Combine(Application.persistentDataPath, "autoplay-report.json")
                : _options.ReportPath;
            try
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(_reportPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_reportPath, _summary.ToJson());
                Debug.Log($"[Autoplay] Bericht: {Path.GetFullPath(_reportPath)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Autoplay] Bericht konnte nicht geschrieben werden ({_reportPath}): {e.Message}");
            }

            if (_options.Quit)
            {
                Debug.Log($"[Autoplay] Beenden mit Exit-Code {_summary.ExitCode}");
                Application.Quit(_summary.ExitCode);
            }
        }

        // ------------------------------------------------------------------ Takt

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Running:
                    MeasureFrame();
                    Step();
                    if (_phase == Phase.Running) CheckHang();
                    break;

                case Phase.BetweenRuns:
                    if (Now < _nextRunAt) break;
                    if (_runIndex < _options.Runs) StartRun();
                    else Finish();
                    break;
            }
        }

        private void MeasureFrame()
        {
            if (_warmup > 0)
            {
                _warmup--;
                return;
            }
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            _frames++;
            _frameTime += dt;
            if (dt > _maxFrame) _maxFrame = dt;
        }

        private void Progress(string what)
        {
            _lastProgress = Now;
            if (!string.IsNullOrEmpty(what)) _lastAction = what;
        }

        private void Step()
        {
            // Kit-Wahl im echten Fenster.
            KitSelectionWindow kits = _boot.KitWindow;
            if (kits.IsOpen)
            {
                if (Now < _nextActionAt) return;
                KnightKit kit = AutoplayBot.ChooseKit(kits.Kits, _seed);
                _report.Kit = kit?.Name ?? "keins";
                Debug.Log($"[Autoplay] Kit: {_report.Kit}");
                kits.Choose(kit);
                Progress($"Kit {_report.Kit}");
                _nextActionAt = Now + ActionPause / Speed;
                return;
            }

            OverworldSession session = _boot.Session;
            if (session == null) return;
            if (session != _attached)
            {
                _attached = session;
                _recorder.Attach(session);
                _warmup = WarmupFrames;
                Progress($"Akt {session.Act}");
            }
            if (session.Turns.CurrentTurn != _lastTurn)
            {
                _lastTurn = session.Turns.CurrentTurn;
                Progress(null);
            }

            if (session.Act >= _options.TargetAct)
            {
                EndRun($"Akt {_options.TargetAct} erreicht");
                return;
            }

            // Arena: abspielen lassen, danach «Weiter».
            ArenaWindow arena = _boot.Arena;
            if (arena.IsOpen)
            {
                if (arena.PlaybackTick != _lastArenaTick)
                {
                    _lastArenaTick = arena.PlaybackTick;
                    Progress(null);
                }
                if (arena.IsFinished && Now >= _nextActionAt)
                {
                    arena.Continue();
                    Progress("Arena: Weiter");
                    _nextActionAt = Now + ActionPause / Speed;
                }
                return;
            }

            if (session.IsGameOver)
            {
                EndRun("Game Over");
                return;
            }
            if (session.Turns.CurrentTurn >= HeadlessAutoplay.MaxTurns)
            {
                EndRun("Zuglimit");
                return;
            }

            OverworldController controller = _boot.Controller;
            if (controller != null && controller.IsTravelling) return;
            if (Now < _nextActionAt) return;

            BotAction action;
            try
            {
                action = _bot.Decide(session);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _nextActionAt = Now + ActionPause / Speed;
                return;
            }
            if (action.Kind == BotActionKind.None)
            {
                _lastAction = action.Description;
                return;
            }

            // Das passende Fenster öffnen und kurz zeigen, bevor die Aktion darin läuft.
            if (action.Kind == BotActionKind.Build && !_boot.BuildWin.IsOpen)
            {
                _boot.OpenBuild();
                _nextActionAt = Now + WindowPause / Speed;
                return;
            }
            if (action.Kind == BotActionKind.Inventory && !_boot.InventoryWin.IsOpen)
            {
                _boot.OpenInventory();
                _nextActionAt = Now + WindowPause / Speed;
                return;
            }
            if (action.Kind != BotActionKind.Build && action.Kind != BotActionKind.Inventory && (_boot.BuildWin.IsOpen || _boot.InventoryWin.IsOpen))
            {
                _boot.CloseLoadoutWindows();
                _nextActionAt = Now + WindowPause / Speed;
                return;
            }

            bool ok;
            try
            {
                ok = action.Kind == BotActionKind.Move && action.Step.HasValue && controller != null
                    ? controller.TravelTo(new List<HexCoord> { action.Step.Value })
                    : action.Execute();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                ok = false;
            }
            if (!ok) _bot.MarkFailed(session, action);
            _recorder.Record(action, ok);
            Progress(action.Description);
            _nextActionAt = Now + ActionPause / Speed;
        }

        private void CheckHang()
        {
            if (Now - _lastProgress <= HangSeconds) return;

            OverworldSession s = _boot.Session;
            string where = s == null ? "ohne Session" : $"Akt {s.Act}, Zug {s.Turns.CurrentTurn}";
            string text = $"{where}: keine Aktion seit {HangSeconds:0} s, zuletzt «{_lastAction}»";
            _report.AddHang(text);
            Debug.LogWarning($"[Autoplay] Hänger: {text}");
            _hangsThisRun++;

            // Befreiungsversuch: Arena weiterklicken, Fenster schliessen.
            _boot.Arena.Continue();
            _boot.CloseLoadoutWindows();
            Progress("Befreiungsversuch nach Hänger");

            if (_hangsThisRun >= MaxHangsPerRun) EndRun("Hänger");
        }

        // ------------------------------------------------------------------ Anzeige

        private void OnGUI()
        {
            if (_phase == Phase.Idle) return;
            UiTheme.Apply();
            string status;
            switch (_phase)
            {
                case Phase.Done:
                    status = $"<b>Testspieler fertig</b>\n{_summary.Summary()}\nBericht: {_reportPath}";
                    break;
                case Phase.BetweenRuns:
                    status = $"<b>Testspieler</b>  Run {_runIndex}/{_options.Runs} beendet: {_report?.EndReason}";
                    break;
                default:
                    status = $"<b>Testspieler</b>  Run {_runIndex + 1}/{_options.Runs}, Seed {_seed}, {_report?.Kit}, Tempo {Speed}×\n{_lastAction}";
                    break;
            }
            const float width = 420f;
            var content = new GUIContent(status);
            float height = UiTheme.Tooltip.CalcHeight(content, width);
            GUI.Label(new Rect(Screen.width - width - 12f, Screen.height - height - 12f, width, height), content, UiTheme.Tooltip);
        }
    }
}
