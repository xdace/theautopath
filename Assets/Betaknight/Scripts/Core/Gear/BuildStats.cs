using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Ein Wert der Stat-Leiste: Name, Text und Vergleichszahl (höher = besser).</summary>
    public readonly struct StatLine
    {
        public string Label { get; }
        public string Text { get; }

        /// <summary>Zahl zum Vergleichen; höher ist immer besser (bei Tempo: Angriffe pro 1000 s).</summary>
        public int Score { get; }

        public StatLine(string label, string text, int score)
        {
            Label = label;
            Text = text;
            Score = score;
        }
    }

    /// <summary>Eine Änderung zwischen zwei Ständen: «Rüstung 6 → 9», Vorzeichen +1 besser, −1 schlechter.</summary>
    public readonly struct StatChange
    {
        public string Label { get; }
        public string Before { get; }
        public string After { get; }
        public int Sign { get; }

        public StatChange(string label, string before, string after, int sign)
        {
            Label = label;
            Before = before;
            After = after;
            Sign = sign;
        }

        public override string ToString() => $"{Label} {Before} → {After}";
    }

    /// <summary>
    /// Werte des Ritters zu Kampfbeginn für die Stat-Leiste (genau wie der Simulator ihn baut) plus aktive Set-Boni,
    /// Tag-Stufen und Duos. Reine Anzeige; <see cref="Compare"/> liefert die Vorschau «vorher → nachher».
    /// </summary>
    public sealed class BuildStats
    {
        public int Hp { get; }
        public int MaxHp { get; }
        public int WeaponDamage { get; }
        public int AttackIntervalTicks { get; }
        public int Armor { get; }
        public int DodgeBp { get; }
        public int BlockBp { get; }
        public int CritBp { get; }
        public int AccuracyBp { get; }
        public int AreaDamageBp { get; }

        /// <summary>Aktive Set-Boni, Tag-Stufen und Duos als kurze Texte («Aegis 2/3», «Ladung 4/6», «Duo ???»).</summary>
        public IReadOnlyList<string> Bonuses { get; }

        public double AttacksPerSecond => (double)Ticks.PerSecond / Math.Max(1, AttackIntervalTicks);

        public BuildStats(int hp, int maxHp, int weaponDamage, int attackIntervalTicks, int armor, int dodgeBp, int blockBp,
            int critBp, int accuracyBp, int areaDamageBp, IReadOnlyList<string> bonuses = null)
        {
            MaxHp = Math.Max(1, maxHp);
            Hp = Math.Max(0, Math.Min(hp, MaxHp));
            WeaponDamage = weaponDamage;
            AttackIntervalTicks = Math.Max(1, attackIntervalTicks);
            Armor = armor;
            DodgeBp = dodgeBp;
            BlockBp = blockBp;
            CritBp = critBp;
            AccuracyBp = accuracyBp;
            AreaDamageBp = areaDamageBp;
            Bonuses = bonuses ?? Array.Empty<string>();
        }

        /// <summary>Liest die Werte eines gebauten Kämpfers (Rüstung samt Multiplikator).</summary>
        public static BuildStats From(Combatant c, int hp, IReadOnlyList<string> bonuses)
        {
            int armor = (int)((long)c.GetStat(StatKind.Armor) * c.GetStat(StatKind.ArmorMultiplier) / BasisPoints.Full);
            return new BuildStats(hp, c.MaxHp, c.GetStat(StatKind.Damage), c.AttackIntervalTicks, armor, c.GetStat(StatKind.Dodge),
                c.GetStat(StatKind.Block), c.GetStat(StatKind.Crit), c.GetStat(StatKind.Accuracy), c.GetStat(StatKind.AreaDamage), bonuses);
        }

        /// <summary>Die Leiste in fester Reihenfolge.</summary>
        public List<StatLine> Lines() => new List<StatLine>
        {
            new StatLine("HP", $"{Hp}/{MaxHp}", MaxHp),
            new StatLine("Waffenschaden", WeaponDamage.ToString(), WeaponDamage),
            new StatLine("Angriffe/s", Hundredths((int)Math.Round(AttacksPerSecond * 100)), (int)Math.Round(AttacksPerSecond * 1000)),
            new StatLine("Rüstung", Armor.ToString(), Armor),
            new StatLine("Ausweichen", SkillInfo.Percent(DodgeBp), DodgeBp),
            new StatLine("Block", SkillInfo.Percent(BlockBp), BlockBp),
            new StatLine("Krit", SkillInfo.Percent(CritBp), CritBp),
            new StatLine("Präzision", SkillInfo.Percent(AccuracyBp), AccuracyBp),
            new StatLine("Flächenschaden", SkillInfo.Percent(AreaDamageBp), AreaDamageBp),
        };

        /// <summary>Was sich von <paramref name="before"/> zu <paramref name="after"/> ändert, inklusive Boni, die dazukommen oder wegfallen.</summary>
        public static List<StatChange> Compare(BuildStats before, BuildStats after)
        {
            var changes = new List<StatChange>();
            if (before == null || after == null) return changes;
            List<StatLine> a = before.Lines(), b = after.Lines();
            for (int i = 0; i < a.Count; i++)
                if (a[i].Score != b[i].Score) changes.Add(new StatChange(a[i].Label, a[i].Text, b[i].Text, Math.Sign(b[i].Score - a[i].Score)));

            foreach (string bonus in after.Bonuses)
                if (!Contains(before.Bonuses, bonus)) changes.Add(new StatChange(bonus, "–", "aktiv", 1));
            foreach (string bonus in before.Bonuses)
                if (!Contains(after.Bonuses, bonus)) changes.Add(new StatChange(bonus, "aktiv", "–", -1));
            return changes;
        }

        /// <summary>125 → «1,25» (ohne Kultur-Einstellungen).</summary>
        private static string Hundredths(int value) => $"{value / 100},{value % 100:00}";

        private static bool Contains(IReadOnlyList<string> list, string value)
        {
            foreach (string s in list) if (s == value) return true;
            return false;
        }
    }
}
