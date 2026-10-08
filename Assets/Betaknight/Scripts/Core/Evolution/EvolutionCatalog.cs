using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Evolution
{
    /// <summary>Was sich entwickelt: ein Skill-Exemplar oder ein Logikbaustein (Rune einer Zeile).</summary>
    public enum EvolutionSubject
    {
        Skill,
        Block,
    }

    /// <summary>Die Rezeptbedingung neben der Höchststufe.</summary>
    public enum EvolutionRequirement
    {
        /// <summary>Ein bestimmtes Modul sitzt am Skill bzw. Baustein.</summary>
        Module,

        /// <summary>Ein bestimmter Baustein (Rune) steht in derselben Zeile wie der Skill.</summary>
        BlockInRow,

        /// <summary>Ein bestimmter Synergie-Tag der Ausrüstung steht mindestens auf Schwelle 4.</summary>
        Tag,
    }

    /// <summary>
    /// Ein Evolutions-Rezept: Skill oder Baustein auf Höchststufe plus eine Bedingung. Nach dem nächsten überlebten Boss
    /// entwickelt sich das Exemplar zur Evolutionsform; Wachstum und Module bleiben. Reine Daten.
    /// </summary>
    public sealed class EvolutionRecipe
    {
        public const int TagThreshold = 4;

        public string Id { get; }

        /// <summary>Synergie-Tag, zu dem das Rezept thematisch gehört (eines pro Tag).</summary>
        public string ThemeTag { get; }

        public EvolutionSubject Subject { get; }

        /// <summary>Skill-Id bzw. Runen-Id, die sich entwickelt.</summary>
        public string FromId { get; }

        /// <summary>Skill-Id bzw. Runen-Id der Evolutionsform.</summary>
        public string ToId { get; }

        public EvolutionRequirement Requirement { get; }

        /// <summary>Modul-Id, Runen-Id oder Tag-Id, je nach <see cref="Requirement"/>.</summary>
        public string RequirementId { get; }

        /// <summary>Hinweis für die Silhouette im Rezeptbuch, solange das Rezept unentdeckt ist.</summary>
        public string Hint { get; }

        public EvolutionRecipe(string id, string themeTag, EvolutionSubject subject, string fromId, string toId,
            EvolutionRequirement requirement, string requirementId, string hint)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            ThemeTag = themeTag;
            Subject = subject;
            FromId = fromId;
            ToId = toId;
            Requirement = requirement;
            RequirementId = requirementId;
            Hint = hint ?? string.Empty;
        }
    }

    public sealed class EvolutionCatalog
    {
        private readonly List<EvolutionRecipe> _all = new List<EvolutionRecipe>();

        public IReadOnlyList<EvolutionRecipe> All => _all;

        public void Register(EvolutionRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            _all.RemoveAll(r => r.Id == recipe.Id);
            _all.Add(recipe);
        }

        public EvolutionRecipe Get(string id) => _all.Find(r => r.Id == id);

        /// <summary>Rezepte, in denen dieser Skill bzw. diese Rune sich entwickelt.</summary>
        public List<EvolutionRecipe> From(EvolutionSubject subject, string id) => _all.FindAll(r => r.Subject == subject && r.FromId == id);

        /// <summary>Rezepte, die dieses Modul, diese Rune (in der Zeile) oder diesen Tag als Bedingung brauchen.</summary>
        public List<EvolutionRecipe> Needing(EvolutionRequirement requirement, string id) =>
            _all.FindAll(r => r.Requirement == requirement && r.RequirementId == id);

        /// <summary>Sechs Rezepte, eines pro (vorläufigem) Synergie-Tag.</summary>
        public static EvolutionCatalog CreateDefault()
        {
            var c = new EvolutionCatalog();
            c.Register(new EvolutionRecipe("evo_inferno", SynergyTagIds.Heat, EvolutionSubject.Skill, SkillIds.Ignite, SkillIds.Inferno,
                EvolutionRequirement.Module, ModuleIds.Area, "Ein Feuer-Skill auf Höchststufe und ein Modul, das alle trifft."));
            c.Register(new EvolutionRecipe("evo_lance", SynergyTagIds.Charge, EvolutionSubject.Skill, SkillIds.ShockStab, SkillIds.LightningLance,
                EvolutionRequirement.Tag, SynergyTagIds.Charge, "Ein schneller Schock-Skill auf Höchststufe, getragen von viel Ladung."));
            c.Register(new EvolutionRecipe("evo_reflex", SynergyTagIds.Phantom, EvolutionSubject.Block, "hp_low", EvolvedRuneIds.PhantomReflex,
                EvolutionRequirement.Module, ModuleIds.Extend, "Ein Leben-Baustein auf Höchststufe, der länger hält als er sollte."));
            c.Register(new EvolutionRecipe("evo_resonance", SynergyTagIds.Tempo, EvolutionSubject.Skill, SkillIds.Echo, SkillIds.Resonance,
                EvolutionRequirement.Module, ModuleIds.Multicast, "Ein Skill, der wiederholt, und ein Modul, das wiederholt."));
            c.Register(new EvolutionRecipe("evo_acid", SynergyTagIds.Toxin, EvolutionSubject.Skill, SkillIds.Drill, SkillIds.AcidDrill,
                EvolutionRequirement.BlockInRow, "enemy_low", "Ein schwerer Angriff auf Höchststufe, der auf geschwächte Gegner wartet."));
            c.Register(new EvolutionRecipe("evo_ram", SynergyTagIds.Scrap, EvolutionSubject.Skill, SkillIds.ShieldBash, SkillIds.ScrapRam,
                EvolutionRequirement.Tag, SynergyTagIds.Scrap, "Ein Schild-Skill auf Höchststufe, umhüllt von Schrott."));
            return c;
        }
    }
}
