using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Verbindet Set-Ids mit ihren Kampfregeln. Ein neues Set braucht Katalog-Teile mit der Set-Id und
    /// eine Registrierung hier; die Regeln selbst sind <see cref="BattleModifier"/>, der Simulator bleibt unverändert.
    /// </summary>
    public sealed class SetBonusRegistry
    {
        private readonly Dictionary<string, Func<int, BattleModifier>> _factories = new Dictionary<string, Func<int, BattleModifier>>();
        private readonly Dictionary<string, string> _names = new Dictionary<string, string>();

        /// <param name="factory">Bekommt die Anzahl getragener Teile, gibt null zurück, wenn noch kein Bonus greift.</param>
        public void Register(string setId, string name, Func<int, BattleModifier> factory)
        {
            if (string.IsNullOrEmpty(setId)) throw new ArgumentException("Set-Id fehlt.", nameof(setId));
            _factories[setId] = factory ?? throw new ArgumentNullException(nameof(factory));
            _names[setId] = name ?? setId;
        }

        public string NameOf(string setId) => setId != null && _names.TryGetValue(setId, out string n) ? n : setId;

        public IEnumerable<string> RegisteredSetIds => _factories.Keys;

        /// <summary>Alle aktiven Set-Regeln für die getragene Ausrüstung.</summary>
        public List<BattleModifier> CreateModifiers(Equipment equipment)
        {
            var result = new List<BattleModifier>();
            if (equipment == null) return result;
            foreach (KeyValuePair<string, Func<int, BattleModifier>> pair in _factories)
            {
                int pieces = equipment.SetPieces(pair.Key);
                if (pieces <= 0) continue;
                BattleModifier modifier = pair.Value(pieces);
                if (modifier != null) result.Add(modifier);
            }
            return result;
        }

        public static SetBonusRegistry CreateDefault()
        {
            var r = new SetBonusRegistry();
            r.Register(SetIds.Overload, "Überlast-Protokoll", p => p >= 2 ? new OverloadSet(p >= 3) : null);
            r.Register(SetIds.Aegis, "Aegis-Firewall", p => p >= 2 ? new AegisSet(p >= 3) : null);
            r.Register(SetIds.Scrap, "Schrott-Ernter", p => p >= 2 ? new ScrapHarvesterSet(p >= 3) : null);
            r.Register(SetIds.Phantom, "Phantom-Signal", p => p >= 2 ? new PhantomSet(p >= 3) : null);
            return r;
        }
    }
}
