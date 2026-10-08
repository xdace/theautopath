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

        /// <param name="keepMessages">Beim Akt-Wechsel bleiben die letzten Meldungen stehen (z. B. «Tafel 4 → 5 Zeilen»).</param>
        public void Initialize(OverworldSession session, bool keepMessages = false)
        {
            Unsubscribe();
            if (!keepMessages) _messages.Clear();
            _session = session;
            _session.BuildImproved += OnImproved;
            _session.EncounterResolved += OnResolved;
            _session.MajorEventResolved += OnMajorResolved;
            _session.MineRaidStarted += OnRaid;
            _session.MineLost += OnMineLost;
        }

        private void OnRaid(MineRaid raid) =>
            Post($"<color=#ff7a6b><b>Goldmine {raid.Coord} angegriffen!</b></color> {OverworldSession.MineRaidTurns} Züge zum Verteidigen");

        private void OnMineLost(MineRaid raid) =>
            Post($"<color=#ff7a6b><b>Goldmine {raid.Coord} verloren.</b></color> Zurückerobern bringt sie wieder");

        /// <summary>Hinweis unten links einblenden, z. B. für andere Systeme.</summary>
        public void Post(string text)
        {
            _messages.Add((text, Time.time + MessageSeconds));
            if (_messages.Count > MaxMessages) _messages.RemoveAt(0);
        }

        private void OnImproved(string text) => Post($"<color=#7ddc6f><b>Verbessert:</b> {text}</color>");

        private void OnResolved(EncounterOutcome outcome) =>
            Post($"<b>{outcome.Definition.Title}</b>: {outcome.Summary}");

        private void OnMajorResolved(MajorEventOutcome outcome) =>
            Post($"<b>{outcome.Title}</b>: {outcome.Summary}");

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
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
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _messageStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
        }

        private void Unsubscribe()
        {
            if (_session == null) return;
            _session.EncounterResolved -= OnResolved;
            _session.BuildImproved -= OnImproved;
            _session.MajorEventResolved -= OnMajorResolved;
            _session.MineRaidStarted -= OnRaid;
            _session.MineLost -= OnMineLost;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
