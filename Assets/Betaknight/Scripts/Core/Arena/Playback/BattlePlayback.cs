using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Zustand eines Kämpfers zu einem Zeitpunkt der Wiedergabe.</summary>
    public sealed class FighterView
    {
        public FighterInfo Info { get; }
        public int Hp { get; internal set; }
        public bool Alive => Hp > 0;
        public bool Stunned => StunnedUntil > 0;
        internal int StunnedUntil;

        /// <summary>Laufende Aktion (Skill-Id) oder null.</summary>
        public string ActionSkill { get; internal set; }
        public int ActionRow { get; internal set; } = -1;
        public int ActionStartTick { get; internal set; }
        public int ActionWindupTicks { get; internal set; }

        /// <summary>Tick, an dem zuletzt eine Wirkung eintraf (für Treffer-Blitze).</summary>
        public int LastHitTick { get; internal set; } = -1000;

        internal FighterView(FighterInfo info)
        {
            Info = info;
            Hp = info.StartHp;
        }

        /// <summary>Fortschritt des Ausholens 0–1, 1 wenn keine Aktion ausholt.</summary>
        public float WindupProgress(int tick) =>
            ActionSkill == null || ActionWindupTicks <= 0 ? 1f : Math.Min(1f, (tick - ActionStartTick) / (float)ActionWindupTicks);
    }

    /// <summary>
    /// Spielt ein Kampfprotokoll ab, ohne neu zu rechnen: Leben, laufende Aktionen, feuernde Zeile, Protokollzeilen.
    /// Unity-frei, die Arena-Ansicht liest nur den Zustand.
    /// </summary>
    public sealed class BattlePlayback
    {
        /// <summary>So lange leuchtet eine Zeile nach dem Auslösen.</summary>
        public const int RowHighlightTicks = Ticks.PerSecond / 2;

        private readonly BattleResult _result;
        private readonly Dictionary<Combatant, FighterView> _byCombatant = new Dictionary<Combatant, FighterView>();
        private readonly List<FighterView> _fighters = new List<FighterView>();
        private readonly List<string> _lines = new List<string>();
        private int _next;

        public int Tick { get; private set; }
        public int EndTick => _result.EndTick;
        public bool IsFinished => Tick >= _result.EndTick && _next >= _result.Events.Count;
        public IReadOnlyList<FighterView> Fighters => _fighters;
        public BattleResult Result => _result;

        /// <summary>Zuletzt ausgelöste Zeile des Spielers und wann.</summary>
        public int LastPlayerRow { get; private set; } = -1;
        public int LastPlayerRowTick { get; private set; } = -1000;

        /// <summary>Protokollzeilen bis zum aktuellen Tick, älteste zuerst.</summary>
        public IReadOnlyList<string> Lines => _lines;

        public BattlePlayback(BattleResult result)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
            foreach (FighterInfo f in result.Fighters)
            {
                var view = new FighterView(f);
                _fighters.Add(view);
                _byCombatant[f.Combatant] = view;
            }
        }

        public FighterView Player => _fighters.Count > 0 ? _fighters[0] : null;

        public bool IsRowHighlighted(int row) => row == LastPlayerRow && Tick - LastPlayerRowTick < RowHighlightTicks;

        /// <summary>Spult um <paramref name="ticks"/> vor und wendet alle Ereignisse bis dahin an.</summary>
        public void Advance(int ticks)
        {
            if (ticks <= 0) return;
            Tick = Math.Min(_result.EndTick, Tick + ticks);
            while (_next < _result.Events.Count && _result.Events[_next].Tick <= Tick)
                Apply(_result.Events[_next++]);
            foreach (FighterView f in _fighters)
                if (f.StunnedUntil > 0 && Tick >= f.StunnedUntil) f.StunnedUntil = 0;
        }

        public void SkipToEnd() => Advance(int.MaxValue / 2);

        private FighterView View(Combatant c) => c != null && _byCombatant.TryGetValue(c, out FighterView v) ? v : null;

        private void Apply(BattleEvent e)
        {
            FighterView source = View(e.Source);
            FighterView target = View(e.Target);

            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted:
                    if (source == null) break;
                    source.ActionSkill = e.Detail;
                    source.ActionRow = e.RowIndex;
                    source.ActionStartTick = e.Tick;
                    source.ActionWindupTicks = e.Amount;
                    if (source.Info.Side == Side.Player)
                    {
                        LastPlayerRow = e.RowIndex;
                        LastPlayerRowTick = e.Tick;
                    }
                    if (e.Detail != SkillDefinition.BasicAttackId) _lines.Add(BattleLogText.Describe(e, _result));
                    break;

                case BattleEventKind.ActionExecuted:
                    if (source != null) source.ActionWindupTicks = 0;
                    break;

                case BattleEventKind.ActionInterrupted:
                    if (source != null) source.ActionSkill = null;
                    _lines.Add(BattleLogText.Describe(e, _result));
                    break;

                case BattleEventKind.Damage:
                case BattleEventKind.SelfDamage:
                    if (target == null) break;
                    target.Hp = Math.Max(0, target.Hp - e.Amount);
                    target.LastHitTick = e.Tick;
                    break;

                case BattleEventKind.Healed:
                    if (target != null) target.Hp = Math.Min(target.Info.MaxHp, target.Hp + e.Amount);
                    _lines.Add(BattleLogText.Describe(e, _result));
                    break;

                case BattleEventKind.StatusApplied:
                    if (target != null && e.Detail == StatusIds.Stun) target.StunnedUntil = e.Tick + e.Amount;
                    _lines.Add(BattleLogText.Describe(e, _result));
                    break;

                case BattleEventKind.Death:
                    if (target != null)
                    {
                        target.Hp = 0;
                        target.ActionSkill = null;
                    }
                    _lines.Add(BattleLogText.Describe(e, _result));
                    break;

                case BattleEventKind.Dodged:
                case BattleEventKind.Blocked:
                case BattleEventKind.Crit:
                case BattleEventKind.Overheat:
                case BattleEventKind.BattleEnd:
                    _lines.Add(BattleLogText.Describe(e, _result));
                    break;
            }
        }
    }
}
