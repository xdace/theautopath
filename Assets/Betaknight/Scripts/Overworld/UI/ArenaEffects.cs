using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Zustand der eigenen Effekte (A-21) während der Wiedergabe, gelesen aus demselben Kampfprotokoll wie
    /// <see cref="BattlePlayback"/>: Hitze je Komponente, Hacks (Bit Flip, Jam, Hijack), Firewall-Ladungen, Tiefe und
    /// Verstärkung der laufenden Ausführung, Verstärkung der Pulse, Stufe des Thermal Throttling. Dazu Protokollzeilen der
    /// neuen Ereignisse und kurze Blitze (Overheat, Parallel Thread …) für die Anzeige. Gerechnet wird nichts.
    /// </summary>
    public sealed class ArenaEffects
    {
        /// <summary>Ein kurzer Hinweis an einer Komponente: Kämpfer, Komponente, Text, Farbe.</summary>
        public readonly struct Flash
        {
            public readonly int Fighter;
            public readonly int Row;
            public readonly string Text;
            public readonly string EffectId;

            public Flash(int fighter, int row, string text, string effectId)
            {
                Fighter = fighter;
                Row = row;
                Text = text;
                EffectId = effectId;
            }
        }

        /// <summary>Zustand eines Kämpfers.</summary>
        private sealed class FighterFx
        {
            public readonly Dictionary<int, int> Heat = new Dictionary<int, int>();
            public readonly Dictionary<int, int> FlippedUntil = new Dictionary<int, int>();
            public readonly Dictionary<int, int> Jam = new Dictionary<int, int>();
            public readonly HashSet<int> Hijacked = new HashSet<int>();
            public int Firewall;
            public int ActionRow = -1;
            public int ActionDepth;
            public int ActionPower;
        }

        private readonly BattleResult _result;
        private readonly Dictionary<Combatant, int> _index = new Dictionary<Combatant, int>();
        private readonly List<FighterFx> _fx = new List<FighterFx>();
        private readonly Dictionary<(int link, int tick), int> _pulsePower = new Dictionary<(int, int), int>();
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly List<Flash> _flashes = new List<Flash>();
        private int _next;
        private int _tick;

        public ArenaEffects(BattleResult result)
        {
            _result = result;
            for (int i = 0; i < result.Fighters.Count; i++)
            {
                FighterInfo f = result.Fighters[i];
                if (f.Combatant != null) _index[f.Combatant] = i;
                LogicBoard board = f.Combatant?.Board;
                _fx.Add(new FighterFx
                {
                    Firewall = board != null ? board.BoardEffectCount(CircuitEffectIds.Firewall) * System.Math.Max(0, board.EffectConfig.FirewallCharges) : 0,
                });
            }
        }

        /// <summary>Stufe des Thermal Throttling (0 = noch nicht) und Tick der letzten Stufe.</summary>
        public int OverheatLevel { get; private set; }
        public int OverheatTick { get; private set; } = -1000;

        /// <summary>Protokollzeilen der neuen Ereignisse (A-21), die <see cref="BattlePlayback"/> nicht selbst schreibt.</summary>
        public IReadOnlyList<LogEntry> Entries => _entries;

        /// <summary>Wendet alle Ereignisse bis <paramref name="tick"/> an (wie die Wiedergabe: Tick ≤ aktueller Tick).</summary>
        public void Advance(int tick)
        {
            _tick = tick;
            IReadOnlyList<BattleEvent> events = _result.Events;
            while (_next < events.Count && events[_next].Tick <= tick) Apply(events[_next++]);
        }

        /// <summary>Holt die Blitze seit dem letzten Aufruf.</summary>
        public List<Flash> TakeFlashes()
        {
            var taken = new List<Flash>(_flashes);
            _flashes.Clear();
            return taken;
        }

        private FighterFx Fx(int fighter) => fighter >= 0 && fighter < _fx.Count ? _fx[fighter] : null;

        public int FighterOf(Combatant c) => c != null && _index.TryGetValue(c, out int i) ? i : -1;

        public int Heat(int fighter, int row) => Fx(fighter) != null && Fx(fighter).Heat.TryGetValue(row, out int h) ? h : 0;

        public int FlippedLeft(int fighter, int relay) =>
            Fx(fighter) != null && Fx(fighter).FlippedUntil.TryGetValue(relay, out int until) ? System.Math.Max(0, until - _tick) : 0;

        public int JamLeft(int fighter, int relay) => Fx(fighter) != null && Fx(fighter).Jam.TryGetValue(relay, out int left) ? left : 0;

        public bool IsHijacked(int fighter, int row) => Fx(fighter)?.Hijacked.Contains(row) ?? false;

        public int Firewall(int fighter) => Fx(fighter)?.Firewall ?? 0;

        /// <summary>Hat der Kämpfer gerade einen aktiven Hack (Flip, Jam, Hijack) auf seiner Platine?</summary>
        public bool IsHacked(int fighter)
        {
            FighterFx fx = Fx(fighter);
            if (fx == null) return false;
            if (fx.Hijacked.Count > 0) return true;
            foreach (KeyValuePair<int, int> j in fx.Jam) if (j.Value > 0) return true;
            foreach (KeyValuePair<int, int> f in fx.FlippedUntil) if (f.Value > _tick) return true;
            return false;
        }

        /// <summary>Gesperrte Relais (Flip/Jam) und gekaperte Komponenten eines Kämpfers, für Schilder unter dem Gegner.</summary>
        public IEnumerable<int> FlippedRelays(int fighter)
        {
            FighterFx fx = Fx(fighter);
            if (fx == null) yield break;
            foreach (KeyValuePair<int, int> f in fx.FlippedUntil) if (f.Value > _tick) yield return f.Key;
        }

        public IEnumerable<int> JammedRelays(int fighter)
        {
            FighterFx fx = Fx(fighter);
            if (fx == null) yield break;
            foreach (KeyValuePair<int, int> j in fx.Jam) if (j.Value > 0) yield return j.Key;
        }

        public IEnumerable<int> HijackedRows(int fighter)
        {
            FighterFx fx = Fx(fighter);
            if (fx == null) yield break;
            foreach (int row in fx.Hijacked) yield return row;
        }

        /// <summary>Rekursions-Tiefe der laufenden Ausführung einer Komponente (0 = keine).</summary>
        public int Depth(int fighter, int row)
        {
            FighterFx fx = Fx(fighter);
            return fx != null && fx.ActionRow == row ? fx.ActionDepth : 0;
        }

        /// <summary>Zusätzliche Wirkung der laufenden Ausführung in Prozent (Verstärker, Recursion).</summary>
        public int Power(int fighter, int row)
        {
            FighterFx fx = Fx(fighter);
            return fx != null && fx.ActionRow == row ? fx.ActionPower : 0;
        }

        /// <summary>Verstärkung eines Pulses des Spielers in Prozent (Verbindung und Start-Tick wie in <see cref="PulseView"/>).</summary>
        public int PulsePower(int link, int startTick) => _pulsePower.TryGetValue((link, startTick), out int p) ? p : 0;

        private void Apply(BattleEvent e)
        {
            int source = FighterOf(e.Source);
            int target = FighterOf(e.Target);
            FighterFx s = Fx(source);
            FighterFx t = Fx(target);

            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted:
                    if (s == null) break;
                    s.ActionRow = e.RowIndex;
                    s.ActionDepth = e.Depth;
                    s.ActionPower = e.Power;
                    return;

                case BattleEventKind.PulseSent:
                    if (e.Power > 0) _pulsePower[(e.Extra, e.Tick)] = e.Power;
                    return;

                case BattleEventKind.Overheat:
                    OverheatLevel = e.Amount;
                    OverheatTick = e.Tick;
                    return;

                case BattleEventKind.HeatChanged:
                    if (s != null) s.Heat[e.RowIndex] = e.Amount;
                    break;

                case BattleEventKind.HeatSkip:
                    _flashes.Add(new Flash(source, e.RowIndex, UiTexts.Effects.HeatSkip, CircuitEffectIds.Overclock));
                    break;

                case BattleEventKind.RecursionCall:
                    break;

                case BattleEventKind.RecursionLimit:
                    _flashes.Add(new Flash(source, e.RowIndex, UiTexts.Effects.StackLimit, CircuitEffectIds.Recursion));
                    break;

                case BattleEventKind.ParallelThread:
                    _flashes.Add(new Flash(source, e.RowIndex, UiTexts.Effects.Parallel, CircuitEffectIds.ParallelThread));
                    break;

                case BattleEventKind.QueueJump:
                    _flashes.Add(new Flash(source, e.RowIndex, UiTexts.Effects.QueueJump, CircuitEffectIds.Interrupt));
                    break;

                case BattleEventKind.OverflowShock:
                    _flashes.Add(new Flash(source, e.RowIndex, UiTexts.Effects.Overflow, CircuitEffectIds.Overflow));
                    break;

                case BattleEventKind.Hacked:
                    if (t == null) break;
                    switch (e.Detail)
                    {
                        case CircuitEffectIds.BitFlip:
                            if (e.Extra >= 0) t.FlippedUntil[e.Extra] = e.Tick + System.Math.Max(1, e.Amount);
                            break;
                        case CircuitEffectIds.Jam:
                            if (e.Extra >= 0) t.Jam[e.Extra] = e.Amount;
                            break;
                        case CircuitEffectIds.Hijack:
                            if (e.Extra >= 0) t.Hijacked.Add(e.Extra);
                            break;
                    }
                    break;

                case BattleEventKind.HackFailed:
                    break;

                case BattleEventKind.HackBlocked:
                    if (t != null) t.Firewall = e.Amount;
                    break;

                case BattleEventKind.RelayJammed:
                    if (s != null && e.Extra >= 0) s.Jam[e.Extra] = e.Amount;
                    break;

                case BattleEventKind.FlipEnded:
                    if (s != null && e.Extra >= 0) s.FlippedUntil.Remove(e.Extra);
                    break;

                case BattleEventKind.HijackedExecution:
                    if (t != null) t.Hijacked.Remove(e.Extra);
                    _flashes.Add(new Flash(target, e.Extra, UiTexts.Effects.Hijacked, CircuitEffectIds.Hijack));
                    break;

                case BattleEventKind.ShortCircuit:
                    _flashes.Add(new Flash(source, e.Extra, UiTexts.Effects.ShortCircuit, CircuitEffectIds.ShortCircuit));
                    break;

                default:
                    return;
            }
            Log(e);
        }

        /// <summary>Protokollzeile wie in <see cref="BattlePlayback"/>: Kategorie «Mine» für Ereignisse des Spielers.</summary>
        private void Log(BattleEvent e)
        {
            bool mine = e.Source != null && e.Source.Side == Side.Player;
            _entries.Add(new LogEntry(e.Tick, BattleLogText.Describe(e, _result), mine ? LogCategory.Mine : LogCategory.None, mine ? e.RowIndex : -1));
        }
    }
}
