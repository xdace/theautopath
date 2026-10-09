using System;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Circuit;
using Betaknight.Core.Modules;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Texte und Auswahl für Module im Build-Fenster, Inventar, Belohnungs- und Shop-Fenstern.</summary>
    public static class ModuleText
    {
        public static string KindName(ModuleKind kind)
        {
            switch (kind)
            {
                case ModuleKind.Skill: return UiTexts.Module.SkillKind;
                case ModuleKind.Block: return UiTexts.Module.BlockKind;
                case ModuleKind.Trigger: return UiTexts.Module.TriggerKind;
                default: return kind.ToString();
            }
        }

        /// <summary>«<b>Modul: Kette</b> [Skill-Modul]», Wirkung und Besitz.</summary>
        public static string Describe(OverworldSession session, string moduleId, string suffix = "")
        {
            if (!session.ModuleCatalog.TryGet(moduleId, out ModuleDefinition module)) return moduleId;
            string text = $"{UiTexts.Module.Title(module.Name, KindName(module.Kind))}{suffix}\n<size=13>{module.Description}";
            // A-21: Effekt-Modul mit Symbol und Farbe aus den Daten.
            if (EffectText.TryGet(moduleId, out CircuitEffectDefinition effect))
                text += $"\n{UiTexts.Effects.ModuleLine(EffectText.Icon(effect), $"<color={effect.Colour}><b>{effect.Name}</b></color>")}";
            string owned = Owned(session, moduleId);
            if (owned.Length > 0) text += $"\n<color=#9fc7ff>{UiTexts.Owned(owned)}</color>";
            text += RuneText.Eases(session, moduleId);
            text += SkillText.EvolutionHints(session.EvolutionHintsForModule(moduleId));
            return text + "</size>";
        }

        /// <summary>«Chain +1 (Drill Strike), Chain (free)» oder leer.</summary>
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
            string tip = (d != null ? d.DescriptionAt(module.Level) : string.Empty) + RuneText.Eases(session, module.ModuleId);
            if (ModuleRules.IsTargeted(module.ModuleId)) return new GUIContent($"↪ {session.DescribeTrigger(module)}", tip);
            // A-21: Effekt-Module mit farbigem Symbol, Tooltip «Name: Text».
            if (EffectText.TryGet(module.ModuleId, out CircuitEffectDefinition effect)) return new GUIContent(name, EffectText.Tip(effect));
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
                if (GUILayout.Button(new GUIContent(UiTexts.Module.Upgrade(from, target.Level + 1), UiTexts.Module.UpgradeTip(from, session.ModuleWhere(target))),
                        GUILayout.Height(28f)))
                    take(SkillDuplicateChoice.Upgrade);
                if (GUILayout.Button(new GUIContent(UiTexts.Module.AnotherCopy, UiTexts.Module.AnotherCopyTip), GUILayout.Height(28f)))
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
