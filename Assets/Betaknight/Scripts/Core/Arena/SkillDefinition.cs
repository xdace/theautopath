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

        /// <summary>Evolutionsform eines anderen Skills: wird nie angeboten, entsteht nur durch Evolution.</summary>
        public bool IsEvolution { get; }

        /// <summary>Zählt für Zähler wie "Jeder 3. Angriff". Basisangriff immer, sonst nach Definition.</summary>
        public bool CountsAsAttack { get; }

        /// <summary>Darf von Wiederholungs-Effekten (Echo) wiederholt werden.</summary>
        public bool CanBeRepeated { get; }

        public bool IsCharge => !IsBasicAttack && CastTicks() >= ChargeThreshold;

        /// <summary>Skill-Arten (Angriff, Feuer, Schock ...): Ziel passiver Effekte der Ausrüstung und Grundlage der Angebote.</summary>
        public SkillKind Kinds { get; }

        public SkillDefinition(string id, string name, int windupTicks, int recoveryTicks, int cooldownTicks,
            IEnumerable<ISkillEffect> effects, string description = null, bool countsAsAttack = false,
            bool canBeRepeated = true, bool isBasicAttack = false, SkillKind kinds = SkillKind.None, bool isEvolution = false)
        {
            Kinds = kinds;
            IsEvolution = isEvolution;
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id missing.", nameof(id));
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

        /// <summary>Der Basisangriff der Gegner: 100 % Waffenschaden, Tempo aus den Kampfwerten.</summary>
        public static SkillDefinition BasicAttack { get; } = CreateBasicAttack(BasisPoints.Full, 0);

        /// <summary>
        /// Ein Basisangriff mit <paramref name="damageBp"/> Waffenschaden. Trifft er, verkürzt er alle laufenden
        /// Skill-Cooldowns um <paramref name="cooldownCutTicks"/> (der Ritter, siehe <see cref="SkillBudgetConfig"/>).
        /// </summary>
        public static SkillDefinition CreateBasicAttack(int damageBp, int cooldownCutTicks)
        {
            string text = ArenaTexts.BasicAttackDescription(damageBp == BasisPoints.Full ? null : SkillInfo.Percent(damageBp));
            if (cooldownCutTicks > 0) text += ArenaTexts.BasicAttackCooldownCut(SkillInfo.Seconds(cooldownCutTicks));
            return new SkillDefinition(BasicAttackId, ArenaTexts.BasicAttack, 0, 0, 0, new ISkillEffect[] { new DamageEffect(damageBp) }, text, isBasicAttack: true)
            {
                CooldownCutOnHitTicks = Math.Max(0, cooldownCutTicks),
            };
        }

        /// <summary>Nur Basisangriff: ein Treffer verkürzt alle laufenden Skill-Cooldowns um so viele Ticks.</summary>
        public int CooldownCutOnHitTicks { get; private set; }

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

        private SkillDefinition Copy(List<ISkillEffect> effects, int cooldown, int level, int power, int cooldownBonus, int castBonus)
        {
            SkillDefinition copy = Clone(effects, cooldown);
            copy.Level = level;
            copy.PowerBonusPercent = power;
            copy.CooldownBonusTicks = cooldownBonus;
            copy.CastBonusPercent = castBonus;
            return copy;
        }

        /// <summary>Kopie mit allen Zusatzwerten (Stufe, Boni, Module); Wirkungen und Cooldown optional neu.</summary>
        private SkillDefinition Clone(List<ISkillEffect> effects = null, int? cooldown = null) =>
            new SkillDefinition(Id, Name, WindupTicks, RecoveryTicks, cooldown ?? CooldownTicks, effects ?? new List<ISkillEffect>(Effects),
                Description, CountsAsAttack, CanBeRepeated, IsBasicAttack, Kinds, IsEvolution)
            {
                Level = Level,
                PowerBonusPercent = PowerBonusPercent,
                CooldownBonusTicks = CooldownBonusTicks,
                CastBonusPercent = CastBonusPercent,
                ExtraCasts = ExtraCasts,
                ExtraTargets = ExtraTargets,
                HpCostBp = HpCostBp,
                _modules = new List<string>(_modules),
                DifficultyTier = DifficultyTier,
                Difficulty = Difficulty,
                _cooldownBeforeDifficulty = _cooldownBeforeDifficulty,
                CooldownCutOnHitTicks = CooldownCutOnHitTicks,
            };

        // ------------------------------------------------------------------ Module (A-07)

        private List<string> _modules = new List<string>();

        /// <summary>Namen der eingesetzten Skill-Module, z. B. «Fläche», für Anzeige und Protokoll.</summary>
        public IReadOnlyList<string> Modules => _modules;

        /// <summary>«Mehrfach»: so oft wird die Wirkung danach wiederholt, jedes Mal mit voller Cast-Zeit.</summary>
        public int ExtraCasts { get; private set; }

        /// <summary>«Kette»: so viele weitere Gegner treffen die zielgerichteten Wirkungen.</summary>
        public int ExtraTargets { get; private set; }

        /// <summary>«Kostet HP statt Cooldown»: Selbstschaden in Basispunkten der Max-HP bei jedem Start, kein Cooldown.</summary>
        public int HpCostBp { get; private set; }

        /// <summary>Kopie mit einem Skill-Modul. <paramref name="change"/> setzt die neuen Werte auf der Kopie.</summary>
        public SkillDefinition WithModule(string moduleName, Func<ISkillEffect, ISkillEffect> mapEffect = null, int extraCasts = 0,
            int extraTargets = 0, int hpCostBp = 0, int? cooldownTicks = null, int castPercent = 0)
        {
            if (IsBasicAttack) return this;
            List<ISkillEffect> effects = null;
            if (mapEffect != null)
            {
                effects = new List<ISkillEffect>();
                foreach (ISkillEffect e in Effects) effects.Add(mapEffect(e) ?? e);
            }
            SkillDefinition copy = Clone(effects, hpCostBp > 0 ? 0 : cooldownTicks);
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

        /// <summary>Cooldown vor dem Schwierigkeits-Bonus (für Anzeige und Auswertung).</summary>
        public int CooldownBeforeDifficulty => DifficultyTier > 0 ? _cooldownBeforeDifficulty : CooldownTicks;

        private int _cooldownBeforeDifficulty;

        /// <summary>
        /// Fassung mit Schwierigkeits-Bonus: Cooldown und Cast-Zeit in Prozent kürzer (Untergrenze bleibt), Wirkung
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

            int cooldown = (int)((long)CooldownTicks * (100 - bonus.CooldownReductionPercent) / 100);
            SkillDefinition copy = Clone(effects, cooldown);
            copy.CastBonusPercent -= bonus.CastReductionPercent;
            copy.DifficultyTier = tier;
            copy.Difficulty = bonus;
            copy._cooldownBeforeDifficulty = CooldownTicks;
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
