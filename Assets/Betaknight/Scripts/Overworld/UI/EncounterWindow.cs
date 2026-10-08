using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Encounters;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Zeigt mittlere Events als Fenster mit Optionen und meldet Ergebnisse aller Events als kurze Hinweise unten links.
    /// Platzhalter per IMGUI, bis das echte UI kommt. Spielregeln kennt es nicht, es ruft nur die Session auf.
    /// </summary>
    public sealed class EncounterWindow : MonoBehaviour
    {
        private const float MessageSeconds = 4f;
        private const int MaxMessages = 5;

        private readonly List<(string text, float until)> _messages = new List<(string, float)>();

        private OverworldSession _session;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _messageStyle;

        public void Initialize(OverworldSession session)
        {
            Unsubscribe();
            _messages.Clear();
            _session = session;
            _session.EncounterResolved += OnResolved;
            _session.MajorEventResolved += OnMajorResolved;
        }

        /// <summary>Hinweis unten links einblenden, z. B. für andere Systeme.</summary>
        public void Post(string text)
        {
            _messages.Add((text, Time.time + MessageSeconds));
            if (_messages.Count > MaxMessages) _messages.RemoveAt(0);
        }

        private void OnResolved(EncounterOutcome outcome) =>
            Post($"<b>{outcome.Definition.Title}</b>: {outcome.Summary}");

        private void OnMajorResolved(MajorEventOutcome outcome) =>
            Post($"<b>{outcome.Title}</b>: {outcome.Summary}");

        private void OnGUI()
        {
            if (_session == null) return;
            EnsureStyles();

            DrawMessages();

            EncounterPrompt prompt = _session.PendingEncounter;
            if (prompt != null) DrawPrompt(prompt);
        }

        private void DrawPrompt(EncounterPrompt prompt)
        {
            const float width = 420f;
            const float height = 240f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"<b>{prompt.Definition.Title}</b>", _titleStyle);
            GUILayout.Label(prompt.Definition.Text, _textStyle);
            GUILayout.FlexibleSpace();

            IReadOnlyList<EncounterOption> options = prompt.Definition.Options;
            for (int i = 0; i < options.Count; i++)
            {
                bool available = options[i].IsAvailable(_session.Stats);
                GUI.enabled = available;
                if (GUILayout.Button(options[i].Text, GUILayout.Height(34f)))
                {
                    _session.ChooseEncounterOption(i);
                }
            }
            GUI.enabled = true;
            GUILayout.EndArea();
        }

        private void DrawMessages()
        {
            _messages.RemoveAll(m => m.until < Time.time);
            float y = Screen.height - 12f - _messages.Count * 28f;
            foreach ((string text, float _) in _messages)
            {
                GUI.Label(new Rect(12f, y, 600f, 28f), text, _messageStyle);
                y += 28f;
            }
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            _messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        }

        private void Unsubscribe()
        {
            if (_session == null) return;
            _session.EncounterResolved -= OnResolved;
            _session.MajorEventResolved -= OnMajorResolved;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
