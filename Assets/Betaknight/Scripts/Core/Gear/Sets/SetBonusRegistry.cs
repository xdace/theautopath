using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Verbindet Set-Ids mit ihren Kampfregeln und Anzeige-Daten. Ein neues Set braucht Katalog-Teile mit der Set-Id
    /// und eine Registrierung hier; die Regeln selbst sind <see cref="BattleModifier"/>, der Simulator bleibt unverändert.
    /// </summary>
    public sealed class SetBonusRegistry
    {
        private readonly Dictionary<string, Func<int, BattleModifier>> _factories = new Dictionary<string, Func<int, BattleModifier>>();
        private readonly Dictionary<string, SetDefinition> _definitions = new Dictionary<string, SetDefinition>();
        private readonly List<SetDefinition> _all = new List<SetDefinition>();

        public IReadOnlyList<SetDefinition> All => _all;

        /// <param name="factory">Bekommt die Anzahl getragener Teile, gibt null zurück, wenn noch kein Bonus greift.</param>
        public void Register(SetDefinition set, Func<int, BattleModifier> factory)
        {
            if (set == null) throw new ArgumentNullException(nameof(set));
            if (_definitions.TryGetValue(set.Id, out SetDefinition old)) _all.Remove(old);
            _factories[set.Id] = factory ?? throw new ArgumentNullException(nameof(factory));
            _definitions[set.Id] = set;
            _all.Add(set);
        }

        public bool TryGet(string setId, out SetDefinition set)
        {
            set = null;
            return setId != null && _definitions.TryGetValue(setId, out set);
        }

        public string NameOf(string setId) => TryGet(setId, out SetDefinition s) ? s.Name : setId;

        public IEnumerable<string> RegisteredSetIds => _factories.Keys;

        /// <summary>Alle aktiven Set-Regeln für die getragene Ausrüstung.</summary>
        public List<BattleModifier> CreateModifiers(Equipment equipment)
        {
            var result = new List<BattleModifier>();
            if (equipment == null) return result;
            foreach (SetDefinition set in _all)
            {
                int pieces = equipment.SetPieces(set.Id);
                if (pieces <= 0) continue;
                BattleModifier modifier = _factories[set.Id](pieces);
                if (modifier != null) result.Add(modifier);
            }
            return result;
        }

        private static SetDefinition Def(string id, string name, string two, string three) =>
            new SetDefinition(id, name, new Dictionary<int, string> { { 2, two }, { 3, three } });

        public static SetBonusRegistry CreateDefault()
        {
            var r = new SetBonusRegistry();
            r.Register(Def(SetIds.Overload, "Overload Protocol",
                    "Every Basic Attack: +1 Haste stack (+10 % Attack Speed), 2 % Max HP Heat damage.",
                    "Heat damage is at most 1 while a powered healing component is on the board."),
                p => p >= 2 ? new OverloadSet(p >= 3) : null);
            r.Register(Def(SetIds.Aegis, "Aegis Firewall",
                    "Every Block: +1 Static (max. 5), at 5 Static Armor ×2. Unlocks the rune \"Static Full\".",
                    "Components powered by \"Static Full\" discharge: Static to 0, Damage = 5 × Armor."),
                p => p >= 2 ? new AegisSet(p >= 3) : null);
            r.Register(Def(SetIds.Scrap, "Scrap Harvester",
                    "+2 Gold per defeated enemy.",
                    "On Gold Mines: +50 % Area Damage, your attacks ignore Armor."),
                p => p >= 2 ? new ScrapHarvesterSet(p >= 3) : null);
            r.Register(Def(SetIds.Phantom, "Phantom Signal",
                    "+20 % Dodge.",
                    "Every Dodge hastes you (−25 % Cast Time for 2 s), Dodge cap 75 %."),
                p => p >= 2 ? new PhantomSet(p >= 3) : null);
            return r;
        }
    }
}
