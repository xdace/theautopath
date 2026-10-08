using System;
using System.Collections.Generic;

namespace Betaknight.Core.Gear
{
    /// <summary>Was im Item-Raster liegen kann.</summary>
    public enum InventoryItemKind
    {
        Equipment,

        /// <summary>Verbrauchsgegenstände (folgen in einem eigenen Auftrag; das Raster nimmt sie schon auf).</summary>
        Consumable,
    }

    /// <summary>Ein Gegenstand im Item-Raster: Ausrüstung oder später Verbrauchsgegenstand.</summary>
    public interface IInventoryItem
    {
        string Id { get; }
        string Name { get; }
        InventoryItemKind ItemKind { get; }
    }

    /// <summary>
    /// Item-Raster: feste Anzahl Zellen, jede leer oder mit einem Gegenstand. Der Spieler sortiert selbst
    /// (<see cref="Move"/>: auf leere Zelle verschieben, auf volle tauschen); Herausnehmen lässt eine Lücke, die
    /// Reihenfolge bleibt erhalten. Was nicht mehr hineinpasst, muss die Session als Entscheidung vorlegen.
    /// Indizes sind immer Zellen-Indizes.
    /// </summary>
    public sealed class Inventory
    {
        public const int DefaultCapacity = 12;

        private readonly IInventoryItem[] _cells;
        private readonly List<EquipmentDefinition> _equipment = new List<EquipmentDefinition>();

        /// <summary>Anzahl der Zellen.</summary>
        public int Capacity => _cells.Length;

        /// <summary>Alle Zellen in Reihenfolge, leere als null.</summary>
        public IReadOnlyList<IInventoryItem> Cells => _cells;

        /// <summary>Die Ausrüstung im Raster in Zellen-Reihenfolge, ohne Lücken (zum Durchgehen, nicht zum Indizieren).</summary>
        public IReadOnlyList<EquipmentDefinition> Items => _equipment;

        /// <summary>Belegte Zellen.</summary>
        public int Count { get; private set; }

        public bool IsFull => Count >= Capacity;

        public event Action Changed;

        public Inventory(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _cells = new IInventoryItem[capacity];
        }

        /// <summary>Ausrüstung in dieser Zelle, sonst null (leer, ungültig oder kein Ausrüstungsteil).</summary>
        public EquipmentDefinition this[int cell] => ItemAt(cell) as EquipmentDefinition;

        /// <summary>Gegenstand in dieser Zelle oder null.</summary>
        public IInventoryItem ItemAt(int cell) => IsCell(cell) ? _cells[cell] : null;

        /// <summary>Legt einen Gegenstand in die erste freie Zelle.</summary>
        public bool TryAdd(IInventoryItem item) => TryPlace(item, FirstFreeCell());

        /// <summary>Legt einen Gegenstand in eine bestimmte freie Zelle.</summary>
        public bool TryPlace(IInventoryItem item, int cell)
        {
            if (item == null || !IsCell(cell) || _cells[cell] != null) return false;
            _cells[cell] = item;
            Refresh();
            return true;
        }

        /// <summary>Nimmt den Gegenstand einer Zelle heraus (die Zelle bleibt leer). Null bei leerer oder ungültiger Zelle.</summary>
        public EquipmentDefinition RemoveAt(int cell) => TakeAt(cell) as EquipmentDefinition;

        public IInventoryItem TakeAt(int cell)
        {
            if (!IsValid(cell)) return null;
            IInventoryItem item = _cells[cell];
            _cells[cell] = null;
            Refresh();
            return item;
        }

        /// <summary>Zelle auf Zelle: auf eine leere verschieben, auf eine volle tauschen. False bei ungültigen Zellen.</summary>
        public bool Move(int from, int to)
        {
            if (!IsValid(from) || !IsCell(to)) return false;
            if (from == to) return true;
            IInventoryItem moving = _cells[from];
            _cells[from] = _cells[to];
            _cells[to] = moving;
            Refresh();
            return true;
        }

        public bool Contains(string itemId) => IndexOf(itemId) >= 0;

        /// <summary>Zelle des ersten Gegenstands mit dieser Id oder −1.</summary>
        public int IndexOf(string itemId)
        {
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] != null && _cells[i].Id == itemId) return i;
            return -1;
        }

        /// <summary>Ersetzt den Gegenstand einer Zelle an seiner Stelle, z. B. durch eine höhere Stufe.</summary>
        public bool ReplaceAt(int cell, IInventoryItem item)
        {
            if (!IsValid(cell) || item == null) return false;
            _cells[cell] = item;
            Refresh();
            return true;
        }

        public int FirstFreeCell()
        {
            for (int i = 0; i < _cells.Length; i++) if (_cells[i] == null) return i;
            return -1;
        }

        /// <summary>Zelle existiert.</summary>
        public bool IsCell(int cell) => cell >= 0 && cell < _cells.Length;

        /// <summary>Zelle existiert und ist belegt.</summary>
        public bool IsValid(int cell) => IsCell(cell) && _cells[cell] != null;

        private void Refresh()
        {
            _equipment.Clear();
            int count = 0;
            foreach (IInventoryItem item in _cells)
            {
                if (item == null) continue;
                count++;
                if (item is EquipmentDefinition equipment) _equipment.Add(equipment);
            }
            Count = count;
            Changed?.Invoke();
        }
    }
}
