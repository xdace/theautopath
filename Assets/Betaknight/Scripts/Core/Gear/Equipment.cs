using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>
    /// Getragene Ausrüstung des Ritters. Liefert Werte, passive Skill-Boni und Set-Teile.
    /// Regel: eine Zweihandwaffe sperrt den Schild-Platz.
    /// </summary>
    public sealed class Equipment
    {
        private readonly Dictionary<EquipmentSlot, EquipmentDefinition> _worn = new Dictionary<EquipmentSlot, EquipmentDefinition>();

        public event Action Changed;

        public EquipmentDefinition Get(EquipmentSlot slot) => _worn.TryGetValue(slot, out EquipmentDefinition item) ? item : null;

        /// <summary>Getragene Teile in Platz-Reihenfolge.</summary>
        public IReadOnlyList<EquipmentDefinition> Items
        {
            get
            {
                var list = new List<EquipmentDefinition>();
                foreach (EquipmentSlot slot in AllSlots)
                    if (_worn.TryGetValue(slot, out EquipmentDefinition item)) list.Add(item);
                return list;
            }
        }

        public static IReadOnlyList<EquipmentSlot> AllSlots { get; } = (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot));

        public bool IsShieldLocked => Get(EquipmentSlot.Weapon)?.TwoHanded == true;

        public bool CanEquip(EquipmentDefinition item, out string reason)
        {
            reason = null;
            if (item == null)
            {
                reason = "Kein Teil.";
                return false;
            }
            if (item.Slot == EquipmentSlot.Shield && IsShieldLocked)
            {
                reason = "Die Zweihandwaffe sperrt den Schild-Platz.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Legt ein Teil an. Gibt die abgelegten Teile zurück (altes Teil im Platz, bei Zweihand auch den Schild),
        /// oder null, wenn das Teil nicht angelegt werden kann.
        /// </summary>
        public List<EquipmentDefinition> Equip(EquipmentDefinition item)
        {
            if (!CanEquip(item, out _)) return null;

            var removed = new List<EquipmentDefinition>();
            if (_worn.TryGetValue(item.Slot, out EquipmentDefinition old)) removed.Add(old);
            if (item.TwoHanded && _worn.TryGetValue(EquipmentSlot.Shield, out EquipmentDefinition shield))
            {
                removed.Add(shield);
                _worn.Remove(EquipmentSlot.Shield);
            }

            _worn[item.Slot] = item;
            Changed?.Invoke();
            return removed;
        }

        public EquipmentDefinition Unequip(EquipmentSlot slot)
        {
            if (!_worn.TryGetValue(slot, out EquipmentDefinition item)) return null;
            _worn.Remove(slot);
            Changed?.Invoke();
            return item;
        }

        /// <summary>Alle passiven Effekte der getragenen Teile in Platz-Reihenfolge.</summary>
        public IReadOnlyList<SkillPassive> Passives
        {
            get
            {
                var list = new List<SkillPassive>();
                foreach (EquipmentDefinition item in Items) list.AddRange(item.Passives);
                return list;
            }
        }

        /// <summary>Summe der passiven Boni für einen Skill (nur Arten, die er hat).</summary>
        public void SkillBonus(SkillDefinition skill, out int powerPercent, out int cooldownTicks)
        {
            powerPercent = 0;
            cooldownTicks = 0;
            foreach (EquipmentDefinition item in _worn.Values)
            {
                foreach (SkillPassive p in item.Passives)
                {
                    if (!p.Affects(skill)) continue;
                    if (p.Effect == SkillPassiveEffect.PowerPercent) powerPercent += p.Value;
                    else cooldownTicks += p.Value;
                }
            }
        }

        /// <summary>Der Skill mit allen passiven Boni der getragenen Ausrüstung.</summary>
        public SkillDefinition Boost(SkillDefinition skill)
        {
            if (skill == null) return null;
            SkillBonus(skill, out int power, out int cooldown);
            return skill.WithBonus(power, cooldown);
        }

        /// <summary>Haben getragene Teile passive Effekte auf diesen Tag?</summary>
        public bool BoostsKind(SkillKind tag)
        {
            foreach (EquipmentDefinition item in _worn.Values)
                foreach (SkillPassive p in item.Passives)
                    if (p.Target.Overlaps(tag)) return true;
            return false;
        }

        public int StatBonus(StatKind kind)
        {
            int total = 0;
            foreach (EquipmentDefinition item in _worn.Values) total += item.StatBonus(kind);
            return total;
        }

        public int SetPieces(string setId)
        {
            if (string.IsNullOrEmpty(setId)) return 0;
            int count = 0;
            foreach (EquipmentDefinition item in _worn.Values) if (item.SetId == setId) count++;
            return count;
        }

        /// <summary>Grundwerte plus Ausrüstungsboni. Max-HP und Angriffsintervall bleiben mindestens 1.</summary>
        public CombatStats ApplyTo(CombatStats baseStats)
        {
            CombatStats stats = (baseStats ?? new CombatStats()).Clone();
            foreach (StatKind kind in (StatKind[])Enum.GetValues(typeof(StatKind)))
            {
                int bonus = StatBonus(kind);
                if (bonus != 0) stats[kind] = stats[kind] + bonus;
            }
            stats[StatKind.MaxHp] = Math.Max(1, stats[StatKind.MaxHp]);
            stats[StatKind.AttackInterval] = Math.Max(1, stats[StatKind.AttackInterval]);
            return stats;
        }
    }
}
