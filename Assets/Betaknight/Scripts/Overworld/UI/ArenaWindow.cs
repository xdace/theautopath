using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Arena-Ansicht per IMGUI: spielt das Protokoll eines Kampfs in 2D-Seitenansicht ab (Ritter links, Gegner rechts).
    /// Die Platine (A-19) zeigt live, welches Relais auslöst (leuchtet), welche Komponenten warten, welche feuert und welche
    /// eingefroren, unversorgt oder zu gross sind (Hovern erklärt den Grund); darunter die Warteschlange. Zustände, Ressourcen
    /// und schwebende Zahlen in der Farbe der auslösenden Komponente zeigen die Wirkung. Gegner-Platinen stehen als kleines
    /// Raster neben jedem Gegner, live wie die des Ritters (<see cref="EnemyBoardView"/>, Zustand aus <see cref="BoardWatch"/>),
    /// und im Tooltip des Gegners. Nach dem Kampf folgt eine Auswertung pro Komponente; wartet Beute, steht es in der Fusszeile. Platzhalter-Grafik aus Rechtecken, gerechnet wird nichts.
    /// A-20: Pins, Logik-Chips und Pulsverbindungen auf der Platine; Pulse laufen als Punkte die Verbindung entlang, Gatter
    /// zeigen offen/geschlossen (Sicherung: durchgebrannt), Kondensatoren ihre Ladung als Punkte, Relais ihren Zustand als Lämpchen.
    /// A-21: Effekt-Symbole an Komponenten, Hitze-Balken (Overclock), Rekursions-Tiefe und Verstärkung der laufenden Ausführung,
    /// «+15%» an verstärkten Pulsen, gehackte Relais und Komponenten flackern (mit Schild und Tooltip), Hacks gegen Gegner als
    /// Schilder unter dem Gegner, Stufe des Thermal Throttling über der Bühne. Zustand aus <see cref="ArenaEffects"/>.
    /// </summary>
    public sealed class ArenaWindow : MonoBehaviour
    {
        /// <summary>Tempo-Stufen; 1× sind <see cref="BaseTicksPerSecond"/> Ticks pro Sekunde (halbe Spielzeit, besser zu verfolgen).</summary>
        private static readonly float[] Speeds = { 0.5f, 1f, 2f, 4f };
        private const float BaseTicksPerSecond = Ticks.PerSecond * 0.5f;
        private const string SpeedPref = "betaknight.arena.speed";

        /// <summary>So lange bleibt die Bühne nach Kampfende mit «VICTORY»/«DEFEAT» stehen, bevor der Bericht kommt.</summary>
        private const float EndHoldSeconds = 1.5f;

        /// <summary>Hervorhebungen bleiben mindestens so lange sichtbar (Echtzeit), auch bei 4×.</summary>
        private const float MinHighlightSeconds = 0.35f;
        private const float PopupSeconds = 1.1f;

        /// <summary>Farbe je Komponente (Lesereihenfolge); der Basisangriff ist grau.</summary>
        private static readonly Color[] RowColors =
        {
            new Color(0.35f, 0.85f, 1.00f),
            new Color(1.00f, 0.60f, 0.20f),
            new Color(0.55f, 0.95f, 0.35f),
            new Color(0.95f, 0.45f, 0.90f),
            new Color(1.00f, 0.85f, 0.30f),
            new Color(0.55f, 0.65f, 1.00f),
            new Color(1.00f, 0.45f, 0.45f),
            new Color(0.70f, 0.55f, 1.00f),
        };
        private static readonly Color FallbackColor = new Color(0.72f, 0.74f, 0.80f);

        private sealed class ActivePopup
        {
            public Popup Popup;
            public float Start;
            public float Offset;
        }

        /// <summary>Ein Puls in Echtzeit: läuft mindestens <see cref="MinPulseSeconds"/>, damit er auch bei 4× zu sehen ist.</summary>
        private sealed class ActivePulse
        {
            public int Link;

            /// <summary>Kämpfer, dessen Platine den Puls schickt (Spieler oder Gegner).</summary>
            public int Fighter;
            public float Start;
            public float Duration;

            /// <summary>A-21: Verstärkung in Prozent (0 = keine).</summary>
            public int Power;
        }

        /// <summary>Kurzer Hinweis an einer Komponente (A-21), in Echtzeit sichtbar.</summary>
        private sealed class ActiveFlash
        {
            public ArenaEffects.Flash Flash;
            public float Start;
        }

        private const float FlashSeconds = 0.9f;
        private const float ThermalFlashSeconds = 1.6f;

        /// <summary>Der Spieler ist der erste Kämpfer der Wiedergabe.</summary>
        private const int PlayerFighter = 0;

        private const float MinPulseSeconds = 0.45f;

        private OverworldSession _session;
        private readonly Queue<CombatResult> _queue = new Queue<CombatResult>();
        private BattlePlayback _playback;
        private ArenaEffects _fx;
        private readonly List<ActiveFlash> _effectFlashes = new List<ActiveFlash>();
        private int _seenOverheat;
        private float _overheatFlashUntil;
        private readonly List<LogEntry> _mergedLog = new List<LogEntry>();
        private int _mergedA = -1;
        private int _mergedB = -1;
        private CombatResult _current;
        private int _speedIndex = 1;
        private float _finishedAt = -1f;
        private bool _paused;
        private float _tickBuffer;
        private Vector2 _logScroll;
        private Vector2 _reportScroll;
        private LogFilter _filter;
        private BattleReport _report;
        private readonly List<ActivePopup> _popups = new List<ActivePopup>();
        private readonly List<ActivePulse> _pulses = new List<ActivePulse>();
        private readonly HashSet<(int link, int start)> _seenPulses = new HashSet<(int, int)>();
        private readonly Dictionary<int, Rect> _bodies = new Dictionary<int, Rect>();
        private readonly Dictionary<int, int> _seenHitTick = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _flashUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, string> _enemyTips = new Dictionary<int, string>();

        /// <summary>Gegner-Platinen: Live-Zustand je Kämpfer und Leuchten der zuletzt gestarteten Komponente in Echtzeit.</summary>
        private readonly Dictionary<int, EnemyBoardView.Live> _enemyBoards = new Dictionary<int, EnemyBoardView.Live>();
        private readonly Dictionary<int, int> _seenEnemyRowTick = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _enemyRowUntil = new Dictionary<int, float>();
        private int _seenRowTick = -1000;
        private float _rowHighlightUntil;
        private Dictionary<char, string> _glyphs;
        private GUIStyle _popup;
        private GUIStyle _popupSmall;
        private GUIStyle _cell;
        private GUIStyle _logLine;
        private GUIStyle _tooltip;
        private GUIStyle _title;
        private GUIStyle _centerTitle;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _row;
        private GUIStyle _nowLine;
        private GUIStyle _thermal;
        private GUIStyle _thermalFlash;
        private Texture2D _white;

        /// <summary>Solange die Arena offen ist, warten Oberwelt und andere Fenster.</summary>
        public bool IsOpen => _playback != null;

        /// <summary>«Open Build» aus der Auswertung: schliesst die Arena und öffnet das Build-Fenster.</summary>
        public System.Action OnEditBoard;

        /// <summary>Zusätzlicher Tempo-Faktor auf die gewählte Wiedergabe-Geschwindigkeit (Testspieler mit -speed).</summary>
        public float SpeedFactor { get; set; } = 1f;

        /// <summary>Der aktuelle Kampf ist fertig abgespielt; die Auswertung wartet auf «Weiter».</summary>
        public bool IsFinished => _playback != null && _playback.IsFinished;

        /// <summary>Fortschritt der Wiedergabe in Ticks, -1 ohne Kampf (für die Hänger-Erkennung des Testspielers).</summary>
        public int PlaybackTick => _playback?.Tick ?? -1;

        /// <summary>Wie «Weiter» nach der Auswertung: nächster Kampf aus der Warteschlange oder Arena schliessen.</summary>
        public bool Continue()
        {
            if (_playback == null || !_playback.IsFinished) return false;
            OpenNext();
            return true;
        }

        public void Initialize(OverworldSession session)
        {
            if (_session != null) _session.CombatFinished -= OnCombatFinished;
            _session = session;
            _queue.Clear();
            _playback = null;
            if (_session != null) _session.CombatFinished += OnCombatFinished;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.CombatFinished -= OnCombatFinished;
        }

        private void OnCombatFinished(CombatResult result)
        {
            if (result.Battle == null) return;
            _queue.Enqueue(result);
            if (_playback == null) OpenNext();
        }

        private void OpenNext()
        {
            if (_queue.Count == 0)
            {
                _playback = null;
                return;
            }
            _current = _queue.Dequeue();
            _playback = new BattlePlayback(_current.Battle);
            _fx = new ArenaEffects(_current.Battle);
            _effectFlashes.Clear();
            _seenOverheat = 0;
            _overheatFlashUntil = 0f;
            _mergedLog.Clear();
            _mergedA = _mergedB = -1;
            _report = null;
            _paused = false;
            _tickBuffer = 0f;
            _finishedAt = -1f;
            _speedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SpeedPref, 1), 0, Speeds.Length - 1);
            _logScroll = Vector2.zero;
            _reportScroll = Vector2.zero;
            _popups.Clear();
            _pulses.Clear();
            _seenPulses.Clear();
            _seenHitTick.Clear();
            _flashUntil.Clear();
            _enemyTips.Clear();
            _enemyBoards.Clear();
            _seenEnemyRowTick.Clear();
            _enemyRowUntil.Clear();
            _ghostHp.Clear();
            _lastHp.Clear();
            _ghostHoldUntil.Clear();
            for (int i = 0; i < _current.Battle.Fighters.Count; i++)
            {
                FighterInfo info = _current.Battle.Fighters[i];
                if (info.Side == Side.Player || !EnemyBoardView.CanDraw(info.Combatant?.Board)) continue;
                int fighter = i;
                var live = new EnemyBoardView.Live { Watch = new BoardWatch(_current.Battle, i), Fx = _fx, Fighter = i, View = _playback.Fighters[i] };
                live.RowLit = row => IsEnemyRowLit(fighter, row);
                _enemyBoards[i] = live;
            }
            _seenRowTick = -1000;
            _rowHighlightUntil = 0f;
        }

        private void Update()
        {
            if (_playback == null) return;
            UpdateGhosts();
            _popups.RemoveAll(p => Time.unscaledTime - p.Start > PopupSeconds);
            _pulses.RemoveAll(p => Time.unscaledTime - p.Start > p.Duration);
            _effectFlashes.RemoveAll(f => Time.unscaledTime - f.Start > FlashSeconds);
            if (_playback.IsFinished)
            {
                if (_finishedAt < 0f) _finishedAt = Time.unscaledTime;
                return;
            }
            if (_paused) return;
            _tickBuffer += Time.unscaledDeltaTime * BaseTicksPerSecond * Speeds[_speedIndex] * Mathf.Max(0.01f, SpeedFactor);
            int ticks = Mathf.FloorToInt(_tickBuffer);
            if (ticks <= 0) return;
            _tickBuffer -= ticks;
            _playback.Advance(ticks);
            TrackHighlights();
        }

        /// <summary>Neue Hervorhebungen in Echtzeit festhalten, damit sie auch bei 4× mindestens 0,3 s sichtbar sind.</summary>
        private void TrackHighlights()
        {
            float now = Time.unscaledTime;
            _fx.Advance(_playback.Tick);
            foreach (ArenaEffects.Flash f in _fx.TakeFlashes()) _effectFlashes.Add(new ActiveFlash { Flash = f, Start = now });
            if (_fx.OverheatLevel != _seenOverheat)
            {
                _seenOverheat = _fx.OverheatLevel;
                _overheatFlashUntil = now + ThermalFlashSeconds;
            }
            float gameSeconds = BattlePlayback.RowHighlightTicks / BaseTicksPerSecond / (Speeds[_speedIndex] * Mathf.Max(0.01f, SpeedFactor));
            if (_playback.LastPlayerRowTick != _seenRowTick)
            {
                _seenRowTick = _playback.LastPlayerRowTick;
                _rowHighlightUntil = now + Mathf.Max(MinHighlightSeconds, gameSeconds);
            }

            for (int i = 0; i < _playback.Fighters.Count; i++)
            {
                int hit = _playback.Fighters[i].LastHitTick;
                if (_seenHitTick.TryGetValue(i, out int seen) && seen == hit) continue;
                _seenHitTick[i] = hit;
                if (hit >= 0) _flashUntil[i] = now + MinHighlightSeconds;
            }

            // Pulse: in Spielzeit oft nur wenige Ticks unterwegs; in Echtzeit mindestens kurz sichtbar.
            float speed = Speeds[_speedIndex] * Mathf.Max(0.01f, SpeedFactor);
            foreach (PulseView p in _playback.Pulses)
            {
                if (!_seenPulses.Add((p.Link, p.StartTick))) continue;
                float seconds = (p.ArriveTick - p.StartTick) / BaseTicksPerSecond / speed;
                _pulses.Add(new ActivePulse { Link = p.Link, Fighter = PlayerFighter, Start = now, Duration = Mathf.Max(MinPulseSeconds, seconds), Power = _fx.PulsePower(p.Link, p.StartTick) });
            }
            TrackEnemyBoards(now, gameSeconds, speed);

            // Viele Zahlen im selben Moment fächern sich leicht auf, statt sich zu überdecken.
            foreach (Popup p in _playback.TakePopups())
            {
                int stacked = _popups.Count(a => a.Popup.TargetIndex == p.TargetIndex && now - a.Start < 0.25f);
                _popups.Add(new ActivePopup { Popup = p, Start = now, Offset = stacked * 18f });
            }
        }

        /// <summary>Gegner-Platinen: Ereignisse bis zum aktuellen Tick, neue Pulse und Leuchten der gestarteten Komponente in Echtzeit.</summary>
        private void TrackEnemyBoards(float now, float gameSeconds, float speed)
        {
            foreach (KeyValuePair<int, EnemyBoardView.Live> pair in _enemyBoards)
            {
                BoardWatch watch = pair.Value.Watch;
                watch.Advance(_playback.Tick);
                foreach (BoardWatch.Pulse p in watch.TakeNewPulses())
                {
                    float seconds = (p.ArriveTick - p.StartTick) / (float)Ticks.PerSecond / speed;
                    _pulses.Add(new ActivePulse { Link = p.Link, Fighter = pair.Key, Start = now, Duration = Mathf.Max(MinPulseSeconds, seconds), Power = p.Power });
                }
                if (_seenEnemyRowTick.TryGetValue(pair.Key, out int seen) && seen == watch.LastRowTick) continue;
                _seenEnemyRowTick[pair.Key] = watch.LastRowTick;
                _enemyRowUntil[pair.Key] = now + Mathf.Max(MinHighlightSeconds, gameSeconds);
            }
        }

        private bool IsEnemyRowLit(int fighter, int row)
        {
            if (!_enemyBoards.TryGetValue(fighter, out EnemyBoardView.Live live) || live.Watch.LastRow != row) return false;
            return (_enemyRowUntil.TryGetValue(fighter, out float until) && Time.unscaledTime < until)
                || live.Watch.Tick - live.Watch.LastRowTick < BattlePlayback.RowHighlightTicks;
        }

        private bool IsRowLit(int row) =>
            row == _playback.LastPlayerRow && (Time.unscaledTime < _rowHighlightUntil || _playback.IsRowHighlighted(row));

        private void SetSpeed(int index)
        {
            _speedIndex = Mathf.Clamp(index, 0, Speeds.Length - 1);
            PlayerPrefs.SetInt(SpeedPref, _speedIndex);
        }

        /// <summary>Spult bis zur nächsten eigenen Aktion (Start einer Komponente) und hält dort an.</summary>
        private void StepToNextAction()
        {
            if (_playback == null || _playback.IsFinished) return;
            int next = _playback.NextTickWhere(e => e.Kind == BattleEventKind.ActionStarted && e.Source != null
                && e.Source.Side == Side.Player && e.Detail != SkillDefinition.BasicAttackId);
            _playback.Advance(next > _playback.Tick ? next - _playback.Tick : 1);
            _tickBuffer = 0f;
            _paused = true;
            TrackHighlights();
        }

        /// <summary>Tasten: Space Pause, 1–4 Tempo, N nächste Aktion, Punkt ein Tick weiter.</summary>
        private void HandleKeys()
        {
            Event e = Event.current;
            if (e.type != EventType.KeyDown || _playback == null || _playback.IsFinished) return;
            switch (e.keyCode)
            {
                case KeyCode.Space: _paused = !_paused; break;
                case KeyCode.Alpha1: case KeyCode.Keypad1: SetSpeed(0); break;
                case KeyCode.Alpha2: case KeyCode.Keypad2: SetSpeed(1); break;
                case KeyCode.Alpha3: case KeyCode.Keypad3: SetSpeed(2); break;
                case KeyCode.Alpha4: case KeyCode.Keypad4: SetSpeed(3); break;
                case KeyCode.N: StepToNextAction(); break;
                case KeyCode.Period:
                    _playback.Advance(1);
                    _paused = true;
                    TrackHighlights();
                    break;
                default: return;
            }
            e.Use();
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            if (_playback == null) return;
            EnsureStyles();
            GUI.depth = -10;
            HandleKeys();

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            Fill(screen, new Color(0.04f, 0.05f, 0.07f, 1f));

            const float pad = 16f;
            float boardWidth = Mathf.Min(420f, Screen.width * 0.36f);
            var stage = new Rect(pad, 56f, Screen.width - boardWidth - pad * 3, Screen.height * 0.52f);
            var board = new Rect(stage.xMax + pad, 56f, boardWidth, stage.height);
            var log = new Rect(pad, stage.yMax + pad, Screen.width - pad * 2, Screen.height - stage.yMax - pad * 2 - 44f);
            var controls = new Rect(pad, Screen.height - pad - 36f, Screen.width - pad * 2, 36f);

            string enemy = string.IsNullOrEmpty(_current.EnemyName) ? UiTexts.Arena.Enemy : _current.EnemyName;
            string portal = string.Empty;
            int survive = _playback.Result.SurviveTicks;
            if (survive > 0)
            {
                int left = Mathf.Max(0, survive - _playback.Tick);
                portal = left > 0 ? $"   <color=#b18cff>{UiTexts.Arena.PortalOpensIn(BattleLogText.Time(left))}</color>" : $"   <color=#b18cff>{UiTexts.Arena.PortalOpen}</color>";
            }
            GUI.Label(new Rect(pad, 12f, Screen.width - pad * 2, 36f),
                UiTexts.Arena.Title(enemy, BattleLogText.Time(_playback.Tick), portal), _title);
            DrawThermal(new Rect(pad, 34f, Screen.width - boardWidth - pad * 3, 22f));

            bool holding = _playback.IsFinished && (_finishedAt < 0f || Time.unscaledTime - _finishedAt < EndHoldSeconds);
            if (_playback.IsFinished && !holding)
            {
                if (_report == null) _report = BattleReport.Create(_playback.Result);
                DrawReport(stage);
            }
            else if (holding)
            {
                DrawStage(stage);
                DrawPopups();
                BattleResult res = _playback.Result;
                Color banner = res.IsVictory ? UiTheme.Good : res.IsSurvived ? UiTheme.Accent : UiTheme.Bad;
                var bannerRect = new Rect(stage.x, stage.y + stage.height * 0.35f, stage.width, 64f);
                Fill(bannerRect, new Color(0f, 0f, 0f, 0.6f));
                GUI.Label(bannerRect, $"<size=40><b><color={UiTheme.Hex(banner)}>{BattleLogText.OutcomeText(res.Outcome).ToUpperInvariant()}</color></b></size>", _centerTitle);
            }
            else
            {
                DrawStage(stage);
                DrawPopups();
            }
            DrawBoard(board);
            DrawLog(log);
            DrawControls(controls);
            DrawTooltip();
        }

        // ------------------------------------------------------------------ Thermal Throttling (A-21)

        /// <summary>«Thermal Throttling 2: +20% computing time, +32% damage», blinkt kurz bei jeder neuen Stufe.</summary>
        private void DrawThermal(Rect area)
        {
            int level = _fx.OverheatLevel;
            if (level <= 0) return;
            ThermalConfig thermal = ThermalConfig.Default;
            long factor = 100;
            for (int i = 0; i < level && factor < 10_000_000; i++) factor = factor * (100 + thermal.DamagePercentPerStep) / 100;
            string text = UiTexts.Effects.Thermal(level, level * thermal.CastPercentPerStep, (int)(factor - 100));
            bool flash = Time.unscaledTime < _overheatFlashUntil && Mathf.Repeat(Time.unscaledTime * 5f, 1f) < 0.5f;
            Color hot = new Color(1f, 0.42f, 0.18f);
            var content = new GUIContent($"<b>{text}</b>", UiTexts.Effects.ThermalTip);
            float width = Mathf.Min(area.width, _small.CalcSize(content).x + 24f);
            var banner = new Rect(area.x, area.y, width, area.height);
            Fill(banner, flash ? new Color(hot.r, hot.g, hot.b, 0.85f) : new Color(0.30f, 0.10f, 0.04f, 0.95f));
            Fill(new Rect(banner.x, banner.yMax - 2f, banner.width * Mathf.Clamp01(level / 8f), 2f), hot);
            GUI.Label(banner, content, flash ? _thermalFlash : _thermal);
        }

        // ------------------------------------------------------------------ Bühne

        private void DrawStage(Rect area)
        {
            Fill(area, new Color(0.09f, 0.10f, 0.13f));
            Fill(new Rect(area.x, area.yMax - 40f, area.width, 40f), new Color(0.14f, 0.15f, 0.18f));

            var card = new Rect(area.x + 8f, area.y + 6f, area.width - 16f, NowCardHeight);
            DrawNowCard(card);
            var field = new Rect(area.x, card.yMax + 6f, area.width, area.yMax - card.yMax - 6f);

            var players = new List<int>();
            var enemies = new List<int>();
            for (int i = 0; i < _playback.Fighters.Count; i++) (_playback.Fighters[i].Info.Side == Side.Player ? players : enemies).Add(i);

            DrawSide(players, new Rect(field.x + 24f, field.y, field.width * 0.4f - 24f, field.height), new Color(0.40f, 0.85f, 1.00f));
            // Gegner bekommen mehr Platz: links neben jedem steht seine Platine.
            float enemyX = _enemyBoards.Count > 0 ? 0.42f : 0.6f;
            DrawSide(enemies, new Rect(field.x + field.width * enemyX, field.y, field.width * (1f - enemyX) - 24f, field.height), new Color(0.80f, 0.35f, 0.30f));
        }

        private const float NowCardHeight = 66f;
        private static readonly Color PlayerStoryColor = new Color(0.40f, 0.85f, 1.00f);
        private static readonly Color EnemyStoryColor = new Color(1.00f, 0.42f, 0.38f);

        /// <summary>
        /// «JETZT»-Karte: die neueste Aktion als ein Satz (Ursache → Komponente → Ziel → Wirkung) groß, darunter die zwei davor
        /// blass. Basisangriffe nur, wenn sonst nichts passiert ist. Spieler mit cyanem, Gegner mit rotem Streifen.
        /// </summary>
        private void DrawNowCard(Rect area)
        {
            Fill(area, new Color(0.05f, 0.06f, 0.08f, 0.92f));
            GUI.Label(area, new GUIContent(string.Empty, UiTexts.Arena.NowTip));
            var lines = new List<ActionStory>();
            IReadOnlyList<ActionStory> stories = _playback.Stories;
            for (int i = stories.Count - 1; i >= 0 && lines.Count < 3; i--)
                if (!stories[i].IsBasicAttack) lines.Add(stories[i]);
            if (lines.Count == 0 && stories.Count > 0) lines.Add(stories[stories.Count - 1]);

            var tag = new Rect(area.x + 8f, area.y + 6f, 48f, 26f);
            GUI.Label(tag, $"<size=12><b><color=#9aa4b2>{UiTexts.Arena.NowTag}</color></b></size>", _small);
            if (lines.Count == 0)
            {
                GUI.Label(new Rect(area.x + 60f, area.y + 6f, area.width - 68f, 26f), $"<color=#9aa4b2>{UiTexts.Arena.NowEmpty}</color>", _text);
                return;
            }

            float x = area.x + 60f;
            float w = area.width - 68f;
            ActionStory main = lines[0];
            Color mc = main.IsPlayer ? PlayerStoryColor : EnemyStoryColor;
            Fill(new Rect(area.x, area.y, 4f, area.height), mc);
            string mainText = main.Sentence();
            if (main.Interrupted) mainText = $"<color=#9aa4b2>{mainText}</color>";
            GUI.Label(new Rect(x, area.y + 2f, w, 30f), $"<size=20><b>{mainText}</b></size>", _nowLine);
            for (int i = 1; i < lines.Count; i++)
            {
                ActionStory s = lines[i];
                string hex = s.IsPlayer ? "#6f9fb0" : "#b0706a";
                GUI.Label(new Rect(x, area.y + 32f + (i - 1) * 16f, w, 18f), $"<size=12><color={hex}>{s.Sentence()}</color></size>", _nowLine);
            }
        }

        /// <summary>Angezeigter «Ghost»-Wert je Kämpfer: folgt Schaden mit Verzögerung, damit der Verlust kurz sichtbar bleibt.</summary>
        private readonly Dictionary<int, float> _ghostHp = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _lastHp = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _ghostHoldUntil = new Dictionary<int, float>();
        private const float GhostHoldSeconds = 0.5f;

        private void UpdateGhosts()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _playback.Fighters.Count; i++)
            {
                FighterView f = _playback.Fighters[i];
                if (!_ghostHp.TryGetValue(i, out float ghost)) ghost = f.Hp;
                if (_lastHp.TryGetValue(i, out int last) && f.Hp < last) _ghostHoldUntil[i] = now + GhostHoldSeconds;
                _lastHp[i] = f.Hp;
                if (ghost < f.Hp) ghost = f.Hp;
                else if (ghost > f.Hp && (!_ghostHoldUntil.TryGetValue(i, out float hold) || now >= hold))
                    ghost = Mathf.MoveTowards(ghost, f.Hp, Mathf.Max(1f, f.Info.MaxHp) * 0.8f * Time.unscaledDeltaTime);
                _ghostHp[i] = ghost;
            }
        }

        /// <summary>Schaden, den angekündigte Gegnerangriffe dem Spieler gleich zufügen (für die rote Vorschau auf seinem Balken).</summary>
        private int IncomingDamage()
        {
            int sum = 0;
            foreach (FighterView f in _playback.Fighters)
                if (f.Alive && f.Info.Side != Side.Player && f.TelegraphSkill != null && f.TelegraphDamage > 0) sum += f.TelegraphDamage;
            return sum;
        }

        /// <summary>Großer HP-Balken mit Zahl, Ghost-Segment (frischer Schaden) und optional roter Vorschau angekündigten Schadens.</summary>
        private void DrawHpBar(int index, FighterView f, Rect bar, int incoming)
        {
            Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), new Color(0f, 0f, 0f, 0.6f));
            Fill(bar, new Color(0.2f, 0.2f, 0.22f));
            float max = Mathf.Max(1f, f.Info.MaxHp);
            float hp = Mathf.Clamp01(f.Hp / max);
            float ghost = _ghostHp.TryGetValue(index, out float g) ? Mathf.Clamp01(g / max) : hp;
            if (ghost > hp) Fill(new Rect(bar.x + bar.width * hp, bar.y, bar.width * (ghost - hp), bar.height), new Color(1f, 0.92f, 0.75f, 0.85f));
            Color fill = hp > 0.3f ? UiTheme.Good : UiTheme.Bad;
            Fill(new Rect(bar.x, bar.y, bar.width * hp, bar.height), fill);
            if (incoming > 0 && f.Alive)
            {
                float cut = Mathf.Clamp01(incoming / max);
                float from = Mathf.Max(0f, hp - cut);
                // Langsames Pulsieren (≈1,2 Hz), damit es auffällt, ohne zu flackern.
                float a = 0.55f + 0.3f * Mathf.Sin(Time.unscaledTime * 7.5f);
                Fill(new Rect(bar.x + bar.width * from, bar.y, bar.width * (hp - from), bar.height), new Color(0.85f, 0.10f, 0.10f, a));
            }
            int size = bar.height >= 26f ? 16 : 14;
            GUI.Label(bar, $"<size={size}><b>{f.Hp} / {f.Info.MaxHp}</b></size>", _centerTitle);
        }

        /// <summary>Ankündigung eines Gegnerangriffs: «! Ram in 0.8 s → Knight −25» und ein dicker roter Countdown-Balken.</summary>
        private void DrawTelegraph(FighterView f, Rect area)
        {
            if (!f.Alive || f.TelegraphSkill == null) return;
            int left = Mathf.Max(0, f.TelegraphTick - _playback.Tick);
            string player = _playback.Fighters[PlayerFighter].Info.Name;
            string text = ArenaTexts.Telegraph(f.TelegraphSkill, BattleLogText.Time(left), f.TelegraphDamage, player);
            Fill(area, new Color(0.30f, 0.06f, 0.05f, 0.92f));
            int total = Mathf.Max(1, f.TelegraphTick - (f.Story?.StartTick ?? _playback.Tick));
            float fill = Mathf.Clamp01(left / (float)total);
            Fill(new Rect(area.x, area.yMax - 6f, area.width * fill, 6f), EnemyStoryColor);
            GUI.Label(new Rect(area.x, area.y, area.width, area.height - 6f),
                new GUIContent($"<size=14><b><color=#ffb0a8>{text}</color></b></size>", UiTexts.Arena.TelegraphTip), _centerTitle);
        }

        private void DrawSide(List<int> indices, Rect area, Color color)
        {
            if (indices.Count == 0) return;
            float slot = area.width / indices.Count;
            int incoming = IncomingDamage();
            for (int n = 0; n < indices.Count; n++)
            {
                int index = indices[n];
                FighterView f = _playback.Fighters[index];
                bool isPlayer = f.Info.Side == Side.Player;
                bool hasBoard = !isPlayer && _enemyBoards.ContainsKey(index);
                float bodyW = Mathf.Min(hasBoard ? 80f : 110f, slot * 0.6f);
                float bodyH = Mathf.Min(bodyW * 1.5f, area.height * 0.38f);
                var body = new Rect(area.x + slot * n + (slot - bodyW) * 0.5f, area.yMax - 40f - bodyH, bodyW, bodyH);
                float column = Mathf.Min(slot - 8f, Mathf.Max(bodyW + 80f, 170f));

                // Gegner-Platine links im Platz, der Gegner mit seinen Balken rechts daneben.
                if (hasBoard)
                {
                    var boardRect = new Rect(area.x + slot * n + 4f, area.y + 2f, slot - column - 12f, area.height - 44f);
                    if (boardRect.width >= 80f)
                    {
                        body.x = area.x + slot * (n + 1) - column * 0.5f - 4f - bodyW * 0.5f;
                        DrawEnemyBoard(index, _enemyBoards[index], boardRect);
                    }
                }

                // Ausholen: Körper lehnt sich leicht in Richtung Gegner.
                float lean = f.ActionSkill != null && f.ActionWindupTicks > 0 ? f.WindupProgress(_playback.Tick) * 10f : 0f;
                float baseX = body.center.x;
                body.x += isPlayer ? lean : -lean;
                _bodies[index] = body;

                Color c = f.Alive ? color : new Color(0.25f, 0.25f, 0.28f);
                if (_flashUntil.TryGetValue(index, out float flash) && Time.unscaledTime < flash) c = Color.Lerp(c, Color.white, 0.6f);
                Fill(body, c);
                // Gegner: Maus darüber zeigt seine Platine.
                if (!isPlayer) GUI.Label(body, new GUIContent(string.Empty, EnemyTooltip(index)));

                // HP-Balken direkt über dem Körper, Name darüber.
                float barW = column;
                float barX = baseX - barW * 0.5f;
                float barH = isPlayer ? 28f : 22f;
                var hpBar = new Rect(barX, body.y - barH - 8f, barW, barH);
                DrawHpBar(index, f, hpBar, isPlayer ? incoming : 0);
                GUI.Label(new Rect(barX - 20f, hpBar.y - 22f, barW + 40f, 22f), $"<b>{f.Info.Name}</b>", _small);

                // Ausholen der laufenden Aktion unter dem Körper im Boden.
                if (f.Alive && f.ActionSkill != null && f.ActionWindupTicks > 0)
                {
                    bool charging = f.ActionWindupTicks >= SkillDefinition.ChargeThreshold;
                    var bar = new Rect(barX, area.yMax - 36f, barW, 6f);
                    Fill(bar, new Color(0.2f, 0.2f, 0.22f));
                    Fill(new Rect(bar.x, bar.y, bar.width * f.WindupProgress(_playback.Tick), bar.height),
                        charging ? new Color(1f, 0.55f, 0.15f) : new Color(0.7f, 0.7f, 0.75f));
                    string cast = UiTexts.Arena.Cast(SkillInfo.Seconds(f.ActionWindupTicks));
                    if (f.ActionCause == ActionCause.Trigger) cast += $", <color=#ffae42>{UiTexts.Arena.FromComponent(f.ActionCauseRow + 1)}</color>";
                    else if (f.ActionCause == ActionCause.Repeat) cast += $", <color=#9fc7ff>{UiTexts.Arena.Repeat}</color>";
                    string label = charging ? $"<color=#ffae42>{UiTexts.Arena.Charging(BattleLogText.SkillName(f.ActionSkill), cast)}</color>"
                        : $"{BattleLogText.SkillName(f.ActionSkill)} ({cast})";
                    GUI.Label(new Rect(bar.x - 40f, bar.yMax, bar.width + 80f, 22f), label, _small);
                }

                // Oben: Ankündigung (Gegner), dann Ressourcen, Hacks und Zustände.
                float y = area.y + 4f;
                if (!isPlayer)
                {
                    var tele = new Rect(Mathf.Max(area.x, barX - 40f), y, Mathf.Min(area.width, barW + 80f), 30f);
                    DrawTelegraph(f, tele);
                    y += 36f;
                }
                y = DrawResources(f, barX, y, barW);
                y = DrawHacks(index, barX, y, barW);
                DrawStatuses(f, barX, y, barW);
                if (!isPlayer) DrawFighterFlashes(index, body);
            }
        }

        /// <summary>Ressourcen des Kämpfers (Hitze, Ladung, Tempo ...) als Balken.</summary>
        private float DrawResources(FighterView f, float x, float y, float width)
        {
            foreach (ResourceView r in f.Resources)
            {
                var back = new Rect(x, y, width, 10f);
                Fill(back, new Color(0.18f, 0.18f, 0.2f));
                Fill(new Rect(back.x, back.y, back.width * r.Fill, back.height), ResourceColor(r.Id));
                string max = r.Max > 0 ? $"/{r.Max}" : string.Empty;
                GUI.Label(new Rect(x - 20f, back.yMax - 1f, width + 40f, 16f), $"<size=13>{SkillInfo.ResourceName(r.Id)} {r.Value}{max}</size>", _small);
                y += 26f;
            }
            return y;
        }

        /// <summary>
        /// A-21: aktive Hacks auf der Platine eines Kämpfers als flackernde Schilder (Bit Flip, Jam, Hijack) und Firewall-Ladungen.
        /// Beim Spieler stehen die Hacks auch auf der Platine selbst.
        /// </summary>
        private float DrawHacks(int fighter, float x, float y, float width)
        {
            LogicBoard board = _playback.Fighters[fighter].Info.Combatant?.Board;
            var tags = new List<(string id, string text, string tip)>();
            foreach (int relay in _fx.FlippedRelays(fighter))
            {
                string left = RowStateText.Seconds(_fx.FlippedLeft(fighter, relay));
                tags.Add((CircuitEffectIds.BitFlip, UiTexts.Effects.Flipped(left), UiTexts.Effects.FlippedTip(RelayNameOf(board, relay), left)));
            }
            foreach (int relay in _fx.JammedRelays(fighter))
            {
                int left = _fx.JamLeft(fighter, relay);
                tags.Add((CircuitEffectIds.Jam, UiTexts.Effects.Jammed(left), UiTexts.Effects.JammedTip(RelayNameOf(board, relay), left)));
            }
            foreach (int row in _fx.HijackedRows(fighter))
                tags.Add((CircuitEffectIds.Hijack, UiTexts.Effects.HijackMark, UiTexts.Effects.HijackedTip(ComponentNameOf(board, row))));
            int firewall = _fx.Firewall(fighter);
            if (firewall > 0) tags.Add((CircuitEffectIds.Firewall, UiTexts.Effects.Firewall(firewall), UiTexts.Effects.FirewallTip(firewall)));
            if (tags.Count == 0) return y;

            const float h = 18f;
            float w = Mathf.Min(width, 96f);
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width + 4f) / (w + 4f)));
            for (int i = 0; i < tags.Count; i++)
            {
                var box = new Rect(x + i % perRow * (w + 4f), y + i / perRow * (h + 3f), w, h);
                Color c = EffectText.ColourOf(tags[i].id);
                bool steady = tags[i].id == CircuitEffectIds.Firewall;
                bool on = steady || CircuitGrid.FlickerOn;
                Fill(box, new Color(c.r * 0.3f, c.g * 0.3f, c.b * 0.3f, on ? 0.95f : 0.5f));
                if (on) UiTheme.Outline(box, c, 1f);
                GUI.Label(box, new GUIContent($"<size=12><b><color={UiTheme.Hex(c)}>{EffectText.Icon(tags[i].id)} {tags[i].text}</color></b></size>", tags[i].tip), _cell);
            }
            return y + Mathf.CeilToInt(tags.Count / (float)perRow) * (h + 3f) + 2f;
        }

        /// <summary>Blitze (Overheat, Short Circuit …) eines Gegners über seinem Körper; Gegner-Platinen werden nicht gezeichnet.</summary>
        private void DrawFighterFlashes(int fighter, Rect body)
        {
            float now = Time.unscaledTime;
            int n = 0;
            foreach (ActiveFlash a in _effectFlashes)
            {
                if (a.Flash.Fighter != fighter) continue;
                float t = Mathf.Clamp01((now - a.Start) / FlashSeconds);
                var rect = new Rect(body.x, body.y + 14f + n * 18f, body.width, body.height * 0.3f);
                CircuitGrid.DrawFlash(rect, a.Flash.Text, EffectText.ColourOf(a.Flash.EffectId), 1f - t * t);
                n++;
            }
        }

        /// <summary>
        /// Platine eines Gegners als kleines Raster mit Live-Zustand: Regeln der Platine oben, Blitze an Komponenten, darunter
        /// der Basisangriff (leuchtet, wenn er feuert).
        /// </summary>
        private void DrawEnemyBoard(int fighter, EnemyBoardView.Live live, Rect area)
        {
            LogicBoard board = live.Watch.Board;
            float now = Time.unscaledTime;
            live.Pulses.Clear();
            foreach (ActivePulse p in _pulses)
                if (p.Fighter == fighter)
                    live.Pulses.Add(new EnemyBoardView.LivePulse(p.Link, p.Duration <= 0f ? 1f : Mathf.Clamp01((now - p.Start) / p.Duration), p.Power));

            float y = area.y;
            string rules = EffectText.BoardRules(board);
            if (rules.Length > 0)
            {
                if (board.HasBoardEffect(CircuitEffectIds.Firewall)) rules += $"  <color=#9aa4b2>({UiTexts.Effects.Firewall(_fx.Firewall(fighter))})</color>";
                GUI.Label(new Rect(area.x, y, area.width, 18f), new GUIContent($"<size=12>{UiTexts.Effects.Board(rules)}</size>", EffectText.BoardRulesTip(board)), _row);
                y += 18f;
            }
            var gridArea = new Rect(area.x, y, area.width, area.yMax - y - 20f);
            Rect grid = EnemyBoardView.Draw(gridArea, board, out float size, live);

            // Blitze an Komponenten des Gegners (Overheat, Parallel Thread, Short Circuit …).
            foreach (ActiveFlash a in _effectFlashes)
            {
                if (a.Flash.Fighter != fighter || a.Flash.Row < 0 || a.Flash.Row >= board.Rows.Count || !board.Rows[a.Flash.Row].Rect.HasValue) continue;
                float t = Mathf.Clamp01((now - a.Start) / FlashSeconds);
                CircuitGrid.DrawFlash(CircuitGrid.RectOf(grid, size, board.Rows[a.Flash.Row].Rect.Value), a.Flash.Text, EffectText.ColourOf(a.Flash.EffectId), 1f - t * t);
            }

            FighterView f = live.View;
            bool basic = f.Alive && f.ActionSkill != null && f.ActionRow == board.FallbackIndex;
            string text = $"↓ {UiTexts.BasicAttack}";
            GUI.Label(new Rect(area.x, grid.yMax + 2f, area.width, 18f),
                basic ? $"<size=12><color=#ffd75e><b>{text}</b></color></size>" : $"<size=12><color=#9aa4b2>{text}</color></size>", _small);
        }

        private static string RelayNameOf(LogicBoard board, int relay) =>
            board != null && relay >= 0 && relay < board.Relays.Count ? ArenaTexts.RelayName(relay, board.Relays[relay].Label) : $"Relay {relay + 1}";

        private static string ComponentNameOf(LogicBoard board, int row) =>
            ArenaTexts.ComponentName(row, board != null && row >= 0 && row < board.Rows.Count ? board.Rows[row].Skill?.Name ?? "?" : "?");

        /// <summary>Aktive Zustände als kleine Kästchen: Kürzel, Restdauer als Balken und Sekunden, Stapel.</summary>
        private void DrawStatuses(FighterView f, float x, float y, float width)
        {
            const float w = 64f;
            const float h = 30f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width + 4f) / (w + 4f)));
            for (int i = 0; i < f.Statuses.Count; i++)
            {
                StatusView st = f.Statuses[i];
                var box = new Rect(x + i % perRow * (w + 4f), y + i / perRow * (h + 4f), w, h);
                Color c = StatusColor(st.Id);
                Fill(box, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.95f));
                Fill(new Rect(box.x, box.yMax - 4f, box.width * st.Remaining(_playback.Tick), 4f), c);
                string stacks = st.Stacks > 1 ? $" ×{st.Stacks}" : string.Empty;
                string tooltip = UiTexts.Arena.StatusTip(BattleLogText.StatusName(st.Id), RowStateText.Seconds(st.TicksLeft(_playback.Tick)), st.Stacks);
                GUI.Label(box, new GUIContent($"<size=13><b>{StatusShort(st.Id)}</b>{stacks}\n{RowStateText.Seconds(st.TicksLeft(_playback.Tick))}</size>", tooltip), _cell);
            }
        }

        /// <summary>Schwebende Zahlen über dem Ziel. Weiss = Schaden, gelb und grösser = Krit, grün = Heilung, Wörter für Block und Ausweichen.</summary>
        private void DrawPopups()
        {
            float now = Time.unscaledTime;
            foreach (ActivePopup a in _popups)
            {
                if (!_bodies.TryGetValue(a.Popup.TargetIndex, out Rect body)) continue;
                float t = Mathf.Clamp01((now - a.Start) / PopupSeconds);
                Popup p = a.Popup;
                bool crit = p.Kind == PopupKind.Crit;
                float size = crit ? 26f : p.Kind == PopupKind.Blocked || p.Kind == PopupKind.Dodged ? 15f : 19f;
                var rect = new Rect(body.center.x - 60f + a.Offset * 0.6f, body.y - 26f - a.Offset - t * 46f, 120f, size + 10f);

                Color text = PopupColor(p.Kind);
                text.a = 1f - t * t;
                if (p.Row >= 0)
                {
                    // Die Zahl trägt die Farbe der auslösenden Komponente als Unterlage, passend zum Streifen auf der Platine.
                    Color row = RowColorFor(p.Row);
                    row.a = 0.55f * (1f - t * t);
                    var chip = new Rect(rect.center.x - 26f, rect.y + 2f, 52f, rect.height - 4f);
                    Fill(chip, row);
                }
                _popup.fontSize = Mathf.RoundToInt(size);
                _popup.normal.textColor = text;
                GUI.Label(rect, crit ? $"<b>{p.Text}</b>" : p.Text, _popup);
            }
        }

        // ------------------------------------------------------------------ Platine

        private void DrawBoard(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            BattleResult r = _playback.Result;
            LogicBoard board = r.PlayerBoard;
            string legend = board != null && board.Links.Count > 0 ? $"{UiTexts.Arena.BoardLegend} · {UiTexts.Circuit.PulseLegend}" : UiTexts.Arena.BoardLegend;
            GUILayout.Label(new GUIContent($"{UiTexts.Arena.BoardTitle}  <size=13><color=#9aa4b2>{legend}</color></size>", QueueRule), _text);
            string rules = EffectText.BoardRules(board);
            if (rules.Length > 0)
            {
                int firewall = _fx.Firewall(PlayerFighter);
                if (board.HasBoardEffect(CircuitEffectIds.Firewall)) rules += $"  <color=#9aa4b2>({UiTexts.Effects.Firewall(firewall)})</color>";
                GUILayout.Label(new GUIContent($"<size=13>{UiTexts.Effects.Board(rules)}</size>", EffectText.BoardRulesTip(board)), _row);
            }
            if (board?.Layout != null && board.Rows.All(x => x.Rect.HasValue) && board.Relays.All(x => x.Rect.HasValue))
                DrawCircuit(board, area.width - 16f, area.height * 0.5f);
            else
                DrawRowList(r);

            // Basisangriff unter der Platine: leuchtet, wenn er gerade feuert.
            int fallback = r.PlayerRowLabels.Count - 1;
            if (fallback >= 0)
            {
                bool lit = IsRowLit(fallback) || _playback.RowStateAt(fallback) == RowDisplay.Firing;
                string text = $"↓ {UiTexts.FallbackLine}";
                GUILayout.Label(lit ? $"<size=13><color=#ffd75e><b>{text}</b></color></size>" : $"<size=13><color=#9aa4b2>{text}</color></size>", _row);
            }

            // A-13: Warteschlange direkt unter der Platine.
            string queue = _playback.QueueText(Glyph('⏳', "…"));
            GUILayout.Label(new GUIContent(queue.Length > 0 ? $"<size=13><color=#7fd7ff>{queue}</color></size>" : $"<size=13><color=#666b75>{UiTexts.Arena.QueueEmpty}</color></size>",
                QueueRule), _row);
            GUILayout.FlexibleSpace();
            DrawEnemyBoards();
            GUILayout.Label($"<size=12>{StateGlyph(RowDisplay.Firing)} {UiTexts.Arena.StateFiring}   {StateGlyph(RowDisplay.Queued)} {UiTexts.Arena.StateQueued}   "
                + $"{StateGlyph(RowDisplay.Frozen)} {UiTexts.Arena.StateFrozen}   {StateGlyph(RowDisplay.TooLarge)} {UiTexts.Arena.StateTooLarge}   "
                + $"{StateGlyph(RowDisplay.Unpowered)} {UiTexts.Arena.StateUnpowered}</size>", _row);
            GUILayout.EndArea();
        }

        /// <summary>Die Platine als Raster: Relais leuchten beim Auslösen, die feuernde Komponente ist hervorgehoben.</summary>
        private void DrawCircuit(LogicBoard board, float maxWidth, float maxHeight)
        {
            BoardLayout layout = board.Layout;
            float size = CircuitGrid.CellSize(layout.Width, layout.Height, maxWidth, maxHeight, 70f, 22f);
            Rect area = GUILayoutUtility.GetRect(maxWidth, layout.Height * size + 4f);
            var grid = new Rect(area.x + (area.width - layout.Width * size) * 0.5f, area.y + 2f, layout.Width * size, layout.Height * size);
            GUIStyle style = size < 56f ? CircuitGrid.Tiny : CircuitGrid.Label;

            CircuitGrid.DrawBackground(grid, size, layout.Width, layout.Height);

            // Leiterbahnen: Relais → versorgte (türkis, hell beim Auslösen) und zu grosse Komponenten (orange).
            for (int i = 0; i < board.Relays.Count; i++)
            {
                LogicRelay relay = board.Relays[i];
                Rect from = CircuitGrid.RectOf(grid, size, relay.Rect.Value);
                Color trace = _playback.IsRelayLit(i) ? CircuitGrid.RelayLit : CircuitGrid.TraceColor;
                foreach (int row in relay.Powered)
                    if (row >= 0 && row < board.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, board.Rows[row].Rect.Value), trace);
                foreach (int row in relay.TooLarge)
                    if (row >= 0 && row < board.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, board.Rows[row].Rect.Value), CircuitGrid.TooLargeBorder);
                // A-20: Gatter hängen an den berührten Relais.
                foreach (int input in relay.Inputs)
                    if (input >= 0 && input < board.Relays.Count && board.Relays[input].Rect.HasValue)
                        CircuitGrid.DrawTrace(CircuitGrid.RectOf(grid, size, board.Relays[input].Rect.Value), from,
                            _playback.IsRelayOn(input) ? CircuitGrid.GateOpen : CircuitGrid.GateBorder);
            }

            if (layout.Core.HasValue)
            {
                Rect core = CircuitGrid.RectOf(grid, size, layout.Core.Value.X, layout.Core.Value.Y);
                foreach (LogicRow row in board.Rows)
                    if (row.TouchesCore) CircuitGrid.DrawTrace(core, CircuitGrid.RectOf(grid, size, row.Rect.Value), CircuitGrid.CoreColor);
                CircuitGrid.DrawCore(core, UiTexts.Build.CoreName, UiTexts.Build.CoreTip(Betaknight.Core.Circuit.CircuitConfig.Default.CoreBonusPercent), style);
            }

            // A-20: Logik-Chips (Gatter mit Zustand, Kondensator mit Ladung) und Pulsverbindungen unter den Komponenten.
            for (int i = 0; i < board.Chips.Count; i++) DrawLogicChip(board, grid, size, i, style);
            for (int i = 0; i < board.Relays.Count; i++)
                if (board.Relays[i].Gate.HasValue) DrawRelayHack(board, CircuitGrid.RectOf(grid, size, board.Relays[i].Rect.Value), i);
            DrawLinks(board, grid, size);

            for (int i = 0; i < board.Relays.Count; i++)
            {
                LogicRelay relay = board.Relays[i];
                if (relay.Gate.HasValue) continue; // als Chip gezeichnet
                Rect rect = CircuitGrid.RectOf(grid, size, relay.Rect.Value);
                bool lit = _playback.IsRelayLit(i);
                Color fill = lit ? CircuitGrid.RelayLit : CircuitGrid.RelayColor;
                string label = lit ? $"<color=#0b1a1c><b>{relay.Label}</b></color>" : relay.Label;
                string text = $"{RuneText.Difficulty(relay.Difficulty)}\n{label}\n<color={(lit ? "#0b1a1c" : "#9aa4b2")}>×{_playback.RelayCount(i)}</color>";
                CircuitGrid.DrawChip(rect, fill, lit ? Color.white : CircuitGrid.RelayLit, lit ? 3f : 1f, text, RelayTooltip(board, i), style);
                // Lämpchen oben rechts: Relais gerade «an» (Zustand erfüllt), für Gatter wichtig.
                float lamp = Mathf.Clamp(size * 0.12f, 5f, 9f);
                var lampRect = new Rect(rect.xMax - lamp - 3f, rect.y + 3f, lamp, lamp);
                UiTheme.Fill(lampRect, _playback.IsRelayOn(i) ? CircuitGrid.GateOpen : new Color(0f, 0f, 0f, 0.55f));
                UiTheme.Outline(lampRect, CircuitGrid.GateOpen, 1f);
                DrawRelayHack(board, rect, i);
            }

            for (int i = 0; i < board.Rows.Count; i++)
            {
                LogicRow row = board.Rows[i];
                Rect rect = CircuitGrid.RectOf(grid, size, row.Rect.Value);
                RowDisplay state = _playback.RowStateAt(i);
                bool lit = IsRowLit(i) || state == RowDisplay.Firing;
                Color fill = lit ? new Color(0.45f, 0.38f, 0.12f) : state == RowDisplay.Queued ? new Color(0.13f, 0.27f, 0.36f)
                    : state == RowDisplay.Frozen ? new Color(0.20f, 0.30f, 0.42f) : CircuitGrid.ComponentColor;
                Color border = lit ? UiTheme.Accent : StateColor(state);
                string skill = i < _playback.Result.PlayerRowSkills.Count ? _playback.Result.PlayerRowSkills[i] : row.Skill?.Name ?? "—";
                string stateText = state == RowDisplay.Frozen ? $"{UiTexts.Arena.StateFrozen} {RowStateText.Seconds(_playback.FrozenLeft(i))}" : StateName(state);
                string text = $"<b>#{i + 1} {skill}</b>\n<color={UiTheme.Hex(StateColor(state))}>{StateGlyph(state)} {stateText}</color>";
                string charge = _playback.ChargeBadge(i);
                if (charge.Length > 0) text += $"\n<color=#ffd75e>{charge}</color>";
                if (lit) text = $"<color=#ffd75e>{text}</color>";
                CircuitGrid.DrawChip(rect, fill, border, lit ? 3f : 2f, text, RowTooltip(i, state), style);
                Fill(new Rect(rect.x + 2f, rect.y + 2f, 4f, rect.height - 4f), RowColorFor(i));
                DrawRowEffects(board, rect, size, i, state);
            }

            DrawPins(board, grid, size);
            DrawPulses(board, grid, size);
            DrawRowFlashes(board, grid, size);
        }

        // ------------------------------------------------------------------ Eigene Effekte auf der Platine (A-21)

        /// <summary>
        /// Effekte einer Komponente: Symbole oben rechts, Hitze-Balken unten, Rekursions-Tiefe und Verstärkung der laufenden
        /// Ausführung oben links, flackernd bei Hijack.
        /// </summary>
        private void DrawRowEffects(LogicBoard board, Rect rect, float size, int row, RowDisplay state)
        {
            LogicRow r = board.Rows[row];
            CircuitGrid.DrawEffectBadges(rect, EffectText.Of(r), size);

            int max = board.EffectConfig.HeatSkipAt;
            int heat = _fx.Heat(PlayerFighter, row);
            bool skip = _effectFlashes.Exists(f => f.Flash.Fighter == PlayerFighter && f.Flash.Row == row && f.Flash.EffectId == CircuitEffectIds.Overclock);
            CircuitGrid.DrawHeatBar(rect, heat, max, skip, UiTexts.Effects.HeatTip(heat, max));

            if (state == RowDisplay.Firing)
            {
                int depth = _fx.Depth(PlayerFighter, row);
                int power = _fx.Power(PlayerFighter, row);
                CircuitGrid.DrawDepth(rect, depth, UiTexts.Effects.DepthTip(depth, depth * board.EffectConfig.RecursionPowerPercentPerDepth));
                if (power > 0)
                {
                    var tag = new Rect(rect.x + (depth > 0 ? 40f : 8f), rect.y + 2f, 40f, 16f);
                    Color c = EffectText.ColourOf(CircuitEffectIds.Amplifier);
                    Fill(tag, new Color(0f, 0f, 0f, 0.75f));
                    UiTheme.Outline(tag, c, 1f);
                    GUI.Label(tag, new GUIContent($"<b><color={UiTheme.Hex(c)}>{UiTexts.Effects.Power(power)}</color></b>", UiTexts.Effects.PowerTip(power)), CircuitGrid.Tiny);
                }
            }

            if (_fx.IsHijacked(PlayerFighter, row))
                CircuitGrid.DrawHack(rect, CircuitEffectIds.Hijack, UiTexts.Effects.HijackMark, UiTexts.Effects.HijackedTip(ComponentNameOf(board, row)));
        }

        /// <summary>Gehacktes Relais des Spielers: Bit Flip (mit Restzeit) oder Jam (mit Rest-Auslösungen) flackern.</summary>
        private void DrawRelayHack(LogicBoard board, Rect rect, int relay)
        {
            int flipped = _fx.FlippedLeft(PlayerFighter, relay);
            int jam = _fx.JamLeft(PlayerFighter, relay);
            if (flipped > 0)
            {
                string left = RowStateText.Seconds(flipped);
                CircuitGrid.DrawHack(rect, CircuitEffectIds.BitFlip, UiTexts.Effects.Flipped(left), UiTexts.Effects.FlippedTip(RelayNameOf(board, relay), left));
            }
            else if (jam > 0)
            {
                CircuitGrid.DrawHack(rect, CircuitEffectIds.Jam, UiTexts.Effects.Jammed(jam), UiTexts.Effects.JammedTip(RelayNameOf(board, relay), jam));
            }
        }

        /// <summary>Blitze an Komponenten des Spielers (Overheat, Parallel Thread, Interrupt, Overflow, Stack Limit …).</summary>
        private void DrawRowFlashes(LogicBoard board, Rect grid, float size)
        {
            float now = Time.unscaledTime;
            foreach (ActiveFlash a in _effectFlashes)
            {
                if (a.Flash.Fighter != PlayerFighter || a.Flash.Row < 0 || a.Flash.Row >= board.Rows.Count) continue;
                float t = Mathf.Clamp01((now - a.Start) / FlashSeconds);
                CircuitGrid.DrawFlash(CircuitGrid.RectOf(grid, size, board.Rows[a.Flash.Row].Rect.Value), a.Flash.Text,
                    EffectText.ColourOf(a.Flash.EffectId), 1f - t * t);
            }
        }

        // ------------------------------------------------------------------ Logik-Chips, Pins und Pulse (A-20)

        private void DrawLogicChip(LogicBoard board, Rect grid, float size, int index, GUIStyle style)
        {
            LogicChip chip = board.Chips[index];
            Rect rect = CircuitGrid.RectOf(grid, size, chip.Rect);
            int relay = chip.RelayIndex;
            bool gate = chip.Definition.IsGate && relay >= 0;
            var look = new CircuitGrid.ChipLook
            {
                ShowState = gate,
                Open = gate && (_playback.IsRelayOn(relay) || _playback.IsRelayLit(relay)),
                Blown = gate && _playback.IsFuseBlown(relay),
                Charge = _playback.CapacitorCharge(index),
                Capacity = chip.Kind == ChipKind.Capacitor ? board.ChipConfig.CapacitorCapacity : 0,
                Border = gate && _playback.IsRelayLit(relay) ? Color.white : (Color?)null,
            };
            CircuitGrid.DrawLogicChip(rect, chip.Definition, chip.Turns, look, ChipTooltip(board, index), style);
        }

        private string ChipTooltip(LogicBoard board, int index)
        {
            LogicChip chip = board.Chips[index];
            var lines = new List<string> { UiTexts.Circuit.ChipTip(chip.Name, chip.Definition.Description) };
            if (chip.Kind == ChipKind.Diode)
                lines.Add(UiTexts.Circuit.DiodeDirection(UiTexts.Circuit.SideName((int)ChipDefinition.DiodeIn(chip.Turns)),
                    UiTexts.Circuit.SideName((int)ChipDefinition.DiodeOut(chip.Turns))));
            if (chip.Kind == ChipKind.Capacitor)
                lines.Add(UiTexts.Circuit.Capacitor(_playback.CapacitorCharge(index), board.ChipConfig.CapacitorCapacity));
            int relay = chip.RelayIndex;
            if (chip.Definition.IsGate && relay >= 0 && relay < board.Relays.Count)
            {
                LogicRelay g = board.Relays[relay];
                string state = _playback.IsFuseBlown(relay) ? UiTexts.Circuit.Blown
                    : _playback.IsRelayOn(relay) || _playback.IsRelayLit(relay) ? UiTexts.Circuit.Open : UiTexts.Circuit.Closed;
                lines.Add(UiTexts.Circuit.GateState(state, _playback.RelayCount(relay)));
                List<string> inputs = g.Inputs.Where(i => i >= 0 && i < board.Relays.Count).Select(i => board.Relays[i].Label).ToList();
                lines.Add(inputs.Count > 0 ? UiTexts.Circuit.GateInputs(string.Join(", ", inputs)) : UiTexts.Circuit.NoInputs);
                lines.Add(g.Powered.Count > 0 ? UiTexts.Circuit.GatePowers(string.Join(", ", g.Powered.Select(i => $"#{i + 1}"))) : UiTexts.Circuit.GatePowersNothing);
                lines.Add(DifficultyText.Tooltip(g.Difficulty));
            }
            List<string> carried = board.Links.Where(l => l.Path.Contains(chip.Rect.Origin)).Select(l => LinkText(board, l)).ToList();
            if (carried.Count > 0) lines.Add(UiTexts.Circuit.CarriesLinks(string.Join(", ", carried)));
            if (chip.Kind == ChipKind.Effect && EffectText.TryGet(chip.Definition.EffectId, out CircuitEffectDefinition fx)) lines[0] = EffectText.Tip(fx);
            return string.Join("\n", lines);
        }

        /// <summary>Pulsverbindungen als Linien in der Farbe der sendenden Komponente; mit laufendem Puls heller.</summary>
        private void DrawLinks(LogicBoard board, Rect grid, float size)
        {
            if (Event.current.type != EventType.Repaint) return;
            float thickness = Mathf.Clamp(size * 0.05f, 2f, 4f);
            for (int i = 0; i < board.Links.Count; i++)
            {
                PulseLink link = board.Links[i];
                bool active = _pulses.Exists(p => p.Link == i && p.Fighter == PlayerFighter);
                Color color = LinkColorFor(link);
                color.a = active ? 0.95f : 0.4f;
                CircuitGrid.DrawLink(CircuitGrid.LinkPoints(grid, size, link), color, active ? thickness + 1f : thickness);
            }
        }

        private Color LinkColorFor(PulseLink link) => link.From.IsCapacitor ? CircuitGrid.PulseColor : RowColorFor(link.From.Index);

        /// <summary>Pins der Komponenten: typisiert in der Farbe der Art, passend mit weissem Rahmen, verbunden in Gold.</summary>
        private void DrawPins(LogicBoard board, Rect grid, float size)
        {
            int percent = PinConfig.Default.TypedPinBonusPercent;
            foreach (LogicRow row in board.Rows)
                foreach (PlacedPin pin in row.Pins)
                {
                    bool matched = CircuitGrid.IsPinMatched(board, row, pin);
                    bool linked = CircuitGrid.IsPinLinked(board, pin);
                    string kind = pin.IsTyped ? SkillKinds.DisplayName(pin.Kind) : null;
                    CircuitGrid.DrawPin(grid, size, pin, matched, linked, UiTexts.Circuit.PinTip(kind, matched, linked, percent));
                }
        }

        /// <summary>Laufende Pulse als leuchtende Punkte entlang ihrer Verbindung (Start-Pin → Chips → Ziel).</summary>
        private void DrawPulses(LogicBoard board, Rect grid, float size)
        {
            if (Event.current.type != EventType.Repaint) return;
            float now = Time.unscaledTime;
            foreach (ActivePulse p in _pulses)
            {
                if (p.Fighter != PlayerFighter || p.Link < 0 || p.Link >= board.Links.Count) continue;
                PulseLink link = board.Links[p.Link];
                float t = p.Duration <= 0f ? 1f : Mathf.Clamp01((now - p.Start) / p.Duration);
                Vector2 at = CircuitGrid.PointOnLink(CircuitGrid.LinkPoints(grid, size, link), t);
                CircuitGrid.DrawPulse(at, size, Color.Lerp(LinkColorFor(link), CircuitGrid.PulseColor, 0.5f));
                CircuitGrid.DrawPulseGain(at, size, p.Power);
            }
        }

        private static string NodeName(PulseNode node) => node.IsCapacitor ? ArenaTexts.CapacitorName(node.Index) : $"#{node.Index + 1}";

        private static string LinkText(LogicBoard board, PulseLink link)
        {
            string text = UiTexts.Circuit.Link(NodeName(link.From), NodeName(link.To), link.Delay);
            if (link.Amplifiers > 0)
                text += $" <color={EffectText.ColourHex(CircuitEffectIds.Amplifier)}>({UiTexts.Circuit.AmplifiedLink(link.Amplifiers, link.Amplifiers * board.EffectConfig.AmplifierPowerPercent)})</color>";
            return text;
        }

        /// <summary>Weg eines Pulses ausgeschrieben: «#1 → Trace (2, 1) → Diode (3, 1) → #2».</summary>
        private static string PathText(LogicBoard board, PulseLink link)
        {
            var parts = new List<string> { NodeName(link.From) };
            foreach (Cell cell in link.Path)
            {
                LogicChip chip = board.Chips.FirstOrDefault(k => k.Rect.Origin == cell);
                parts.Add(chip != null ? $"{chip.Name} {CircuitGrid.CellText(cell)}" : CircuitGrid.CellText(cell));
            }
            parts.Add(NodeName(link.To));
            return string.Join(" → ", parts);
        }

        /// <summary>Pulse einer Komponente und warum sie gerade feuert oder wartet (für den Tooltip).</summary>
        private List<string> PulseLines(LogicBoard board, int row, RowDisplay state)
        {
            var lines = new List<string>();
            if (board == null || row < 0 || row >= board.Rows.Count) return lines;
            PulseNode node = PulseNode.Component(row);

            FighterView player = _playback.Player;
            if (state == RowDisplay.Firing && player != null && player.ActionRow == row && player.ActionCause == ActionCause.Pulse && player.ActionCauseRow >= 0)
            {
                PulseLink link = board.Links.FirstOrDefault(l => l.To.Equals(node) && l.From.Equals(PulseNode.Component(player.ActionCauseRow)))
                    ?? board.Links.FirstOrDefault(l => l.To.Equals(node));
                lines.Add($"<color=#ffd75e>{UiTexts.Circuit.FiredByPulse(link != null ? PathText(board, link) : NodeName(PulseNode.Component(player.ActionCauseRow)))}</color>");
            }
            else if (state == RowDisplay.Queued)
            {
                // Letzte Protokollzeile zum Einreihen dieser Komponente (nennt bei Pulsen Sender und Relais).
                QueueView q = _playback.Queue.FirstOrDefault(x => x.Row == row);
                if (q != null)
                    for (int i = _playback.Entries.Count - 1; i >= 0; i--)
                    {
                        LogEntry e = _playback.Entries[i];
                        if (e.Tick < q.SinceTick) break;
                        if (e.Row == row && e.Tick == q.SinceTick)
                        {
                            lines.Add(UiTexts.Circuit.Why(e.Text));
                            break;
                        }
                    }
            }

            List<string> from = board.Links.Where(l => l.To.Equals(node)).Select(l => PathText(board, l)).ToList();
            if (from.Count > 0) lines.Add($"<color=#ffd75e>{UiTexts.Circuit.PulsesFrom(string.Join(" · ", from))}</color>");
            List<string> to = board.LinksFrom(node).Select(l => LinkText(board, l)).ToList();
            if (to.Count > 0) lines.Add($"<color=#ffd75e>{UiTexts.Circuit.PulsesTo(string.Join(", ", to))}</color>");
            return lines;
        }

        /// <summary>Ersatz ohne Raster (Platinen ohne Lage, z. B. aus Tests): eine Zeile pro Komponente.</summary>
        private void DrawRowList(BattleResult r)
        {
            for (int i = 0; i < r.PlayerRowLabels.Count - 1; i++)
            {
                string skill = i < r.PlayerRowSkills.Count ? r.PlayerRowSkills[i] : "?";
                RowDisplay state = _playback.RowStateAt(i);
                bool lit = IsRowLit(i) || state == RowDisplay.Firing;

                Rect line = GUILayoutUtility.GetRect(new GUIContent(" "), _row, GUILayout.MinHeight(30f));
                if (lit) Fill(line, new Color(1f, 0.84f, 0.37f, 0.22f));
                Fill(new Rect(line.x, line.y + 3f, 4f, line.height - 6f), RowColorFor(i));

                var badge = new Rect(line.x + 8f, line.y + 4f, 22f, 22f);
                Fill(badge, StateColor(state) * new Color(1f, 1f, 1f, 0.35f));
                GUI.Label(badge, $"<b>{StateGlyph(state)}</b>", _popupSmall);

                string text = $"#{i + 1} [{r.PlayerRowLabels[i]}] → {skill}";
                string chargeBadge = _playback.ChargeBadge(i);
                if (chargeBadge.Length > 0) text += $"  <color=#ffd75e>{chargeBadge}</color>";
                if (lit) text = $"<color=#ffd75e><b>{text}</b></color>";
                else if (state == RowDisplay.Orphaned || state == RowDisplay.Unpowered || state == RowDisplay.TooLarge) text = $"<color=#777777>{text}</color>";
                else if (state == RowDisplay.Queued) text = $"<color=#7fd7ff>{text}</color>";
                GUI.Label(new Rect(badge.xMax + 6f, line.y, line.width - badge.width - 16f, line.height), new GUIContent(text, RowTooltip(i, state)), _row);
            }
        }

        /// <summary>Gegner-Platinen: Name mit Tooltip, der die Platine Zeile für Zeile zeigt.</summary>
        private void DrawEnemyBoards()
        {
            var names = new List<string>();
            string tip = null;
            for (int i = 0; i < _playback.Fighters.Count; i++)
            {
                FighterView f = _playback.Fighters[i];
                if (f.Info.Side == Side.Player) continue;
                names.Add(f.Info.Name);
                tip = tip == null ? EnemyTooltip(i) : $"{tip}\n\n{EnemyTooltip(i)}";
            }
            if (names.Count == 0) return;
            GUILayout.Label(new GUIContent($"<size=13>{UiTexts.Arena.EnemyBoardTitle}: {string.Join(", ", names)}  <color=#9aa4b2>({(_enemyBoards.Count > 0 ? UiTexts.Arena.EnemyBoardsLive : UiTexts.Arena.EnemyBoardHint)})</color></size>", tip), _row);
        }

        /// <summary>Platine eines Gegners als Tooltip-Text («Every 7 s → Ram (2×1): …»), einmal pro Kampf gebaut.</summary>
        private string EnemyTooltip(int fighter)
        {
            if (_enemyTips.TryGetValue(fighter, out string cached)) return cached;
            FighterView f = _playback.Fighters[fighter];
            LogicBoard board = f.Info.Combatant?.Board ?? LogicBoard.FallbackOnly;
            string text = UiTexts.Arena.EnemyTip(f.Info.Name, string.Join("\n", EnemyBoard.Lines(board).Select(l => $"• {l}")));
            // A-21: eigene Effekte und Hacks des Gegners mit Symbol und Text aus den Daten.
            List<string> effects = board.Rows.Where(r => r.Skill != null).SelectMany(r => r.Skill.CircuitEffects).Distinct().ToList();
            foreach (string tip in EffectText.Tips(effects)) text += "\n" + tip;
            string rules = EffectText.BoardRules(board);
            if (rules.Length > 0) text += "\n" + UiTexts.Effects.Board(rules);
            _enemyTips[fighter] = text;
            return text;
        }

        /// <summary>Die Regel der Warteschlange in einem Satz (A-13).</summary>
        private const string QueueRule = QueueConfig.RuleText;

        private string RelayTooltip(LogicBoard board, int relay)
        {
            LogicRelay r = board.Relays[relay];
            string powers = r.Powered.Count > 0 ? UiTexts.Arena.Powers(string.Join(", ", r.Powered.Select(i => $"#{i + 1}"))) : UiTexts.Arena.PowersNothing;
            if (r.TooLarge.Count > 0) powers += $" · {UiTexts.Build.TooLargeHere(string.Join(", ", r.TooLarge.Select(i => $"#{i + 1}")))}";
            return $"{UiTexts.Arena.RelayTip(r.Label, _playback.RelayCount(relay), powers)}\n{DifficultyText.Tooltip(r.Difficulty)}";
        }

        private string RowTooltip(int row, RowDisplay state)
        {
            string now;
            switch (state)
            {
                case RowDisplay.Queued: now = UiTexts.Arena.NowQueued; break;
                case RowDisplay.Firing: now = UiTexts.Arena.NowFiring; break;
                case RowDisplay.Frozen: now = UiTexts.Arena.NowFrozen(RowStateText.Seconds(_playback.FrozenLeft(row))); break;
                case RowDisplay.Unpowered: now = UiTexts.Arena.NowUnpowered; break;
                case RowDisplay.TooLarge: now = UiTexts.Arena.NowTooLarge; break;
                case RowDisplay.Orphaned: now = UiTexts.Arena.NowOrphaned; break;
                default: now = UiTexts.Arena.NowIdle; break;
            }
            LogicBoard board = _playback.Result.PlayerBoard;
            string head = string.Empty;
            if (board != null && row >= 0 && row < board.Rows.Count)
            {
                LogicRow r = board.Rows[row];
                string relays = r.Relays.Count > 0 ? UiTexts.Arena.PoweredBy(string.Join(", ", r.Relays.Select(x => x.Label)))
                    : r.TooLargeFor.Count > 0 ? UiTexts.NotPoweredTooLarge : UiTexts.NotPowered;
                head = UiTexts.Arena.ComponentHead($"#{row + 1} {r.Skill?.Name ?? "—"}", r.Rect?.Shape.ToString() ?? r.Skill?.Shape.ToString() ?? "?", relays) + "\n";
            }
            string skipped = _playback.LastSkipReason(row);
            string text = skipped != null ? UiTexts.Arena.NowAndLast(now, skipped) : UiTexts.Arena.Now(now);
            List<string> pulses = PulseLines(board, row, state);
            if (pulses.Count > 0) text += "\n" + string.Join("\n", pulses);
            if (board != null && row >= 0 && row < board.Rows.Count)
            {
                // A-21: Effekte der Komponente und Hitze.
                foreach (string tip in EffectText.Tips(EffectText.Of(board.Rows[row]))) text += "\n" + tip;
                int heat = _fx.Heat(PlayerFighter, row);
                if (heat > 0) text += "\n" + UiTexts.Effects.HeatTip(heat, board.EffectConfig.HeatSkipAt);
            }
            return $"{head}{text}\n{QueueRule}";
        }

        // ------------------------------------------------------------------ Auswertung nach dem Kampf

        private void DrawReport(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            BattleResult r = _playback.Result;
            GUILayout.Label(UiTexts.Arena.Report(BattleLogText.OutcomeText(r.Outcome), BattleLogText.Time(r.EndTick), _report.TotalDamage, _report.TotalHealing), _text);
            if (_report.TotalDamage > 0)
            {
                // A-12: Skills sollen der Hauptschaden sein; der Anteil steht deshalb gross über der Tabelle.
                string color = _report.BasicAttackShareBp <= BasisPoints.Percent(30) ? "#7ddc6f" : "#ffd75e";
                GUILayout.Label(new GUIContent($"<size=18><b><color={color}>{_report.DamageSplitText}</color></b></size>",
                    UiTexts.Arena.SplitTip), _text);
            }

            _reportScroll = GUILayout.BeginScrollView(_reportScroll);
            float w = area.width - 40f;
            // Pro Komponente: Versorgung, Feuern, Auslösen, Schaden, Anteil, Bonus, Warteschlange, Missed Triggers und ihr Hauptgrund.
            bool reasons = _report.HasMissedTriggers;
            float[] cols = reasons
                ? new[] { w * 0.18f, w * 0.06f, w * 0.06f, w * 0.07f, w * 0.06f, w * 0.06f, w * 0.06f, w * 0.09f, w * 0.09f, w * 0.10f, w * 0.06f, w * 0.11f }
                : new[] { w * 0.21f, w * 0.07f, w * 0.07f, w * 0.08f, w * 0.07f, w * 0.06f, w * 0.06f, w * 0.10f, w * 0.10f, w * 0.11f, w * 0.07f };
            var header = new List<GUIContent>
            {
                new GUIContent(UiTexts.Arena.HeaderRow),
                new GUIContent(UiTexts.Arena.HeaderPower, UiTexts.Arena.HeaderPowerTip),
                new GUIContent(UiTexts.Arena.HeaderFired, UiTexts.Arena.HeaderFiredTip),
                new GUIContent(UiTexts.Arena.HeaderTriggered, UiTexts.Arena.HeaderTriggeredTip),
                new GUIContent(UiTexts.Arena.HeaderDamage),
                new GUIContent(UiTexts.Arena.HeaderHealing),
                new GUIContent(UiTexts.Arena.HeaderShare, UiTexts.Arena.HeaderShareTip),
                new GUIContent(UiTexts.Arena.HeaderBonus, UiTexts.Arena.HeaderBonusTip),
                new GUIContent(UiTexts.Arena.HeaderQueued, UiTexts.Arena.HeaderQueuedTip),
                new GUIContent(UiTexts.Arena.HeaderCharged, UiTexts.Arena.HeaderChargedTip),
                new GUIContent(UiTexts.Arena.HeaderMissed, MissedTriggerTip),
            };
            if (reasons) header.Add(new GUIContent(UiTexts.Arena.HeaderOther, UiTexts.Arena.HeaderOtherTip));
            ReportRow(cols, Color.clear, header.ToArray());
            foreach (RowReport row in _report.Rows)
            {
                string name = row.IsFallback ? $"↓ {row.Name}" : $"{row.Name} {RuneText.Difficulty(row.Difficulty)} <color=#9aa4b2>[{row.Label}]</color>";
                var cells = new List<GUIContent>
                {
                    new GUIContent(name, row.DifficultyText), PowerCell(row),
                    new GUIContent(FiredText(row), row.FromPulse > 0 ? UiTexts.Arena.PulsedByTip(row.PulsedByText) : null),
                    new GUIContent(row.IsFallback ? "–" : $"{row.Triggered}×"), new GUIContent(row.Damage.ToString()), new GUIContent(row.Healing.ToString()),
                    new GUIContent(SkillInfo.Percent(row.DamageShareBp)), new GUIContent(BonusText(row)), new GUIContent(QueueCell(row)),
                    new GUIContent(row.ChargeText.Length > 0 ? $"<color=#ffd75e>{row.ChargeText}</color>" : "–"),
                    new GUIContent(row.Missed > 0 ? $"{row.Missed}×" : "–"),
                };
                if (reasons)
                    cells.Add(new GUIContent(row.MainMissReason.HasValue
                        ? $"{RowStateText.Reason(row.MainMissReason.Value)} ({row.MissCount(row.MainMissReason.Value)}×)" : "–"));
                ReportRow(cols, RowColorFor(row.Index), cells.ToArray());
            }
            if (_report.OtherDamage > 0)
                ReportRow(cols, Color.clear, $"<color=#9aa4b2>{UiTexts.Arena.NoRowDamage}</color>", "", "", "", _report.OtherDamage.ToString());

            GUILayout.Space(8f);
            foreach (string hint in _report.Hints) GUILayout.Label($"• {hint}", _row);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Versorgung einer Komponente im Kampf: ✔ versorgt, «too large» (orange) oder «not powered» (rot).</summary>
        private GUIContent PowerCell(RowReport row)
        {
            if (row.IsFallback) return new GUIContent("–");
            if (row.IsPowered) return new GUIContent($"<color={UiTheme.Hex(CircuitGrid.PoweredBorder)}>✔</color>", UiTexts.Powered);
            if (row.IsTooLargeSomewhere)
                return new GUIContent($"<color={UiTheme.Hex(CircuitGrid.TooLargeBorder)}>{UiTexts.Arena.StateTooLarge}</color>", UiTexts.NotPoweredTooLarge);
            return new GUIContent($"<color={UiTheme.Hex(CircuitGrid.UnpoweredBorder)}>✖</color>", UiTexts.NotPowered);
        }

        /// <summary>Was der Schwierigkeits-Bonus ausgemacht hat: «+140 · −1.5 s cast», «–» ohne Bonus.</summary>
        private static string BonusText(RowReport row)
        {
            if (row.BonusExecutions == 0) return "–";
            var parts = new List<string>();
            if (row.BonusDamage > 0) parts.Add($"+{row.BonusDamage}");
            if (row.BonusHealing > 0) parts.Add($"+{row.BonusHealing} HP");
            if (row.CastSavedTicks > 0) parts.Add(UiTexts.Arena.CastSaved(SkillInfo.Seconds(row.CastSavedTicks)));
            return $"<color=#ffae42>{(parts.Count > 0 ? string.Join(" · ", parts) : $"{row.BonusExecutions}×")}</color>";
        }

        /// <summary>A-13: «4× · Ø 1,2 s» (wie oft eingereiht, mittlere Wartezeit bis zum Start), «–» ohne.</summary>
        private static string QueueCell(RowReport row)
        {
            if (row.Queued == 0) return "–";
            string text = row.StartedFromQueue > 0 ? UiTexts.Arena.QueueAverage(row.Queued, SkillInfo.Seconds(row.AverageWaitTicks)) : $"{row.Queued}×";
            return $"<color=#7fd7ff>{text}</color>";
        }

        /// <summary>«5×», mit Anteil ausgelöster (↪) und wiederholter (↻) Starts.</summary>
        private static string FiredText(RowReport row)
        {
            string text = $"{row.Fired}×";
            if (row.Triggered > 0) text += $" <color=#ffae42>↪{row.Triggered}</color>";
            if (row.FromPulse > 0) text += $" <color=#ffd75e>⚡{row.FromPulse}</color>";
            if (row.Repeated > 0) text += $" <color=#9fc7ff>↻{row.Repeated}</color>";
            return text;
        }

        /// <summary>H-04: Erklärung der Spalte «Missed Trigger» (Kopf der Auswertung).</summary>
        private const string MissedTriggerTip = UiTexts.Arena.HeaderMissedTip;

        private void ReportRow(float[] cols, Color color, params string[] cells)
        {
            var content = new GUIContent[cells.Length];
            for (int i = 0; i < cells.Length; i++) content[i] = new GUIContent(cells[i]);
            ReportRow(cols, color, content);
        }

        private void ReportRow(float[] cols, Color color, params GUIContent[] cells)
        {
            Rect line = GUILayoutUtility.GetRect(new GUIContent(" "), _row, GUILayout.MinHeight(24f));
            if (color.a > 0f) Fill(new Rect(line.x, line.y + 3f, 4f, line.height - 6f), color);
            float x = line.x + 8f;
            for (int i = 0; i < cells.Length && i < cols.Length; i++)
            {
                GUI.Label(new Rect(x, line.y, cols[i], line.height), cells[i], _row);
                x += cols[i];
            }
        }

        // ------------------------------------------------------------------ Protokoll und Steuerung

        private void DrawLog(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(UiTexts.Arena.LogTitle, _text, GUILayout.Width(110f));
            FilterButton(LogFilter.All, UiTexts.Arena.FilterAll);
            FilterButton(LogFilter.Mine, UiTexts.Arena.FilterMine);
            FilterButton(LogFilter.Damage, UiTexts.Arena.FilterDamage);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            var lines = new List<LogEntry>();
            foreach (LogEntry e in MergedLog()) if (e.Matches(_filter)) lines.Add(e);

            if (_playback.IsFinished)
            {
                _logScroll = GUILayout.BeginScrollView(_logScroll);
                foreach (LogEntry e in lines) GUILayout.Label(LogText(e), _logLine);
                GUILayout.EndScrollView();
            }
            else
            {
                int visible = Mathf.Max(1, Mathf.FloorToInt((area.height - 44f) / 19f));
                for (int i = Mathf.Max(0, lines.Count - visible); i < lines.Count; i++) GUILayout.Label(LogText(lines[i]), _logLine);
            }
            GUILayout.EndArea();
        }

        /// <summary>Protokoll der Wiedergabe und Zeilen der eigenen Effekte (A-21), nach Zeit zusammengeführt (zwischengespeichert).</summary>
        private List<LogEntry> MergedLog()
        {
            IReadOnlyList<LogEntry> a = _playback.Entries;
            IReadOnlyList<LogEntry> b = _fx.Entries;
            if (a.Count == _mergedA && b.Count == _mergedB) return _mergedLog;
            _mergedLog.Clear();
            int i = 0, j = 0;
            while (i < a.Count || j < b.Count)
            {
                if (j >= b.Count || (i < a.Count && a[i].Tick <= b[j].Tick)) _mergedLog.Add(a[i++]);
                else _mergedLog.Add(b[j++]);
            }
            _mergedA = a.Count;
            _mergedB = b.Count;
            return _mergedLog;
        }

        private void FilterButton(LogFilter filter, string label)
        {
            string text = _filter == filter ? $"<b>[{label}]</b>" : label;
            if (GUILayout.Button(text, _row, GUILayout.Width(130f), GUILayout.Height(24f))) _filter = filter;
        }

        private string LogText(LogEntry e)
        {
            if (e.Row < 0) return e.Text;
            return $"<color=#{ColorUtility.ToHtmlStringRGB(RowColorFor(e.Row))}>▌</color> {e.Text}";
        }

        private void DrawControls(Rect area)
        {
            GUILayout.BeginArea(area);
            GUILayout.BeginHorizontal();
            if (_playback.IsFinished)
            {
                BattleResult r = _playback.Result;
                string loot = r.IsVictory && _current.Loot != null && _current.Loot.Count > 0 && _current.LootPicks > 0
                    ? $"   <color=#ffd75e>{UiTexts.Arena.LootWaiting(_current.LootPicks)}</color>" : string.Empty;
                GUILayout.Label($"<b>{BattleLogText.OutcomeText(r.Outcome)}</b>   −{_current.DamageTaken} HP   +{_current.GoldReward} Gold{loot}", _text);
                GUILayout.FlexibleSpace();
                if (OnEditBoard != null && r.IsSurvived && GUILayout.Button(UiTexts.Arena.OpenBuild, GUILayout.Width(160f), GUILayout.Height(32f)))
                {
                    _queue.Clear();
                    OpenNext();
                    OnEditBoard();
                }
                if (GUILayout.Button(UiTexts.Arena.Continue, GUILayout.Width(140f), GUILayout.Height(32f))) OpenNext();
            }
            else
            {
                for (int i = 0; i < Speeds.Length; i++)
                {
                    string speedText = $"{Speeds[i]:0.#}×";
                    string label = i == _speedIndex ? $"<b>[{speedText}]</b>" : speedText;
                    if (GUILayout.Button(new GUIContent(label, UiTexts.Arena.SpeedKeyTip(i + 1)), _row, GUILayout.Width(64f), GUILayout.Height(32f))) SetSpeed(i);
                }
                if (GUILayout.Button(new GUIContent(_paused ? UiTexts.Arena.Continue : UiTexts.Arena.Pause, UiTexts.Arena.PauseKeyTip), GUILayout.Width(90f), GUILayout.Height(32f))) _paused = !_paused;
                if (GUILayout.Button(new GUIContent(UiTexts.Arena.NextAction, UiTexts.Arena.NextActionTip), GUILayout.Width(120f), GUILayout.Height(32f))) StepToNextAction();
                GUILayout.Label($"<color=#9aa4b2>{UiTexts.Arena.KeysHint}</color>", _row, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(UiTexts.Arena.Skip, GUILayout.Width(140f), GUILayout.Height(32f)))
                {
                    _playback.SkipToEnd();
                    _playback.TakePopups();
                    _popups.Clear();
                    _pulses.Clear();
                    _fx.Advance(_playback.Tick);
                    _fx.TakeFlashes();
                    _effectFlashes.Clear();
                    foreach (EnemyBoardView.Live live in _enemyBoards.Values)
                    {
                        live.Watch.Advance(_playback.Tick);
                        live.Watch.TakeNewPulses();
                    }
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawTooltip()
        {
            string text = GUI.tooltip;
            // Wie UiTheme.DrawTooltip: zurücksetzen, sonst bleibt der letzte Tooltip stehen.
            if (Event.current.type == EventType.Repaint) GUI.tooltip = string.Empty;
            if (string.IsNullOrEmpty(text)) return;
            Vector2 mouse = Event.current.mousePosition;
            var content = new GUIContent(text);
            float width = 320f;
            float height = _tooltip.CalcHeight(content, width) + 10f;
            var rect = new Rect(Mathf.Min(mouse.x + 16f, Screen.width - width - 8f), Mathf.Min(mouse.y + 16f, Screen.height - height - 8f), width, height);
            Fill(rect, new Color(0.05f, 0.06f, 0.08f, 1f));
            GUI.Label(new Rect(rect.x + 6f, rect.y + 5f, rect.width - 12f, rect.height - 10f), content, _tooltip);
        }

        // ------------------------------------------------------------------ Farben und Zeichen

        private static Color RowColor(int row) => row >= 0 && row < RowColors.Length ? RowColors[row] : FallbackColor;

        private Color RowColorFor(int row) => row == _playback.Result.PlayerRowLabels.Count - 1 ? FallbackColor : RowColor(row);

        private static Color PopupColor(PopupKind kind)
        {
            switch (kind)
            {
                case PopupKind.Crit: return new Color(1f, 0.88f, 0.25f);
                case PopupKind.Heal: return new Color(0.45f, 1f, 0.5f);
                case PopupKind.Blocked: return new Color(0.7f, 0.82f, 1f);
                case PopupKind.Dodged: return new Color(0.8f, 0.8f, 0.85f);
                case PopupKind.SelfDamage: return new Color(1f, 0.55f, 0.45f);
                default: return Color.white;
            }
        }

        internal static Color StateColor(RowDisplay state)
        {
            switch (state)
            {
                case RowDisplay.Firing: return UiTheme.Accent;
                case RowDisplay.Queued: return new Color(0.5f, 0.85f, 1f);
                case RowDisplay.Frozen: return new Color(0.65f, 0.85f, 1f);
                case RowDisplay.TooLarge: return CircuitGrid.TooLargeBorder;
                case RowDisplay.Unpowered: return CircuitGrid.UnpoweredBorder;
                case RowDisplay.Orphaned: return new Color(0.5f, 0.5f, 0.55f);
                default: return CircuitGrid.PoweredBorder;
            }
        }

        internal static string StateName(RowDisplay state)
        {
            switch (state)
            {
                case RowDisplay.Firing: return UiTexts.Arena.StateFiring;
                case RowDisplay.Queued: return UiTexts.Arena.StateQueued;
                case RowDisplay.Frozen: return UiTexts.Arena.StateFrozen;
                case RowDisplay.TooLarge: return UiTexts.Arena.StateTooLarge;
                case RowDisplay.Unpowered: return UiTexts.Arena.StateUnpowered;
                case RowDisplay.Orphaned: return UiTexts.Arena.StateOrphaned;
                default: return UiTexts.Arena.StateIdle;
            }
        }

        private static Color ResourceColor(string id)
        {
            switch (id)
            {
                case ResourceIds.Heat: return new Color(1f, 0.45f, 0.2f);
                case ResourceIds.Charge: return new Color(0.4f, 0.75f, 1f);
                case ResourceIds.Tempo: return new Color(0.6f, 1f, 0.5f);
                default: return new Color(0.8f, 0.8f, 0.6f);
            }
        }

        private static Color StatusColor(string id)
        {
            switch (id)
            {
                case StatusIds.Burn: return new Color(1f, 0.45f, 0.15f);
                case StatusIds.Stun: return new Color(1f, 0.85f, 0.3f);
                case StatusIds.ArmorBreak: return new Color(0.75f, 0.6f, 1f);
                case StatusIds.ShieldWall: return new Color(0.45f, 0.75f, 1f);
                case StatusIds.Blinded: return new Color(0.85f, 0.85f, 0.9f);
                default: return new Color(0.55f, 0.85f, 0.75f);
            }
        }

        private static string StatusShort(string id)
        {
            switch (id)
            {
                case StatusIds.Burn: return UiTexts.Arena.StatusBurn;
                case StatusIds.Stun: return UiTexts.Arena.StatusStun;
                case StatusIds.ArmorBreak: return UiTexts.Arena.StatusArmorBreak;
                case StatusIds.ShieldWall: return UiTexts.Arena.StatusShieldWall;
                case StatusIds.Blinded: return UiTexts.Arena.StatusBlind;
                case StatusIds.Anchor: return UiTexts.Arena.StatusAnchor;
                case StatusIds.Thrusters: return UiTexts.Arena.StatusThrusters;
                case StatusIds.Latency: return UiTexts.Arena.StatusLatency;
                default: return id;
            }
        }

        /// <summary>▶ ⧗ ❄ ⚠ ✖ ⌀, mit Ersatzzeichen, falls die Schrift ein Zeichen nicht kennt.</summary>
        private string StateGlyph(RowDisplay state)
        {
            switch (state)
            {
                case RowDisplay.Firing: return Glyph('▶', ">");
                case RowDisplay.Queued: return Glyph('⧗', "»");
                case RowDisplay.Frozen: return Glyph('❄', "*");
                case RowDisplay.TooLarge: return Glyph('⚠', "!");
                case RowDisplay.Unpowered: return Glyph('✖', "×");
                case RowDisplay.Orphaned: return Glyph('⌀', "Ø");
                default: return "·";
            }
        }

        private string Glyph(char symbol, string fallback)
        {
            if (_glyphs == null) _glyphs = new Dictionary<char, string>();
            if (_glyphs.TryGetValue(symbol, out string cached)) return cached;
            Font font = _row.font != null ? _row.font : GUI.skin.font;
            string result = font != null && font.HasCharacter(symbol) ? symbol.ToString() : fallback;
            _glyphs[symbol] = result;
            return result;
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _white);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_white == null) _white = Texture2D.whiteTexture;
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _centerTitle = new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            _nowLine = new GUIStyle(_row) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            _row.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
            _logLine = new GUIStyle(_row) { fontSize = 13, wordWrap = false };
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _popupSmall = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter };
            _cell = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = false };
            _cell.normal.textColor = Color.white;
            _tooltip = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _tooltip.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
            _thermal = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            _thermal.normal.textColor = new Color(1f, 0.62f, 0.35f);
            _thermalFlash = new GUIStyle(_thermal);
            _thermalFlash.normal.textColor = Color.white;
        }
    }
}
