using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Gear;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Fenster «Inventar» (Taste I), klassisch wie im Rollenspiel: oben die Figur des Ritters mit den 7 Ausrüstungsplätzen
    /// (Helm am Kopf, Handschuhe an der Hand, Brust, Beinschienen, Stiefel, Waffe und Schild an den Seiten), daneben Details
    /// und Tags, darunter das Item-Raster mit fester Zellenzahl und eigener Reihenfolge. Ausrüsten und Sortieren per Drag &amp; Drop,
    /// Doppel- oder Rechtsklick legt an bzw. ab. Runen und Skills stehen im Fenster «Build».
    /// Alle Aktionen laufen über die Session; während Kampf und offenen Entscheidungen ist das Fenster nur lesbar.
    /// </summary>
    public sealed class InventoryWindow : MonoBehaviour
    {
        private const int GridColumns = 6;
        private const float CellWidth = 128f;
        private const float CellHeight = 62f;
        private const float SlotWidth = 116f;
        private const float SlotHeight = 52f;

        private OverworldSession _session;
        private readonly DragDrop _drag = new DragDrop();
        private readonly StatBar _statBar = new StatBar();
        private Vector2 _detailScroll;

        // Auswahl für die Details: ein Platz an der Figur oder eine Zelle (nie beides).
        private EquipmentSlot? _selectedSlot;
        private int _selectedCell = -1;

        // Überfahrenes Element für die Stat-Vorschau (im Repaint gesammelt, im nächsten Frame gezeigt).
        private DragItem? _hover;
        private DragItem? _hoverNext;

        public bool IsOpen { get; private set; }

        public void Initialize(OverworldSession session)
        {
            _session = session;
            Close();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open()
        {
            IsOpen = _session != null;
            ClearSelection();
        }

        public void Close()
        {
            IsOpen = false;
            _drag.Cancel();
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedSlot = null;
            _selectedCell = -1;
            _hover = null;
        }

        private void OnGUI()
        {
            if (!IsOpen || _session == null) return;
            UiTheme.Apply();
            GUI.depth = -5;
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                Event.current.Use();
                return;
            }

            _drag.Enabled = _session.CanChangeLoadout;
            if (Event.current.type == EventType.Repaint) _hoverNext = null;

            Rect area = OverworldHud.BelowTopBar(1180f, 880f);
            float width = area.width;
            float height = area.height;
            GUILayout.BeginArea(area, GUI.skin.box);

            DrawHeader();
            DrawStats();

            GUILayout.BeginHorizontal();
            DrawFigure();
            DrawDetails(width - 420f, 470f);
            GUILayout.EndHorizontal();

            DrawGrid();
            GUILayout.Label($"<color=#9aa4b2>{UiTexts.Inventory.Hint}</color>", UiTheme.Small);
            GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint) _hover = _hoverNext;
            if (!_drag.IsDragging) UiTheme.DrawTooltip();
            _drag.Finish();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            Inventory inventory = _session.Inventory;
            GUILayout.Label($"{UiTexts.Inventory.TitleName}  <size={UiTheme.SmallSize}><color=#9aa4b2>{UiTexts.Inventory.Title(inventory.Count, inventory.Capacity, _session.Stats.Gold)}</color></size>", UiTheme.Title);
            GUILayout.FlexibleSpace();
            if (!_session.CanChangeLoadout)
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Bad)}>{UiTexts.ReadOnly}</color>", UiTheme.Small, GUILayout.ExpandWidth(false));
            if (GUILayout.Button(new GUIContent(UiTexts.Close, UiTexts.Inventory.CloseTip), GUILayout.Width(34f), GUILayout.Height(28f))) Close();
            GUILayout.EndHorizontal();
        }

        /// <summary>Stat-Leiste; beim Ziehen oder Überfahren eines Teils mit Vorschau «Rüstung 6 → 9».</summary>
        private void DrawStats()
        {
            DragItem? focus = _drag.Dragging ?? _hover;
            List<StatChange> preview = null;
            string title = null;
            if (focus.HasValue && focus.Value.Kind == DragKind.Item && _session.Inventory[focus.Value.A] is EquipmentDefinition item)
            {
                preview = _session.PreviewEquip(item);
                title = UiTexts.Inventory.PreviewEquip(item.Name);
            }
            else if (focus.HasValue && focus.Value.Kind == DragKind.Equipped && _session.Gear.Get((EquipmentSlot)focus.Value.A) is EquipmentDefinition worn)
            {
                preview = _session.PreviewUnequip((EquipmentSlot)focus.Value.A);
                title = UiTexts.Inventory.PreviewUnequip(worn.Name);
            }
            _statBar.Draw(_session, preview, title);
        }

        // ------------------------------------------------------------------ Figur

        private void DrawFigure()
        {
            const float w = 400f, h = 470f;
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(w), GUILayout.Height(h));
            GUILayout.Label(UiTexts.Inventory.Knight, UiTheme.Text);
            Rect figure = GUILayoutUtility.GetRect(w - 20f, h - 50f);
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint) DrawSilhouette(figure);

            float cx = figure.center.x - SlotWidth * 0.5f;
            DrawSlot(EquipmentSlot.Helmet, new Rect(cx, figure.y + 4f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Chest, new Rect(cx, figure.y + 104f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Legs, new Rect(cx, figure.y + 228f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Boots, new Rect(cx, figure.y + 340f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Weapon, new Rect(figure.x + 2f, figure.y + 120f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Gloves, new Rect(figure.x + 2f, figure.y + 196f, SlotWidth, SlotHeight));
            DrawSlot(EquipmentSlot.Shield, new Rect(figure.xMax - SlotWidth - 2f, figure.y + 120f, SlotWidth, SlotHeight));
        }

        /// <summary>Platzhalter-Figur aus Flächen: Kopf, Rumpf, Arme, Beine.</summary>
        private static void DrawSilhouette(Rect f)
        {
            var body = new Color(0.20f, 0.23f, 0.29f, 1f);
            var limb = new Color(0.17f, 0.195f, 0.25f, 1f);
            float cx = f.center.x;
            UiTheme.Fill(new Rect(cx - 30f, f.y + 2f, 60f, 62f), body);            // Kopf
            UiTheme.Fill(new Rect(cx - 12f, f.y + 64f, 24f, 18f), limb);           // Hals
            UiTheme.Fill(new Rect(cx - 64f, f.y + 82f, 128f, 140f), body);         // Rumpf
            UiTheme.Fill(new Rect(cx - 112f, f.y + 92f, 48f, 26f), limb);          // linker Arm
            UiTheme.Fill(new Rect(cx - 112f, f.y + 92f, 22f, 150f), limb);
            UiTheme.Fill(new Rect(cx + 64f, f.y + 92f, 48f, 26f), limb);           // rechter Arm
            UiTheme.Fill(new Rect(cx + 90f, f.y + 92f, 22f, 110f), limb);
            UiTheme.Fill(new Rect(cx - 52f, f.y + 222f, 40f, 170f), limb);         // Beine
            UiTheme.Fill(new Rect(cx + 12f, f.y + 222f, 40f, 170f), limb);
        }

        private void DrawSlot(EquipmentSlot slot, Rect rect)
        {
            EquipmentDefinition item = _session.Gear.Get(slot);
            bool locked = slot == EquipmentSlot.Shield && _session.Gear.IsShieldLocked;
            string name = item != null ? $"<b>{item.Name}</b>" : locked ? $"<color=#888888>{UiTexts.Inventory.Locked}</color>" : $"<color=#888888>{UiTexts.Inventory.Empty}</color>";
            string text = $"<color=#9aa4b2>{slot.DisplayName()}</color>\n{name}";
            GUIStyle style = _selectedSlot == slot ? UiTheme.CellSelected : item != null ? UiTheme.Cell : UiTheme.EmptyCell;
            var cell = new GUIStyle(style) { wordWrap = true, alignment = TextAnchor.MiddleCenter };
            GUI.Box(rect, new GUIContent(text, item != null ? ItemText.Details(item, _session) : null), cell);

            if (item != null)
            {
                _drag.Source(rect, new DragItem(DragKind.Equipped, (int)slot, item.Name),
                    click: () => Select(slot), shortcut: () => _session.UnequipToInventory(slot));
                Hover(rect, new DragItem(DragKind.Equipped, (int)slot, item.Name));
            }
            _drag.Target(rect, d => d.Kind == DragKind.Item && _session.CanEquipTo(d.A, slot), d => Equip(d.A, slot), redWhenInvalid: true);
        }

        private void Equip(int cell, EquipmentSlot slot)
        {
            if (_session.EquipFromInventoryTo(cell, slot)) ClearSelection();
        }

        // ------------------------------------------------------------------ Details

        private void DrawDetails(float width, float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(width), GUILayout.Height(height));
            _detailScroll = GUILayout.BeginScrollView(_detailScroll);
            EquipmentDefinition item = _selectedSlot.HasValue ? _session.Gear.Get(_selectedSlot.Value) : _session.Inventory[_selectedCell];
            if (item == null)
            {
                GUILayout.Label(UiTexts.Inventory.Details, UiTheme.Text);
                GUILayout.Label($"<color=#9aa4b2>{UiTexts.Inventory.DetailsHint}</color>", UiTheme.Small);
            }
            else
            {
                DrawSelection(item);
            }
            GUILayout.Space(8f);
            DrawTags();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSelection(EquipmentDefinition item)
        {
            GUILayout.Label(ItemText.Details(item, _session) + RuneText.Eases(_session, item.Id), UiTheme.Text);
            GUILayout.Label(ItemText.Compare(item, _session.Gear.Get(item.Slot)), UiTheme.Small);
            if (!_selectedSlot.HasValue)
            {
                string preview = ItemText.TagPreview(_session, item);
                if (preview.Length > 0) GUILayout.Label(preview, UiTheme.Small);
                string evolutions = SkillText.EvolutionHints(_session.EvolutionHintsForItem(item));
                if (evolutions.Length > 0) GUILayout.Label(evolutions.TrimStart('\n'), UiTheme.Small);
            }

            GUILayout.BeginHorizontal();
            if (_selectedSlot.HasValue)
            {
                EquipmentSlot slot = _selectedSlot.Value;
                GUI.enabled = _session.CanUnequipToInventory(slot);
                if (GUILayout.Button(UiTexts.Inventory.Unequip, GUILayout.Height(28f)) && _session.UnequipToInventory(slot)) ClearSelection();
            }
            else
            {
                int cell = _selectedCell;
                GUI.enabled = _session.CanEquipFromInventory(cell);
                if (GUILayout.Button(UiTexts.Inventory.Equip, GUILayout.Height(28f)) && _session.EquipFromInventory(cell)) ClearSelection();
                GUI.enabled = _session.CanChangeLoadout;
                if (GUILayout.Button(UiTexts.Inventory.Discard, GUILayout.Height(28f)) && _session.DiscardItem(cell)) ClearSelection();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!_selectedSlot.HasValue && !_session.Gear.CanEquip(item, out string reason))
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Bad)}>{reason}</color>", UiTheme.Small);
        }

        /// <summary>Tag-Zähler und getragene Sets kompakt; das Rezeptbuch steht im Fenster «Build».</summary>
        private void DrawTags()
        {
            GUILayout.Label($"{UiTexts.Inventory.TagsTitle}  <color=#9aa4b2>{UiTexts.Inventory.TagsLegend}</color>", UiTheme.Text);
            // Alle Tags mit ihren Stufen, auch ohne getragenes Teil: so ist sichtbar, was Toxin, Schrott, Ladung … bewirken.
            foreach (SynergyTag tag in _session.Synergies.Tags)
            {
                int count = _session.Gear.TagCount(tag.Id);
                string block = ItemText.TagBlock(tag, count, count, false);
                GUILayout.Label(count > 0 ? block : $"<color={UiTheme.Hex(UiTheme.MutedColor)}>{block}</color>", UiTheme.Small);
            }
            foreach (SynergyDuo duo in _session.ActiveDuos())
            {
                string text = _session.IsDuoDiscovered(duo.Id) ? duo.Effect.Text : UiTexts.Inventory.DuoUnknown;
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Good)}>{UiTexts.Inventory.DuoActive(_session.DuoName(duo), text)}</color>", UiTheme.Small);
            }

            // Alle Sets mit ihren Boni, auch ohne getragenes Teil: so ist sichtbar, wofür man sammelt.
            GUILayout.Space(4f);
            foreach (SetDefinition set in _session.Sets.All)
            {
                int pieces = _session.Gear.SetPieces(set.Id);
                string block = ItemText.SetBlock(set, pieces, true);
                GUILayout.Label(pieces > 0 ? block : $"<color={UiTheme.Hex(UiTheme.MutedColor)}>{block}</color>", UiTheme.Small);
            }
        }

        // ------------------------------------------------------------------ Item-Raster

        private void DrawGrid()
        {
            Inventory inventory = _session.Inventory;
            GUILayout.BeginVertical(UiTheme.Section);
            GUILayout.Label($"{UiTexts.Inventory.GridTitle}  <color=#9aa4b2>{UiTexts.Inventory.GridLegend}</color>", UiTheme.Text);
            int rows = (inventory.Capacity + GridColumns - 1) / GridColumns;
            for (int r = 0; r < rows; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < GridColumns; c++)
                {
                    int cell = r * GridColumns + c;
                    if (cell >= inventory.Capacity) break;
                    DrawCell(cell);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawCell(int cell)
        {
            IInventoryItem entry = _session.Inventory.ItemAt(cell);
            EquipmentDefinition item = entry as EquipmentDefinition;
            GUIStyle baseStyle = entry == null ? UiTheme.EmptyCell : _selectedCell == cell ? UiTheme.CellSelected : UiTheme.Cell;
            var style = new GUIStyle(baseStyle) { wordWrap = true, alignment = TextAnchor.MiddleCenter };

            string text;
            string tip = null;
            if (item != null)
            {
                string set = item.SetId != null ? $"\n<color=#ffd75e>{_session.Sets.NameOf(item.SetId)}</color>" : string.Empty;
                text = $"<b>{item.Name}</b>\n<color=#9aa4b2>{item.Slot.DisplayName()}</color>{set}";
                tip = ItemText.Details(item, _session);
            }
            else
            {
                text = entry != null ? $"<b>{entry.Name}</b>" : string.Empty;
            }

            GUILayout.Box(new GUIContent(text, tip), style, GUILayout.Width(CellWidth), GUILayout.Height(CellHeight));
            Rect rect = GUILayoutUtility.GetLastRect();
            if (entry != null)
            {
                _drag.Source(rect, new DragItem(DragKind.Item, cell, entry.Name),
                    click: () => Select(cell), shortcut: () => { if (_session.EquipFromInventory(cell)) ClearSelection(); });
                Hover(rect, new DragItem(DragKind.Item, cell, entry.Name));
            }
            _drag.Target(rect, d => AcceptsOnCell(d, cell), d => DropOnCell(d, cell));
        }

        private bool AcceptsOnCell(DragItem d, int cell)
        {
            switch (d.Kind)
            {
                case DragKind.Item: return d.A != cell;
                case DragKind.Equipped: return _session.CanUnequipToInventory((EquipmentSlot)d.A, cell);
                default: return false;
            }
        }

        private void DropOnCell(DragItem d, int cell)
        {
            if (d.Kind == DragKind.Item && _session.MoveInventoryItem(d.A, cell))
            {
                if (_selectedCell == d.A) _selectedCell = cell;
                else if (_selectedCell == cell) _selectedCell = d.A;
            }
            else if (d.Kind == DragKind.Equipped && _session.UnequipToInventory((EquipmentSlot)d.A, cell))
            {
                ClearSelection();
            }
        }

        // ------------------------------------------------------------------ Hilfen

        private void Select(EquipmentSlot slot)
        {
            ClearSelection();
            _selectedSlot = slot;
        }

        private void Select(int cell)
        {
            ClearSelection();
            _selectedCell = cell;
        }

        private void Hover(Rect rect, DragItem item)
        {
            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition)) _hoverNext = item;
        }
    }
}
