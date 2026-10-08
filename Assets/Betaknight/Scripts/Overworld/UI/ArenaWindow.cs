using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Arena-Ansicht per IMGUI: spielt das Protokoll eines Kampfs in 2D-Seitenansicht ab (Ritter links, Gegner rechts).
    /// Die Platine (A-19) zeigt live, welches Relais auslöst (leuchtet), welche Komponenten warten, welche feuert und welche
    /// eingefroren, unversorgt oder zu gross sind (Hovern erklärt den Grund); darunter die Warteschlange. Zustände, Ressourcen
    /// und schwebende Zahlen in der Farbe der auslösenden Komponente zeigen die Wirkung. Gegner-Platinen stehen im Tooltip
    /// des Gegners. Nach dem Kampf folgt eine Auswertung pro Komponente. Platzhalter-Grafik aus Rechtecken, gerechnet wird nichts.
    /// </summary>
    public sealed class ArenaWindow : MonoBehaviour
    {
        private static readonly int[] Speeds = { 1, 2, 4 };

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

        private OverworldSession _session;
        private readonly Queue<CombatResult> _queue = new Queue<CombatResult>();
        private BattlePlayback _playback;
        private CombatResult _current;
        private int _speedIndex;
        private bool _paused;
        private float _tickBuffer;
        private Vector2 _logScroll;
        private Vector2 _reportScroll;
        private LogFilter _filter;
        private BattleReport _report;
        private readonly List<ActivePopup> _popups = new List<ActivePopup>();
        private readonly Dictionary<int, Rect> _bodies = new Dictionary<int, Rect>();
        private readonly Dictionary<int, int> _seenHitTick = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _flashUntil = new Dictionary<int, float>();
        private readonly Dictionary<int, string> _enemyTips = new Dictionary<int, string>();
        private int _seenRowTick = -1000;
        private float _rowHighlightUntil;
        private Dictionary<char, string> _glyphs;
        private GUIStyle _popup;
        private GUIStyle _popupSmall;
        private GUIStyle _cell;
        private GUIStyle _logLine;
        private GUIStyle _tooltip;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _row;
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
            _report = null;
            _paused = false;
            _tickBuffer = 0f;
            _logScroll = Vector2.zero;
            _reportScroll = Vector2.zero;
            _popups.Clear();
            _seenHitTick.Clear();
            _flashUntil.Clear();
            _enemyTips.Clear();
            _seenRowTick = -1000;
            _rowHighlightUntil = 0f;
        }

        private void Update()
        {
            if (_playback == null) return;
            _popups.RemoveAll(p => Time.unscaledTime - p.Start > PopupSeconds);
            if (_paused || _playback.IsFinished) return;
            _tickBuffer += Time.unscaledDeltaTime * Ticks.PerSecond * Speeds[_speedIndex] * Mathf.Max(0.01f, SpeedFactor);
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
            float gameSeconds = BattlePlayback.RowHighlightTicks / (float)Ticks.PerSecond / (Speeds[_speedIndex] * Mathf.Max(0.01f, SpeedFactor));
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

            // Viele Zahlen im selben Moment fächern sich leicht auf, statt sich zu überdecken.
            foreach (Popup p in _playback.TakePopups())
            {
                int stacked = _popups.Count(a => a.Popup.TargetIndex == p.TargetIndex && now - a.Start < 0.25f);
                _popups.Add(new ActivePopup { Popup = p, Start = now, Offset = stacked * 18f });
            }
        }

        private bool IsRowLit(int row) =>
            row == _playback.LastPlayerRow && (Time.unscaledTime < _rowHighlightUntil || _playback.IsRowHighlighted(row));

        private void OnGUI()
        {
            UiTheme.Apply();
            if (_playback == null) return;
            EnsureStyles();
            GUI.depth = -10;

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

            if (_playback.IsFinished)
            {
                if (_report == null) _report = BattleReport.Create(_playback.Result);
                DrawReport(stage);
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

        // ------------------------------------------------------------------ Bühne

        private void DrawStage(Rect area)
        {
            Fill(area, new Color(0.09f, 0.10f, 0.13f));
            Fill(new Rect(area.x, area.yMax - 40f, area.width, 40f), new Color(0.14f, 0.15f, 0.18f));

            var players = new List<int>();
            var enemies = new List<int>();
            for (int i = 0; i < _playback.Fighters.Count; i++) (_playback.Fighters[i].Info.Side == Side.Player ? players : enemies).Add(i);

            DrawSide(players, new Rect(area.x + 24f, area.y, area.width * 0.4f - 24f, area.height), new Color(0.40f, 0.85f, 1.00f));
            DrawSide(enemies, new Rect(area.x + area.width * 0.6f, area.y, area.width * 0.4f - 24f, area.height), new Color(0.80f, 0.35f, 0.30f));
        }

        private void DrawSide(List<int> indices, Rect area, Color color)
        {
            if (indices.Count == 0) return;
            float slot = area.width / indices.Count;
            for (int n = 0; n < indices.Count; n++)
            {
                int index = indices[n];
                FighterView f = _playback.Fighters[index];
                float bodyW = Mathf.Min(70f, slot * 0.6f);
                float bodyH = bodyW * 1.5f;
                var body = new Rect(area.x + slot * n + (slot - bodyW) * 0.5f, area.yMax - 40f - bodyH, bodyW, bodyH);

                // Ausholen: Körper lehnt sich leicht in Richtung Gegner.
                float lean = f.ActionSkill != null && f.ActionWindupTicks > 0 ? f.WindupProgress(_playback.Tick) * 10f : 0f;
                body.x += f.Info.Side == Side.Player ? lean : -lean;
                _bodies[index] = body;

                Color c = f.Alive ? color : new Color(0.25f, 0.25f, 0.28f);
                if (_flashUntil.TryGetValue(index, out float flash) && Time.unscaledTime < flash) c = Color.Lerp(c, Color.white, 0.6f);
                Fill(body, c);
                // Gegner: Maus darüber zeigt seine Platine.
                if (f.Info.Side != Side.Player) GUI.Label(body, new GUIContent(string.Empty, EnemyTooltip(index)));

                float barX = body.x - 30f;
                float barW = body.width + 60f;
                var hpBack = new Rect(barX, area.y + 64f, barW, 12f);
                Fill(hpBack, new Color(0.2f, 0.2f, 0.22f));
                float hp = f.Info.MaxHp > 0 ? Mathf.Clamp01(f.Hp / (float)f.Info.MaxHp) : 0f;
                Fill(new Rect(hpBack.x, hpBack.y, hpBack.width * hp, hpBack.height), Color.Lerp(new Color(0.85f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.45f), hp));
                GUI.Label(new Rect(hpBack.x - 20f, hpBack.y - 40f, hpBack.width + 40f, 40f), $"<b>{f.Info.Name}</b>\n{f.Hp}/{f.Info.MaxHp}", _small);

                float y = hpBack.yMax + 4f;
                if (f.Alive && f.ActionSkill != null && f.ActionWindupTicks > 0)
                {
                    bool charging = f.ActionWindupTicks >= SkillDefinition.ChargeThreshold;
                    var bar = new Rect(hpBack.x, y, hpBack.width, 6f);
                    Fill(bar, new Color(0.2f, 0.2f, 0.22f));
                    Fill(new Rect(bar.x, bar.y, bar.width * f.WindupProgress(_playback.Tick), bar.height),
                        charging ? new Color(1f, 0.55f, 0.15f) : new Color(0.7f, 0.7f, 0.75f));
                    string cast = UiTexts.Arena.Cast(SkillInfo.Seconds(f.ActionWindupTicks));
                    if (f.ActionCause == ActionCause.Trigger) cast += $", <color=#ffae42>{UiTexts.Arena.FromComponent(f.ActionCauseRow + 1)}</color>";
                    else if (f.ActionCause == ActionCause.Repeat) cast += $", <color=#9fc7ff>{UiTexts.Arena.Repeat}</color>";
                    string label = charging ? $"<color=#ffae42>{UiTexts.Arena.Charging(BattleLogText.SkillName(f.ActionSkill), cast)}</color>"
                        : $"{BattleLogText.SkillName(f.ActionSkill)} ({cast})";
                    GUI.Label(new Rect(bar.x - 30f, bar.yMax, bar.width + 60f, 20f), label, _small);
                }
                y += 28f;

                y = DrawResources(f, barX, y, barW);
                DrawStatuses(f, barX, y, barW);
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
            GUILayout.Label(new GUIContent($"{UiTexts.Arena.BoardTitle}  <size=13><color=#9aa4b2>{UiTexts.Arena.BoardLegend}</color></size>", QueueRule), _text);
            BattleResult r = _playback.Result;
            LogicBoard board = r.PlayerBoard;
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
            }

            if (layout.Core.HasValue)
            {
                Rect core = CircuitGrid.RectOf(grid, size, layout.Core.Value.X, layout.Core.Value.Y);
                foreach (LogicRow row in board.Rows)
                    if (row.TouchesCore) CircuitGrid.DrawTrace(core, CircuitGrid.RectOf(grid, size, row.Rect.Value), CircuitGrid.CoreColor);
                CircuitGrid.DrawCore(core, UiTexts.Build.CoreName, UiTexts.Build.CoreTip(Betaknight.Core.Circuit.CircuitConfig.Default.CoreBonusPercent), style);
            }

            for (int i = 0; i < board.Relays.Count; i++)
            {
                LogicRelay relay = board.Relays[i];
                Rect rect = CircuitGrid.RectOf(grid, size, relay.Rect.Value);
                bool lit = _playback.IsRelayLit(i);
                Color fill = lit ? CircuitGrid.RelayLit : CircuitGrid.RelayColor;
                string label = lit ? $"<color=#0b1a1c><b>{relay.Label}</b></color>" : relay.Label;
                string text = $"{RuneText.Difficulty(relay.Difficulty)}\n{label}\n<color={(lit ? "#0b1a1c" : "#9aa4b2")}>×{_playback.RelayCount(i)}</color>";
                CircuitGrid.DrawChip(rect, fill, lit ? Color.white : CircuitGrid.RelayLit, lit ? 3f : 1f, text, RelayTooltip(board, i), style);
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
                if (lit) text = $"<color=#ffd75e>{text}</color>";
                CircuitGrid.DrawChip(rect, fill, border, lit ? 3f : 2f, text, RowTooltip(i, state), style);
                Fill(new Rect(rect.x + 2f, rect.y + 2f, 4f, rect.height - 4f), RowColorFor(i));
            }
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
            GUILayout.Label(new GUIContent($"<size=13>{UiTexts.Arena.EnemyBoardTitle}: {string.Join(", ", names)}  <color=#9aa4b2>({UiTexts.Arena.EnemyBoardHint})</color></size>", tip), _row);
        }

        /// <summary>Platine eines Gegners als Tooltip-Text («Every 7 s → Ram (2×1): …»), einmal pro Kampf gebaut.</summary>
        private string EnemyTooltip(int fighter)
        {
            if (_enemyTips.TryGetValue(fighter, out string cached)) return cached;
            FighterView f = _playback.Fighters[fighter];
            LogicBoard board = f.Info.Combatant?.Board ?? LogicBoard.FallbackOnly;
            string text = UiTexts.Arena.EnemyTip(f.Info.Name, string.Join("\n", EnemyBoard.Lines(board).Select(l => $"• {l}")));
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
                ? new[] { w * 0.20f, w * 0.07f, w * 0.07f, w * 0.07f, w * 0.07f, w * 0.06f, w * 0.06f, w * 0.10f, w * 0.10f, w * 0.07f, w * 0.13f }
                : new[] { w * 0.24f, w * 0.08f, w * 0.08f, w * 0.08f, w * 0.08f, w * 0.07f, w * 0.07f, w * 0.12f, w * 0.10f, w * 0.08f };
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
                new GUIContent(UiTexts.Arena.HeaderMissed, MissedTriggerTip),
            };
            if (reasons) header.Add(new GUIContent(UiTexts.Arena.HeaderOther, UiTexts.Arena.HeaderOtherTip));
            ReportRow(cols, Color.clear, header.ToArray());
            foreach (RowReport row in _report.Rows)
            {
                string name = row.IsFallback ? $"↓ {row.Name}" : $"{row.Name} {RuneText.Difficulty(row.Difficulty)} <color=#9aa4b2>[{row.Label}]</color>";
                var cells = new List<GUIContent>
                {
                    new GUIContent(name, row.DifficultyText), PowerCell(row), new GUIContent(FiredText(row)),
                    new GUIContent(row.IsFallback ? "–" : $"{row.Triggered}×"), new GUIContent(row.Damage.ToString()), new GUIContent(row.Healing.ToString()),
                    new GUIContent(SkillInfo.Percent(row.DamageShareBp)), new GUIContent(BonusText(row)), new GUIContent(QueueCell(row)),
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
            foreach (LogEntry e in _playback.Entries) if (e.Matches(_filter)) lines.Add(e);

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
                GUILayout.Label($"<b>{BattleLogText.OutcomeText(r.Outcome)}</b>   −{_current.DamageTaken} HP   +{_current.GoldReward} Gold", _text);
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
                    string label = i == _speedIndex ? $"<b>[{Speeds[i]}×]</b>" : $"{Speeds[i]}×";
                    if (GUILayout.Button(label, _row, GUILayout.Width(60f), GUILayout.Height(32f))) _speedIndex = i;
                }
                if (GUILayout.Button(_paused ? UiTexts.Arena.Continue : UiTexts.Arena.Pause, GUILayout.Width(90f), GUILayout.Height(32f))) _paused = !_paused;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(UiTexts.Arena.Skip, GUILayout.Width(140f), GUILayout.Height(32f)))
                {
                    _playback.SkipToEnd();
                    _playback.TakePopups();
                    _popups.Clear();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip)) return;
            Vector2 mouse = Event.current.mousePosition;
            var content = new GUIContent(GUI.tooltip);
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

        private static Color StateColor(RowDisplay state)
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

        private static string StateName(RowDisplay state)
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
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            _row.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
            _logLine = new GUIStyle(_row) { fontSize = 13, wordWrap = false };
            _popup = new GUIStyle(GUI.skin.label) { fontSize = 19, richText = true, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _popupSmall = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter };
            _cell = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = false };
            _cell.normal.textColor = Color.white;
            _tooltip = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
            _tooltip.normal.textColor = new Color(0.92f, 0.94f, 0.98f);
        }
    }
}
