using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Inventar per IMGUI: links die 7 Ausrüstungsplätze, rechts das Inventar, darunter Runentafel, Runen-Inventar und Skills.
    /// Ein Klick auf ein Teil zeigt Werte, passive Effekte und Set und vergleicht mit dem angelegten Teil.
    /// Alle Änderungen laufen über die Fassade und sind im Kampf und bei offenen Fenstern gesperrt.
    /// </summary>
    public sealed class InventoryWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _cell;
        private Vector2 _scroll;

        // Auswahl: ein angelegter Platz oder ein Inventarplatz (nie beides).
        private EquipmentSlot? _selectedSlot;
        private int _selectedItem = -1;
        private int _selectedRune = -1;

        public bool IsOpen { get; private set; }

        public void Initialize(OverworldSession session)
        {
            _session = session;
            IsOpen = false;
            ClearSelection();
        }

        public void Toggle()
        {
            IsOpen = !IsOpen && _session != null;
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedSlot = null;
            _selectedItem = -1;
            _selectedRune = -1;
        }

        private void OnGUI()
        {
            if (!IsOpen || _session == null) return;
            EnsureStyles();
            GUI.depth = -5;

            float width = Mathf.Min(Screen.width - 20f, 900f);
            float height = Mathf.Min(Screen.height - 40f, 760f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label($"<b>Inventar</b>   Ausrüstung {_session.Inventory.Count}/{_session.Inventory.Capacity}, "
                + $"Runen {_session.RuneInventory.Count}/{_session.RuneInventory.Capacity}", _title);
            if (!_session.CanChangeLoadout)
                GUILayout.Label("<color=#ff7a6b>Wechseln geht erst, wenn kein Kampf läuft und kein anderes Fenster offen ist.</color>", _small);

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.BeginHorizontal();
            DrawEquipped(width * 0.42f);
            DrawInventory(width * 0.52f);
            GUILayout.EndHorizontal();

            DrawSelection();
            GUILayout.Space(8f);
            DrawRunes();
            GUILayout.Space(8f);
            DrawSkills();
            GUILayout.Space(8f);
            DrawTags();
            GUILayout.EndScrollView();

            if (GUILayout.Button("Schliessen", GUILayout.Height(30f))) IsOpen = false;
            GUILayout.EndArea();
            DrawTooltip();
        }

        /// <summary>Details eines Skills beim Überfahren mit der Maus.</summary>
        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip)) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 13, richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft };
            var content = new GUIContent(GUI.tooltip);
            const float tipWidth = 340f;
            float tipHeight = style.CalcHeight(content, tipWidth);
            Vector2 mouse = Event.current.mousePosition;
            float x = Mathf.Min(mouse.x + 16f, Screen.width - tipWidth - 8f);
            float y = Mathf.Min(mouse.y + 16f, Screen.height - tipHeight - 8f);
            GUI.Label(new Rect(x, y, tipWidth, tipHeight), content, style);
        }

        // ------------------------------------------------------------------ Ausrüstung

        private void DrawEquipped(float width)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));
            GUILayout.Label("<b>Angelegt</b>", _text);
            foreach (EquipmentSlot slot in Equipment.AllSlots)
            {
                EquipmentDefinition item = _session.Gear.Get(slot);
                string name = item != null ? item.Name : slot == EquipmentSlot.Shield && _session.Gear.IsShieldLocked
                    ? "<color=#888888>gesperrt (Zweihand)</color>" : "<color=#888888>leer</color>";
                bool selected = _selectedSlot == slot;
                string label = $"{(selected ? "▶ " : string.Empty)}<b>{slot.DisplayName()}</b>: {name}{SetTag(item)}";
                if (GUILayout.Button(label, _cell, GUILayout.Height(30f)) && item != null)
                {
                    ClearSelection();
                    _selectedSlot = slot;
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawInventory(float width)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width));
            GUILayout.Label("<b>Inventar</b>", _text);
            Inventory inventory = _session.Inventory;
            for (int i = 0; i < inventory.Capacity; i++)
            {
                EquipmentDefinition item = inventory[i];
                if (item == null)
                {
                    GUILayout.Label("<color=#666666>  – frei –</color>", _small, GUILayout.Height(22f));
                    continue;
                }

                bool selected = _selectedItem == i;
                string label = $"{(selected ? "▶ " : string.Empty)}<b>{item.Name}</b> [{item.Slot.DisplayName()}]{SetTag(item)}";
                if (GUILayout.Button(label, _cell, GUILayout.Height(26f)))
                {
                    ClearSelection();
                    _selectedItem = i;
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawSelection()
        {
            EquipmentDefinition item = _selectedSlot.HasValue ? _session.Gear.Get(_selectedSlot.Value) : _session.Inventory[_selectedItem];
            if (item == null) return;

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(ItemText.Details(item, _session.Sets), _text);
            if (item.SetId != null)
                GUILayout.Label($"Set getragen: {_session.Gear.SetPieces(item.SetId)}/3 Teile", _small);
            GUILayout.Label(ItemText.Compare(item, _session.Gear.Get(item.Slot)), _text);
            if (!_selectedSlot.HasValue)
            {
                string preview = ItemText.TagPreview(_session, item);
                if (preview.Length > 0) GUILayout.Label(preview, _small);
            }

            GUILayout.BeginHorizontal();
            if (_selectedSlot.HasValue)
            {
                GUI.enabled = _session.CanUnequipToInventory(_selectedSlot.Value);
                if (GUILayout.Button("Ablegen (ins Inventar)", GUILayout.Height(28f)))
                {
                    _session.UnequipToInventory(_selectedSlot.Value);
                    ClearSelection();
                }
            }
            else
            {
                GUI.enabled = _session.CanEquipFromInventory(_selectedItem);
                if (GUILayout.Button("Anlegen", GUILayout.Height(28f)))
                {
                    _session.EquipFromInventory(_selectedItem);
                    ClearSelection();
                }
                GUI.enabled = _session.CanChangeLoadout;
                if (GUILayout.Button("Verwerfen", GUILayout.Height(28f)))
                {
                    _session.DiscardItem(_selectedItem);
                    ClearSelection();
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            if (!_selectedSlot.HasValue && !_session.Gear.CanEquip(item, out string reason))
                GUILayout.Label($"<color=#ff7a6b>{reason}</color>", _small);
            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ Runen

        private void DrawRunes()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Runentafel</b> ({_session.Runes.Rows.Count}/{_session.Runes.Slots})", _text);
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{i + 1}. <b>{rows[i].Name}</b>{RuneText.LevelBadge(rows[i].Rune, rows[i].Level)}", _small);
                GUI.enabled = _session.CanChangeLoadout && _selectedRune >= 0;
                if (GUILayout.Button("↔ tauschen", GUILayout.Width(90f)))
                {
                    _session.SwapRune(i, _selectedRune);
                    _selectedRune = -1;
                }
                GUI.enabled = _session.CanChangeLoadout && !_session.RuneInventory.IsFull;
                if (GUILayout.Button("ablegen", GUILayout.Width(70f))) _session.UnequipRune(i);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Runen-Inventar</b> ({_session.RuneInventory.Count}/{_session.RuneInventory.Capacity})", _text);
            IReadOnlyList<StoredRune> stored = _session.RuneInventory.Runes;
            if (stored.Count == 0) GUILayout.Label("<color=#666666>leer</color>", _small);
            for (int i = 0; i < stored.Count; i++)
            {
                GUILayout.BeginHorizontal();
                bool selected = _selectedRune == i;
                if (GUILayout.Button($"{(selected ? "▶ " : string.Empty)}<b>{stored[i].Name}</b>{RuneText.LevelBadge(stored[i].Rune, stored[i].Level)}  <size=11>{stored[i].Description}</size>", _cell))
                    _selectedRune = selected ? -1 : i;
                GUI.enabled = _session.CanChangeLoadout && !_session.Runes.IsFull;
                if (GUILayout.Button("einsetzen", GUILayout.Width(80f)))
                {
                    _session.EquipRuneFromInventory(i);
                    _selectedRune = -1;
                }
                GUI.enabled = _session.CanChangeLoadout;
                if (GUILayout.Button("verwerfen", GUILayout.Width(80f)))
                {
                    _session.DiscardRune(i);
                    _selectedRune = -1;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (stored.Count > 0)
                GUILayout.Label("<size=11>Rune anklicken, dann bei einer Zeile «tauschen»: der Skill bleibt an der Zeile.</size>", _small);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ Synergie-Tags

        /// <summary>
        /// Tag-Zähler mit allen Stufen (erreichte hervorgehoben, nächste Schwelle) und das Rezeptbuch der Duos:
        /// unentdeckte als Silhouette «???», aktive grün.
        /// </summary>
        private void DrawTags()
        {
            SynergyRegistry registry = _session.Synergies;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Synergie-Tags</b>  <size=11>(Schwellen 2/4/6 getragene Teile, Duo ab 4 + 4)</size>", _text);
            foreach (SynergyTag tag in registry.Tags)
            {
                int count = _session.Gear.TagCount(tag.Id);
                SynergyCounter counter = registry.Counter(tag, count);
                string head = count > 0 ? $"<b>{counter.Text}</b>" : $"<color=#888888>{counter.Text}</color>";
                var tiers = new List<string>();
                foreach (KeyValuePair<int, SynergyEffect> tier in tag.Tiers)
                {
                    string line = $"{tier.Key}: {tier.Value.Text}";
                    tiers.Add(count >= tier.Key ? $"<color=#ffd75e>{line}</color>" : $"<color=#888888>{line}</color>");
                }
                GUILayout.Label($"{head}\n<size=11>{string.Join("\n", tiers)}</size>", _small);
            }

            GUILayout.Label("<b>Rezeptbuch</b>", _text);
            foreach (SynergyDuo duo in registry.Duos)
            {
                bool active = registry.IsDuoActive(duo, _session.Gear);
                bool known = _session.IsDuoDiscovered(duo.Id);
                string pair = $"{registry.NameOf(duo.TagA)} {_session.Gear.TagCount(duo.TagA)}/4 + {registry.NameOf(duo.TagB)} {_session.Gear.TagCount(duo.TagB)}/4";
                string text = known ? $"<b>{duo.Name}</b> ({pair}): {duo.Effect.Text}" : $"<b>???</b> ({pair})";
                GUILayout.Label(active ? $"<color=#7ddc6f>{text}</color>" : text, _small);
            }
            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ Skills

        /// <summary>Alle Skill-Exemplare mit Stufe, Arten und Ort. Einsetzen und Umsetzen geht im Tafel-Editor.</summary>
        private void DrawSkills()
        {
            IReadOnlyList<SkillInstance> all = _session.Skills.All;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>Skills</b> ({all.Count}, davon {_session.Skills.Free.Count} frei)", _text);
            if (all.Count == 0) GUILayout.Label("<color=#666666>keine</color>", _small);
            SkillUserStats stats = all.Count > 0 ? _session.SkillUserStats() : null;
            foreach (SkillInstance skill in all)
            {
                SkillInfo info = _session.DescribeSkill(skill, stats);
                string kinds = info != null ? SkillKinds.Names(info.Skill.Kinds) : string.Empty;
                string where = skill.IsFree ? "<color=#7ddc6f>frei</color>" : SkillText.Where(_session, skill);
                string text = $"<b>{skill.NameFrom(_session.SkillCatalog)}</b>  Stufe {skill.Level}/{_session.Progression.MaxSkillLevel}"
                    + $"  [{kinds}]  {where}\n<size=11>{info?.Summary}</size>";
                GUILayout.Label(new GUIContent(text, info?.Details), _small);
            }
            if (_session.Skills.Free.Count > 0)
                GUILayout.Label("<size=11>Freie Skills setzt du im Tafel-Editor an eine Zeile.</size>", _small);
            GUILayout.EndVertical();
        }

        private string SetTag(EquipmentDefinition item) =>
            item?.SetId != null ? $"  <color=#ffd75e>{_session.Sets.NameOf(item.SetId)}</color>" : string.Empty;

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            _cell = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4),
            };
        }
    }
}
