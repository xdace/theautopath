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
                string counter = next > 0 ? $"{NameOf(tagId)} {now}/{next}" : $"{NameOf(tagId)} {now} (max)";
                bool crossed = ReachedThreshold(now) > ReachedThreshold(was);
                result.Add(crossed ? $"→ {counter}: Schwelle!" : $"→ {counter}");
            }
            foreach (SynergyDuo duo in _duos)
            {
                bool wasActive = Get(before, duo.TagA) >= DuoThreshold && Get(before, duo.TagB) >= DuoThreshold;
                bool nowActive = Get(after, duo.TagA) >= DuoThreshold && Get(after, duo.TagB) >= DuoThreshold;
                if (nowActive && !wasActive) result.Add($"→ Duo frei: {duoName?.Invoke(duo) ?? duo.Name}");
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
            int halfSecond = Ticks.FromTenths(5);

            r.Register(Tag(SynergyTagIds.Heat, "Hitze",
                E("Feuer-Skills −20 % Cast-Zeit.", null, SkillPassive.Cast(SkillKind.Fire, -20)),
                E("+25 % Schaden gegen brennende Gegner.", () => new SynergyBonusVsStatus(StatusIds.Burn, 25)),
                E("Basisangriffe setzen Brennen (3 s, 25 % Waffenschaden pro Sekunde).", () => new SynergyBurnOnBasic())));
            r.Register(Tag(SynergyTagIds.Charge, "Ladung",
                E("+10 % Block.", () => new SynergyStatBonus(StatKind.Block, BasisPoints.Percent(10))),
                E("Schock-Skills −25 % Cast-Zeit.", null, SkillPassive.Cast(SkillKind.Shock, -25)),
                E("Jeder Block senkt alle eigenen Cooldowns um 0,5 s.", () => new SynergyCooldownOnDefense(BattleEventKind.Blocked, halfSecond))));
            r.Register(Tag(SynergyTagIds.Phantom, "Phantom",
                E("+10 % Ausweichen.", () => new SynergyStatBonus(StatKind.Dodge, BasisPoints.Percent(10))),
                E("Bewegung-Skills −30 % Cast-Zeit und −1 s Cooldown.", null,
                    SkillPassive.Cast(SkillKind.Movement, -30), SkillPassive.Cooldown(SkillKind.Movement, -Ticks.PerSecond)),
                E("Nach jedem Ausweichen trifft der nächste eigene Angriff +50 %.", () => new SynergyRiposte(50))));
            r.Register(Tag(SynergyTagIds.Tempo, "Takt",
                E("+10 % Angriffstempo.", () => new SynergyStatBonus(StatKind.AttackSpeed, BasisPoints.Percent(10))),
                E("Alle Skills −15 % Cast-Zeit.", null, SkillPassive.Cast(SkillKinds.Every, -15)),
                E("Alle Skills weitere −15 % Cast-Zeit (zusammen −30 %).", null, SkillPassive.Cast(SkillKinds.Every, -15))));
            r.Register(Tag(SynergyTagIds.Toxin, "Toxin",
                E("Eigene Skill-Treffer vergiften 4 s (1 Schaden pro Sekunde und Stapel, bis 5 Stapel).", () => new SynergyPoisonOnHit(false)),
                E("Eigene Angriffe +1 Schaden je Gift-Stapel des Ziels.", () => new SynergyDamagePerPoison()),
                E("Auch Basisangriffe vergiften.", () => new SynergyPoisonOnHit(true))));
            r.Register(Tag(SynergyTagIds.Scrap, "Schrott",
                E("+1 Gold je besiegtem Gegner.", () => new SynergyGoldPerKill(1)),
                E("+3 Rüstung.", () => new SynergyStatBonus(StatKind.Armor, 3)),
                E("Eigene Angriffe ignorieren Rüstung.", () => new SynergyIgnoreArmor())));

            r.Register(new SynergyDuo("ember_rhythm", "Glutrhythmus", SynergyTagIds.Heat, SynergyTagIds.Tempo,
                E("Basisangriffe gegen brennende Gegner senken alle eigenen Cooldowns um 0,25 s.",
                    () => new SynergyCooldownOnBasicVsStatus(StatusIds.Burn, Ticks.FromTenths(5) / 2))));
            r.Register(new SynergyDuo("phase_shield", "Phasenschild", SynergyTagIds.Charge, SynergyTagIds.Phantom,
                E("Jeder Block gibt +5 % Ausweichen bis Kampfende (höchstens +25 %).",
                    () => new SynergyStackOnDefense(BattleEventKind.Blocked, StatKind.Dodge, BasisPoints.Percent(5), 5))));
            r.Register(new SynergyDuo("fire_venom", "Brandgift", SynergyTagIds.Heat, SynergyTagIds.Toxin,
                E("Brennen auf vergifteten Gegnern macht +50 % Schaden.",
                    () => new SynergyDotBoostVsStatus(StatusIds.Burn, StatusIds.Poison, 50))));
            r.Register(new SynergyDuo("scrap_capacitor", "Schrottkondensator", SynergyTagIds.Scrap, SynergyTagIds.Charge,
                E("Jeder Block gibt +1 Rüstung bis Kampfende (höchstens +10).",
                    () => new SynergyStackOnDefense(BattleEventKind.Blocked, StatKind.Armor, 1, 10))));
            r.Register(new SynergyDuo("ghost_step", "Geisterschritt", SynergyTagIds.Phantom, SynergyTagIds.Tempo,
                E("Jedes Ausweichen gibt +10 % Angriffstempo bis Kampfende (höchstens +50 %).",
                    () => new SynergyStackOnDefense(BattleEventKind.Dodged, StatKind.AttackSpeed, BasisPoints.Percent(10), 5))));
            r.Register(new SynergyDuo("acid_bite", "Säurefraß", SynergyTagIds.Toxin, SynergyTagIds.Scrap,
                E("Eigene Angriffe gegen vergiftete Gegner ignorieren Rüstung.", () => new SynergyIgnoreArmor(StatusIds.Poison))));
            return r;
        }
    }
}
