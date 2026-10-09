using System;
using System.Collections.Generic;

namespace Betaknight.Core.Runes
{
    /// <summary>
    /// Ausrichtung einer Rune. Angebote bevorzugen Tags, die der Spieler schon hat, damit ein Build entsteht.
    /// </summary>
    public enum RuneTag
    {
        /// <summary>Angriff und Druck.</summary>
        Blade = 0,

        /// <summary>Verteidigung, Block und Konter.</summary>
        Shield = 1,

        /// <summary>Takt und Zähler.</summary>
        Spark = 2,

        /// <summary>Notfall, Feuer und Risiko.</summary>
        Ember = 3,

        /// <summary>Bewegung und Ausweichen.</summary>
        Phantom = 4,
    }

    /// <summary>Wie sich eine Bedingung verhält. Bestimmt nur Beschreibung und Anzeige, die Logik steckt in der Bedingung.</summary>
    public enum ConditionKind
    {
        /// <summary>Wahr, solange ein Zustand gilt.</summary>
        State,

        /// <summary>Wahr für ein kurzes Fenster nach einem Ereignis.</summary>
        Event,

        /// <summary>Wird nach einer Zeit fällig und bleibt fällig, bis die Zeile feuert.</summary>
        Clock,

        /// <summary>Wird nach N Ereignissen fällig und bleibt fällig, bis die Zeile feuert.</summary>
        Counter,

        /// <summary>Liest einen Zähler wie Ladung oder Tempo-Stapel.</summary>
        Resource,

        /// <summary>Hängt von der Oberwelt ab (Goldmine, Boss) und gilt für den ganzen Kampf.</summary>
        Context,
    }

    public static class RuneTagExtensions
    {
        public static string DisplayName(this RuneTag tag)
        {
            switch (tag)
            {
                case RuneTag.Blade: return CatalogTexts.RuneTagBlade;
                case RuneTag.Shield: return CatalogTexts.RuneTagShield;
                case RuneTag.Spark: return CatalogTexts.RuneTagSpark;
                case RuneTag.Ember: return CatalogTexts.RuneTagEmber;
                case RuneTag.Phantom: return CatalogTexts.RuneTagPhantom;
                default: return tag.ToString();
            }
        }
    }

    /// <summary>
    /// Eine Logik-Rune als reine Daten: eine Bedingung ("Wann") für eine Zeile der Logik-Tafel.
    /// Was dann passiert, kommt aus der Ausrüstung. Die Bedingung selbst baut die
    /// ConditionRegistry der Arena aus Id und Parameter der aktuellen Stufe.
    /// </summary>
    public sealed class RuneDefinition
    {
        private readonly int[] _levels;

        public string Id { get; }

        /// <summary>Name, darf "{0}" für den Parameter enthalten (z. B. "HP unter {0} %").</summary>
        public string NameTemplate { get; }
        public string DescriptionTemplate { get; }
        public RuneTag Tag { get; }
        public ConditionKind Kind { get; }

        /// <summary>Relative Häufigkeit in Angeboten.</summary>
        public int Weight { get; }

        /// <summary>Kommt nie in Angeboten vor, nur über Set-Boni o. Ä.</summary>
        public bool IsExclusive { get; }

        /// <summary>Set, das diese exklusive Rune freischaltet (ab 2 Teilen), oder null.</summary>
        public string UnlockSetId { get; }

        /// <summary>Parameter pro Stufe (Prozent, Sekunden, Anzahl). Stufe 0 ist der Startwert, höhere Stufen sind Verstärkungen.</summary>
        public IReadOnlyList<int> Levels => _levels;
        public int MaxLevel => Math.Max(0, _levels.Length - 1);

        /// <summary>
        /// Grundschwierigkeit 0–3 (0 = immer/sehr häufig, 1 = häufig, 2 = selten, 3 = sehr selten). Bestimmt den
        /// Schwierigkeits-Bonus auf den Skill der Zeile; Erleichterungen ändern sie nicht.
        /// </summary>
        public int Difficulty { get; }

        /// <summary>Schwierigkeit der umgekehrten Bedingung (Modul «Umkehren»), eigener Datenwert.</summary>
        public int InvertedDifficulty { get; }

        /// <summary>
        /// Zustands-Runen: solange der Zustand gilt, löst das Relais alle so viele Sekunden erneut aus (0 = nur bei der
        /// steigenden Flanke). Jedes Auslösen lädt zu grosse Komponenten um die Feld-Grenze des Relais auf.
        /// </summary>
        public int PulseSeconds { get; }

        /// <summary>Zustands-Runen ohne eigenen Takt lösen alle so viele Sekunden erneut aus, solange der Zustand gilt.</summary>
        public const int DefaultStatePulseSeconds = 2;

        /// <summary>Tatsächlicher Takt: eigener Wert, sonst bei Zustands-Runen <see cref="DefaultStatePulseSeconds"/>, sonst 0.</summary>
        public int EffectivePulseSeconds => PulseSeconds > 0 ? PulseSeconds : Kind == ConditionKind.State ? DefaultStatePulseSeconds : 0;

        /// <summary>Schwierigkeit mit oder ohne «Umkehren».</summary>
        public int DifficultyFor(bool inverted) => inverted ? InvertedDifficulty : Difficulty;

        public RuneDefinition(string id, string name, RuneTag tag, ConditionKind kind, string description,
            int[] levels = null, int weight = 10, bool exclusive = false, string unlockSetId = null, int difficulty = 0,
            int invertedDifficulty = 0, int pulseSeconds = 0)
        {
            PulseSeconds = Math.Max(0, pulseSeconds);
            Difficulty = Math.Max(0, Math.Min(3, difficulty));
            InvertedDifficulty = Math.Max(0, Math.Min(3, invertedDifficulty));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (weight < 0) throw new ArgumentOutOfRangeException(nameof(weight));
            Id = id;
            NameTemplate = name ?? id;
            DescriptionTemplate = description ?? string.Empty;
            Tag = tag;
            Kind = kind;
            _levels = levels != null && levels.Length > 0 ? (int[])levels.Clone() : new[] { 0 };
            Weight = weight;
            IsExclusive = exclusive;
            UnlockSetId = unlockSetId;
        }

        public int ParameterAt(int level) => _levels[Math.Max(0, Math.Min(level, MaxLevel))];

        public string NameAt(int level) => string.Format(NameTemplate, ParameterAt(level));
        public string DescriptionAt(int level) => string.Format(DescriptionTemplate, ParameterAt(level));

        /// <summary>Stufe als kurzer Text, z. B. "Stufe 1/2" oder "Stufe 2/2 max". Leer, wenn die Rune keine Stufen hat.</summary>
        public string LevelText(int level)
        {
            if (MaxLevel == 0) return string.Empty;
            int clamped = Math.Max(0, Math.Min(level, MaxLevel));
            return clamped >= MaxLevel ? CatalogTexts.RuneLevelMax(clamped, MaxLevel) : CatalogTexts.RuneLevel(clamped, MaxLevel);
        }

        /// <summary>Name auf Stufe 0.</summary>
        public string Name => NameAt(0);
        public string Description => DescriptionAt(0);

        public override string ToString() => $"{Name} [{Tag.DisplayName()}]";
    }
}
