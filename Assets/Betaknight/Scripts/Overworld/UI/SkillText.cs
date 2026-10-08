using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Growth;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Texte und Auswahl für Skills in Belohnungs- und Shop-Fenstern.</summary>
    public static class SkillText
    {
        /// <summary>«<b>Schildstoss</b> [Schild] · Kennzahlen» plus Besitz: welche Exemplare man hat und wo sie sitzen.</summary>
        public static string Describe(OverworldSession session, string skillId, string suffix = "")
        {
            if (!session.SkillCatalog.TryGet(skillId, out SkillDefinition skill)) return skillId;
            SkillInfo info = session.DescribeSkill(skillId, null, 0);
            string kinds = SkillKinds.Names(skill.Kinds);
            string text = $"{UiTexts.Skill.Title(skill.Name)}{(kinds.Length > 0 ? $"  [{kinds}]" : string.Empty)}{suffix}\n<size=13>{skill.Description}";
            if (info != null) text += $"\n<color=#ffd75e>{info.Summary}</color>";
            GrowthRule rule = session.GrowthCatalog.ForSkill(skillId);
            if (rule != null) text += $"\n<color=#b5e48c>{UiTexts.Grows(rule.Text)}</color>";
            string owned = Owned(session, skillId);
            if (owned.Length > 0) text += $"\n<color=#9fc7ff>{UiTexts.Owned(owned)}</color>";
            text += RuneText.Eases(session, skillId);
            text += EvolutionHints(session.EvolutionHintsForSkill(skillId));
            return text + "</size>";
        }

        /// <summary>Evolutions-Fortschritt als violette Zeilen («Evolution ???: fehlt Modul Fläche»), leer ohne Hinweis.</summary>
        public static string EvolutionHints(IEnumerable<string> hints)
        {
            string text = string.Empty;
            foreach (string hint in hints) text += $"\n<color=#d29bff>{hint}</color>";
            return text;
        }

        /// <summary>«Shield Bash +1 (#2 on board), Shield Bash (free)» oder leer.</summary>
        public static string Owned(OverworldSession session, string skillId)
        {
            var parts = new List<string>();
            foreach (SkillInstance s in session.Skills.OfSkill(skillId)) parts.Add($"{s.NameFrom(session.SkillCatalog)} ({Where(session, s)})");
            return string.Join(", ", parts);
        }

        public static string Where(OverworldSession session, SkillInstance skill)
        {
            if (skill.Holder is ComponentSlot component) return UiTexts.OnBoard(session.Board.IndexOf(component) + 1);
            return skill.Holder != null ? skill.Holder.HolderName : UiTexts.Free;
        }

        /// <summary>
        /// Knöpfe für einen angebotenen Skill. Besitzt man ihn schon und kann er steigen: «Wachstum +5» oder
        /// «Zweites Exemplar». Sonst ein Knopf zum Nehmen. Ruft <paramref name="take"/> mit der Wahl auf.
        /// </summary>
        public static void DrawChoice(OverworldSession session, string skillId, bool enabled, string takeLabel, Action<SkillDuplicateChoice> take)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = enabled;
            SkillInstance target = session.UpgradeTarget(skillId);
            if (target != null)
            {
                string from = target.NameFrom(session.SkillCatalog);
                int grown = target.Growth + GrowthStages.DuplicateGrowth;
                string tip = UiTexts.Skill.GrowthTip(from, Where(session, target), GrowthStages.DuplicateGrowth, session.MilestoneText(grown, true));
                if (GUILayout.Button(new GUIContent(UiTexts.Skill.Growth(GrowthStages.DuplicateGrowth, from, grown), tip), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.Upgrade);
                if (GUILayout.Button(new GUIContent(UiTexts.Skill.SecondCopy, UiTexts.Skill.SecondCopyTip), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.KeepCopy);
            }
            else if (GUILayout.Button(session.OwnsSkill(skillId) ? UiTexts.Skill.TakeAnother(takeLabel) : takeLabel, GUILayout.Height(28f)))
            {
                take(SkillDuplicateChoice.KeepCopy);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
