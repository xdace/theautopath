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
            // Runen sind Bedingungen ("Wann") für die Logik-Tafel. Das "Was" liefert später die Ausrüstung.
            new RuneDefinition("on_hit", "Bei Treffer", RuneTag.Blade, "Wenn ein eigener Angriff trifft."),
            new RuneDefinition("enemy_low", "Gegner geschwächt", RuneTag.Blade, "Solange der Gegner unter 25 % Leben hat."),
            new RuneDefinition("every_5s", "Alle 5 Sekunden", RuneTag.Blade, "Alle 5 Sekunden einmal."),

            new RuneDefinition("when_hit", "Wenn getroffen", RuneTag.Shield, "Kurz nachdem der Ritter getroffen wurde."),
            new RuneDefinition("after_block", "Nach Block", RuneTag.Shield, "Kurz nachdem ein Treffer geblockt wurde."),
            new RuneDefinition("enemy_charging", "Gegner lädt auf", RuneTag.Shield, "Solange ein Gegner einen Angriff auflädt."),

            new RuneDefinition("every_3rd", "Jeder 3. Angriff", RuneTag.Spark, "Bei jedem dritten eigenen Angriff."),
            new RuneDefinition("battle_start", "Kampfbeginn", RuneTag.Spark, "Einmal zu Beginn des Kampfes."),
            new RuneDefinition("after_dodge", "Nach Ausweichen", RuneTag.Spark, "Kurz nachdem der Ritter ausgewichen ist."),

            new RuneDefinition("hp_low", "HP unter 30 %", RuneTag.Ember, "Solange das eigene Leben unter 30 % liegt."),
            new RuneDefinition("enemy_burning", "Gegner brennt", RuneTag.Ember, "Solange der Gegner brennt."),
            new RuneDefinition("enemy_dies", "Gegner fällt", RuneTag.Ember, "Kurz nachdem ein Gegner besiegt wurde."),
        });
    }
}
