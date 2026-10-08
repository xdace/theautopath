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
        /// <summary>Erster Reroll eines Shop-Besuchs.</summary>
        public int Reroll = 3;

        /// <summary>Jeder weitere Reroll im selben Besuch kostet so viel mehr (A-21).</summary>
        public int RerollStep = 2;

        /// <summary>So viele Angebote lassen sich gleichzeitig sperren (Lock, A-21).</summary>
        public int LockSlots = 2;
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

    /// <summary>Art eines Shop-Angebots (für Lock, A-21).</summary>
    public enum ShopOfferKind
    {
        Rune,
        Item,
        Skill,
        Module,
        Chip,
    }

    /// <summary>Ein gesperrtes Angebot: Art und Id.</summary>
    public readonly struct ShopOffer : IEquatable<ShopOffer>
    {
        public readonly ShopOfferKind Kind;
        public readonly string Id;

        public ShopOffer(ShopOfferKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public bool Equals(ShopOffer other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is ShopOffer o && Equals(o);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Id?.GetHashCode() ?? 0);
        public override string ToString() => $"{Kind} {Id}";
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

        /// <summary>Steht dieses Angebot im Bestand?</summary>
        public bool Has(ShopOfferKind kind, string id)
        {
            switch (kind)
            {
                case ShopOfferKind.Rune: return _runes.Exists(r => r.Id == id);
                case ShopOfferKind.Item: return _items.Contains(id);
                case ShopOfferKind.Skill: return _skills.Contains(id);
                case ShopOfferKind.Module: return _modules.Contains(id);
                default: return _chips.Contains(id);
            }
        }

        /// <summary>Id des Angebots an dieser Stelle (Runen: Runen-Id), null ausserhalb.</summary>
        public string IdAt(ShopOfferKind kind, int index)
        {
            switch (kind)
            {
                case ShopOfferKind.Rune: return index >= 0 && index < _runes.Count ? _runes[index].Id : null;
                case ShopOfferKind.Item: return index >= 0 && index < _items.Count ? _items[index] : null;
                case ShopOfferKind.Skill: return index >= 0 && index < _skills.Count ? _skills[index] : null;
                case ShopOfferKind.Module: return index >= 0 && index < _modules.Count ? _modules[index] : null;
                default: return index >= 0 && index < _chips.Count ? _chips[index] : null;
            }
        }

        /// <summary>Gesperrtes Angebot (Lock) vorne einreihen, falls es fehlt. Es verdrängt das hinterste Angebot, die Zahl bleibt gleich.</summary>
        internal void Keep(ShopOfferKind kind, string id, RuneDefinition rune = null)
        {
            if (id == null || Has(kind, id)) return;
            switch (kind)
            {
                case ShopOfferKind.Rune: if (rune != null) KeepIn(_runes, rune); break;
                case ShopOfferKind.Item: KeepIn(_items, id); break;
                case ShopOfferKind.Skill: KeepIn(_skills, id); break;
                case ShopOfferKind.Module: KeepIn(_modules, id); break;
                default: KeepIn(_chips, id); break;
            }
        }

        private static void KeepIn<T>(List<T> offers, T offer)
        {
            int count = offers.Count;
            offers.Insert(0, offer);
            if (count > 0) offers.RemoveAt(offers.Count - 1);
        }

        internal void Replace(IEnumerable<RuneDefinition> runes)
        {
            _runes.Clear();
            _runes.AddRange(runes);
        }
    }
}
