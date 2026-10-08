using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Arena;
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
            string text = $"<b>Skill: {skill.Name}</b>{(kinds.Length > 0 ? $"  [{kinds}]" : string.Empty)}{suffix}\n<size=12>{skill.Description}";
            if (info != null) text += $"\n<color=#ffd75e>{info.Summary}</color>";
            string owned = Owned(session, skillId);
            if (owned.Length > 0) text += $"\n<color=#9fc7ff>Besitzt: {owned}</color>";
            return text + "</size>";
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
        /// Knöpfe für einen angebotenen Skill. Besitzt man ihn schon und kann er steigen: «Stufe erhöhen» oder
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
                string tip = $"{from} ({Where(session, target)}) steigt auf Stufe +{target.Level + 1}";
                if (GUILayout.Button(new GUIContent($"▲ Stufe erhöhen ({from} → +{target.Level + 1})", tip), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.Upgrade);
                if (GUILayout.Button(new GUIContent("Zweites Exemplar", "Ein weiteres Exemplar auf Stufe 0, z. B. für eine zweite Zeile"), GUILayout.Height(28f)))
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
