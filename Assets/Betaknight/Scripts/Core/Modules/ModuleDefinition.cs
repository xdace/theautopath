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

        /// <summary>«Repeat while true» (A-19): Relais-Modul, versorgte Komponenten reihen sich erneut ein, solange die Bedingung gilt.</summary>
        public const string RepeatWhileTrue = "repeat_while_true";

        // Erleichterer (A-11): wirken, solange sie an einem Baustein sitzen (siehe ReliefCatalog).
        public const string AlarmSensor = Arena.ReliefCarrierIds.AlarmSensor;
        public const string Scent = Arena.ReliefCarrierIds.ScentModule;
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

        /// <param name="effects">Eigene Effekte der Platine (A-21): die in Form «Modul» kommen als Skill-Module dazu.</param>
        public static ModuleCatalog CreateDefault(Circuit.CircuitEffectCatalog effects = null)
        {
            var c = new ModuleCatalog();
            c.Register(new ModuleDefinition(ModuleIds.Multicast, "Multicast", ModuleKind.Skill, new[]
            {
                "Multicast ×2: the effect repeats after another Cast Time.",
                "Multicast ×3: the effect repeats twice, each time with Cast Time.",
            }, weight: 6));
            c.Register(new ModuleDefinition(ModuleIds.Area, "Area", ModuleKind.Skill, new[]
            {
                "Area: Damage hits all enemies at 70 %.",
                "Area: Damage hits all enemies at 85 %.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Chain, "Chain", ModuleKind.Skill, new[]
            {
                "Chain to 2 targets: targeted effects hit one more enemy.",
                "Chain to 3 targets: targeted effects hit two more enemies.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.BloodCost, "Blood Toll", ModuleKind.Skill, new[]
            {
                "Blood Toll: costs 5 % Max HP per cast, +40 % effect.",
                "Blood Toll: costs 4 % Max HP per cast, +50 % effect.",
            }, weight: 6));
            c.Register(new ModuleDefinition(ModuleIds.Quickcast, "Quickcast", ModuleKind.Skill, new[]
            {
                "Quickcast: −30 % Cast Time, −15 % effect.",
                "Quickcast: −40 % Cast Time, −15 % effect.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Invert, "Invert", ModuleKind.Block, new[]
            {
                "Invert (NOT): the relay triggers when the Condition is not met.",
            }, weight: 8));
            c.Register(new ModuleDefinition(ModuleIds.RepeatWhileTrue, "Repeat while true", ModuleKind.Block, new[]
            {
                "Repeat while true: after firing, the powered components queue again as long as the Condition still holds.",
            }, weight: 5));
            c.Register(new ModuleDefinition(ModuleIds.Extend, "Extend", ModuleKind.Block, new[]
            {
                "Extend: the Condition holds 1 s longer.",
                "Extend: the Condition holds 1.5 s longer.",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Threshold, "Threshold", ModuleKind.Block, new[]
            {
                "Threshold +10 % (only runes with a percent threshold, e.g. \"HP Below 30 %\" → 40 %).",
                "Threshold +15 % (only runes with a percent threshold).",
            }));
            c.Register(new ModuleDefinition(ModuleIds.Trigger, "Trigger", ModuleKind.Trigger, new[]
            {
                "On a component: triggers the target after it executes. On a relay: triggers the target when the relay triggers. "
                + "The target is queued and casts normally; if it is already queued, the trigger is missed.",
            }, weight: 8));
            c.Register(new ModuleDefinition(ModuleIds.AlarmSensor, "Alarm Sensor", ModuleKind.Block, new[]
            {
                "Easer: HP threshold runes (\"HP Below x %\") trigger 10 percentage points earlier. Their bonus stays.",
            }, weight: 6));
            c.Register(new ModuleDefinition(ModuleIds.Scent, "Scent", ModuleKind.Block, new[]
            {
                "Easer: \"Enemy Below x %\" triggers 10 percentage points earlier. The bonus stays.",
            }, weight: 6));
            foreach (Circuit.CircuitEffectDefinition e in (effects ?? Circuit.CircuitEffectCatalog.Shared).InForm(Circuit.CircuitEffectForm.Module))
                c.Register(new ModuleDefinition(e.Id, e.Name, ModuleKind.Skill, new[] { $"{e.Name}: {e.Description}" }, e.Weight));
            return c;
        }
    }
}
