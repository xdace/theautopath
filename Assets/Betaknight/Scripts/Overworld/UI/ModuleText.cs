using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Modules;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Texte und Auswahl für Module in Tafel-Editor, Inventar, Belohnungs- und Shop-Fenstern.</summary>
    public static class ModuleText
    {
        public static string KindName(ModuleKind kind)
        {
            switch (kind)
            {
                case ModuleKind.Skill: return "Skill-Modul";
                case ModuleKind.Block: return "Baustein-Modul";
                case ModuleKind.Trigger: return "Auslöser";
                default: return kind.ToString();
            }
        }

        /// <summary>«<b>Modul: Kette</b> [Skill-Modul]», Wirkung und Besitz.</summary>
        public static string Describe(OverworldSession session, string moduleId, string suffix = "")
        {
            if (!session.ModuleCatalog.TryGet(moduleId, out ModuleDefinition module)) return moduleId;
            string text = $"<b>Modul: {module.Name}</b>  [{KindName(module.Kind)}]{suffix}\n<size=12>{module.Description}";
            string owned = Owned(session, moduleId);
            if (owned.Length > 0) text += $"\n<color=#9fc7ff>Besitzt: {owned}</color>";
            text += SkillText.EvolutionHints(session.EvolutionHintsForModule(moduleId));
            return text + "</size>";
        }

        /// <summary>«Kette +1 (Skill Bohrstoß (Zeile 2)), Kette (frei)» oder leer.</summary>
        public static string Owned(OverworldSession session, string moduleId)
        {
            var parts = new List<string>();
            foreach (ModuleInstance m in session.Modules.OfModule(moduleId)) parts.Add($"{m.NameFrom(session.ModuleCatalog)} ({session.ModuleWhere(m)})");
            return string.Join(", ", parts);
        }

        /// <summary>Kurzname mit Wirkung als Tooltip; Auslöser mit ihrem Ziel.</summary>
        public static GUIContent Chip(OverworldSession session, ModuleInstance module)
        {
            ModuleDefinition d = session.ModuleDefinitionOf(module);
            string name = module.NameFrom(session.ModuleCatalog);
            string tip = d != null ? d.DescriptionAt(module.Level) : string.Empty;
            if (module.ModuleId == ModuleIds.Trigger) return new GUIContent($"↪ {session.DescribeTrigger(module)}", tip);
            return new GUIContent(name, tip);
        }

        /// <summary>Knöpfe für ein angebotenes Modul: «Stufe erhöhen» oder «Weiteres Exemplar», sonst Nehmen.</summary>
        public static void DrawChoice(OverworldSession session, string moduleId, bool enabled, string takeLabel, Action<SkillDuplicateChoice> take)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = enabled;
            ModuleInstance target = session.ModuleUpgradeTarget(moduleId);
            if (target != null)
            {
                string from = target.NameFrom(session.ModuleCatalog);
                if (GUILayout.Button(new GUIContent($"▲ Stufe erhöhen ({from} → +{target.Level + 1})", $"{from} ({session.ModuleWhere(target)}) wird stärker"),
                        GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.Upgrade);
                if (GUILayout.Button(new GUIContent("Weiteres Exemplar", "Ein zweites Modul auf Stufe 0 für einen anderen Platz"), GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.KeepCopy);
            }
            else if (GUILayout.Button(takeLabel, GUILayout.Height(28f)))
            {
                take(SkillDuplicateChoice.KeepCopy);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }
}
