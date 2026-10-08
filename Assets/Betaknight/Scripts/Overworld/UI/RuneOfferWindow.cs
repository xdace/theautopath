using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Fenster für eine Runenwahl: eine von drei Runen nehmen oder verzichten.
    /// Sind alle Plätze belegt, wählt der Spieler danach, welche Rune ersetzt wird.
    /// </summary>
    public sealed class RuneOfferWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private RuneOffer _offer;
        private int _choiceAwaitingSlot = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _textStyle;

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _offer = null;
            _choiceAwaitingSlot = -1;
        }

        private void OnGUI()
        {
            if (_session == null) return;

            RuneOffer offer = _session.PendingRuneOffer;
            if (offer == null) return;
            if (offer != _offer)
            {
                _offer = offer;
                _choiceAwaitingSlot = -1;
            }

            EnsureStyles();

            const float width = 520f;
            const float height = 420f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (_choiceAwaitingSlot < 0) DrawOptions(offer);
            else DrawReplace(offer.Options[_choiceAwaitingSlot]);

            GUILayout.EndArea();
        }

        private void DrawOptions(RuneOffer offer)
        {
            GUILayout.Label($"<b>Runenwahl</b> – {offer.Source}", _titleStyle);
            GUILayout.Label($"Plätze: {_session.Runes.Runes.Count}/{_session.Runes.Slots}", _textStyle);
            GUILayout.Space(6f);

            IReadOnlyList<RuneDefinition> options = offer.Options;
            for (int i = 0; i < options.Count; i++)
            {
                RuneDefinition rune = options[i];
                bool synergy = _session.Runes.HasTag(rune.Tag);
                string label = $"<b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]{(synergy ? "  ★" : string.Empty)}\n{rune.Description}";
                if (GUILayout.Button(label, _nameStyle, GUILayout.Height(64f)))
                {
                    if (_session.Runes.IsFull) _choiceAwaitingSlot = i;
                    else _session.TakeRune(i);
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Verzichten (+{OverworldSession.SkipRuneGold} Gold)", GUILayout.Height(30f)))
            {
                _session.SkipRuneOffer();
            }
        }

        private void DrawReplace(RuneDefinition incoming)
        {
            GUILayout.Label($"<b>{incoming.Name}</b> ersetzt …", _titleStyle);
            GUILayout.Space(6f);

            IReadOnlyList<RuneDefinition> equipped = _session.Runes.Runes;
            for (int slot = 0; slot < equipped.Count; slot++)
            {
                RuneDefinition rune = equipped[slot];
                if (GUILayout.Button($"<b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]\n{rune.Description}", _nameStyle, GUILayout.Height(56f)))
                {
                    _session.TakeRune(_choiceAwaitingSlot, slot);
                    _choiceAwaitingSlot = -1;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Zurück", GUILayout.Height(30f))) _choiceAwaitingSlot = -1;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _nameStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };
        }
    }
}
