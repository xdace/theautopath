using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Encounters;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Zeigt mittlere Events als Fenster mit Optionen. Ergebnisse landen im Journal der Session und erscheinen als Toasts
    /// (<see cref="ToastLayer"/>). Spielregeln kennt es nicht, es ruft nur die Session auf.
    /// </summary>
    public sealed class EncounterWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;

        public void Initialize(OverworldSession session) => _session = session;

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
            if (_session == null) return;
            EnsureStyles();

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

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        }
    }
}
