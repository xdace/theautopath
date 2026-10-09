using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Modules
{
    /// <summary>
    /// Alle Modul-Exemplare des Runs. Ein Exemplar sitzt höchstens an einem Ort; Orte haben begrenzte Plätze und
    /// nehmen nur passende Module (Skill-Module an Skills, Baustein-Module an Zeilen, Auslöser an beiden).
    /// </summary>
    public sealed class ModuleCollection
    {
        private readonly List<ModuleInstance> _all = new List<ModuleInstance>();
        private int _nextId = 1;

        public IReadOnlyList<ModuleInstance> All => _all;
        public IReadOnlyList<ModuleInstance> Free => _all.Where(m => m.IsFree).ToList();
        public int Count => _all.Count;

        public event Action Changed;

        public ModuleInstance Add(string moduleId, int level = 0)
        {
            var m = new ModuleInstance(moduleId, level) { InstanceId = _nextId++ };
            _all.Add(m);
            Changed?.Invoke();
            return m;
        }

        public ModuleInstance Get(int instanceId) => _all.FirstOrDefault(m => m.InstanceId == instanceId);
        public IReadOnlyList<ModuleInstance> OfModule(string moduleId) => _all.Where(m => m.ModuleId == moduleId).ToList();
        public bool Owns(string moduleId) => _all.Any(m => m.ModuleId == moduleId);

        /// <summary>Passt das Modul an diesen Ort und ist dort ein Platz frei (oder sitzt es schon dort)?</summary>
        public static bool Fits(ModuleDefinition definition, IModuleHolder holder, ModuleInstance module = null)
        {
            if (definition == null || holder == null) return false;
            if (holder.IsSkillHolder ? !definition.FitsSkill : !definition.FitsBlock) return false;
            if (module != null && module.Holder == holder) return true;
            return holder.Modules.Count < holder.ModuleSlots;
        }

        /// <summary>Setzt ein Modul an einen Ort. Sein alter Ort wird frei. Ein neuer Ort setzt das Auslöser-Ziel nicht zurück.</summary>
        public bool Place(ModuleInstance module, ModuleDefinition definition, IModuleHolder holder)
        {
            if (module == null || !_all.Contains(module) || !Fits(definition, holder, module)) return false;
            if (module.Holder == holder) return true;
            module.Holder?.DetachModule(module);
            module.Holder = holder;
            holder.AttachModule(module);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Nimmt ein Modul ab; es liegt danach frei in der Sammlung.</summary>
        /// <summary>Verwirft ein Modul ganz (vorher abgenommen, falls eingesetzt).</summary>
        public bool Remove(ModuleInstance module)
        {
            if (module == null || !_all.Contains(module)) return false;
            if (module.Holder != null) TakeOff(module);
            _all.Remove(module);
            Changed?.Invoke();
            return true;
        }

        public bool TakeOff(ModuleInstance module)
        {
            if (module?.Holder == null) return false;
            module.Holder.DetachModule(module);
            module.Holder = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Alle Module eines Orts werden frei (z. B. wenn die Zeile verschwindet).</summary>
        public void Release(IModuleHolder holder)
        {
            if (holder == null) return;
            foreach (ModuleInstance m in holder.Modules.ToList()) TakeOff(m);
        }

        public void SetTarget(ModuleInstance module, ModuleTarget? target)
        {
            if (module == null) return;
            module.Target = target;
            Changed?.Invoke();
        }

        public bool Upgrade(ModuleInstance module, int maxLevel)
        {
            if (module == null || module.Level >= maxLevel) return false;
            module.Level++;
            Changed?.Invoke();
            return true;
        }
    }
}
