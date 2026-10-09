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
        /// <summary>
        /// Evolutionen (Rezepte) sind vorübergehend aus dem Spiel genommen (werden neu gedacht): der Katalog wirkt leer, also
        /// entwickelt sich nichts, es gibt keine ^-Marken und keine Rezept-Hinweise. Tests der Evolutions-Regeln schalten sie ein.
        /// </summary>
        public static bool Enabled { get; set; }

        private readonly List<EvolutionRecipe> _all = new List<EvolutionRecipe>();

        public IReadOnlyList<EvolutionRecipe> All => Enabled ? (IReadOnlyList<EvolutionRecipe>)_all : Array.Empty<EvolutionRecipe>();

        /// <summary>Alle Rezepte, auch wenn Evolutionen aus sind (für Text-Prüfungen).</summary>
        public IReadOnlyList<EvolutionRecipe> Registered => _all;

        public void Register(EvolutionRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            _all.RemoveAll(r => r.Id == recipe.Id);
            _all.Add(recipe);
        }

        public EvolutionRecipe Get(string id) => Enabled ? _all.Find(r => r.Id == id) : null;

        /// <summary>Rezepte, in denen dieser Skill bzw. diese Rune sich entwickelt.</summary>
        public List<EvolutionRecipe> From(EvolutionSubject subject, string id) =>
            Enabled ? _all.FindAll(r => r.Subject == subject && r.FromId == id) : new List<EvolutionRecipe>();

        /// <summary>Rezepte, die dieses Modul, diese Rune (in der Zeile) oder diesen Tag als Bedingung brauchen.</summary>
        public List<EvolutionRecipe> Needing(EvolutionRequirement requirement, string id) =>
            Enabled ? _all.FindAll(r => r.Requirement == requirement && r.RequirementId == id) : new List<EvolutionRecipe>();

        /// <summary>Sechs Rezepte, eines pro (vorläufigem) Synergie-Tag.</summary>
        public static EvolutionCatalog CreateDefault()
        {
            var c = new EvolutionCatalog();
            c.Register(new EvolutionRecipe("evo_inferno", SynergyTagIds.Heat, EvolutionSubject.Skill, SkillIds.Ignite, SkillIds.Inferno,
                EvolutionRequirement.Module, ModuleIds.Area, "A Fire skill at max level and a module that hits everyone."));
            c.Register(new EvolutionRecipe("evo_lance", SynergyTagIds.Charge, EvolutionSubject.Skill, SkillIds.ShockStab, SkillIds.LightningLance,
                EvolutionRequirement.Tag, SynergyTagIds.Charge, "A fast Shock skill at max level, carried by plenty of Static."));
            c.Register(new EvolutionRecipe("evo_reflex", SynergyTagIds.Phantom, EvolutionSubject.Block, "hp_low", EvolvedRuneIds.PhantomReflex,
                EvolutionRequirement.Module, ModuleIds.Extend, "An HP rune at max level that holds longer than it should."));
            c.Register(new EvolutionRecipe("evo_resonance", SynergyTagIds.Tempo, EvolutionSubject.Skill, SkillIds.Echo, SkillIds.Resonance,
                EvolutionRequirement.Module, ModuleIds.Multicast, "A skill that repeats, and a module that repeats."));
            c.Register(new EvolutionRecipe("evo_acid", SynergyTagIds.Toxin, EvolutionSubject.Skill, SkillIds.Drill, SkillIds.AcidDrill,
                EvolutionRequirement.BlockInRow, "enemy_low", "A heavy attack at max level that waits for weakened enemies."));
            c.Register(new EvolutionRecipe("evo_ram", SynergyTagIds.Scrap, EvolutionSubject.Skill, SkillIds.ShieldBash, SkillIds.ScrapRam,
                EvolutionRequirement.Tag, SynergyTagIds.Scrap, "A Shield skill at max level, wrapped in Scrap."));
            return c;
        }
    }
}
