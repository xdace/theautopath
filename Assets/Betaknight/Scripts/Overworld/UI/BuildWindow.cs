using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Evolution;
using Betaknight.Core.Gear;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Fenster «Build» (Taste B): in der Mitte die Logik-Tafel (eine Zeile pro Rune + Skill), links das Skill-Inventar,
    /// rechts Module und Runen-Inventar (oder das Rezeptbuch), oben die Stat-Leiste. Zusammengesteckt wird per Drag &amp; Drop,
    /// Doppel- oder Rechtsklick ist der Kurzweg für «in Zeile / aus Zeile». Jede Aktion ruft eine Methode der Session;
    /// während Kampf und offenen Entscheidungen ist das Fenster nur lesbar. Ausrüstung steht im Fenster «Inventar».
    /// </summary>
    public sealed class BuildWindow : MonoBehaviour
    {
        private const float RowHeight = 30f;
        private const float SideWidth = 290f;

        private OverworldSession _session;
        private readonly DragDrop _drag = new DragDrop();
        private readonly StatBar _statBar = new StatBar();
        private readonly List<Rect> _rowRects = new List<Rect>();
        private Vector2 _skillScroll, _boardScroll, _moduleScroll, _runeScroll, _recipeScroll;
        private bool _showRecipes;
        private SkillUserStats _skillStats;
        private int _skillStatsFrame = -1;

        // Was die Maus gerade überfährt (für die Vorschau), im Repaint gesammelt und im nächsten Frame gezeigt.
        private string _hover;
        private string _hoverNext;

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
            _hover = null;
        }

        public void Close()
        {
            IsOpen = false;
            _drag.Cancel();
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
            // Werte des Ritters einmal pro Frame, nicht pro OnGUI-Ereignis.
            if (_skillStatsFrame != Time.frameCount || _skillStats == null)
            {
                _skillStats = _session.SkillUserStats();
                _skillStatsFrame = Time.frameCount;
            }
            if (Event.current.type == EventType.Repaint) _hoverNext = null;

            float width = Mathf.Min(Screen.width - 24f, 1560f);
            float height = Mathf.Min(Screen.height - 24f, 880f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(area, GUI.skin.box);

            DrawHeader();
            _statBar.Draw(_session, null, null);
            DrawPreviewLine();

            float columns = height - 170f;
            GUILayout.BeginHorizontal();
            DrawSkillColumn(columns);
            DrawBoardColumn(width - SideWidth * 2f - 50f, columns);
            DrawRightColumn(columns);
            GUILayout.EndHorizontal();

            GUILayout.Label($"<color=#9aa4b2>{UiTexts.Build.Hint}</color>",
                UiTheme.Small);
            GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint) _hover = _hoverNext;
            if (!_drag.IsDragging) UiTheme.DrawTooltip();
            _drag.Finish();
        }

        // ------------------------------------------------------------------ Kopf und Vorschau

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            string rows = UiTexts.Build.Rows(_session.Runes.Rows.Count, _session.Runes.Slots, _session.Progression.MaxBoardRows);
            GUILayout.Label($"{UiTexts.Build.TitleName}  <size={UiTheme.SmallSize}><color=#9aa4b2>{rows} · {UiTexts.Build.FiresRule}</color></size>", UiTheme.Title);
            GUILayout.FlexibleSpace();
            if (!_session.CanChangeLoadout)
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Bad)}>{UiTexts.ReadOnly}</color>", UiTheme.Small, GUILayout.ExpandWidth(false));
            if (GUILayout.Button(new GUIContent(UiTexts.Close, UiTexts.Build.CloseTip), GUILayout.Width(34f), GUILayout.Height(28f))) Close();
            GUILayout.EndHorizontal();
        }

        private void DrawPreviewLine()
        {
            string text = _drag.IsDragging ? UiTexts.Build.Dragging(_drag.Dragging.Value.Label) : _hover;
            GUILayout.Label(string.IsNullOrEmpty(text) ? $"<color=#666c78>{UiTexts.Build.HoverHint}</color>" : text,
                UiTheme.SmallLine, GUILayout.Height(20f));
        }

        private void Hover(Rect rect, string text)
        {
            if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition)) _hoverNext = text;
        }

        // ------------------------------------------------------------------ Skill-Inventar

        private void DrawSkillColumn(float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(SideWidth), GUILayout.Height(height));
            IReadOnlyList<SkillInstance> all = _session.Skills.All;
            GUILayout.Label($"{UiTexts.Build.SkillsTitle}  <color=#9aa4b2>{UiTexts.Build.FreeOf(_session.Skills.Free.Count, all.Count)}</color>", UiTheme.Text);
            _skillScroll = GUILayout.BeginScrollView(_skillScroll);

            DrawBasicAttackCard();
            foreach (SkillInstance skill in all.OrderBy(s => s.IsFree ? 0 : 1)) DrawSkillCard(skill);
            if (all.Count == 0) GUILayout.Label($"<color=#888888>{UiTexts.Build.NoSkills}</color>", UiTheme.Small);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Skill aus einer Zeile hierher = herausnehmen.
            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.RowSkill && RowSkill(d.A) != null, d => _session.RemoveSkill(d.A));
        }

        private void DrawBasicAttackCard()
        {
            const string label = UiTexts.BasicAttack;
            GUILayout.Box(new GUIContent($"<b>{label}</b>  <color=#9aa4b2>{UiTexts.Build.AlwaysAvailable}</color>", UiTexts.Build.BasicAttackTip),
                UiTheme.Cell, GUILayout.Height(26f));
            Rect r = GUILayoutUtility.GetLastRect();
            _drag.Source(r, new DragItem(DragKind.BasicAttack, 0, label));
            Hover(r, $"<b>{label}</b>: {ShortStats(_session.DescribeSkill(SkillInstance.BasicAttack(), _skillStats))}");
        }

        private void DrawSkillCard(SkillInstance skill)
        {
            SkillInfo info = _session.DescribeSkill(skill, _skillStats);
            string name = skill.NameFrom(_session.SkillCatalog);
            int row = skill.Holder is RuneSlot slot ? _session.Runes.IndexOfRow(slot) : -1;
            string where = row >= 0 ? $"<color=#9aa4b2>{UiTexts.Row(row + 1)}</color>" : $"<color={UiTheme.Hex(UiTheme.Good)}>{UiTexts.Free}</color>";
            string modules = skill.Modules.Count > 0 ? $" <color=#ffd75e>◆{skill.Modules.Count}</color>" : string.Empty;
            string text = $"<b>{name}</b>{modules}  {where}\n<color=#ffd75e>{ShortStats(info)}</color>";
            GUILayout.Box(new GUIContent(text, SkillTip(skill, info)), skill.IsFree ? UiTheme.Cell : UiTheme.EmptyCell, GUILayout.Height(42f));
            Rect r = GUILayoutUtility.GetLastRect();
            _drag.Source(r, new DragItem(DragKind.Skill, skill.InstanceId, name), shortcut: () => SkillShortcut(skill));
            Hover(r, $"<b>{name}</b>: {ShortStats(info)} · {info?.TimingText} <color=#9aa4b2>{UiTexts.Build.SkillsKeepBar}</color>");
        }

        /// <summary>Kurzweg: freier Skill in die erste Zeile ohne Skill (sonst die erste mit Basisangriff), eingesetzter heraus.</summary>
        private void SkillShortcut(SkillInstance skill)
        {
            if (skill.Holder is RuneSlot slot)
            {
                _session.RemoveSkill(_session.Runes.IndexOfRow(slot));
                return;
            }
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            int target = rows.ToList().FindIndex(r => r.Skill == null);
            if (target < 0) target = rows.ToList().FindIndex(r => r.Skill.IsBasicAttack);
            if (target >= 0) _session.PlaceSkill(skill.InstanceId, target);
        }

        // ------------------------------------------------------------------ Logik-Tafel

        private void DrawBoardColumn(float width, float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.Label($"{UiTexts.Build.BoardTitle}  <color=#9aa4b2>{UiTexts.Build.BoardLegend}</color>", UiTheme.Text);
            Hover(GUILayoutUtility.GetLastRect(), UiTexts.Build.BoardRule(RowQueueConfig.RuleText));
            _boardScroll = GUILayout.BeginScrollView(_boardScroll);

            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            List<OverworldSession.TriggerLink> links = _session.TriggerLinks();
            for (int i = 0; i < rows.Count; i++) DrawRow(i, rows[i], links.Count);
            for (int i = rows.Count; i < _session.Runes.Slots; i++) DrawFreeSlot(i);
            DrawTriggerLinks(links, rows.Count);

            GUILayout.Box($"↓  [{UiTexts.Always}] → <b>{UiTexts.BasicAttack}</b>  <color=#9aa4b2>{UiTexts.Build.FallbackLegend} · {ShortStats(_session.DescribeSkill(SkillInstance.BasicAttack(), _skillStats))}</color>",
                UiTheme.EmptyCell, GUILayout.Height(RowHeight));

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawRow(int index, RuneSlot row, int linkCount)
        {
            GUILayout.BeginHorizontal(UiTheme.Cell, GUILayout.Height(RowHeight));

            // Griff: ganze Zeile ziehen.
            GUILayout.Label($"<color=#9aa4b2>≡ {index + 1}.</color>", UiTheme.SmallLine, GUILayout.Width(40f), GUILayout.Height(RowHeight - 6f));
            Rect handle = GUILayoutUtility.GetLastRect();
            _drag.Source(handle, new DragItem(DragKind.Row, index, UiTexts.Build.RowLabel(index + 1, row.Name)));

            // Rune (Wann) mit Stufen-Abzeichen und Wachstum.
            string growth = row.Growth > 0 ? $" <color=#b5e48c>+{row.Growth}</color>" : string.Empty;
            string rune = $"{RuneText.Difficulty(_session.RowDifficulty(row))} <b>{row.Name}</b>{RuneText.LevelBadge(row.Rune, row.Level)}{growth}";
            GUILayout.Label(new GUIContent(rune, RowTip(row)), UiTheme.SmallLine, GUILayout.Width(250f), GUILayout.Height(RowHeight - 6f));
            Rect runeRect = GUILayoutUtility.GetLastRect();
            _drag.Source(runeRect, new DragItem(DragKind.RowRune, index, row.Name), shortcut: () => _session.UnequipRune(index));
            Hover(runeRect, $"<b>{row.Name}</b>: {row.Description} {row.Rune.LevelText(row.Level)} · {DifficultyText.Tooltip(_session.RowDifficulty(row))}");

            DrawModuleSlots(row, index);
            GUILayout.Label("→", UiTheme.SmallLine, GUILayout.Width(16f));

            // Skill (Was) mit Kurzwerten.
            SkillInstance skill = row.Skill;
            // Werte so, wie sie von dieser Zeile aus wirken: inklusive Schwierigkeits-Bonus des Bausteins.
            SkillInfo info = skill != null ? _session.DescribeRowSkill(row, _skillStats) : null;
            string skillName = skill == null ? null : skill.IsBasicAttack ? UiTexts.BasicAttack : skill.NameFrom(_session.SkillCatalog);
            string skillText = skill == null
                ? $"<color=#888888>{UiTexts.Build.EmptyRow}</color>"
                : $"<b>{skillName}</b>  <color=#ffd75e>{ShortStats(info)}</color>";
            GUILayout.Label(new GUIContent(skillText, skill != null ? SkillTip(skill, info) : null), UiTheme.SmallLine, GUILayout.MinWidth(180f), GUILayout.Height(RowHeight - 6f));
            Rect skillRect = GUILayoutUtility.GetLastRect();
            if (skill != null)
            {
                _drag.Source(skillRect, new DragItem(DragKind.RowSkill, index, skillName), shortcut: () => _session.RemoveSkill(index));
                Hover(skillRect, $"<b>{skillName}</b>: {ShortStats(info)} · {info?.TimingText}");
            }

            if (skill != null && !skill.IsBasicAttack) DrawModuleSlots(skill, index);

            string marks = _session.IsEvolutionReady(row) ? $" <color=#d29bff>✦</color>" : string.Empty;
            GUILayout.Label(new GUIContent(marks, marks.Length > 0 ? UiTexts.Build.EvolvesTip : null), UiTheme.SmallLine, GUILayout.Width(18f));
            GUILayout.Space(16f + 9f * linkCount);
            GUILayout.EndHorizontal();

            Rect rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.Repaint)
            {
                while (_rowRects.Count <= index) _rowRects.Add(Rect.zero);
                _rowRects[index] = rowRect;
            }

            _drag.Target(rowRect, d => AcceptsOnRow(d, index), d => DropOnRow(d, index));
        }

        private bool AcceptsOnRow(DragItem d, int row)
        {
            switch (d.Kind)
            {
                case DragKind.Skill: return _session.Skills.Get(d.A) != null && _session.Skills.Get(d.A) != RowSkill(row);
                case DragKind.BasicAttack: return RowSkill(row)?.IsBasicAttack != true;
                case DragKind.RowSkill: return d.A != row;
                case DragKind.Row: return d.A != row;
                case DragKind.Rune: return true;
                default: return false;
            }
        }

        private void DropOnRow(DragItem d, int row)
        {
            switch (d.Kind)
            {
                case DragKind.Skill:
                    SkillInstance skill = _session.Skills.Get(d.A);
                    int from = skill?.Holder is RuneSlot slot ? _session.Runes.IndexOfRow(slot) : -1;
                    if (from >= 0) _session.SwapSkills(from, row);
                    else _session.PlaceSkill(d.A, row);
                    break;
                case DragKind.BasicAttack: _session.PlaceBasicAttack(row); break;
                case DragKind.RowSkill: _session.SwapSkills(d.A, row); break;
                case DragKind.Row: _session.MoveRow(d.A, row); break;
                case DragKind.Rune: _session.SwapRune(row, d.A); break;
            }
        }

        private void DrawFreeSlot(int index)
        {
            GUILayout.Box(UiTexts.Build.FreeSlot(index + 1), UiTheme.EmptyCell, GUILayout.Height(RowHeight));
            Rect r = GUILayoutUtility.GetLastRect();
            _drag.Target(r, d => d.Kind == DragKind.Rune && !_session.Runes.IsFull, d => _session.EquipRuneFromInventory(d.A, index));
        }

        /// <summary>Modul-Plätze eines Bausteins oder Skills: ◆ besetzt (ziehbar, Auslöser: Klick wählt das Ziel), ◇ frei (Ziel).</summary>
        private void DrawModuleSlots(IModuleHolder holder, int row)
        {
            foreach (ModuleInstance m in holder.Modules)
            {
                GUIContent chip = ModuleText.Chip(_session, m);
                bool trigger = m.ModuleId == ModuleIds.Trigger;
                if (trigger) chip.tooltip = chip.tooltip + UiTexts.Build.TriggerClick;
                chip.text = $"<color=#ffd75e>◆</color>{Shorten(chip.text, trigger ? 14 : 9)}";
                GUILayout.Label(chip, UiTheme.SmallLine, GUILayout.Width(trigger ? 112f : 76f), GUILayout.Height(RowHeight - 6f));
                Rect r = GUILayoutUtility.GetLastRect();
                int id = m.InstanceId;
                _drag.Source(r, new DragItem(DragKind.Module, id, m.NameFrom(_session.ModuleCatalog)),
                    click: trigger ? () => _session.CycleTriggerTarget(id) : (System.Action)null,
                    shortcut: () => _session.TakeOffModule(id));
                Hover(r, $"<b>{m.NameFrom(_session.ModuleCatalog)}</b>: {chip.tooltip}");
            }

            for (int i = holder.Modules.Count; i < holder.ModuleSlots; i++)
            {
                string tip = holder is RuneSlot ? UiTexts.Build.FreeRuneModuleSlot : UiTexts.Build.FreeSkillModuleSlot;
                GUILayout.Label(new GUIContent("<color=#666c78>◇</color>", tip), UiTheme.SmallLine, GUILayout.Width(20f), GUILayout.Height(RowHeight - 6f));
                Rect r = GUILayoutUtility.GetLastRect();
                _drag.Target(r, d => d.Kind == DragKind.Module && _session.CanPlaceModule(_session.Modules.Get(d.A), holder), d => PlaceModule(d.A, holder, row));
            }
        }

        private void PlaceModule(int moduleId, IModuleHolder holder, int row)
        {
            if (holder is RuneSlot) _session.PlaceModuleOnRow(moduleId, row);
            else if (holder is SkillInstance skill) _session.PlaceModuleOnSkill(moduleId, skill.InstanceId);
        }

        /// <summary>Auslöser als Linien rechts an den Zeilen (orange vom Skill, türkis vom Baustein), jede auf eigener Spur.</summary>
        private void DrawTriggerLinks(List<OverworldSession.TriggerLink> links, int rowCount)
        {
            if (Event.current.type != EventType.Repaint || _rowRects.Count < rowCount) return;
            for (int k = 0; k < links.Count; k++)
            {
                OverworldSession.TriggerLink link = links[k];
                if (link.From >= rowCount || link.To >= rowCount) continue;
                Rect from = _rowRects[link.From];
                Rect to = _rowRects[link.To];
                float lane = from.xMax - 8f - 9f * k;
                float start = lane - 12f;
                float y1 = from.y + from.height * (link.FromBlock ? 0.3f : 0.6f);
                float y2 = to.y + to.height * 0.8f;
                if (link.From == link.To) y2 = from.y + from.height * 0.85f;
                Color color = link.FromBlock ? new Color(0.45f, 0.9f, 0.95f, 1f) : new Color(1f, 0.65f, 0.25f, 1f);
                UiTheme.Fill(new Rect(start, y1 - 1f, lane - start + 2f, 2f), color);
                UiTheme.Fill(new Rect(lane, Mathf.Min(y1, y2), 2f, Mathf.Abs(y2 - y1)), color);
                UiTheme.Fill(new Rect(start, y2 - 1f, lane - start + 2f, 2f), color);
                UiTheme.Fill(new Rect(start - 4f, y2 - 3f, 4f, 6f), color);
            }
        }

        // ------------------------------------------------------------------ Module, Runen, Rezeptbuch

        private void DrawRightColumn(float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(SideWidth), GUILayout.Height(height));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_showRecipes, UiTexts.Build.ModulesAndRunes, GUI.skin.button, GUILayout.Height(26f))) _showRecipes = false;
            if (GUILayout.Toggle(_showRecipes, UiTexts.Build.RecipeBook, GUI.skin.button, GUILayout.Height(26f))) _showRecipes = true;
            GUILayout.EndHorizontal();

            if (_showRecipes)
            {
                DrawRecipeBook(height - 34f);
            }
            else
            {
                DrawModuleColumn((height - 40f) * 0.5f);
                DrawRuneColumn((height - 40f) * 0.5f);
            }
            GUILayout.EndVertical();
        }

        private void DrawModuleColumn(float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Height(height));
            IReadOnlyList<ModuleInstance> all = _session.Modules.All;
            GUILayout.Label($"{UiTexts.Build.ModulesTitle}  <color=#9aa4b2>{UiTexts.Build.FreeOf(_session.Modules.Free.Count, all.Count)}</color>", UiTheme.Text);
            _moduleScroll = GUILayout.BeginScrollView(_moduleScroll);
            if (all.Count == 0) GUILayout.Label($"<color=#888888>{UiTexts.Build.NoModules}</color>", UiTheme.Small);
            foreach (ModuleInstance m in all.OrderBy(x => x.IsFree ? 0 : 1))
            {
                ModuleDefinition d = _session.ModuleDefinitionOf(m);
                string name = m.NameFrom(_session.ModuleCatalog);
                string where = m.IsFree ? $"<color={UiTheme.Hex(UiTheme.Good)}>{UiTexts.Free}</color>" : $"<color=#9aa4b2>{_session.ModuleWhere(m)}</color>";
                string kind = d != null ? ModuleText.KindName(d.Kind) : "?";
                GUILayout.Box(new GUIContent($"<b>{name}</b> <color=#9aa4b2>[{kind}]</color>  {where}", d?.DescriptionAt(m.Level)),
                    m.IsFree ? UiTheme.Cell : UiTheme.EmptyCell, GUILayout.Height(26f));
                Rect r = GUILayoutUtility.GetLastRect();
                int id = m.InstanceId;
                _drag.Source(r, new DragItem(DragKind.Module, id, name), shortcut: () => ModuleShortcut(id));
                Hover(r, $"<b>{name}</b> [{kind}]: {d?.DescriptionAt(m.Level)}");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.Module && _session.Modules.Get(d.A)?.IsFree == false, d => _session.TakeOffModule(d.A));
        }

        /// <summary>Kurzweg: eingesetztes Modul abnehmen, freies auf den ersten passenden freien Platz (Baustein vor Skill, oben zuerst).</summary>
        private void ModuleShortcut(int moduleId)
        {
            ModuleInstance m = _session.Modules.Get(moduleId);
            if (m == null) return;
            if (!m.IsFree)
            {
                _session.TakeOffModule(moduleId);
                return;
            }
            IReadOnlyList<RuneSlot> rows = _session.Runes.Rows;
            for (int i = 0; i < rows.Count; i++)
            {
                if (_session.CanPlaceModule(m, rows[i])) { _session.PlaceModuleOnRow(moduleId, i); return; }
                SkillInstance skill = rows[i].Skill;
                if (skill != null && !skill.IsBasicAttack && _session.CanPlaceModule(m, skill)) { _session.PlaceModuleOnSkill(moduleId, skill.InstanceId); return; }
            }
        }

        private void DrawRuneColumn(float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Height(height));
            IReadOnlyList<StoredRune> stored = _session.RuneInventory.Runes;
            GUILayout.Label($"{UiTexts.Build.RuneInventoryTitle}  <color=#9aa4b2>{stored.Count}/{_session.RuneInventory.Capacity}</color>", UiTheme.Text);
            _runeScroll = GUILayout.BeginScrollView(_runeScroll);
            if (stored.Count == 0) GUILayout.Label($"<color=#888888>{UiTexts.Build.RuneInventoryEmpty}</color>", UiTheme.Small);
            for (int i = 0; i < stored.Count; i++)
            {
                StoredRune rune = stored[i];
                string growth = rune.Growth > 0 ? $" <color=#b5e48c>+{rune.Growth}</color>" : string.Empty;
                GUILayout.Box(new GUIContent($"{RuneText.Difficulty(rune.Rune.Difficulty)} <b>{rune.Name}</b>{RuneText.LevelBadge(rune.Rune, rune.Level)}{growth}",
                    $"{rune.Description}\n{RuneText.DifficultyTip(rune.Rune)}"), UiTheme.Cell, GUILayout.Height(26f));
                Rect r = GUILayoutUtility.GetLastRect();
                int index = i;
                _drag.Source(r, new DragItem(DragKind.Rune, index, rune.Name), shortcut: () => _session.EquipRuneFromInventory(index));
                Hover(r, $"<b>{rune.Name}</b>: {rune.Description} {rune.Rune.LevelText(rune.Level)} · {DifficultyText.Tooltip(rune.Rune.Difficulty)}");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.RowRune && !_session.RuneInventory.IsFull, d => _session.UnequipRune(d.A));
        }

        /// <summary>Rezeptbuch: Duos und Evolutionen, unentdeckte als Silhouette «???» mit Hinweis, entdeckte mit Rezept.</summary>
        private void DrawRecipeBook(float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Height(height));
            GUILayout.Label($"{UiTexts.Build.RecipeBookTitle}  <color=#9aa4b2>{UiTexts.Build.RecipeBookLegend}</color>", UiTheme.Text);
            _recipeScroll = GUILayout.BeginScrollView(_recipeScroll);
            foreach (EvolutionRecipe recipe in _session.EvolutionCatalog.All)
            {
                bool known = _session.RecipeBook.HasEvolution(recipe.Id);
                string head = UiTexts.Build.Evolution(_session.EvolutionName(recipe));
                string text = known ? $"{head}: {_session.RecipeText(recipe)}" : $"{head}\n<color=#888888>{recipe.Hint}</color>";
                GUILayout.Label($"<color=#d29bff>{text}</color>\n<color=#9aa4b2>{_session.EvolutionProgress(recipe)}</color>", UiTheme.Small);
                GUILayout.Space(4f);
            }
            SynergyRegistry registry = _session.Synergies;
            foreach (SynergyDuo duo in registry.Duos)
            {
                bool known = _session.IsDuoDiscovered(duo.Id);
                bool active = registry.IsDuoActive(duo, _session.Gear);
                string text = known ? UiTexts.Build.Duo(duo.Name, duo.Effect.Text) : $"{UiTexts.Build.UnknownDuo}\n<color=#888888>{_session.DuoHint(duo)}</color>";
                GUILayout.Label(active ? $"<color={UiTheme.Hex(UiTheme.Good)}>{text}</color>" : text, UiTheme.Small);
                GUILayout.Space(4f);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ Texte

        private SkillInstance RowSkill(int row) => row >= 0 && row < _session.Runes.Rows.Count ? _session.Runes.Rows[row].Skill : null;

        /// <summary>Kurzwerte für eine Zeile: erste Wirkung und Cooldown, z. B. «120 % ≈ 4 · CD 4 s».</summary>
        private static string ShortStats(SkillInfo info)
        {
            if (info == null) return string.Empty;
            string effect = info.Effects.Count > 0 ? info.Effects[0].Text : UiTexts.Build.NoEffect;
            string cd = info.Skill.IsBasicAttack ? string.Empty : info.CooldownTicks > 0 ? UiTexts.Build.Cooldown(SkillInfo.Seconds(info.CooldownTicks)) : UiTexts.Build.NoCooldown;
            return effect + cd;
        }

        private string SkillTip(SkillInstance skill, SkillInfo info)
        {
            var lines = new List<string>();
            if (info != null) lines.Add(info.Details);
            GrowthRule rule = _session.SkillGrowthRule(skill);
            if (rule != null)
            {
                string effect = _session.GrowthEffectText(rule, skill.Growth);
                lines.Add(UiTexts.Build.GrowsNow(rule.Text, effect, _session.MilestoneText(skill.Growth, true)));
            }
            if (!skill.IsBasicAttack) lines.Add(UiTexts.Build.ModuleSlots(skill.Modules.Count, skill.ModuleSlots));
            foreach (string evolution in _session.EvolutionHintsForSkill(skill.SkillId)) lines.Add(evolution);
            return string.Join("\n", lines);
        }

        private string RowTip(RuneSlot row)
        {
            var lines = new List<string> { $"{row.Name}: {row.Description}" };
            string level = row.Rune.LevelText(row.Level);
            if (level.Length > 0) lines.Add(level);
            GrowthRule rule = _session.RowGrowthRule(row);
            if (rule != null)
            {
                string effect = _session.GrowthEffectText(rule, row.Growth, row);
                lines.Add(UiTexts.Build.GrowsRow(rule.Text, effect, _session.MilestoneText(row.Growth, false)));
            }
            lines.Add(RuneText.DifficultyTip(row.Rune, _session.RowDifficulty(row) != row.Rune.Difficulty));
            lines.Add(UiTexts.Build.ModuleSlots(row.Modules.Count, row.ModuleSlots));
            lines.AddRange(_session.EvolutionProgressFor(row));
            lines.Add(UiTexts.Build.RowDragHint);
            return string.Join("\n", lines);
        }

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";
    }
}
