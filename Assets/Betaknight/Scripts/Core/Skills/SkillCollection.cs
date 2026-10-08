using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Skills
{
    /// <summary>
    /// Alle Skill-Exemplare eines Runs, ohne Obergrenze. Sorgt dafür, dass jedes Exemplar an höchstens einem Ort sitzt:
    /// Einsetzen an einem neuen Ort räumt den alten, ein verdrängtes Exemplar wird frei.
    /// </summary>
    public sealed class SkillCollection
    {
        private readonly List<SkillInstance> _all = new List<SkillInstance>();
        private int _nextId = 1;

        /// <summary>Alle Exemplare in Erhalt-Reihenfolge.</summary>
        public IReadOnlyList<SkillInstance> All => _all;

        /// <summary>Exemplare, die gerade nirgends sitzen.</summary>
        public IReadOnlyList<SkillInstance> Free => _all.Where(s => s.IsFree).ToList();

        public int Count => _all.Count;

        public event Action Changed;

        /// <summary>Ein neues Exemplar, frei in der Sammlung.</summary>
        public SkillInstance Add(string skillId, int level = 0)
        {
            var skill = new SkillInstance(skillId, level);
            Adopt(skill);
            return skill;
        }

        /// <summary>
        /// Übernimmt ein bestehendes Exemplar (z. B. eines, das beim Aufbau schon an einer Zeile sitzt) und gibt ihm eine Id.
        /// Basisangriffe gehören zu keiner Sammlung und werden ignoriert.
        /// </summary>
        public void Adopt(SkillInstance skill, ISkillHolder holder = null)
        {
            if (skill == null || skill.IsBasicAttack || _all.Contains(skill)) return;
            if (skill.InstanceId <= 0 || Get(skill.InstanceId) != null) skill.InstanceId = _nextId;
            _nextId = Math.Max(_nextId, skill.InstanceId + 1);
            _all.Add(skill);
            if (holder != null && holder.Skill == skill) skill.Holder = holder;
            Changed?.Invoke();
        }

        public bool Contains(SkillInstance skill) => skill != null && _all.Contains(skill);

        public SkillInstance Get(int instanceId) => _all.FirstOrDefault(s => s.InstanceId == instanceId);

        public IReadOnlyList<SkillInstance> OfSkill(string skillId) => _all.Where(s => s.SkillId == skillId).ToList();

        public bool Owns(string skillId) => _all.Any(s => s.SkillId == skillId);

        /// <summary>
        /// Setzt ein Exemplar an einen Ort. Sitzt es woanders, wird der alte Ort leer; sitzt dort schon ein anderes
        /// Exemplar, wird es frei. Ein Basisangriff darf überall stehen und wird nicht verfolgt.
        /// </summary>
        public bool Place(SkillInstance skill, ISkillHolder holder)
        {
            if (skill == null || holder == null) return false;
            if (!skill.IsBasicAttack && !Contains(skill)) return false;
            if (holder.Skill == skill) return true;

            SkillInstance previous = holder.Skill;
            if (previous != null) previous.Holder = null;

            if (!skill.IsBasicAttack && skill.Holder != null)
            {
                skill.Holder.Hold(null);
                skill.Holder = null;
            }

            holder.Hold(skill);
            if (!skill.IsBasicAttack) skill.Holder = holder;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Nimmt das Exemplar von einem Ort; es wird frei, der Ort bleibt leer. Gibt das Exemplar zurück.</summary>
        public SkillInstance TakeFrom(ISkillHolder holder)
        {
            SkillInstance skill = holder?.Skill;
            if (skill == null) return null;
            holder.Hold(null);
            skill.Holder = null;
            Changed?.Invoke();
            return skill.IsBasicAttack ? null : skill;
        }

        /// <summary>Ein Ort verschwindet (z. B. Zeile entfernt): sein Exemplar wird frei.</summary>
        public void Release(ISkillHolder holder)
        {
            if (holder?.Skill == null) return;
            holder.Skill.Holder = null;
            Changed?.Invoke();
        }

        /// <summary>Tauscht die Exemplare zweier Orte (z. B. Umsetzen zwischen Tafel-Zeilen).</summary>
        public bool Swap(ISkillHolder a, ISkillHolder b)
        {
            if (a == null || b == null || a == b) return false;
            SkillInstance sa = a.Skill;
            SkillInstance sb = b.Skill;
            a.Hold(sb);
            b.Hold(sa);
            if (sa != null && !sa.IsBasicAttack) sa.Holder = b;
            if (sb != null && !sb.IsBasicAttack) sb.Holder = a;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hebt ein Exemplar eine Stufe an, höchstens bis <paramref name="maxLevel"/>.</summary>
        public bool Upgrade(SkillInstance skill, int maxLevel)
        {
            if (!Contains(skill) || skill.Level >= maxLevel) return false;
            skill.Level++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Entfernt ein freies Exemplar (z. B. verkauft).</summary>
        public bool Remove(SkillInstance skill)
        {
            if (!Contains(skill) || !skill.IsFree) return false;
            _all.Remove(skill);
            Changed?.Invoke();
            return true;
        }
    }
}
