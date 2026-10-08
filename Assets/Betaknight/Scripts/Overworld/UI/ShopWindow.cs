using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Shop-Fenster: Runen und Teile kaufen (anlegen oder ins Inventar), Inventar verkaufen, heilen, Runenplatz, neu würfeln.</summary>
    public sealed class ShopWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private int _runeAwaitingSlot = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _plainStyle;
        private Vector2 _scroll;

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _runeAwaitingSlot = -1;
        }

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
            if (_session == null || _session.PendingShop == null)
            {
                _runeAwaitingSlot = -1;
                return;
            }

            EnsureStyles();

            const float width = 540f;
            float height = Mathf.Min(Screen.height - 40f, 720f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (_runeAwaitingSlot < 0) DrawStock();
            else DrawReplace();

            GUILayout.EndArea();
            UiTheme.DrawTooltip();
        }

        private void DrawStock()
        {
            ShopVisit shop = _session.PendingShop;
            ShopPrices prices = _session.ShopPrices;

            GUILayout.Label("<b>Shop</b>", _titleStyle);
            GUILayout.Label($"Gold: {_session.Stats.Gold}   HP: {_session.Stats.Hp}/{_session.Stats.MaxHp}   Runen: {_session.Runes.Runes.Count}/{_session.Runes.Slots}", _textStyle);
            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll);

            IReadOnlyList<RuneDefinition> runes = shop.Inventory.Runes;
            if (runes.Count == 0) GUILayout.Label("Ausverkauft.", _textStyle);

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinition rune = runes[i];
                GUI.enabled = _session.CanBuyShopRune(i);
                string hints = SkillText.EvolutionHints(_session.EvolutionHintsForRune(rune.Id));
                string label = $"{RuneText.DifficultyBadge(rune)}  <b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]  – {prices.Rune} Gold\n{rune.Description}{hints}";
                if (GUILayout.Button(new GUIContent(label, RuneText.DifficultyTip(rune)), _itemStyle, GUILayout.Height(58f + 18f * (hints.Split('\n').Length - 1))))
                {
                    if (_session.Runes.IsFull) _runeAwaitingSlot = i;
                    else _session.BuyShopRune(i);
                }
            }

            IReadOnlyList<string> items = shop.Inventory.ItemIds;
            for (int i = 0; i < items.Count; i++)
            {
                if (!_session.Items.TryGet(items[i], out EquipmentDefinition item)) continue;
                EquipmentDefinition worn = _session.Gear.Get(item.Slot);
                string set = item.SetId != null ? $"  Set: {_session.Sets.NameOf(item.SetId)}" : string.Empty;
                string setBlock = ItemText.SetBlock(_session, item);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{item.Name}</b>  [{item.Slot.DisplayName()}]  – {prices.Item} Gold{set}\n{ItemText.Describe(item)}{RuneText.Eases(_session, item.Id)}\n<size=13>{ItemText.Compare(item, worn)}  {ItemText.TagPreview(_session, item)}{SkillText.EvolutionHints(_session.EvolutionHintsForItem(item))}</size>{(setBlock.Length > 0 ? $"\n<size=13>{setBlock}</size>" : string.Empty)}", _plainStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = _session.CanBuyShopItem(i, ItemPlacement.Equip);
                if (GUILayout.Button(worn != null ? $"Kaufen und anlegen ({worn.Name} ins Inventar)" : "Kaufen und anlegen", GUILayout.Height(28f)))
                    _session.BuyShopItem(i, ItemPlacement.Equip);
                GUI.enabled = _session.CanBuyShopItem(i, ItemPlacement.Inventory);
                if (GUILayout.Button("Kaufen, ins Inventar", GUILayout.Height(28f))) _session.BuyShopItem(i, ItemPlacement.Inventory);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            IReadOnlyList<string> skills = shop.Inventory.SkillIds;
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SkillText.Describe(_session, skills[i], $"  – {prices.Skill} Gold"), _plainStyle);
                SkillText.DrawChoice(_session, skills[i], _session.CanBuyShopSkill(i), "Skill kaufen", choice => _session.BuyShopSkill(index, choice));
                GUILayout.EndVertical();
            }

            IReadOnlyList<string> modules = shop.Inventory.ModuleIds;
            for (int i = 0; i < modules.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(ModuleText.Describe(_session, modules[i], $"  – {prices.Module} Gold"), _plainStyle);
                ModuleText.DrawChoice(_session, modules[i], _session.CanBuyShopModule(i), "Modul kaufen", choice => _session.BuyShopModule(index, choice));
                GUILayout.EndVertical();
            }

            DrawSell(prices);

            GUILayout.Space(8f);
            GUI.enabled = _session.CanBuyHeal;
            if (GUILayout.Button($"Heilen (+{prices.HealAmount} HP) – {prices.Heal} Gold", GUILayout.Height(30f))) _session.BuyHeal();

            GUI.enabled = _session.CanBuyRuneSlot;
            string slot = _session.CanExpandBoard ? $"Zusätzlicher Runenplatz – {_session.RuneSlotPrice} Gold"
                : $"Tafel voll ({_session.Progression.MaxBoardRows} Zeilen)";
            if (GUILayout.Button(slot, GUILayout.Height(30f))) _session.BuyRuneSlot();

            GUI.enabled = _session.CanRerollShop;
            if (GUILayout.Button($"Angebot neu würfeln – {prices.Reroll} Gold", GUILayout.Height(30f))) _session.RerollShop();

            GUI.enabled = true;
            GUILayout.EndScrollView();
            if (GUILayout.Button("Shop verlassen", GUILayout.Height(32f))) _session.LeaveShop();
        }

        /// <summary>Verkauf aus dem Inventar für den halben Preis. Angelegtes muss erst abgelegt werden.</summary>
        private void DrawSell(ShopPrices prices)
        {
            if (_session.Inventory.Count == 0 && _session.RuneInventory.Count == 0) return;

            GUILayout.Space(8f);
            GUILayout.Label($"<b>Verkaufen</b> (halber Preis: Teil {prices.SellItem} Gold, Rune {prices.SellRune} Gold)", _plainStyle);
            // Indizes sind Zellen des Item-Rasters; leere Zellen überspringen.
            for (int i = 0; i < _session.Inventory.Capacity; i++)
            {
                EquipmentDefinition item = _session.Inventory[i];
                if (item == null) continue;
                if (GUILayout.Button($"{item.Name} [{item.Slot.DisplayName()}] verkaufen  +{prices.SellItem} Gold", GUILayout.Height(26f)))
                {
                    _session.SellItem(i);
                    return;
                }
            }
            IReadOnlyList<StoredRune> runes = _session.RuneInventory.Runes;
            for (int i = 0; i < runes.Count; i++)
            {
                if (GUILayout.Button($"Rune {runes[i].Name} verkaufen  +{prices.SellRune} Gold", GUILayout.Height(26f)))
                {
                    _session.SellRune(i);
                    return;
                }
            }
        }

        private void DrawReplace()
        {
            IReadOnlyList<RuneDefinition> stock = _session.PendingShop.Inventory.Runes;
            if (_runeAwaitingSlot >= stock.Count)
            {
                _runeAwaitingSlot = -1;
                return;
            }

            GUILayout.Label($"<b>{stock[_runeAwaitingSlot].Name}</b>: Tafel ist voll", _titleStyle);
            GUILayout.Space(6f);

            if (GUILayout.Button("Kaufen, ins Runen-Inventar", GUILayout.Height(32f)))
            {
                _session.BuyShopRune(_runeAwaitingSlot);
                _runeAwaitingSlot = -1;
                return;
            }
            GUILayout.Label("… oder eine Zeile tauschen (die alte Rune wandert mit ihrer Stufe ins Inventar, der Skill bleibt):", _plainStyle);

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
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _plainStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
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
