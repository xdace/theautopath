using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Erscheint, wenn ein Teil oder eine Rune nicht mehr ins volle Inventar passt:
    /// ein vorhandenes verwerfen (das neue rückt nach) oder das neue ablehnen.
    /// </summary>
    public sealed class InventoryFullWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _button;
        private Vector2 _scroll;

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        public void Initialize(OverworldSession session) => _session = session;

        private void OnGUI()
        {
            if (Hidden != null && Hidden()) return;
            if (_session == null || (_session.PendingItem == null && _session.PendingRune == null)) return;
            EnsureStyles();
            GUI.depth = -6;

            const float width = 520f;
            float height = Mathf.Min(Screen.height - 40f, 560f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (_session.PendingItem != null) DrawItem(_session.PendingItem);
            else DrawRune(_session.PendingRune);

            GUILayout.EndArea();
        }

        private void DrawItem(EquipmentDefinition incoming)
        {
            GUILayout.Label("<b>Inventar voll</b>", _title);
            GUILayout.Label($"Neu: {ItemText.Details(incoming, _session.Sets)}", _text);
            GUILayout.Label("Ein Teil verwerfen, damit das neue hineinpasst:", _text);

            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<EquipmentDefinition> items = _session.Inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (GUILayout.Button($"<b>{items[i].Name}</b> [{items[i].Slot.DisplayName()}] verwerfen\n<size=12>{ItemText.Describe(items[i])}</size>", _button))
                {
                    _session.DiscardItem(i);
                    break;
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button($"{incoming.Name} ablehnen", GUILayout.Height(32f))) _session.RejectPendingItem();
        }

        private void DrawRune(StoredRune incoming)
        {
            GUILayout.Label("<b>Runen-Inventar voll</b>", _title);
            GUILayout.Label($"Neu: <b>{incoming.Name}</b>\n{incoming.Description}", _text);
            GUILayout.Label("Eine Rune verwerfen, damit die neue hineinpasst:", _text);

            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<StoredRune> runes = _session.RuneInventory.Runes;
            for (int i = 0; i < runes.Count; i++)
            {
                if (GUILayout.Button($"<b>{runes[i].Name}</b> verwerfen\n<size=12>{runes[i].Description}</size>", _button))
                {
                    _session.DiscardRune(i);
                    break;
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button($"{incoming.Name} ablehnen", GUILayout.Height(32f))) _session.RejectPendingRune();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };
        }
    }
}
