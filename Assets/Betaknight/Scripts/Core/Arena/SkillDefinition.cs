using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

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

    /// <summary>
    /// Eine Aktion, das "Was" einer Komponente der Platine. Reine Daten plus Wirkungen. Es gibt keine Cooldowns (A-19):
    /// die Form (<see cref="Shape"/>) bestimmt, wie stark ein Skill sein darf und welche Relais ihn versorgen können.
    /// </summary>
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

        /// <summary>Form auf der Platine (Daten je Skill). Mehr Zellen = mehr Wirkung, aber nur schwere Relais versorgen sie.</summary>
        public Shape Shape { get; }
        public IReadOnlyList<ISkillEffect> Effects { get; }

        public bool IsBasicAttack { get; }

        /// <summary>Evolutionsform eines anderen Skills: wird nie angeboten, entsteht nur durch Evolution.</summary>
        public bool IsEvolution { get; }

        /// <summary>Zählt für Zähler wie "Jeder 3. Angriff". Basisangriff immer, sonst nach Definition.</summary>
        public bool CountsAsAttack { get; }

        /// <summary>Darf von Wiederholungs-Effekten (Echo) wiederholt werden.</summary>
        public bool CanBeRepeated { get; }

        public bool IsCharge => !IsBasicAttack && CastTicks() >= ChargeThreshold;

        /// <summary>Skill-Arten (Angriff, Feuer, Schock ...): Ziel passiver Effekte der Ausrüstung und Grundlage der Angebote.</summary>
        public SkillKind Kinds { get; }

        public SkillDefinition(string id, string name, int windupTicks, int recoveryTicks,
            IEnumerable<ISkillEffect> effects, string description = null, bool countsAsAttack = false,
            bool canBeRepeated = true, bool isBasicAttack = false, SkillKind kinds = SkillKind.None, bool isEvolution = false,
            Shape? shape = null)
        {
            Shape = shape ?? Shape.One;
            Kinds = kinds;
            IsEvolution = isEvolution;
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id missing.", nameof(id));
            if (windupTicks < 0 || recoveryTicks < 0) throw new ArgumentOutOfRangeException(nameof(windupTicks));

            Id = id;
            Name = name ?? id;
            Description = description ?? string.Empty;
            WindupTicks = windupTicks;
            RecoveryTicks = recoveryTicks;
            Effects = new List<ISkillEffect>(effects ?? Array.Empty<ISkillEffect>());
            IsBasicAttack = isBasicAttack;
            CountsAsAttack = countsAsAttack || isBasicAttack;
            CanBeRepeated = canBeRepeated;
        }

        /// <summary>Der Basisangriff der Gegner: 100 % Waffenschaden, Tempo aus den Kampfwerten.</summary>
        public static SkillDefinition BasicAttack { get; } = CreateBasicAttack(BasisPoints.Full);

        /// <summary>Ein Basisangriff mit <paramref name="damageBp"/> Waffenschaden (der Ritter, siehe <see cref="SkillBudgetConfig"/>).</summary>
        public static SkillDefinition CreateBasicAttack(int damageBp)
        {
            string text = ArenaTexts.BasicAttackDescription(damageBp == BasisPoints.Full ? null : SkillInfo.Percent(damageBp));
            return new SkillDefinition(BasicAttackId, ArenaTexts.BasicAttack, 0, 0, new ISkillEffect[] { new DamageEffect(damageBp) }, text, isBasicAttack: true);
        }

        /// <summary>Stufe des Skill-Exemplars (0 = Grundform). Höhere Stufen haben stärkere Wirkungen.</summary>
        public int Level { get; private set; }

        /// <summary>Wirkungsbonus in Prozent aus passiven Effekten der Ausrüstung (nur Anzeige, schon eingerechnet).</summary>
        public int PowerBonusPercent { get; private set; }

        /// <summary>
        /// Derselbe Skill auf einer Stufe: stufbare Wirkungen werden stärker, Zeiten bleiben gleich.
        /// Der Basisangriff hat keine Stufen.
        /// </summary>
        public SkillDefinition AtLevel(int level, SkillLevelRules rules)
        {
            if (level <= 0 || rules == null || IsBasicAttack) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect e in Effects) effects.Add(e is ILevelableEffect l ? l.AtLevel(level, rules) : e);
            return Copy(effects, level, PowerBonusPercent, CastBonusPercent);
        }

        /// <summary>
        /// Derselbe Skill mit passiven Boni: Wirkung (Schaden, Brennen, Heilung) +<paramref name="powerPercent"/> %,
        /// Cast-Zeit um <paramref name="castPercent"/> % verändert. Ohne Boni derselbe Skill.
        /// </summary>
        public SkillDefinition WithBonus(int powerPercent, int castPercent = 0)
        {
            if ((powerPercent == 0 && castPercent == 0) || IsBasicAttack) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect e in Effects) effects.Add(powerPercent != 0 && e is IBoostableEffect b ? b.Boosted(powerPercent) : e);
            return Copy(effects, Level, PowerBonusPercent + powerPercent, CastBonusPercent + castPercent);
        }

        private SkillDefinition Copy(List<ISkillEffect> effects, int level, int power, int castBonus)
        {
            SkillDefinition copy = Clone(effects);
            copy.Level = level;
            copy.PowerBonusPercent = power;
            copy.CastBonusPercent = castBonus;
            return copy;
        }

        /// <summary>Kopie mit allen Zusatzwerten (Stufe, Boni, Module); Wirkungen optional neu.</summary>
        private SkillDefinition Clone(List<ISkillEffect> effects = null) =>
            new SkillDefinition(Id, Name, WindupTicks, RecoveryTicks, effects ?? new List<ISkillEffect>(Effects),
                Description, CountsAsAttack, CanBeRepeated, IsBasicAttack, Kinds, IsEvolution, Shape)
            {
                Level = Level,
                PowerBonusPercent = PowerBonusPercent,
                CastBonusPercent = CastBonusPercent,
                ExtraCasts = ExtraCasts,
                ExtraTargets = ExtraTargets,
                HpCostBp = HpCostBp,
                _modules = new List<string>(_modules),
                DifficultyTier = DifficultyTier,
                Difficulty = Difficulty,
            };

        // ------------------------------------------------------------------ Module (A-07)

        private List<string> _modules = new List<string>();

        /// <summary>Namen der eingesetzten Skill-Module, z. B. «Fläche», für Anzeige und Protokoll.</summary>
        public IReadOnlyList<string> Modules => _modules;

        /// <summary>«Mehrfach»: so oft wird die Wirkung danach wiederholt, jedes Mal mit voller Cast-Zeit.</summary>
        public int ExtraCasts { get; private set; }

        /// <summary>«Kette»: so viele weitere Gegner treffen die zielgerichteten Wirkungen.</summary>
        public int ExtraTargets { get; private set; }

        /// <summary>«Blood Toll»: Selbstschaden in Basispunkten der Max-HP bei jedem Start (dafür mehr Wirkung).</summary>
        public int HpCostBp { get; private set; }

        /// <summary>Kopie mit einem Skill-Modul. <paramref name="change"/> setzt die neuen Werte auf der Kopie.</summary>
        public SkillDefinition WithModule(string moduleName, Func<ISkillEffect, ISkillEffect> mapEffect = null, int extraCasts = 0,
            int extraTargets = 0, int hpCostBp = 0, int castPercent = 0)
        {
            if (IsBasicAttack) return this;
            List<ISkillEffect> effects = null;
            if (mapEffect != null)
            {
                effects = new List<ISkillEffect>();
                foreach (ISkillEffect e in Effects) effects.Add(mapEffect(e) ?? e);
            }
            SkillDefinition copy = Clone(effects);
            copy.ExtraCasts += Math.Max(0, extraCasts);
            copy.ExtraTargets += Math.Max(0, extraTargets);
            copy.HpCostBp = Math.Max(copy.HpCostBp, hpCostBp);
            copy.CastBonusPercent += castPercent;
            if (!string.IsNullOrEmpty(moduleName)) copy._modules.Add(moduleName);
            return copy;
        }

        // ------------------------------------------------------------------ Schwierigkeits-Bonus (A-11)

        /// <summary>Stufe des Schwierigkeits-Bonus, mit dem diese Fassung läuft (0 = keiner).</summary>
        public int DifficultyTier { get; private set; }

        /// <summary>Der Bonus dieser Fassung (leer bei Stufe 0).</summary>
        public DifficultyBonus Difficulty { get; private set; }

        /// <summary>
        /// Fassung mit Schwierigkeits-Bonus: Cast-Zeit in Prozent kürzer (Untergrenze bleibt), Wirkung
        /// (Schaden, Heilung, Schild) stärker, Status-Wirkungen länger. Zählt nicht zu den Ausrüstungs-Boni.
        /// </summary>
        public SkillDefinition WithDifficultyBonus(int tier, DifficultyBonus bonus)
        {
            if (IsBasicAttack || tier <= 0 || bonus.IsNone) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect original in Effects)
            {
                ISkillEffect e = original;
                if (bonus.PowerPercent != 0)
                {
                    if (e is IBoostableEffect b) e = b.Boosted(bonus.PowerPercent);
                    else if (e is StatModifierEffect m && !m.OnTarget) e = m.Scaled(bonus.PowerPercent);
                }
                if (bonus.ExtraStatusTicks > 0 && e is IDurationEffect d) e = d.Extended(bonus.ExtraStatusTicks);
                effects.Add(e);
            }

            SkillDefinition copy = Clone(effects);
            copy.CastBonusPercent -= bonus.CastReductionPercent;
            copy.DifficultyTier = tier;
            copy.Difficulty = bonus;
            return copy;
        }

        /// <summary>Kopie mit veränderten Wirkungen (z. B. Wachstum), alle Zusatzwerte bleiben.</summary>
        public SkillDefinition MapEffects(Func<ISkillEffect, ISkillEffect> map)
        {
            if (map == null || IsBasicAttack) return this;
            var effects = new List<ISkillEffect>();
            foreach (ISkillEffect e in Effects) effects.Add(map(e) ?? e);
            return Clone(effects);
        }

        public override string ToString() => Name;
    }
}
