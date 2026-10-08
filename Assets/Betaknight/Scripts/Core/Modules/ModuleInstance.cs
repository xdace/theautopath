using System.Collections.Generic;

namespace Betaknight.Core.Modules
{
    /// <summary>Worauf ein Auslöser zielt: ein Skill-Exemplar oder eine Tafel-Zeile, jeweils per stabiler Id.</summary>
    public enum ModuleTargetKind
    {
        Skill,
        Row,
    }

    public readonly struct ModuleTarget
    {
        public readonly ModuleTargetKind Kind;
        public readonly int Id;

        public ModuleTarget(ModuleTargetKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static ModuleTarget Skill(int instanceId) => new ModuleTarget(ModuleTargetKind.Skill, instanceId);
        public static ModuleTarget Row(int rowId) => new ModuleTarget(ModuleTargetKind.Row, rowId);

        public override string ToString() => $"{Kind}#{Id}";
    }

    /// <summary>Ein Ort für Module: ein Skill-Exemplar oder ein Logikbaustein (Tafel-Zeile).</summary>
    public interface IModuleHolder
    {
        /// <summary>Anzeige, z. B. «Zeile 2» oder der Skill-Name.</summary>
        string ModuleHolderName { get; }

        /// <summary>Anzahl Modul-Plätze (Start 1).</summary>
        int ModuleSlots { get; }

        /// <summary>Sitzt hier ein Skill (true) oder ein Baustein (false)?</summary>
        bool IsSkillHolder { get; }

        IReadOnlyList<ModuleInstance> Modules { get; }

        void AttachModule(ModuleInstance module);
        void DetachModule(ModuleInstance module);
    }

    /// <summary>Ein Modul-Exemplar: Modul-Id, Stufe, eindeutige Id, Ort und (bei Auslösern) Ziel.</summary>
    public sealed class ModuleInstance
    {
        public int InstanceId { get; internal set; }
        public string ModuleId { get; }
        public int Level { get; internal set; }

        /// <summary>Wo das Modul sitzt, oder null (frei in der Sammlung).</summary>
        public IModuleHolder Holder { get; internal set; }

        /// <summary>Ziel eines Auslösers, oder null (noch nicht gewählt: der Auslöser tut nichts).</summary>
        public ModuleTarget? Target { get; internal set; }

        public bool IsFree => Holder == null;

        public ModuleInstance(string moduleId, int level = 0)
        {
            ModuleId = moduleId;
            Level = level;
        }

        public string NameFrom(ModuleCatalog catalog)
        {
            string name = catalog != null && catalog.TryGet(ModuleId, out ModuleDefinition d) ? d.Name : ModuleId;
            return Level > 0 ? $"{name} +{Level}" : name;
        }

        public override string ToString() => $"{ModuleId}+{Level}#{InstanceId}";
    }

    /// <summary>Gemeinsame Ablage der Module eines Orts.</summary>
    public sealed class ModuleSlotList
    {
        private readonly List<ModuleInstance> _modules = new List<ModuleInstance>();
        public IReadOnlyList<ModuleInstance> Modules => _modules;
        public void Attach(ModuleInstance m) { if (!_modules.Contains(m)) _modules.Add(m); }
        public void Detach(ModuleInstance m) => _modules.Remove(m);
    }
}
