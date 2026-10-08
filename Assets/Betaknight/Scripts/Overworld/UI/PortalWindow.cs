using Betaknight.Core;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Erscheint nach dem Boss, sobald nichts anderes mehr wartet: Durchs Portal in den nächsten Akt.</summary>
    public sealed class PortalWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        public void Initialize(OverworldSession session) => _session = session;

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
            if (_session == null || !_session.CanEnterPortal) return;

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter };
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, alignment = TextAnchor.MiddleCenter };
            }

            const float width = 420f;
            const float height = 210f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>Das Fluchtportal ist offen</b>", _titleStyle);
            GUILayout.Label($"Es führt in Akt {_session.Act + 1}: eine neue Karte mit stärkeren Gegnern. "
                + "Ritter, Ausrüstung, Tafel, Gold und Splitter kommen mit, eroberte Minen bleiben zurück.", _textStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Durchs Portal → Akt {_session.Act + 1}", GUILayout.Height(36f))) _session.EnterPortal();
            GUILayout.EndArea();
        }
    }
}
