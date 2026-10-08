using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Eine Zeile der Logik-Tafel: Bedingung + Skill. Ohne Skill ist die Zeile verwaist und wird übersprungen.</summary>
    public sealed class LogicRow
    {
        public ICondition Condition { get; }
        public SkillDefinition Skill { get; }

        /// <summary>Anzeigename der Bedingung, z. B. der Runenname.</summary>
        public string Label { get; }

        public bool IsOrphaned => Skill == null;

        /// <summary>
        /// Grundschwierigkeit des Bausteins (0–3, bei «Umkehren» die eigene Stufe der umgekehrten Bedingung). Bestimmt den
        /// Schwierigkeits-Bonus auf den Skill; Erleichterungen ändern sie nicht.
        /// </summary>
        public int Difficulty { get; }

        private readonly SkillDefinition[] _byTier = new SkillDefinition[DifficultyBonusConfig.MaxTier + 1];

        public LogicRow(ICondition condition, SkillDefinition skill, string label = null, int difficulty = 0)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Skill = skill;
            Label = label ?? condition.GetType().Name;
            Difficulty = DifficultyBonusConfig.Clamp(difficulty);
        }

        /// <summary>Der Skill der Zeile mit dem Bonus einer Stufe (zwischengespeichert, je Zeile eine Konfiguration).</summary>
        public SkillDefinition SkillAt(int tier, DifficultyBonusConfig config)
        {
            if (Skill == null) return null;
            tier = DifficultyBonusConfig.Clamp(tier);
            return _byTier[tier] ?? (_byTier[tier] = (config ?? DifficultyBonusConfig.Default).Apply(Skill, tier));
        }

        public override string ToString() => $"[{Label}] -> [{Skill?.Name ?? "—"}]";
    }

    /// <summary>
    /// Die Logik-Tafel: Zeilen von oben nach unten, die erste erfüllte Zeile mit bereitem Skill feuert.
    /// Darunter steht immer die feste Zeile [Immer] -> [Basisangriff]. Gegner haben eine feste Tafel.
    /// </summary>
    public sealed class LogicBoard
    {
        public IReadOnlyList<LogicRow> Rows { get; }
        public LogicRow Fallback { get; }

        /// <summary>Index, mit dem die Fallback-Zeile im Protokoll erscheint.</summary>
        public int FallbackIndex => Rows.Count;

        /// <summary>Auslöser zwischen Bausteinen und Skills der Zeilen (Kreise erlaubt).</summary>
        public LogicGraph Graph { get; }

        /// <summary>Schwierigkeits-Bonus je Stufe für diese Tafel.</summary>
        public DifficultyBonusConfig Bonus { get; }

        public LogicBoard(IEnumerable<LogicRow> rows, SkillDefinition fallbackSkill = null, LogicGraph graph = null,
            DifficultyBonusConfig bonus = null)
        {
            Rows = new List<LogicRow>(rows ?? Array.Empty<LogicRow>());
            Graph = graph ?? LogicGraph.Empty;
            Bonus = bonus ?? DifficultyBonusConfig.Default;
            Fallback = new LogicRow(AlwaysCondition.Instance, fallbackSkill ?? SkillDefinition.BasicAttack, ArenaTexts.AlwaysLabel);
        }

        public static LogicBoard FallbackOnly { get; } = new LogicBoard(null);

        /// <summary>Gibt es Bedingungen mit Gedächtnis oder Baustein-Auslöser? Nur dann beobachtet der Kampf jeden Tick.</summary>
        public bool NeedsObservation
        {
            get
            {
                if (_needsObservation.HasValue) return _needsObservation.Value;
                bool needed = false;
                for (int i = 0; i < Rows.Count && !needed; i++)
                    needed = Rows[i].Condition is IObservingCondition || Graph.HasEdgesFrom(GraphNode.Block(i));
                _needsObservation = needed;
                return needed;
            }
        }

        private bool? _needsObservation;

        /// <summary>Zeile nach Index inklusive Fallback.</summary>
        public LogicRow RowAt(int index) => index == FallbackIndex ? Fallback : Rows[index];
    }
}
