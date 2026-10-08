using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Shop-Fenster: Runen kaufen, heilen, Runenplatz kaufen, Angebot neu würfeln.</summary>
    public sealed class ShopWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private int _runeAwaitingSlot = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _itemStyle;

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _runeAwaitingSlot = -1;
        }

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            if (Hidden != null && Hidden()) return;
            if (_session == null || _session.PendingShop == null)
            {
                _runeAwaitingSlot = -1;
                return;
            }

            EnsureStyles();

            const float width = 540f;
            const float height = 500f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (_runeAwaitingSlot < 0) DrawStock();
            else DrawReplace();

            GUILayout.EndArea();
        }

        private void DrawStock()
        {
            ShopVisit shop = _session.PendingShop;
            ShopPrices prices = _session.ShopPrices;

            GUILayout.Label("<b>Shop</b>", _titleStyle);
            GUILayout.Label($"Gold: {_session.Stats.Gold}   HP: {_session.Stats.Hp}/{_session.Stats.MaxHp}   Runen: {_session.Runes.Runes.Count}/{_session.Runes.Slots}", _textStyle);
            GUILayout.Space(6f);

            IReadOnlyList<RuneDefinition> runes = shop.Inventory.Runes;
            if (runes.Count == 0) GUILayout.Label("Ausverkauft.", _textStyle);

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinition rune = runes[i];
                GUI.enabled = _session.CanBuyShopRune(i);
                string label = $"<b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]  – {prices.Rune} Gold\n{rune.Description}";
                if (GUILayout.Button(label, _itemStyle, GUILayout.Height(58f)))
                {
                    if (_session.Runes.IsFull) _runeAwaitingSlot = i;
                    else _session.BuyShopRune(i);
                }
            }

            GUILayout.Space(8f);
            GUI.enabled = _session.CanBuyHeal;
            if (GUILayout.Button($"Heilen (+{prices.HealAmount} HP) – {prices.Heal} Gold", GUILayout.Height(30f))) _session.BuyHeal();

            GUI.enabled = _session.CanBuyRuneSlot;
            if (GUILayout.Button($"Zusätzlicher Runenplatz – {prices.Slot} Gold", GUILayout.Height(30f))) _session.BuyRuneSlot();

            GUI.enabled = _session.CanRerollShop;
            if (GUILayout.Button($"Runen neu würfeln – {prices.Reroll} Gold", GUILayout.Height(30f))) _session.RerollShop();

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Shop verlassen", GUILayout.Height(32f))) _session.LeaveShop();
        }

        private void DrawReplace()
        {
            IReadOnlyList<RuneDefinition> stock = _session.PendingShop.Inventory.Runes;
            if (_runeAwaitingSlot >= stock.Count)
            {
                _runeAwaitingSlot = -1;
                return;
            }

            GUILayout.Label($"<b>{stock[_runeAwaitingSlot].Name}</b> ersetzt …", _titleStyle);
            GUILayout.Space(6f);

            IReadOnlyList<RuneDefinition> equipped = _session.Runes.Runes;
            for (int slot = 0; slot < equipped.Count; slot++)
            {
                RuneDefinition rune = equipped[slot];
                if (GUILayout.Button($"<b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]\n{rune.Description}", _itemStyle, GUILayout.Height(52f)))
                {
                    _session.BuyShopRune(_runeAwaitingSlot, slot);
                    _runeAwaitingSlot = -1;
                    return;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Zurück", GUILayout.Height(30f))) _runeAwaitingSlot = -1;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _itemStyle = new GUIStyle(GUI.skin.button)
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
