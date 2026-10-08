using System;
using System.Collections.Generic;

namespace Betaknight.Core.Runes
{
    /// <summary>Alle Runen des Spiels, nachschlagbar über die Id. Neue Runen: Eintrag hier + Bedingung in der ConditionRegistry.</summary>
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

        private const int Common = 10;
        private const int Uncommon = 6;
        private const int Rare = 3;

        public static RuneCatalog CreateDefault() => new RuneCatalog(new[]
        {
            // Takt und Zähler
            new RuneDefinition("every_3rd", "Jeder {0}. Angriff", RuneTag.Spark, ConditionKind.Counter, "Bei jedem {0}. eigenen Angriff.", new[] { 3, 2 }),
            new RuneDefinition("every_nth_attack", "Nach jedem {0}. Angriff", RuneTag.Spark, ConditionKind.Counter, "Nach jedem {0}. eigenen Angriff.", new[] { 2 }, Uncommon),
            new RuneDefinition("every_5s", "Alle {0} Sekunden", RuneTag.Spark, ConditionKind.Clock, "Alle {0} Sekunden einmal.", new[] { 5, 4, 3 }),
            new RuneDefinition("every_20s", "Alle {0} Sekunden", RuneTag.Spark, ConditionKind.Clock, "Alle {0} Sekunden einmal. Für sehr starke Skills.", new[] { 20, 15 }, Rare),
            new RuneDefinition("battle_start", "Kampfbeginn", RuneTag.Spark, ConditionKind.Event, "Einmal zu Beginn des Kampfes."),
            new RuneDefinition("every_nth_hit_taken", "Jeder {0}. erlittene Treffer", RuneTag.Spark, ConditionKind.Counter, "Nach jedem {0}. Treffer, den der Ritter einsteckt.", new[] { 5, 4, 3 }),
            new RuneDefinition("chain", "Kette", RuneTag.Spark, ConditionKind.Event, "Direkt nachdem die Zeile darüber gefeuert hat.", null, Uncommon),
            new RuneDefinition("after_own_skill", "Nach eigenem Skill", RuneTag.Spark, ConditionKind.Event, "Nach jedem eigenen Skill ausser dem Basisangriff."),

            // Angriff
            new RuneDefinition("on_hit", "Bei Treffer", RuneTag.Blade, ConditionKind.Event, "Wenn ein eigener Angriff trifft."),
            new RuneDefinition("on_crit", "Nach Krit", RuneTag.Blade, ConditionKind.Event, "Nach einem eigenen kritischen Treffer."),
            new RuneDefinition("enemy_low", "Gegner unter {0} %", RuneTag.Blade, ConditionKind.State, "Solange ein Gegner unter {0} % Leben hat.", new[] { 25, 35 }),
            new RuneDefinition("enemy_armored", "Gegner gepanzert", RuneTag.Blade, ConditionKind.State, "Solange ein Gegner Rüstung hat."),
            new RuneDefinition("enemy_stunned", "Gegner betäubt", RuneTag.Blade, ConditionKind.State, "Solange ein Gegner betäubt ist."),
            new RuneDefinition("enemy_stronger", "Gegner stärker", RuneTag.Blade, ConditionKind.State, "Solange ein Gegner mehr Leben hat als der Ritter (absolut)."),
            new RuneDefinition("tempo_stacks", "Tempo ≥ {0}", RuneTag.Blade, ConditionKind.Resource, "Ab {0} Tempo-Stapeln.", new[] { 10, 8, 6 }, Uncommon),

            // Verteidigung
            new RuneDefinition("when_hit", "Wenn getroffen", RuneTag.Shield, ConditionKind.Event, "Kurz nachdem der Ritter getroffen wurde."),
            new RuneDefinition("after_block", "Nach Block", RuneTag.Shield, ConditionKind.Event, "Kurz nachdem ein Treffer geblockt wurde."),
            new RuneDefinition("enemy_charging", "Gegner lädt auf", RuneTag.Shield, ConditionKind.State, "Solange ein Gegner einen Angriff auflädt."),
            new RuneDefinition("hp_full", "HP voll", RuneTag.Shield, ConditionKind.State, "Solange das eigene Leben voll ist."),
            new RuneDefinition("big_hit_taken", "Schwerer Treffer", RuneTag.Shield, ConditionKind.Event, "Nach einem Treffer über {0} % Max-HP.", new[] { 15, 10 }),
            new RuneDefinition("charge_full", "Ladung voll", RuneTag.Shield, ConditionKind.Resource, "Bei {0} Ladung (Aegis-Firewall).", new[] { 5 }, 0, exclusive: true, unlockSetId: "aegis"),

            // Risiko und Notfall
            new RuneDefinition("hp_low", "HP unter {0} %", RuneTag.Ember, ConditionKind.State, "Solange das eigene Leben unter {0} % liegt.", new[] { 30, 40, 50 }),
            new RuneDefinition("hp_critical", "HP unter {0} %", RuneTag.Ember, ConditionKind.State, "Solange das eigene Leben unter {0} % liegt.", new[] { 10, 15 }, Uncommon),
            new RuneDefinition("after_self_damage", "Nach Selbstschaden", RuneTag.Ember, ConditionKind.Event, "Kurz nachdem der Ritter sich selbst verletzt hat."),
            new RuneDefinition("after_heal", "Nach Heilung", RuneTag.Ember, ConditionKind.Event, "Kurz nachdem der Ritter geheilt wurde."),
            new RuneDefinition("enemy_burning", "Gegner brennt", RuneTag.Ember, ConditionKind.State, "Solange ein Gegner brennt."),
            new RuneDefinition("enemy_dies", "Gegner fällt", RuneTag.Ember, ConditionKind.Event, "Kurz nachdem ein Gegner besiegt wurde."),
            new RuneDefinition("overheat", "Überhitzung", RuneTag.Ember, ConditionKind.State, "Sobald der Kampf das Zeitlimit überschreitet.", null, Uncommon),

            // Bewegung und Ausweichen
            new RuneDefinition("after_dodge", "Nach Ausweichen", RuneTag.Phantom, ConditionKind.Event, "Kurz nachdem der Ritter ausgewichen ist."),
            new RuneDefinition("dodge_streak", "{0} Ausweicher in Folge", RuneTag.Phantom, ConditionKind.Counter, "Nach {0} Ausweichern ohne Treffer dazwischen.", new[] { 2 }, Uncommon),
            new RuneDefinition("always", "Immer", RuneTag.Phantom, ConditionKind.State, "Immer wahr. Gebremst nur durch den Cooldown des Skills.", null, Rare),

            // Kontext
            new RuneDefinition("on_goldmine", "Auf Goldmine", RuneTag.Ember, ConditionKind.Context, "Wenn der Kampf auf einem Goldminen-Feld stattfindet.", null, Rare),
            new RuneDefinition("vs_boss", "Gegen Boss", RuneTag.Shield, ConditionKind.Context, "Wenn der Gegner ein Boss ist.", null, Rare),
            new RuneDefinition("outnumbered", "In Unterzahl", RuneTag.Shield, ConditionKind.State, "Solange mindestens {0} Gegner stehen.", new[] { 3 }, Rare),
            new RuneDefinition("last_enemy", "Letzter Gegner", RuneTag.Blade, ConditionKind.State, "Solange nur noch ein Gegner steht.", null, Rare),
        });
    }
}
