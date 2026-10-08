using System;
using Betaknight.Core;
using Betaknight.Core.Map;
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
        private Action _onNewMap;
        private GUIStyle _style;

        public void Initialize(OverworldSession session, OverworldController controller, Action onNewMap)
        {
            _session = session;
            _controller = controller;
            _onNewMap = onNewMap;
        }

        private static readonly Rect PanelRect = new Rect(12, 12, 340, 230);

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
                _style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
            }

            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label($"<b>Betaknight – Oberwelt</b>", _style);
            GUILayout.Label($"Zug: {_session.Turns.CurrentTurn}", _style);
            GUILayout.Label($"Position: {_session.Player.Position}", _style);
            GUILayout.Label($"Feld: {Describe(_session.CurrentCell.Content)}", _style);
            GUILayout.Label($"Seed: {_session.Map.Seed}", _style);

            if (_controller != null && _controller.HoveredCoord.HasValue
                && _session.Map.TryGetCell(_controller.HoveredCoord.Value, out HexCell hovered))
            {
                string info = hovered.Visibility == CellVisibility.Explored ? Describe(hovered.Content) : "unbekannt";
                GUILayout.Label($"Zeiger: {hovered.Coord} – {info}", _style);
            }

            GUILayout.FlexibleSpace();
            if (_onNewMap != null && GUILayout.Button("Neue Karte"))
            {
                _onNewMap();
            }
            GUILayout.EndArea();
        }

        private static string Describe(CellContent content)
        {
            switch (content)
            {
                case CellContent.Enemy: return "Gegner";
                case CellContent.Boss: return "Boss";
                case CellContent.Shop: return "Shop";
                case CellContent.Treasure: return "Schatztruhe";
                case CellContent.GoldMine: return "Goldmine";
                default: return "leer";
            }
        }
    }
}
