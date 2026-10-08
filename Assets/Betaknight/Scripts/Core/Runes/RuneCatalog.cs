using System;
using System.Collections.Generic;

namespace Betaknight.Core.Runes
{
    /// <summary>Alle Runen des Spiels, nachschlagbar über die Id.</summary>
    public sealed class RuneCatalog
    {
        private readonly Dictionary<string, RuneDefinition> _byId = new Dictionary<string, RuneDefinition>();
        private readonly List<RuneDefinition> _all = new List<RuneDefinition>();

        public IReadOnlyList<RuneDefinition> All => _all;

        public RuneCatalog(IEnumerable<RuneDefinition> runes)
        {
            if (runes == null) throw new ArgumentNullException(nameof(runes));
            foreach (RuneDefinition r in runes)
            {
                if (_byId.ContainsKey(r.Id)) throw new ArgumentException($"Doppelte Runen-Id {r.Id}.");
                _byId.Add(r.Id, r);
                _all.Add(r);
            }
        }

        public bool TryGet(string id, out RuneDefinition rune)
        {
            rune = null;
            return id != null && _byId.TryGetValue(id, out rune);
        }

        public RuneDefinition Get(string id)
        {
            if (!TryGet(id, out RuneDefinition r)) throw new KeyNotFoundException($"Unbekannte Rune {id}.");
            return r;
        }

        public static RuneCatalog CreateDefault() => new RuneCatalog(new[]
        {
            new RuneDefinition("whetstone", "Wetzstein", RuneTag.Blade, "Bei Treffer: +1 Angriff bis Kampfende."),
            new RuneDefinition("frenzy", "Raserei", RuneTag.Blade, "Wenn Angriff 5 erreicht: Angriffe treffen doppelt."),
            new RuneDefinition("execution", "Hinrichtung", RuneTag.Blade, "Gegner unter 25 % Leben erhalten doppelten Schaden."),

            new RuneDefinition("bulwark", "Bollwerk", RuneTag.Shield, "Wenn getroffen: +2 Block."),
            new RuneDefinition("riposte", "Riposte", RuneTag.Shield, "Wenn Block einen Treffer ganz abfängt: Gegenangriff."),
            new RuneDefinition("iron_skin", "Eisenhaut", RuneTag.Shield, "Kampfbeginn: Block in Höhe von 10 % Max-HP."),

            new RuneDefinition("spark_counter", "Funkenzähler", RuneTag.Spark, "Jeder 3. Angriff: Blitz auf einen zufälligen Gegner."),
            new RuneDefinition("overload", "Überladung", RuneTag.Spark, "Zähler-Runen lösen einen Schritt früher aus."),
            new RuneDefinition("chain", "Kettenblitz", RuneTag.Spark, "Blitze springen auf einen zweiten Gegner über."),

            new RuneDefinition("kindling", "Zunder", RuneTag.Ember, "Bei Treffer: Gegner brennt 2 Runden."),
            new RuneDefinition("wildfire", "Lauffeuer", RuneTag.Ember, "Stirbt ein brennender Gegner, springt das Feuer über."),
            new RuneDefinition("forge_heart", "Schmiedeherz", RuneTag.Ember, "Pro brennendem Gegner: +1 Angriff."),
        });
    }
}
