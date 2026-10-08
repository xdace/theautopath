using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Encounters;
using Betaknight.Core.Map;
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

        public void Initialize(OverworldSession session, OverworldController controller, EncounterCatalog encounters, Action onNewMap)
        {
            _session = session;
            _controller = controller;
            _encounters = encounters;
            _onNewMap = onNewMap;
        }

        private static readonly Rect PanelRect = new Rect(12, 12, 360, 360);

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
            GUILayout.Label($"<b>Betaknight{kit}</b>", _style);
            GUILayout.Label($"Zug: {_session.Turns.CurrentTurn}", _style);
            GUILayout.Label($"HP: {_session.Stats.Hp}/{_session.Stats.MaxHp}   Gold: {_session.Stats.Gold}   Splitter: {_session.Stats.Shards}", _style);
            GUILayout.Label($"Runen ({_session.Runes.Runes.Count}/{_session.Runes.Slots}): {RuneList()}", _style);
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

        private string RuneList()
        {
            if (_session.Runes.Runes.Count == 0) return "keine";
            var names = new List<string>();
            foreach (RuneDefinition rune in _session.Runes.Runes) names.Add(rune.Name);
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
                case CellContent.Shop: return "Shop";
                case CellContent.Treasure: return "Schatztruhe";
                case CellContent.GoldMine: return "Goldmine";
                default: return "Start";
            }
        }
    }
}
