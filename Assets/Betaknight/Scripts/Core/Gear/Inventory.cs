using System;
using System.Collections.Generic;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Ausrüstungs-Inventar: gesammelte Teile, die gerade nicht getragen werden. Begrenzte Plätze;
    /// was nicht mehr hineinpasst, muss die Session als Entscheidung (verwerfen oder ablehnen) vorlegen.
    /// </summary>
    public sealed class Inventory
    {
        public const int DefaultCapacity = 12;

        private readonly List<EquipmentDefinition> _items = new List<EquipmentDefinition>();

        public int Capacity { get; }
        public IReadOnlyList<EquipmentDefinition> Items => _items;
        public int Count => _items.Count;
        public bool IsFull => _items.Count >= Capacity;

        public event Action Changed;

        public Inventory(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public EquipmentDefinition this[int index] => IsValid(index) ? _items[index] : null;

        public bool TryAdd(EquipmentDefinition item)
        {
            if (item == null || IsFull) return false;
            _items.Add(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Nimmt ein Teil heraus. Null bei ungültigem Index.</summary>
        public EquipmentDefinition RemoveAt(int index)
        {
            if (!IsValid(index)) return null;
            EquipmentDefinition item = _items[index];
            _items.RemoveAt(index);
            Changed?.Invoke();
            return item;
        }

        public bool Contains(string itemId)
        {
            foreach (EquipmentDefinition item in _items) if (item.Id == itemId) return true;
            return false;
        }

        public int IndexOf(string itemId)
        {
            for (int i = 0; i < _items.Count; i++) if (_items[i].Id == itemId) return i;
            return -1;
        }

        /// <summary>Ersetzt ein Teil an seiner Stelle, z. B. durch eine höhere Stufe.</summary>
        public bool ReplaceAt(int index, EquipmentDefinition item)
        {
            if (!IsValid(index) || item == null) return false;
            _items[index] = item;
            Changed?.Invoke();
            return true;
        }

        public bool IsValid(int index) => index >= 0 && index < _items.Count;
    }
}
