using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Fenster für eine Belohnung: eine Rune, ein Teil, einen Skill oder ein Modul nehmen oder verzichten. Ein doppelter Skill
    /// steigt eine Stufe oder bleibt als zweites Exemplar. Teile werden angelegt oder kommen
    /// ins Inventar; bei voller Tafel kommt eine Rune ins Runen-Inventar oder tauscht eine Zeile (die alte Rune wandert
    /// ins Inventar). Nichts geht verloren.
    /// </summary>
    public sealed class RuneOfferWindow : MonoBehaviour
    {
        private OverworldSession _session;
        private RuneOffer _offer;
        private int _choiceAwaitingSlot = -1;
        private GUIStyle _titleStyle;
        private GUIStyle _nameStyle;
        private GUIStyle _textStyle;
        private GUIStyle _plainStyle;

        public void Initialize(OverworldSession session)
        {
            _session = session;
            _offer = null;
            _choiceAwaitingSlot = -1;
        }

        /// <summary>Solange true, bleibt das Fenster verborgen (z. B. während die Arena läuft).</summary>
        public System.Func<bool> Hidden;

        private void OnGUI()
        {
            if (Hidden != null && Hidden()) return;
            if (_session == null) return;

            RuneOffer offer = _session.PendingRuneOffer;
            if (offer == null) return;
            if (offer != _offer)
            {
                _offer = offer;
                _choiceAwaitingSlot = -1;
            }

            EnsureStyles();

            const float width = 520f;
            float height = Mathf.Min(Screen.height - 40f, 600f);
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            GUILayout.BeginArea(rect, GUI.skin.box);

            if (_choiceAwaitingSlot < 0) DrawOptions(offer);
            else DrawReplace(offer.Options[_choiceAwaitingSlot]);

            GUILayout.EndArea();
        }

        private void DrawOptions(RuneOffer offer)
        {
            GUILayout.Label($"<b>Runenwahl</b> – {offer.Source}", _titleStyle);
            GUILayout.Label($"Tafel: {_session.Runes.Runes.Count}/{_session.Runes.Slots}   Inventar: {_session.Inventory.Count}/{_session.Inventory.Capacity} Teile, "
                + $"{_session.RuneInventory.Count}/{_session.RuneInventory.Capacity} Runen", _textStyle);
            GUILayout.Space(6f);

            IReadOnlyList<RuneDefinition> options = offer.Options;
            for (int i = 0; i < options.Count; i++)
            {
                RuneDefinition rune = options[i];
                if (_session.OwnsRune(rune))
                {
                    // Doppelte Rune: die vorhandene steigt eine Stufe.
                    string hints = SkillText.EvolutionHints(_session.EvolutionHintsForRune(rune.Id));
                    string upgrade = $"<color=#7ddc6f>▲ Stufe erhöhen</color>  <b>{RuneLevelText(rune)}</b>\n{rune.Description}{hints}";
                    GUI.enabled = _session.CanUpgradeRune(rune);
                    if (GUILayout.Button(upgrade, _nameStyle, GUILayout.Height(RuneHeight(hints)))) _session.TakeRune(i);
                    GUI.enabled = true;
                    continue;
                }

                bool synergy = _session.Runes.HasTag(rune.Tag);
                string runeHints = SkillText.EvolutionHints(_session.EvolutionHintsForRune(rune.Id));
                string label = $"<b>{rune.Name}</b>  [{rune.Tag.DisplayName()}]{(synergy ? "  ★" : string.Empty)}\n{rune.Description}{runeHints}";
                if (GUILayout.Button(label, _nameStyle, GUILayout.Height(RuneHeight(runeHints))))
                {
                    if (_session.Runes.IsFull) _choiceAwaitingSlot = i;
                    else _session.TakeRune(i);
                }
            }

            for (int i = 0; i < offer.ItemIds.Count; i++)
            {
                if (!_session.Items.TryGet(offer.ItemIds[i], out EquipmentDefinition item)) continue;
                EquipmentDefinition worn = _session.Gear.Get(item.Slot);
                string set = item.SetId != null ? $"  Set: {_session.Sets.NameOf(item.SetId)} ({_session.Gear.SetPieces(item.SetId)}/3)" : string.Empty;
                if (_session.CanUpgradeItem(item.Id))
                {
                    // Doppeltes Teil: das vorhandene wird aufgewertet (Werte und passive Effekte).
                    string where = worn != null && worn.Id == item.Id ? "angelegt" : "im Inventar";
                    string text = $"<color=#7ddc6f>▲ Aufwerten</color>  <b>{item.BaseName}</b> ({where}) → Stufe +{OwnedLevel(item) + 1}\n{ItemText.Describe(item)}";
                    if (GUILayout.Button(text, _nameStyle, GUILayout.Height(64f))) _session.TakeItem(i);
                    continue;
                }
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"<b>{item.Name}</b>  [{item.Slot.DisplayName()}]{set}\n{ItemText.Describe(item)}\n<size=12>{ItemText.Compare(item, worn)}  {ItemText.TagPreview(_session, item)}{SkillText.EvolutionHints(_session.EvolutionHintsForItem(item))}</size>", _plainStyle);
                GUILayout.BeginHorizontal();
                GUI.enabled = _session.CanTakeItem(i, ItemPlacement.Equip);
                string equip = worn != null ? $"Anlegen ({worn.Name} ins Inventar)" : "Anlegen";
                if (GUILayout.Button(equip, GUILayout.Height(28f))) _session.TakeItem(i, ItemPlacement.Equip);
                GUI.enabled = _session.CanTakeItem(i, ItemPlacement.Inventory);
                if (GUILayout.Button("Ins Inventar", GUILayout.Height(28f))) _session.TakeItem(i, ItemPlacement.Inventory);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }

            for (int i = 0; i < offer.SkillIds.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(SkillText.Describe(_session, offer.SkillIds[i]), _plainStyle);
                SkillText.DrawChoice(_session, offer.SkillIds[i], _session.CanTakeSkill(i), "Skill nehmen (frei in die Sammlung)",
                    choice => _session.TakeSkill(index, choice));
                GUILayout.EndVertical();
            }

            for (int i = 0; i < offer.ModuleIds.Count; i++)
            {
                int index = i;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(ModuleText.Describe(_session, offer.ModuleIds[i], "  <color=#ffd75e>selten</color>"), _plainStyle);
                ModuleText.DrawChoice(_session, offer.ModuleIds[i], _session.CanTakeModule(i), "Modul nehmen (frei in die Sammlung)",
                    choice => _session.TakeModule(index, choice));
                GUILayout.EndVertical();
            }

            if (offer.BoardExpansion)
            {
                GUI.enabled = _session.CanExpandBoard;
                string text = $"<color=#7ddc6f>▲ Tafel-Erweiterung: +1 Zeile</color>  ({_session.Runes.Slots} → {_session.Runes.Slots + 1} von {_session.Progression.MaxBoardRows})";
                if (GUILayout.Button(text, _nameStyle, GUILayout.Height(44f))) _session.TakeBoardExpansion();
                GUI.enabled = true;
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"Verzichten (+{OverworldSession.SkipRuneGold} Gold)", GUILayout.Height(30f)))
            {
                _session.SkipRuneOffer();
            }
        }

        /// <summary>Knopfhöhe einer Rune: eine Zeile mehr pro Evolutions-Hinweis.</summary>
        private static float RuneHeight(string hints) => 64f + 18f * (hints.Split('\n').Length - 1);

        private string RuneLevelText(RuneDefinition rune)
        {
            int row = _session.Runes.IndexOf(rune);
            int level = row >= 0 ? _session.Runes.Rows[row].Level : _session.RuneInventory[_session.RuneInventory.IndexOf(rune)].Level;
            return level < rune.MaxLevel ? $"{rune.NameAt(level)} → {rune.NameAt(level + 1)}" : $"{rune.NameAt(level)} (höchste Stufe)";
        }

        private int OwnedLevel(EquipmentDefinition item)
        {
            EquipmentDefinition worn = _session.Gear.Get(item.Slot);
            if (worn != null && worn.Id == item.Id) return worn.Level;
            int index = _session.Inventory.IndexOf(item.Id);
            return index >= 0 ? _session.Inventory[index].Level : 0;
        }

        private void DrawReplace(RuneDefinition incoming)
        {
            GUILayout.Label($"<b>{incoming.Name}</b>: Tafel ist voll", _titleStyle);
            GUILayout.Space(6f);

            if (GUILayout.Button("Ins Runen-Inventar legen", GUILayout.Height(32f)))
            {
                _session.TakeRune(_choiceAwaitingSlot);
                _choiceAwaitingSlot = -1;
                return;
            }
            GUILayout.Label("… oder eine Zeile tauschen (die alte Rune wandert mit ihrer Stufe ins Inventar, der Skill bleibt):", _plainStyle);

            IReadOnlyList<RuneDefinition> equipped = _session.Runes.Runes;
            for (int slot = 0; slot < equipped.Count; slot++)
            {
                RuneDefinition rune = equipped[slot];
                RuneSlot row = _session.Runes.Rows[slot];
                if (GUILayout.Button($"statt <b>{row.Name}</b>  [{rune.Tag.DisplayName()}]\n{row.Description}", _nameStyle, GUILayout.Height(56f)))
                {
                    _session.TakeRune(_choiceAwaitingSlot, slot);
                    _choiceAwaitingSlot = -1;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Zurück", GUILayout.Height(30f))) _choiceAwaitingSlot = -1;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, richText = true };
            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _plainStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, wordWrap = true };
            _nameStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 6, 6),
            };
        }
    }
}
