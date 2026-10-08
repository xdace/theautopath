using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;

namespace Betaknight.Overworld.UI
{
    /// <summary>Kurztexte für Ausrüstung in Fenstern: passive Effekte, Werte, Besonderheiten.</summary>
    public static class ItemText
    {
        public static string Describe(EquipmentDefinition item)
        {
            var parts = new List<string>();
            foreach (SkillPassive passive in item.Passives) parts.Add(passive.Text);
            foreach (KeyValuePair<StatKind, int> stat in item.Stats)
                if (stat.Value != 0) parts.Add(Stat(stat.Key, stat.Value));
            if (item.Tags.Count > 0) parts.Add(TagNames(item));
            if (item.TwoHanded) parts.Add(UiTexts.Item.TwoHandedLocksShield);
            return parts.Count > 0 ? string.Join(", ", parts) : item.Description;
        }

        /// <summary>
        /// Wertevergleich mit dem angelegten Teil im selben Platz: grün = besser, rot = schlechter.
        /// Ohne angelegtes Teil zählt alles als Gewinn.
        /// </summary>
        public static string Compare(EquipmentDefinition item, EquipmentDefinition worn)
        {
            if (worn == item) return $"<color=#888888>{UiTexts.Item.Equipped}</color>";
            var kinds = new List<StatKind>();
            foreach (StatKind k in item.Stats.Keys) if (!kinds.Contains(k)) kinds.Add(k);
            if (worn != null) foreach (StatKind k in worn.Stats.Keys) if (!kinds.Contains(k)) kinds.Add(k);

            var parts = new List<string>();
            foreach (StatKind kind in kinds)
            {
                int diff = item.StatBonus(kind) - (worn?.StatBonus(kind) ?? 0);
                if (diff == 0) continue;
                // Beim Angriffsintervall ist weniger besser.
                bool better = kind == StatKind.AttackInterval ? diff < 0 : diff > 0;
                parts.Add($"<color={(better ? "#7ddc6f" : "#ff7a6b")}>{Stat(kind, diff)}</color>");
            }
            if (parts.Count == 0) return worn != null ? UiTexts.Item.SameStats(worn.Name) : UiTexts.Item.NoStats;
            return (worn != null ? UiTexts.Item.Instead(worn.Name) : UiTexts.Item.SlotFree) + string.Join(", ", parts);
        }

        /// <summary>
        /// Set eines Teils mit allen Boni: aktive gelb mit ✔, noch nicht erreichte grau mit ○. Wird der Bonus erst mit
        /// diesem Teil aktiv, steht er grün mit «neu». <paramref name="pieces"/> = Teile mit diesem Teil angelegt.
        /// </summary>
        public static string SetBlock(SetDefinition set, int pieces, bool worn)
        {
            int before = worn ? pieces : pieces - 1;
            string head = worn ? UiTexts.Item.SetWorn(pieces, set.MaxPieces) : UiTexts.Item.SetWith(pieces, set.MaxPieces);
            var lines = new List<string> { UiTexts.Item.SetHead(set.Name, head) };
            foreach (KeyValuePair<int, string> bonus in set.Bonuses)
            {
                bool active = SetDefinition.IsActive(bonus.Key, pieces);
                bool fresh = active && !SetDefinition.IsActive(bonus.Key, before);
                string line = $"{(active ? "●" : "○")} {UiTexts.Item.Pieces(bonus.Key, bonus.Value)}";
                string color = fresh ? UiTheme.Hex(UiTheme.Good) : active ? UiTheme.Hex(UiTheme.Accent) : UiTheme.Hex(UiTheme.MutedColor);
                lines.Add($"<color={color}>{line}{(fresh ? UiTexts.Item.New : string.Empty)}</color>");
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Synergie-Tag mit allen Stufen, gefärbt wie <see cref="SetBlock(SetDefinition,int,bool)"/>.
        /// <paramref name="before"/> = Teile ohne dieses Teil (für «neu»), <paramref name="count"/> = mit.
        /// </summary>
        public static string TagBlock(SynergyTag tag, int count, int before, bool preview)
        {
            string head = preview ? UiTexts.Item.TagWith(count) : UiTexts.Item.TagWorn(count);
            var lines = new List<string> { UiTexts.Item.TagHead(tag.Name, head) };
            foreach (KeyValuePair<int, SynergyEffect> tier in tag.Tiers)
            {
                bool active = count >= tier.Key;
                bool fresh = active && before < tier.Key;
                string color = fresh ? UiTheme.Hex(UiTheme.Good) : active ? UiTheme.Hex(UiTheme.Accent) : UiTheme.Hex(UiTheme.MutedColor);
                lines.Add($"<color={color}>{(active ? "●" : "○")} {UiTexts.Item.Pieces(tier.Key, tier.Value.Text)}{(fresh ? UiTexts.Item.New : string.Empty)}</color>");
            }
            return string.Join("\n", lines);
        }

        /// <summary>Alle Tags und das Set eines Teils mit ihren Wirkungen (Vorschau beim Anlegen), leer ohne beides.</summary>
        public static string SynergyBlock(Betaknight.Core.OverworldSession session, EquipmentDefinition item)
        {
            if (item == null) return string.Empty;
            var blocks = new List<string>();
            bool worn = session.Gear.Get(item.Slot) == item;
            var seen = new HashSet<string>();
            foreach (string tagId in item.Tags)
            {
                if (!seen.Add(tagId) || !session.Synergies.TryGetTag(tagId, out SynergyTag tag)) continue;
                int count = session.TagCountWith(item, tagId);
                int before = worn ? count - 1 : session.Gear.TagCount(tagId);
                blocks.Add(TagBlock(tag, count, before, !worn));
            }
            string set = SetBlock(session, item);
            if (set.Length > 0) blocks.Add(set);
            return string.Join("\n", blocks);
        }

        /// <summary>Set-Block für ein Teil in der Session (getragen oder als Vorschau beim Anlegen), leer ohne Set.</summary>
        public static string SetBlock(Betaknight.Core.OverworldSession session, EquipmentDefinition item)
        {
            if (item?.SetId == null || !session.Sets.TryGet(item.SetId, out SetDefinition set)) return string.Empty;
            bool worn = session.Gear.Get(item.Slot) == item;
            return SetBlock(set, worn ? session.Gear.SetPieces(item.SetId) : session.SetPiecesWith(item), worn);
        }

        /// <summary>Alle Angaben zu einem Teil: Platz, Werte, passive Effekte auf Skill-Arten, Set mit allen Boni.</summary>
        public static string Details(EquipmentDefinition item, Betaknight.Core.OverworldSession session) =>
            Details(item, session.Sets, SynergyBlock(session, item));

        /// <summary>Alle Angaben zu einem Teil; ohne Session stehen die Set-Boni ohne Teilezahl da.</summary>
        public static string Details(EquipmentDefinition item, SetBonusRegistry sets, string setBlock = null)
        {
            var lines = new List<string> { $"<b>{item.Name}</b>  [{item.Slot.DisplayName()}]{(item.TwoHanded ? UiTexts.Item.TwoHanded : string.Empty)}" };
            var stats = new List<string>();
            foreach (KeyValuePair<StatKind, int> stat in item.Stats)
                if (stat.Value != 0) stats.Add(Stat(stat.Key, stat.Value));
            lines.Add(stats.Count > 0 ? UiTexts.Item.StatsPrefix + string.Join(", ", stats) : UiTexts.Item.StatsNone);
            foreach (SkillPassive passive in item.Passives) lines.Add(UiTexts.Item.Passive(passive.Text));
            if (item.Tags.Count > 0 && string.IsNullOrEmpty(setBlock)) lines.Add(UiTexts.Item.Tags(TagNames(item)));
            if (!string.IsNullOrEmpty(setBlock)) lines.Add(setBlock);
            else if (item.SetId != null && sets != null && sets.TryGet(item.SetId, out SetDefinition set)) lines.Add(SetBlock(set, 1, false));
            else if (item.SetId != null) lines.Add(UiTexts.Item.Set(item.SetId));
            if (item.Description.Length > 0) lines.Add($"<i>{item.Description}</i>");
            return string.Join("\n", lines);
        }

        private static readonly SynergyRegistry Synergies = SynergyRegistry.CreateDefault();

        /// <summary>«[Hitze] [Takt]».</summary>
        public static string TagNames(EquipmentDefinition item)
        {
            var names = new List<string>();
            foreach (string tag in item.Tags) names.Add($"[{Synergies.NameOf(tag)}]");
            return string.Join(" ", names);
        }

        /// <summary>Vorschau der Tags beim Anlegen, eine Zeile: «→ Ladung 4/6: Schwelle!, → Duo frei: ???». Leer ohne Tags.</summary>
        public static string TagPreview(Betaknight.Core.OverworldSession session, EquipmentDefinition item)
        {
            List<string> lines = session.TagPreview(item);
            if (lines.Count == 0) return string.Empty;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].EndsWith("!") || lines[i].Contains("Duo")) lines[i] = $"<color=#7ddc6f>{lines[i]}</color>";
            return string.Join("  ", lines);
        }

        public static string Stat(StatKind kind, int value)
        {
            string sign = value > 0 ? "+" : "−";
            int abs = value < 0 ? -value : value;
            switch (kind)
            {
                case StatKind.MaxHp: return UiTexts.Item.MaxHp(sign, abs);
                case StatKind.Damage: return UiTexts.Item.Damage(sign, abs);
                case StatKind.Armor: return UiTexts.Item.Armor(sign, abs);
                // Weniger Ticks zwischen Angriffen = schneller, daher umgekehrtes Vorzeichen.
                case StatKind.AttackInterval: return value < 0 ? UiTexts.Item.Faster(abs) : UiTexts.Item.Slower(abs);
                case StatKind.Dodge: return UiTexts.Item.Dodge(sign, abs / 100);
                case StatKind.Block: return UiTexts.Item.Block(sign, abs / 100);
                case StatKind.Crit: return UiTexts.Item.Crit(sign, abs / 100);
                default: return $"{kind} {sign}{abs}";
            }
        }
    }
}
