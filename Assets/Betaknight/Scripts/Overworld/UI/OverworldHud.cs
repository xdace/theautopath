using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Circuit;
using Betaknight.Core.Hex;
using Betaknight.Core.Encounters;
using Betaknight.Core.Map;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Overworld.Controllers;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Schlichtes Debug-HUD per IMGUI (kein Canvas-Setup nötig).
    /// Wird ersetzt, sobald das echte UI kommt.
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
        }

        private const float PanelWidth = 380f;
        private const float ButtonBarHeight = 4 * 30f + 16f;

        /// <summary>Info-Bereich links oben; scrollt, wenn der Inhalt zu lang wird.</summary>
        private static Rect InfoRect => new Rect(12f, 12f, PanelWidth, Mathf.Max(160f, Screen.height - 32f - ButtonBarHeight));

        /// <summary>Feste Knopfleiste unter dem Info-Bereich, scrollt nie mit.</summary>
        private static Rect ButtonRect => new Rect(12f, InfoRect.yMax + 8f, PanelWidth, ButtonBarHeight);

        private Vector2 _scroll;

        /// <summary>Liegt ein Bildschirmpunkt (Ursprung unten links) über dem HUD? Dann ignoriert die Karte den Klick.</summary>
        public static bool ContainsScreenPoint(Vector2 screen)
        {
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return InfoRect.Contains(guiPoint) || ButtonRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            if (_session == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
            }

            GUILayout.BeginArea(InfoRect, GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);
            string kit = _session.Kit != null ? $" – {_session.Kit.Name}" : string.Empty;
            GUILayout.Label(UiTexts.Hud.Title(kit, _session.Act), _style);
            int boss = _session.TurnsUntilBoss;
            string bossText = boss <= 3 ? $"<color=#ff7a6b>{UiTexts.Hud.BossIn(boss)}</color>" : UiTexts.Hud.BossIn(boss);
            GUILayout.Label(UiTexts.Hud.Turn(_session.Turns.CurrentTurn, bossText), _style);
            GUILayout.Label(UiTexts.Hud.Resources(_session.Stats.Hp, _session.Stats.MaxHp, _session.Stats.Gold, _session.Stats.Shards), _style);
            GUILayout.Label(UiTexts.Hud.Board(_session.BoardSize, _session.MaxBoardSize, _session.Board.Relays.Count, _session.Board.Components.Count, BoardList()), _style);
            GUILayout.Label($"<size=13>{BuildSummary()}</size>", _style);
            GUILayout.Label(UiTexts.Hud.Gear(GearList()), _style);
            string sets = SetList();
            if (sets.Length > 0)
            {
                // Maus darüber: was jedes getragene Set bei 2 und 3 Teilen bewirkt.
                var tip = new List<string>();
                foreach ((SetDefinition set, int pieces) in _session.WornSets()) tip.Add(set.Describe(pieces));
                GUILayout.Label(new GUIContent(UiTexts.Hud.Sets(sets), string.Join("\n\n", tip)), _style);
            }
            string tags = TagList();
            if (tags.Length > 0) GUILayout.Label(new GUIContent(UiTexts.Hud.Tags(tags), TagTooltip()), _style);
            foreach (MineRaid raid in _session.Raids)
            {
                string state = raid.IsLost ? UiTexts.Hud.MineLost : UiTexts.Hud.MineAttacked(raid.TurnsLeft(_session.Turns.CurrentTurn));
                GUILayout.Label($"<color=#ff7a6b>{UiTexts.Hud.Mine(raid.Coord.ToString(), state)}</color>", _style);
            }
            GUILayout.Label(UiTexts.Hud.Position(_session.Player.Position.ToString()), _style);
            GUILayout.Label(UiTexts.Hud.Tile(Describe(_session.CurrentCell)), _style);
            GUILayout.Label(UiTexts.Hud.Seed(_session.Map.Seed), _style);

            if (_controller != null && _controller.HoveredCoord.HasValue
                && _session.Map.TryGetCell(_controller.HoveredCoord.Value, out HexCell hovered))
            {
                string info = hovered.IsContentKnown ? Describe(hovered) : UiTexts.Hud.Unknown;
                string enemies = hovered.IsContentKnown && !hovered.IsResolved ? EnemyBoards(hovered.Coord) : string.Empty;
                GUILayout.Label(UiTexts.Hud.Pointer(hovered.Coord.ToString(), info), _style);
                if (enemies.Length > 0) GUILayout.Label($"<size=13>{enemies}</size>", _style);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            GUILayout.BeginArea(ButtonRect, GUI.skin.box);
            if (OnOpenBuild != null && !_session.IsGameOver && GUILayout.Button(new GUIContent(UiTexts.Hud.BuildButton, UiTexts.Hud.BuildTip)))
            {
                OnOpenBuild();
            }
            if (OnOpenInventory != null && !_session.IsGameOver
                && GUILayout.Button(new GUIContent(UiTexts.Hud.InventoryButton(_session.Inventory.Count, _session.Inventory.Capacity), UiTexts.Hud.InventoryTip)))
            {
                OnOpenInventory();
            }
            if (_session.CanOpenShop && GUILayout.Button(UiTexts.Hud.OpenShop))
            {
                _session.OpenShop();
            }
            if (_onNewMap != null && GUILayout.Button(UiTexts.Hud.NewRun))
            {
                _onNewMap();
            }
            GUILayout.EndArea();
            UiTheme.DrawTooltip();
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
        private string _enemyText = string.Empty;

        /// <summary>Mögliche Gegner eines Feldes mit ihren Platinen (A-19: auf der Karte lesbar), pro Feld einmal gebaut.</summary>
        private string EnemyBoards(HexCoord coord)
        {
            if (_enemyCoord == coord) return _enemyText;
            _enemyCoord = coord;
            var blocks = new List<string>();
            foreach (EnemyBoardPreview enemy in _session.EnemyBoardsAt(coord))
                blocks.Add(UiTexts.Hud.EnemyBoard(enemy.Name, string.Join("\n", enemy.Lines.Select(l => $"• {l}"))));
            _enemyText = blocks.Count > 0 ? $"{UiTexts.Hud.PossibleEnemies}\n{string.Join("\n", blocks)}" : string.Empty;
            return _enemyText;
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
