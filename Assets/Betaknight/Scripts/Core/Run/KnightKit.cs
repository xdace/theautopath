using System;
using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Run
{
    /// <summary>
    /// Start-Ausrüstung, die der Spieler vor dem Run wählt. Gibt dem Build eine erste Richtung:
    /// Werte, Start-Rune und Startausrüstung (die Waffe liefert den ersten Skill).
    /// </summary>
    public sealed class KnightKit
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public RuneTag Tag { get; }
        public string StartRuneId { get; }
        public int MaxHp { get; }
        public int Gold { get; }

        /// <summary>Ids aus dem Ausrüstungs-Katalog, die der Ritter zu Beginn trägt.</summary>
        public IReadOnlyList<string> StartItemIds { get; }

        /// <summary>Skill, den die Start-Rune zu Beginn auslöst (aus der Startwaffe).</summary>
        public string StartSkillId { get; }

        public KnightKit(string id, string name, RuneTag tag, string startRuneId, int maxHp, int gold, string description,
            IEnumerable<string> startItemIds = null, string startSkillId = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            Tag = tag;
            StartRuneId = startRuneId;
            MaxHp = maxHp;
            Gold = gold;
            Description = description ?? string.Empty;
            StartItemIds = new List<string>(startItemIds ?? Array.Empty<string>());
            StartSkillId = startSkillId;
        }

        public PlayerStats CreateStats() => new PlayerStats(MaxHp, Gold);

        public static IReadOnlyList<KnightKit> Defaults { get; } = new[]
        {
            new KnightKit("blade", "Klingenritter", RuneTag.Blade, "on_hit", 28, 5,
                "Setzt auf Druck: seine Zeilen feuern, wenn Angriffe treffen.",
                new[] { "short_blade" }, "armor_break"),
            new KnightKit("shield", "Schildritter", RuneTag.Shield, "when_hit", 36, 3,
                "Hält viel aus und reagiert, wenn er getroffen wird.",
                new[] { "short_sword", "round_shield" }, "shield_bash"),
            new KnightKit("spark", "Funkenritter", RuneTag.Spark, "every_3rd", 26, 8,
                "Zählt Angriffe und löst im Takt aus.",
                new[] { "spark_staff" }, "ignite"),
        };
    }
}
