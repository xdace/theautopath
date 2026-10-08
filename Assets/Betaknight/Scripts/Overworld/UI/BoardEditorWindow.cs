using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Tafel-Editor per IMGUI: Zeilen nach oben/unten schieben (Priorität) und jeder Rune einen Skill aus der
    /// Ausrüstung zuordnen. Unter jedem Skill stehen seine Kennzahlen (aus den Effekten abgeleitet, siehe SkillInfo).
    /// Ändert nur die Session, gerechnet wird erst im nächsten Kampf.
    /// </summary>
    public sealed class BoardEditorWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private GUIStyle _title;
        private GUIStyle _text;
        private GUIStyle _info;
        private GUIStyle _tooltip;
        private Vector2 _scroll;

        // Werte des Ritters (Waffe, Stats, Set-Boni) einmal pro Frame, nicht pro OnGUI-Aufruf.
        private SkillUserStats _stats;
        private int _statsFrame = -1;

        public bool IsOpen { get; private set; }

        public void Initialize(OverworldSession session)
        {
            _session = session;
            IsOpen = false;
        }

        public void Toggle() => IsOpen = !IsOpen && _session != null;

        /// <summary>Öffnet den Editor, z. B. direkt aus der Kampf-Auswertung.</summary>
        public void Open() => IsOpen = _session != null;

        private void OnGUI()
        {
            if (!IsOpen || _session == null) return;
            EnsureStyles();
            GUI.depth = -5;

            if (_statsFrame != Time.frameCount)
            {
                _stats = _session.SkillUserStats();
                _statsFrame = Time.frameCount;
            }

            const float width = 700f;
            float content = 112f * (_session.Runes.Rows.Count + 1) + 90f * _session.WornSets().Count + 44f * (_session.RuneInventory.Count + 1);
            float height = Mathf.Min(Screen.height - 40f, 150f + content);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("<b>Logik-Tafel</b>  –  oberste erfüllte Zeile mit bereitem Skill feuert", _title);
            GUILayout.Label($"<size=12>Waffenschaden {_stats.WeaponDamage}, Werte mit aktueller Ausrüstung und aktiven Set-Boni, "
                + "gegen ein Ziel ohne Rüstung. Maus über eine Zeile zeigt alle Details.</size>", _info);
            GUILayout.Space(4f);

            // Bei vielen Zeilen und Sets scrollt der Inhalt, das Fenster läuft nie über den Bildschirm.
            _scroll = GUILayout.BeginScrollView(_scroll);
            List<string> options = SkillOptions();
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            for (int i = 0; i < rows.Count; i++) DrawRow(i, rows[i], options, rows.Count);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("↓  [Immer] → <b>Basisangriff</b> (fest, ganz unten)", _text);
            DrawSkillInfo(SkillDefinition.BasicAttackId);
            GUILayout.EndVertical();

            DrawRuneInventory(rows.Count);
            DrawSets();
            GUILayout.EndScrollView();

            if (GUILayout.Button("Schliessen", GUILayout.Height(30f))) IsOpen = false;
            GUILayout.EndArea();

            DrawTooltip();
        }

        private void DrawRow(int index, RuneSlot row, List<string> options, int count)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(28f));
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲")) _session.MoveRow(index, index - 1);
            GUI.enabled = index < count - 1;
            if (GUILayout.Button("▼")) _session.MoveRow(index, index + 1);
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Label($"{index + 1}. <b>{row.Name}</b>\n<size=12>{row.Description}</size>", _text, GUILayout.Width(330f));

            int current = options.IndexOf(_session.Gear.ProvidesSkill(row.SkillId) ? row.SkillId : null);
            if (GUILayout.Button("◀", GUILayout.Width(28f))) Assign(index, options, current - 1);
            GUILayout.Label(current >= 0 ? SkillLabel(options[current]) : "<color=#888888>— (leer)</color>", _text, GUILayout.Width(190f));
            if (GUILayout.Button("▶", GUILayout.Width(28f))) Assign(index, options, current + 1);
            GUI.enabled = _session.CanChangeLoadout && !_session.RuneInventory.IsFull;
            if (GUILayout.Button(new GUIContent("ab", "Rune ablegen (ins Runen-Inventar, behält ihre Stufe)"), GUILayout.Width(32f)))
                _session.UnequipRune(index);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            DrawSkillInfo(current >= 0 ? options[current] : null);
            GUILayout.EndVertical();
        }

        /// <summary>Infozeile unter dem Skill: Wirkung, Schaden (Prozent und konkret), CD, Ausholen, Erholung. Tooltip mit allen Details.</summary>
        private void DrawSkillInfo(string skillId)
        {
            SkillInfo info = skillId != null ? _session.DescribeSkill(skillId, _stats) : null;
            if (info == null)
            {
                GUILayout.Label("<size=12><color=#888888>Kein Skill: die Zeile pausiert und wird übersprungen.</color></size>", _info);
                return;
            }

            string other = info.OtherEffectsText;
            string effects = other.Length > 0 ? $"{info.DamageText} · {other}" : info.DamageText;
            string text = $"<size=12>{info.Skill.Description}\n<color=#ffd75e>{effects}</color>\n<color=#9fc7ff>{info.TimingText}</color></size>";
            GUILayout.Label(new GUIContent(text, info.Details), _info);
        }

        private void DrawTooltip()
        {
            if (string.IsNullOrEmpty(GUI.tooltip)) return;

            var content = new GUIContent(GUI.tooltip);
            const float tipWidth = 340f;
            float tipHeight = _tooltip.CalcHeight(content, tipWidth);
            Vector2 mouse = Event.current.mousePosition;
            float x = Mathf.Min(mouse.x + 16f, Screen.width - tipWidth - 8f);
            float y = Mathf.Min(mouse.y + 16f, Screen.height - tipHeight - 8f);
            GUI.Label(new Rect(x, y, tipWidth, tipHeight), content, _tooltip);
        }

        /// <summary>Runen-Inventar: als neue Zeile einsetzen oder mit einer Zeile tauschen (der Skill bleibt an der Zeile).</summary>
        private void DrawRuneInventory(int rowCount)
        {
            IReadOnlyList<StoredRune> stored = _session.RuneInventory.Runes;
            GUILayout.Space(4f);
            GUILayout.Label($"<b>Runen-Inventar</b> ({stored.Count}/{_session.RuneInventory.Capacity})"
                + (stored.Count == 0 ? "  <color=#888888>leer</color>" : string.Empty), _text);

            for (int i = 0; i < stored.Count; i++)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"<b>{stored[i].Name}</b>\n<size=12>{stored[i].Description}</size>", _info, GUILayout.Width(300f));
                GUI.enabled = _session.CanChangeLoadout && !_session.Runes.IsFull;
                if (GUILayout.Button("einsetzen", GUILayout.Width(80f)))
                {
                    _session.EquipRuneFromInventory(i);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    return;
                }
                GUI.enabled = _session.CanChangeLoadout;
                for (int row = 0; row < rowCount; row++)
                {
                    if (GUILayout.Button(new GUIContent($"↔{row + 1}", $"Mit Zeile {row + 1} tauschen"), GUILayout.Width(40f)))
                    {
                        _session.SwapRune(row, i);
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                        return;
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawSets()
        {
            foreach ((SetDefinition set, int pieces) in _session.WornSets())
            {
                GUILayout.Space(4f);
                GUILayout.Label($"<b>{set.Name}</b> {pieces}/{set.MaxPieces}", _text);
                foreach (KeyValuePair<int, string> bonus in set.Bonuses)
                {
                    string line = $"  {bonus.Key} Teile: {bonus.Value}";
                    GUILayout.Label(pieces >= bonus.Key ? $"<color=#ffd75e>{line}</color>" : $"<color=#888888>{line}</color>", _text);
                }
            }
        }

        private void Assign(int row, List<string> options, int index)
        {
            if (options.Count == 0) return;
            index = ((index % options.Count) + options.Count) % options.Count;
            _session.AssignSkill(row, options[index]);
        }

        /// <summary>Getragene Skills, dann Basisangriff, dann leer (Zeile pausiert).</summary>
        private List<string> SkillOptions()
        {
            var options = new List<string>(_session.Gear.SkillIds);
            options.Add(SkillDefinition.BasicAttackId);
            options.Add(null);
            return options;
        }

        private string SkillLabel(string id)
        {
            if (id == null) return "<color=#888888>— (leer)</color>";
            return _session.SkillCatalog.TryGet(id, out SkillDefinition s) ? s.Name : id;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            _info = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            _tooltip = new GUIStyle(GUI.skin.box)
            {
                fontSize = 13, richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(8, 8, 6, 6),
            };
        }
    }
}
