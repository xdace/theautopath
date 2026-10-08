using System;
using System.Collections.Generic;

namespace Betaknight.Core.Modules
{
    /// <summary>Wo ein Modul sitzt und was es verändert.</summary>
    public enum ModuleKind
    {
        /// <summary>Verändert seinen Skill (Mehrfach, Fläche, Kette, HP-Kosten, Schnellcast). Sitzt an einem Skill-Exemplar.</summary>
        Skill,

        /// <summary>Verändert die Bedingung seines Logikbausteins (Umkehren, Verlängern, Schwelle). Sitzt an einer Tafel-Zeile.</summary>
        Block,

        /// <summary>
        /// Auslöser: am Skill «nach Ausführung», am Baustein «wenn erfüllt» löst er ein Ziel aus (Skill-Exemplar oder Zeile).
        /// </summary>
        Trigger,
    }

    public static class ModuleIds
    {
        public const string Multicast = "multicast";
        public const string Area = "area";
        public const string Chain = "chain";
        public const string BloodCost = "blood_cost";
        public const string Quickcast = "quickcast";
        public const string Invert = "invert";
        public const string Extend = "extend";
        public const string Threshold = "threshold";
        public const string Trigger = "trigger";
    }

    /// <summary>Ein Modul als Karte: Name, Art, Wirkung je Stufe als Text. Die Regeln stehen in <see cref="ModuleRules"/>.</summary>
    public sealed class ModuleDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public ModuleKind Kind { get; }

        /// <summary>Beschreibung je Stufe (Index = Stufe). Die letzte gilt für alle höheren.</summary>
        public IReadOnlyList<string> Descriptions { get; }

        /// <summary>Gewicht in Angeboten.</summary>
        public int Weight { get; }

        public ModuleDefinition(string id, string name, ModuleKind kind, IEnumerable<string> descriptions, int weight = 10)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            Kind = kind;
            Descriptions = new List<string>(descriptions ?? new[] { string.Empty });
            Weight = Math.Max(0, weight);
        }

        public string DescriptionAt(int level) => Descriptions.Count == 0 ? string.Empty : Descriptions[Math.Max(0, Math.Min(level, Descriptions.Count - 1))];

        public string Description => DescriptionAt(0);

        /// <summary>Passt das Modul an einen Skill bzw. an einen Baustein?</summary>
        public bool FitsSkill => Kind == ModuleKind.Skill || Kind == ModuleKind.Trigger;
        public bool FitsBlock => Kind == ModuleKind.Block || Kind == ModuleKind.Trigger;

        public override string ToString() => Name;
    }

    public sealed class ModuleCatalog
    {
        private readonly List<ModuleDefinition> _all = new List<ModuleDefinition>();

        public IReadOnlyList<ModuleDefinition> All => _all;

        public void Register(ModuleDefinition module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            _all.RemoveAll(m => m.Id == module.Id);
            _all.Add(module);
        }

        public bool TryGet(string id, out ModuleDefinition module)
        {
            module = _all.Find(m => m.Id == id);
            return module != null;
        }

        public ModuleDefinition Get(string id) =>
            TryGet(id, out ModuleDefinition m) ? m : throw new KeyNotFoundException($"Unbekanntes Modul {id}.");

        public bool Contains(string id) => TryGet(id, out _);

        public static ModuleCatalog CreateDefault()
        {
            var c = new ModuleCatalog();
            c.Register(new ModuleDefinition(ModuleIds.Multicast, "Mehrfach", ModuleKind.Skill, new[]
            {
                "Mehrfach ×2: die Wirkung wird nach erneuter Cast-Zeit wiederholt.",
                "Mehrfach ×3: die Wirkung wird zweimal wiederholt, jedes Mal mit Cast-Zeit.",
            }, weight: 6));
            c.Register(new ModuleDefinition(ModuleIds.Area, "Fläche", ModuleKind.Skill, new[]
            {
                "Fläche: Schaden trifft alle Gegner mit 70 %.",
                "Fläche: Schaden trifft alle Gegner mit 85 %.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Chain, "Kette", ModuleKind.Skill, new[]
            {
                "Kette auf 2 Ziele: zielgerichtete Wirkungen treffen einen weiteren Gegner.",
                "Kette auf 3 Ziele: zielgerichtete Wirkungen treffen zwei weitere Gegner.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.BloodCost, "Blutzoll", ModuleKind.Skill, new[]
            {
                "Kostet 5 % Max-HP statt Cooldown.",
                "Kostet 4 % Max-HP statt Cooldown.",
            }, weight: 6));
            c.Register(new ModuleDefinition(ModuleIds.Quickcast, "Schnellcast", ModuleKind.Skill, new[]
            {
                "Schnellcast: −30 % Cast-Zeit, +30 % Cooldown.",
                "Schnellcast: −40 % Cast-Zeit, +30 % Cooldown.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Invert, "Umkehren", ModuleKind.Block, new[]
            {
                "Umkehren (NICHT): die Zeile gilt, wenn die Bedingung nicht erfüllt ist.",
            }, weight: 8));
            c.Register(new ModuleDefinition(ModuleIds.Extend, "Verlängern", ModuleKind.Block, new[]
            {
                "Verlängern: die Bedingung gilt 1 s länger.",
                "Verlängern: die Bedingung gilt 1,5 s länger.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Threshold, "Schwelle", ModuleKind.Block, new[]
            {
                "Schwelle +10 % (nur Runen mit Prozent-Schwelle, z. B. «HP unter 30 %» → 40 %).",
                "Schwelle +15 % (nur Runen mit Prozent-Schwelle).",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Trigger, "Auslöser", ModuleKind.Trigger, new[]
            {
                "Am Skill: nach der Ausführung Ziel auslösen. Am Baustein: wenn er erfüllt wird, Ziel auslösen. "
                + "Das Ziel castet normal; ist es nicht bereit, verfällt der Auslöser.",
            }, weight: 8));
            return c;
        }
    }
}
