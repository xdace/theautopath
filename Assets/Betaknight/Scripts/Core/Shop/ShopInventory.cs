using System;
using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Shop
{
    /// <summary>Preise im Shop. Werte als Felder, damit sie leicht anpassbar sind.</summary>
    public sealed class ShopPrices
    {
        public int Rune = 10;
        public int Heal = 5;
        public int HealAmount = 10;
        public int Slot = 20;
        public int Reroll = 3;
        public int Item = 12;
        public int Skill = 14;

        /// <summary>Teurer Modul-Platz (nicht in jedem Shop).</summary>
        public int Module = 30;

        /// <summary>Logik-Chip (A-20, nicht in jedem Shop).</summary>
        public int Chip = 15;

        /// <summary>Verkauf aus dem Inventar: halber Preis.</summary>
        public int SellItem => Item / 2;
        public int SellRune => Rune / 2;
    }

    /// <summary>Warenbestand eines Shops. Bleibt pro Shop-Feld erhalten, Gekauftes verschwindet.</summary>
    public sealed class ShopInventory
    {
        private readonly List<RuneDefinition> _runes;
        private readonly List<string> _items = new List<string>();
        private readonly List<string> _skills = new List<string>();
        private readonly List<string> _modules = new List<string>();

        /// <summary>Module im Angebot (selten, teurer Platz).</summary>
        public IReadOnlyList<string> ModuleIds => _modules;

        internal void RemoveModuleAt(int index) => _modules.RemoveAt(index);

        internal void ReplaceModules(IEnumerable<string> moduleIds)
        {
            _modules.Clear();
            _modules.AddRange(moduleIds);
        }

        private readonly List<string> _chips = new List<string>();

        /// <summary>Logik-Chips im Angebot (A-20).</summary>
        public IReadOnlyList<string> ChipIds => _chips;

        internal void RemoveChipAt(int index) => _chips.RemoveAt(index);

        internal void ReplaceChips(IEnumerable<string> chipIds)
        {
            _chips.Clear();
            if (chipIds != null) _chips.AddRange(chipIds);
        }

        /// <summary>Skills im Angebot (Ids aus dem Skill-Katalog).</summary>
        public IReadOnlyList<string> SkillIds => _skills;

        public IReadOnlyList<RuneDefinition> Runes => _runes;

        /// <summary>Ausrüstung im Angebot (Ids aus dem Ausrüstungs-Katalog).</summary>
        public IReadOnlyList<string> ItemIds => _items;

        /// <summary>Der zusätzliche Runenplatz ist pro Shop nur einmal käuflich.</summary>
        public bool SlotSold { get; internal set; }

        public ShopInventory(IEnumerable<RuneDefinition> runes, IEnumerable<string> itemIds = null, IEnumerable<string> skillIds = null,
            IEnumerable<string> moduleIds = null)
        {
            if (moduleIds != null) _modules.AddRange(moduleIds);
            _runes = new List<RuneDefinition>(runes ?? throw new ArgumentNullException(nameof(runes)));
            if (itemIds != null) _items.AddRange(itemIds);
            if (skillIds != null) _skills.AddRange(skillIds);
        }

        internal void RemoveSkillAt(int index) => _skills.RemoveAt(index);

        internal void ReplaceSkills(IEnumerable<string> skillIds)
        {
            _skills.Clear();
            _skills.AddRange(skillIds);
        }

        internal void RemoveItemAt(int index) => _items.RemoveAt(index);

        internal void ReplaceItems(IEnumerable<string> itemIds)
        {
            _items.Clear();
            _items.AddRange(itemIds);
        }

        internal void Remove(RuneDefinition rune) => _runes.Remove(rune);

        internal void Replace(IEnumerable<RuneDefinition> runes)
        {
            _runes.Clear();
            _runes.AddRange(runes);
        }
    }
}
