using System;
using System.Collections.Generic;

namespace Betaknight.Core.Runes
{
    /// <summary>Eine abgelegte Rune. Sie behält ihre Stufe (Lagerfeuer), bis sie wieder eingesetzt wird.</summary>
    public sealed class StoredRune
    {
        public RuneDefinition Rune { get; }
        public int Level { get; }

        public StoredRune(RuneDefinition rune, int level = 0)
        {
            Rune = rune ?? throw new ArgumentNullException(nameof(rune));
            Level = Math.Max(0, Math.Min(level, rune.MaxLevel));
        }

        public string Name => Rune.NameAt(Level);
        public string Description => Rune.DescriptionAt(Level);
    }

    /// <summary>Runen-Inventar: Runen, die gerade keine Zeile der Tafel belegen. Begrenzte Plätze.</summary>
    public sealed class RuneInventory
    {
        public const int DefaultCapacity = 6;

        private readonly List<StoredRune> _runes = new List<StoredRune>();

        public int Capacity { get; }
        public IReadOnlyList<StoredRune> Runes => _runes;
        public int Count => _runes.Count;
        public bool IsFull => _runes.Count >= Capacity;

        public event Action Changed;

        public RuneInventory(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public StoredRune this[int index] => IsValid(index) ? _runes[index] : null;

        public bool Contains(RuneDefinition rune)
        {
            if (rune == null) return false;
            foreach (StoredRune r in _runes) if (r.Rune.Id == rune.Id) return true;
            return false;
        }

        public bool TryAdd(StoredRune rune, int index = -1)
        {
            if (rune == null || IsFull || Contains(rune.Rune)) return false;
            if (index >= 0 && index <= _runes.Count) _runes.Insert(index, rune);
            else _runes.Add(rune);
            Changed?.Invoke();
            return true;
        }

        public StoredRune RemoveAt(int index)
        {
            if (!IsValid(index)) return null;
            StoredRune rune = _runes[index];
            _runes.RemoveAt(index);
            Changed?.Invoke();
            return rune;
        }

        /// <summary>Hebt eine gelagerte Rune eine Stufe an. False, wenn sie schon die höchste Stufe hat.</summary>
        public bool Upgrade(int index)
        {
            if (!IsValid(index) || _runes[index].Level >= _runes[index].Rune.MaxLevel) return false;
            _runes[index] = new StoredRune(_runes[index].Rune, _runes[index].Level + 1);
            Changed?.Invoke();
            return true;
        }

        public int IndexOf(RuneDefinition rune)
        {
            for (int i = 0; i < _runes.Count; i++) if (rune != null && _runes[i].Rune.Id == rune.Id) return i;
            return -1;
        }

        public bool IsValid(int index) => index >= 0 && index < _runes.Count;
    }
}
