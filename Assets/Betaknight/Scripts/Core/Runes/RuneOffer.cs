using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Runes
{
    /// <summary>Eine Runenwahl: der Spieler nimmt eine der angebotenen Runen oder verzichtet.</summary>
    public sealed class RuneOffer
    {
        /// <summary>Wodurch die Wahl ausgelöst wurde, z. B. "Runensplitter" oder "Sieg".</summary>
        public string Source { get; }
        public IReadOnlyList<RuneDefinition> Options { get; }

        /// <summary>Ausrüstung, die statt einer Rune gewählt werden kann (gemischte Belohnung). Ids aus dem Ausrüstungs-Katalog.</summary>
        public IReadOnlyList<string> ItemIds { get; }

        /// <summary>Bietet zusätzlich «Tafel-Erweiterung: +1 Zeile» an.</summary>
        public bool BoardExpansion { get; }

        /// <summary>Skills als eigener Belohnungstyp (Ids aus dem Skill-Katalog).</summary>
        public IReadOnlyList<string> SkillIds { get; }

        /// <summary>Module als seltene zusätzliche Wahl (Ids aus dem Modul-Katalog).</summary>
        public IReadOnlyList<string> ModuleIds { get; }

        public int Count => Options.Count + ItemIds.Count + SkillIds.Count + ModuleIds.Count + (BoardExpansion ? 1 : 0);

        public RuneOffer(string source, IReadOnlyList<RuneDefinition> options, IReadOnlyList<string> itemIds = null, bool boardExpansion = false,
            IReadOnlyList<string> skillIds = null, IReadOnlyList<string> moduleIds = null)
        {
            ModuleIds = moduleIds ?? Array.Empty<string>();
            Source = source ?? string.Empty;
            Options = options ?? throw new ArgumentNullException(nameof(options));
            ItemIds = itemIds ?? Array.Empty<string>();
            SkillIds = skillIds ?? Array.Empty<string>();
            BoardExpansion = boardExpansion;
        }

        /// <summary>Dasselbe Angebot mit Ausrüstung anstelle der letzten Runen.</summary>
        public RuneOffer WithItems(IReadOnlyList<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return this;
            int keep = Math.Max(1, Options.Count - itemIds.Count);
            return new RuneOffer(Source, Options.Take(keep).ToList(), itemIds, BoardExpansion, SkillIds, ModuleIds);
        }

        /// <summary>Dasselbe Angebot mit Skills anstelle der letzten Runen (mindestens eine Rune bleibt).</summary>
        public RuneOffer WithSkills(IReadOnlyList<string> skillIds)
        {
            if (skillIds == null || skillIds.Count == 0) return this;
            int keep = Math.Max(1, Options.Count - skillIds.Count);
            return new RuneOffer(Source, Options.Take(keep).ToList(), ItemIds, BoardExpansion, skillIds, ModuleIds);
        }

        /// <summary>Dasselbe Angebot mit Modulen als zusätzlicher Wahl (Runen bleiben).</summary>
        public RuneOffer WithModules(IReadOnlyList<string> moduleIds)
        {
            if (moduleIds == null || moduleIds.Count == 0) return this;
            return new RuneOffer(Source, Options, ItemIds, BoardExpansion, SkillIds, moduleIds);
        }

        /// <summary>Dasselbe Angebot mit der Tafel-Erweiterung als zusätzlicher Wahl.</summary>
        public RuneOffer WithBoardExpansion() => new RuneOffer(Source, Options, ItemIds, true, SkillIds, ModuleIds);

        /// <summary>Dasselbe Angebot mit anderen Runen, Teilen und Skills (null = Skills unverändert).</summary>
        public RuneOffer With(IReadOnlyList<RuneDefinition> options, IReadOnlyList<string> itemIds, IReadOnlyList<string> skillIds = null) =>
            new RuneOffer(Source, options, itemIds, BoardExpansion, skillIds ?? SkillIds, ModuleIds);

        /// <summary>
        /// Stellt ein Angebot zusammen: keine Doppelten, nichts schon Ausgerüstetes, und wenn möglich
        /// mindestens eine Rune mit einem Tag, den der Spieler bereits hat. Passende Tags sind doppelt gewichtet.
        /// </summary>
        /// <param name="isUnlocked">Freigeschaltete exklusive Runen (z. B. durch ein Set). Sie kommen garantiert ins Angebot.</param>
        /// <param name="isOwned">Runen, die der Spieler sonst noch besitzt (z. B. im Runen-Inventar); sie werden nicht angeboten.</param>
        public static RuneOffer Create(string source, RuneCatalog catalog, RuneLoadout loadout, Random random, int count = 3,
            Func<RuneDefinition, bool> isUnlocked = null, Func<RuneDefinition, bool> isOwned = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (random == null) throw new ArgumentNullException(nameof(random));

            bool Owned(RuneDefinition r) => loadout.Contains(r) || (isOwned != null && isOwned(r));
            List<RuneDefinition> pool = catalog.All.Where(r => r.Weight > 0 && !r.IsExclusive && !Owned(r)).ToList();
            var picked = new List<RuneDefinition>();

            if (isUnlocked != null)
            {
                foreach (RuneDefinition unlocked in catalog.All.Where(r => r.IsExclusive && isUnlocked(r) && !Owned(r)))
                {
                    if (picked.Count >= count) break;
                    picked.Add(unlocked);
                }
            }

            List<RuneDefinition> matching = pool.Where(r => loadout.HasTag(r.Tag)).ToList();
            if (matching.Count > 0 && picked.Count < count)
            {
                RuneDefinition first = PickWeighted(matching, random, r => r.Weight);
                picked.Add(first);
                pool.Remove(first);
            }

            while (picked.Count < count && pool.Count > 0)
            {
                RuneDefinition next = PickWeighted(pool, random, r => loadout.HasTag(r.Tag) ? r.Weight * 2 : r.Weight);
                picked.Add(next);
                pool.Remove(next);
            }

            return new RuneOffer(source, picked);
        }

        private static RuneDefinition PickWeighted(List<RuneDefinition> list, Random random, Func<RuneDefinition, int> weight)
        {
            int total = list.Sum(weight);
            int roll = random.Next(total);
            foreach (RuneDefinition r in list)
            {
                int w = weight(r);
                if (roll < w) return r;
                roll -= w;
            }
            return list[list.Count - 1];
        }
    }
}
