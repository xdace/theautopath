using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
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
            string text = $"<b>Skill: {skill.Name}</b>{(kinds.Length > 0 ? $"  [{kinds}]" : string.Empty)}{suffix}\n<size=13>{skill.Description}";
            if (info != null) text += $"\n<color=#ffd75e>{info.Summary}</color>";
            GrowthRule rule = session.GrowthCatalog.ForSkill(skillId);
            if (rule != null) text += $"\n<color=#b5e48c>Wächst: {rule.Text}</color>";
            string owned = Owned(session, skillId);
            if (owned.Length > 0) text += $"\n<color=#9fc7ff>Besitzt: {owned}</color>";
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

        /// <summary>«Schildstoss +1 (Zeile 2), Schildstoss (frei)» oder leer.</summary>
        public static string Owned(OverworldSession session, string skillId)
        {
            var parts = new List<string>();
            foreach (SkillInstance s in session.Skills.OfSkill(skillId)) parts.Add($"{s.NameFrom(session.SkillCatalog)} ({Where(session, s)})");
            return string.Join(", ", parts);
        }

        public static string Where(OverworldSession session, SkillInstance skill)
        {
            if (skill.Holder is RuneSlot row) return $"Zeile {session.Runes.IndexOfRow(row) + 1}";
            return skill.Holder != null ? skill.Holder.HolderName : "frei";
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
                string tip = $"{from} ({Where(session, target)}) wächst um {GrowthStages.DuplicateGrowth}: {session.MilestoneText(grown, true)}";
                if (GUILayout.Button(new GUIContent($"▲ Wachstum +{GrowthStages.DuplicateGrowth} ({from} → +{grown})", tip), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.Upgrade);
                if (GUILayout.Button(new GUIContent("Zweites Exemplar", "Ein weiteres Exemplar mit Wachstum 0, z. B. für eine zweite Zeile"), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.KeepCopy);
            }
            else if (GUILayout.Button(session.OwnsSkill(skillId) ? $"{takeLabel} (weiteres Exemplar)" : takeLabel, GUILayout.Height(28f)))
            {
                take(SkillDuplicateChoice.KeepCopy);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
