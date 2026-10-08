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

        public int Count => Options.Count + ItemIds.Count;

        public RuneOffer(string source, IReadOnlyList<RuneDefinition> options, IReadOnlyList<string> itemIds = null)
        {
            Source = source ?? string.Empty;
            Options = options ?? throw new ArgumentNullException(nameof(options));
            ItemIds = itemIds ?? Array.Empty<string>();
        }

        /// <summary>Dasselbe Angebot mit Ausrüstung anstelle der letzten Runen.</summary>
        public RuneOffer WithItems(IReadOnlyList<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return this;
            int keep = Math.Max(1, Options.Count - itemIds.Count);
            return new RuneOffer(Source, Options.Take(keep).ToList(), itemIds);
        }

        /// <summary>
        /// Stellt ein Angebot zusammen: keine Doppelten, nichts schon Ausgerüstetes, und wenn möglich
        /// mindestens eine Rune mit einem Tag, den der Spieler bereits hat. Passende Tags sind doppelt gewichtet.
        /// </summary>
        /// <param name="isUnlocked">Freigeschaltete exklusive Runen (z. B. durch ein Set). Sie kommen garantiert ins Angebot.</param>
        public static RuneOffer Create(string source, RuneCatalog catalog, RuneLoadout loadout, Random random, int count = 3,
            Func<RuneDefinition, bool> isUnlocked = null)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (random == null) throw new ArgumentNullException(nameof(random));

            List<RuneDefinition> pool = catalog.All.Where(r => r.Weight > 0 && !r.IsExclusive && !loadout.Contains(r)).ToList();
            var picked = new List<RuneDefinition>();

            if (isUnlocked != null)
            {
                foreach (RuneDefinition unlocked in catalog.All.Where(r => r.IsExclusive && isUnlocked(r) && !loadout.Contains(r)))
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
