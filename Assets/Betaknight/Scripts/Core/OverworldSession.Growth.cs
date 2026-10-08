using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
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
    /// Wachsen und Evolution. Jedes Skill-Exemplar und jedes Relais hat einen Zähler (Wachstum), der aus Kämpfen steigt.
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

        public GrowthRule RelayGrowthRule(RelayChip relay) => relay == null ? null : GrowthCatalog.ForRune(relay.Rune.Id);

        /// <summary>«+7 Schaden» o. Ä.: was das Wachstum gerade bewirkt, leer ohne Wirkung.</summary>
        public string GrowthEffectText(GrowthRule rule, int growth, RelayChip row = null)
        {
            if (rule == null || growth <= 0) return string.Empty;
            int bonus = rule.BonusAt(growth);
            switch (rule.Effect)
            {
                case GrowthEffect.FlatDamage: return SessionTexts.GrowthDamage(bonus);
                case GrowthEffect.StunTicks: return SessionTexts.GrowthStun(SkillInfo.Seconds(bonus));
                case GrowthEffect.PowerPercent: return SessionTexts.GrowthPower(bonus);
                case GrowthEffect.ThresholdPercent:
                    if (row == null) return string.Empty;
                    int base0 = row.Rune.ParameterAt(row.Level);
                    int grown = GrowthApplier.ApplyToParameter(row.Rune, base0, rule, growth);
                    return grown > base0 ? SessionTexts.GrowthThreshold(base0, grown) : string.Empty;
                default: return string.Empty;
            }
        }

        /// <summary>«Wachstum 7 · nächster Meilenstein 10 (Modul-Platz)».</summary>
        public string MilestoneText(int growth, bool skill)
        {
            int next = GrowthStages.NextMilestone(growth, skill);
            if (next < 0) return SessionTexts.Growth(growth);
            bool stage = skill && Array.IndexOf(GrowthStages.SkillStageThresholds, next) >= 0;
            bool slot = Array.IndexOf(GrowthStages.ModuleSlotThresholds, next) >= 0;
            string what = stage && slot ? SessionTexts.MilestoneLevelAndSlot : stage ? SessionTexts.MilestoneLevel(GrowthStages.StageFor(next)) : SessionTexts.MilestoneSlot;
            return SessionTexts.GrowthMilestone(growth, next, what);
        }

        /// <summary>Nach jedem Kampf: Wachstumspunkte aus dem Protokoll an Skill-Exemplare (Komponenten) und Relais verteilen.</summary>
        private void GrowFromBattle(CombatResult result)
        {
            if (result.Battle == null) return;
            Dictionary<int, RowTally> tallies = GrowthTally.Count(result.Battle);
            var components = Board.Components.ToList();
            var relays = Board.Relays.ToList();

            for (int i = 0; i < components.Count; i++)
            {
                if (!tallies.TryGetValue(i, out RowTally tally)) continue;
                SkillInstance skill = components[i].Skill;
                GrowthRule skillRule = SkillGrowthRule(skill);
                int skillPoints = skillRule != null ? tally.PointsFor(skillRule.Trigger, result.Victory) : 0;
                if (skillPoints <= 0 || !Skills.Contains(skill)) continue;
                int before = skill.Growth;
                int stage = skill.Level;
                Skills.Grow(skill, skillPoints);
                AnnounceMilestones(skill.NameFrom(SkillCatalog), before, skill.Growth, true, stage, skill.Level);
            }

            Dictionary<int, RowTally> relayTallies = GrowthTally.CountRelays(result.Battle);
            for (int i = 0; i < relays.Count; i++)
            {
                if (!relayTallies.TryGetValue(i, out RowTally tally)) continue;
                RelayChip relay = relays[i];
                GrowthRule rule = RelayGrowthRule(relay);
                int points = rule != null ? tally.PointsFor(rule.Trigger, result.Victory) : 0;
                if (points <= 0) continue;
                int before = relay.Growth;
                Board.Grow(relay, points);
                AnnounceMilestones(SessionTexts.RuneQuoted(relay.Name), before, relay.Growth, false, 0, 0);
            }
        }

        private void AnnounceMilestones(string name, int before, int after, bool skill, int stageBefore, int stageAfter)
        {
            if (skill && stageAfter > stageBefore) BuildImproved?.Invoke(SessionTexts.LevelReached(name, stageAfter));
            if (GrowthStages.ModuleSlotsFor(after) > GrowthStages.ModuleSlotsFor(before))
                BuildImproved?.Invoke(SessionTexts.ModuleSlotUnlocked(name, GrowthStages.ModuleSlotsFor(after)));
        }

        // ------------------------------------------------------------------ Evolution

        private int MaxSkillStage => Math.Min(Progression.MaxSkillLevel, GrowthStages.MaxStage);

        /// <summary>Name der Evolutionsform oder «???», solange sie nicht im Rezeptbuch steht.</summary>
        public string EvolutionName(EvolutionRecipe recipe) =>
            recipe == null ? string.Empty : RecipeBook.HasEvolution(recipe.Id) ? FormName(recipe.Subject, recipe.ToId) : SessionTexts.Unknown;

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
                    return SessionTexts.RequirementModule(ModuleCatalog.TryGet(recipe.RequirementId, out ModuleDefinition m) ? m.Name : recipe.RequirementId);
                case EvolutionRequirement.BlockInRow:
                    return SessionTexts.RequirementRuneInRow(RuneCatalog.TryGet(recipe.RequirementId, out RuneDefinition r) ? r.Name : recipe.RequirementId);
                case EvolutionRequirement.Tag:
                    return SessionTexts.RequirementTag(Synergies.NameOf(recipe.RequirementId), EvolutionRecipe.TagThreshold);
                default: return recipe.RequirementId;
            }
        }

        /// <summary>Das ganze Rezept, z. B. «Entzünden auf Höchststufe + Modul Fläche → Feuersturm».</summary>
        public string RecipeText(EvolutionRecipe recipe) =>
            SessionTexts.Recipe(FormName(recipe.Subject, recipe.FromId), RequirementText(recipe), FormName(recipe.Subject, recipe.ToId));

        /// <summary>Was einem Skill-Exemplar zu diesem Rezept fehlt; leer = bereit (entwickelt sich nach dem nächsten Boss).</summary>
        public List<string> EvolutionMissing(EvolutionRecipe recipe, SkillInstance skill)
        {
            var missing = new List<string>();
            if (recipe == null || recipe.Subject != EvolutionSubject.Skill) return missing;
            if (skill == null || skill.SkillId != recipe.FromId)
            {
                missing.Add(SessionTexts.MissingSkill(FormName(EvolutionSubject.Skill, recipe.FromId)));
                missing.Add(SessionTexts.MissingLevel(MaxSkillStage));
                missing.Add(RequirementText(recipe));
                return missing;
            }

            if (skill.Level < MaxSkillStage) missing.Add(SessionTexts.MissingLevelWithGrowth(MaxSkillStage, skill.Growth, GrowthStages.GrowthForStage(MaxSkillStage)));
            ComponentSlot slot = skill.Holder as ComponentSlot;
            bool onBoard = slot != null && Board.Components.Contains(slot);
            if (!onBoard) missing.Add(SessionTexts.MissingOnBoard);
            switch (recipe.Requirement)
            {
                case EvolutionRequirement.Module:
                    if (!skill.Modules.Any(m => m.ModuleId == recipe.RequirementId)) missing.Add(RequirementText(recipe));
                    break;
                case EvolutionRequirement.BlockInRow:
                    // A-19: «in derselben Zeile» heisst jetzt: ein Relais mit dieser Rune versorgt die Komponente (berührt sie innerhalb seiner Grenze).
                    if (!onBoard || !PoweringRelays(slot).Any(r => r.Rune.Id == recipe.RequirementId)) missing.Add(RequirementText(recipe));
                    break;
                case EvolutionRequirement.Tag:
                    AddTagMissing(missing, recipe);
                    break;
            }
            return missing;
        }

        /// <summary>Was einem Relais zu diesem Rezept fehlt; leer = bereit.</summary>
        public List<string> EvolutionMissing(EvolutionRecipe recipe, RelayChip row)
        {
            var missing = new List<string>();
            if (recipe == null || recipe.Subject != EvolutionSubject.Block) return missing;
            if (row == null || row.Rune.Id != recipe.FromId)
            {
                missing.Add(SessionTexts.MissingRune(FormName(EvolutionSubject.Block, recipe.FromId)));
                missing.Add(SessionTexts.MissingMaxLevel);
                missing.Add(RequirementText(recipe));
                return missing;
            }

            if (row.Level < row.Rune.MaxLevel) missing.Add(SessionTexts.MissingMaxLevelRune(row.Level + 1, row.Rune.MaxLevel + 1));
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
                missing.Add(SessionTexts.MissingTag(Synergies.NameOf(recipe.RequirementId), count, EvolutionRecipe.TagThreshold));
        }

        /// <summary>Das Exemplar, das diesem Rezept am nächsten ist (am wenigsten fehlt), oder null ohne passendes.</summary>
        public SkillInstance BestCandidate(EvolutionRecipe recipe) =>
            Skills.OfSkill(recipe.FromId).OrderBy(s => EvolutionMissing(recipe, s).Count).ThenByDescending(s => s.Growth).FirstOrDefault();

        public RelayChip BestRelayCandidate(EvolutionRecipe recipe) => Board.Relays.FirstOrDefault(r => r.Rune.Id == recipe.FromId);

        /// <summary>«Evolution ???: fehlt Stufe 3, Modul Fläche» bzw. «… bereit, entwickelt sich nach dem nächsten Boss».</summary>
        public string EvolutionProgress(EvolutionRecipe recipe)
        {
            List<string> missing = recipe.Subject == EvolutionSubject.Skill
                ? EvolutionMissing(recipe, BestCandidate(recipe))
                : EvolutionMissing(recipe, BestRelayCandidate(recipe));
            return ProgressText(recipe, missing);
        }

        private string ProgressText(EvolutionRecipe recipe, List<string> missing)
        {
            string name = EvolutionName(recipe);
            return missing.Count == 0
                ? SessionTexts.EvolutionReady(name)
                : SessionTexts.EvolutionMissing(name, string.Join(", ", missing));
        }

        /// <summary>Fortschritt der Evolutionen eines Skill-Exemplars, für Build-Fenster und Karte.</summary>
        public List<string> EvolutionProgressFor(SkillInstance skill)
        {
            var lines = new List<string>();
            if (skill == null || skill.IsBasicAttack) return lines;
            foreach (EvolutionRecipe r in EvolutionCatalog.From(EvolutionSubject.Skill, skill.SkillId))
                lines.Add(ProgressText(r, EvolutionMissing(r, skill)));
            return lines;
        }

        /// <summary>Fortschritt der Evolutionen eines Relais.</summary>
        public List<string> EvolutionProgressFor(RelayChip relay)
        {
            var lines = new List<string>();
            if (relay == null) return lines;
            foreach (EvolutionRecipe r in EvolutionCatalog.From(EvolutionSubject.Block, relay.Rune.Id))
                lines.Add(ProgressText(r, EvolutionMissing(r, relay)));
            return lines;
        }

        /// <summary>True, wenn sich das Exemplar nach dem nächsten Boss entwickelt.</summary>
        public bool IsEvolutionReady(SkillInstance skill) =>
            skill != null && !skill.IsBasicAttack
            && EvolutionCatalog.From(EvolutionSubject.Skill, skill.SkillId).Any(r => EvolutionMissing(r, skill).Count == 0);

        /// <summary>True, wenn sich das Relais nach dem nächsten Boss entwickelt.</summary>
        public bool IsEvolutionReady(RelayChip relay) =>
            relay != null && EvolutionCatalog.From(EvolutionSubject.Block, relay.Rune.Id).Any(r => EvolutionMissing(r, relay).Count == 0);

        /// <summary>Fortschritt zu Evolutionen, die ein angebotener Skill, ein Modul, eine Rune oder ein Teil voranbringt.</summary>
        public List<string> EvolutionHintsForSkill(string skillId) =>
            EvolutionCatalog.From(EvolutionSubject.Skill, skillId).Select(EvolutionProgress).ToList();

        public List<string> EvolutionHintsForModule(string moduleId) =>
            EvolutionCatalog.Needing(EvolutionRequirement.Module, moduleId).Where(OwnsSubject)
                .Select(r => SessionTexts.EvolutionHintModule(EvolutionProgress(r), FormName(r.Subject, r.FromId))).ToList();

        public List<string> EvolutionHintsForRune(string runeId)
        {
            var hints = EvolutionCatalog.Needing(EvolutionRequirement.BlockInRow, runeId).Where(OwnsSubject)
                .Select(r => SessionTexts.EvolutionHintRune(EvolutionProgress(r), FormName(r.Subject, r.FromId))).ToList();
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
                    hints.Add(SessionTexts.EvolutionHintTag(Synergies.NameOf(tag), count + 1, EvolutionRecipe.TagThreshold, FormName(r.Subject, r.FromId)));
            }
            return hints;
        }

        private bool OwnsSubject(EvolutionRecipe r) =>
            r.Subject == EvolutionSubject.Skill ? OwnsSkill(r.FromId) : Board.Relays.Any(relay => relay.Rune.Id == r.FromId);

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
                    foreach (RelayChip row in Board.Relays.ToList())
                    {
                        if (EvolutionMissing(recipe, row).Count > 0 || !RuneCatalog.TryGet(recipe.ToId, out RuneDefinition evolved)) continue;
                        string from = row.Name;
                        Board.Evolve(row, evolved);
                        lines.Add(Announce(recipe, $"{from} → {row.Name}"));
                    }
                }
            }
            return lines;
        }

        private string Announce(EvolutionRecipe recipe, string change)
        {
            RecipeBook.DiscoverEvolution(recipe.Id);
            string line = SessionTexts.Evolution(change);
            Evolved?.Invoke(recipe, change);
            BuildImproved?.Invoke(line);
            return line;
        }
    }
}
