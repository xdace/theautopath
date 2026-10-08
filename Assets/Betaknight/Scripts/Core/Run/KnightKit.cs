using System;
using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Run
{
    /// <summary>Start-Ausrüstung, die der Spieler vor dem Run wählt. Gibt dem Build eine erste Richtung.</summary>
    public sealed class KnightKit
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RuneTag Tag { get; }
        public string StartRuneId { get; }
        public int MaxHp { get; }
        public int Gold { get; }

        public KnightKit(string id, string name, RuneTag tag, string startRuneId, int maxHp, int gold, string description)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            Tag = tag;
            StartRuneId = startRuneId;
            MaxHp = maxHp;
            Gold = gold;
            Description = description ?? string.Empty;
        }

        public PlayerStats CreateStats() => new PlayerStats(MaxHp, Gold);

        public static IReadOnlyList<KnightKit> Defaults { get; } = new[]
        {
            new KnightKit("blade", "Klingenritter", RuneTag.Blade, "whetstone", 28, 5,
                "Wird im Kampf mit jedem Treffer stärker."),
            new KnightKit("shield", "Schildritter", RuneTag.Shield, "bulwark", 36, 3,
                "Hält viel aus und kontert aus der Deckung."),
            new KnightKit("spark", "Funkenritter", RuneTag.Spark, "spark_counter", 26, 8,
                "Zählt Angriffe und entlädt Blitze."),
        };
    }
}
