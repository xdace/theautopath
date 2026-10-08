using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Runes
{
    /// <summary>
    /// Eine Zeile der Logik-Tafel aus Spielersicht: Rune (das "Wann") mit Stufe und zugeordnetem Skill (das "Was").
    /// Ohne Skill ist die Zeile verwaist und wird im Kampf übersprungen.
    /// </summary>
    public sealed class RuneSlot
    {
        public RuneDefinition Rune { get; internal set; }
        public int Level { get; internal set; }
        public string SkillId { get; internal set; }

        internal RuneSlot(RuneDefinition rune, string skillId)
        {
            Rune = rune;
            SkillId = skillId;
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

        public bool HasTag(RuneTag tag) => _rows.Any(r => r.Rune.Tag == tag);

        public bool TryAdd(RuneDefinition rune, string skillId = null)
        {
            if (rune == null || IsFull || Contains(rune)) return false;
            _rows.Add(new RuneSlot(rune, skillId));
            Changed?.Invoke();
            return true;
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

        public bool AssignSkill(int index, string skillId)
        {
            if (!IsValid(index)) return false;
            _rows[index].SkillId = skillId;
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
