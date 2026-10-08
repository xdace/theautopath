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
            UiTheme.Apply();
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
            GUILayout.Label(UiTexts.InventoryFull.ItemTitle, _title);
            GUILayout.Label(UiTexts.InventoryFull.New(ItemText.Details(incoming, _session)), _text);
            GUILayout.Label(UiTexts.InventoryFull.ItemHint, _text);

            _scroll = GUILayout.BeginScrollView(_scroll);
            // Indizes sind Zellen des Item-Rasters; leere Zellen überspringen.
            for (int i = 0; i < _session.Inventory.Capacity; i++)
            {
                EquipmentDefinition item = _session.Inventory[i];
                if (item == null) continue;
                if (GUILayout.Button(UiTexts.InventoryFull.DiscardItem(item.Name, item.Slot.DisplayName(), ItemText.Describe(item)), _button))
                {
                    _session.DiscardItem(i);
                    break;
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(UiTexts.InventoryFull.Reject(incoming.Name), GUILayout.Height(32f))) _session.RejectPendingItem();
        }

        private void DrawRune(StoredRune incoming)
        {
            GUILayout.Label(UiTexts.InventoryFull.RuneTitle, _title);
            GUILayout.Label(UiTexts.InventoryFull.New($"<b>{incoming.Name}</b>\n{incoming.Description}"), _text);
            GUILayout.Label(UiTexts.InventoryFull.RuneHint, _text);

            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<StoredRune> runes = _session.RuneInventory.Runes;
            for (int i = 0; i < runes.Count; i++)
            {
                if (GUILayout.Button(UiTexts.InventoryFull.DiscardRune(runes[i].Name, runes[i].Description), _button))
                {
                    _session.DiscardRune(i);
                    break;
                }
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(UiTexts.InventoryFull.Reject(incoming.Name), GUILayout.Height(32f))) _session.RejectPendingRune();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            _button = new GUIStyle(GUI.skin.button)
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
