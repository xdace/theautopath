using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Synergie-Tags auf Ausrüstung: Jedes Teil trägt 1–2 Tags, gezählt über alle getragenen Teile. Die Schwellen
    /// 2/4/6 schalten je Tag eine Stufe frei (Stufen gelten zusammen). Haben zwei Tags gleichzeitig mindestens 4,
    /// wird ihr Duo frei. Wirkungen sind Daten: passive Effekte auf Skills und <see cref="BattleModifier"/>.
    /// </summary>
    public sealed class SynergyRegistry
    {
        /// <summary>Schwellen in Teilen.</summary>
        public static readonly IReadOnlyList<int> Thresholds = new[] { 2, 4, 6 };

        /// <summary>Ab so vielen Teilen beider Tags greift ein Duo.</summary>
        public const int DuoThreshold = 4;

        private readonly List<SynergyTag> _tags = new List<SynergyTag>();
        private readonly List<SynergyDuo> _duos = new List<SynergyDuo>();

        public IReadOnlyList<SynergyTag> Tags => _tags;
        public IReadOnlyList<SynergyDuo> Duos => _duos;

        public void Register(SynergyTag tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag));
            _tags.RemoveAll(t => t.Id == tag.Id);
            _tags.Add(tag);
        }

        public void Register(SynergyDuo duo)
        {
            if (duo == null) throw new ArgumentNullException(nameof(duo));
            _duos.RemoveAll(d => d.Id == duo.Id);
            _duos.Add(duo);
        }

        public bool TryGetTag(string id, out SynergyTag tag)
        {
            tag = _tags.FirstOrDefault(t => t.Id == id);
            return tag != null;
        }

        public string NameOf(string tagId) => TryGetTag(tagId, out SynergyTag t) ? t.Name : tagId;

        // ------------------------------------------------------------------ Zählen

        /// <summary>Höchste erreichte Schwelle für eine Teilezahl, 0 ohne.</summary>
        public static int ReachedThreshold(int count)
        {
            int reached = 0;
            foreach (int t in Thresholds) if (count >= t) reached = t;
            return reached;
        }

        /// <summary>Nächste Schwelle über der Teilezahl, 0 wenn alle erreicht.</summary>
        public static int NextThreshold(int count)
        {
            foreach (int t in Thresholds) if (count < t) return t;
            return 0;
        }

        public SynergyCounter Counter(SynergyTag tag, int count) => new SynergyCounter(tag, count, ReachedThreshold(count), NextThreshold(count));

        /// <summary>Zähler aller Tags mit mindestens einem getragenen Teil, in Katalog-Reihenfolge.</summary>
        public List<SynergyCounter> Counters(Equipment equipment)
        {
            var result = new List<SynergyCounter>();
            if (equipment == null) return result;
            foreach (SynergyTag tag in _tags)
            {
                int count = equipment.TagCount(tag.Id);
                if (count > 0) result.Add(Counter(tag, count));
            }
            return result;
        }

        /// <summary>Alle freigeschalteten Stufen: (Tag, Schwelle, Wirkung).</summary>
        public List<(SynergyTag tag, int pieces, SynergyEffect effect)> ActiveTiers(Equipment equipment)
        {
            var result = new List<(SynergyTag, int, SynergyEffect)>();
            if (equipment == null) return result;
            foreach (SynergyTag tag in _tags)
            {
                int count = equipment.TagCount(tag.Id);
                foreach (KeyValuePair<int, SynergyEffect> tier in tag.Tiers)
                    if (count >= tier.Key) result.Add((tag, tier.Key, tier.Value));
            }
            return result;
        }

        public bool IsDuoActive(SynergyDuo duo, Equipment equipment) =>
            equipment != null && equipment.TagCount(duo.TagA) >= DuoThreshold && equipment.TagCount(duo.TagB) >= DuoThreshold;

        public List<SynergyDuo> ActiveDuos(Equipment equipment) => _duos.Where(d => IsDuoActive(d, equipment)).ToList();

        /// <summary>Passive Effekte aller aktiven Stufen und Duos (z. B. Cast-Zeit nach Skill-Art).</summary>
        public List<SkillPassive> Passives(Equipment equipment)
        {
            var result = new List<SkillPassive>();
            foreach ((SynergyTag _, int _, SynergyEffect effect) in ActiveTiers(equipment)) result.AddRange(effect.Passives);
            foreach (SynergyDuo duo in ActiveDuos(equipment)) result.AddRange(duo.Effect.Passives);
            return result;
        }

        /// <summary>Kampfregeln aller aktiven Stufen und Duos, neu für jeden Kampf.</summary>
        public List<BattleModifier> CreateModifiers(Equipment equipment)
        {
            var result = new List<BattleModifier>();
            foreach ((SynergyTag _, int _, SynergyEffect effect) in ActiveTiers(equipment))
            {
                BattleModifier m = effect.CreateModifier();
                if (m != null) result.Add(m);
            }
            foreach (SynergyDuo duo in ActiveDuos(equipment))
            {
                BattleModifier m = duo.Effect.CreateModifier();
                if (m != null) result.Add(m);
            }
            return result;
        }

        // ------------------------------------------------------------------ Vorschau für Angebote

        /// <summary>
        /// Was ein Teil an Tags bewirken würde, wenn man es anlegt (verdrängte Teile zählen nicht mehr):
        /// «→ Ladung 4/6: Schwelle!», «→ Hitze 1/2», «→ Duo frei: Brandgift». Leer ohne Tags.
        /// </summary>
        public List<string> Preview(Equipment equipment, EquipmentDefinition item, Func<SynergyDuo, string> duoName = null)
        {
            var result = new List<string>();
            if (item == null || item.Tags.Count == 0) return result;
            Dictionary<string, int> before = Counts(equipment);
            Dictionary<string, int> after = CountsIfEquipped(equipment, item);

            foreach (string tagId in item.Tags)
            {
                int was = before.TryGetValue(tagId, out int b) ? b : 0;
                int now = after.TryGetValue(tagId, out int a) ? a : 0;
                int next = NextThreshold(now);
                string counter = next > 0 ? CatalogTexts.TagCounter(NameOf(tagId), now, next) : CatalogTexts.TagCounterMax(NameOf(tagId), now);
                bool crossed = ReachedThreshold(now) > ReachedThreshold(was);
                result.Add(crossed ? CatalogTexts.PreviewTagThreshold(counter) : CatalogTexts.PreviewTag(counter));
            }
            foreach (SynergyDuo duo in _duos)
            {
                bool wasActive = Get(before, duo.TagA) >= DuoThreshold && Get(before, duo.TagB) >= DuoThreshold;
                bool nowActive = Get(after, duo.TagA) >= DuoThreshold && Get(after, duo.TagB) >= DuoThreshold;
                if (nowActive && !wasActive) result.Add(CatalogTexts.PreviewDuo(duoName?.Invoke(duo) ?? duo.Name));
            }
            return result;
        }

        private static int Get(Dictionary<string, int> counts, string id) => counts.TryGetValue(id, out int v) ? v : 0;

        private static Dictionary<string, int> Counts(Equipment equipment)
        {
            var counts = new Dictionary<string, int>();
            if (equipment == null) return counts;
            foreach (EquipmentDefinition worn in equipment.Items)
                foreach (string tag in worn.Tags) counts[tag] = Get(counts, tag) + 1;
            return counts;
        }

        private static Dictionary<string, int> CountsIfEquipped(Equipment equipment, EquipmentDefinition item)
        {
            var counts = new Dictionary<string, int>();
            if (equipment != null)
            {
                foreach (EquipmentDefinition worn in equipment.Items)
                {
                    if (worn.Slot == item.Slot || (item.TwoHanded && worn.Slot == EquipmentSlot.Shield)) continue;
                    foreach (string tag in worn.Tags) counts[tag] = Get(counts, tag) + 1;
                }
            }
            foreach (string tag in item.Tags) counts[tag] = Get(counts, tag) + 1;
            return counts;
        }

        // ------------------------------------------------------------------ Standard-Tags und Duos (vorläufig)

        private static SynergyEffect E(string text, Func<BattleModifier> modifier = null, params SkillPassive[] passives) =>
            new SynergyEffect(text, passives, modifier);

        private static SynergyTag Tag(string id, string name, SynergyEffect two, SynergyEffect four, SynergyEffect six) =>
            new SynergyTag(id, name, new Dictionary<int, SynergyEffect> { { 2, two }, { 4, four }, { 6, six } });

        public static SynergyRegistry CreateDefault()
        {
            var r = new SynergyRegistry();

            r.Register(Tag(SynergyTagIds.Heat, "Heat",
                E("Fire Skills −20 % Cast Time.", null, SkillPassive.Cast(SkillKind.Fire, -20)),
                E("+25 % Damage against burning enemies.", () => new SynergyBonusVsStatus(StatusIds.Burn, 25)),
                E("Basic Attacks apply Burn (3 s, 25 % Weapon Damage per second).", () => new SynergyBurnOnBasic())));
            r.Register(Tag(SynergyTagIds.Charge, "Static",
                E("+10 % Block.", () => new SynergyStatBonus(StatKind.Block, BasisPoints.Percent(10))),
                E("Shock Skills −25 % Cast Time.", null, SkillPassive.Cast(SkillKind.Shock, -25)),
                E("Every Block hastes you: −20 % Cast Time for 2 s.", () => new SynergyHasteOnDefense(BattleEventKind.Blocked, 20, Ticks.FromSeconds(2)))));
            r.Register(Tag(SynergyTagIds.Phantom, "Phantom",
                E("+10 % Dodge.", () => new SynergyStatBonus(StatKind.Dodge, BasisPoints.Percent(10))),
                E("Movement Skills −45 % Cast Time.", null, SkillPassive.Cast(SkillKind.Movement, -45)),
                E("After every Dodge your next attack hits +50 %.", () => new SynergyRiposte(50))));
            r.Register(Tag(SynergyTagIds.Tempo, "Haste",
                E("+10 % Attack Speed.", () => new SynergyStatBonus(StatKind.AttackSpeed, BasisPoints.Percent(10))),
                E("All Skills −15 % Cast Time.", null, SkillPassive.Cast(SkillKinds.Every, -15)),
                E("All Skills another −15 % Cast Time (−30 % total).", null, SkillPassive.Cast(SkillKinds.Every, -15))));
            r.Register(Tag(SynergyTagIds.Toxin, "Toxin",
                E("Your skill hits poison for 4 s (1 Damage per second per stack, up to 5 stacks).", () => new SynergyPoisonOnHit(false)),
                E("Your attacks deal +1 Damage per Poison stack on the target.", () => new SynergyDamagePerPoison()),
                E("Basic Attacks poison too.", () => new SynergyPoisonOnHit(true))));
            r.Register(Tag(SynergyTagIds.Scrap, "Scrap",
                E("+1 Gold per defeated enemy.", () => new SynergyGoldPerKill(1)),
                E("+3 Armor.", () => new SynergyStatBonus(StatKind.Armor, 3)),
                E("Your attacks ignore Armor.", () => new SynergyIgnoreArmor())));

            r.Register(new SynergyDuo("ember_rhythm", "Ember Rhythm", SynergyTagIds.Heat, SynergyTagIds.Tempo,
                E("Basic Attacks against burning enemies haste you: −15 % Cast Time for 1 s.",
                    () => new SynergyHasteOnBasicVsStatus(StatusIds.Burn, 15, Ticks.FromSeconds(1)))));
            r.Register(new SynergyDuo("phase_shield", "Phase Shield", SynergyTagIds.Charge, SynergyTagIds.Phantom,
                E("Every Block grants +5 % Dodge until the fight ends (max. +25 %).",
                    () => new SynergyStackOnDefense(BattleEventKind.Blocked, StatKind.Dodge, BasisPoints.Percent(5), 5))));
            r.Register(new SynergyDuo("fire_venom", "Fire Venom", SynergyTagIds.Heat, SynergyTagIds.Toxin,
                E("Burn on poisoned enemies deals +50 % Damage.",
                    () => new SynergyDotBoostVsStatus(StatusIds.Burn, StatusIds.Poison, 50))));
            r.Register(new SynergyDuo("scrap_capacitor", "Scrap Capacitor", SynergyTagIds.Scrap, SynergyTagIds.Charge,
                E("Every Block grants +1 Armor until the fight ends (max. +10).",
                    () => new SynergyStackOnDefense(BattleEventKind.Blocked, StatKind.Armor, 1, 10))));
            r.Register(new SynergyDuo("ghost_step", "Ghost Step", SynergyTagIds.Phantom, SynergyTagIds.Tempo,
                E("Every Dodge grants +10 % Attack Speed until the fight ends (max. +50 %).",
                    () => new SynergyStackOnDefense(BattleEventKind.Dodged, StatKind.AttackSpeed, BasisPoints.Percent(10), 5))));
            r.Register(new SynergyDuo("acid_bite", "Acid Bite", SynergyTagIds.Toxin, SynergyTagIds.Scrap,
                E("Your attacks against poisoned enemies ignore Armor.", () => new SynergyIgnoreArmor(StatusIds.Poison))));
            return r;
        }
    }
}
