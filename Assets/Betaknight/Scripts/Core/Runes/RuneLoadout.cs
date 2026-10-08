using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Runes
{
    /// <summary>Die ausgerüsteten Runen des Spielers. Die begrenzten Plätze zwingen zu Entscheidungen.</summary>
    public sealed class RuneLoadout
    {
        private readonly List<RuneDefinition> _runes = new List<RuneDefinition>();

        public int Slots { get; private set; }
        public IReadOnlyList<RuneDefinition> Runes => _runes;
        public bool IsFull => _runes.Count >= Slots;

        public event Action Changed;

        public RuneLoadout(int slots = 3)
        {
            if (slots < 1) throw new ArgumentOutOfRangeException(nameof(slots));
            Slots = slots;
        }

        public bool Contains(RuneDefinition rune) => rune != null && _runes.Any(r => r.Id == rune.Id);

        public bool HasTag(RuneTag tag) => _runes.Any(r => r.Tag == tag);

        public bool TryAdd(RuneDefinition rune)
        {
            if (rune == null || IsFull || Contains(rune)) return false;
            _runes.Add(rune);
            Changed?.Invoke();
            return true;
        }

        public bool TryReplace(int index, RuneDefinition rune)
        {
            if (rune == null || index < 0 || index >= _runes.Count || Contains(rune)) return false;
            _runes[index] = rune;
            Changed?.Invoke();
            return true;
        }

        public void AddSlot()
        {
            Slots++;
            Changed?.Invoke();
        }

        /// <summary>Anzahl Runen pro Tag, z. B. für die Anzeige "2x Klinge".</summary>
        public Dictionary<RuneTag, int> CountByTag()
        {
            var result = new Dictionary<RuneTag, int>();
            foreach (RuneDefinition r in _runes)
            {
                result.TryGetValue(r.Tag, out int n);
                result[r.Tag] = n + 1;
            }
            return result;
        }
    }
}
