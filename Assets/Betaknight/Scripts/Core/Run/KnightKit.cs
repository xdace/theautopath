using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core.Run
{
    /// <summary>
    /// Start-Ausrüstung, die der Spieler vor dem Run wählt. Gibt dem Build eine erste Richtung:
    /// Werte, Start-Rune, Startausrüstung und 2–3 Start-Skills in der Sammlung. Auf der Platine liegt das Start-Relais oben
    /// links, der erste Skill als Komponente daneben und am Kern (A-19). Er passt immer in die Grenze des Start-Relais.
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

        /// <summary>Skills, mit denen der Run beginnt. Der erste liegt am Start-Relais, weitere frei in der Sammlung.</summary>
        public IReadOnlyList<string> StartSkillIds { get; }

        /// <summary>Skill, den das Start-Relais zu Beginn versorgt.</summary>
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

        /// <summary>Startaufbau der Platine: Relais auf (0, 0), der Skill daneben (flach, wenn es geht), berührt den Kern auf (1, 1).</summary>
        public static void LayOut(CircuitBoard board, RuneDefinition startRune, SkillInstance firstSkill)
        {
            RelayChip relay = board.AddRelay(startRune, new Cell(0, 0));
            if (relay == null || firstSkill == null || firstSkill.IsBasicAttack) return;
            Shape shape = board.ShapeOfSkill(firstSkill.SkillId);
            if (board.FreeSpotTouching(relay, shape, out Cell origin, out bool rotated)) board.Place(firstSkill, origin, rotated);
        }

        public static IReadOnlyList<KnightKit> Defaults { get; } = new[]
        {
            new KnightKit("blade", "Blade Knight", RuneTag.Blade, "on_hit", 32, 5,
                "Plays for pressure: its components fire when attacks hit.",
                new[] { "short_blade" }, "shock_stab", "armor_break"),
            new KnightKit("shield", "Shield Knight", RuneTag.Shield, "when_hit", 36, 3,
                "Takes a beating and strikes back when hit.",
                new[] { "short_sword", "round_shield" }, "shield_bash", "drill", "shield_wall"),
            new KnightKit("spark", "Spark Knight", RuneTag.Spark, "every_3rd", 30, 8,
                "Counts attacks and triggers in rhythm.",
                new[] { "spark_staff" }, "ignite", "coolant"),
        };
    }
}
