using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Bergen nach einem Sieg: oben die Platine des Gegners aus dem letzten Kampf, darunter die Teile, die er benutzt hat
    /// (Skill, Modul, Chip, Rune) als Karten mit Text und Knopf «Take». «Pick N more» zählt die Wahlen; «Leave the rest»
    /// verzichtet. Danach folgt die gewohnte Belohnungswahl. Fährt die Maus über eine Karte, ist das Teil auf der Platine
    /// golden umrandet. Öffnet sich nach der Arena (wie die Belohnung) und schliesst, sobald nichts mehr zu bergen ist.
    /// </summary>
    public sealed class SalvageWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private SalvageOffer _offer;
        private Vector2 _scroll;
        private int _hover = -1;
        private int _nextHover = -1;
        private GUIStyle _plainStyle;

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        /// <summary>Wartet ein Bergen und ist das Fenster sichtbar?</summary>
        public bool IsOpen => _session?.PendingSalvage != null && (Hidden == null || !Hidden());

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _offer = null;
            _hover = -1;
        }

        private void OnGUI()
        {
            UiTheme.Apply();
            if (Hidden != null && Hidden()) return;
            if (_session == null) return;

            SalvageOffer offer = _session.PendingSalvage;
            if (offer == null)
            {
                _offer = null;
                return;
            }
            if (offer != _offer)
            {
                _offer = offer;
                _scroll = Vector2.zero;
                _hover = -1;
            }
            EnsureStyles();
            if (Event.current.type == EventType.Layout)
            {
                _hover = _nextHover;
                _nextHover = -1;
            }

            float width = Mathf.Min(Screen.width - 40f, 720f);
            float height = Mathf.Min(Screen.height - 40f, 700f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            GUILayout.Label(UiTexts.Salvage.Title(offer.EnemyName), UiTheme.Title);
            GUILayout.Label($"<color=#ffd75e><b>{UiTexts.Salvage.PicksLeft(offer.PicksLeft)}</b></color>   <size=13><color=#9aa4b2>"
                + UiTexts.Offer.Status(_session.BoardSize, _session.Board.Relays.Count, _session.Inventory.Count, _session.Inventory.Capacity,
                    _session.RuneInventory.Count, _session.RuneInventory.Capacity) + "</color></size>", UiTheme.Text);

            EnemyLoot? focus = _hover >= 0 && _hover < offer.Parts.Count ? offer.Parts[_hover] : (EnemyLoot?)null;
            DrawBoards(width - 24f, height * 0.36f, focus);

            GUILayout.Space(4f);
            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < offer.Parts.Count; i++)
            {
                if (DrawPart(offer, i)) break;
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button(UiTexts.Salvage.LeaveRest, GUILayout.Height(30f))) _session.SkipSalvage();

            GUILayout.EndArea();
            UiTheme.DrawTooltip();
        }

        /// <summary>Platinen der Gegner aus dem letzten Kampf (nur Kämpfer mit Komponenten), sonst ein Hinweis.</summary>
        private void DrawBoards(float width, float maxHeight, EnemyLoot? focus)
        {
            List<FighterInfo> enemies = EnemiesOfLastFight();
            GUILayout.Label(UiTexts.Salvage.BoardTitle, UiTheme.Text);
            if (enemies.Count == 0)
            {
                GUILayout.Label(UiTexts.Salvage.NoBoard, UiTheme.Small);
                return;
            }
            float each = Mathf.Max(60f, maxHeight / enemies.Count);
            foreach (FighterInfo f in enemies)
            {
                LogicBoard board = f.Combatant.Board;
                if (enemies.Count > 1) GUILayout.Label($"<size=13>{f.Name}</size>", UiTheme.Small);
                float h = EnemyBoardView.Height(board, width, each, 48f);
                Rect area = GUILayoutUtility.GetRect(width, h);
                EnemyBoardView.Draw(area, board, focus: focus, max: 48f);
            }
        }

        /// <summary>Gegner des letzten Kampfes mit zeichnenbarer Platine und mindestens einer Komponente.</summary>
        private List<FighterInfo> EnemiesOfLastFight()
        {
            var list = new List<FighterInfo>();
            BattleResult battle = _session.LastCombat?.Battle;
            if (battle == null) return list;
            foreach (FighterInfo f in battle.Fighters)
            {
                LogicBoard board = f.Combatant?.Board;
                if (f.Side != Side.Player && board != null && board.Rows.Count > 0 && EnemyBoardView.CanDraw(board)) list.Add(f);
            }
            return list;
        }

        /// <summary>Eine Karte: Art, Name, Text mit Tooltip und «Take». True, wenn genommen wurde (Liste hat sich geändert).</summary>
        private bool DrawPart(SalvageOffer offer, int index)
        {
            EnemyLoot part = offer.Parts[index];
            Color kind = EnemyBoardView.KindColour(part.Kind);
            GUILayout.BeginVertical(UiTheme.Section);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b><color={UiTheme.Hex(kind)}>{EnemyBoardView.KindName(part.Kind)}</color></b>", UiTheme.Text, GUILayout.Width(70f));
            (string text, string tip) = Describe(part);
            GUILayout.Label(new GUIContent(text, tip), _plainStyle);
            GUI.enabled = _session.CanTakeSalvage(index);
            bool take = GUILayout.Button(new GUIContent(UiTexts.Salvage.Take, UiTexts.Salvage.TakeTip), GUILayout.Width(90f), GUILayout.Height(30f));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
            {
                Rect card = GUILayoutUtility.GetLastRect();
                UiTheme.Fill(new Rect(card.x, card.y + 3f, 3f, card.height - 6f), kind);
                if (card.Contains(Event.current.mousePosition)) _nextHover = index;
            }
            if (!take) return false;
            _session.TakeSalvage(index);
            return true;
        }

        /// <summary>Text und Tooltip eines Teils, wie in Belohnung und Shop.</summary>
        private (string text, string tip) Describe(EnemyLoot part)
        {
            switch (part.Kind)
            {
                case EnemyLootKind.Skill:
                    return (SkillText.Describe(_session, part.Id), null);
                case EnemyLootKind.Module:
                    return (ModuleText.Describe(_session, part.Id), null);
                case EnemyLootKind.Chip:
                    if (!_session.ChipCatalog.TryGet(part.Id, out ChipDefinition chip)) return ($"<b>{part.Name}</b>", null);
                    bool effect = EffectText.TryGet(chip.EffectId, out CircuitEffectDefinition fx);
                    return (UiTexts.Salvage.ChipLine(effect ? EffectText.Icon(fx) + " " : string.Empty, chip.Name, chip.Description),
                        effect ? EffectText.Tip(fx) : UiTexts.Circuit.ChipTip(chip.Name, chip.Description));
                default:
                    if (!_session.RuneCatalog.TryGet(part.Id, out RuneDefinition rune)) return ($"<b>{part.Name}</b>", null);
                    return (UiTexts.Salvage.RuneLine(RuneText.DifficultyBadge(rune), rune.Name, rune.Tag.DisplayName(), rune.Description),
                        RuneText.DifficultyTip(rune));
            }
        }

        private void EnsureStyles()
        {
            if (_plainStyle != null) return;
            _plainStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true, wordWrap = true };
        }
    }
}
