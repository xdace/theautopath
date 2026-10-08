using System;
using System.Collections.Generic;

namespace Betaknight.Core.Runes
{
    /// <summary>Alle Runen des Spiels, nachschlagbar über die Id. Neue Runen: Eintrag hier + Bedingung in der ConditionRegistry.</summary>
    /// <summary>Ids der Evolutionsformen von Bausteinen.</summary>
    public static class EvolvedRuneIds
    {
        public const string PhantomReflex = "phantom_reflex";
    }

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
            new RuneDefinition("every_3rd", "Every {0} Attacks", RuneTag.Spark, ConditionKind.Counter, "Once every {0} of your attacks.", new[] { 3, 2 }, difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("every_nth_attack", "After Every {0} Attacks", RuneTag.Spark, ConditionKind.Counter, "After every {0} of your attacks.", new[] { 2 }, Uncommon, difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("every_5s", "Every {0} Seconds", RuneTag.Spark, ConditionKind.Clock, "Once every {0} seconds.", new[] { 5, 4, 3 }, difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("every_20s", "Every {0} Seconds", RuneTag.Spark, ConditionKind.Clock, "Once every {0} seconds. For very strong skills.", new[] { 20, 15 }, Rare, difficulty: 3, invertedDifficulty: 0),
            new RuneDefinition("battle_start", "Battle Start", RuneTag.Spark, ConditionKind.Event, "Once at the start of the fight.", difficulty: 0, invertedDifficulty: 0),
            new RuneDefinition("every_nth_hit_taken", "Every {0} Hits Taken", RuneTag.Spark, ConditionKind.Counter, "After every {0} hits the knight takes.", new[] { 5, 4, 3 }, difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("chain", "Chain", RuneTag.Spark, ConditionKind.Event, "Right after the row above has fired.", null, Uncommon, difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("after_own_skill", "After Own Skill", RuneTag.Spark, ConditionKind.Event, "After each of your skills except the Basic Attack.", difficulty: 1, invertedDifficulty: 0),

            // Angriff
            new RuneDefinition("on_hit", "On Hit", RuneTag.Blade, ConditionKind.Event, "When one of your attacks hits.", difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("on_crit", "After Crit", RuneTag.Blade, ConditionKind.Event, "After one of your critical hits.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("enemy_low", "Enemy Below {0} %", RuneTag.Blade, ConditionKind.State, "While an enemy is below {0} % HP.", new[] { 25, 35 }, difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("enemy_armored", "Enemy Armored", RuneTag.Blade, ConditionKind.State, "While an enemy has Armor.", difficulty: 1, invertedDifficulty: 1),
            new RuneDefinition("enemy_stunned", "Enemy Stunned", RuneTag.Blade, ConditionKind.State, "While an enemy is stunned.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("enemy_stronger", "Enemy Stronger", RuneTag.Blade, ConditionKind.State, "While an enemy has more HP than the knight (absolute).", difficulty: 1, invertedDifficulty: 1),
            new RuneDefinition("tempo_stacks", "Haste ≥ {0}", RuneTag.Blade, ConditionKind.Resource, "From {0} Haste stacks.", new[] { 10, 8, 6 }, Uncommon, difficulty: 2, invertedDifficulty: 0),

            // Verteidigung
            new RuneDefinition("when_hit", "When Hit", RuneTag.Shield, ConditionKind.Event, "Shortly after the knight was hit.", difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("after_block", "After Block", RuneTag.Shield, ConditionKind.Event, "Shortly after a hit was blocked.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("enemy_charging", "Enemy Charging", RuneTag.Shield, ConditionKind.State, "While an enemy is charging an attack.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("hp_full", "HP Full", RuneTag.Shield, ConditionKind.State, "While your HP is full.", difficulty: 1, invertedDifficulty: 0),
            new RuneDefinition("big_hit_taken", "Heavy Hit", RuneTag.Shield, ConditionKind.Event, "After a hit for more than {0} % Max HP.", new[] { 15, 10 }, difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("charge_full", "Charge Full", RuneTag.Shield, ConditionKind.Resource, "At {0} Charge (Aegis Firewall).", new[] { 5 }, 0, exclusive: true, unlockSetId: "aegis", difficulty: 3, invertedDifficulty: 0),

            // Risiko und Notfall
            new RuneDefinition("hp_low", "HP Below {0} %", RuneTag.Ember, ConditionKind.State, "While your HP is below {0} %.", new[] { 30, 40, 50 }, difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("hp_critical", "HP Below {0} %", RuneTag.Ember, ConditionKind.State, "While your HP is below {0} %.", new[] { 10, 15 }, Uncommon, difficulty: 3, invertedDifficulty: 0),
            new RuneDefinition("after_self_damage", "After Self-Damage", RuneTag.Ember, ConditionKind.Event, "Shortly after the knight hurt itself.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("after_heal", "After Healing", RuneTag.Ember, ConditionKind.Event, "Shortly after the knight was healed.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("enemy_burning", "Enemy Burning", RuneTag.Ember, ConditionKind.State, "While an enemy is burning.", difficulty: 1, invertedDifficulty: 1),
            new RuneDefinition("enemy_dies", "Enemy Falls", RuneTag.Ember, ConditionKind.Event, "Shortly after an enemy was defeated.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("overheat", "Overheat", RuneTag.Ember, ConditionKind.State, "Once the fight exceeds the time limit.", null, Uncommon, difficulty: 3, invertedDifficulty: 0),

            // Bewegung und Ausweichen
            new RuneDefinition("after_dodge", "After Dodge", RuneTag.Phantom, ConditionKind.Event, "Shortly after the knight dodged.", difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("dodge_streak", "{0} Dodges in a Row", RuneTag.Phantom, ConditionKind.Counter, "After {0} dodges with no hit in between.", new[] { 2 }, Uncommon, difficulty: 3, invertedDifficulty: 0),
            new RuneDefinition("always", "Always", RuneTag.Phantom, ConditionKind.State, "Always true. Only held back by the skill's Cooldown.", null, Rare, difficulty: 0, invertedDifficulty: 3),

            // Kontext
            new RuneDefinition("on_goldmine", "On Gold Mine", RuneTag.Ember, ConditionKind.Context, "When the fight takes place on a Gold Mine tile.", null, Rare, difficulty: 3, invertedDifficulty: 0),
            new RuneDefinition("vs_boss", "Vs. Boss", RuneTag.Shield, ConditionKind.Context, "When the enemy is a Boss.", null, Rare, difficulty: 3, invertedDifficulty: 0),
            new RuneDefinition("outnumbered", "Outnumbered", RuneTag.Shield, ConditionKind.State, "While at least {0} enemies are standing.", new[] { 3 }, Rare, difficulty: 2, invertedDifficulty: 0),
            new RuneDefinition("last_enemy", "Last Enemy", RuneTag.Blade, ConditionKind.State, "While only one enemy is left standing.", null, Rare, difficulty: 1, invertedDifficulty: 1),

            // Evolutionsformen: nie angeboten, entstehen nur durch Evolution (siehe EvolutionCatalog).
            new RuneDefinition(EvolvedRuneIds.PhantomReflex, "HP Below {0} % or Dodged", RuneTag.Ember, ConditionKind.State,
                "While your HP is below {0} %, and shortly after every dodge.", new[] { 30, 40, 50 }, 0, exclusive: true, difficulty: 2, invertedDifficulty: 0),
        });
    }
}
