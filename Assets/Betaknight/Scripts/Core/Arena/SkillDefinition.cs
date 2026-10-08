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

        /// <summary>Ab so vielen Ticks Cast-Zeit gilt eine Aktion als sichtbare Aufladung.</summary>
        public const int ChargeThreshold = Ticks.PerSecond;

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        /// <summary>Grund-Cast-Zeit bis zur Wirkung. Beim Basisangriff ergibt sie sich aus dem Angriffstempo.</summary>
        public int WindupTicks { get; }

        /// <summary>Änderung der Cast-Zeit in Prozent aus Ausrüstung und Tags (−20 = 20 % schneller), schon summiert.</summary>
        public int CastBonusPercent { get; private set; }

        /// <summary>Aktuelle Cast-Zeit mit allen Änderungen, nie unter <paramref name="minTicks"/>.</summary>
        public int CastTicks(int minTicks = CastTime.DefaultMinTicks) => CastTime.Apply(WindupTicks, CastBonusPercent, minTicks);
        public int RecoveryTicks { get; }
        public int CooldownTicks { get; }
        public IReadOnlyList<ISkillEffect> Effects { get; }

        public bool IsBasicAttack { get; }

        /// <summary>Zählt für Zähler wie "Jeder 3. Angriff". Basisangriff immer, sonst nach Definition.</summary>
        public bool CountsAsAttack { get; }

        /// <summary>Darf von Wiederholungs-Effekten (Echo) wiederholt werden.</summary>
        public bool CanBeRepeated { get; }

        public bool IsCharge => !IsBasicAttack && CastTicks() >= ChargeThreshold;

        /// <summary>Skill-Arten (Angriff, Feuer, Schock ...): Ziel passiver Effekte der Ausrüstung und Grundlage der Angebote.</summary>
        public SkillKind Kinds { get; }

        public SkillDefinition(string id, string name, int windupTicks, int recoveryTicks, int cooldownTicks,
            IEnumerable<ISkillEffect> effects, string description = null, bool countsAsAttack = false,
            bool canBeRepeated = true, bool isBasicAttack = false, SkillKind kinds = SkillKind.None)
        {
            Kinds = kinds;
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

        /// <summary>Stufe des Skill-Exemplars (0 = Grundform). Höhere Stufen haben stärkere Wirkungen.</summary>
        public int Level { get; private set; }

        /// <summary>Wirkungsbonus in Prozent aus passiven Effekten der Ausrüstung (nur Anzeige, schon eingerechnet).</summary>
        public int PowerBonusPercent { get; private set; }

        /// <summary>Cooldown-Änderung in Ticks aus passiven Effekten (nur Anzeige, schon eingerechnet).</summary>
        public int CooldownBonusTicks { get; private set; }

        /// <summary>
        /// Derselbe Skill auf einer Stufe: stufbare Wirkungen werden stärker, Zeiten bleiben gleich.
        /// Der Basisangriff hat keine Stufen.
        /// </summary>
        public SkillDefinition AtLevel(int level, SkillLevelRules rules)
        {
            if (level <= 0 || rules == null || IsBasicAttack) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect e in Effects) effects.Add(e is ILevelableEffect l ? l.AtLevel(level, rules) : e);
            return Copy(effects, CooldownTicks, level, PowerBonusPercent, CooldownBonusTicks, CastBonusPercent);
        }

        /// <summary>
        /// Derselbe Skill mit passiven Boni der Ausrüstung: Wirkung (Schaden, Brennen, Heilung) +<paramref name="powerPercent"/> %,
        /// Cooldown um <paramref name="cooldownTicks"/> verändert (nie unter 0), Cast-Zeit um <paramref name="castPercent"/> %.
        /// Ohne Boni derselbe Skill.
        /// </summary>
        public SkillDefinition WithBonus(int powerPercent, int cooldownTicks, int castPercent = 0)
        {
            if ((powerPercent == 0 && cooldownTicks == 0 && castPercent == 0) || IsBasicAttack) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect e in Effects) effects.Add(powerPercent != 0 && e is IBoostableEffect b ? b.Boosted(powerPercent) : e);
            return Copy(effects, Math.Max(0, CooldownTicks + cooldownTicks), Level, PowerBonusPercent + powerPercent,
                CooldownBonusTicks + cooldownTicks, CastBonusPercent + castPercent);
        }

        private SkillDefinition Copy(List<ISkillEffect> effects, int cooldown, int level, int power, int cooldownBonus, int castBonus) =>
            new SkillDefinition(Id, Name, WindupTicks, RecoveryTicks, cooldown, effects, Description, CountsAsAttack,
                CanBeRepeated, IsBasicAttack, Kinds)
            {
                Level = level,
                PowerBonusPercent = power,
                CooldownBonusTicks = cooldownBonus,
                CastBonusPercent = castBonus,
            };

        public override string ToString() => Name;
    }
}
