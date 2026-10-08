using System;
using System.Collections.Generic;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Startbildschirm eines Runs: der Spieler wählt sein Ritter-Kit.</summary>
    public sealed class KitSelectionWindow : MonoBehaviour
    {
        private IReadOnlyList<KnightKit> _kits;
        private RuneCatalog _runes;
        private Action<KnightKit> _onChosen;
        private GUIStyle _titleStyle;
        private GUIStyle _kitStyle;

        public bool IsOpen => _kits != null;

        /// <summary>Die angebotenen Kits, leer bei geschlossenem Fenster (für den Testspieler).</summary>
        public IReadOnlyList<KnightKit> Kits => _kits ?? Array.Empty<KnightKit>();

        /// <summary>Wählt ein Kit wie ein Klick auf seinen Knopf.</summary>
        public bool Choose(KnightKit kit)
        {
            if (_kits == null || kit == null) return false;
            Action<KnightKit> callback = _onChosen;
            _kits = null;
            _onChosen = null;
            callback?.Invoke(kit);
            return true;
        }

        public void Open(IReadOnlyList<KnightKit> kits, RuneCatalog runes, Action<KnightKit> onChosen)
        {
            _kits = kits;
            _runes = runes;
            _onChosen = onChosen;
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            if (_kits == null) return;
            EnsureStyles();

            const float width = 560f;
            float height = 110f + _kits.Count * 96f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("<b>Wähle deinen Ritter</b>", _titleStyle);
            GUILayout.Space(8f);

            foreach (KnightKit kit in _kits)
            {
                string rune = _runes != null && _runes.TryGet(kit.StartRuneId, out RuneDefinition r)
                    ? $"{r.Name}: {r.Description}"
                    : "keine";
                string label = $"<b>{kit.Name}</b>  [{kit.Tag.DisplayName()}]   {kit.MaxHp} HP, {kit.Gold} Gold\n{kit.Description}\nStart-Rune {rune}";

                if (GUILayout.Button(label, _kitStyle, GUILayout.Height(88f)))
                {
                    Choose(kit);
                    break;
                }
            }

            GUILayout.EndArea();
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, alignment = TextAnchor.MiddleCenter };
            _kitStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 6, 6),
            };
        }
    }
}
