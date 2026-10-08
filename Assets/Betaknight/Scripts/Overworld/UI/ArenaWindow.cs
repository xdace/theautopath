using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Arena-Ansicht per IMGUI: spielt das Protokoll eines Kampfs in 2D-Seitenansicht ab (Ritter links, Gegner rechts),
    /// lässt die feuernde Zeile der Logik-Tafel aufleuchten und zeigt danach das Protokoll.
    /// Platzhalter-Grafik aus Rechtecken, bis es Sprites gibt. Gerechnet wird nichts, nur abgespielt.
    /// </summary>
    public sealed class ArenaWindow : MonoBehaviour
    {
        private static readonly int[] Speeds = { 1, 2, 4 };

        private OverworldSession _session;
        private readonly Queue<CombatResult> _queue = new Queue<CombatResult>();
        private BattlePlayback _playback;
        private CombatResult _current;
        private int _speedIndex;
        private bool _paused;
        private float _tickBuffer;
        private Vector2 _logScroll;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _row;
        private Texture2D _white;

        /// <summary>Solange die Arena offen ist, warten Oberwelt und andere Fenster.</summary>
        public bool IsOpen => _playback != null;

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
            _paused = false;
            _tickBuffer = 0f;
            _logScroll = Vector2.zero;
        }

        private void Update()
        {
            if (_playback == null || _paused || _playback.IsFinished) return;
            _tickBuffer += Time.unscaledDeltaTime * Ticks.PerSecond * Speeds[_speedIndex];
            int ticks = Mathf.FloorToInt(_tickBuffer);
            if (ticks <= 0) return;
            _tickBuffer -= ticks;
            _playback.Advance(ticks);
        }

        private void OnGUI()
        {
            if (_playback == null) return;
            EnsureStyles();
            GUI.depth = -10;

            var screen = new Rect(0, 0, Screen.width, Screen.height);
            Fill(screen, new Color(0.04f, 0.05f, 0.07f, 0.96f));

            const float pad = 16f;
            float boardWidth = Mathf.Min(340f, Screen.width * 0.32f);
            var stage = new Rect(pad, 56f, Screen.width - boardWidth - pad * 3, Screen.height * 0.5f);
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

            DrawStage(stage);
            DrawBoard(board);
            DrawLog(log);
            DrawControls(controls);
        }

        // ------------------------------------------------------------------ Bühne

        private void DrawStage(Rect area)
        {
            Fill(area, new Color(0.09f, 0.10f, 0.13f));
            Fill(new Rect(area.x, area.yMax - 40f, area.width, 40f), new Color(0.14f, 0.15f, 0.18f));

            var players = new List<FighterView>();
            var enemies = new List<FighterView>();
            foreach (FighterView f in _playback.Fighters) (f.Info.Side == Side.Player ? players : enemies).Add(f);

            DrawSide(players, new Rect(area.x + 24f, area.y, area.width * 0.4f - 24f, area.height), new Color(0.40f, 0.85f, 1.00f));
            DrawSide(enemies, new Rect(area.x + area.width * 0.6f, area.y, area.width * 0.4f - 24f, area.height), new Color(0.80f, 0.35f, 0.30f));
        }

        private void DrawSide(List<FighterView> fighters, Rect area, Color color)
        {
            if (fighters.Count == 0) return;
            float slot = area.width / fighters.Count;
            for (int i = 0; i < fighters.Count; i++)
            {
                FighterView f = fighters[i];
                float bodyW = Mathf.Min(70f, slot * 0.6f);
                float bodyH = bodyW * 1.5f;
                var body = new Rect(area.x + slot * i + (slot - bodyW) * 0.5f, area.yMax - 40f - bodyH, bodyW, bodyH);

                // Ausholen: Körper lehnt sich leicht in Richtung Gegner.
                float lean = f.ActionSkill != null && f.ActionWindupTicks > 0 ? f.WindupProgress(_playback.Tick) * 10f : 0f;
                body.x += f.Info.Side == Side.Player ? lean : -lean;

                Color c = f.Alive ? color : new Color(0.25f, 0.25f, 0.28f);
                if (_playback.Tick - f.LastHitTick < 4) c = Color.Lerp(c, Color.white, 0.6f);
                Fill(body, c);
                if (f.Stunned) GUI.Label(new Rect(body.x - 20f, body.y - 24f, body.width + 40f, 20f), "<color=#ffd75e>betäubt</color>", _small);

                var hpBack = new Rect(body.x - 20f, body.y - 60f, body.width + 40f, 12f);
                Fill(hpBack, new Color(0.2f, 0.2f, 0.22f));
                float hp = f.Info.MaxHp > 0 ? Mathf.Clamp01(f.Hp / (float)f.Info.MaxHp) : 0f;
                Fill(new Rect(hpBack.x, hpBack.y, hpBack.width * hp, hpBack.height), Color.Lerp(new Color(0.85f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.45f), hp));
                GUI.Label(new Rect(hpBack.x, hpBack.y - 40f, hpBack.width, 40f), $"<b>{f.Info.Name}</b>\n{f.Hp}/{f.Info.MaxHp}", _small);

                if (f.Alive && f.ActionSkill != null && f.ActionWindupTicks > 0)
                {
                    bool charging = f.ActionWindupTicks >= SkillDefinition.ChargeThreshold;
                    var bar = new Rect(hpBack.x, hpBack.yMax + 4f, hpBack.width, 6f);
                    Fill(bar, new Color(0.2f, 0.2f, 0.22f));
                    Fill(new Rect(bar.x, bar.y, bar.width * f.WindupProgress(_playback.Tick), bar.height),
                        charging ? new Color(1f, 0.55f, 0.15f) : new Color(0.7f, 0.7f, 0.75f));
                    string label = charging ? $"<color=#ffae42>lädt auf: {BattleLogText.SkillName(f.ActionSkill)}</color>" : BattleLogText.SkillName(f.ActionSkill);
                    GUI.Label(new Rect(bar.x - 30f, bar.yMax, bar.width + 60f, 20f), label, _small);
                }
            }
        }

        // ------------------------------------------------------------------ Logik-Tafel

        private void DrawBoard(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("<b>Logik-Tafel</b>", _text);
            BattleResult r = _playback.Result;
            for (int i = 0; i < r.PlayerRowLabels.Count; i++)
            {
                bool fallback = i == r.PlayerRowLabels.Count - 1;
                string skill = i < r.PlayerRowSkills.Count ? r.PlayerRowSkills[i] : "?";
                string prefix = fallback ? "↓" : $"{i + 1}.";
                string text = $"{prefix} [{r.PlayerRowLabels[i]}] → {skill}";
                if (_playback.IsRowHighlighted(i)) text = $"<color=#ffd75e><b>{text}</b></color>";
                else if (skill == "—") text = $"<color=#777777>{text}</color>";
                GUILayout.Label(text, _row);
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ Protokoll und Steuerung

        private void DrawLog(Rect area)
        {
            GUILayout.BeginArea(area, GUI.skin.box);
            IReadOnlyList<string> lines = _playback.Lines;
            if (_playback.IsFinished)
            {
                GUILayout.Label("<b>Protokoll</b>", _text);
                _logScroll = GUILayout.BeginScrollView(_logScroll);
                foreach (string line in lines) GUILayout.Label(line, _small);
                GUILayout.EndScrollView();
            }
            else
            {
                int visible = Mathf.Max(1, Mathf.FloorToInt((area.height - 10f) / 20f));
                for (int i = Mathf.Max(0, lines.Count - visible); i < lines.Count; i++) GUILayout.Label(lines[i], _small);
            }
            GUILayout.EndArea();
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
                if (GUILayout.Button("Überspringen", GUILayout.Width(140f), GUILayout.Height(32f))) _playback.SkipToEnd();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
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
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, richText = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            _row.normal.textColor = new Color(0.9f, 0.92f, 0.96f);
        }
    }
}
