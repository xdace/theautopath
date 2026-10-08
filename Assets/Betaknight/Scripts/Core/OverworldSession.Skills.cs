using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Gear;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>Was mit einem Skill passiert, den man schon besitzt.</summary>
    public enum SkillDuplicateChoice
    {
        /// <summary>Ein vorhandenes Exemplar steigt eine Stufe (sitzt eines an der Tafel, dieses).</summary>
        Upgrade,

        /// <summary>Als zweites Exemplar behalten, z. B. um denselben Skill an zwei Zeilen zu nutzen.</summary>
        KeepCopy,
    }

    /// <summary>
    /// Skills als eigene Exemplare: Sammlung, Einsetzen an der Tafel, Erhalt aus Belohnungen und Shop, Stufen.
    /// Die Tafel ist Rune (Wann) + Skill (Was), beides frei kombinierbar; Ausrüstung gibt nur Werte und passive Boni.
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Alle Skill-Exemplare des Runs. Keine Obergrenze.</summary>
        public SkillCollection Skills { get; }

        /// <summary>Ein Skill kam neu in die Sammlung (nicht bei Stufen-Erhöhung).</summary>
        public event Action<SkillInstance> SkillGained;

        /// <summary>Übernimmt Exemplare, die direkt an Zeilen gesetzt wurden (Aufbau, Tests), in die Sammlung.</summary>
        private void AdoptRowSkills()
        {
            if (Skills == null || Runes == null) return;
            foreach (RuneSlot row in Runes.Rows)
                if (row.Skill != null && !row.Skill.IsBasicAttack && !Skills.Contains(row.Skill)) Skills.Adopt(row.Skill, row);
        }

        /// <summary>Skill für eine neue Zeile: das erste freie Exemplar, sonst der Basisangriff (nie verwaist).</summary>
        private SkillInstance SkillForNewRow() => Skills.Free.FirstOrDefault() ?? SkillInstance.BasicAttack();

        public bool OwnsSkill(string skillId) => Skills.Owns(skillId);

        /// <summary>Höchste Stufe der eigenen Exemplare dieses Skills, 0 ohne Exemplar.</summary>
        public int HighestSkillLevel(string skillId)
        {
            int level = 0;
            foreach (SkillInstance s in Skills.OfSkill(skillId)) level = Math.Max(level, s.Level);
            return level;
        }

        /// <summary>Hat der Spieler ein Exemplar dieses Skills, das noch steigen kann?</summary>
        public bool CanUpgradeSkill(string skillId) => Skills.OfSkill(skillId).Any(s => s.Level < Progression.MaxSkillLevel);

        // ------------------------------------------------------------------ Tafel

        /// <summary>Skills an der Tafel umsetzen geht immer, nur nicht mitten im Kampf oder nach dem Tod.</summary>
        public bool CanEditSkills => !IsInCombat && !IsGameOver;

        /// <summary>Setzt ein Exemplar aus der Sammlung an eine Zeile. Sein alter Ort wird leer, das bisherige Exemplar der Zeile frei.</summary>
        public bool PlaceSkill(int instanceId, int row)
        {
            if (!CanEditSkills || !IsRow(row)) return false;
            SkillInstance skill = Skills.Get(instanceId);
            return skill != null && Skills.Place(skill, Runes.Rows[row]);
        }

        /// <summary>Setzt den Basisangriff an eine Zeile (kein Exemplar nötig).</summary>
        public bool PlaceBasicAttack(int row)
        {
            if (!CanEditSkills || !IsRow(row)) return false;
            if (Runes.Rows[row].Skill?.IsBasicAttack == true) return true;
            return Skills.Place(SkillInstance.BasicAttack(), Runes.Rows[row]);
        }

        /// <summary>Nimmt den Skill einer Zeile heraus: die Zeile ist verwaist, das Exemplar frei.</summary>
        public bool RemoveSkill(int row)
        {
            if (!CanEditSkills || !IsRow(row) || Runes.Rows[row].Skill == null) return false;
            Skills.TakeFrom(Runes.Rows[row]);
            return true;
        }

        /// <summary>Tauscht die Skills zweier Zeilen (Umsetzen zwischen Zeilen).</summary>
        public bool SwapSkills(int rowA, int rowB)
        {
            if (!CanEditSkills || !IsRow(rowA) || !IsRow(rowB) || rowA == rowB) return false;
            return Skills.Swap(Runes.Rows[rowA], Runes.Rows[rowB]);
        }

        private bool IsRow(int row) => row >= 0 && row < Runes.Rows.Count;

        // ------------------------------------------------------------------ Erhalt

        /// <summary>
        /// Ein Skill kommt dazu. Besitzt man ihn schon, entscheidet <paramref name="choice"/>: Stufe erhöhen (wenn möglich)
        /// oder ein zweites Exemplar. Neue Exemplare liegen frei in der Sammlung. Gibt das betroffene Exemplar zurück.
        /// </summary>
        public SkillInstance GainSkill(string skillId, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!SkillCatalog.TryGet(skillId, out SkillDefinition definition) || definition.IsBasicAttack) return null;

            if (choice == SkillDuplicateChoice.Upgrade)
            {
                SkillInstance target = UpgradeTarget(skillId);
                if (target != null)
                {
                    SkillInfo before = DescribeSkill(target);
                    string oldName = target.NameFrom(SkillCatalog);
                    Skills.Upgrade(target, Progression.MaxSkillLevel);
                    string change = PowerChange(before, DescribeSkill(target));
                    BuildImproved?.Invoke(change != null ? $"{oldName} → {target.NameFrom(SkillCatalog)}: {change}" : $"{oldName} → {target.NameFrom(SkillCatalog)}");
                    return target;
                }
            }

            SkillInstance added = Skills.Add(skillId);
            SkillGained?.Invoke(added);
            BuildImproved?.Invoke(SessionTexts.NewSkill(definition.Name, OwnsSkillTwice(skillId)));
            return added;
        }

        private bool OwnsSkillTwice(string skillId) => Skills.OfSkill(skillId).Count > 1;

        /// <summary>Welches Exemplar bei «Stufe erhöhen» steigt: zuerst eines an der Tafel, dann das höchste.</summary>
        public SkillInstance UpgradeTarget(string skillId)
        {
            List<SkillInstance> owned = Skills.OfSkill(skillId).Where(s => s.Level < Progression.MaxSkillLevel).ToList();
            if (owned.Count == 0) return null;
            SkillInstance placed = owned.FirstOrDefault(s => !s.IsFree);
            return placed ?? owned.OrderByDescending(s => s.Level).First();
        }

        public bool CanTakeSkill(int index) =>
            PendingRuneOffer != null && index >= 0 && index < PendingRuneOffer.SkillIds.Count && SkillCatalog.Contains(PendingRuneOffer.SkillIds[index]);

        /// <summary>Nimmt einen Skill aus dem wartenden Angebot.</summary>
        public bool TakeSkill(int index, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!CanTakeSkill(index)) return false;
            string id = PendingRuneOffer.SkillIds[index];
            PendingRuneOffer = null;
            GainSkill(id, choice);
            CheckShards();
            return true;
        }

        public bool CanBuyShopSkill(int index) =>
            PendingShop != null && index >= 0 && index < PendingShop.Inventory.SkillIds.Count && Stats.Gold >= ShopPrices.Skill;

        public bool BuyShopSkill(int index, SkillDuplicateChoice choice = SkillDuplicateChoice.Upgrade)
        {
            if (!CanBuyShopSkill(index)) return false;
            string id = PendingShop.Inventory.SkillIds[index];
            Stats.TrySpendGold(ShopPrices.Skill);
            PendingShop.Inventory.RemoveSkillAt(index);
            GainSkill(id, choice);
            return true;
        }

        // ------------------------------------------------------------------ Angebote

        /// <summary>
        /// Skill-Arten, zu denen der Build passt: eigene Skills, passive Effekte der Ausrüstung und die Runen
        /// (Klinge → Angriff, Schild → Schild, Funke → Schock, Glut → Feuer und Heilung, Phantom → Bewegung).
        /// </summary>
        public SkillKind BuildKinds()
        {
            SkillKind kinds = SkillKind.None;
            foreach (SkillInstance s in Skills.All)
                if (SkillCatalog.TryGet(s.SkillId, out SkillDefinition d)) kinds |= d.Kinds;
            foreach (SkillPassive p in Gear.Passives) kinds |= p.Target;
            foreach (RuneSlot row in Runes.Rows) kinds |= SkillKindsFor(row.Rune.Tag);
            return kinds;
        }

        public static SkillKind SkillKindsFor(RuneTag tag)
        {
            switch (tag)
            {
                case RuneTag.Blade: return SkillKind.Attack;
                case RuneTag.Shield: return SkillKind.Shield;
                case RuneTag.Spark: return SkillKind.Shock;
                case RuneTag.Ember: return SkillKind.Fire | SkillKind.Healing;
                case RuneTag.Phantom: return SkillKind.Movement;
                default: return SkillKind.None;
            }
        }

        /// <summary>Verbessert dieser Skill den Build? Eine Stufe für einen eigenen Skill oder ein neuer Skill passender Art.</summary>
        public bool IsImprovementSkill(string skillId)
        {
            if (!SkillCatalog.TryGet(skillId, out SkillDefinition skill) || skill.IsBasicAttack) return false;
            if (OwnsSkill(skillId)) return CanUpgradeSkill(skillId);
            return skill.Kinds.Overlaps(BuildKinds());
        }

        /// <summary>Angebotsgewicht: Skills mit Arten, die zum Build passen, kommen häufiger.</summary>
        public int SkillOfferWeight(SkillDefinition skill)
        {
            if (skill == null || skill.IsBasicAttack) return 0;
            int weight = Progression.SkillOfferWeight;
            return skill.Kinds.Overlaps(BuildKinds()) ? weight * Progression.SkillKindMatchFactor : weight;
        }

        private List<string> RollRewardSkills(string source)
        {
            int chance = Progression.SkillOfferChance(source);
            return chance > 0 && _random.Next(100) < chance ? PickSkills(1) : new List<string>();
        }

        /// <summary>Würfelt verschiedene Skills nach Angebotsgewicht. Eigene Skills dürfen kommen (Stufe oder zweites Exemplar).</summary>
        private List<string> PickSkills(int count, ICollection<string> exclude = null)
        {
            var pool = SkillCatalog.All.Where(s => !s.IsBasicAttack && !s.IsEvolution && (exclude == null || !exclude.Contains(s.Id))).ToList();
            var result = new List<string>();
            while (result.Count < count && pool.Count > 0)
            {
                int total = pool.Sum(SkillOfferWeight);
                if (total <= 0) break;
                int roll = _random.Next(total);
                foreach (SkillDefinition skill in pool)
                {
                    roll -= SkillOfferWeight(skill);
                    if (roll >= 0) continue;
                    result.Add(skill.Id);
                    pool.Remove(skill);
                    break;
                }
            }
            return result;
        }

        /// <summary>Eine Stufe für einen eigenen Skill als Verbesserung, bevorzugt einer an der Tafel.</summary>
        private string PickImprovementSkill(IReadOnlyCollection<string> already)
        {
            var placed = Runes.Rows.Where(r => r.Skill != null && !r.Skill.IsBasicAttack && r.Skill.Level < Progression.MaxSkillLevel)
                .Select(r => r.SkillId).Where(id => !already.Contains(id)).Distinct().ToList();
            if (placed.Count > 0) return placed[_random.Next(placed.Count)];
            var owned = Skills.All.Where(s => s.Level < Progression.MaxSkillLevel).Select(s => s.SkillId)
                .Where(id => !already.Contains(id)).Distinct().ToList();
            return owned.Count > 0 ? owned[_random.Next(owned.Count)] : null;
        }
    }
}
