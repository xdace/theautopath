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
    /// Ausrüstung zuordnen. Ändert nur die Session, gerechnet wird erst im nächsten Kampf.
    /// </summary>
    public sealed class BoardEditorWindow : MonoBehaviour
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        private OverworldSession _session;
        private GUIStyle _title;
        private GUIStyle _text;

        public bool IsOpen { get; private set; }

        public void Initialize(OverworldSession session)
        {
            _session = session;
            IsOpen = false;
        }

        public void Toggle() => IsOpen = !IsOpen && _session != null;

        private void OnGUI()
        {
            if (!IsOpen || _session == null) return;
            EnsureStyles();
            GUI.depth = -5;

            const float width = 620f;
            float height = Mathf.Min(Screen.height - 40f, 140f + 64f * (_session.Runes.Rows.Count + 1) + 90f * _session.WornSets().Count);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("<b>Logik-Tafel</b>  –  oberste erfüllte Zeile mit bereitem Skill feuert", _title);
            GUILayout.Space(6f);

            List<string> options = SkillOptions();
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            for (int i = 0; i < rows.Count; i++) DrawRow(i, rows[i], options, rows.Count);

            GUILayout.Label("↓  [Immer] → Basisangriff (fest)", _text);
            DrawSets();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Schliessen", GUILayout.Height(30f))) IsOpen = false;
            GUILayout.EndArea();
        }

        private void DrawRow(int index, RuneSlot row, List<string> options, int count)
        {
            GUILayout.BeginHorizontal(GUI.skin.box);

            GUILayout.BeginVertical(GUILayout.Width(28f));
            GUI.enabled = index > 0;
            if (GUILayout.Button("▲")) _session.MoveRow(index, index - 1);
            GUI.enabled = index < count - 1;
            if (GUILayout.Button("▼")) _session.MoveRow(index, index + 1);
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Label($"{index + 1}. <b>{row.Name}</b>\n<size=12>{row.Description}</size>", _text, GUILayout.Width(300f));

            int current = options.IndexOf(_session.Gear.ProvidesSkill(row.SkillId) ? row.SkillId : null);
            if (GUILayout.Button("◀", GUILayout.Width(28f))) Assign(index, options, current - 1);
            GUILayout.Label(current >= 0 ? SkillLabel(options[current]) : "<color=#888888>— (leer)</color>", _text, GUILayout.Width(170f));
            if (GUILayout.Button("▶", GUILayout.Width(28f))) Assign(index, options, current + 1);

            GUILayout.EndHorizontal();
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

        private static string SkillLabel(string id)
        {
            if (id == null) return "<color=#888888>— (leer)</color>";
            return Skills.TryGet(id, out SkillDefinition s) ? s.Name : id;
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true, wordWrap = true };
            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
        }
    }
}
