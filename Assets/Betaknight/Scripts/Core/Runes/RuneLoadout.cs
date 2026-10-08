using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Skills;

namespace Betaknight.Core.Runes
{
    /// <summary>
    /// Eine Zeile der Logik-Tafel aus Spielersicht: Rune (das "Wann") mit Stufe und ein Skill-Exemplar (das "Was").
    /// Ohne Skill ist die Zeile verwaist und wird im Kampf übersprungen. Als <see cref="ISkillHolder"/> ist sie ein Ort
    /// für genau ein Exemplar; belegt wird sie über die <see cref="SkillCollection"/> der Session.
    /// </summary>
    public sealed class RuneSlot : ISkillHolder
    {
        private readonly RuneLoadout _owner;

        public RuneDefinition Rune { get; internal set; }
        public int Level { get; internal set; }

        /// <summary>Das Skill-Exemplar der Zeile oder null (verwaist).</summary>
        public SkillInstance Skill { get; private set; }

        /// <summary>Id des Skills der Zeile oder null.</summary>
        public string SkillId => Skill?.SkillId;

        /// <summary>Stufe des Skill-Exemplars (0 ohne Skill).</summary>
        public int SkillLevel => Skill?.Level ?? 0;

        internal RuneSlot(RuneLoadout owner, RuneDefinition rune, SkillInstance skill)
        {
            _owner = owner;
            Rune = rune;
            Skill = skill;
        }

        public string HolderName => $"Zeile {_owner.IndexOfRow(this) + 1}";

        void ISkillHolder.Hold(SkillInstance skill)
        {
            Skill = skill;
            _owner.NotifyChanged();
        }

        public string Name => Rune.NameAt(Level);
        public string Description => Rune.DescriptionAt(Level);
        public bool CanUpgrade => Level < Rune.MaxLevel;
    }

    /// <summary>
    /// Die ausgerüsteten Runen des Spielers als Zeilen der Logik-Tafel. Reihenfolge = Priorität.
    /// Die begrenzten Plätze zwingen zu Entscheidungen.
    /// </summary>
    public sealed class RuneLoadout
    {
        private readonly List<RuneSlot> _rows = new List<RuneSlot>();

        public int Slots { get; private set; }

        /// <summary>Zeilen in Prioritätsreihenfolge.</summary>
        public IReadOnlyList<RuneSlot> Rows => _rows;

        /// <summary>Nur die Runen, in Prioritätsreihenfolge.</summary>
        public IReadOnlyList<RuneDefinition> Runes => _rows.Select(r => r.Rune).ToList();

        public bool IsFull => _rows.Count >= Slots;

        public event Action Changed;

        public RuneLoadout(int slots = 3)
        {
            if (slots < 1) throw new ArgumentOutOfRangeException(nameof(slots));
            Slots = slots;
        }

        public bool Contains(RuneDefinition rune) => rune != null && _rows.Any(r => r.Rune.Id == rune.Id);

        public int IndexOf(RuneDefinition rune) => rune == null ? -1 : _rows.FindIndex(r => r.Rune.Id == rune.Id);

        public bool HasTag(RuneTag tag) => _rows.Any(r => r.Rune.Tag == tag);

        /// <summary>
        /// Neue Zeile mit einem neuen Exemplar dieses Skills (oder ohne Skill). Die Session übernimmt das Exemplar
        /// in ihre Sammlung. Für Aufbau und Tests; im Spiel setzt die Session vorhandene Exemplare ein.
        /// </summary>
        public bool TryAdd(RuneDefinition rune, string skillId = null, int level = 0) =>
            TryAdd(rune, skillId != null ? new SkillInstance(skillId) : null, level);

        /// <summary>Neue Zeile mit diesem Exemplar. Es darf noch nirgends sitzen.</summary>
        public bool TryAdd(RuneDefinition rune, SkillInstance skill, int level = 0)
        {
            if (rune == null || IsFull || Contains(rune)) return false;
            if (skill != null && !skill.IsBasicAttack && skill.Holder != null) return false;
            var row = new RuneSlot(this, rune, skill) { Level = Math.Max(0, Math.Min(level, rune.MaxLevel)) };
            if (skill != null && !skill.IsBasicAttack) skill.Holder = row;
            _rows.Add(row);
            Changed?.Invoke();
            return true;
        }

        internal int IndexOfRow(RuneSlot row) => _rows.IndexOf(row);

        internal void NotifyChanged() => Changed?.Invoke();

        /// <summary>
        /// Tauscht die Rune einer Zeile gegen eine andere mit deren Stufe. Der zugeordnete Skill bleibt an der Zeile.
        /// Die bisherige Rune kommt mit ihrer Stufe zurück, z. B. fürs Runen-Inventar.
        /// </summary>
        public bool SwapRune(int index, RuneDefinition rune, int level, out RuneDefinition oldRune, out int oldLevel)
        {
            oldRune = null;
            oldLevel = 0;
            if (rune == null || !IsValid(index)) return false;
            for (int i = 0; i < _rows.Count; i++)
                if (i != index && _rows[i].Rune.Id == rune.Id) return false;

            oldRune = _rows[index].Rune;
            oldLevel = _rows[index].Level;
            _rows[index].Rune = rune;
            _rows[index].Level = Math.Max(0, Math.Min(level, rune.MaxLevel));
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Entfernt eine Zeile und gibt sie zurück (Rune, Stufe). Ihr Skill-Exemplar wird frei. Null bei ungültigem Index.
        /// </summary>
        public RuneSlot RemoveAt(int index)
        {
            if (!IsValid(index)) return null;
            RuneSlot row = _rows[index];
            if (row.Skill != null) row.Skill.Holder = null;
            _rows.RemoveAt(index);
            Changed?.Invoke();
            return row;
        }

        /// <summary>Ersetzt die Rune einer Zeile. Der zugeordnete Skill bleibt, die Stufe beginnt neu.</summary>
        public bool TryReplace(int index, RuneDefinition rune)
        {
            if (rune == null || !IsValid(index) || Contains(rune)) return false;
            _rows[index].Rune = rune;
            _rows[index].Level = 0;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Verschiebt eine Zeile, z. B. um ihre Priorität zu erhöhen.</summary>
        public bool Move(int from, int to)
        {
            if (!IsValid(from) || !IsValid(to)) return false;
            if (from == to) return true;
            RuneSlot row = _rows[from];
            _rows.RemoveAt(from);
            _rows.Insert(to, row);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hebt die Rune einer Zeile eine Stufe an (Lagerfeuer).</summary>
        public bool Upgrade(int index)
        {
            if (!IsValid(index) || !_rows[index].CanUpgrade) return false;
            _rows[index].Level++;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Erste Zeile mit der niedrigsten Stufe, die noch steigen kann, oder -1.</summary>
        public int BestUpgradeTarget()
        {
            int best = -1;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].CanUpgrade && (best < 0 || _rows[i].Level < _rows[best].Level)) best = i;
            return best;
        }

        public void AddSlot()
        {
            Slots++;
            Changed?.Invoke();
        }

        /// <summary>Anzahl Runen pro Tag, z. B. für die Anzeige "2x Klinge".</summary>
        public Dictionary<RuneTag, int> CountByTag()
        {
            var result = new Dictionary<RuneTag, int>();
            foreach (RuneSlot r in _rows)
            {
                result.TryGetValue(r.Rune.Tag, out int n);
                result[r.Rune.Tag] = n + 1;
            }
            return result;
        }

        private bool IsValid(int index) => index >= 0 && index < _rows.Count;
    }
}
