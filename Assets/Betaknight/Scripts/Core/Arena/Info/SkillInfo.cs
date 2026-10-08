using System;
using System.Collections.Generic;
using System.Text;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Werte des Anwenders, mit denen Kennzahlen konkret werden: Waffenschaden, Flächenbonus, Max-HP, Angriffstakt.
    /// Gelesen zu Kampfbeginn, also mit Ausrüstung und aktiven Set-Boni, aber ohne Stapel aus dem Kampf.
    /// </summary>
    public sealed class SkillUserStats
    {
        public int WeaponDamage { get; }
        public int AreaDamageBp { get; }
        public int MaxHp { get; }

        /// <summary>Ticks zwischen zwei Basisangriffen inklusive Tempo-Bonus.</summary>
        public int AttackIntervalTicks { get; }

        public SkillUserStats(int weaponDamage, int areaDamageBp = 0, int maxHp = 30, int attackIntervalTicks = Ticks.PerSecond)
        {
            WeaponDamage = Math.Max(0, weaponDamage);
            AreaDamageBp = areaDamageBp;
            MaxHp = Math.Max(1, maxHp);
            AttackIntervalTicks = Math.Max(1, attackIntervalTicks);
        }

        public static SkillUserStats From(Combatant c) =>
            new SkillUserStats(c.GetStat(StatKind.Damage), c.GetStat(StatKind.AreaDamage), c.MaxHp, c.AttackIntervalTicks);
    }

    public enum EffectInfoKind
    {
        Damage,
        DamageOverTime,
        Heal,
        Stun,
        Interrupt,
        StatChange,
        Status,
        Resource,
        Repeat,
        Other,
    }

    /// <summary>
    /// Kennzahlen einer einzelnen Wirkung. Schaden ist auf ein Ziel ohne Rüstung, Block und Krit gerechnet.
    /// </summary>
    public sealed class EffectInfo
    {
        public EffectInfoKind Kind { get; }

        /// <summary>Kompakter Anzeigetext, z. B. "120 % Waffenschaden ≈ 18 an allen Gegnern".</summary>
        public string Text { get; }

        /// <summary>Schaden in Basispunkten des Waffenschadens (bei Schaden über Zeit: pro Sekunde).</summary>
        public int DamageBp { get; }

        /// <summary>Konkreter Wert: Schaden pro Treffer bzw. pro Sekunde, Heilung, sonst 0.</summary>
        public int Amount { get; }

        /// <summary>Gesamtwert pro Ziel: bei Schaden über Zeit über die ganze Dauer, sonst wie <see cref="Amount"/>.</summary>
        public int Total { get; }

        public int DurationTicks { get; }

        /// <summary>Chance in Basispunkten, 10000 = sicher.</summary>
        public int ChanceBp { get; }

        public bool AllEnemies { get; }

        public bool IsDamage => Kind == EffectInfoKind.Damage || Kind == EffectInfoKind.DamageOverTime;

        public EffectInfo(EffectInfoKind kind, string text, int damageBp = 0, int amount = 0, int total = -1,
            int durationTicks = 0, int chanceBp = BasisPoints.Full, bool allEnemies = false)
        {
            Kind = kind;
            Text = text ?? string.Empty;
            DamageBp = damageBp;
            Amount = amount;
            Total = total >= 0 ? total : amount;
            DurationTicks = durationTicks;
            ChanceBp = chanceBp;
            AllEnemies = allEnemies;
        }

        /// <summary>Dieselbe Wirkung, nur mit einer Chance (Text bekommt "20 % Chance: " vorangestellt).</summary>
        /// <summary>Dieselbe Kennzahl für alle Gegner («an allen Gegnern»).</summary>
        public EffectInfo ForAllEnemies() => AllEnemies ? this
            : new EffectInfo(Kind, Text + ArenaTexts.ToAllEnemies, DamageBp, Amount, Total, DurationTicks, ChanceBp, true);

        public EffectInfo WithChance(int chanceBp) => new EffectInfo(Kind, ArenaTexts.WithChance(SkillInfo.Percent(chanceBp), Text),
            DamageBp, Amount, Total, DurationTicks, (int)((long)ChanceBp * chanceBp / BasisPoints.Full), AllEnemies);
    }

    /// <summary>Sammelt die Kennzahlen, die die Wirkungen eines Skills über <see cref="ISkillEffect.Describe"/> melden.</summary>
    public sealed class SkillInfoBuilder
    {
        private readonly List<EffectInfo> _effects = new List<EffectInfo>();

        public SkillUserStats Stats { get; }
        public IReadOnlyList<EffectInfo> Effects => _effects;

        public SkillInfoBuilder(SkillUserStats stats) => Stats = stats ?? throw new ArgumentNullException(nameof(stats));

        public void Add(EffectInfo info)
        {
            if (info != null) _effects.Add(info);
        }

        /// <summary>Eine leere Sammlung mit denselben Werten, z. B. für die innere Wirkung einer Chance.</summary>
        public SkillInfoBuilder Nested() => new SkillInfoBuilder(Stats);
    }

    /// <summary>
    /// Kennzahlen eines Skills für die Anzeige: Zeiten, Wirkungen, Schaden in Prozent und konkret.
    /// Alles kommt aus den <see cref="ISkillEffect"/>-Bausteinen; neue Skills erscheinen automatisch richtig.
    /// </summary>
    public sealed class SkillInfo
    {
        public SkillDefinition Skill { get; }
        public SkillUserStats Stats { get; }
        /// <summary>Aktuelle Cast-Zeit (mit allen Änderungen, nie unter der Untergrenze).</summary>
        public int WindupTicks { get; }

        /// <summary>Grund-Cast-Zeit ohne Änderungen. Beim Basisangriff gleich der aktuellen.</summary>
        public int BaseCastTicks { get; }
        public int RecoveryTicks { get; }
        public int CooldownTicks { get; }
        public IReadOnlyList<EffectInfo> Effects { get; }

        public bool DealsDamage
        {
            get
            {
                foreach (EffectInfo e in Effects) if (e.IsDamage && e.Total > 0) return true;
                return false;
            }
        }

        private SkillInfo(SkillDefinition skill, SkillUserStats stats, int windup, int recovery, int cooldown, IReadOnlyList<EffectInfo> effects)
        {
            Skill = skill;
            Stats = stats;
            WindupTicks = windup;
            BaseCastTicks = skill.IsBasicAttack ? windup : Math.Max(1, skill.WindupTicks);
            RecoveryTicks = recovery;
            CooldownTicks = cooldown;
            Effects = effects;
        }

        public static SkillInfo Create(SkillDefinition skill, SkillUserStats stats)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            stats = stats ?? new SkillUserStats(0);

            Battle.ActionTiming(skill, stats.AttackIntervalTicks, out int windup, out int recovery);
            var builder = new SkillInfoBuilder(stats);
            foreach (ISkillEffect effect in skill.Effects) effect.Describe(builder);
            return new SkillInfo(skill, stats, windup, recovery, skill.CooldownTicks, builder.Effects);
        }

        /// <summary>Alle Schadens-Wirkungen, z. B. "120 % Waffenschaden ≈ 18 an allen Gegnern", sonst "kein Schaden".</summary>
        public string DamageText
        {
            get
            {
                var parts = new List<string>();
                foreach (EffectInfo e in Effects) if (e.IsDamage) parts.Add(e.Text);
                return parts.Count > 0 ? string.Join(", ", parts) : ArenaTexts.NoDamage;
            }
        }

        /// <summary>Wirkungen ohne Schaden (Betäubung, Heilung, Zustände ...), mit Komma getrennt.</summary>
        public string OtherEffectsText
        {
            get
            {
                var parts = new List<string>();
                foreach (EffectInfo e in Effects) if (!e.IsDamage) parts.Add(e.Text);
                return string.Join(", ", parts);
            }
        }

        /// <summary>"CD 5 s · Cast 0,8 s · Erholung 0,3 s"; geänderte Cast-Zeit mit Grundwert: "Cast 0,6 s (Grund 0,8 s)".</summary>
        public string TimingText
        {
            get
            {
                string cd = Skill.IsBasicAttack ? ArenaTexts.BasicAttackInterval(Seconds(WindupTicks + RecoveryTicks))
                    : CooldownTicks > 0 ? ArenaTexts.Cooldown(Seconds(CooldownTicks)) : ArenaTexts.NoCooldown;
                return $"{cd} · {CastText} · {ArenaTexts.Recovery(Seconds(RecoveryTicks))}";
            }
        }

        /// <summary>"Cast 0,8 s" oder mit Änderung "Cast 0,6 s (Grund 0,8 s)".</summary>
        public string CastText
        {
            get
            {
                string text = ArenaTexts.Cast(Seconds(WindupTicks));
                return WindupTicks != BaseCastTicks ? ArenaTexts.CastWithBase(text, Seconds(BaseCastTicks)) : text;
            }
        }

        /// <summary>Kompakte Infozeile: Schaden, dann Zeiten.</summary>
        public string Summary => $"{DamageText} · {TimingText}";

        /// <summary>Passive Boni von Ausrüstung und Tags auf diesen Skill, z. B. «+20 % Wirkung, −1 s CD, −20 % Cast-Zeit». Leer ohne Bonus.</summary>
        public string BonusText
        {
            get
            {
                var parts = new List<string>();
                if (Skill.PowerBonusPercent != 0) parts.Add(ArenaTexts.PowerBonus(Skill.PowerBonusPercent > 0 ? "+" : "−", Math.Abs(Skill.PowerBonusPercent)));
                if (Skill.CooldownBonusTicks != 0) parts.Add(ArenaTexts.CooldownBonus(Skill.CooldownBonusTicks < 0 ? "−" : "+", Seconds(Math.Abs(Skill.CooldownBonusTicks))));
                if (Skill.CastBonusPercent != 0) parts.Add(ArenaTexts.CastBonus(Skill.CastBonusPercent < 0 ? "−" : "+", Math.Abs(Skill.CastBonusPercent)));
                return string.Join(", ", parts);
            }
        }

        /// <summary>«Baustein ◆◆ Schwer: −30 % Cooldown, +25 % Wirkung (eingerechnet)» oder leer ohne Bonus.</summary>
        public string DifficultyLine =>
            Skill.DifficultyTier > 0
                ? ArenaTexts.DifficultyLine(DifficultyText.Symbol(Skill.DifficultyTier), DifficultyText.Name(Skill.DifficultyTier), Skill.Difficulty.Text)
                : string.Empty;

        /// <summary>Alle Details, eine Wirkung pro Zeile (für Tooltips).</summary>
        public string Details
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append(Skill.Name);
                if (Skill.Description.Length > 0) sb.Append(": ").Append(Skill.Description);
                if (Effects.Count == 0) sb.Append("\n• ").Append(ArenaTexts.NoDamage);
                foreach (EffectInfo e in Effects) sb.Append("\n• ").Append(e.Text);
                if (!DealsDamage && Effects.Count > 0) sb.Append("\n• ").Append(ArenaTexts.NoDamage);
                sb.Append('\n').Append(TimingText);
                if (Skill.Kinds != SkillKind.None) sb.Append(ArenaTexts.DetailsKinds).Append(SkillKinds.Names(Skill.Kinds));
                string bonus = BonusText;
                if (bonus.Length > 0) sb.Append(ArenaTexts.DetailsGear).Append(bonus);
                string difficulty = DifficultyLine;
                if (difficulty.Length > 0) sb.Append('\n').Append(difficulty);
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ Formatierung

        /// <summary>Ticks als Sekunden mit Punkt, ohne unnötige Nullen: 0.5 s, 1.5 s, 5 s, 0.35 s.</summary>
        public static string Seconds(int ticks) => ArenaTexts.Seconds(ticks);

        /// <summary>Basispunkte als Prozent: 12000 → "120 %", 2500 → "25 %", 1250 → "12.5 %".</summary>
        public static string Percent(int bp) => ArenaTexts.Percent(bp);

        public static string StatName(StatKind kind) => ArenaTexts.StatName(kind);

        /// <summary>Werte in Basispunkten zeigen Prozent, alle anderen ganze Zahlen.</summary>
        public static bool IsPercentStat(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.MaxHp:
                case StatKind.Damage:
                case StatKind.AttackInterval:
                case StatKind.Armor:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>"+100 % Block", "Rüstung −50 %", "+3 Rüstung".</summary>
        public static string StatChange(StatKind kind, int amount)
        {
            string value = IsPercentStat(kind) ? Percent(amount) : $"{(amount < 0 ? "−" : string.Empty)}{Math.Abs(amount)}";
            if (amount >= 0) value = "+" + value;
            return $"{StatName(kind)} {value}";
        }

        public static string ResourceName(string id) => ArenaTexts.ResourceName(id) ?? id;
    }
}
