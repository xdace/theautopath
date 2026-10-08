using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Ids der Synergie-Tags auf Ausrüstung. Vorläufig; Namen und Wirkungen stehen in <see cref="SynergyRegistry.CreateDefault"/>.</summary>
    public static class SynergyTagIds
    {
        public const string Heat = "heat";
        public const string Charge = "charge";
        public const string Phantom = "phantom";
        public const string Tempo = "tempo";
        public const string Toxin = "toxin";
        public const string Scrap = "scrap";
    }

    /// <summary>
    /// Eine Stufe eines Tags oder eine Duo-Wirkung: Anzeigetext, passive Effekte auf Skills (Cast-Zeit, Wirkung, Cooldown)
    /// und optional eine Kampfregel. Reine Daten plus Fabrik.
    /// </summary>
    public sealed class SynergyEffect
    {
        public string Text { get; }
        public IReadOnlyList<SkillPassive> Passives { get; }
        private readonly Func<BattleModifier> _modifier;

        public SynergyEffect(string text, IEnumerable<SkillPassive> passives = null, Func<BattleModifier> modifier = null)
        {
            Text = text ?? string.Empty;
            Passives = new List<SkillPassive>(passives ?? Array.Empty<SkillPassive>());
            _modifier = modifier;
        }

        /// <summary>Neue Kampfregel für einen Kampf (Regeln dürfen Zustand halten), oder null.</summary>
        public BattleModifier CreateModifier() => _modifier?.Invoke();
    }

    /// <summary>Ein Synergie-Tag: Name und Wirkung je Schwelle (2/4/6 Teile). Stufen gelten zusammen, nicht statt.</summary>
    public sealed class SynergyTag
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>Wirkung je Schwelle, z. B. 2 → …, 4 → …, 6 → ….</summary>
        public IReadOnlyDictionary<int, SynergyEffect> Tiers { get; }

        public SynergyTag(string id, string name, IDictionary<int, SynergyEffect> tiers)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            Tiers = new SortedDictionary<int, SynergyEffect>(tiers ?? new Dictionary<int, SynergyEffect>());
        }
    }

    /// <summary>Duo-Synergie: wird frei, wenn beide Tags mindestens die Duo-Schwelle erreichen.</summary>
    public sealed class SynergyDuo
    {
        public string Id { get; }
        public string Name { get; }
        public string TagA { get; }
        public string TagB { get; }
        public SynergyEffect Effect { get; }

        public SynergyDuo(string id, string name, string tagA, string tagB, SynergyEffect effect)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (tagA == tagB) throw new ArgumentException("Ein Duo braucht zwei verschiedene Tags.", nameof(tagB));
            Id = id;
            Name = name ?? id;
            TagA = tagA;
            TagB = tagB;
            Effect = effect ?? new SynergyEffect(string.Empty);
        }

        public bool Uses(string tagId) => TagA == tagId || TagB == tagId;
    }

    /// <summary>Zählerstand eines Tags für HUD und Inventar: «Ladung 3/4».</summary>
    public readonly struct SynergyCounter
    {
        public readonly SynergyTag Tag;
        public readonly int Count;

        /// <summary>Höchste erreichte Schwelle, 0 ohne.</summary>
        public readonly int Reached;

        /// <summary>Nächste Schwelle, 0 wenn alle erreicht.</summary>
        public readonly int Next;

        public SynergyCounter(SynergyTag tag, int count, int reached, int next)
        {
            Tag = tag;
            Count = count;
            Reached = reached;
            Next = next;
        }

        /// <summary>«Ladung 3/4» oder bei allen Schwellen «Ladung 6 (max)».</summary>
        public string Text => Next > 0 ? $"{Tag.Name} {Count}/{Next}" : $"{Tag.Name} {Count} (max)";
    }
}
