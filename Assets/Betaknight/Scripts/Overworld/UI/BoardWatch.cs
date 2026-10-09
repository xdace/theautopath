using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Live-Zustand der Platine eines beliebigen Kämpfers (Gegner-Platinen), gelesen aus demselben Kampfprotokoll wie
    /// <see cref="BattlePlayback"/>, das nur die Platine des Spielers verfolgt: Relais leuchten und zählen, Gatter an/aus,
    /// Sicherungen, Kondensator-Ladung, laufende Pulse (mit Verstärkung), Warteschlange, Freeze und die zuletzt ausgelöste
    /// Komponente. Gerechnet wird nichts.
    /// </summary>
    public sealed class BoardWatch
    {
        /// <summary>Ein Puls dieses Kämpfers: Verbindung, Start, Ankunft und Verstärkung in Prozent.</summary>
        public readonly struct Pulse
        {
            public readonly int Link;
            public readonly int StartTick;
            public readonly int ArriveTick;
            public readonly int Power;

            public Pulse(int link, int startTick, int arriveTick, int power)
            {
                Link = link;
                StartTick = startTick;
                ArriveTick = arriveTick;
                Power = power;
            }
        }

        private readonly BattleResult _result;
        private readonly Combatant _combatant;
        private readonly int[] _relayTicks;
        private readonly int[] _relayCounts;
        private readonly bool[] _relayOn;
        private readonly bool[] _fuseBlown;
        private readonly int[] _capacitorCharge;
        private readonly int[] _frozenUntil;
        private readonly HashSet<int> _queued = new HashSet<int>();
        private readonly List<Pulse> _pulses = new List<Pulse>();
        private readonly List<Pulse> _newPulses = new List<Pulse>();
        private int _next;

        public int Tick { get; private set; }

        /// <summary>Platine des Kämpfers in diesem Kampf (null ohne).</summary>
        public LogicBoard Board { get; }

        /// <summary>Zuletzt gestartete Komponente und wann (für das Leuchten in Echtzeit).</summary>
        public int LastRow { get; private set; } = -1;
        public int LastRowTick { get; private set; } = -1000;

        public BoardWatch(BattleResult result, int fighter)
        {
            _result = result;
            FighterInfo info = result != null && fighter >= 0 && fighter < result.Fighters.Count ? result.Fighters[fighter] : null;
            _combatant = info?.Combatant;
            Board = _combatant?.Board;
            int relays = Board?.Relays.Count ?? 0;
            _relayTicks = new int[relays];
            _relayCounts = new int[relays];
            _relayOn = new bool[relays];
            _fuseBlown = new bool[relays];
            for (int i = 0; i < relays; i++) _relayTicks[i] = -1000;
            _capacitorCharge = new int[Board?.Chips.Count ?? 0];
            _frozenUntil = new int[Board?.Rows.Count ?? 0];
        }

        public bool IsRelayLit(int relay) => relay >= 0 && relay < _relayTicks.Length && Tick - _relayTicks[relay] < BattlePlayback.RowHighlightTicks;
        public int RelayCount(int relay) => relay >= 0 && relay < _relayCounts.Length ? _relayCounts[relay] : 0;
        public bool IsRelayOn(int relay) => relay >= 0 && relay < _relayOn.Length && _relayOn[relay];
        public bool IsFuseBlown(int relay) => relay >= 0 && relay < _fuseBlown.Length && _fuseBlown[relay];
        public int CapacitorCharge(int chip) => chip >= 0 && chip < _capacitorCharge.Length ? _capacitorCharge[chip] : 0;
        public int FrozenLeft(int row) => row >= 0 && row < _frozenUntil.Length ? System.Math.Max(0, _frozenUntil[row] - Tick) : 0;
        public bool IsQueued(int row) => _queued.Contains(row);

        /// <summary>Pulse, die gerade unterwegs sind.</summary>
        public IReadOnlyList<Pulse> Pulses => _pulses;

        /// <summary>Holt die Pulse, die seit dem letzten Aufruf losgelaufen sind (für die Mindest-Sichtbarkeit in Echtzeit).</summary>
        public List<Pulse> TakeNewPulses()
        {
            var taken = new List<Pulse>(_newPulses);
            _newPulses.Clear();
            return taken;
        }

        /// <summary>Wendet alle Ereignisse bis <paramref name="tick"/> an.</summary>
        public void Advance(int tick)
        {
            Tick = tick;
            if (_result == null || _combatant == null) return;
            IReadOnlyList<BattleEvent> events = _result.Events;
            while (_next < events.Count && events[_next].Tick <= tick) Apply(events[_next++]);
            _pulses.RemoveAll(p => p.ArriveTick < tick);
        }

        private void Apply(BattleEvent e)
        {
            bool mine = e.Source == _combatant;
            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted:
                    if (!mine) break;
                    if (e.FromQueue) _queued.Remove(e.RowIndex);
                    LastRow = e.RowIndex;
                    LastRowTick = e.Tick;
                    break;

                case BattleEventKind.RelayTriggered:
                    if (!mine || e.Relay < 0 || e.Relay >= _relayTicks.Length) break;
                    _relayTicks[e.Relay] = e.Tick;
                    _relayCounts[e.Relay]++;
                    break;

                case BattleEventKind.RelayState:
                    if (mine && e.Extra >= 0 && e.Extra < _relayOn.Length) _relayOn[e.Extra] = e.Amount == 1;
                    break;

                case BattleEventKind.FuseBlown:
                    if (mine && e.Relay >= 0 && e.Relay < _fuseBlown.Length) _fuseBlown[e.Relay] = true;
                    break;

                case BattleEventKind.PulseSent:
                    if (!mine) break;
                    var pulse = new Pulse(e.Extra, e.Tick, e.Tick + e.Amount, e.Power);
                    _pulses.Add(pulse);
                    _newPulses.Add(pulse);
                    break;

                case BattleEventKind.CapacitorStored:
                    if (mine && e.Extra >= 0 && e.Extra < _capacitorCharge.Length) _capacitorCharge[e.Extra] = e.Amount;
                    break;

                case BattleEventKind.CapacitorReleased:
                    if (mine && e.Extra >= 0 && e.Extra < _capacitorCharge.Length) _capacitorCharge[e.Extra] = 0;
                    break;

                case BattleEventKind.RowQueued:
                    if (mine) _queued.Add(e.RowIndex);
                    break;

                case BattleEventKind.Frozen:
                    if (e.Target == _combatant && e.Extra >= 0 && e.Extra < _frozenUntil.Length)
                        _frozenUntil[e.Extra] = System.Math.Max(_frozenUntil[e.Extra], e.Tick + e.Amount);
                    break;

                case BattleEventKind.Death:
                    if (e.Target == _combatant) _queued.Clear();
                    break;

                case BattleEventKind.BattleEnd:
                    _queued.Clear();
                    break;
            }
        }
    }
}
