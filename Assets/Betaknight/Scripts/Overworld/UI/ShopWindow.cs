using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Core.Shop;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Shop-Fenster: Runen und Teile kaufen (anlegen oder ins Inventar), Logik-Chips (A-20, ins Chip-Inventar), Inventar
    /// verkaufen, heilen, Platinen-Erweiterung, neu würfeln.
    /// A-21: Reroll mit steigendem Preis je Besuch; jedes Angebot hat einen Lock-Knopf (gesperrt bleibt es beim Reroll und im
    /// nächsten Shop), gesperrte Angebote sind gold markiert, der Knopf ist aus, wenn alle Lock-Plätze belegt sind.
    /// </summary>
    public sealed class ShopWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private int _runeAwaitingSlot = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _itemStyle;
        private GUIStyle _plainStyle;
        private GUIStyle _lockStyle;
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

            GUILayout.Label(UiTexts.Shop.Title, _titleStyle);
            GUILayout.Label(UiTexts.Shop.Status(_session.Stats.Gold, _session.Stats.Hp, _session.Stats.MaxHp, _session.BoardSize, _session.Board.Relays.Count), _textStyle);
            GUILayout.Label(new GUIContent($"<color=#ffd75e>{UiTexts.Shop.Locks(_session.LockedOffers.Count, prices.LockSlots)}</color>", UiTexts.Shop.LockTip(prices.LockSlots)), _plainStyle);
            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll);

            IReadOnlyList<RuneDefinition> runes = shop.Inventory.Runes;
            if (runes.Count == 0) GUILayout.Label(UiTexts.Shop.SoldOut, _textStyle);

            for (int i = 0; i < runes.Count; i++)
            {
                RuneDefinition rune = runes[i];
                string hints = SkillText.EvolutionHints(_session.EvolutionHintsForRune(rune.Id));
                string label = UiTexts.Shop.RuneLabel(RuneText.DifficultyBadge(rune), rune.Name, rune.Tag.DisplayName(), prices.Rune, rune.Description, hints)
                    + LockedMark(ShopOfferKind.Rune, i);
                float height = 58f + 18f * (hints.Split('\n').Length - 1);
                GUILayout.BeginHorizontal();
                GUI.enabled = _session.CanBuyShopRune(i);
                bool buy = GUILayout.Button(new GUIContent(label, RuneText.DifficultyTip(rune)), _itemStyle, GUILayout.Height(height));
                GUI.enabled = true;
                DrawLock(ShopOfferKind.Rune, i, prices, height);
                GUILayout.EndHorizontal();
                if (buy)
                {
                    if (_session.Board.IsFull) _runeAwaitingSlot = i;
                    else _session.BuyShopRune(i);
                }
            }

            IReadOnlyList<string> items = shop.Inventory.ItemIds;
            for (int i = 0; i < items.Count; i++)
            {
                if (!_session.Items.TryGet(items[i], out EquipmentDefinition item)) continue;
                EquipmentDefinition worn = _session.Gear.Get(item.Slot);
                string set = item.SetId != null ? $"  Set: {_session.Sets.NameOf(item.SetId)}" : string.Empty;
                string setBlock = ItemText.SynergyBlock(_session, item);
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{UiTexts.Shop.ItemHead(item.Name, item.Slot.DisplayName(), prices.Item, set)}{LockedMark(ShopOfferKind.Item, i)}\n{ItemText.Describe(item)}{RuneText.Eases(_session, item.Id)}\n<size=13>{ItemText.Compare(item, worn)}  {ItemText.TagPreview(_session, item)}{SkillText.EvolutionHints(_session.EvolutionHintsForItem(item))}</size>{(setBlock.Length > 0 ? $"\n<size=13>{setBlock}</size>" : string.Empty)}", _plainStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = _session.CanBuyShopItem(i, ItemPlacement.Equip);
                if (GUILayout.Button(worn != null ? UiTexts.Shop.BuyAndEquipSwap(worn.Name) : UiTexts.Shop.BuyAndEquip, GUILayout.Height(28f)))
                    _session.BuyShopItem(i, ItemPlacement.Equip);
                GUI.enabled = _session.CanBuyShopItem(i, ItemPlacement.Inventory);
                if (GUILayout.Button(UiTexts.Shop.BuyToInventory, GUILayout.Height(28f))) _session.BuyShopItem(i, ItemPlacement.Inventory);
                GUI.enabled = true;
                DrawLock(ShopOfferKind.Item, i, prices, 28f);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            IReadOnlyList<string> skills = shop.Inventory.SkillIds;
            for (int i = 0; i < skills.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SkillText.Describe(_session, skills[i], UiTexts.PriceTag(prices.Skill) + LockedMark(ShopOfferKind.Skill, i)), _plainStyle);
                GUILayout.BeginHorizontal();
                SkillText.DrawChoice(_session, skills[i], _session.CanBuyShopSkill(i), UiTexts.Shop.BuySkill, choice => _session.BuyShopSkill(index, choice));
                DrawLock(ShopOfferKind.Skill, i, prices, 28f);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            IReadOnlyList<string> modules = shop.Inventory.ModuleIds;
            for (int i = 0; i < modules.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(ModuleText.Describe(_session, modules[i], UiTexts.PriceTag(prices.Module) + LockedMark(ShopOfferKind.Module, i)), _plainStyle);
                GUILayout.BeginHorizontal();
                ModuleText.DrawChoice(_session, modules[i], _session.CanBuyShopModule(i), UiTexts.Shop.BuyModule, choice => _session.BuyShopModule(index, choice));
                DrawLock(ShopOfferKind.Module, i, prices, 28f);
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            DrawChips(shop, prices);
            DrawSell(prices);

            GUILayout.Space(8f);
            GUI.enabled = _session.CanBuyHeal;
            if (GUILayout.Button(UiTexts.Shop.Heal(prices.HealAmount, prices.Heal), GUILayout.Height(30f))) _session.BuyHeal();

            GUI.enabled = _session.CanBuyBoardExpansion;
            string expansion = !_session.CanExpandBoard ? UiTexts.Shop.BoardMaxed(_session.MaxBoardSize)
                : shop.Inventory.SlotSold ? UiTexts.Shop.BoardExpansionSold
                : UiTexts.Shop.BoardExpansion(_session.BoardSize, NextBoardSize(), _session.BoardExpansionPrice);
            if (GUILayout.Button(expansion, GUILayout.Height(30f))) _session.BuyBoardExpansion();

            // A-21: Preis steigt mit jedem Reroll dieses Besuchs.
            GUI.enabled = _session.CanRerollShop;
            int reroll = _session.ShopRerollPrice;
            if (GUILayout.Button(new GUIContent(UiTexts.Shop.Reroll(reroll), UiTexts.Shop.RerollTip(reroll + prices.RerollStep)), GUILayout.Height(30f)))
                _session.RerollShop();

            GUI.enabled = true;
            GUILayout.EndScrollView();
            if (GUILayout.Button(UiTexts.Shop.Leave, GUILayout.Height(32f))) _session.LeaveShop();
        }

        /// <summary>Logik-Chips im Angebot (A-20): Knopf mit Name, Preis und Text; gekauft landet der Chip im Chip-Inventar.</summary>
        private void DrawChips(ShopVisit shop, ShopPrices prices)
        {
            IReadOnlyList<string> chips = shop.Inventory.ChipIds;
            for (int i = 0; i < chips.Count; i++)
            {
                if (!_session.ChipCatalog.TryGet(chips[i], out ChipDefinition chip)) continue;
                // A-21: Effekt-Chips mit Symbol und Farbe ihres Effekts.
                string label = UiTexts.Shop.ChipLabel(chip.Name, prices.Chip, chip.Description);
                if (EffectText.TryGet(chip.EffectId, out CircuitEffectDefinition effect))
                    label = $"{EffectText.Icon(effect)} {label}";
                label += LockedMark(ShopOfferKind.Chip, i);
                GUILayout.BeginHorizontal();
                GUI.enabled = _session.CanBuyShopChip(i);
                bool bought = GUILayout.Button(new GUIContent(label, UiTexts.Shop.ChipTip), _itemStyle, GUILayout.Height(58f));
                GUI.enabled = true;
                DrawLock(ShopOfferKind.Chip, i, prices, 58f);
                GUILayout.EndHorizontal();
                if (bought)
                {
                    _session.BuyShopChip(i);
                    return;
                }
            }
            GUI.enabled = true;
        }

        /// <summary>
        /// Lock-Knopf eines Angebots (A-21): «Lock» bzw. gold «■ Locked»; aus, wenn alle Lock-Plätze belegt sind (CanLock).
        /// </summary>
        private void DrawLock(ShopOfferKind kind, int index, ShopPrices prices, float height)
        {
            bool locked = _session.IsLockedAt(kind, index);
            bool can = _session.CanLock(kind, index);
            string tip = locked ? UiTexts.Shop.UnlockTip : can ? UiTexts.Shop.LockTip(prices.LockSlots) : UiTexts.Shop.LockFullTip(prices.LockSlots);
            bool old = GUI.enabled;
            GUI.enabled = can;
            if (GUILayout.Button(new GUIContent(locked ? UiTexts.Shop.Locked : UiTexts.Shop.Lock, tip), _lockStyle, GUILayout.Width(84f), GUILayout.Height(height)))
                _session.ToggleLock(kind, index);
            GUI.enabled = old;
        }

        /// <summary>Goldene Markierung «■ locked» hinter einem gesperrten Angebot, sonst leer.</summary>
        private string LockedMark(ShopOfferKind kind, int index) => _session.IsLockedAt(kind, index) ? UiTexts.Shop.LockedMark : string.Empty;

        /// <summary>«4×4»: Grösse nach der nächsten Erweiterung.</summary>
        private string NextBoardSize() => _session.Board.Config.SizeAt(_session.Board.Step + 1).ToString();

        /// <summary>Verkauf aus dem Inventar für den halben Preis. Angelegtes muss erst abgelegt werden.</summary>
        private void DrawSell(ShopPrices prices)
        {
            if (_session.Inventory.Count == 0 && _session.RuneInventory.Count == 0) return;

            GUILayout.Space(8f);
            GUILayout.Label(UiTexts.Shop.SellTitle(prices.SellItem, prices.SellRune), _plainStyle);
            // Indizes sind Zellen des Item-Rasters; leere Zellen überspringen.
            for (int i = 0; i < _session.Inventory.Capacity; i++)
            {
                EquipmentDefinition item = _session.Inventory[i];
                if (item == null) continue;
                if (GUILayout.Button(UiTexts.Shop.SellItem(item.Name, item.Slot.DisplayName(), prices.SellItem), GUILayout.Height(26f)))
                {
                    _session.SellItem(i);
                    return;
                }
            }
            IReadOnlyList<StoredRune> runes = _session.RuneInventory.Runes;
            for (int i = 0; i < runes.Count; i++)
            {
                if (GUILayout.Button(UiTexts.Shop.SellRune(runes[i].Name, prices.SellRune), GUILayout.Height(26f)))
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

            GUILayout.Label(UiTexts.BoardFull(stock[_runeAwaitingSlot].Name), _titleStyle);
            GUILayout.Space(6f);

            if (GUILayout.Button(UiTexts.Shop.BuyToRuneInventory, GUILayout.Height(32f)))
            {
                _session.BuyShopRune(_runeAwaitingSlot);
                _runeAwaitingSlot = -1;
                return;
            }
            GUILayout.Label(UiTexts.SwapRelayHint, _plainStyle);

            IReadOnlyList<RelayChip> relays = _session.Board.Relays;
            for (int slot = 0; slot < relays.Count; slot++)
            {
                RelayChip relay = relays[slot];
                if (GUILayout.Button($"{UiTexts.Offer.Instead(relay.Name)}  [{relay.Rune.Tag.DisplayName()}]\n{relay.Description}", _itemStyle, GUILayout.Height(52f)))
                {
                    _session.BuyShopRune(_runeAwaitingSlot, slot);
                    _runeAwaitingSlot = -1;
                    return;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button(UiTexts.Back, GUILayout.Height(30f))) _runeAwaitingSlot = -1;
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
            _lockStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, richText = true, alignment = TextAnchor.MiddleCenter };
        }
    }
}
