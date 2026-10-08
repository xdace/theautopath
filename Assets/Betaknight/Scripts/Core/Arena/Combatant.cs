using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    public enum Side
    {
        Player,
        Enemy,
    }

    /// <summary>Bauplan eines Kämpfers für einen Kampf. Der Simulator erzeugt daraus einen <see cref="Combatant"/>.</summary>
    public sealed class CombatantSetup
    {
        public string Name = "Kämpfer";
        public CombatStats Stats = new CombatStats();

        /// <summary>Leben zu Kampfbeginn. 0 = volles Leben (z. B. Spieler bringt seine Oberwelt-HP mit).</summary>
        public int StartHp;

        public LogicBoard Board;

        public List<BattleModifier> Modifiers = new List<BattleModifier>();

        /// <summary>Startwerte für Ressourcen wie Ladung oder Hitze.</summary>
        public Dictionary<string, int> Resources = new Dictionary<string, int>();
    }

    /// <summary>Laufende Aktion eines Kämpfers.</summary>
    public sealed class ActionState
    {
        public SkillDefinition Skill { get; internal set; }
        public Combatant Target { get; internal set; }
        public int RowIndex { get; internal set; }
        public int StartTick { get; internal set; }
        public int WindupLeft { get; internal set; }
        public int RecoveryLeft { get; internal set; }
        public bool EffectApplied { get; internal set; }

        public bool InWindup => !EffectApplied;
    }

    /// <summary>Ein Kämpfer im laufenden Kampf: Werte, Leben, Aktion, Cooldowns, Zustände, Ressourcen.</summary>
    public sealed class Combatant
    {
        private readonly CombatStats _stats;
        private readonly Dictionary<string, int> _cooldowns = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _resources = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _rollFailures = new Dictionary<string, int>();
        internal readonly List<StatusEffect> StatusList = new List<StatusEffect>();
        internal readonly List<BattleModifier> ModifierList = new List<BattleModifier>();

        internal Battle Battle;

        public string Name { get; }
        public Side Side { get; }

        /// <summary>Position in der Kampfliste (0 = Spieler).</summary>
        public int Index { get; }
        public int Hp { get; internal set; }
        public bool IsAlive => Hp > 0;
        public LogicBoard Board { get; }
        public ActionState Action { get; internal set; }

        public IReadOnlyList<StatusEffect> Statuses => StatusList;
        public IReadOnlyList<BattleModifier> Modifiers => ModifierList;

        /// <summary>Zuletzt beendete Aktion: Zeile und Tick. Für Ketten-Bedingungen.</summary>
        public int LastActionRow { get; internal set; } = -1;
        public int LastActionEndTick { get; internal set; } = -1;

        /// <summary>Zuletzt ausgeführter wiederholbarer Skill (für Echo). Basisangriff zählt nicht.</summary>
        public SkillDefinition LastRepeatableSkill { get; internal set; }

        internal Combatant(CombatantSetup setup, Side side, int index)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            Name = setup.Name;
            Side = side;
            Index = index;
            _stats = setup.Stats.Clone();
            Board = setup.Board ?? LogicBoard.FallbackOnly;
            Hp = setup.StartHp > 0 ? Math.Min(setup.StartHp, _stats[StatKind.MaxHp]) : _stats[StatKind.MaxHp];
            ModifierList.AddRange(setup.Modifiers);
            foreach (KeyValuePair<string, int> r in setup.Resources) _resources[r.Key] = r.Value;
        }

        public int MaxHp => Math.Max(1, GetStat(StatKind.MaxHp));

        public int BaseStat(StatKind kind) => _stats[kind];

        /// <summary>Grundwert + Zustände + Modifikatoren.</summary>
        public int GetStat(StatKind kind)
        {
            int value = _stats[kind];
            foreach (StatusEffect s in StatusList) value += s.StatBonus(kind);
            if (Battle != null)
            {
                foreach (BattleModifier m in ModifierList) value += m.StatBonus(Battle, this, kind);
            }
            return value;
        }

        /// <summary>Rüstung nach Multiplikator, nie negativ.</summary>
        public int EffectiveArmor => Math.Max(0, BasisPoints.Of(GetStat(StatKind.Armor), Math.Max(0, GetStat(StatKind.ArmorMultiplier))));

        /// <summary>Ticks zwischen zwei Basisangriffen mit Tempo-Bonus, mindestens 1.</summary>
        public int AttackIntervalTicks
        {
            get
            {
                int speed = BasisPoints.Full + Math.Max(-BasisPoints.Full / 2, GetStat(StatKind.AttackSpeed));
                return Math.Max(1, (int)((long)GetStat(StatKind.AttackInterval) * BasisPoints.Full / speed));
            }
        }

        public int HpPercentBp => (int)((long)Hp * BasisPoints.Full / MaxHp);

        public bool IsStunned
        {
            get
            {
                foreach (StatusEffect s in StatusList) if (s.Stuns) return true;
                return false;
            }
        }

        public bool HasStatus(string id)
        {
            foreach (StatusEffect s in StatusList) if (s.Id == id) return true;
            return false;
        }

        /// <summary>Frei = keine Aktion und nicht betäubt.</summary>
        public bool IsFree => IsAlive && Action == null && !IsStunned;

        /// <summary>Holt gerade eine sichtbare Aufladung aus (für "Gegner lädt auf").</summary>
        public bool IsCharging => Action != null && Action.InWindup && Action.Skill.IsCharge;

        public int Cooldown(string skillId) => _cooldowns.TryGetValue(skillId, out int v) ? v : 0;
        public bool IsReady(SkillDefinition skill) => Cooldown(skill.Id) <= 0;

        internal void SetCooldown(string skillId, int ticks) => _cooldowns[skillId] = Math.Max(0, ticks);

        /// <summary>Senkt alle Cooldowns um <paramref name="ticks"/>, nie unter 0.</summary>
        public void ReduceCooldowns(int ticks)
        {
            if (ticks <= 0) return;
            var keys = new List<string>(_cooldowns.Keys);
            foreach (string key in keys) _cooldowns[key] = Math.Max(0, _cooldowns[key] - ticks);
        }

        public int GetResource(string id) => _resources.TryGetValue(id, out int v) ? v : 0;
        internal void SetResourceRaw(string id, int value) => _resources[id] = value;

        /// <summary>Kopie aller Ressourcen-Zähler (für die Wiedergabe).</summary>
        public Dictionary<string, int> ResourceSnapshot() => new Dictionary<string, int>(_resources);

        /// <summary>Fehlversuche seit dem letzten Erfolg je Wurf-Art (Pseudo-Zufall).</summary>
        internal int RollFailures(string key) => _rollFailures.TryGetValue(key, out int v) ? v : 0;
        internal void SetRollFailures(string key, int value) => _rollFailures[key] = value;

        public override string ToString() => $"{Name} ({Hp}/{MaxHp})";
    }
}
