using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Alles, was eine Wirkung über ihre Aktion wissen muss.</summary>
    public readonly struct SkillContext
    {
        public readonly Battle Battle;
        public readonly Combatant User;

        /// <summary>Hauptziel (meist ein Gegner). Kann null sein, wenn niemand mehr steht.</summary>
        public readonly Combatant Target;
        public readonly SkillDefinition Skill;
        public readonly int RowIndex;

        public SkillContext(Battle battle, Combatant user, Combatant target, SkillDefinition skill, int rowIndex)
        {
            Battle = battle;
            User = user;
            Target = target;
            Skill = skill;
            RowIndex = rowIndex;
        }
    }

    /// <summary>
    /// Eine Wirkung eines Skills (Schaden, Heilung, Betäubung, ...). Neue Wirkungen sind neue Klassen,
    /// der Simulator muss dafür nicht angepasst werden.
    /// </summary>
    public interface ISkillEffect
    {
        void Apply(in SkillContext context);

        /// <summary>
        /// Meldet die Kennzahlen dieser Wirkung (Schaden, Dauer, Chance ...) für die Anzeige.
        /// Muss dieselben Formeln wie <see cref="Apply"/> nutzen, damit Anzeige und Kampf übereinstimmen.
        /// </summary>
        void Describe(SkillInfoBuilder info);
    }

    /// <summary>Eine Aktion, das "Was" einer Zeile der Logik-Tafel. Reine Daten plus Wirkungen.</summary>
    public sealed class SkillDefinition
    {
        public const string BasicAttackId = "basic_attack";

        /// <summary>Ab so vielen Ticks Ausholen gilt eine Aktion als sichtbare Aufladung.</summary>
        public const int ChargeThreshold = Ticks.PerSecond;

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>Ausholen bis zur Wirkung. Beim Basisangriff ergibt es sich aus dem Angriffstempo.</summary>
        public int WindupTicks { get; }
        public int RecoveryTicks { get; }
        public int CooldownTicks { get; }
        public IReadOnlyList<ISkillEffect> Effects { get; }

        public bool IsBasicAttack { get; }

        /// <summary>Zählt für Zähler wie "Jeder 3. Angriff". Basisangriff immer, sonst nach Definition.</summary>
        public bool CountsAsAttack { get; }

        /// <summary>Darf von Wiederholungs-Effekten (Echo) wiederholt werden.</summary>
        public bool CanBeRepeated { get; }

        public bool IsCharge => !IsBasicAttack && WindupTicks >= ChargeThreshold;

        public SkillDefinition(string id, string name, int windupTicks, int recoveryTicks, int cooldownTicks,
            IEnumerable<ISkillEffect> effects, string description = null, bool countsAsAttack = false,
            bool canBeRepeated = true, bool isBasicAttack = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            if (windupTicks < 0 || recoveryTicks < 0 || cooldownTicks < 0) throw new ArgumentOutOfRangeException(nameof(windupTicks));

            Id = id;
            Name = name ?? id;
            Description = description ?? string.Empty;
            WindupTicks = windupTicks;
            RecoveryTicks = recoveryTicks;
            CooldownTicks = cooldownTicks;
            Effects = new List<ISkillEffect>(effects ?? Array.Empty<ISkillEffect>());
            IsBasicAttack = isBasicAttack;
            CountsAsAttack = countsAsAttack || isBasicAttack;
            CanBeRepeated = canBeRepeated;
        }

        /// <summary>Der Basisangriff: 100 % Waffenschaden, Tempo aus den Kampfwerten.</summary>
        public static SkillDefinition BasicAttack { get; } = new SkillDefinition(
            BasicAttackId, "Basisangriff", 0, 0, 0, new ISkillEffect[] { new DamageEffect(BasisPoints.Full) },
            "Waffenschaden.", isBasicAttack: true);

        public override string ToString() => Name;
    }
}
