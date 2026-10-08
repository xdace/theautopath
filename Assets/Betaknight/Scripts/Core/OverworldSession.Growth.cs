using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Evolution;
using Betaknight.Core.Gear;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>
    /// Wachsen und Evolution. Jedes Skill-Exemplar und jeder Baustein hat einen Zähler (Wachstum), der aus Kämpfen steigt.
    /// Meilensteine: Skill-Stufen (5/15/30) und Modul-Plätze (10/25). Auf Höchststufe mit erfülltem Rezept entwickelt
    /// sich ein Exemplar nach dem nächsten überlebten Boss; Wachstum und Module bleiben, das Rezept kommt ins Rezeptbuch.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public GrowthCatalog GrowthCatalog { get; } = GrowthCatalog.CreateDefault();
        public EvolutionCatalog EvolutionCatalog { get; } = EvolutionCatalog.CreateDefault();

        /// <summary>Eine Evolution ist passiert (nach einem Boss): Rezept und Exemplar-Beschreibung.</summary>
        public event Action<EvolutionRecipe, string> Evolved;

        public GrowthRule SkillGrowthRule(SkillInstance skill) =>
            skill == null || skill.IsBasicAttack ? null : GrowthCatalog.ForSkill(skill.SkillId);

        public GrowthRule RowGrowthRule(RuneSlot row) => row == null ? null : GrowthCatalog.ForRune(row.Rune.Id);

        /// <summary>«+7 Schaden» o. Ä.: was das Wachstum gerade bewirkt, leer ohne Wirkung.</summary>
        public string GrowthEffectText(GrowthRule rule, int growth, RuneSlot row = null)
        {
            if (rule == null || growth <= 0) return string.Empty;
            int bonus = rule.BonusAt(growth);
            switch (rule.Effect)
            {
                case GrowthEffect.FlatDamage: return $"+{bonus} Schaden";
                case GrowthEffect.StunTicks: return $"+{SkillInfo.Seconds(bonus)} Betäubung";
                case GrowthEffect.PowerPercent: return $"+{bonus} % Wirkung";
                case GrowthEffect.ThresholdPercent:
                    if (row == null) return string.Empty;
                    int base0 = row.Rune.ParameterAt(row.Level);
                    int grown = GrowthApplier.ApplyToParameter(row.Rune, base0, rule, growth);
                    return grown > base0 ? $"Schwelle {base0} → {grown} %" : string.Empty;
                default: return string.Empty;
            }
        }

        /// <summary>«Wachstum 7 · nächster Meilenstein 10 (Modul-Platz)».</summary>
        public string MilestoneText(int growth, bool skill)
        {
            int next = GrowthStages.NextMilestone(growth, skill);
            if (next < 0) return $"Wachstum {growth}";
            bool stage = skill && Array.IndexOf(GrowthStages.SkillStageThresholds, next) >= 0;
            bool slot = Array.IndexOf(GrowthStages.ModuleSlotThresholds, next) >= 0;
            string what = stage && slot ? "Stufe und Modul-Platz" : stage ? $"Stufe {GrowthStages.StageFor(next)}" : "Modul-Platz";
            return $"Wachstum {growth}, bei {next}: {what}";
        }

        /// <summary>Nach jedem Kampf: Wachstumspunkte aus dem Protokoll an Skill-Exemplare und Bausteine verteilen.</summary>
        private void GrowFromBattle(CombatResult result)
        {
            if (result.Battle == null) return;
            Dictionary<int, RowTally> tallies = GrowthTally.Count(result.Battle);

            for (int i = 0; i < Runes.Rows.Count; i++)
            {
                if (!tallies.TryGetValue(i, out RowTally tally)) continue;
                RuneSlot row = Runes.Rows[i];

                SkillInstance skill = row.Skill;
                GrowthRule skillRule = SkillGrowthRule(skill);
                int skillPoints = skillRule != null ? tally.PointsFor(skillRule.Trigger, result.Victory) : 0;
                if (skillPoints > 0 && Skills.Contains(skill))
                {
                    int before = skill.Growth;
                    int stage = skill.Level;
                    Skills.Grow(skill, skillPoints);
                    AnnounceMilestones(skill.NameFrom(SkillCatalog), before, skill.Growth, true, stage, skill.Level);
                }

                GrowthRule rowRule = RowGrowthRule(row);
                int rowPoints = rowRule != null ? tally.PointsFor(rowRule.Trigger, result.Victory) : 0;
                if (rowPoints > 0)
                {
                    int before = row.Growth;
                    Runes.Grow(row, rowPoints);
                    AnnounceMilestones($"Baustein «{row.Name}»", before, row.Growth, false, 0, 0);
                }
            }
        }

        private void AnnounceMilestones(string name, int before, int after, bool skill, int stageBefore, int stageAfter)
        {
            if (skill && stageAfter > stageBefore) BuildImproved?.Invoke($"{name}: Stufe {stageAfter} erreicht");
            if (GrowthStages.ModuleSlotsFor(after) > GrowthStages.ModuleSlotsFor(before))
                BuildImproved?.Invoke($"{name}: {GrowthStages.ModuleSlotsFor(after)}. Modul-Platz frei");
        }

        // ------------------------------------------------------------------ Evolution

        private int MaxSkillStage => Math.Min(Progression.MaxSkillLevel, GrowthStages.MaxStage);

        /// <summary>Name der Evolutionsform oder «???», solange sie nicht im Rezeptbuch steht.</summary>
        public string EvolutionName(EvolutionRecipe recipe) =>
            recipe == null ? string.Empty : RecipeBook.HasEvolution(recipe.Id) ? FormName(recipe.Subject, recipe.ToId) : "???";

        public string FormName(EvolutionSubject subject, string id)
        {
            if (subject == EvolutionSubject.Skill) return SkillCatalog.TryGet(id, out SkillDefinition s) ? s.Name : id;
            return RuneCatalog.TryGet(id, out RuneDefinition r) ? r.Name : id;
        }

        /// <summary>«Modul Fläche», «Baustein «Gegner unter 25 %» in derselben Zeile», «Tag Ladung 4».</summary>
        public string RequirementText(EvolutionRecipe recipe)
        {
            switch (recipe.Requirement)
            {
                case EvolutionRequirement.Module:
                    return $"Modul {(ModuleCatalog.TryGet(recipe.RequirementId, out ModuleDefinition m) ? m.Name : recipe.RequirementId)}";
                case EvolutionRequirement.BlockInRow:
                    return $"Baustein «{(RuneCatalog.TryGet(recipe.RequirementId, out RuneDefinition r) ? r.Name : recipe.RequirementId)}» in derselben Zeile";
                case EvolutionRequirement.Tag:
                    return $"Tag {Synergies.NameOf(recipe.RequirementId)} {EvolutionRecipe.TagThreshold}";
                default: return recipe.RequirementId;
            }
        }

        /// <summary>Das ganze Rezept, z. B. «Entzünden auf Höchststufe + Modul Fläche → Feuersturm».</summary>
        public string RecipeText(EvolutionRecipe recipe) =>
            $"{FormName(recipe.Subject, recipe.FromId)} auf Höchststufe + {RequirementText(recipe)} → {FormName(recipe.Subject, recipe.ToId)}";

        /// <summary>Was einem Skill-Exemplar zu diesem Rezept fehlt; leer = bereit (entwickelt sich nach dem nächsten Boss).</summary>
        public List<string> EvolutionMissing(EvolutionRecipe recipe, SkillInstance skill)
        {
            var missing = new List<string>();
            if (recipe == null || recipe.Subject != EvolutionSubject.Skill) return missing;
            if (skill == null || skill.SkillId != recipe.FromId)
            {
                missing.Add($"Skill {FormName(EvolutionSubject.Skill, recipe.FromId)}");
                missing.Add($"Stufe {MaxSkillStage}");
                missing.Add(RequirementText(recipe));
                return missing;
            }

            if (skill.Level < MaxSkillStage) missing.Add($"Stufe {MaxSkillStage} (Wachstum {skill.Growth}/{GrowthStages.GrowthForStage(MaxSkillStage)})");
            RuneSlot row = skill.Holder as RuneSlot;
            if (row == null || !Runes.Rows.Contains(row)) missing.Add("an der Tafel");
            switch (recipe.Requirement)
            {
                case EvolutionRequirement.Module:
                    if (!skill.Modules.Any(m => m.ModuleId == recipe.RequirementId)) missing.Add(RequirementText(recipe));
                    break;
                case EvolutionRequirement.BlockInRow:
                    if (row == null || row.Rune.Id != recipe.RequirementId) missing.Add(RequirementText(recipe));
                    break;
                case EvolutionRequirement.Tag:
                    AddTagMissing(missing, recipe);
                    break;
            }
            return missing;
        }

        /// <summary>Was einem Baustein (Zeile) zu diesem Rezept fehlt; leer = bereit.</summary>
        public List<string> EvolutionMissing(EvolutionRecipe recipe, RuneSlot row)
        {
            var missing = new List<string>();
            if (recipe == null || recipe.Subject != EvolutionSubject.Block) return missing;
            if (row == null || row.Rune.Id != recipe.FromId)
            {
                missing.Add($"Baustein {FormName(EvolutionSubject.Block, recipe.FromId)}");
                missing.Add("Höchststufe");
                missing.Add(RequirementText(recipe));
                return missing;
            }

            if (row.Level < row.Rune.MaxLevel) missing.Add($"Höchststufe (Stufe {row.Level + 1}/{row.Rune.MaxLevel + 1}, Lagerfeuer oder doppelte Rune)");
            switch (recipe.Requirement)
            {
                case EvolutionRequirement.Module:
                    if (!row.Modules.Any(m => m.ModuleId == recipe.RequirementId)) missing.Add(RequirementText(recipe));
                    break;
                case EvolutionRequirement.Tag:
                    AddTagMissing(missing, recipe);
                    break;
                case EvolutionRequirement.BlockInRow:
                    break;
            }
            return missing;
        }

        private void AddTagMissing(List<string> missing, EvolutionRecipe recipe)
        {
            int count = Gear.TagCount(recipe.RequirementId);
            if (count < EvolutionRecipe.TagThreshold)
                missing.Add($"Tag {Synergies.NameOf(recipe.RequirementId)} {count}/{EvolutionRecipe.TagThreshold}");
        }

        /// <summary>Das Exemplar, das diesem Rezept am nächsten ist (am wenigsten fehlt), oder null ohne passendes.</summary>
        public SkillInstance BestCandidate(EvolutionRecipe recipe) =>
            Skills.OfSkill(recipe.FromId).OrderBy(s => EvolutionMissing(recipe, s).Count).ThenByDescending(s => s.Growth).FirstOrDefault();

        public RuneSlot BestRowCandidate(EvolutionRecipe recipe) => Runes.Rows.FirstOrDefault(r => r.Rune.Id == recipe.FromId);

        /// <summary>«Evolution ???: fehlt Stufe 3, Modul Fläche» bzw. «… bereit, entwickelt sich nach dem nächsten Boss».</summary>
        public string EvolutionProgress(EvolutionRecipe recipe)
        {
            List<string> missing = recipe.Subject == EvolutionSubject.Skill
                ? EvolutionMissing(recipe, BestCandidate(recipe))
                : EvolutionMissing(recipe, BestRowCandidate(recipe));
            return ProgressText(recipe, missing);
        }

        private string ProgressText(EvolutionRecipe recipe, List<string> missing)
        {
            string name = EvolutionName(recipe);
            return missing.Count == 0
                ? $"Evolution {name}: bereit, nach dem nächsten Boss"
                : $"Evolution {name}: fehlt {string.Join(", ", missing)}";
        }

        /// <summary>Fortschritt der Evolutionen genau dieser Zeile (ihr Skill und ihr Baustein), für Tafel und Karte.</summary>
        public List<string> EvolutionProgressFor(RuneSlot row)
        {
            var lines = new List<string>();
            if (row == null) return lines;
            if (row.Skill != null && !row.Skill.IsBasicAttack)
                foreach (EvolutionRecipe r in EvolutionCatalog.From(EvolutionSubject.Skill, row.Skill.SkillId))
                    lines.Add(ProgressText(r, EvolutionMissing(r, row.Skill)));
            foreach (EvolutionRecipe r in EvolutionCatalog.From(EvolutionSubject.Block, row.Rune.Id))
                lines.Add(ProgressText(r, EvolutionMissing(r, row)));
            return lines;
        }

        /// <summary>True, wenn sich der Skill oder der Baustein dieser Zeile nach dem nächsten Boss entwickelt.</summary>
        public bool IsEvolutionReady(RuneSlot row)
        {
            if (row == null) return false;
            if (row.Skill != null && !row.Skill.IsBasicAttack
                && EvolutionCatalog.From(EvolutionSubject.Skill, row.Skill.SkillId).Any(r => EvolutionMissing(r, row.Skill).Count == 0))
                return true;
            return EvolutionCatalog.From(EvolutionSubject.Block, row.Rune.Id).Any(r => EvolutionMissing(r, row).Count == 0);
        }

        /// <summary>Fortschritt zu Evolutionen, die ein angebotener Skill, ein Modul, eine Rune oder ein Teil voranbringt.</summary>
        public List<string> EvolutionHintsForSkill(string skillId) =>
            EvolutionCatalog.From(EvolutionSubject.Skill, skillId).Select(EvolutionProgress).ToList();

        public List<string> EvolutionHintsForModule(string moduleId) =>
            EvolutionCatalog.Needing(EvolutionRequirement.Module, moduleId).Where(OwnsSubject)
                .Select(r => $"{EvolutionProgress(r)} (Teil des Rezepts für {FormName(r.Subject, r.FromId)})").ToList();

        public List<string> EvolutionHintsForRune(string runeId)
        {
            var hints = EvolutionCatalog.Needing(EvolutionRequirement.BlockInRow, runeId).Where(OwnsSubject)
                .Select(r => $"{EvolutionProgress(r)} (in die Zeile von {FormName(r.Subject, r.FromId)})").ToList();
            hints.AddRange(EvolutionCatalog.From(EvolutionSubject.Block, runeId).Select(EvolutionProgress));
            return hints;
        }

        public List<string> EvolutionHintsForItem(EquipmentDefinition item)
        {
            var hints = new List<string>();
            if (item == null) return hints;
            foreach (string tag in item.Tags)
            foreach (EvolutionRecipe r in EvolutionCatalog.Needing(EvolutionRequirement.Tag, tag).Where(OwnsSubject))
            {
                int count = Gear.TagCount(tag);
                if (count < EvolutionRecipe.TagThreshold)
                    hints.Add($"→ {Synergies.NameOf(tag)} {count + 1}/{EvolutionRecipe.TagThreshold} für Evolution von {FormName(r.Subject, r.FromId)}");
            }
            return hints;
        }

        private bool OwnsSubject(EvolutionRecipe r) =>
            r.Subject == EvolutionSubject.Skill ? OwnsSkill(r.FromId) : Runes.Rows.Any(row => row.Rune.Id == r.FromId);

        /// <summary>Nach einem überlebten Boss: alles Bereite entwickelt sich. Gibt Meldungen für das Ergebnisfenster zurück.</summary>
        private List<string> EvolveAfterBoss()
        {
            var lines = new List<string>();
            foreach (EvolutionRecipe recipe in EvolutionCatalog.All)
            {
                if (recipe.Subject == EvolutionSubject.Skill)
                {
                    foreach (SkillInstance skill in Skills.OfSkill(recipe.FromId).ToList())
                    {
                        if (EvolutionMissing(recipe, skill).Count > 0 || !SkillCatalog.Contains(recipe.ToId)) continue;
                        string from = skill.NameFrom(SkillCatalog);
                        Skills.Evolve(skill, recipe.ToId);
                        lines.Add(Announce(recipe, $"{from} → {skill.NameFrom(SkillCatalog)}"));
                    }
                }
                else
                {
                    foreach (RuneSlot row in Runes.Rows.ToList())
                    {
                        if (EvolutionMissing(recipe, row).Count > 0 || !RuneCatalog.TryGet(recipe.ToId, out RuneDefinition evolved)) continue;
                        string from = row.Name;
                        Runes.Evolve(row, evolved);
                        lines.Add(Announce(recipe, $"{from} → {row.Name}"));
                    }
                }
            }
            return lines;
        }

        private string Announce(EvolutionRecipe recipe, string change)
        {
            RecipeBook.DiscoverEvolution(recipe.Id);
            string line = $"Evolution: {change}";
            Evolved?.Invoke(recipe, change);
            BuildImproved?.Invoke(line);
            return line;
        }
    }
}
