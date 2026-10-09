using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Circuit;
using Betaknight.Core.Hex;
using Betaknight.Core.Encounters;
using Betaknight.Core.Map;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Overworld.Controllers;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// HUD per IMGUI: Statusleiste oben (HP-Balken, Gold, Splitter, Akt/Zug, Boss-Uhr, Gefahrenstufe, Knöpfe), Ziel-Panel,
    /// Inspector für das Feld unter der Maus und Debug-Infos auf F3. «New Run» fragt nach.
    /// </summary>
    public sealed class OverworldHud : MonoBehaviour
    {
        private OverworldSession _session;
        private OverworldController _controller;
        private EncounterCatalog _encounters;
        private Action _onNewMap;
        private GUIStyle _style;

        /// <summary>Öffnet das Fenster «Build». Ohne Zuweisung gibt es keinen Knopf.</summary>
        public Action OnOpenBuild;

        /// <summary>Öffnet das Inventar. Ohne Zuweisung gibt es keinen Knopf.</summary>
        public Action OnOpenInventory;

        public void Initialize(OverworldSession session, OverworldController controller, EncounterCatalog encounters, Action onNewMap)
        {
            _session = session;
            _controller = controller;
            _encounters = encounters;
            _onNewMap = onNewMap;
            _enemyCoord = null;
            _enemyPreviews.Clear();
            _enemyText = string.Empty;
        }

        private const float PanelWidth = 380f;
        private const float BarHeight = 62f;

        /// <summary>Unterkante der Statusleiste; grosse Fenster (Build, Inventar) beginnen darunter.</summary>
        public const float TopBarHeight = BarHeight;

        /// <summary>Platz für ein grosses Fenster unter der Statusleiste, zentriert im Rest.</summary>
        public static Rect BelowTopBar(float maxWidth, float maxHeight)
        {
            float top = TopBarHeight + 6f;
            float width = Mathf.Min(Screen.width - 24f, maxWidth);
            float height = Mathf.Min(Screen.height - top - 8f, maxHeight);
            return new Rect((Screen.width - width) * 0.5f, top + (Screen.height - top - 8f - height) * 0.5f, width, height);
        }

        /// <summary>Statusleiste oben über die ganze Breite: HP, Gold, Splitter, Akt/Zug, Boss-Uhr, Knöpfe.</summary>
        private static Rect TopBarRect => new Rect(0f, 0f, Screen.width, BarHeight);

        /// <summary>Ziel-Panel links unter der Leiste: worum es in diesem Akt geht.</summary>
        private static Rect GoalRect => new Rect(12f, BarHeight + 8f, PanelWidth, GoalHeight);
        private static float GoalHeight = 52f;

        /// <summary>Debug-Infos (F3) rechts unter der Leiste.</summary>
        private static Rect DebugRect => new Rect(Screen.width - PanelWidth - 12f, BarHeight + 8f, PanelWidth, Mathf.Max(160f, Screen.height - BarHeight - 24f));

        private static Rect ConfirmRect => new Rect(Screen.width * 0.5f - 210f, Screen.height * 0.5f - 80f, 420f, 160f);

        private static bool _debugVisible;
        private static bool _confirmNewRun;

        private Vector2 _scroll;
        private Vector2 _inspectorScroll;
        private GUIStyle _bar;
        private GUIStyle _barCenter;
        private float _ghostHp = -1f;
        private int _lastHp = -1;
        private float _ghostHoldUntil;

        /// <summary>Liegt ein Bildschirmpunkt (Ursprung unten links) über dem HUD? Dann ignoriert die Karte den Klick.</summary>
        public static bool ContainsScreenPoint(Vector2 screen)
        {
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            if (_confirmNewRun) return true;
            return TopBarRect.Contains(guiPoint) || GoalRect.Contains(guiPoint) || (_debugVisible && DebugRect.Contains(guiPoint));
        }

        private void Update()
        {
            if (_session == null) return;
            int hp = _session.Stats.Hp;
            if (_ghostHp < 0f) _ghostHp = hp;
            if (_lastHp >= 0 && hp < _lastHp) _ghostHoldUntil = Time.unscaledTime + 0.5f;
            _lastHp = hp;
            if (_ghostHp < hp) _ghostHp = hp;
            else if (Time.unscaledTime >= _ghostHoldUntil)
                _ghostHp = Mathf.MoveTowards(_ghostHp, hp, Mathf.Max(1f, _session.Stats.MaxHp) * 0.8f * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            if (_session == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
                _bar = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = false, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
                _barCenter = new GUIStyle(_bar) { alignment = TextAnchor.MiddleCenter };
            }

            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F3)
            {
                _debugVisible = !_debugVisible;
                e.Use();
            }

            DrawTopBar();
            DrawGoal();
            DrawInspector();
            if (_debugVisible) DrawDebug();
            if (_confirmNewRun) DrawConfirm();
            UiTheme.DrawTooltip();
        }

        // ------------------------------------------------------------------ Top-Bar

        private void DrawTopBar()
        {
            Rect bar = TopBarRect;
            UiTheme.Fill(bar, new Color(0.05f, 0.06f, 0.08f, 0.94f));
            UiTheme.Fill(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), UiTheme.BorderColor);

            // Zeile 1: HP-Balken, Gold, Splitter, Akt/Zug, Boss-Uhr, Gefahrenstufe hier.
            float x = 12f;
            var hpRect = new Rect(x, 6f, 240f, 24f);
            DrawHpBar(hpRect);
            x = hpRect.xMax + 16f;
            x = BarLabel(x, 6f, new GUIContent($"<color={UiTheme.Hex(UiTheme.Accent)}><b>{_session.Stats.Gold}</b> {UiTexts.Hud.GoldWord}</color>"));
            x = DrawShardPips(x, 6f);
            string kit = _session.Kit != null ? $" · {_session.Kit.Name}" : string.Empty;
            x = BarLabel(x, 6f, new GUIContent(UiTexts.Hud.ActTurn(_session.Act, _session.Turns.CurrentTurn) + $"<color=#9aa4b2>{kit}</color>"));
            int boss = _session.TurnsUntilBoss;
            string bossHex = boss <= 3 ? UiTheme.Hex(UiTheme.Bad) : "#d6b3ff";
            x = BarLabel(x, 6f, new GUIContent($"<color={bossHex}><b>{UiTexts.Hud.BossIn(boss)}</b></color>", UiTexts.Hud.BossTip));
            int here = _session.TierAt(_session.Player.Position);
            BarLabel(x, 6f, new GUIContent(UiTexts.Hud.DangerHere(here), UiTexts.Hud.DangerTip));

            // Zeile 2: Tags, Sets, Minen links; Knöpfe rechts.
            float right = Screen.width - 12f;
            right = BarButton(right, UiTexts.Hud.NewRun, UiTexts.Hud.NewRunTip, () => _confirmNewRun = true, 96f);
            if (_session.CanOpenShop) right = BarButton(right, UiTexts.Hud.OpenShop, null, () => _session.OpenShop(), 110f);
            if (OnOpenInventory != null && !_session.IsGameOver)
                right = BarButton(right, UiTexts.Hud.InventoryButton(_session.Inventory.Count, _session.Inventory.Capacity), UiTexts.Hud.InventoryTip, OnOpenInventory, 170f);
            if (OnOpenBuild != null && !_session.IsGameOver)
                right = BarButton(right, UiTexts.Hud.BuildButton, UiTexts.Hud.BuildTip, OnOpenBuild, 100f);

            var parts = new List<string>();
            string tags = TagList();
            string sets = SetList();
            if (tags.Length > 0) parts.Add(UiTexts.Hud.Tags(tags));
            if (sets.Length > 0) parts.Add(UiTexts.Hud.Sets(sets));
            string tip = TagTooltip();
            var setTips = new List<string>();
            foreach ((SetDefinition set, int pieces) in _session.WornSets()) setTips.Add(set.Describe(pieces));
            if (setTips.Count > 0) tip = tip.Length > 0 ? tip + "\n\n" + string.Join("\n\n", setTips) : string.Join("\n\n", setTips);
            if (parts.Count > 0)
                GUI.Label(new Rect(12f, 34f, right - 24f, 24f), new GUIContent($"<size=13>{string.Join("   ", parts)}</size>", tip), _bar);
        }

        private float BarLabel(float x, float y, GUIContent content)
        {
            float w = _bar.CalcSize(content).x + 4f;
            GUI.Label(new Rect(x, y, w, 24f), content, _bar);
            return x + w + 18f;
        }

        private float BarButton(float right, string text, string tip, Action onClick, float width)
        {
            var rect = new Rect(right - width, 34f, width, 24f);
            if (GUI.Button(rect, new GUIContent(text, tip))) onClick();
            return rect.x - 6f;
        }

        /// <summary>HP als Balken mit Zahl; frischer Schaden bleibt kurz hell stehen, unter 30 % pulsiert der Rahmen rot.</summary>
        private void DrawHpBar(Rect rect)
        {
            float max = Mathf.Max(1f, _session.Stats.MaxHp);
            float hp = Mathf.Clamp01(_session.Stats.Hp / max);
            float ghost = Mathf.Clamp01(Mathf.Max(_ghostHp, _session.Stats.Hp) / max);
            UiTheme.Fill(rect, new Color(0.2f, 0.2f, 0.22f));
            if (ghost > hp) UiTheme.Fill(new Rect(rect.x + rect.width * hp, rect.y, rect.width * (ghost - hp), rect.height), new Color(1f, 0.92f, 0.75f, 0.85f));
            UiTheme.Fill(new Rect(rect.x, rect.y, rect.width * hp, rect.height), hp > 0.3f ? UiTheme.Good : UiTheme.Bad);
            if (hp <= 0.3f)
            {
                Color c = UiTheme.Bad;
                c.a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                UiTheme.Outline(rect, c, 2f);
            }
            GUI.Label(rect, new GUIContent($"<b>HP {_session.Stats.Hp} / {_session.Stats.MaxHp}</b>", UiTexts.Hud.HpTip), _barCenter);
        }

        /// <summary>Splitter als Pips: drei volle ergeben eine Runen-Belohnung.</summary>
        private float DrawShardPips(float x, float y)
        {
            int have = _session.Stats.Shards;
            int need = OverworldSession.ShardsPerRune;
            var content = new GUIContent(UiTexts.Hud.ShardsWord, UiTexts.Hud.ShardsTip(have, need));
            float w = _bar.CalcSize(content).x + 4f;
            GUI.Label(new Rect(x, y, w, 24f), content, _bar);
            x += w + 4f;
            for (int i = 0; i < need; i++)
            {
                var pip = new Rect(x + i * 16f, y + 6f, 12f, 12f);
                bool full = i < have;
                UiTheme.Fill(pip, full ? new Color(0.70f, 0.55f, 1.00f) : new Color(0.2f, 0.2f, 0.24f));
                UiTheme.Outline(pip, new Color(0.70f, 0.55f, 1.00f), 1f);
            }
            x += need * 16f;
            if (have > need) x = BarLabel(x, y, new GUIContent($"+{have - need}")) - 18f;
            return x + 18f;
        }

        // ------------------------------------------------------------------ Ziel

        /// <summary>«Survive the boss: he finds you in 7 turns. Danger here 5, centre 2» plus Minen-Warnungen.</summary>
        private void DrawGoal()
        {
            var lines = new List<string>();
            int boss = _session.TurnsUntilBoss;
            int here = _session.TierAt(_session.Player.Position);
            int centre = _session.TierAt(_session.Map.Center);
            lines.Add(UiTexts.Hud.Goal(boss));
            lines.Add($"<size=13><color=#9aa4b2>{UiTexts.Hud.GoalTier(here, centre)}</color></size>");
            foreach (MineRaid raid in _session.Raids)
            {
                string state = raid.IsLost ? UiTexts.Hud.MineLost : UiTexts.Hud.MineAttacked(raid.TurnsLeft(_session.Turns.CurrentTurn));
                lines.Add($"<size=13><color=#ff7a6b>{UiTexts.Hud.Mine(raid.Coord.ToString(), state)}</color></size>");
            }
            var content = new GUIContent(string.Join("\n", lines), UiTexts.Hud.GoalTip);
            GoalHeight = _style.CalcHeight(content, PanelWidth - 16f) + 12f;
            Rect rect = GoalRect;
            UiTheme.Fill(rect, new Color(0.05f, 0.06f, 0.08f, 0.88f));
            UiTheme.Fill(new Rect(rect.x, rect.y, 3f, rect.height), new Color(0.82f, 0.70f, 1f));
            GUI.Label(new Rect(rect.x + 10f, rect.y + 6f, rect.width - 16f, rect.height - 12f), content, _style);
        }

        // ------------------------------------------------------------------ Inspector (Feld unter der Maus)

        private void DrawInspector()
        {
            if (_controller == null || !_controller.HoveredCoord.HasValue
                || !_session.Map.TryGetCell(_controller.HoveredCoord.Value, out HexCell hovered)) return;
            float top = GoalRect.yMax + 8f;
            var area = new Rect(12f, top, PanelWidth, Screen.height - top - 12f);
            GUILayout.BeginArea(area);
            _inspectorScroll = GUILayout.BeginScrollView(_inspectorScroll, GUIStyle.none, GUIStyle.none);
            GUILayout.BeginVertical(GUI.skin.box);
            string info = hovered.IsContentKnown ? Describe(hovered) : UiTexts.Hud.Unknown;
            string enemies = hovered.IsContentKnown && !hovered.IsResolved ? EnemyBoards(hovered) : string.Empty;
            GUILayout.Label($"<b>{info}</b>", _style);
            if (hovered.IsContentKnown && !hovered.IsResolved && IsFight(hovered))
                GUILayout.Label($"<size=13>{UiTexts.Hud.DangerHere(_session.TierAt(hovered.Coord))}</size>", _style);
            if (enemies.Length > 0) GUILayout.Label($"<size=13>{enemies}</size>", _style);
            if (hovered.IsContentKnown && !hovered.IsResolved) DrawExactEnemies();
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static bool IsFight(HexCell cell) =>
            cell.Content == CellContent.Enemy || cell.Content == CellContent.Elite || cell.Content == CellContent.Boss
            || (cell.Content == CellContent.GoldMine && cell.IsUnderAttack);

        // ------------------------------------------------------------------ Debug (F3)

        private void DrawDebug()
        {
            GUILayout.BeginArea(DebugRect, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label($"<b>{UiTexts.Hud.DebugTitle}</b>", _style);
            GUILayout.Label(UiTexts.Hud.Board(_session.BoardSize, _session.MaxBoardSize, _session.Board.Relays.Count, _session.Board.Components.Count, BoardList()), _style);
            GUILayout.Label($"<size=13>{BuildSummary()}</size>", _style);
            GUILayout.Label(UiTexts.Hud.Gear(GearList()), _style);
            GUILayout.Label(UiTexts.Hud.Position(_session.Player.Position.ToString()), _style);
            GUILayout.Label(UiTexts.Hud.Tile(Describe(_session.CurrentCell)), _style);
            GUILayout.Label(UiTexts.Hud.Seed(_session.Map.Seed), _style);
            if (_controller != null && _controller.HoveredCoord.HasValue)
                GUILayout.Label(UiTexts.Hud.Pointer(_controller.HoveredCoord.Value.ToString(), string.Empty), _style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------ Neuer Run mit Rückfrage

        private void DrawConfirm()
        {
            UiTheme.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            Rect rect = ConfirmRect;
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"<b>{UiTexts.Hud.NewRunQuestion}</b>", _style);
            GUILayout.Label($"<size=13><color=#9aa4b2>{UiTexts.Hud.NewRunWarning}</color></size>", _style);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(UiTexts.Hud.NewRunConfirm, GUILayout.Height(30f)))
            {
                _confirmNewRun = false;
                _onNewMap?.Invoke();
            }
            if (GUILayout.Button(UiTexts.Hud.Cancel, GUILayout.Height(30f))) _confirmNewRun = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _confirmNewRun = false;
                e.Use();
            }
        }

        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        /// <summary>Komponenten der Platine in Lesereihenfolge: «#1 ◆ Shock Stab 1×1 ← On Hit», unversorgte rot.</summary>
        private string BoardList()
        {
            var lines = new List<string>();
            List<OverworldSession.TriggerLink> links = _session.TriggerLinks();
            for (int i = 0; i < _session.Board.Components.Count; i++)
            {
                ComponentSlot c = _session.Board.Components[i];
                string skill = c.Skill.NameFrom(Skills);
                int modules = c.Skill.Modules.Count;
                string marks = modules > 0 ? $" <color=#ffd75e>◆{modules}</color>" : string.Empty;
                foreach (OverworldSession.TriggerLink link in links)
                    if (!link.FromBlock && link.From == i) marks += $" <color=#ffae42>↪{link.To + 1}</color>";
                if (_session.Board.TouchesCore(c)) marks += $" <color=#b18cff>+{_session.Board.Config.CoreBonusPercent} %</color>";
                if (_session.IsEvolutionReady(c.Skill)) marks += " <color=#d29bff>✦</color>";
                List<RelayChip> powering = _session.PoweringRelays(c);
                string power = powering.Count > 0
                    ? $"{RuneText.Difficulty(_session.ComponentDifficulty(c))} ← {string.Join(", ", powering.Select(r => r.Growth > 0 ? $"{r.Name} +{r.Growth}" : r.Name))}"
                    : _session.IsTooLarge(c) ? $"<color=#ff9e38>{UiTexts.NotPoweredTooLarge}</color>" : $"<color=#ff6b61>{UiTexts.NotPowered}</color>";
                lines.Add($"#{i + 1} {skill} {c.Shape}{marks}  {power}");
            }
            if (lines.Count == 0) lines.Add(UiTexts.Hud.NoComponents);
            lines.Add($"↓ {UiTexts.FallbackLine}");
            return string.Join("\n", lines);
        }

        private HexCoord? _enemyCoord;
        private CellContent _enemyContent;
        private bool _enemyRaid;
        private string _enemyText = string.Empty;
        private readonly List<EnemyBoardPreview> _enemyPreviews = new List<EnemyBoardPreview>();

        /// <summary>
        /// Gegner eines Feldes mit ihren Platinen (A-19: auf der Karte lesbar), pro Feld einmal gebaut. Mögliche Gegner
        /// (Goldminen-Überfall) als Zeilen; der genaue Gegner eines Kampffeldes wird als Raster gezeichnet (<see cref="DrawExactEnemies"/>).
        /// </summary>
        private string EnemyBoards(HexCell cell)
        {
            if (_enemyCoord == cell.Coord && _enemyContent == cell.Content && _enemyRaid == cell.IsUnderAttack) return _enemyText;
            _enemyCoord = cell.Coord;
            _enemyContent = cell.Content;
            _enemyRaid = cell.IsUnderAttack;
            _enemyPreviews.Clear();
            _enemyPreviews.AddRange(_session.EnemyBoardsAt(cell.Coord));
            var blocks = new List<string>();
            foreach (EnemyBoardPreview enemy in _enemyPreviews)
                if (!IsDrawable(enemy)) blocks.Add(UiTexts.Hud.EnemyBoard(enemy.Name, string.Join("\n", enemy.Lines.Select(l => $"• {l}"))));
            bool exact = _enemyPreviews.Exists(e => e.IsExact);
            _enemyText = blocks.Count > 0 ? $"{(exact ? UiTexts.Hud.EnemyHere : UiTexts.Hud.PossibleEnemies)}\n{string.Join("\n", blocks)}" : string.Empty;
            return _enemyText;
        }

        /// <summary>Genauer Gegner mit zeichnenbaren Platinen (sonst bleiben die Zeilen als Ersatz).</summary>
        private static bool IsDrawable(EnemyBoardPreview enemy) =>
            enemy.IsExact && enemy.Fighters.Count > 0 && enemy.Fighters.All(f => EnemyBoardView.CanDraw(f.Board));

        /// <summary>
        /// Der Gegner, der genau hier wartet: Name, Platine jedes Kämpfers als kleines Raster (Hovern erklärt die Teile),
        /// darunter die Beute, die man nach einem Sieg bergen kann, und wie viele Teile.
        /// </summary>
        private void DrawExactEnemies()
        {
            foreach (EnemyBoardPreview enemy in _enemyPreviews)
            {
                if (!IsDrawable(enemy)) continue;
                GUILayout.Label($"<size=13>{UiTexts.Hud.EnemyHere} <b>{enemy.Name}</b></size>", _style);
                const float width = PanelWidth - 48f;
                foreach (CombatantSetup fighter in enemy.Fighters)
                {
                    if (enemy.Fighters.Count > 1) GUILayout.Label($"<size=13>{fighter.Name}</size>", _style);
                    float height = EnemyBoardView.Height(fighter.Board, width, 220f, 40f);
                    Rect area = GUILayoutUtility.GetRect(width, height);
                    EnemyBoardView.Draw(area, fighter.Board, max: 40f);
                }
                if (enemy.Loot.Count == 0) continue;
                string loot = string.Join(", ", enemy.Loot.Select(EnemyBoardView.LootText));
                GUILayout.Label($"<size=13>{UiTexts.Hud.Loot(loot)}\n<color=#ffd75e>{UiTexts.Hud.SalvageAfterVictory(enemy.LootPicks)}</color></size>", _style);
            }
        }

        /// <summary>
        /// Tag-Zähler «Ladung 3/4» (erreichte Schwelle hervorgehoben) und aktive Duos. Unentdeckte Duos bleiben «???»,
        /// bis sie im ersten Kampf auslösen.
        /// </summary>
        /// <summary>Maus über «Tags»: alle Stufen der getragenen Tags (aktive ●) und aktive Duos.</summary>
        private string TagTooltip()
        {
            var blocks = new List<string>();
            foreach (SynergyCounter c in _session.TagCounters()) blocks.Add(c.Tag.Describe(c.Count));
            foreach (SynergyDuo duo in _session.ActiveDuos())
                blocks.Add(_session.IsDuoDiscovered(duo.Id) ? UiTexts.Hud.Duo(duo.Name, duo.Effect.Text) : UiTexts.Hud.UnknownDuo);
            return string.Join("\n\n", blocks);
        }

        private string TagList()
        {
            var parts = new List<string>();
            foreach (SynergyCounter c in _session.TagCounters())
                parts.Add(c.Reached > 0 ? $"<color=#ffd75e>{c.Text}</color>" : c.Text);
            foreach (SynergyDuo duo in _session.ActiveDuos())
                parts.Add($"<color=#7ddc6f>{UiTexts.Hud.DuoName(_session.DuoName(duo))}</color>");
            return string.Join(", ", parts);
        }

        /// <summary>Getragene Sets mit Teilezahl; aktive Boni (ab 2 Teilen) hervorgehoben.</summary>
        private string SetList()
        {
            var parts = new List<string>();
            foreach ((SetDefinition set, int pieces) in _session.WornSets())
            {
                string text = $"{set.Name} {pieces}/{set.MaxPieces}";
                parts.Add(pieces >= SetDefinition.FirstBonusPieces ? $"<color=#ffd75e>{text}</color>" : text);
            }
            return string.Join(", ", parts);
        }

        private int _weapon;
        private int _weaponFrame = -1;

        /// <summary>Kurze Build-Übersicht: Waffenschaden, Stufen von Ausrüstung und Runen, Inventar.</summary>
        private string BuildSummary()
        {
            int itemLevels = 0;
            foreach (EquipmentDefinition item in _session.Gear.Items) itemLevels += item.Level;
            int runeLevels = 0;
            foreach (RelayChip relay in _session.Board.Relays) runeLevels += relay.Level;
            if (_weaponFrame != Time.frameCount)
            {
                _weapon = _session.SkillUserStats().WeaponDamage;
                _weaponFrame = Time.frameCount;
            }
            int weapon = _weapon;
            return UiTexts.Hud.BuildSummary(weapon, itemLevels, runeLevels, _session.Inventory.Count, _session.RuneInventory.Count);
        }

        private string GearList()
        {
            if (_session.Gear.Items.Count == 0) return UiTexts.Hud.Nothing;
            var names = new List<string>();
            foreach (EquipmentDefinition item in _session.Gear.Items) names.Add(item.Name);
            return string.Join(", ", names);
        }

        private string Describe(HexCell cell)
        {
            string text = DescribeContent(cell);
            return cell.IsResolved ? UiTexts.Hud.Resolved(text) : text;
        }

        private string DescribeContent(HexCell cell)
        {
            if (cell.Content == CellContent.Encounter && _encounters != null
                && _encounters.TryGet(cell.EncounterId, out EncounterDefinition encounter))
                return encounter.Title;

            switch (cell.Content)
            {
                case CellContent.Enemy: return UiTexts.Hud.Enemy;
                case CellContent.Boss: return UiTexts.Hud.Boss;
                case CellContent.Elite: return UiTexts.Hud.Elite;
                case CellContent.Shop: return UiTexts.Hud.Shop;
                case CellContent.Treasure: return UiTexts.Hud.Treasure;
                case CellContent.GoldMine: return UiTexts.Hud.GoldMine;
                default: return UiTexts.Hud.Start;
            }
        }
    }
}
