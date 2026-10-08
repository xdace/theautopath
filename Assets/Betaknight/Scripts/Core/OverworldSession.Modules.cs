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
                    result.Add((ModuleTarget.Skill(row.Skill.InstanceId), SessionTexts.SkillInRow(row.Skill.NameFrom(SkillCatalog), i)));
                else
                    result.Add((ModuleTarget.Row(row.RowId), SessionTexts.Row(i)));
            }
            return result;
        }

        /// <summary>«Bohrstoß (Zeile 3)», «Zeile 2» oder «kein Ziel», wenn das Ziel gerade nicht an der Tafel ist.</summary>
        public string DescribeTarget(ModuleTarget? target)
        {
            int row = Betaknight.Core.Gear.RuneLoadoutBoard.TargetRow(Runes, target);
            if (!target.HasValue) return SessionTexts.NoTarget;
            if (row < 0) return target.Value.Kind == ModuleTargetKind.Skill ? SessionTexts.TargetNotOnBoard : SessionTexts.RowMissing;
            RuneSlot slot = Runes.Rows[row];
            return target.Value.Kind == ModuleTargetKind.Skill ? SessionTexts.SkillInRow(slot.Skill.NameFrom(SkillCatalog), row) : SessionTexts.Row(row);
        }

        /// <summary>Freie Module, die an diesen Ort passen (für die Auswahl im Tafel-Editor).</summary>
        public List<ModuleInstance> FreeModulesFor(IModuleHolder holder) =>
            Modules.Free.Where(m => holder != null && ModuleCollection.Fits(ModuleDefinitionOf(m), holder, m)).ToList();

        /// <summary>«Skill Bohrstoß (Zeile 2)», «Baustein Zeile 1» oder «frei».</summary>
        public string ModuleWhere(ModuleInstance module)
        {
            switch (module?.Holder)
            {
                case RuneSlot row: return SessionTexts.RuneRow(Runes.IndexOfRow(row));
                case SkillInstance skill:
                    int at = skill.Holder is RuneSlot slot ? Runes.IndexOfRow(slot) : -1;
                    return at >= 0 ? SessionTexts.HolderSkill(skill.NameFrom(SkillCatalog), at) : SessionTexts.HolderSkillFree(skill.NameFrom(SkillCatalog));
                default: return SessionTexts.Free;
            }
        }

        /// <summary>«Nach Ausführung → Bohrstoß (Zeile 3)» bzw. «Wenn erfüllt → Zeile 2». Nur für Auslöser.</summary>
        public string DescribeTrigger(ModuleInstance module)
        {
            if (module == null || module.ModuleId != ModuleIds.Trigger) return string.Empty;
            string when = module.Holder is RuneSlot ? SessionTexts.WhenMet : SessionTexts.AfterExecution;
            return SessionTexts.ModuleTrigger(when, DescribeTarget(module.Target));
        }

        /// <summary>Wählt das nächste mögliche Ziel eines Auslösers (Reihenfolge der Zeilen, danach «kein Ziel»).</summary>
        public bool CycleTriggerTarget(int moduleId, int step = 1)
        {
            ModuleInstance module = Modules.Get(moduleId);
            if (module == null || module.ModuleId != ModuleIds.Trigger) return false;
            var targets = TriggerTargets().Select(t => (ModuleTarget?)t.target).ToList();
            targets.Add(null);
            int current = targets.FindIndex(t => t.HasValue == module.Target.HasValue
                && (!t.HasValue || (t.Value.Kind == module.Target.Value.Kind && t.Value.Id == module.Target.Value.Id)));
            if (current < 0) current = targets.Count - 1;
            int next = ((current + step) % targets.Count + targets.Count) % targets.Count;
            return SetTriggerTarget(moduleId, targets[next]);
        }

        /// <summary>Eine Auslöser-Verbindung an der Tafel, z. B. zum Zeichnen als Linie.</summary>
        public readonly struct TriggerLink
        {
            public readonly int From;
            public readonly int To;

            /// <summary>Vom Baustein («wenn erfüllt») statt vom Skill («nach Ausführung»).</summary>
            public readonly bool FromBlock;

            public TriggerLink(int from, int to, bool fromBlock)
            {
                From = from;
                To = to;
                FromBlock = fromBlock;
            }
        }

        /// <summary>Alle Auslöser der Tafel mit gültigem Ziel, als Verbindungen zwischen Zeilen (auch Kreise und auf sich selbst).</summary>
        public List<TriggerLink> TriggerLinks()
        {
            var links = new List<TriggerLink>();
            for (int i = 0; i < Runes.Rows.Count; i++)
            {
                RuneSlot row = Runes.Rows[i];
                foreach (ModuleInstance m in row.Modules) AddLink(links, m, i, true);
                if (row.Skill != null) foreach (ModuleInstance m in row.Skill.Modules) AddLink(links, m, i, false);
            }
            return links;
        }

        private void AddLink(List<TriggerLink> links, ModuleInstance m, int from, bool fromBlock)
        {
            if (m.ModuleId != ModuleIds.Trigger) return;
            int to = Betaknight.Core.Gear.RuneLoadoutBoard.TargetRow(Runes, m.Target);
            if (to >= 0) links.Add(new TriggerLink(from, to, fromBlock));
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
                    Modules.Upgrade(target, MaxModuleLevel(moduleId));
                    BuildImproved?.Invoke(SessionTexts.ModuleUpgrade(old, target.NameFrom(ModuleCatalog)));
                    return target;
                }
            }
            ModuleInstance added = Modules.Add(moduleId);
            BuildImproved?.Invoke(SessionTexts.NewModule(definition.Name));
            return added;
        }

        /// <summary>Welches Exemplar bei «Stufe erhöhen» steigt: zuerst ein eingesetztes, dann irgendeines.</summary>
        public ModuleInstance ModuleUpgradeTarget(string moduleId)
        {
            int max = MaxModuleLevel(moduleId);
            List<ModuleInstance> owned = Modules.OfModule(moduleId).Where(m => m.Level < max).ToList();
            return owned.FirstOrDefault(m => !m.IsFree) ?? owned.FirstOrDefault();
        }

        /// <summary>Höchste Stufe eines Moduls: so viele Stufen, wie es Beschreibungen hat (Umkehren und Auslöser haben keine).</summary>
        public int MaxModuleLevel(string moduleId) =>
            ModuleCatalog.TryGet(moduleId, out ModuleDefinition d) ? System.Math.Min(ModuleRules.MaxLevel, d.Descriptions.Count - 1) : 0;

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
            return module != null ? SessionTexts.ModuleLabel(module.NameFrom(ModuleCatalog)) : null;
        }

        private void CarryModules(OverworldSession previous) => Modules = previous.Modules;
    }
}
