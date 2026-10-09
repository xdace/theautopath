using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
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
    /// Fenster «Build» (Taste B), A-19: in der Mitte die Platine als Raster (Kern, Relais-Chips, Komponenten in ihrer Form),
    /// darunter die Liste aller Komponenten und Relais mit Grösse, Cast-Zeit, Wirkung, Versorgung und Modul-Plätzen. Links das
    /// Skill-Inventar, rechts Module und Chips (Runen-Inventar) oder das Rezeptbuch, oben die Stat-Leiste.
    /// Zusammengesteckt wird per Drag &amp; Drop: Skill auf eine Zelle = legen, Komponente oder Relais ziehen = verschieben
    /// (R dreht beim Ziehen), Rechtsklick auf eine Komponente = drehen, zurück in eine Liste = abnehmen. Jede Aktion ruft eine
    /// Methode der Session; während Kampf und offenen Entscheidungen ist das Fenster nur lesbar.
    /// A-20: Pins als Kerben an den Komponenten (typisiert in der Farbe der Art, passend hervorgehoben), Logik-Chips auf dem
    /// Raster und als Leiste unter dem Raster (ziehen = legen/verschieben, Rechtsklick = drehen, zurück auf die Leiste =
    /// abnehmen) und die Pulsverbindungen der kompilierten Platine als Linien.
    /// A-21: Effekt-Symbole (Farbe aus den Daten) an Komponenten, Skills und Modulen, mit «Name: Text» im Tooltip; Verstärker,
    /// Watchdog und Effekt-Chips in der Farbe ihres Effekts; Regeln der ganzen Platine (Overflow, Firewall) unter dem Raster.
    /// </summary>
    public sealed class BuildWindow : MonoBehaviour
    {
        private const float SideWidth = 290f;
        private const float LineHeight = 40f;

        private static readonly Color SelectedColor = UiTheme.Accent;

        private OverworldSession _session;
        private readonly DragDrop _drag = new DragDrop();
        private readonly StatBar _statBar = new StatBar();
        private Vector2 _skillScroll, _partsScroll, _moduleScroll, _runeScroll, _recipeScroll;
        private bool _showRecipes;
        private SkillUserStats _skillStats;
        private int _skillStatsFrame = -1;
        private readonly Dictionary<ComponentSlot, SkillInfo> _infos = new Dictionary<ComponentSlot, SkillInfo>();

        // Was die Maus gerade überfährt (für die Vorschau), im Repaint gesammelt und im nächsten Frame gezeigt.
        private string _hover;
        private string _hoverNext;
        private object _hoverPart;
        private object _hoverPartNext;

        // Ausgewähltes Teil (Komponente oder Relais), hervorgehoben in Raster und Liste.
        private object _selected;

        // Verknüpfen: Id des Trigger- bzw. Charge-Link-Moduls, dessen Ziel als Nächstes auf der Platine gewählt wird (-1 = aus).
        private int _linking = -1;

        // Verwerfen: was in den Papierkorb gezogen wurde und auf «Ja» wartet.
        private DragItem? _pendingDiscard;

        // Ziehen auf der Platine: welche Zelle der Komponente gepackt wurde, und ob beim Ziehen gedreht wird (Taste R).
        private int _grabX, _grabY;
        private bool _dragRotated;

        // Logik-Chips (A-20): Vierteldrehungen beim Ziehen (Taste R) und die kompilierte Platine für Pins und Pulsverbindungen.
        private int _dragTurns;
        private LogicBoard _compiled;

        public bool IsOpen { get; private set; }

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _selected = null;
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
            _hoverPart = null;
        }

        public void Close()
        {
            IsOpen = false;
            _drag.Cancel();
            _dragRotated = false;
            _dragTurns = 0;
        }

        private CircuitBoard Board => _session.Board;

        private void OnGUI()
        {
            if (!IsOpen || _session == null) return;
            UiTheme.Apply();
            GUI.depth = -5;
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && _linking >= 0)
            {
                _linking = -1;
                e.Use();
                return;
            }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
                e.Use();
                return;
            }
            if (!_drag.IsDragging)
            {
                _dragRotated = false;
                _dragTurns = 0;
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
            {
                _dragRotated = !_dragRotated;
                _dragTurns = (_dragTurns + 1) % 4;
                e.Use();
            }

            _drag.Enabled = _session.CanChangeLoadout;
            // Werte des Ritters und der Komponenten einmal pro Frame, nicht pro OnGUI-Ereignis.
            if (_skillStatsFrame != Time.frameCount || _skillStats == null)
            {
                _skillStats = _session.SkillUserStats();
                _skillStatsFrame = Time.frameCount;
                _infos.Clear();
                foreach (ComponentSlot c in Board.Components) _infos[c] = _session.DescribeComponentSkill(c, _skillStats);
                if (_selected is ComponentSlot sc && Board.IndexOf(sc) < 0) _selected = null;
                if (_selected is RelayChip sr && Board.IndexOf(sr) < 0) _selected = null;
                if (_selected is BoardChip sb && Board.IndexOf(sb) < 0) _selected = null;
                _compiled = _session.CompileBoard();
            }
            if (e.type == EventType.Repaint)
            {
                _hoverNext = null;
                _hoverPartNext = null;
            }

            Rect area = OverworldHud.BelowTopBar(1560f, 880f);
            float width = area.width;
            float height = area.height;
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

            GUILayout.Label($"<color=#9aa4b2>{UiTexts.Build.Hint}</color>", UiTheme.Small);
            GUILayout.EndArea();

            if (e.type == EventType.Repaint)
            {
                _hover = _hoverNext;
                _hoverPart = _hoverPartNext;
            }
            if (!_drag.IsDragging) UiTheme.DrawTooltip();
            _drag.Finish();
        }

        // ------------------------------------------------------------------ Kopf und Vorschau

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            string size = UiTexts.Build.BoardSize(_session.BoardSize, _session.MaxBoardSize, _session.CanExpandBoard);
            GUILayout.Label($"{UiTexts.Build.TitleName}  <size={UiTheme.SmallSize}><color=#9aa4b2>{size} · {UiTexts.Build.FiresRule}</color></size>", UiTheme.Title);
            GUILayout.FlexibleSpace();
            if (!_session.CanChangeLoadout)
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Bad)}>{UiTexts.ReadOnly}</color>", UiTheme.Small, GUILayout.ExpandWidth(false));
            DrawTrash();
            if (GUILayout.Button(new GUIContent(UiTexts.Close, UiTexts.Build.CloseTip), GUILayout.Width(34f), GUILayout.Height(28f))) Close();
            GUILayout.EndHorizontal();
        }

        private void DrawPreviewLine()
        {
            string text = _hover;
            ModuleInstance linking = _linking >= 0 ? _session.Modules.Get(_linking) : null;
            if (linking == null || linking.IsFree) _linking = -1;
            if (_drag.IsDragging)
            {
                string label = _drag.Dragging.Value.Label;
                text = IsChipDrag(_drag.Dragging.Value) ? UiTexts.Build.DraggingTurned(label, _dragTurns * 90)
                    : _dragRotated ? UiTexts.Build.DraggingRotated(label) : UiTexts.Build.Dragging(label);
            }
            GUILayout.Label(string.IsNullOrEmpty(text) ? $"<color=#666c78>{UiTexts.Build.HoverHint}</color>" : text,
                UiTheme.SmallLine, GUILayout.Height(20f));
        }

        private void Hover(Rect rect, string text, object part = null)
        {
            if (Event.current.type != EventType.Repaint || !rect.Contains(Event.current.mousePosition)) return;
            _hoverNext = text;
            if (part != null) _hoverPartNext = part;
        }

        // ------------------------------------------------------------------ Skill-Inventar

        private void DrawSkillColumn(float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(SideWidth), GUILayout.Height(height));
            IReadOnlyList<SkillInstance> all = _session.Skills.All;
            GUILayout.Label($"{UiTexts.Build.SkillsTitle}  <color=#9aa4b2>{UiTexts.Build.FreeOf(_session.Skills.Free.Count, all.Count)}</color>", UiTheme.Text);
            DrawBasicAttackCard();

            // Inventar als Raster wie die Platine: jede freie Komponente liegt in ihrer echten Form darin (kleinere Felder).
            List<SkillInstance> free = all.Where(x => x.IsFree).ToList();
            List<SkillInventoryLayout.Placement> placed = SkillInventoryLayout.Pack(
                free.Select(x => (x.InstanceId, Board.ShapeOfSkill(x.SkillId))), out int rows);
            float size = Mathf.Floor((SideWidth - 34f) / SkillInventoryLayout.Columns);

            _skillScroll = GUILayout.BeginScrollView(_skillScroll);
            Rect grid = GUILayoutUtility.GetRect(size * SkillInventoryLayout.Columns, size * rows,
                GUILayout.Width(size * SkillInventoryLayout.Columns), GUILayout.Height(size * rows));
            CircuitGrid.DrawBackground(grid, size, SkillInventoryLayout.Columns, rows);
            foreach (SkillInventoryLayout.Placement p in placed) DrawInventorySkill(grid, size, _session.Skills.Get(p.Id), p);
            if (free.Count == 0)
                GUI.Label(new Rect(grid.x + 6f, grid.y + 6f, grid.width - 12f, 40f),
                    $"<color=#888888>{(all.Count == 0 ? UiTexts.Build.NoSkills : UiTexts.Build.AllSkillsOnBoard)}</color>", UiTheme.Small);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Komponente hierher = von der Platine nehmen.
            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.Component && d.A >= 0 && d.A < Board.Components.Count, d => _session.RemoveComponent(d.A));
        }

        /// <summary>Eine freie Komponente im Inventar-Raster: Form, Farbe nach Skill-Art, Name, Grösse; ziehbar auf die Platine.</summary>
        private void DrawInventorySkill(Rect grid, float size, SkillInstance skill, SkillInventoryLayout.Placement p)
        {
            if (skill == null) return;
            Rect r = CircuitGrid.RectOf(grid, size, p.Rect);
            SkillInfo info = _session.DescribeSkill(skill, _skillStats);
            string name = skill.NameFrom(_session.SkillCatalog);
            Color kind = _session.SkillCatalog.TryGet(skill.SkillId, out SkillDefinition def) ? KindColorOf(def.Kinds) : CircuitGrid.PinColor;
            Shape shape = Board.ShapeOfSkill(skill.SkillId);
            string modules = skill.Modules.Count > 0 ? $" <color=#ffd75e>●{skill.Modules.Count}</color>" : string.Empty;
            string text = p.Rect.Shape.Cells == 1
                ? $"<b>{Abbreviate(name)}</b>"
                : $"<b>{name}</b>{modules}\n<color=#9aa4b2>{shape.Width}×{shape.Height}</color>";
            bool over = r.Contains(Event.current.mousePosition);
            Color fill = Color.Lerp(CircuitGrid.ComponentColor, kind, over ? 0.32f : 0.18f);
            CircuitGrid.DrawChip(r, fill, kind, over ? 2f : 1f, text, SkillTip(skill, info, null), CircuitGrid.Tiny);

            int id = skill.InstanceId;
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition)) _grabX = _grabY = 0;
            _drag.Source(r, new DragItem(DragKind.Skill, id, name), click: () => _selected = Board.ComponentOf(_session.Skills.Get(id)),
                shortcut: () => SkillShortcut(id));
            Hover(r, $"<b>{name}</b> ({shape.Width}×{shape.Height}): {Effect(info)} · {info?.TimingText} <color=#9aa4b2>{UiTexts.Build.SkillsKeepBar}</color>");
        }

        /// <summary>Farbe der ersten Skill-Art (Angriff, Schild, Feuer …).</summary>
        private static Color KindColorOf(SkillKind kinds) => kinds == SkillKind.None ? CircuitGrid.PinColor : UiTheme.Kind(kinds);

        /// <summary>Kurzname für 1×1-Felder: «Shock Stab» → «ShSt», einzelne Wörter bis 5 Zeichen.</summary>
        private static string Abbreviate(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string[] words = name.Split(' ');
            if (words.Length == 1) return name.Length <= 5 ? name : name.Substring(0, 5);
            return string.Concat(words.Take(3).Select(w => w.Length <= 2 ? w : w.Substring(0, 2)));
        }

        private void DrawBasicAttackCard()
        {
            const string label = UiTexts.BasicAttack;
            SkillInfo info = _session.DescribeSkill(SkillInstance.BasicAttack(), _skillStats);
            GUILayout.Box(new GUIContent($"<b>{label}</b>  <color=#9aa4b2>{UiTexts.Build.AlwaysAvailable}</color>", UiTexts.Build.BasicAttackTip),
                UiTheme.EmptyCell, GUILayout.Height(26f));
            Rect r = GUILayoutUtility.GetLastRect();
            Hover(r, $"<b>{label}</b>: {Effect(info)} · {info?.TimingText}");
        }

        /// <summary>Kurzweg: freier Skill auf die beste freie Lage (an einem passenden Relais), gelegter zurück in die Sammlung.</summary>
        private void SkillShortcut(int instanceId)
        {
            SkillInstance skill = _session.Skills.Get(instanceId);
            if (skill == null) return;
            ComponentSlot slot = Board.ComponentOf(skill);
            if (slot != null)
            {
                _session.RemoveComponent(Board.IndexOf(slot));
                return;
            }
            if (_session.BestSpotFor(skill, out Cell origin, out bool rotated)) _session.PlaceSkill(instanceId, origin, rotated);
        }

        // ------------------------------------------------------------------ Platine

        private void DrawBoardColumn(float width, float height)
        {
            GUILayout.BeginVertical(UiTheme.Section, GUILayout.Width(width), GUILayout.Height(height));
            DrawLinkBanner();
            GUILayout.Label($"{UiTexts.Build.BoardTitle}  <color=#9aa4b2>{_session.BoardSize} · {UiTexts.Build.BoardLegend}</color>", UiTheme.Text);
            Hover(GUILayoutUtility.GetLastRect(), BoardRule());

            float inner = width - 24f;
            float size = CircuitGrid.CellSize(Board.Width, Board.Height, inner, Mathf.Max(150f, height * 0.56f));
            Rect area = GUILayoutUtility.GetRect(inner, Board.Height * size + 4f, GUILayout.ExpandWidth(true));
            var grid = new Rect(area.x + (area.width - Board.Width * size) * 0.5f, area.y + 2f, Board.Width * size, Board.Height * size);
            DrawGrid(grid, size);

            SkillInfo basic = _session.DescribeSkill(SkillInstance.BasicAttack(), _skillStats);
            GUILayout.Label($"<color=#9aa4b2>↓ {UiTexts.FallbackLine} · {Effect(basic)}</color>", UiTheme.SmallLine);
            string rules = EffectText.BoardRules(_compiled);
            if (rules.Length > 0)
            {
                GUILayout.Label(new GUIContent(UiTexts.Effects.Board(rules), EffectText.BoardRulesTip(_compiled)), UiTheme.SmallLine);
                Hover(GUILayoutUtility.GetLastRect(), EffectText.BoardRulesTip(_compiled));
            }
            DrawChipStrip(inner);

            _partsScroll = GUILayout.BeginScrollView(_partsScroll);
            DrawDetailPanel();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        /// <summary>Verknüpfen-Modus als gelbes Banner mit Abbrechen-Knopf: Ziel auf der Platine anklicken.</summary>
        private void DrawLinkBanner()
        {
            ModuleInstance linking = _linking >= 0 ? _session.Modules.Get(_linking) : null;
            if (linking == null || linking.IsFree) return;
            GUILayout.BeginHorizontal(UiTheme.CellSelected);
            Rect r = GUILayoutUtility.GetRect(10f, 30f, GUILayout.ExpandWidth(true));
            UiTheme.Fill(r, new Color(0.45f, 0.36f, 0.08f, 0.95f));
            GUI.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height),
                $"<b><color=#ffe9a8>{UiTexts.Build.LinkingHint(linking.NameFrom(_session.ModuleCatalog), _session.ModuleWhere(linking))}</color></b>", UiTheme.SmallLine);
            if (GUILayout.Button(UiTexts.Build.CancelLink, GUILayout.Width(120f), GUILayout.Height(30f))) _linking = -1;
            GUILayout.EndHorizontal();
        }

        // ------------------------------------------------------------------ Detailpanel für das gewählte Teil

        /// <summary>
        /// Alles zum gewählten Teil an einer Stelle: Werte, Versorgung, Kern, Pulse, Effekte, Wachstum, Modul-Plätze (ziehen,
        /// ablegen, ×) und Knöpfe. Ersetzt die Listen unter der Platine.
        /// </summary>
        private void DrawDetailPanel()
        {
            switch (_selected)
            {
                case ComponentSlot c when Board.IndexOf(c) >= 0: DrawComponentDetail(c, Board.IndexOf(c)); break;
                case RelayChip relay when Board.IndexOf(relay) >= 0: DrawRelayDetail(relay, Board.IndexOf(relay)); break;
                case BoardChip chip when Board.IndexOf(chip) >= 0:
                    GUILayout.BeginVertical(UiTheme.Cell);
                    GUILayout.Label($"<b>{chip.Name}</b>", UiTheme.Text);
                    GUILayout.Label(ChipTip(chip, GateOf(chip)), UiTheme.Small);
                    GUILayout.BeginHorizontal();
                    GUI.enabled = _session.CanChangeLoadout;
                    if (GUILayout.Button(UiTexts.Build.Rotate, GUILayout.Height(26f))) _session.RotateChip(Board.IndexOf(chip));
                    if (GUILayout.Button(UiTexts.Build.TakeBack, GUILayout.Height(26f))) _session.RemoveChip(Board.IndexOf(chip));
                    GUI.enabled = true;
                    if (GUILayout.Button(UiTexts.Build.Deselect, GUILayout.Height(26f))) _selected = null;
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    break;
                default:
                    GUILayout.Label($"<color=#9aa4b2>{UiTexts.Build.DetailEmpty}</color>", UiTheme.Small);
                    break;
            }
        }

        private void DrawComponentDetail(ComponentSlot c, int index)
        {
            _infos.TryGetValue(c, out SkillInfo info);
            string name = ComponentName(c);
            Color kind = _session.SkillCatalog.TryGet(c.Skill.SkillId, out SkillDefinition def) ? KindColorOf(def.Kinds) : UiTheme.Neutral;
            GUILayout.BeginVertical(UiTheme.Cell);
            Rect head = GUILayoutUtility.GetRect(10f, 26f, GUILayout.ExpandWidth(true));
            UiTheme.Fill(new Rect(head.x, head.y + 2f, 4f, head.height - 4f), kind);
            GUI.Label(new Rect(head.x + 10f, head.y, head.width - 10f, head.height),
                $"<b>#{index + 1} {name}</b>  <color=#ffd75e>{ShortStats(info)}</color>  <color={StateHex(c)}>{StateShort(c)}</color>", UiTheme.Text);
            GUILayout.Label(ComponentTip(c, index, info, panel: true), UiTheme.Small);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9aa4b2>{UiTexts.Build.ModuleSlots(c.Skill.Modules.Count, c.Skill.ModuleSlots)}</color>", UiTheme.SmallLine,
                GUILayout.Width(120f), GUILayout.Height(LineHeight - 8f));
            DrawModuleSlots(c.Skill, id => _session.PlaceModuleOnSkill(id, c.Skill.InstanceId));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _session.CanChangeLoadout;
            if (GUILayout.Button(UiTexts.Build.Rotate, GUILayout.Height(26f))) _session.RotateComponent(index);
            if (GUILayout.Button(UiTexts.Build.TakeBack, GUILayout.Height(26f))) { _session.RemoveComponent(index); _selected = null; }
            GUI.enabled = true;
            if (GUILayout.Button(UiTexts.Build.Deselect, GUILayout.Height(26f))) _selected = null;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void DrawRelayDetail(RelayChip relay, int index)
        {
            int difficulty = _session.RelayDifficulty(relay);
            GUILayout.BeginVertical(UiTheme.Cell);
            string growth = relay.Growth > 0 ? $" <color=#b5e48c>+{relay.Growth}</color>" : string.Empty;
            GUILayout.Label($"{RuneText.Difficulty(difficulty)} <b>{ArenaTexts.RelayName(index, relay.Name)}</b>{RuneText.LevelBadge(relay.Rune, relay.Level)}{growth}", UiTheme.Text);
            GUILayout.Label(RelayTip(relay, index, panel: true), UiTheme.Small);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<color=#9aa4b2>{UiTexts.Build.ModuleSlots(relay.Modules.Count, relay.ModuleSlots)}</color>", UiTheme.SmallLine,
                GUILayout.Width(120f), GUILayout.Height(LineHeight - 8f));
            DrawModuleSlots(relay, id => _session.PlaceModuleOnRelay(id, Board.IndexOf(relay)));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUI.enabled = _session.CanChangeLoadout;
            if (GUILayout.Button(UiTexts.Build.TakeBack, GUILayout.Height(26f))) { _session.UnequipRune(index); _selected = null; }
            GUI.enabled = true;
            if (GUILayout.Button(UiTexts.Build.Deselect, GUILayout.Height(26f))) _selected = null;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            // Rune aus dem Inventar auf das Panel = Rune dieses Relais tauschen.
            if (_drag.IsDragging && _drag.Dragging.Value.Kind == DragKind.Rune)
                _drag.Target(GUILayoutUtility.GetLastRect(), d => d.Kind == DragKind.Rune, d => _session.SwapRune(index, d.A));
        }

        private string BoardRule()
        {
            var limits = new List<string>();
            for (int tier = 0; tier <= 3; tier++)
                limits.Add(UiTexts.Build.Limit(DifficultyText.Symbol(tier), _session.DifficultyBonus.MaxCells(tier)));
            return UiTexts.Build.BoardRule(string.Join(", ", limits), Board.Config.CoreBonusPercent, QueueConfig.RuleText);
        }

        private void DrawGrid(Rect grid, float size)
        {
            Event e = Event.current;
            CircuitGrid.DrawBackground(grid, size, Board.Width, Board.Height);

            // Leiterbahnen: Relais → berührte Komponenten (türkis versorgt, orange zu gross), Kern → berührte Komponenten (violett).
            foreach (RelayChip relay in Board.Relays)
            {
                Rect from = CircuitGrid.RectOf(grid, size, relay.Rect);
                int max = _session.RelayMaxCells(relay);
                foreach (ComponentSlot c in Board.ComponentsTouching(relay))
                    CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, c.Rect), c.Cells <= max ? CircuitGrid.TraceColor : CircuitGrid.TooLargeBorder);
            }
            Rect core = CircuitGrid.RectOf(grid, size, Board.CoreRect);
            foreach (ComponentSlot c in Board.Components)
                if (Board.TouchesCore(c)) CircuitGrid.DrawTrace(core, CircuitGrid.RectOf(grid, size, c.Rect), CircuitGrid.CoreColor);

            int percent = Board.Config.CoreBonusPercent;
            CircuitGrid.DrawCore(core, $"{UiTexts.Build.CoreName}\n+{percent} %", UiTexts.Build.CoreTip(percent));
            Hover(core, UiTexts.Build.CoreTip(percent));

            // A-20: Gatter-Leitungen, Logik-Chips und Pulsverbindungen liegen unter den Komponenten.
            DrawGateTraces(grid, size);
            for (int i = 0; i < Board.Chips.Count; i++) DrawBoardChip(grid, size, i);
            DrawPulseLinks(grid, size);

            List<OverworldSession.TriggerLink> links = _session.TriggerLinks();
            for (int i = 0; i < Board.Components.Count; i++) DrawComponentChip(grid, size, i, links);
            for (int i = 0; i < Board.Relays.Count; i++) DrawRelayChip(grid, size, i, links);
            DrawPins(grid, size);

            // Vorschau beim Ziehen: Fussabdruck an der Maus (grün passt, rot nicht).
            if (_drag.IsDragging && e.type == EventType.Repaint
                && CircuitGrid.CellAt(grid, size, Board.Width, Board.Height, e.mousePosition, out Cell over)
                && Footprint(_drag.Dragging.Value, over, out CellRect footprint, out bool fits))
            {
                Rect r = CircuitGrid.RectOf(grid, size, footprint);
                Color c = fits ? UiTheme.Good : UiTheme.Bad;
                UiTheme.Fill(r, new Color(c.r, c.g, c.b, 0.25f));
                UiTheme.Outline(r, c, 2f);
            }

            // Ziele: jede Zelle.
            for (int y = 0; y < Board.Height; y++)
                for (int x = 0; x < Board.Width; x++)
                {
                    var cell = new Cell(x, y);
                    _drag.Target(CircuitGrid.SlotOf(grid, size, x, y), d => AcceptsAt(d, cell), d => DropAt(d, cell));
                }

            // Freie Zellen: Hinweis beim Überfahren.
            if (e.type == EventType.Repaint && CircuitGrid.CellAt(grid, size, Board.Width, Board.Height, e.mousePosition, out Cell hovered)
                && Board.At(hovered) == null && !Board.IsCore(hovered))
                _hoverNext = UiTexts.Build.EmptyCellTip;
        }

        private void DrawComponentChip(Rect grid, float size, int index, List<OverworldSession.TriggerLink> links)
        {
            ComponentSlot c = Board.Components[index];
            Rect rect = CircuitGrid.RectOf(grid, size, c.Rect);
            _infos.TryGetValue(c, out SkillInfo info);
            string name = ComponentName(c);
            bool focus = c == _selected || c == _hoverPart;

            var marks = new List<string>();
            if (Board.TouchesCore(c)) marks.Add($"<color=#b18cff>+{Board.Config.CoreBonusPercent} %</color>");
            foreach (OverworldSession.TriggerLink link in links)
                if (!link.FromBlock && link.From == index) marks.Add($"<color=#ffae42>{(link.Charges ? UiTexts.Build.ChargesTo(link.To + 1) : UiTexts.Build.TriggersTo(link.To + 1))}</color>");
            if (_session.IsEvolutionReady(c.Skill)) marks.Add("<color=#d29bff>^</color>");

            string cast = info != null ? SkillInfo.Seconds(info.WindupTicks) : "?";
            string text = $"<b>#{index + 1} {name}</b>\n{c.Shape} · {cast}\n<color={StateHex(c)}>{StateShort(c)}</color>";
            if (marks.Count > 0) text += "\n" + string.Join(" ", marks);
            Color fill = focus ? Color.Lerp(CircuitGrid.ComponentColor, UiTheme.CellHover, 0.8f) : CircuitGrid.ComponentColor;
            Color border = c == _selected ? SelectedColor : StateColor(c);
            CircuitGrid.DrawChip(rect, fill, border, focus ? 3f : 2f, text, ComponentTip(c, index, info), size < 60f ? CircuitGrid.Tiny : CircuitGrid.Label);
            // Skill-Art als farbige linke Kante (gleiche Farbe wie im Inventar und in der Arena).
            if (_session.SkillCatalog.TryGet(c.Skill.SkillId, out SkillDefinition kindDef))
                UiTheme.Fill(new Rect(rect.x + 3f, rect.y + 3f, 4f, rect.height - 6f), KindColorOf(kindDef.Kinds));
            CircuitGrid.DrawEffectBadges(rect, EffectText.Of(CompiledRow(index)), size);
            DrawModuleBadges(rect, size, c.Skill);

            // Gepackte Zelle merken, damit die Komponente beim Verschieben unter der Maus bleibt.
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition)
                && CircuitGrid.CellAt(grid, size, Board.Width, Board.Height, e.mousePosition, out Cell grabbed))
            {
                _grabX = grabbed.X - c.Origin.X;
                _grabY = grabbed.Y - c.Origin.Y;
            }
            if (_linking >= 0) UiTheme.Outline(rect, new Color(1f, 0.84f, 0.37f, 0.9f), 2f);
            _drag.Source(rect, new DragItem(DragKind.Component, index, $"#{index + 1} {name}"),
                click: () => ClickComponent(c), shortcut: () => _session.RotateComponent(Board.IndexOf(c)));
            Hover(rect, $"<b>#{index + 1} {name}</b>: {ShortStats(info)} · <color={StateHex(c)}>{StateLong(c)}</color>", c);
        }

        private void DrawRelayChip(Rect grid, float size, int index, List<OverworldSession.TriggerLink> links)
        {
            RelayChip relay = Board.Relays[index];
            Rect rect = CircuitGrid.RectOf(grid, size, relay.Rect);
            bool focus = relay == _selected || relay == _hoverPart;
            int max = _session.RelayMaxCells(relay);
            int difficulty = _session.RelayDifficulty(relay);

            string text = $"{RuneText.Difficulty(difficulty)}\n<b>{relay.Name}</b>\n<color=#9aa4b2>{UiTexts.Build.MaxCells(max)}</color>";
            var marks = new List<string>();
            foreach (OverworldSession.TriggerLink link in links)
                if (link.FromBlock && link.From == index) marks.Add($"<color=#45e6f2>{(link.Charges ? UiTexts.Build.ChargesTo(link.To + 1) : UiTexts.Build.TriggersTo(link.To + 1))}</color>");
            if (_session.IsEvolutionReady(relay)) marks.Add("<color=#d29bff>^</color>");
            if (marks.Count > 0) text += " " + string.Join(" ", marks);

            Color fill = focus ? Color.Lerp(CircuitGrid.RelayColor, CircuitGrid.RelayLit, 0.25f) : CircuitGrid.RelayColor;
            Color border = relay == _selected ? SelectedColor : CircuitGrid.RelayLit;
            CircuitGrid.DrawChip(rect, fill, border, focus ? 3f : 1f, text, RelayTip(relay, index), size < 60f ? CircuitGrid.Tiny : CircuitGrid.Label);
            DrawModuleBadges(rect, size, relay);

            _drag.Source(rect, new DragItem(DragKind.Relay, index, relay.Name),
                click: () => _selected = relay, shortcut: () => _session.UnequipRune(Board.IndexOf(relay)));
            Hover(rect, $"<b>{relay.Name}</b>: {relay.Description} · {UiTexts.Build.RelayLimit(max)} · {PowersText(relay)}", relay);
        }

        // ------------------------------------------------------------------ Drag & Drop auf die Platine

        /// <summary>Wo das Gezogene landen würde, wenn es über <paramref name="cell"/> losgelassen wird.</summary>
        private bool Footprint(DragItem d, Cell cell, out CellRect rect, out bool fits)
        {
            rect = default;
            fits = false;
            switch (d.Kind)
            {
                case DragKind.Skill:
                {
                    SkillInstance skill = _session.Skills.Get(d.A);
                    if (skill == null) return false;
                    bool rotated = PlacementFor(skill, cell, out bool ok);
                    rect = new CellRect(cell, Board.ShapeOfSkill(skill.SkillId).Turned(rotated));
                    fits = ok;
                    return true;
                }
                case DragKind.Component:
                {
                    if (d.A < 0 || d.A >= Board.Components.Count) return false;
                    ComponentSlot c = Board.Components[d.A];
                    Cell origin = GrabOrigin(cell);
                    bool rotated = MoveFor(c, origin, out bool ok);
                    rect = new CellRect(origin, c.BaseShape.Turned(rotated));
                    fits = ok;
                    return true;
                }
                case DragKind.Relay:
                case DragKind.Rune:
                case DragKind.LogicChip:
                case DragKind.BoardChip:
                    rect = new CellRect(cell, Shape.One);
                    fits = AcceptsAt(d, cell);
                    return true;
                default:
                    return false;
            }
        }

        private Cell GrabOrigin(Cell cell) => new Cell(cell.X - _grabX, cell.Y - _grabY);

        /// <summary>Drehung für einen Skill an dieser Zelle: die gewünschte, sonst die andere, wenn nur sie passt.</summary>
        private bool PlacementFor(SkillInstance skill, Cell at, out bool ok)
        {
            bool wanted = _dragRotated;
            if (_session.CanPlaceSkill(skill.InstanceId, at, wanted))
            {
                ok = true;
                return wanted;
            }
            ok = _session.CanPlaceSkill(skill.InstanceId, at, !wanted);
            return ok ? !wanted : wanted;
        }

        /// <summary>Drehung für eine verschobene Komponente: die bisherige (mit R umgedreht), sonst die andere, wenn nur sie passt.</summary>
        private bool MoveFor(ComponentSlot c, Cell origin, out bool ok)
        {
            bool wanted = c.Rotated ^ _dragRotated;
            if (!_session.CanEditSkills)
            {
                ok = false;
                return wanted;
            }
            if (Board.CanPlace(c.Skill.SkillId, origin, wanted, c))
            {
                ok = true;
                return wanted;
            }
            ok = Board.CanPlace(c.Skill.SkillId, origin, !wanted, c);
            return ok ? !wanted : wanted;
        }

        private IModuleHolder HolderAt(Cell cell)
        {
            object part = Board.At(cell);
            if (part is RelayChip relay) return relay;
            if (part is ComponentSlot c) return c.Skill;
            return null;
        }

        private bool AcceptsAt(DragItem d, Cell cell)
        {
            switch (d.Kind)
            {
                case DragKind.Skill:
                {
                    SkillInstance skill = _session.Skills.Get(d.A);
                    if (skill == null) return false;
                    PlacementFor(skill, cell, out bool ok);
                    return ok;
                }
                case DragKind.Component:
                {
                    if (d.A < 0 || d.A >= Board.Components.Count) return false;
                    MoveFor(Board.Components[d.A], GrabOrigin(cell), out bool ok);
                    return ok;
                }
                case DragKind.Relay:
                    return d.A >= 0 && d.A < Board.Relays.Count && !Board.IsCore(cell)
                        && Board.IsFree(new CellRect(cell, Shape.One), Board.Relays[d.A]);
                case DragKind.Rune:
                    if (d.A < 0 || d.A >= _session.RuneInventory.Count || Board.IsCore(cell)) return false;
                    return Board.At(cell) is RelayChip || Board.IsFree(new CellRect(cell, Shape.One));
                case DragKind.Module:
                {
                    IModuleHolder holder = HolderAt(cell);
                    return holder != null && _session.CanPlaceModule(_session.Modules.Get(d.A), holder);
                }
                case DragKind.LogicChip:
                    return d.A >= 0 && d.A < _session.ChipInventory.Count && _session.CanPlaceChip(cell);
                case DragKind.BoardChip:
                    return _session.CanEditChips && d.A >= 0 && d.A < Board.Chips.Count
                        && Board.IsFree(new CellRect(cell, Shape.One), Board.Chips[d.A]);
                default:
                    return false;
            }
        }

        private void DropAt(DragItem d, Cell cell)
        {
            switch (d.Kind)
            {
                case DragKind.Skill:
                {
                    SkillInstance skill = _session.Skills.Get(d.A);
                    if (skill == null) return;
                    bool rotated = PlacementFor(skill, cell, out bool ok);
                    if (ok && _session.PlaceSkill(d.A, cell, rotated)) _selected = Board.ComponentOf(skill);
                    break;
                }
                case DragKind.Component:
                {
                    if (d.A < 0 || d.A >= Board.Components.Count) return;
                    ComponentSlot c = Board.Components[d.A];
                    Cell origin = GrabOrigin(cell);
                    bool rotated = MoveFor(c, origin, out bool ok);
                    if (ok && _session.MoveComponent(d.A, origin, rotated)) _selected = c;
                    break;
                }
                case DragKind.Relay:
                    if (d.A >= 0 && d.A < Board.Relays.Count)
                    {
                        RelayChip relay = Board.Relays[d.A];
                        if (_session.MoveRelay(d.A, cell)) _selected = relay;
                    }
                    break;
                case DragKind.Rune:
                    if (Board.At(cell) is RelayChip target) _session.SwapRune(Board.IndexOf(target), d.A);
                    else _session.EquipRuneFromInventory(d.A, cell);
                    break;
                case DragKind.Module:
                    object part = Board.At(cell);
                    if (part is RelayChip r) _session.PlaceModuleOnRelay(d.A, Board.IndexOf(r));
                    else if (part is ComponentSlot comp) _session.PlaceModuleOnSkill(d.A, comp.Skill.InstanceId);
                    break;
                case DragKind.LogicChip:
                    if (_session.PlaceChip(d.A, cell, _dragTurns)) _selected = Board.At(cell) as BoardChip;
                    break;
                case DragKind.BoardChip:
                    if (d.A >= 0 && d.A < Board.Chips.Count)
                    {
                        BoardChip chip = Board.Chips[d.A];
                        if (!_session.MoveChip(d.A, cell)) break;
                        // Nach dem Verschieben ändert sich die Lesereihenfolge: Index neu suchen, dann mit R gewählte Drehung anwenden.
                        for (int t = 0; t < _dragTurns; t++) _session.RotateChip(Board.IndexOf(chip));
                        _selected = chip;
                    }
                    break;
            }
        }

        private static bool IsChipDrag(DragItem d) => d.Kind == DragKind.LogicChip || d.Kind == DragKind.BoardChip;

        // ------------------------------------------------------------------ Modul-Plätze (Detailpanel)

        /// <summary>Modul-Plätze eines Relais oder einer Komponente: ● besetzt (ziehbar, Auslöser: Klick wählt das Ziel), ○ frei (Ziel).</summary>
        private void DrawModuleSlots(IModuleHolder holder, System.Action<int> place)
        {
            foreach (ModuleInstance m in holder.Modules)
            {
                GUIContent chip = ModuleText.Chip(_session, m);
                bool trigger = ModuleRules.IsTargeted(m.ModuleId);
                if (trigger) chip.tooltip = chip.tooltip + UiTexts.Build.TriggerClick;
                // A-21: Effekt-Module zeigen ihr Symbol statt ●.
                string mark = EffectText.Icon(m.ModuleId);
                chip.text = $"{(mark.Length > 0 ? mark : "<color=#ffd75e>●</color>")}{Shorten(chip.text, trigger ? 14 : 9)}";
                GUILayout.Label(chip, UiTheme.SmallLine, GUILayout.Width(trigger ? 112f : 76f), GUILayout.Height(LineHeight - 8f));
                Rect r = GUILayoutUtility.GetLastRect();
                int id = m.InstanceId;
                _drag.Source(r, new DragItem(DragKind.Module, id, m.NameFrom(_session.ModuleCatalog)),
                    click: trigger ? () => _linking = id : (System.Action)null,
                    shortcut: () => _session.TakeOffModule(id));
                Hover(r, $"<b>{m.NameFrom(_session.ModuleCatalog)}</b>: {chip.tooltip}");
            }

            for (int i = holder.Modules.Count; i < holder.ModuleSlots; i++)
            {
                string tip = holder is RelayChip ? UiTexts.Build.FreeRuneModuleSlot : UiTexts.Build.FreeSkillModuleSlot;
                GUILayout.Label(new GUIContent("<color=#666c78>○</color>", tip), UiTheme.SmallLine, GUILayout.Width(20f), GUILayout.Height(LineHeight - 8f));
                Rect r = GUILayoutUtility.GetLastRect();
                _drag.Target(r, d => d.Kind == DragKind.Module && _session.CanPlaceModule(_session.Modules.Get(d.A), holder), d => place(d.A));
            }
        }

        // ------------------------------------------------------------------ Module, Chips, Rezeptbuch

        private void DrawRightColumn(float height)
        {
            GUILayout.BeginVertical(GUILayout.Width(SideWidth), GUILayout.Height(height));
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(!_showRecipes, UiTexts.Build.ModulesAndRunes, GUI.skin.button, GUILayout.Height(26f))) _showRecipes = false;
            // Rezepte sind vorübergehend aus: ohne Evolutionen kein Rezeptbuch-Reiter.
            if (!Betaknight.Core.Evolution.EvolutionCatalog.Enabled) _showRecipes = false;
            else if (GUILayout.Toggle(_showRecipes, UiTexts.Build.RecipeBook, GUI.skin.button, GUILayout.Height(26f))) _showRecipes = true;
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
                string icon = EffectText.Icon(m.ModuleId);
                GUILayout.BeginHorizontal();
                GUILayout.Box(new GUIContent($"{(icon.Length > 0 ? icon + " " : string.Empty)}<b>{name}</b> <color=#9aa4b2>[{kind}]</color>  {where}", d?.DescriptionAt(m.Level)),
                    m.IsFree ? UiTheme.Cell : UiTheme.EmptyCell, GUILayout.Height(26f));
                Rect r = GUILayoutUtility.GetLastRect();
                int id = m.InstanceId;
                GUI.enabled = !m.IsFree && _session.CanEditModules;
                if (!m.IsFree && GUILayout.Button(new GUIContent("×", UiTexts.Build.TakeOffTip), GUILayout.Width(26f), GUILayout.Height(26f)))
                    _session.TakeOffModule(id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                bool linkable = !m.IsFree && ModuleRules.IsTargeted(m.ModuleId);
                _drag.Source(r, new DragItem(DragKind.Module, id, name), click: linkable ? () => _linking = id : (System.Action)null,
                    shortcut: () => ModuleShortcut(id));
                Hover(r, $"<b>{name}</b> [{kind}]: {d?.DescriptionAt(m.Level)}"
                    + (linkable ? $" · {_session.DescribeTrigger(m)} <color=#9aa4b2>{UiTexts.Build.LinkClick}</color>" : string.Empty));
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.Module && _session.Modules.Get(d.A)?.IsFree == false, d => _session.TakeOffModule(d.A));
        }

        /// <summary>Kurzweg: eingesetztes Modul abnehmen, freies auf den ersten passenden freien Platz (Relais vor Komponenten, Lesereihenfolge).</summary>
        private void ModuleShortcut(int moduleId)
        {
            ModuleInstance m = _session.Modules.Get(moduleId);
            if (m == null) return;
            if (!m.IsFree)
            {
                _session.TakeOffModule(moduleId);
                return;
            }
            for (int i = 0; i < Board.Relays.Count; i++)
                if (_session.CanPlaceModule(m, Board.Relays[i])) { _session.PlaceModuleOnRelay(moduleId, i); return; }
            foreach (ComponentSlot c in Board.Components)
                if (_session.CanPlaceModule(m, c.Skill)) { _session.PlaceModuleOnSkill(moduleId, c.Skill.InstanceId); return; }
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
                int max = _session.DifficultyBonus.MaxCells(rune.Rune.Difficulty);
                GUILayout.Box(new GUIContent($"{RuneText.Difficulty(rune.Rune.Difficulty)} <b>{rune.Name}</b>{RuneText.LevelBadge(rune.Rune, rune.Level)}{growth}  <color=#9aa4b2>{UiTexts.Build.MaxCells(max)}</color>",
                    $"{rune.Description}\n{RuneText.DifficultyTip(rune.Rune)}\n{UiTexts.Build.RelayLimit(max)}\n{UiTexts.Build.RuneDropHint}"), UiTheme.Cell, GUILayout.Height(26f));
                Rect r = GUILayoutUtility.GetLastRect();
                int index = i;
                _drag.Source(r, new DragItem(DragKind.Rune, index, rune.Name), shortcut: () => _session.EquipRuneFromInventory(index));
                Hover(r, $"<b>{rune.Name}</b>: {rune.Description} {rune.Rune.LevelText(rune.Level)} · {DifficultyText.Tooltip(rune.Rune.Difficulty)}");
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            Rect column = GUILayoutUtility.GetLastRect();
            _drag.Target(column, d => d.Kind == DragKind.Relay && !_session.RuneInventory.IsFull, d => _session.UnequipRune(d.A));
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

        // ------------------------------------------------------------------ Logik-Chips, Pins, Pulse (A-20)

        /// <summary>Gatter-Relais der kompilierten Platine zu einem Chip (UND, ODER, NICHT, Sicherung), sonst null.</summary>
        private LogicRelay GateOf(BoardChip chip)
        {
            LogicChip compiled = CompiledChip(chip);
            return compiled != null && compiled.RelayIndex >= 0 && compiled.RelayIndex < _compiled.Relays.Count ? _compiled.Relays[compiled.RelayIndex] : null;
        }

        private LogicChip CompiledChip(BoardChip chip)
        {
            if (_compiled == null || chip == null) return null;
            foreach (LogicChip k in _compiled.Chips)
                if (k.Rect.Origin == chip.Position) return k;
            return null;
        }

        /// <summary>Gatter: Leitungen von den Eingangs-Relais (violett) und zu den versorgten Komponenten (türkis, orange zu gross).</summary>
        private void DrawGateTraces(Rect grid, float size)
        {
            if (_compiled == null) return;
            foreach (LogicRelay gate in _compiled.Relays)
            {
                if (!gate.Gate.HasValue || !gate.Rect.HasValue) continue;
                Rect from = CircuitGrid.RectOf(grid, size, gate.Rect.Value);
                foreach (int input in gate.Inputs)
                    if (input >= 0 && input < _compiled.Relays.Count && _compiled.Relays[input].Rect.HasValue)
                        CircuitGrid.DrawTrace(CircuitGrid.RectOf(grid, size, _compiled.Relays[input].Rect.Value), from, CircuitGrid.GateBorder);
                foreach (int row in gate.Powered)
                    if (row >= 0 && row < _compiled.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, _compiled.Rows[row].Rect.Value), CircuitGrid.TraceColor);
                foreach (int row in gate.TooLarge)
                    if (row >= 0 && row < _compiled.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, _compiled.Rows[row].Rect.Value), CircuitGrid.TooLargeBorder);
            }
        }

        private void DrawBoardChip(Rect grid, float size, int index)
        {
            BoardChip chip = Board.Chips[index];
            Rect rect = CircuitGrid.RectOf(grid, size, chip.Rect);
            bool focus = chip == _selected || chip == _hoverPart;
            LogicRelay gate = GateOf(chip);
            var look = new CircuitGrid.ChipLook
            {
                Focus = focus,
                Border = chip == _selected ? SelectedColor : (Color?)null,
                Caption = gate != null ? RuneText.Difficulty(gate.Difficulty) : null,
                Capacity = chip.Kind == ChipKind.Capacitor ? _session.ChipCatalog.Config.CapacitorCapacity : 0,
            };
            CircuitGrid.DrawLogicChip(rect, chip.Definition, chip.Turns, look, ChipTip(chip, gate) + "\n" + UiTexts.Circuit.ChipDragHint,
                size < 60f ? CircuitGrid.Tiny : CircuitGrid.Label);

            _drag.Source(rect, new DragItem(DragKind.BoardChip, index, chip.Name),
                click: () => _selected = chip, shortcut: () => _session.RotateChip(Board.IndexOf(chip)));
            Hover(rect, $"<b>{chip.Name}</b>: {chip.Definition.Description}", chip);
        }

        /// <summary>Pulsverbindungen der kompilierten Platine als goldene Linien (Start-Pin → Chips → Ziel-Pin).</summary>
        private void DrawPulseLinks(Rect grid, float size)
        {
            if (_compiled == null || Event.current.type != EventType.Repaint) return;
            float thickness = Mathf.Clamp(size * 0.05f, 2f, 4f);
            foreach (PulseLink link in _compiled.Links)
                CircuitGrid.DrawLink(CircuitGrid.LinkPoints(grid, size, link), CircuitGrid.LinkColor, thickness);
        }

        /// <summary>Pins aller Komponenten als Kerben, über den Komponenten.</summary>
        private void DrawPins(Rect grid, float size)
        {
            if (_compiled == null) return;
            int percent = PinConfig.Default.TypedPinBonusPercent;
            foreach (LogicRow row in _compiled.Rows)
                foreach (PlacedPin pin in row.Pins)
                {
                    bool matched = CircuitGrid.IsPinMatched(_compiled, row, pin);
                    bool linked = CircuitGrid.IsPinLinked(_compiled, pin);
                    string kind = pin.IsTyped ? SkillKinds.DisplayName(pin.Kind) : null;
                    string tip = UiTexts.Circuit.PinTip(kind, matched, linked, percent);
                    Rect notch = CircuitGrid.DrawPin(grid, size, pin, matched, linked, tip);
                    Hover(notch, tip);
                }
        }

        /// <summary>Leiste mit den Logik-Chips des Inventars: ziehen = legen, Doppelklick = erste freie Zelle; Ziel zum Abnehmen.</summary>
        private void DrawChipStrip(float width)
        {
            IReadOnlyList<ChipDefinition> chips = _session.ChipInventory;
            string legend = chips.Count == 0 ? UiTexts.Circuit.ChipsEmpty : UiTexts.Circuit.ChipsLegend;
            GUILayout.Label($"{UiTexts.Circuit.ChipsTitle} {chips.Count}  <color=#9aa4b2>{legend}</color>", UiTheme.SmallLine);

            const float tile = 40f;
            const float gap = 4f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width - gap) / (tile + gap)));
            int rows = Mathf.Max(1, Mathf.CeilToInt(chips.Count / (float)perRow));
            Rect strip = GUILayoutUtility.GetRect(width, rows * (tile + gap) + gap, GUILayout.ExpandWidth(true));
            UiTheme.Fill(strip, UiTheme.Background);
            UiTheme.Outline(strip, UiTheme.BorderColor, 1f);
            GUI.Label(strip, new GUIContent(string.Empty, UiTexts.Circuit.StripDropHint));

            int capacity = _session.ChipCatalog.Config.CapacitorCapacity;
            for (int i = 0; i < chips.Count; i++)
            {
                ChipDefinition chip = chips[i];
                var rect = new Rect(strip.x + gap + i % perRow * (tile + gap), strip.y + gap + i / perRow * (tile + gap), tile, tile);
                var look = new CircuitGrid.ChipLook { Capacity = chip.Kind == ChipKind.Capacitor ? capacity : 0 };
                string tip = $"{ChipHead(chip)}\n{UiTexts.Circuit.InventoryDragHint}";
                CircuitGrid.DrawLogicChip(rect, chip, 0, look, tip, CircuitGrid.Tiny);
                int index = i;
                _drag.Source(rect, new DragItem(DragKind.LogicChip, index, chip.Name), shortcut: () => QuickPlaceChip(index));
                Hover(rect, $"<b>{chip.Name}</b>: {chip.Description}");
            }

            _drag.Target(strip, d => d.Kind == DragKind.BoardChip && _session.CanEditChips && d.A >= 0 && d.A < Board.Chips.Count,
                d => _session.RemoveChip(d.A));
        }

        /// <summary>Doppelklick auf einen Chip der Leiste: erste freie Zelle in Lesereihenfolge.</summary>
        private void QuickPlaceChip(int inventoryIndex)
        {
            Cell? free = Board.FreeCellFor(Shape.One);
            if (free.HasValue && _session.PlaceChip(inventoryIndex, free.Value)) _selected = Board.At(free.Value) as BoardChip;
        }

        /// <summary>Tooltip eines Chips auf der Platine: Name, Text, Diode-Richtung, Gatter-Eingänge, Kondensator, Verbindungen.</summary>
        private string ChipTip(BoardChip chip, LogicRelay gate)
        {
            var lines = new List<string> { ChipHead(chip.Definition) };
            if (chip.Kind == ChipKind.Diode)
                lines.Add(UiTexts.Circuit.DiodeDirection(UiTexts.Circuit.SideName((int)ChipDefinition.DiodeIn(chip.Turns)),
                    UiTexts.Circuit.SideName((int)ChipDefinition.DiodeOut(chip.Turns))));
            if (chip.Kind == ChipKind.Capacitor)
                lines.Add(UiTexts.Circuit.Capacitor(0, _session.ChipCatalog.Config.CapacitorCapacity));
            if (gate != null)
            {
                List<string> inputs = gate.Inputs.Where(i => i >= 0 && i < _compiled.Relays.Count).Select(i => _compiled.Relays[i].Label).ToList();
                lines.Add(inputs.Count > 0 ? UiTexts.Circuit.GateInputs(string.Join(", ", inputs)) : UiTexts.Circuit.NoInputs);
                lines.Add(UiTexts.Circuit.GateDifficulty(DifficultyText.Tooltip(gate.Difficulty)));
                lines.Add(gate.Powered.Count > 0 ? UiTexts.Circuit.GatePowers(string.Join(", ", gate.Powered.Select(i => $"#{i + 1}"))) : UiTexts.Circuit.GatePowersNothing);
            }
            if (_compiled != null)
            {
                List<string> carried = _compiled.Links.Where(l => l.Path.Contains(chip.Position)).Select(LinkText).ToList();
                if (carried.Count > 0) lines.Add(UiTexts.Circuit.CarriesLinks(string.Join(", ", carried)));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Erste Tooltip-Zeile eines Chips; bei Effekt-Chips (A-21) mit Symbol und Farbe des Effekts.</summary>
        private static string ChipHead(ChipDefinition chip)
        {
            if (EffectText.TryGet(chip.EffectId, out CircuitEffectDefinition e))
                return UiTexts.Effects.Tip(EffectText.Icon(e), $"<color={e.Colour}>{chip.Name}</color>", chip.Description);
            return UiTexts.Circuit.ChipTip(chip.Name, chip.Description);
        }

        /// <summary>Komponente der kompilierten Platine zur Komponente <paramref name="index"/> des Builds, sonst null.</summary>
        private LogicRow CompiledRow(int index)
        {
            if (_compiled == null || index < 0 || index >= _compiled.Rows.Count || index >= Board.Components.Count) return null;
            LogicRow row = _compiled.Rows[index];
            return row.Rect?.Origin == Board.Components[index].Origin ? row : null;
        }

        /// <summary>Pins und Pulse einer Komponente für ihren Tooltip.</summary>
        private IEnumerable<string> PinAndPulseLines(int index)
        {
            if (_compiled == null || index < 0 || index >= _compiled.Rows.Count) yield break;
            LogicRow row = _compiled.Rows[index];
            if (index >= Board.Components.Count || row.Rect?.Origin != Board.Components[index].Origin) yield break;
            int percent = PinConfig.Default.TypedPinBonusPercent;
            if (row.Pins.Count > 0)
            {
                int plain = row.Pins.Count(p => !p.IsTyped);
                var parts = new List<string>();
                if (plain > 0) parts.Add($"{plain}× {UiTexts.Circuit.PlainPin}");
                foreach (PlacedPin pin in row.Pins.Where(p => p.IsTyped))
                    parts.Add(UiTexts.Circuit.TypedPin(SkillKinds.DisplayName(pin.Kind), CircuitGrid.IsPinMatched(_compiled, row, pin), percent));
                yield return UiTexts.Circuit.PinsLine(string.Join(", ", parts));
            }
            List<string> to = _compiled.LinksFrom(PulseNode.Component(index)).Select(LinkText).ToList();
            if (to.Count > 0) yield return $"<color=#ffd75e>{UiTexts.Circuit.PulsesTo(string.Join(", ", to))}</color>";
            List<string> from = _compiled.Links.Where(l => l.To.Equals(PulseNode.Component(index))).Select(LinkText).ToList();
            if (from.Count > 0) yield return $"<color=#ffd75e>{UiTexts.Circuit.PulsesFrom(string.Join(", ", from))}</color>";
        }

        private static string NodeName(PulseNode node) => node.IsCapacitor ? ArenaTexts.CapacitorName(node.Index) : $"#{node.Index + 1}";

        private string LinkText(PulseLink link)
        {
            string text = UiTexts.Circuit.Link(NodeName(link.From), NodeName(link.To), link.Delay);
            if (link.Amplifiers > 0 && _compiled != null)
                text += $" <color={EffectText.ColourHex(CircuitEffectIds.Amplifier)}>({UiTexts.Circuit.AmplifiedLink(link.Amplifiers, link.Amplifiers * _compiled.EffectConfig.AmplifierPowerPercent)})</color>";
            return text;
        }

        // ------------------------------------------------------------------ Zustand und Texte

        private string ComponentName(ComponentSlot c) => c.Skill.NameFrom(_session.SkillCatalog);

        private Color StateColor(ComponentSlot c) =>
            _session.IsPowered(c) ? CircuitGrid.PoweredBorder : _session.IsTooLarge(c) ? CircuitGrid.TooLargeBorder : CircuitGrid.UnpoweredBorder;

        private string StateHex(ComponentSlot c) => UiTheme.Hex(StateColor(c));

        /// <summary>Kurz für den Chip: «√ ●» (versorgt, Schwierigkeit), «× too large» oder «× not powered».</summary>
        private string StateShort(ComponentSlot c)
        {
            if (_session.IsPowered(c)) return $"√ {DifficultyText.Symbol(_session.ComponentDifficulty(c))}";
            return _session.IsTooLarge(c) ? $"× {UiTexts.Arena.StateTooLarge}" : $"× {UiTexts.NotPowered}";
        }

        /// <summary>«powered by Relay 1 (On Hit), Relay 2 (Clock 2 s)», «not powered (too large)» oder «not powered».</summary>
        private string StateLong(ComponentSlot c)
        {
            List<RelayChip> powering = _session.PoweringRelays(c);
            if (powering.Count > 0)
                return UiTexts.Build.PoweredBy(string.Join(", ", powering.Select(r => ArenaTexts.RelayName(Board.IndexOf(r), r.Name))));
            return _session.IsTooLarge(c) ? UiTexts.NotPoweredTooLarge : UiTexts.NotPowered;
        }

        /// <summary>«powers #1, #2 · too large here: #3» oder der Hinweis, dass es nichts versorgt.</summary>
        private string PowersText(RelayChip relay)
        {
            int max = _session.RelayMaxCells(relay);
            IReadOnlyList<ComponentSlot> touching = Board.ComponentsTouching(relay);
            List<string> powered = touching.Where(c => c.Cells <= max).Select(c => $"#{Board.IndexOf(c) + 1}").ToList();
            List<string> large = touching.Where(c => c.Cells > max).Select(c => $"#{Board.IndexOf(c) + 1}").ToList();
            var parts = new List<string> { powered.Count > 0 ? UiTexts.Build.Powers(string.Join(", ", powered)) : UiTexts.Build.PowersNothing };
            if (large.Count > 0) parts.Add($"<color={UiTheme.Hex(CircuitGrid.TooLargeBorder)}>{UiTexts.Build.TooLargeHere(string.Join(", ", large))}</color>");
            return string.Join(" · ", parts);
        }

        /// <summary>Erste Wirkung, z. B. «60 % ≈ 6».</summary>
        private static string Effect(SkillInfo info)
        {
            if (info == null) return string.Empty;
            return info.Effects.Count > 0 ? info.Effects[0].Text : UiTexts.Build.NoEffect;
        }

        /// <summary>Kurzwerte: Form, erste Wirkung und Cast-Zeit, z. B. «2×1 · 150 % ≈ 15 · Cast 0.8 s».</summary>
        private static string ShortStats(SkillInfo info)
        {
            if (info == null) return string.Empty;
            return $"{info.Skill.Shape} · {Effect(info)} · {info.CastText}";
        }

        private string SkillTip(SkillInstance skill, SkillInfo info, ComponentSlot slot)
        {
            var lines = new List<string>();
            if (info != null) lines.Add(info.Details);
            if (slot != null) lines.Add($"<color={StateHex(slot)}>{UiTexts.OnBoard(Board.IndexOf(slot) + 1)}: {StateLong(slot)}</color>");
            GrowthRule rule = _session.SkillGrowthRule(skill);
            if (rule != null)
            {
                string effect = _session.GrowthEffectText(rule, skill.Growth);
                lines.Add(UiTexts.Build.GrowsNow(rule.Text, effect, _session.MilestoneText(skill.Growth, true)));
            }
            if (!skill.IsBasicAttack) lines.AddRange(ModuleLines(skill));
            foreach (string evolution in _session.EvolutionHintsForSkill(skill.SkillId)) lines.Add(evolution);
            return string.Join("\n", lines);
        }

        private string ComponentTip(ComponentSlot c, int index, SkillInfo info, bool panel = false)
        {
            var lines = new List<string>();
            if (info != null) lines.Add(info.Details);
            lines.Add($"<color={StateHex(c)}>{StateLong(c)}</color>");
            if (_session.IsTooLarge(c))
            {
                int limit = Board.RelaysTouching(c).Select(r => _session.RelayMaxCells(r)).DefaultIfEmpty(0).Max();
                lines.Add(UiTexts.Build.TooLargeTip(c.Cells, limit));
            }
            else if (!_session.IsPowered(c))
            {
                lines.Add(UiTexts.Build.NotPoweredTip);
            }
            if (Board.TouchesCore(c)) lines.Add($"<color=#b18cff>{UiTexts.Build.CoreTip(Board.Config.CoreBonusPercent)}</color>");
            lines.AddRange(PinAndPulseLines(index));
            // A-21: Effekte aus Skill, Modulen und berührten Effekt-Chips (SkillInfo nennt die des Skills schon).
            IReadOnlyList<string> effects = EffectText.Of(CompiledRow(index));
            if (effects.Count > 0) lines.Add(UiTexts.Effects.Line(string.Join(" ", effects.Select(id => $"{EffectText.Icon(id)} {ArenaTexts.EffectName(id)}"))));
            GrowthRule rule = _session.SkillGrowthRule(c.Skill);
            if (rule != null)
            {
                string effect = _session.GrowthEffectText(rule, c.Skill.Growth);
                lines.Add(UiTexts.Build.GrowsNow(rule.Text, effect, _session.MilestoneText(c.Skill.Growth, true)));
            }
            if (!panel) lines.AddRange(ModuleLines(c.Skill));
            lines.AddRange(_session.EvolutionProgressFor(c.Skill));
            if (!panel) lines.Add(UiTexts.Build.ComponentDragHint);
            return string.Join("\n", lines);
        }

        private string RelayTip(RelayChip relay, int index, bool panel = false)
        {
            var lines = new List<string> { $"{ArenaTexts.RelayName(index, relay.Name)}: {relay.Description}" };
            string level = relay.Rune.LevelText(relay.Level);
            if (level.Length > 0) lines.Add(level);
            GrowthRule rule = _session.RelayGrowthRule(relay);
            if (rule != null)
            {
                string effect = _session.GrowthEffectText(rule, relay.Growth, relay);
                lines.Add(UiTexts.Build.GrowsRelay(rule.Text, effect, _session.MilestoneText(relay.Growth, false)));
            }
            lines.Add(RuneText.DifficultyTip(relay.Rune, _session.RelayDifficulty(relay) != relay.Rune.Difficulty));
            lines.Add(UiTexts.Build.RelayLimit(_session.RelayMaxCells(relay)));
            lines.Add(PowersText(relay));
            if (!panel) lines.AddRange(ModuleLines(relay));
            lines.AddRange(_session.EvolutionProgressFor(relay));
            if (!panel) lines.Add(UiTexts.Build.RelayDragHint);
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Papierkorb im Kopf: Skill, Komponente, Relais, Rune, Modul oder Chip hierher ziehen, dann mit «Yes» endgültig verwerfen.
        /// </summary>
        private void DrawTrash()
        {
            if (_pendingDiscard.HasValue)
            {
                DragItem d = _pendingDiscard.Value;
                GUILayout.Label($"<color={UiTheme.Hex(UiTheme.Bad)}>{UiTexts.Build.DiscardAsk(d.Label)}</color>", UiTheme.Small, GUILayout.ExpandWidth(false));
                if (GUILayout.Button(UiTexts.Build.DiscardYes, GUILayout.Width(52f), GUILayout.Height(28f)))
                {
                    Discard(d);
                    _pendingDiscard = null;
                }
                if (GUILayout.Button(UiTexts.Build.DiscardNo, GUILayout.Width(52f), GUILayout.Height(28f))) _pendingDiscard = null;
                return;
            }
            GUILayout.Box(new GUIContent($"<color=#ff8a80>×</color> {UiTexts.Build.Trash}", UiTexts.Build.TrashTip), UiTheme.Cell,
                GUILayout.Width(150f), GUILayout.Height(28f));
            _drag.Target(GUILayoutUtility.GetLastRect(), d => _session.CanChangeLoadout && d.Kind != DragKind.Item && d.Kind != DragKind.Equipped,
                d => _pendingDiscard = StableDiscard(d), redWhenInvalid: true);
        }

        /// <summary>Komponente → ihr Skill-Exemplar (stabile Id), damit die Rückfrage nicht an einer Position hängt.</summary>
        private DragItem StableDiscard(DragItem d) =>
            d.Kind == DragKind.Component && d.A >= 0 && d.A < Board.Components.Count && Board.Components[d.A].Skill != null
                ? new DragItem(DragKind.Skill, Board.Components[d.A].Skill.InstanceId, d.Label) : d;

        private void Discard(DragItem d)
        {
            switch (d.Kind)
            {
                case DragKind.Skill:
                    _session.DiscardSkill(d.A);
                    break;
                case DragKind.Component:
                    if (d.A >= 0 && d.A < Board.Components.Count && Board.Components[d.A].Skill != null)
                        _session.DiscardSkill(Board.Components[d.A].Skill.InstanceId);
                    break;
                case DragKind.Relay:
                    _session.DiscardRelay(d.A);
                    break;
                case DragKind.Rune:
                    _session.DiscardRune(d.A);
                    break;
                case DragKind.Module:
                    _session.DiscardModule(d.A);
                    break;
                case DragKind.LogicChip:
                    _session.DiscardChip(d.A);
                    break;
                case DragKind.BoardChip:
                    _session.DiscardBoardChip(d.A);
                    break;
            }
            _selected = null;
        }

        /// <summary>
        /// Ausgerüstete Module als Kärtchen am unteren Rand eines Teils: Symbol und Kurzname, Tooltip mit Wirkung.
        /// Rechts- oder Doppelklick nimmt ab, Ziehen verschiebt (oder zurück in die Modul-Liste), Klick bei Trigger/Charge Link
        /// wählt das Ziel. Vor der Quelle des Teils gezeichnet, damit die Kärtchen den Klick bekommen.
        /// </summary>
        private void DrawModuleBadges(Rect rect, float size, IModuleHolder holder)
        {
            if (holder == null || holder.Modules.Count == 0) return;
            float h = Mathf.Clamp(size * 0.26f, 14f, 20f);
            float w = Mathf.Max(h, (rect.width - 4f) / Mathf.Max(1, holder.Modules.Count) - 2f);
            float x = rect.x + 2f;
            foreach (ModuleInstance m in holder.Modules.ToList())
            {
                var badge = new Rect(x, rect.yMax - h - 2f, w, h);
                string name = m.NameFrom(_session.ModuleCatalog);
                string icon = EffectText.Icon(m.ModuleId);
                bool targeted = ModuleRules.IsTargeted(m.ModuleId);
                string label = (icon.Length > 0 ? icon : targeted ? (m.ModuleId == ModuleIds.ChargeLink ? "»" : "→") : "●") + Shorten(name, w > 48f ? 9 : 4);
                ModuleDefinition d = _session.ModuleDefinitionOf(m);
                string effect = targeted ? _session.DescribeTrigger(m) : d?.DescriptionAt(m.Level) ?? string.Empty;
                string tip = $"<b>{name}</b>: {effect}\n<color=#9aa4b2>{(targeted ? UiTexts.Build.ModuleBadgeTipTargeted : UiTexts.Build.ModuleBadgeTip)}</color>";
                bool over = badge.Contains(Event.current.mousePosition);
                UiTheme.Fill(badge, over ? new Color(0.45f, 0.38f, 0.10f, 0.98f) : new Color(0.20f, 0.17f, 0.06f, 0.95f));
                UiTheme.Outline(badge, new Color(1f, 0.84f, 0.37f, 1f), 1f);
                GUI.Label(badge, new GUIContent($"<color=#ffd75e>{label}</color>", tip), CircuitGrid.Tiny);
                int id = m.InstanceId;
                _drag.Source(badge, new DragItem(DragKind.Module, id, name), click: targeted ? () => _linking = id : (System.Action)null,
                    shortcut: () => _session.TakeOffModule(id));
                Hover(badge, tip.Replace("\n", " · "));
                x += w + 2f;
            }
        }

        /// <summary>Klick auf eine Komponente: im Verknüpfen-Modus wird sie das Ziel, sonst ausgewählt.</summary>
        private void ClickComponent(ComponentSlot c)
        {
            if (_linking >= 0)
            {
                if (c.Skill != null) _session.SetTriggerTarget(_linking, ModuleTarget.Skill(c.Skill.InstanceId));
                _linking = -1;
                return;
            }
            _selected = c;
        }

        /// <summary>«Module slots 1/2» und darunter jedes Modul mit seiner Wirkung (Trigger/Charge Link mit Ziel).</summary>
        private List<string> ModuleLines(IModuleHolder holder)
        {
            var lines = new List<string> { UiTexts.Build.ModuleSlots(holder.Modules.Count, holder.ModuleSlots) };
            foreach (ModuleInstance m in holder.Modules)
            {
                ModuleDefinition d = _session.ModuleDefinitionOf(m);
                string name = m.NameFrom(_session.ModuleCatalog);
                string effect = ModuleRules.IsTargeted(m.ModuleId) ? _session.DescribeTrigger(m) : d?.DescriptionAt(m.Level) ?? string.Empty;
                lines.Add($"<color=#ffd75e>● {name}</color>: {effect}");
            }
            return lines;
        }

        private static string Shorten(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";
    }
}
