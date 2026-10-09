using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Circuit
{
    /// <summary>Eigene Effekte der Platine (A-21). Die Id ist zugleich die Id des Moduls, Chips oder Skills, der sie trägt.</summary>
    public static class CircuitEffectIds
    {
        public const string Overclock = "overclock";
        public const string Interrupt = "interrupt";
        public const string ParallelThread = "parallel_thread";
        public const string Buffer = "buffer";
        public const string Recursion = "recursion";
        public const string Spillover = "spillover";
        public const string Amplifier = "amplifier";
        public const string Watchdog = "watchdog";
        public const string Overflow = "overflow";

        public const string BitFlip = "bit_flip";
        public const string Jam = "jam";
        public const string Hijack = "hijack";
        public const string ShortCircuit = "short_circuit";
        public const string Latency = "latency";
        public const string Firewall = "firewall";
    }

    /// <summary>In welcher Form ein Effekt ins Spiel kommt. Steht als Daten im <see cref="CircuitEffectCatalog"/>.</summary>
    public enum CircuitEffectForm
    {
        /// <summary>Modul an einer Komponente (A-07): die Komponente hat den Effekt.</summary>
        Module,

        /// <summary>Logik-Chip (A-20): berührte Komponenten haben den Effekt (Verstärker leitet Pulse, Watchdog ist ein Relais).</summary>
        Chip,

        /// <summary>Eigener Skill: die Komponente selbst hat den Effekt.</summary>
        Skill,
    }

    /// <summary>Worauf ein Effekt wirkt.</summary>
    public enum CircuitEffectScope
    {
        /// <summary>Verändert, wie seine Komponente feuert (Overclock, Interrupt, Parallel Thread, Buffer, Recursion).</summary>
        Component,

        /// <summary>Verstärkt Pulse (Amplifier).</summary>
        Pulse,

        /// <summary>Löst bei Stillstand aus (Watchdog).</summary>
        Watchdog,

        /// <summary>Regel für die ganze Platine, solange ein Träger liegt (Overflow, Firewall).</summary>
        Board,

        /// <summary>Hack gegen die gegnerische Platine bei jeder Ausführung des Trägers.</summary>
        Hack,
    }

    /// <summary>Alle Zahlen der Effekte als Daten (Startwerte).</summary>
    public sealed class CircuitEffectConfig
    {
        /// <summary>Overclock: Cast-Zeit in Prozent.</summary>
        public int OverclockCastPercent { get; set; } = -50;

        /// <summary>Overclock: Hitze für jede berührte Komponente pro Ausführung.</summary>
        public int OverclockHeatPerExecution { get; set; } = 1;

        /// <summary>Ab so viel Hitze überspringt eine Komponente eine Ausführung, danach ist die Hitze wieder 0.</summary>
        public int HeatSkipAt { get; set; } = 5;

        /// <summary>Buffer: so oft darf die Komponente gleichzeitig in der Warteschlange stehen.</summary>
        public int BufferEntries { get; set; } = 3;

        /// <summary>Überladung: Wirkung je Feld Ladung über der Grösse der Komponente (ohne Obergrenze).</summary>
        public int OverchargePowerPercentPerCell { get; set; } = 10;

        /// <summary>Recursion: Wirkung je Tiefe.</summary>
        public int RecursionPowerPercentPerDepth { get; set; } = 20;

        /// <summary>Recursion: grösste Tiefe; danach endet die Rekursion («Stack Limit»).</summary>
        public int RecursionMaxDepth { get; set; } = 5;

        /// <summary>Amplifier: Wirkung je Verstärker auf dem Weg eines Pulses.</summary>
        public int AmplifierPowerPercent { get; set; } = 15;

        /// <summary>Watchdog: so lange darf nichts gefeuert haben.</summary>
        public int WatchdogIdleTicks { get; set; } = Ticks.FromSeconds(2);

        /// <summary>Watchdog: Schwierigkeit (Grössen-Grenze und Bonus) des Watchdog-Relais.</summary>
        public int WatchdogDifficulty { get; set; } = 2;

        /// <summary>Overflow: ab so vielen Einträgen wird jeder weitere sofort zum Schock.</summary>
        public int OverflowQueueLimit { get; set; } = 3;

        /// <summary>Overflow: Schaden des Schocks in Prozent der Grössen-Wucht der Komponente (siehe SkillBudget), an alle Gegner.</summary>
        public int OverflowDamagePercent { get; set; } = 50;

        /// <summary>Bit Flip: so lange ist das Relais umgekehrt.</summary>
        public int BitFlipTicks { get; set; } = Ticks.FromSeconds(3);

        /// <summary>Jam: so viele Auslösungen ignoriert das Relais.</summary>
        public int JamTriggers { get; set; } = 2;

        /// <summary>Latency: Cast-Zeit der Gegner in Prozent.</summary>
        public int LatencyCastPercent { get; set; } = 50;

        /// <summary>Latency: Dauer.</summary>
        public int LatencyTicks { get; set; } = Ticks.FromSeconds(4);

        /// <summary>Firewall: so viele Hacks blockt jeder Träger pro Kampf.</summary>
        public int FirewallCharges { get; set; } = 1;

        public static CircuitEffectConfig Default { get; } = new CircuitEffectConfig();
    }

    /// <summary>Ein Effekt als Daten: Name, Text, Form, Wirkungsbereich, Farbe und Symbol für die Anzeige.</summary>
    public sealed class CircuitEffectDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public CircuitEffectForm Form { get; }
        public CircuitEffectScope Scope { get; }

        /// <summary>Farbe als Hex («#ff8a3d») für Symbol, Rahmen und Protokoll.</summary>
        public string Colour { get; }

        /// <summary>Kurzes Symbol für Platine und HUD.</summary>
        public string Icon { get; }

        /// <summary>Gewicht als Belohnung (Modul, Chip oder Skill).</summary>
        public int Weight { get; }

        /// <summary>Nur bei Form Skill: Form und Cast-Zeit des Skills.</summary>
        public Shape SkillShape { get; }
        public int SkillCastTicks { get; }

        public CircuitEffectDefinition(string id, string name, string description, CircuitEffectForm form, CircuitEffectScope scope,
            string colour, string icon, int weight = 4, Shape? skillShape = null, int? skillCastTicks = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            Description = description ?? string.Empty;
            Form = form;
            Scope = scope;
            Colour = colour ?? "#ffffff";
            Icon = icon ?? "•";
            Weight = Math.Max(0, weight);
            SkillShape = skillShape ?? Shape.One;
            SkillCastTicks = skillCastTicks ?? CastTime.Fast;
        }

        /// <summary>Dieselbe Definition in einer anderen Form (Daten, z. B. für Tests oder Balancing).</summary>
        public CircuitEffectDefinition InForm(CircuitEffectForm form) =>
            new CircuitEffectDefinition(Id, Name, Description, form, Scope, Colour, Icon, Weight, SkillShape, SkillCastTicks);

        public bool IsHack => Scope == CircuitEffectScope.Hack;

        public override string ToString() => Name;
    }

    /// <summary>
    /// Alle eigenen Effekte der Platine (A-21) mit ihrer Form. Module-, Chip- und Skill-Katalog lesen von hier, welche
    /// Effekte sie als Modul, Chip bzw. Skill anbieten; die Wirkung im Kampf hängt nur an der Id, nicht an der Form.
    /// </summary>
    public sealed class CircuitEffectCatalog
    {
        private readonly List<CircuitEffectDefinition> _all = new List<CircuitEffectDefinition>();

        public CircuitEffectConfig Config { get; }

        public CircuitEffectCatalog(CircuitEffectConfig config = null) => Config = config ?? CircuitEffectConfig.Default;

        public IReadOnlyList<CircuitEffectDefinition> All => _all;

        public void Register(CircuitEffectDefinition effect)
        {
            if (effect == null) throw new ArgumentNullException(nameof(effect));
            _all.RemoveAll(e => e.Id == effect.Id);
            _all.Add(effect);
        }

        public bool TryGet(string id, out CircuitEffectDefinition effect)
        {
            effect = id == null ? null : _all.Find(e => e.Id == id);
            return effect != null;
        }

        public bool Contains(string id) => TryGet(id, out _);

        public IEnumerable<CircuitEffectDefinition> InForm(CircuitEffectForm form) => _all.Where(e => e.Form == form);

        /// <summary>Kopie mit einem Effekt in anderer Form.</summary>
        public CircuitEffectCatalog WithForm(string id, CircuitEffectForm form)
        {
            var copy = new CircuitEffectCatalog(Config);
            foreach (CircuitEffectDefinition e in _all) copy.Register(e.Id == id ? e.InForm(form) : e);
            return copy;
        }

        private static CircuitEffectCatalog _shared;

        /// <summary>Gemeinsamer Standard-Katalog (nur lesend genutzt).</summary>
        public static CircuitEffectCatalog Shared => _shared ?? (_shared = CreateDefault());

        public static CircuitEffectCatalog CreateDefault(CircuitEffectConfig config = null)
        {
            config = config ?? CircuitEffectConfig.Default;
            var c = new CircuitEffectCatalog(config);
            string Pct(int p) => p < 0 ? $"−{-p} %" : $"+{p} %";
            string Sec(int t) => ArenaTexts.Seconds(t);

            // Eigene Komponenten: Module.
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Overclock, "Overclock",
                $"{Pct(config.OverclockCastPercent)} computing time. Each execution gives touching components {config.OverclockHeatPerExecution} Heat; "
                + $"at {config.HeatSkipAt} Heat a component skips one execution, then its Heat resets.",
                CircuitEffectForm.Module, CircuitEffectScope.Component, "#ff6a3d", "⏫"));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Interrupt, "Interrupt",
                "Jumps to the front of the queue.", CircuitEffectForm.Module, CircuitEffectScope.Component, "#ffd23d", "⤒"));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.ParallelThread, "Parallel Thread",
                "When triggered while another execution runs, it runs at the same time, bypassing the queue (once per trigger).",
                CircuitEffectForm.Module, CircuitEffectScope.Component, "#5ee1ff", "⇉"));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Buffer, "Buffer",
                $"May stand in the queue up to {config.BufferEntries} times instead of once.",
                CircuitEffectForm.Module, CircuitEffectScope.Component, "#9ad16b", "☰"));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Recursion, "Recursion",
                $"If its relay's condition still holds after it executed, it calls itself again; each depth +{config.RecursionPowerPercentPerDepth} % effect "
                + $"(max. depth {config.RecursionMaxDepth}).",
                CircuitEffectForm.Module, CircuitEffectScope.Component, "#c77dff", "↻"));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Spillover, "Spillover",
                "Overcharge from a relay is not used as a bonus: it charges the touching components instead "
                + "(smallest need first, the rest spread evenly).",
                CircuitEffectForm.Module, CircuitEffectScope.Component, "#7df9ff", "⤳"));

            // Pulse, Stillstand, ganze Platine: Chips.
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Amplifier, "Amplifier",
                $"Conducts pulses like a straight trace. Each pulse passing through gains +{config.AmplifierPowerPercent} % effect.",
                CircuitEffectForm.Chip, CircuitEffectScope.Pulse, "#ffb03d", "▲", weight: 3));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Watchdog, "Watchdog",
                $"If none of your components fired for {Sec(config.WatchdogIdleTicks)}, it triggers your largest touching component "
                + $"(as a difficulty {config.WatchdogDifficulty} relay).",
                CircuitEffectForm.Chip, CircuitEffectScope.Watchdog, "#7aa2ff", "⌚", weight: 3));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Overflow, "Overflow",
                $"While your queue holds more than {config.OverflowQueueLimit} entries, every further entry turns at once into a shock "
                + "against all enemies (damage by its size).",
                CircuitEffectForm.Chip, CircuitEffectScope.Board, "#ff5ea8", "⚠", weight: 2));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Firewall, "Firewall",
                $"Blocks the next {(config.FirewallCharges == 1 ? "enemy hack" : config.FirewallCharges + " enemy hacks")} against you.",
                CircuitEffectForm.Chip, CircuitEffectScope.Board, "#3dd68c", "▣", weight: 3));

            // Hacks gegen die gegnerische Platine: eigene Skills.
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.BitFlip, "Bit Flip",
                $"Hack: an enemy state relay that currently holds is inverted for {Sec(config.BitFlipTicks)} (its condition flips). Event relays like Clock cannot be flipped.",
                CircuitEffectForm.Skill, CircuitEffectScope.Hack, "#00e5c7", "⇅", skillShape: new Shape(1, 1), skillCastTicks: CastTime.Fast));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Jam, "Jam",
                $"Hack: the enemy's most important relay ignores its next {config.JamTriggers} triggers.",
                CircuitEffectForm.Skill, CircuitEffectScope.Hack, "#a0a0a0", "▧", skillShape: new Shape(1, 2), skillCastTicks: CastTime.Fast));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Hijack, "Hijack",
                "Hack: the next execution of the enemy's largest component happens for you.",
                CircuitEffectForm.Skill, CircuitEffectScope.Hack, "#ff3df2", "⇄", weight: 2, skillShape: new Shape(2, 2), skillCastTicks: CastTime.Medium));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.ShortCircuit, "Short Circuit",
                "Hack: the enemy's largest component fires immediately and hits its own side.",
                CircuitEffectForm.Skill, CircuitEffectScope.Hack, "#ffe14d", "ϟ", weight: 3, skillShape: new Shape(2, 1), skillCastTicks: CastTime.Medium));
            c.Register(new CircuitEffectDefinition(CircuitEffectIds.Latency, "Latency",
                $"Hack: enemy computing time +{config.LatencyCastPercent} % for {Sec(config.LatencyTicks)}.",
                CircuitEffectForm.Skill, CircuitEffectScope.Hack, "#6b8cff", "⏳", skillShape: new Shape(1, 2), skillCastTicks: CastTime.Fast));
            return c;
        }
    }
}
