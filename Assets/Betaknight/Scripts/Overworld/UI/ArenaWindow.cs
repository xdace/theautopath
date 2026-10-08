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
    /// Die Logik-Tafel zeigt live, welche Zeile bereit ist, warum andere übersprungen werden (Hovern) und welche feuert;
    /// Zustände, Ressourcen und schwebende Zahlen in der Farbe der auslösenden Zeile zeigen die Wirkung.
    /// Nach dem Kampf folgt eine Auswertung pro Zeile. Platzhalter-Grafik aus Rechtecken, gerechnet wird nichts.
    /// </summary>
    public sealed class ArenaWindow : MonoBehaviour
    {
        private static readonly int[] Speeds = { 1, 2, 4 };

        /// <summary>Hervorhebungen bleiben mindestens so lange sichtbar (Echtzeit), auch bei 4×.</summary>
        private const float MinHighlightSeconds = 0.35f;
        private const float PopupSeconds = 1.1f;

        /// <summary>Farbe je Tafel-Zeile; die Fallback-Zeile (Basisangriff) ist grau.</summary>
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

        /// <summary>«Tafel bearbeiten» aus der Auswertung: schliesst die Arena und öffnet den Tafel-Editor.</summary>
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
            float boardWidth = Mathf.Min(360f, Screen.width * 0.33f);
            var stage = new Rect(pad, 56f, Screen.width - boardWidth - pad * 3, Screen.height * 0.52f);
            var board = new Rect(stage.xMax + pad, 56f, boardWidth, stage.height);
            var log = new Rect(pad, stage.yMax + pad, Screen.width - pad * 2, Screen.height - stage.yMax - pad * 2 - 44f);
            var controls = new Rect(pad, Screen.height - pad - 36f, Screen.width - pad * 2, 36f);

            string enemy = string.IsNullOrEmpty(_current.EnemyName) ? "Gegner" : _current.EnemyName;
            string portal = string.Empty;
            int survive = _playback.Result.SurviveTicks;
            if (survive > 0)
            {
                int left = Mathf.Max(0, survive - _playback.Tick);
                portal = left > 0 ? $"   <color=#b18cff>Portal öffnet in {BattleLogText.Time(left)}</color>" : "   <color=#b18cff>Portal offen</color>";
            }
            GUI.Label(new Rect(pad, 12f, Screen.width - pad * 2, 36f),
                $"<b>Arena</b> – Ritter gegen {enemy}   <color=#9aa4b2>{BattleLogText.Time(_playback.Tick)}</color>{portal}", _title);

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
                    string cast = $"Cast {SkillInfo.Seconds(f.ActionWindupTicks)}";
                    if (f.ActionCause == ActionCause.Trigger) cast += $", <color=#ffae42>↪ von Zeile {f.ActionCauseRow + 1}</color>";
                    else if (f.ActionCause == ActionCause.Repeat) cast += ", <color=#9fc7ff>↻ Wiederholung</color>";
                    string label = charging ? $"<color=#ffae42>lädt auf: {BattleLogText.SkillName(f.ActionSkill)} ({cast})</color>"
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
                string tooltip = $"{BattleLogText.StatusName(st.Id)}: noch {RowStateText.Seconds(st.TicksLeft(_playback.Tick))}{(st.Stacks > 1 ? $", {st.Stacks} Stapel" : string.Empty)}";
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
                    // Die Zahl trägt die Farbe der auslösenden Zeile als Unterlage, passend zum Streifen auf der Tafel.
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

        // ------------------------------------------------------------------ Logik-Tafel

        private void DrawBoard(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("<b>Logik-Tafel</b>  <size=13><color=#9aa4b2>Zeile hovern für den Grund</color></size>", _text);
            BattleResult r = _playback.Result;
            for (int i = 0; i < r.PlayerRowLabels.Count; i++)
            {
                bool fallback = i == r.PlayerRowLabels.Count - 1;
                string skill = i < r.PlayerRowSkills.Count ? r.PlayerRowSkills[i] : "?";
                string prefix = fallback ? "↓" : $"{i + 1}.";
                RowDisplay state = _playback.RowStateAt(i);
                bool lit = IsRowLit(i);

                Rect line = GUILayoutUtility.GetRect(new GUIContent(" "), _row, GUILayout.MinHeight(30f));
                if (lit) Fill(line, new Color(1f, 0.84f, 0.37f, 0.22f));
                Fill(new Rect(line.x, line.y + 3f, 4f, line.height - 6f), RowColorFor(i));

                var badge = new Rect(line.x + 8f, line.y + 4f, 22f, 22f);
                Fill(badge, StateColor(state) * new Color(1f, 1f, 1f, 0.35f));
                GUI.Label(badge, $"<b>{StateGlyph(state)}</b>", _popupSmall);

                string text = $"{prefix} [{r.PlayerRowLabels[i]}] → {skill}";
                if (lit) text = $"<color=#ffd75e><b>{text}</b></color>";
                else if (state == RowDisplay.Orphaned || skill == "—") text = $"<color=#777777>{text}</color>";
                GUI.Label(new Rect(badge.xMax + 6f, line.y, line.width - badge.width - 16f, line.height), new GUIContent(text, RowTooltip(i, state)), _row);

                if (state == RowDisplay.Cooldown)
                {
                    float frac = _playback.CooldownFraction(i);
                    var bar = new Rect(badge.xMax + 6f, line.yMax - 4f, (line.width - badge.width - 16f), 3f);
                    Fill(bar, new Color(0.2f, 0.2f, 0.22f));
                    Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height), new Color(1f, 0.7f, 0.25f));
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=13>{StateGlyph(RowDisplay.Ready)} bereit   {StateGlyph(RowDisplay.ConditionFalse)} Bedingung falsch   "
                + $"{StateGlyph(RowDisplay.Cooldown)} Cooldown   {StateGlyph(RowDisplay.Orphaned)} verwaist</size>", _row);
            GUILayout.EndArea();
        }

        private string RowTooltip(int row, RowDisplay state)
        {
            string now;
            switch (state)
            {
                case RowDisplay.Ready: now = "Bedingung erfüllt, Skill bereit"; break;
                case RowDisplay.ConditionFalse: now = "Bedingung nicht erfüllt"; break;
                case RowDisplay.Cooldown: now = $"Skill im Cooldown (noch {RowStateText.Seconds(_playback.CooldownLeft(row))})"; break;
                case RowDisplay.Orphaned: now = "verwaist (kein Skill)"; break;
                default: now = "noch keine Entscheidung"; break;
            }
            string skipped = _playback.LastSkipReason(row);
            return skipped != null ? $"Jetzt: {now}\nZuletzt übersprungen bei {skipped}" : $"Jetzt: {now}";
        }

        // ------------------------------------------------------------------ Auswertung nach dem Kampf

        private void DrawReport(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            BattleResult r = _playback.Result;
            GUILayout.Label($"<b>Auswertung</b>   {BattleLogText.OutcomeText(r.Outcome)}, {BattleLogText.Time(r.EndTick)}, "
                + $"Schaden gesamt {_report.TotalDamage}, Heilung {_report.TotalHealing}", _text);

            _reportScroll = GUILayout.BeginScrollView(_reportScroll);
            float w = area.width - 40f;
            float[] cols = { w * 0.28f, w * 0.08f, w * 0.07f, w * 0.08f, w * 0.07f, w * 0.07f, w * 0.13f, w * 0.08f, w * 0.14f };
            ReportRow(cols, Color.clear, "<b>Zeile</b>", "<b>gefeuert</b>", "<b>erfüllt</b>", "<b>Schaden</b>", "<b>Heilung</b>", "<b>Anteil</b>",
                "<b>Bonus</b>", "<b>übersprungen</b>", "<b>häufigster Grund</b>");
            foreach (RowReport row in _report.Rows)
            {
                string prefix = row.IsFallback ? "↓" : $"{row.Index + 1}. {RuneText.Difficulty(row.Difficulty)}";
                string reason = row.MainReason.HasValue ? RowStateText.Reason(row.MainReason.Value) : "–";
                string met = row.IsFallback || row.ConditionMet < 0 ? "–" : $"{row.ConditionMet}×";
                ReportRow(cols, RowColorFor(row.Index), $"{prefix} [{row.Label}] → {row.Skill}", FiredText(row), met, row.Damage.ToString(),
                    row.Healing.ToString(), SkillInfo.Percent(row.DamageShareBp), BonusText(row), row.Skipped > 0 ? $"{row.Skipped}×" : "–", reason);
            }
            if (_report.OtherDamage > 0)
                ReportRow(cols, Color.clear, "<color=#9aa4b2>ohne Zeile (Set-Boni, Rückschlag)</color>", "", "", _report.OtherDamage.ToString(), "", "", "", "", "");

            GUILayout.Space(8f);
            foreach (string hint in _report.Hints) GUILayout.Label($"• {hint}", _row);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        /// <summary>Was der Schwierigkeits-Bonus ausgemacht hat: «+140 · −6 s CD», «–» ohne Bonus.</summary>
        private static string BonusText(RowReport row)
        {
            if (row.BonusExecutions == 0) return "–";
            var parts = new List<string>();
            if (row.BonusDamage > 0) parts.Add($"+{row.BonusDamage}");
            if (row.BonusHealing > 0) parts.Add($"+{row.BonusHealing} HP");
            if (row.CooldownSavedTicks > 0) parts.Add($"−{SkillInfo.Seconds(row.CooldownSavedTicks)} CD");
            return $"<color=#ffae42>{(parts.Count > 0 ? string.Join(" · ", parts) : $"{row.BonusExecutions}×")}</color>";
        }

        /// <summary>«5×», mit Anteil ausgelöster (↪) und wiederholter (↻) Starts.</summary>
        private static string FiredText(RowReport row)
        {
            string text = $"{row.Fired}×";
            if (row.Triggered > 0) text += $" <color=#ffae42>↪{row.Triggered}</color>";
            if (row.Repeated > 0) text += $" <color=#9fc7ff>↻{row.Repeated}</color>";
            return text;
        }

        private void ReportRow(float[] cols, Color color, params string[] cells)
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
            GUILayout.Label("<b>Protokoll</b>", _text, GUILayout.Width(110f));
            FilterButton(LogFilter.All, "Alles");
            FilterButton(LogFilter.Mine, "Meine Aktionen");
            FilterButton(LogFilter.Damage, "Nur Schaden");
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
                if (OnEditBoard != null && r.IsSurvived && GUILayout.Button("Build öffnen (B)", GUILayout.Width(160f), GUILayout.Height(32f)))
                {
                    _queue.Clear();
                    OpenNext();
                    OnEditBoard();
                }
                if (GUILayout.Button("Weiter", GUILayout.Width(140f), GUILayout.Height(32f))) OpenNext();
            }
            else
            {
                for (int i = 0; i < Speeds.Length; i++)
                {
                    string label = i == _speedIndex ? $"<b>[{Speeds[i]}×]</b>" : $"{Speeds[i]}×";
                    if (GUILayout.Button(label, _row, GUILayout.Width(60f), GUILayout.Height(32f))) _speedIndex = i;
                }
                if (GUILayout.Button(_paused ? "Weiter" : "Pause", GUILayout.Width(90f), GUILayout.Height(32f))) _paused = !_paused;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Überspringen", GUILayout.Width(140f), GUILayout.Height(32f)))
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
                case RowDisplay.Ready: return new Color(0.4f, 0.95f, 0.5f);
                case RowDisplay.ConditionFalse: return new Color(1f, 0.4f, 0.35f);
                case RowDisplay.Cooldown: return new Color(1f, 0.7f, 0.25f);
                default: return new Color(0.5f, 0.5f, 0.55f);
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
                case StatusIds.Burn: return "Brand";
                case StatusIds.Stun: return "Betäubt";
                case StatusIds.ArmorBreak: return "R.-Bruch";
                case StatusIds.ShieldWall: return "Schild";
                case StatusIds.Blinded: return "Blind";
                case StatusIds.Anchor: return "Anker";
                case StatusIds.Thrusters: return "Düsen";
                default: return id;
            }
        }

        /// <summary>✔ ✖ ⏳ ⌀, mit Ersatzzeichen, falls die Schrift ein Zeichen nicht kennt.</summary>
        private string StateGlyph(RowDisplay state)
        {
            switch (state)
            {
                case RowDisplay.Ready: return Glyph('✔', "+");
                case RowDisplay.ConditionFalse: return Glyph('✖', "×");
                case RowDisplay.Cooldown: return Glyph('⏳', "…");
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
