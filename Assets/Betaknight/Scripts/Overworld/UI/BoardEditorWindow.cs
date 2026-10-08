using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Tafel-Editor per IMGUI: Zeilen nach oben/unten schieben (Priorität), Rune und Skill pro Zeile getrennt wählen.
    /// Runen kommen aus dem Runen-Inventar, Skills aus der Skill-Sammlung (jedes Exemplar sitzt an genau einer Zeile).
    /// Unter jedem Skill stehen seine Kennzahlen (aus den Effekten abgeleitet, siehe SkillInfo).
    /// Module sitzen an Baustein (Zeile) oder Skill; Auslöser zeigen ihr Ziel und werden als Linie zwischen den Zeilen gezeichnet.
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
        private readonly List<Rect> _rowRects = new List<Rect>();

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
            float content = 142f * (_session.Runes.Rows.Count + 1) + 44f * (_session.Modules.Count + 1) + 90f * _session.WornSets().Count + 44f * (_session.RuneInventory.Count + 1)
                + 44f * (_session.Skills.Count + 1);
            float height = Mathf.Min(Screen.height - 40f, 150f + content);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label("<b>Logik-Tafel</b>  –  oberste erfüllte Zeile mit bereitem Skill feuert", _title);
            GUILayout.Label($"<size=12>Waffenschaden {_stats.WeaponDamage}, Werte mit aktueller Ausrüstung und aktiven Set-Boni, "
                + "gegen ein Ziel ohne Rüstung. Maus über eine Zeile zeigt alle Details.</size>", _info);
            GUILayout.Space(4f);

            // Bei vielen Zeilen und Sets scrollt der Inhalt, das Fenster läuft nie über den Bildschirm.
            _scroll = GUILayout.BeginScrollView(_scroll);
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            for (int i = 0; i < rows.Count; i++) DrawRow(i, rows[i], rows.Count);
            DrawTriggerLinks(rows.Count);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("↓  [Immer] → <b>Basisangriff</b> (fest, ganz unten)", _text);
            DrawSkillInfo(SkillInstance.BasicAttack());
            GUILayout.EndVertical();

            DrawSkillCollection(rows.Count);
            DrawModuleCollection();
            DrawRuneInventory(rows.Count);
            DrawSets();
            GUILayout.EndScrollView();

            if (GUILayout.Button("Schliessen", GUILayout.Height(30f))) IsOpen = false;
            GUILayout.EndArea();

            DrawTooltip();
        }

        private void DrawRow(int index, RuneSlot row, int count)
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

            List<SkillInstance> options = SkillOptions(row);
            int current = row.Skill == null ? -1 : options.FindIndex(o => o == row.Skill || (o.IsBasicAttack && row.Skill.IsBasicAttack));
            GUI.enabled = _session.CanEditSkills;
            if (GUILayout.Button(new GUIContent("◀", "Vorheriger freier Skill aus der Sammlung"), GUILayout.Width(28f))) Assign(index, options, current - 1);
            GUILayout.Label(SkillLabel(row.Skill), _text, GUILayout.Width(150f));
            if (GUILayout.Button(new GUIContent("▶", "Nächster freier Skill aus der Sammlung"), GUILayout.Width(28f))) Assign(index, options, current + 1);
            GUI.enabled = _session.CanEditSkills && row.Skill != null;
            if (GUILayout.Button(new GUIContent("×", "Skill herausnehmen: zurück in die Sammlung, die Zeile pausiert"), GUILayout.Width(26f)))
                _session.RemoveSkill(index);
            GUI.enabled = _session.CanChangeLoadout && !_session.RuneInventory.IsFull;
            if (GUILayout.Button(new GUIContent("ab", "Rune ablegen (ins Runen-Inventar, behält ihre Stufe)"), GUILayout.Width(32f)))
                _session.UnequipRune(index);
            GUI.enabled = true;

            GUILayout.EndHorizontal();
            DrawSkillInfo(row.Skill);
            DrawModuleSlots("Baustein", row);
            if (row.Skill != null && !row.Skill.IsBasicAttack) DrawModuleSlots("Skill", row.Skill);
            GUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
            {
                while (_rowRects.Count <= index) _rowRects.Add(Rect.zero);
                _rowRects[index] = GUILayoutUtility.GetLastRect();
            }
        }

        /// <summary>
        /// Modul-Plätze eines Orts: eingesetzte Module (× nimmt ab, Auslöser: Klick wählt das nächste Ziel),
        /// bei freiem Platz Knöpfe für passende freie Module aus der Sammlung.
        /// </summary>
        private void DrawModuleSlots(string label, IModuleHolder holder)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<size=12>{label}-Module {holder.Modules.Count}/{holder.ModuleSlots}:</size>", _info, GUILayout.Width(130f));
            GUI.enabled = _session.CanEditModules;
            foreach (ModuleInstance m in holder.Modules)
            {
                GUIContent chip = ModuleText.Chip(_session, m);
                if (m.ModuleId == ModuleIds.Trigger)
                {
                    chip.tooltip = $"{chip.tooltip}\nKlick: nächstes Ziel wählen";
                    if (GUILayout.Button(chip, GUILayout.MaxWidth(280f))) _session.CycleTriggerTarget(m.InstanceId);
                }
                else
                {
                    GUILayout.Label(chip, _info, GUILayout.ExpandWidth(false));
                }
                if (GUILayout.Button(new GUIContent("×", "Modul abnehmen (zurück in die Sammlung)"), GUILayout.Width(22f)))
                {
                    _session.TakeOffModule(m.InstanceId);
                    break;
                }
            }

            if (holder.Modules.Count < holder.ModuleSlots)
            {
                var shown = new HashSet<string>();
                foreach (ModuleInstance m in _session.FreeModulesFor(holder))
                {
                    if (!shown.Add($"{m.ModuleId}+{m.Level}")) continue;
                    ModuleDefinition d = _session.ModuleDefinitionOf(m);
                    if (!GUILayout.Button(new GUIContent($"+ {m.NameFrom(_session.ModuleCatalog)}", d?.DescriptionAt(m.Level)), GUILayout.ExpandWidth(false))) continue;
                    if (holder is RuneSlot row) _session.PlaceModuleOnRow(m.InstanceId, _session.Runes.IndexOfRow(row));
                    else if (holder is SkillInstance skill) _session.PlaceModuleOnSkill(m.InstanceId, skill.InstanceId);
                    break;
                }
                if (shown.Count == 0) GUILayout.Label("<size=12><color=#888888>leer</color></size>", _info);
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// Auslöser als Linien rechts an den Zeilen: vom Skill (orange) bzw. Baustein (türkis) zur Zielzeile, Pfeil am Ziel.
        /// Jede Verbindung bekommt eine eigene Spur, so bleiben auch Kreise (A→B, B→A) und Selbst-Auslöser sichtbar.
        /// </summary>
        private void DrawTriggerLinks(int rowCount)
        {
            if (Event.current.type != EventType.Repaint || _rowRects.Count < rowCount) return;
            List<OverworldSession.TriggerLink> links = _session.TriggerLinks();
            Color old = GUI.color;
            for (int k = 0; k < links.Count; k++)
            {
                OverworldSession.TriggerLink link = links[k];
                Rect from = _rowRects[link.From];
                Rect to = _rowRects[link.To];
                float lane = from.xMax - 10f - 9f * k;
                float start = lane - 26f;
                float y1 = from.y + from.height * (link.FromBlock ? 0.25f : 0.5f);
                float y2 = to.y + to.height * 0.75f;
                if (link.From == link.To) y2 = from.y + from.height * 0.8f;

                GUI.color = link.FromBlock ? new Color(0.45f, 0.9f, 0.95f, 0.9f) : new Color(1f, 0.65f, 0.25f, 0.9f);
                Line(start, y1, lane, y1);
                Line(lane, Mathf.Min(y1, y2), lane, Mathf.Max(y1, y2));
                Line(start, y2, lane, y2);
                GUI.Label(new Rect(start - 14f, y2 - 10f, 16f, 20f), "◀", _info);
            }
            GUI.color = old;
        }

        private static void Line(float x1, float y1, float x2, float y2)
        {
            const float thickness = 2f;
            var rect = Mathf.Approximately(y1, y2)
                ? new Rect(Mathf.Min(x1, x2), y1 - thickness * 0.5f, Mathf.Abs(x2 - x1) + thickness, thickness)
                : new Rect(x1 - thickness * 0.5f, Mathf.Min(y1, y2), thickness, Mathf.Abs(y2 - y1));
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        /// <summary>Alle Modul-Exemplare mit Stufe, Art und Ort; eingesetzte lassen sich hier abnehmen.</summary>
        private void DrawModuleCollection()
        {
            IReadOnlyList<ModuleInstance> all = _session.Modules.All;
            GUILayout.Space(4f);
            GUILayout.Label($"<b>Module</b> ({all.Count}, davon {_session.Modules.Free.Count} frei)"
                + (all.Count == 0 ? "  <color=#888888>noch keine – selten aus Elite, Truhen, Boss-Flucht und Shop</color>" : string.Empty), _text);

            foreach (ModuleInstance m in all)
            {
                ModuleDefinition d = _session.ModuleDefinitionOf(m);
                string where = m.IsFree ? "<color=#7ddc6f>frei</color>" : _session.ModuleWhere(m);
                string trigger = m.ModuleId == ModuleIds.Trigger && !m.IsFree ? $" · {_session.DescribeTrigger(m)}" : string.Empty;
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"<b>{m.NameFrom(_session.ModuleCatalog)}</b>  <size=12>[{(d != null ? ModuleText.KindName(d.Kind) : "?")}] {where}{trigger}"
                    + $"\n{d?.DescriptionAt(m.Level)}</size>", _info);
                GUI.enabled = _session.CanEditModules && !m.IsFree;
                if (GUILayout.Button(new GUIContent("ab", "Modul abnehmen"), GUILayout.Width(32f)))
                {
                    _session.TakeOffModule(m.InstanceId);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                    return;
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>Infozeile unter dem Skill: Wirkung, Schaden (Prozent und konkret), CD, Ausholen, Erholung. Tooltip mit allen Details.</summary>
        private void DrawSkillInfo(SkillInstance skill)
        {
            SkillInfo info = skill != null ? _session.DescribeSkill(skill, _stats) : null;
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

        /// <summary>
        /// Sammlung: alle Skill-Exemplare mit Stufe, Arten und Ort. Ein freies Exemplar an eine Zeile setzen,
        /// ein eingesetztes mit dem Skill einer anderen Zeile tauschen (so verwaist keine Zeile).
        /// </summary>
        private void DrawSkillCollection(int rowCount)
        {
            IReadOnlyList<SkillInstance> all = _session.Skills.All;
            GUILayout.Space(4f);
            GUILayout.Label($"<b>Skill-Sammlung</b> ({all.Count}, davon {_session.Skills.Free.Count} frei)"
                + (all.Count == 0 ? "  <color=#888888>leer</color>" : string.Empty), _text);

            for (int i = 0; i < all.Count; i++)
            {
                SkillInstance skill = all[i];
                SkillInfo info = _session.DescribeSkill(skill, _stats);
                int at = skill.Holder is RuneSlot slot ? _session.Runes.IndexOfRow(slot) : -1;
                string where = at >= 0 ? $"Zeile {at + 1}" : "<color=#7ddc6f>frei</color>";
                string kinds = info != null ? SkillKinds.Names(info.Skill.Kinds) : string.Empty;

                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label(new GUIContent($"<b>{skill.NameFrom(_session.SkillCatalog)}</b>  <size=12>{where}"
                    + (kinds.Length > 0 ? $" · {kinds}" : string.Empty) + $"\n<color=#ffd75e>{info?.Summary}</color></size>", info?.Details),
                    _info, GUILayout.Width(380f));
                GUI.enabled = _session.CanEditSkills;
                for (int row = 0; row < rowCount; row++)
                {
                    if (row == at) continue;
                    string tip = at >= 0 ? $"Mit dem Skill von Zeile {row + 1} tauschen" : $"An Zeile {row + 1} setzen";
                    if (GUILayout.Button(new GUIContent($"→{row + 1}", tip), GUILayout.Width(40f)))
                    {
                        if (at >= 0) _session.SwapSkills(at, row);
                        else _session.PlaceSkill(skill.InstanceId, row);
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                        return;
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void Assign(int row, List<SkillInstance> options, int index)
        {
            if (options.Count == 0) return;
            index = ((index % options.Count) + options.Count) % options.Count;
            SkillInstance pick = options[index];
            if (pick.IsBasicAttack) _session.PlaceBasicAttack(row);
            else _session.PlaceSkill(pick.InstanceId, row);
        }

        /// <summary>Für eine Zeile wählbar: ihr eigener Skill und alle freien Exemplare (Reihenfolge der Sammlung), dann der Basisangriff.</summary>
        private List<SkillInstance> SkillOptions(RuneSlot row)
        {
            var options = new List<SkillInstance>();
            foreach (SkillInstance s in _session.Skills.All)
                if (s.IsFree || s == row.Skill) options.Add(s);
            options.Add(row.Skill != null && row.Skill.IsBasicAttack ? row.Skill : SkillInstance.BasicAttack());
            return options;
        }

        private string SkillLabel(SkillInstance skill)
        {
            if (skill == null) return "<color=#888888>— (leer)</color>";
            return skill.IsBasicAttack ? "Basisangriff" : skill.NameFrom(_session.SkillCatalog);
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
