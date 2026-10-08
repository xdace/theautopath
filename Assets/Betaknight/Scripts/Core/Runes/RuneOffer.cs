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

        public RuneOffer(string source, IReadOnlyList<RuneDefinition> options)
        {
            Source = source ?? string.Empty;
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Stellt ein Angebot zusammen: keine Doppelten, nichts schon Ausgerüstetes, und wenn möglich
        /// mindestens eine Rune mit einem Tag, den der Spieler bereits hat. Passende Tags sind doppelt gewichtet.
        /// </summary>
        public static RuneOffer Create(string source, RuneCatalog catalog, RuneLoadout loadout, Random random, int count = 3)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (loadout == null) throw new ArgumentNullException(nameof(loadout));
            if (random == null) throw new ArgumentNullException(nameof(random));

            List<RuneDefinition> pool = catalog.All.Where(r => r.Weight > 0 && !r.IsExclusive && !loadout.Contains(r)).ToList();
            var picked = new List<RuneDefinition>();

            List<RuneDefinition> matching = pool.Where(r => loadout.HasTag(r.Tag)).ToList();
            if (matching.Count > 0)
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
