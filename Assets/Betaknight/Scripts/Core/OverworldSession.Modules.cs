using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>
    /// Module als Exemplare: Sammlung, Einsetzen an Skill-Exemplaren und Logikbausteinen (Tafel-Zeilen), Auslöser-Ziele,
    /// seltener Erhalt (Elite, Boss-Flucht, Truhe, teurer Shop-Platz). Ein Modul sitzt an genau einem Ort.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public ModuleCatalog ModuleCatalog { get; } = ModuleCatalog.CreateDefault();

        /// <summary>Alle Modul-Exemplare des Runs, wandert durch die Akte mit.</summary>
        public ModuleCollection Modules { get; private set; } = new ModuleCollection();

        public bool CanEditModules => CanEditSkills;

        /// <summary>Module, deren Ort verschwunden ist (Zeile entfernt, Exemplar weg), werden frei.</summary>
        private void ReleaseOrphanModules()
        {
            foreach (ModuleInstance m in Modules.All.ToList())
            {
                bool gone = m.Holder is RuneSlot row ? !Runes.Rows.Contains(row)
                    : m.Holder is SkillInstance skill && !Skills.Contains(skill);
                if (gone) Modules.TakeOff(m);
            }
        }

        public ModuleDefinition ModuleDefinitionOf(ModuleInstance module) =>
            module != null && ModuleCatalog.TryGet(module.ModuleId, out ModuleDefinition d) ? d : null;

        /// <summary>Kann dieses Modul an das Skill-Exemplar bzw. an die Zeile?</summary>
        public bool CanPlaceModule(ModuleInstance module, IModuleHolder holder) =>
            CanEditModules && module != null && ModuleCollection.Fits(ModuleDefinitionOf(module), holder, module);

        /// <summary>Setzt ein Modul an ein eigenes Skill-Exemplar.</summary>
        public bool PlaceModuleOnSkill(int moduleId, int skillInstanceId)
        {
            ModuleInstance module = Modules.Get(moduleId);
            SkillInstance skill = Skills.Get(skillInstanceId);
            return skill != null && CanPlaceModule(module, skill) && Modules.Place(module, ModuleDefinitionOf(module), skill);
        }

        /// <summary>Setzt ein Modul an den Logikbaustein einer Zeile.</summary>
        public bool PlaceModuleOnRow(int moduleId, int row)
        {
            ModuleInstance module = Modules.Get(moduleId);
            if (!IsRow(row)) return false;
            RuneSlot slot = Runes.Rows[row];
            return CanPlaceModule(module, slot) && Modules.Place(module, ModuleDefinitionOf(module), slot);
        }

        /// <summary>Nimmt ein Modul ab; es liegt danach frei in der Sammlung.</summary>
        public bool TakeOffModule(int moduleId) => CanEditModules && Modules.TakeOff(Modules.Get(moduleId));

        /// <summary>Setzt das Ziel eines Auslösers (Skill-Exemplar oder Zeile per stabiler Id), null = kein Ziel.</summary>
        public bool SetTriggerTarget(int moduleId, ModuleTarget? target)
        {
            ModuleInstance module = Modules.Get(moduleId);
            if (!CanEditModules || module == null || module.ModuleId != ModuleIds.Trigger) return false;
            Modules.SetTarget(module, target);
            return true;
        }

        /// <summary>Mögliche Auslöser-Ziele: jede Zeile mit Skill (als Skill-Exemplar, sonst als Zeile).</summary>
        public List<(ModuleTarget target, string label)> TriggerTargets()
        {
            var result = new List<(ModuleTarget, string)>();
            for (int i = 0; i < Runes.Rows.Count; i++)
            {
                RuneSlot row = Runes.Rows[i];
                if (row.Skill != null && !row.Skill.IsBasicAttack)
                    result.Add((ModuleTarget.Skill(row.Skill.InstanceId), $"{row.Skill.NameFrom(SkillCatalog)} (Zeile {i + 1})"));
                else
                    result.Add((ModuleTarget.Row(row.RowId), $"Zeile {i + 1}"));
            }
            return result;
        }

        /// <summary>«Bohrstoß (Zeile 3)», «Zeile 2» oder «kein Ziel», wenn das Ziel gerade nicht an der Tafel ist.</summary>
        public string DescribeTarget(ModuleTarget? target)
        {
            int row = Betaknight.Core.Gear.RuneLoadoutBoard.TargetRow(Runes, target);
            if (!target.HasValue) return "kein Ziel";
            if (row < 0) return target.Value.Kind == ModuleTargetKind.Skill ? "Ziel nicht an der Tafel" : "Zeile fehlt";
            RuneSlot slot = Runes.Rows[row];
            return target.Value.Kind == ModuleTargetKind.Skill ? $"{slot.Skill.NameFrom(SkillCatalog)} (Zeile {row + 1})" : $"Zeile {row + 1}";
        }

        // ------------------------------------------------------------------ Erhalt

        /// <summary>Ein Modul kommt dazu; ein vorhandenes steigt (Upgrade) oder ein weiteres Exemplar entsteht.</summary>
        public ModuleInstance GainModule(string moduleId, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!ModuleCatalog.TryGet(moduleId, out ModuleDefinition definition)) return null;
            if (choice == SkillDuplicateChoice.Upgrade)
            {
                ModuleInstance target = ModuleUpgradeTarget(moduleId);
                if (target != null)
                {
                    string old = target.NameFrom(ModuleCatalog);
                    Modules.Upgrade(target, ModuleRules.MaxLevel);
                    BuildImproved?.Invoke($"Modul {old} → {target.NameFrom(ModuleCatalog)}");
                    return target;
                }
            }
            ModuleInstance added = Modules.Add(moduleId);
            BuildImproved?.Invoke($"Neues Modul: {definition.Name}");
            return added;
        }

        /// <summary>Welches Exemplar bei «Stufe erhöhen» steigt: zuerst ein eingesetztes, dann irgendeines.</summary>
        public ModuleInstance ModuleUpgradeTarget(string moduleId)
        {
            List<ModuleInstance> owned = Modules.OfModule(moduleId).Where(m => m.Level < ModuleRules.MaxLevel).ToList();
            return owned.FirstOrDefault(m => !m.IsFree) ?? owned.FirstOrDefault();
        }

        public bool CanTakeModule(int index) =>
            PendingRuneOffer != null && index >= 0 && index < PendingRuneOffer.ModuleIds.Count && ModuleCatalog.Contains(PendingRuneOffer.ModuleIds[index]);

        public bool TakeModule(int index, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!CanTakeModule(index)) return false;
            string id = PendingRuneOffer.ModuleIds[index];
            PendingRuneOffer = null;
            GainModule(id, choice);
            CheckShards();
            return true;
        }

        public bool CanBuyShopModule(int index) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.ModuleIds.Count && Stats.Gold >= ShopPrices.Module;

        public bool BuyShopModule(int index, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!CanBuyShopModule(index)) return false;
            string id = PendingShop.Inventory.ModuleIds[index];
            Stats.TrySpendGold(ShopPrices.Module);
            PendingShop.Inventory.RemoveModuleAt(index);
            GainModule(id, choice);
            return true;
        }

        private List<string> RollRewardModules(string source)
        {
            int chance = Progression.ModuleOfferChance(source);
            return chance > 0 && _random.Next(100) < chance ? PickModules(1) : new List<string>();
        }

        private List<string> RollShopModules() =>
            _random.Next(100) < Progression.ShopModuleChance ? PickModules(1) : new List<string>();

        /// <summary>Würfelt verschiedene Module nach Gewicht.</summary>
        private List<string> PickModules(int count)
        {
            var pool = ModuleCatalog.All.Where(m => m.Weight > 0).ToList();
            var result = new List<string>();
            while (result.Count < count && pool.Count > 0)
            {
                int roll = _random.Next(pool.Sum(m => m.Weight));
                foreach (ModuleDefinition m in pool)
                {
                    roll -= m.Weight;
                    if (roll >= 0) continue;
                    result.Add(m.Id);
                    pool.Remove(m);
                    break;
                }
            }
            return result;
        }

        /// <summary>Boss-Flucht: ein Modul ist garantiert.</summary>
        private string GrantBossModule()
        {
            List<string> picked = PickModules(1);
            if (picked.Count == 0) return null;
            ModuleInstance module = GainModule(picked[0], SkillDuplicateChoice.KeepCopy);
            return module != null ? $"Modul: {module.NameFrom(ModuleCatalog)}" : null;
        }

        private void CarryModules(OverworldSession previous) => Modules = previous.Modules;
    }
}
