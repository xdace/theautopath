using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Growth
{
    /// <summary>Wofür ein Exemplar einen Wachstumspunkt bekommt.</summary>
    public enum GrowthTrigger
    {
        /// <summary>Ein Gegner fällt durch einen Treffer dieses Skills.</summary>
        Kill,

        /// <summary>Der Skill betäubt einen Gegner.</summary>
        Stun,

        /// <summary>Der Skill heilt.</summary>
        Heal,

        /// <summary>Gewonnener Kampf, in dem das Exemplar (Skill bzw. Zeile) mindestens einmal gefeuert hat.</summary>
        Win,
    }

    /// <summary>Was ein Wachstumspunkt bewirkt.</summary>
    public enum GrowthEffect
    {
        /// <summary>Nur Zähler (Stufen und Modul-Plätze), keine eigene Wirkung.</summary>
        None,

        /// <summary>+X fester Schaden pro Treffer.</summary>
        FlatDamage,

        /// <summary>+X Ticks Betäubungsdauer.</summary>
        StunTicks,

        /// <summary>+X % Wirkung (Schaden, Brennen, Heilung).</summary>
        PowerPercent,

        /// <summary>+X Prozentpunkte Schwelle bei Runen mit Prozent-Schwelle, höchstens bis <see cref="GrowthRule.Cap"/>.</summary>
        ThresholdPercent,
    }

    /// <summary>
    /// Eine Wachstums-Regel: wofür es Punkte gibt und was jeder Punkt bewirkt, z. B. «+1 Schaden pro Kill mit diesem Skill
    /// (max. +30)». Reine Daten; die Zähler gehören dem Exemplar.
    /// </summary>
    public sealed class GrowthRule
    {
        public GrowthTrigger Trigger { get; }
        public GrowthEffect Effect { get; }

        /// <summary>Wirkung pro Punkt (Schaden, Ticks, Prozent).</summary>
        public int PerPoint { get; }

        /// <summary>Obergrenze: bei Schwellen der Endwert in Prozent, sonst die gesamte Zusatzwirkung. 0 = ohne.</summary>
        public int Cap { get; }

        public string Text { get; }

        public GrowthRule(GrowthTrigger trigger, GrowthEffect effect, int perPoint, int cap, string text)
        {
            Trigger = trigger;
            Effect = effect;
            PerPoint = perPoint;
            Cap = Math.Max(0, cap);
            Text = text ?? string.Empty;
        }

        /// <summary>Zusatzwirkung bei <paramref name="growth"/> Punkten, mit Obergrenze (nicht für Schwellen).</summary>
        public int BonusAt(int growth)
        {
            long bonus = (long)Math.Max(0, growth) * PerPoint;
            if (Cap > 0 && Effect != GrowthEffect.ThresholdPercent) bonus = Math.Min(bonus, Cap);
            return (int)Math.Min(int.MaxValue, bonus);
        }
    }

    /// <summary>
    /// Ein Zähler für alles: Wachstum. Die Skill-Stufe (früher +1…+3 aus Duplikaten mit eigenem Schadensbonus) ist jetzt
    /// ein Meilenstein des Wachstums ohne eigene Werte; die Wirkung kommt allein aus der Wachstums-Regel. Ein Duplikat
    /// gibt <see cref="DuplicateGrowth"/> Punkte. Modul-Plätze sind ebenfalls Meilensteine.
    /// </summary>
    public static class GrowthStages
    {
        /// <summary>Wachstum für ein doppeltes Exemplar («Wachstum +5» statt eigener Stufe).</summary>
        public const int DuplicateGrowth = 5;

        /// <summary>Wachstum für Stufe 1, 2, 3.</summary>
        public static readonly int[] SkillStageThresholds = { 5, 15, 30 };

        /// <summary>Wachstum für den 2. und 3. Modul-Platz (Skill und Baustein).</summary>
        public static readonly int[] ModuleSlotThresholds = { 10, 25 };

        public static int MaxStage => SkillStageThresholds.Length;

        public static int StageFor(int growth)
        {
            int stage = 0;
            foreach (int t in SkillStageThresholds) if (growth >= t) stage++;
            return stage;
        }

        /// <summary>Wachstum, ab dem eine Stufe erreicht ist (Stufe 0 = 0).</summary>
        public static int GrowthForStage(int stage)
        {
            if (stage <= 0) return 0;
            return SkillStageThresholds[Math.Min(stage, SkillStageThresholds.Length) - 1];
        }

        public static int ModuleSlotsFor(int growth)
        {
            int slots = 1;
            foreach (int t in ModuleSlotThresholds) if (growth >= t) slots++;
            return slots;
        }

        /// <summary>Nächste Schwelle (Stufe oder Modul-Platz) über <paramref name="growth"/>, oder -1.</summary>
        public static int NextMilestone(int growth, bool skill)
        {
            int next = -1;
            void Check(int t)
            {
                if (t > growth && (next < 0 || t < next)) next = t;
            }
            if (skill) foreach (int t in SkillStageThresholds) Check(t);
            foreach (int t in ModuleSlotThresholds) Check(t);
            return next;
        }
    }

    /// <summary>Wendet Wachstum auf Skills und Bausteine an.</summary>
    public static class GrowthApplier
    {
        public static SkillDefinition Apply(SkillDefinition skill, GrowthRule rule, int growth)
        {
            if (skill == null || skill.IsBasicAttack || rule == null || growth <= 0) return skill;
            int bonus = rule.BonusAt(growth);
            if (bonus <= 0) return skill;
            switch (rule.Effect)
            {
                case GrowthEffect.FlatDamage: return skill.MapEffects(e => Map(e, x => x is DamageEffect d ? d.WithFlatBonus(bonus) : x));
                case GrowthEffect.StunTicks: return skill.MapEffects(e => Map(e, x => x is StunEffect s ? new StunEffect(s.Ticks + bonus, s.AllEnemies) : x));
                case GrowthEffect.PowerPercent: return skill.WithBonus(bonus, 0);
                default: return skill;
            }
        }

        /// <summary>Wirkung auch in Chancen und «an allen Gegnern» verpackt.</summary>
        private static ISkillEffect Map(ISkillEffect effect, Func<ISkillEffect, ISkillEffect> map)
        {
            switch (effect)
            {
                case ChanceEffect c: return new ChanceEffect(c.ChanceBp, Map(c.Inner, map));
                case AllEnemiesEffect a: return new AllEnemiesEffect(Map(a.Inner, map));
                default: return map(effect);
            }
        }

        /// <summary>Schwellen-Wachstum: +X Prozentpunkte bis zur Obergrenze; höhere Grundwerte bleiben.</summary>
        public static int ApplyToParameter(RuneDefinition rune, int parameter, GrowthRule rule, int growth)
        {
            if (rule == null || rule.Effect != GrowthEffect.ThresholdPercent || growth <= 0 || !ModuleRules.HasPercentThreshold(rune)) return parameter;
            int raised = (int)Math.Min(int.MaxValue, parameter + (long)growth * rule.PerPoint);
            return rule.Cap > 0 ? Math.Max(parameter, Math.Min(rule.Cap, raised)) : raised;
        }
    }

    /// <summary>Welche Skills und Runen welche Wachstums-Regel tragen. Daten, leicht änderbar.</summary>
    public sealed class GrowthCatalog
    {
        private readonly Dictionary<string, GrowthRule> _skills = new Dictionary<string, GrowthRule>();
        private readonly Dictionary<string, GrowthRule> _runes = new Dictionary<string, GrowthRule>();

        /// <summary>Für Runen ohne eigenen Eintrag: Punkte für gewonnene Kämpfe, in denen die Zeile feuerte.</summary>
        public GrowthRule DefaultRuneRule { get; set; } = new GrowthRule(GrowthTrigger.Win, GrowthEffect.None, 0, 0,
            "+1 Wachstum pro gewonnenem Kampf, in dem die Zeile feuerte");

        public void SetSkill(string skillId, GrowthRule rule) => _skills[skillId] = rule;
        public void SetRune(string runeId, GrowthRule rule) => _runes[runeId] = rule;

        public GrowthRule ForSkill(string skillId) => skillId != null && _skills.TryGetValue(skillId, out GrowthRule r) ? r : null;
        public GrowthRule ForRune(string runeId) => runeId != null && _runes.TryGetValue(runeId, out GrowthRule r) ? r : DefaultRuneRule;

        public static GrowthRule Kills(int cap = 30) =>
            new GrowthRule(GrowthTrigger.Kill, GrowthEffect.FlatDamage, 1, cap, $"+1 Schaden pro Kill mit diesem Skill (max. +{cap})");

        public static GrowthRule Stuns(int capTicks = 40) =>
            new GrowthRule(GrowthTrigger.Stun, GrowthEffect.StunTicks, Ticks.FromTenths(1), capTicks,
                $"+0,1 s Dauer pro Betäubung (max. +{SkillInfo.Seconds(capTicks)})");

        public static GrowthRule Heals(int cap = 50) =>
            new GrowthRule(GrowthTrigger.Heal, GrowthEffect.PowerPercent, 1, cap, $"+1 % Heilung pro Heilung (max. +{cap} %)");

        public static GrowthRule Wins(int cap = 50) =>
            new GrowthRule(GrowthTrigger.Win, GrowthEffect.PowerPercent, 1, cap, $"+1 % Wirkung pro gewonnenem Kampf, in dem er feuerte (max. +{cap} %)");

        /// <summary>Nur Zähler (für Skills ohne steigerbare Zahl, z. B. Buffs): Stufen und Modul-Plätze.</summary>
        public static GrowthRule WinsCounter() =>
            new GrowthRule(GrowthTrigger.Win, GrowthEffect.None, 0, 0, "+1 Wachstum pro gewonnenem Kampf, in dem er feuerte");

        public static GrowthRule Threshold(int cap = 50) =>
            new GrowthRule(GrowthTrigger.Win, GrowthEffect.ThresholdPercent, 1, cap, $"+1 % Schwelle pro gewonnenem Kampf, in dem die Zeile feuerte (max. {cap} %)");

        public static GrowthCatalog CreateDefault()
        {
            var c = new GrowthCatalog();
            c.SetSkill(SkillIds.ArmorBreak, Kills());
            c.SetSkill(SkillIds.Drill, Kills());
            c.SetSkill(SkillIds.ShockStab, Kills());
            c.SetSkill(SkillIds.Ignite, Wins());
            c.SetSkill(SkillIds.ShieldBash, Stuns());
            c.SetSkill(SkillIds.EmpBash, Stuns());
            c.SetSkill(SkillIds.Repair, Heals());
            c.SetSkill(SkillIds.Coolant, Heals());
            c.SetSkill(SkillIds.ShieldWall, WinsCounter());
            c.SetSkill(SkillIds.Flashbang, WinsCounter());
            c.SetSkill(SkillIds.Anchor, WinsCounter());
            c.SetSkill(SkillIds.Thrusters, WinsCounter());
            c.SetSkill(SkillIds.Echo, WinsCounter());

            // Evolutionsformen wachsen wie ihr Ursprung weiter.
            c.SetSkill(SkillIds.Inferno, Wins());
            c.SetSkill(SkillIds.LightningLance, Kills());
            c.SetSkill(SkillIds.Resonance, WinsCounter());
            c.SetSkill(SkillIds.AcidDrill, Kills());
            c.SetSkill(SkillIds.ScrapRam, Stuns());
            c.SetRune(EvolvedRuneIds.PhantomReflex, Threshold());

            // Runen mit Prozent-Schwelle wachsen in der Schwelle, alle anderen nur im Zähler.
            c.SetRune("hp_low", Threshold());
            c.SetRune("hp_critical", Threshold());
            c.SetRune("enemy_low", Threshold());
            return c;
        }
    }
}
