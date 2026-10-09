using System.Collections.Generic;
using Betaknight.Core;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Meldungen als Toasts oben rechts unter der Statusleiste und das Journal (Taste J). Quelle ist das
    /// <see cref="RunJournal"/> der Session: Es überlebt den Aktwechsel. Solange die Arena läuft, werden Toasts gepuffert
    /// (ihre Zeit läuft erst, wenn sie zu sehen sind). Maus darüber hält die Zeit an, Klick schliesst; Gefahren bleiben stehen.
    /// </summary>
    public sealed class ToastLayer : MonoBehaviour
    {
        private const int MaxVisible = 4;
        private const float Width = 400f;

        private sealed class Toast
        {
            public JournalEntry Entry;
            public float Left;
            public Rect Rect;
        }

        private OverworldSession _session;
        private RunJournal _journal;
        private readonly Queue<JournalEntry> _pending = new Queue<JournalEntry>();
        private readonly List<Toast> _visible = new List<Toast>();
        private int _hovered = -1;
        private Vector2 _scroll;
        private GUIStyle _text;
        private GUIStyle _header;

        private static bool _journalOpen;
        private static readonly List<Rect> Blocking = new List<Rect>();

        /// <summary>Solange true, bleiben Toasts verborgen und gepuffert (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        public static bool JournalOpen => _journalOpen;

        private static Rect JournalRect => new Rect(Screen.width - 492f, OverworldHud.TopBarHeight + 8f, 480f, Screen.height - OverworldHud.TopBarHeight - 20f);

        /// <summary>GUI-Punkt über einem Toast oder dem Journal? Dann ignoriert die Karte den Klick.</summary>
        public static bool ContainsGuiPoint(Vector2 point)
        {
            if (_journalOpen && JournalRect.Contains(point)) return true;
            foreach (Rect r in Blocking)
                if (r.Contains(point)) return true;
            return false;
        }

        /// <param name="keep">Beim Aktwechsel bleiben offene und gepufferte Toasts stehen.</param>
        public void Initialize(OverworldSession session, bool keep = false)
        {
            if (_journal != null) _journal.Added -= OnAdded;
            if (!keep)
            {
                _pending.Clear();
                _visible.Clear();
                _journalOpen = false;
            }
            _session = session;
            _journal = session?.Journal;
            if (_journal != null) _journal.Added += OnAdded;
        }

        private void OnDestroy()
        {
            if (_journal != null) _journal.Added -= OnAdded;
        }

        private void OnAdded(JournalEntry entry) => _pending.Enqueue(entry);

        public void ToggleJournal() => _journalOpen = !_journalOpen;

        private bool IsHidden => _session == null || (Hidden != null && Hidden());

        private static float Seconds(JournalEntry e) => Mathf.Clamp(3f + (e.Title.Length + e.Text.Length) / 40f, 3f, 7f);

        private void Update()
        {
            if (IsHidden) return;
            while (_visible.Count < MaxVisible && _pending.Count > 0)
            {
                JournalEntry e = _pending.Dequeue();
                _visible.Add(new Toast { Entry = e, Left = Seconds(e) });
            }
            for (int i = 0; i < _visible.Count; i++)
                if (i != _hovered && !_visible[i].Entry.Sticky) _visible[i].Left -= Time.unscaledDeltaTime;
            _visible.RemoveAll(t => t.Left <= 0f);
        }

        public static Color ColorOf(JournalKind kind)
        {
            switch (kind)
            {
                case JournalKind.Danger: return UiTheme.Bad;
                case JournalKind.Reward: return UiTheme.Accent;
                case JournalKind.Good: return UiTheme.Good;
                default: return UiTheme.MutedColor;
            }
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            Blocking.Clear();
            if (IsHidden) return;
            if (_text == null)
            {
                _text = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
                _header = new GUIStyle(_text) { fontSize = 13 };
            }
            GUI.depth = -7;

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.J)
            {
                ToggleJournal();
                e.Use();
            }
            else if (_journalOpen && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _journalOpen = false;
                e.Use();
            }

            if (_journalOpen) DrawJournal();
            else DrawToasts();
        }

        private static string Line(JournalEntry entry)
        {
            string title = $"<b><color={UiTheme.Hex(ColorOf(entry.Kind))}>{entry.Title}</color></b>";
            return entry.Text.Length > 0 ? $"{title}\n{entry.Text}" : title;
        }

        private void DrawToasts()
        {
            float x = Screen.width - Width - 12f;
            float y = OverworldHud.TopBarHeight + 8f;
            int hovered = -1;
            int close = -1;
            for (int i = 0; i < _visible.Count; i++)
            {
                Toast t = _visible[i];
                string text = Line(t.Entry);
                if (t.Entry.Sticky) text += $"\n<size=11><color=#9aa4b2>{UiTexts.Journal.ClickToClose}</color></size>";
                float h = _text.CalcHeight(new GUIContent(text), Width - 22f) + 12f;
                var rect = new Rect(x, y, Width, h);
                t.Rect = rect;
                Blocking.Add(rect);
                bool over = rect.Contains(Event.current.mousePosition);
                if (over) hovered = i;
                UiTheme.Fill(rect, new Color(0.06f, 0.07f, 0.09f, over ? 0.98f : 0.92f));
                UiTheme.Fill(new Rect(rect.x, rect.y, 4f, rect.height), ColorOf(t.Entry.Kind));
                if (!t.Entry.Sticky)
                {
                    float fill = Mathf.Clamp01(t.Left / Seconds(t.Entry));
                    UiTheme.Fill(new Rect(rect.x + 4f, rect.yMax - 2f, (rect.width - 4f) * fill, 2f), new Color(1f, 1f, 1f, 0.15f));
                }
                GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 22f, rect.height - 12f), text, _text);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) close = i;
                y += h + 6f;
            }
            if (Event.current.type == EventType.Repaint) _hovered = hovered;
            if (close >= 0) _visible.RemoveAt(close);
            if (_pending.Count > 0)
                GUI.Label(new Rect(x, y, Width, 20f), $"<size=12><color=#9aa4b2>{UiTexts.Journal.More(_pending.Count)}</color></size>", _header);
        }

        private void DrawJournal()
        {
            Rect rect = JournalRect;
            UiTheme.Fill(rect, new Color(0.05f, 0.06f, 0.08f, 0.97f));
            UiTheme.Outline(rect, UiTheme.BorderColor, 1f);
            GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f));
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{UiTexts.Journal.Title}</b>  <size=12><color=#9aa4b2>{UiTexts.Journal.Hint}</color></size>", _text);
            if (GUILayout.Button(UiTexts.Journal.Close, GUILayout.Width(90f))) _journalOpen = false;
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<JournalEntry> entries = _journal.Entries;
            if (entries.Count == 0) GUILayout.Label($"<color=#9aa4b2>{UiTexts.Journal.Empty}</color>", _text);
            int act = -1, turn = -1;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                JournalEntry entry = entries[i];
                if (entry.Act != act || entry.Turn != turn)
                {
                    act = entry.Act;
                    turn = entry.Turn;
                    GUILayout.Space(4f);
                    GUILayout.Label($"<color=#9aa4b2>{UiTexts.Journal.Turn(act, turn)}</color>", _header);
                }
                GUILayout.Label(Line(entry), _text);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
