using System;
using System.Collections.Generic;
using Betaknight.Core.Modules;
using Betaknight.Core.Arena;
using Betaknight.Core.Growth;

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

        /// <summary>Modul-Plätze des Exemplars: 1, mehr ab Wachstum 10 und 25. Der Basisangriff hat keine.</summary>
        public int ModuleSlots => IsBasicAttack ? 0 : GrowthStages.ModuleSlotsFor(Growth);

        public IReadOnlyList<ModuleInstance> Modules => _modules.Modules;
        string IModuleHolder.ModuleHolderName => SkillId;
        bool IModuleHolder.IsSkillHolder => true;
        void IModuleHolder.AttachModule(ModuleInstance module) => _modules.Attach(module);
        void IModuleHolder.DetachModule(ModuleInstance module) => _modules.Detach(module);

        /// <summary>Eindeutig innerhalb der Sammlung eines Runs, 0 solange das Exemplar noch keiner Sammlung gehört.</summary>
        public int InstanceId { get; internal set; }
        /// <summary>Welcher Skill. Ändert sich nur durch Evolution; Exemplar, Wachstum und Module bleiben.</summary>
        public string SkillId { get; internal set; }

        /// <summary>
        /// Wachstum: der eine Zähler des Exemplars. Punkte aus der Wachstums-Regel des Skills (Kills, Betäubungen …)
        /// und +5 je Duplikat. Bleibt über den ganzen Run, auch durch Evolution.
        /// </summary>
        public int Growth { get; internal set; }

        /// <summary>Stufe 0–3 als Meilenstein des Wachstums (5/15/30), siehe <see cref="GrowthStages"/>. Ohne eigene Werte.</summary>
        public int Level => GrowthStages.StageFor(Growth);

        /// <summary>Wo das Exemplar sitzt, null = frei in der Sammlung.</summary>
        public ISkillHolder Holder { get; internal set; }

        public bool IsFree => Holder == null;
        public bool IsBasicAttack => SkillId == SkillDefinition.BasicAttackId;

        /// <param name="level">Startet mit dem Wachstum dieser Stufe (für Aufbau und Tests).</param>
        public SkillInstance(string skillId, int level = 0)
        {
            if (string.IsNullOrEmpty(skillId)) throw new ArgumentException("Skill id missing.", nameof(skillId));
            SkillId = skillId;
            Growth = GrowthStages.GrowthForStage(Math.Max(0, level));
        }

        /// <summary>Ein neuer Basisangriff für eine Zeile (gehört zu keiner Sammlung).</summary>
        public static SkillInstance BasicAttack() => new SkillInstance(SkillDefinition.BasicAttackId);

        /// <summary>«Bohrstoß +7» (Wachstum) mit dem Namen aus dem Katalog.</summary>
        public string NameFrom(SkillCatalog catalog)
        {
            string name = catalog != null && catalog.TryGet(SkillId, out SkillDefinition s) ? s.Name : SkillId;
            return Growth > 0 ? $"{name} +{Growth}" : name;
        }

        public override string ToString() => Growth > 0 ? $"{SkillId} +{Growth} #{InstanceId}" : $"{SkillId} #{InstanceId}";
    }
}
