using System;
using System.Collections.Generic;
using Betaknight.Core;
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

        /// <summary>Öffnet den Tafel-Editor. Ohne Zuweisung gibt es keinen Knopf.</summary>
        public Action OnEditBoard;

        /// <summary>Öffnet das Inventar. Ohne Zuweisung gibt es keinen Knopf.</summary>
        public Action OnOpenInventory;

        public void Initialize(OverworldSession session, OverworldController controller, EncounterCatalog encounters, Action onNewMap)
        {
            _session = session;
            _controller = controller;
            _encounters = encounters;
            _onNewMap = onNewMap;
        }

        private static readonly Rect PanelRect = new Rect(12, 12, 380, 540);

        /// <summary>Liegt ein Bildschirmpunkt (Ursprung unten links) über dem HUD? Dann ignoriert die Karte den Klick.</summary>
        public static bool ContainsScreenPoint(Vector2 screen)
        {
            var guiPoint = new Vector2(screen.x, Screen.height - screen.y);
            return PanelRect.Contains(guiPoint);
        }

        private void OnGUI()
        {
            if (_session == null) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true, wordWrap = true };
            }

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            string kit = _session.Kit != null ? $" – {_session.Kit.Name}" : string.Empty;
            GUILayout.Label($"<b>Betaknight{kit}</b>   Akt {_session.Act}", _style);
            int boss = _session.TurnsUntilBoss;
            string bossText = boss <= 3 ? $"<color=#ff7a6b>Boss in {boss} Zügen</color>" : $"Boss in {boss} Zügen";
            GUILayout.Label($"Zug: {_session.Turns.CurrentTurn}   {bossText}", _style);
            GUILayout.Label($"HP: {_session.Stats.Hp}/{_session.Stats.MaxHp}   Gold: {_session.Stats.Gold}   Splitter: {_session.Stats.Shards}", _style);
            GUILayout.Label($"Logik-Tafel: {_session.Runes.Rows.Count} Runen, Zeilen {_session.Runes.Slots}/{_session.Progression.MaxBoardRows}\n{BoardList()}", _style);
            GUILayout.Label($"<size=13>{BuildSummary()}</size>", _style);
            GUILayout.Label($"Ausrüstung: {GearList()}", _style);
            string sets = SetList();
            if (sets.Length > 0) GUILayout.Label($"Sets: {sets}", _style);
            foreach (MineRaid raid in _session.Raids)
            {
                string state = raid.IsLost ? "verloren" : $"angegriffen, noch {raid.TurnsLeft(_session.Turns.CurrentTurn)} Züge";
                GUILayout.Label($"<color=#ff7a6b>Mine {raid.Coord}: {state}</color>", _style);
            }
            GUILayout.Label($"Position: {_session.Player.Position}", _style);
            GUILayout.Label($"Feld: {Describe(_session.CurrentCell)}", _style);
            GUILayout.Label($"Seed: {_session.Map.Seed}", _style);

            if (_controller != null && _controller.HoveredCoord.HasValue
                && _session.Map.TryGetCell(_controller.HoveredCoord.Value, out HexCell hovered))
            {
                string info = hovered.IsContentKnown ? Describe(hovered) : "unbekannt";
                GUILayout.Label($"Zeiger: {hovered.Coord} – {info}", _style);
            }

            GUILayout.FlexibleSpace();
            if (OnEditBoard != null && !_session.IsBusy && !_session.IsGameOver && GUILayout.Button("Tafel bearbeiten"))
            {
                OnEditBoard();
            }
            if (OnOpenInventory != null && !_session.IsGameOver
                && GUILayout.Button($"Inventar ({_session.Inventory.Count}/{_session.Inventory.Capacity})"))
            {
                OnOpenInventory();
            }
            if (_session.CanOpenShop && GUILayout.Button("Shop öffnen"))
            {
                _session.OpenShop();
            }
            if (_onNewMap != null && GUILayout.Button("Neuer Run"))
            {
                _onNewMap();
            }
            GUILayout.EndArea();
        }

        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        private string BoardList()
        {
            var lines = new List<string>();
            for (int i = 0; i < _session.Runes.Rows.Count; i++)
            {
                RuneSlot row = _session.Runes.Rows[i];
                bool orphaned = !_session.Gear.ProvidesSkill(row.SkillId);
                string skill = orphaned ? "<color=#888888>—</color>" : SkillName(row.SkillId);
                lines.Add($"{i + 1}. [{row.Name}] → {skill}");
            }
            lines.Add("↓ [Immer] → Basisangriff");
            return string.Join("\n", lines);
        }

        private static string SkillName(string id) => Skills.TryGet(id, out SkillDefinition skill) ? skill.Name : id;

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
            foreach (RuneSlot row in _session.Runes.Rows) runeLevels += row.Level;
            if (_weaponFrame != Time.frameCount)
            {
                _weapon = _session.SkillUserStats().WeaponDamage;
                _weaponFrame = Time.frameCount;
            }
            int weapon = _weapon;
            return $"Build: Waffenschaden {weapon}, Ausrüstung +{itemLevels}, Runen-Stufen +{runeLevels}, "
                + $"Inventar {_session.Inventory.Count} Teile / {_session.RuneInventory.Count} Runen";
        }

        private string GearList()
        {
            if (_session.Gear.Items.Count == 0) return "nichts";
            var names = new List<string>();
            foreach (EquipmentDefinition item in _session.Gear.Items) names.Add(item.Name);
            return string.Join(", ", names);
        }

        private string Describe(HexCell cell)
        {
            string text = DescribeContent(cell);
            return cell.IsResolved ? $"{text} (erledigt)" : text;
        }

        private string DescribeContent(HexCell cell)
        {
            if (cell.Content == CellContent.Encounter && _encounters != null
                && _encounters.TryGet(cell.EncounterId, out EncounterDefinition encounter))
                return encounter.Title;

            switch (cell.Content)
            {
                case CellContent.Enemy: return "Gegner";
                case CellContent.Boss: return "Boss";
                case CellContent.Elite: return "Elite-Gegner";
                case CellContent.Shop: return "Shop";
                case CellContent.Treasure: return "Schatztruhe";
                case CellContent.GoldMine: return "Goldmine";
                default: return "Start";
            }
        }
    }
}
