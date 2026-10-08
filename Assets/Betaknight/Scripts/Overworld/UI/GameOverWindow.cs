using System;
using Betaknight.Core;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Wird angezeigt, wenn der Ritter fällt. Bietet einen neuen Run an.</summary>
    public sealed class GameOverWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private Action _onNewRun;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;

        public void Initialize(OverworldSession session, Action onNewRun)
        {
            _session = session;
            _onNewRun = onNewRun;
        }

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
            if (_session == null || !_session.IsGameOver) return;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter };
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            }

            const float width = 380f;
            const float height = 200f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label(UiTexts.GameOver.Title, _titleStyle);
            GUILayout.Label(UiTexts.GameOver.Summary(_session.Act, _session.Turns.CurrentTurn, _session.Runes.Runes.Count, _session.Stats.Gold), _textStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(UiTexts.GameOver.NewRun, GUILayout.Height(36f))) _onNewRun?.Invoke();
            GUILayout.EndArea();
        }
    }
}
