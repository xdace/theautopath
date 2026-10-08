using System;
using System.Collections.Generic;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Run
{
    /// <summary>
    /// Start-Ausrüstung, die der Spieler vor dem Run wählt. Gibt dem Build eine erste Richtung:
    /// Werte, Start-Rune, Startausrüstung und 2–3 Start-Skills in der Sammlung (der erste sitzt an der Start-Rune).
    /// Jedes Kit hat mindestens einen Schadens-Skill an der Start-Rune (A-12: Skills sind der Hauptschaden).
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

        /// <summary>Skills, mit denen der Run beginnt. Der erste sitzt an der Start-Rune, weitere liegen frei in der Sammlung.</summary>
        public IReadOnlyList<string> StartSkillIds { get; }

        /// <summary>Skill, den die Start-Rune zu Beginn auslöst.</summary>
        public string StartSkillId => StartSkillIds.Count > 0 ? StartSkillIds[0] : null;

        public KnightKit(string id, string name, RuneTag tag, string startRuneId, int maxHp, int gold, string description,
            IEnumerable<string> startItemIds = null, params string[] startSkillIds)
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
            StartSkillIds = new List<string>(startSkillIds ?? Array.Empty<string>());
        }

        public PlayerStats CreateStats() => new PlayerStats(MaxHp, Gold);

        public static IReadOnlyList<KnightKit> Defaults { get; } = new[]
        {
            new KnightKit("blade", "Blade Knight", RuneTag.Blade, "on_hit", 32, 5,
                "Plays for pressure: its rows fire when attacks hit.",
                new[] { "short_blade" }, "armor_break", "shock_stab"),
            new KnightKit("shield", "Shield Knight", RuneTag.Shield, "when_hit", 36, 3,
                "Takes a beating and strikes back when hit.",
                new[] { "short_sword", "round_shield" }, "drill", "shield_bash", "shield_wall"),
            new KnightKit("spark", "Spark Knight", RuneTag.Spark, "every_3rd", 30, 8,
                "Counts attacks and triggers in rhythm.",
                new[] { "spark_staff" }, "ignite", "coolant"),
        };
    }
}
