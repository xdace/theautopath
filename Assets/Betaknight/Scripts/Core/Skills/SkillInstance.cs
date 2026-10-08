using System;
using System.Collections.Generic;
using Betaknight.Core.Modules;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Skills
{
    /// <summary>
    /// Ein Ort, an dem genau ein Skill-Exemplar sitzen kann: heute eine Zeile der Logik-Tafel, später ein Sockel
    /// in einem Ausrüstungsteil. Belegt und geleert wird ein Ort nur über die <see cref="SkillCollection"/>.
    /// </summary>
    public interface ISkillHolder
    {
        /// <summary>Das Exemplar an diesem Ort oder null.</summary>
        SkillInstance Skill { get; }

        /// <summary>Anzeige des Orts, z. B. «Zeile 2» oder «Sockel: Kurzklinge».</summary>
        string HolderName { get; }

        /// <summary>Setzt das Exemplar. Nur die <see cref="SkillCollection"/> ruft das auf.</summary>
        void Hold(SkillInstance skill);
    }

    /// <summary>
    /// Ein Skill-Exemplar im Run: welcher Skill, welche Stufe, und wo es gerade sitzt. Denselben Skill zweimal
    /// einzusetzen braucht zwei Exemplare. Der Basisangriff ist kein Exemplar: er kommt von der Waffe und darf
    /// an beliebig vielen Zeilen stehen (<see cref="IsBasicAttack"/>).
    /// </summary>
    public sealed class SkillInstance : IModuleHolder
    {
        private readonly ModuleSlotList _modules = new ModuleSlotList();

        /// <summary>Modul-Plätze des Exemplars (Start 1, der Basisangriff hat keine).</summary>
        public int ModuleSlots { get; internal set; }

        public IReadOnlyList<ModuleInstance> Modules => _modules.Modules;
        string IModuleHolder.ModuleHolderName => SkillId;
        bool IModuleHolder.IsSkillHolder => true;
        void IModuleHolder.AttachModule(ModuleInstance module) => _modules.Attach(module);
        void IModuleHolder.DetachModule(ModuleInstance module) => _modules.Detach(module);

        /// <summary>Eindeutig innerhalb der Sammlung eines Runs, 0 solange das Exemplar noch keiner Sammlung gehört.</summary>
        public int InstanceId { get; internal set; }
        public string SkillId { get; }
        public int Level { get; internal set; }

        /// <summary>Wo das Exemplar sitzt, null = frei in der Sammlung.</summary>
        public ISkillHolder Holder { get; internal set; }

        public bool IsFree => Holder == null;
        public bool IsBasicAttack => SkillId == SkillDefinition.BasicAttackId;

        public SkillInstance(string skillId, int level = 0)
        {
            if (string.IsNullOrEmpty(skillId)) throw new ArgumentException("Skill-Id fehlt.", nameof(skillId));
            SkillId = skillId;
            Level = Math.Max(0, level);
            ModuleSlots = IsBasicAttack ? 0 : 1;
        }

        /// <summary>Ein neuer Basisangriff für eine Zeile (gehört zu keiner Sammlung).</summary>
        public static SkillInstance BasicAttack() => new SkillInstance(SkillDefinition.BasicAttackId);

        /// <summary>«Bohrstoß +2» mit dem Namen aus dem Katalog.</summary>
        public string NameFrom(SkillCatalog catalog)
        {
            string name = catalog != null && catalog.TryGet(SkillId, out SkillDefinition s) ? s.Name : SkillId;
            return Level > 0 ? $"{name} +{Level}" : name;
        }

        public override string ToString() => Level > 0 ? $"{SkillId} +{Level} #{InstanceId}" : $"{SkillId} #{InstanceId}";
    }
}
