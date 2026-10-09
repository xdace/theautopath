using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Circuit;
using ChipKind = Betaknight.Core.Circuit.ChipKind;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Ein laufender Kampf in festen Ticks (20 pro Sekunde) auf Platinen (A-19). Es gibt keine Cooldowns. Ablauf pro Tick:
    /// 1. Zeit: Ereignisse des Vortags an Modifikatoren, Zustände, Überhitzung.
    /// 2. Laufende Aktionen weiterschalten, Wirkungen anwenden.
    /// 3. Relais prüfen: Ereignisse lösen bei jedem Ereignis aus, Zustände bei der steigenden Flanke; ausgelöste Relais
    ///    reihen ihre versorgten Komponenten ein (Ereignisse dieses Ticks sehen Bedingungen erst im nächsten).
    /// 4. Freie Kämpfer starten die wartende Komponente, die in Lesereihenfolge zuerst kommt; ohne Warteschlange füllt
    ///    der Basisangriff die Lücke. Die Cast-Zeit ist die einzige Schleifen-Regel.
    /// 5. Siegprüfung.
    /// </summary>
    public sealed class Battle
    {
        private readonly BattleSetup _setup;
        private readonly List<Combatant> _all = new List<Combatant>();
        private readonly List<Combatant> _enemies = new List<Combatant>();
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private List<BattleEvent> _pending = new List<BattleEvent>();
        private readonly Dictionary<Combatant, RowRuntime[]> _relays = new Dictionary<Combatant, RowRuntime[]>();

        // Wer gerade wirkt und aus welcher Komponente: Ereignisse dieses Kämpfers bekommen die Komponente angeheftet.
        private Combatant _actor;
        private int _actorRow = -1;

        // Wirkungsbonus (Prozent) der laufenden Ausführung aus dem Schwierigkeits-Bonus, nur für die Auswertung.
        private int _actorPower;

        private readonly QueueConfig _queue;

        // Pulse unterwegs und Kondensator-Ladung je Kämpfer (A-20).
        private readonly Dictionary<Combatant, List<PulseInFlight>> _pulses = new Dictionary<Combatant, List<PulseInFlight>>();
        private readonly Dictionary<Combatant, CapacitorState[]> _capacitors = new Dictionary<Combatant, CapacitorState[]>();

        // Eigene Effekte (A-21): Hitze, Hacks, Firewall, letzte Ausführung je Kämpfer.
        private readonly Dictionary<Combatant, EffectRuntime> _fx = new Dictionary<Combatant, EffectRuntime>();

        public int Tick { get; private set; }
        public Random Random { get; }
        public BattleContext Context { get; }
        public Combatant Player { get; }
        public IReadOnlyList<Combatant> Enemies => _enemies;
        public IReadOnlyList<Combatant> All => _all;
        public IReadOnlyList<BattleEvent> Events => _events;

        public int TimeLimitTicks => _setup.TimeLimitTicks;
        /// <summary>Thermal Throttling läuft (A-21): ab dem Zeitlimit heizen beide Platinen auf.</summary>
        public bool IsOverheated => OverheatLevel > 0;

        /// <summary>Stufe des Thermal Throttling (0 = noch nicht).</summary>
        public int OverheatLevel { get; private set; }

        private ThermalConfig Thermal => _setup.Thermal ?? ThermalConfig.Default;
        public int BonusGold { get; private set; }

        public Battle(BattleSetup setup)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            if (setup.Player == null) throw new ArgumentException("Player missing.");
            if (setup.Enemies == null || setup.Enemies.Count == 0) throw new ArgumentException("At least one enemy required.");

            Random = new Random(setup.Seed);
            Context = setup.Context ?? new BattleContext();
            _queue = setup.Queue ?? QueueConfig.Default;

            Player = Add(setup.Player, Side.Player);
            foreach (CombatantSetup enemy in setup.Enemies) _enemies.Add(Add(enemy, Side.Enemy));
        }

        private Combatant Add(CombatantSetup s, Side side)
        {
            var c = new Combatant(s, side, _all.Count) { Battle = this };
            _all.Add(c);

            var relays = new RowRuntime[c.Board.Relays.Count];
            for (int i = 0; i < relays.Length; i++) relays[i] = new RowRuntime(i) { Powered = c.Board.Relays[i].Powered };
            _relays.Add(c, relays);
            _pulses.Add(c, new List<PulseInFlight>());
            var caps = new CapacitorState[c.Board.Chips.Count];
            for (int i = 0; i < caps.Length; i++) caps[i] = new CapacitorState();
            _capacitors.Add(c, caps);
            CircuitEffectConfig fx = c.Board.EffectConfig;
            _fx.Add(c, new EffectRuntime(c.Board.Rows.Count, relays.Length)
            {
                Firewall = c.Board.BoardEffectCount(CircuitEffectIds.Firewall) * Math.Max(0, fx.FirewallCharges),
            });
            return c;
        }

        /// <summary>Laufzeit-Daten eines Relais (letztes Auslösen, Zahl der Auslösungen).</summary>
        public RowRuntime RelayState(Combatant c, int relay) => _relays[c][relay];

        // ------------------------------------------------------------------ Ablauf

        public BattleResult Run()
        {
            var fighters = new List<FighterInfo>();
            foreach (Combatant c in _all) fighters.Add(new FighterInfo(c, c.MaxHp, c.Hp, c.ResourceSnapshot()));

            Emit(new BattleEvent(0, BattleEventKind.BattleStart, null, null));
            foreach (Combatant c in _all)
                foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnBattleStart(this, c);

            BattleOutcome? outcome = null;
            for (Tick = 1; Tick <= _setup.MaxTicks; Tick++)
            {
                UpdateTime();
                AdvanceActions();
                TriggerRelays();
                DeliverPulses();
                Decide();
                outcome = CheckEnd();
                if (outcome.HasValue) break;
            }

            if (!outcome.HasValue)
            {
                Tick = _setup.MaxTicks;
                outcome = BattleOutcome.Timeout;
            }

            // Die Warteschlange leert sich am Kampfende, Pulse unterwegs verfallen.
            foreach (Combatant c in _all)
            {
                c.QueueList.Clear();
                _pulses[c].Clear();
            }
            Emit(new BattleEvent(Tick, BattleEventKind.BattleEnd, null, null, (int)outcome.Value));
            foreach (Combatant c in _all)
                foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnBattleEnd(this, c, outcome.Value);

            int defeated = 0;
            foreach (Combatant e in _enemies) if (!e.IsAlive) defeated++;

            var labels = new List<string>();
            var skills = new List<string>();
            var difficulties = new List<int>();
            for (int i = 0; i <= Player.Board.Rows.Count; i++)
            {
                LogicRow row = Player.Board.RowAt(i);
                labels.Add(row.Label);
                skills.Add(row.Skill?.Name ?? "—");
                difficulties.Add(row.Difficulty);
            }

            return new BattleResult(outcome.Value, Tick, Math.Max(0, Player.Hp), Player.MaxHp, defeated, BonusGold, _events, labels,
                skills, fighters)
            {
                SurviveTicks = _setup.SurviveTicks, PlayerRowDifficulty = difficulties, PlayerBoard = Player.Board,
            };
        }

        private void UpdateTime()
        {
            // Ereignisse des letzten Ticks an passive Regeln. Neue Ereignisse daraus landen im nächsten Tick.
            List<BattleEvent> dispatch = _pending;
            _pending = new List<BattleEvent>();
            foreach (BattleEvent e in dispatch)
                foreach (Combatant c in _all)
                    foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnEvent(this, c, e);

            foreach (Combatant c in _all)
            {
                if (!c.IsAlive) continue;
                for (int i = c.StatusList.Count - 1; i >= 0; i--)
                {
                    if (i >= c.StatusList.Count) continue;
                    StatusEffect s = c.StatusList[i];
                    BeginActor(s.Source, s.SourceRow, s.SourcePowerPercent);
                    s.OnTick(this, c);
                    EndActor();
                    s.TicksLeft--;
                    if (s.TicksLeft <= 0 && c.StatusList.Remove(s))
                        Emit(new BattleEvent(Tick, BattleEventKind.StatusExpired, s.Source, c, 0, s.Id));
                }
            }

            // Thermal Throttling (A-21): ab dem Zeitlimit jede Stufe längere Cast-Zeiten und mehr Schaden für beide Seiten.
            int start = Math.Max(1, _setup.TimeLimitTicks);
            if (Tick >= start && (Tick - start) % Math.Max(1, Thermal.StepTicks) == 0)
            {
                OverheatLevel++;
                Emit(new BattleEvent(Tick, BattleEventKind.Overheat, null, null, OverheatLevel));
            }

            // Bit Flip endet.
            foreach (Combatant c in _all)
            {
                int[] flipped = _fx[c].FlippedUntil;
                for (int i = 0; i < flipped.Length; i++)
                    if (flipped[i] == Tick) Emit(new BattleEvent(Tick, BattleEventKind.FlipEnded, c, null, 0, c.Board.Relays[i].Label) { Extra = i, Relay = i });
            }
        }

        /// <summary>Cast-Zeit-Aufschlag des Thermal Throttling in Prozent.</summary>
        public int ThermalCastPercent => OverheatLevel * Thermal.CastPercentPerStep;

        /// <summary>
        /// Schadens-Aufschlag des Thermal Throttling in Prozent. Die Stufen bauen aufeinander auf (+15 % auf den Wert der
        /// Stufe davor), so endet auch ein Patt mit hoher Rüstung.
        /// </summary>
        public int ThermalDamagePercent
        {
            get
            {
                long factor = 100;
                for (int i = 0; i < OverheatLevel && factor < 10_000_000; i++) factor = factor * (100 + Thermal.DamagePercentPerStep) / 100;
                return (int)(factor - 100);
            }
        }

        private void AdvanceActions()
        {
            foreach (Combatant c in _all)
            {
                ActionState a = c.Action;
                if (a != null && c.IsAlive && !c.IsStunned)
                {
                    if (a.InWindup)
                    {
                        a.WindupLeft--;
                        if (a.WindupLeft <= 0)
                        {
                            Execute(c, a);
                            if (c.Action == a) // sonst durch Tod oder Betäubung abgebrochen
                            {
                                a.EffectApplied = true;
                                AfterExecution(c, a);
                                if (a.RecoveryLeft <= 0) Finish(c, a);
                            }
                        }
                    }
                    else
                    {
                        a.RecoveryLeft--;
                        if (a.RecoveryLeft <= 0) Finish(c, a);
                    }
                }
                AdvanceThreads(c);
            }
        }

        /// <summary>Parallel Threads (A-21) laufen neben der Hauptaktion mit eigener Cast-Zeit und Erholung.</summary>
        private void AdvanceThreads(Combatant c)
        {
            if (c.ThreadList.Count == 0) return;
            if (!c.IsAlive || c.IsStunned) return;
            foreach (ActionState a in c.ThreadList.ToArray())
            {
                if (!c.ThreadList.Contains(a)) continue;
                if (a.InWindup)
                {
                    if (--a.WindupLeft > 0) continue;
                    Execute(c, a);
                    if (!c.ThreadList.Contains(a)) continue;
                    a.EffectApplied = true;
                    AfterExecution(c, a);
                    if (a.RecoveryLeft <= 0) c.ThreadList.Remove(a);
                }
                else if (--a.RecoveryLeft <= 0)
                {
                    c.ThreadList.Remove(a);
                }
            }
        }

        private void Execute(Combatant c, ActionState a)
        {
            if (!a.Skill.IsBasicAttack) _fx[c].LastComponentTick = Tick;
            if (a.HijackedBy != null && a.HijackedBy.IsAlive)
            {
                ExecuteHijacked(c, a);
                return;
            }
            Combatant target = a.Target != null && a.Target.IsAlive ? a.Target : DefaultTarget(c);
            Emit(new BattleEvent(Tick, BattleEventKind.ActionExecuted, c, target, a.Skill.CountsAsAttack ? 1 : 0, a.Skill.Id, a.RowIndex)
                { Cause = a.Cause, CauseRow = a.CauseRow, Relay = a.Relay });

            if (!a.Skill.IsBasicAttack && a.Skill.CanBeRepeated) c.LastRepeatableSkill = a.Skill;

            var context = new SkillContext(this, c, target, a.Skill, a.RowIndex);
            BeginActor(c, a.RowIndex, a.Skill.Difficulty.PowerPercent);
            foreach (ISkillEffect effect in a.Skill.Effects)
            {
                if (!c.IsAlive) break;
                effect.Apply(context);
            }

            // Modul «Chain»: zielgerichtete Wirkungen treffen weitere Gegner.
            if (a.Skill.ExtraTargets > 0 && target != null)
            {
                int extra = 0;
                foreach (Combatant other in OpponentsOf(c))
                {
                    if (extra >= a.Skill.ExtraTargets || !c.IsAlive) break;
                    if (other == target) continue;
                    extra++;
                    var chained = new SkillContext(this, c, other, a.Skill, a.RowIndex);
                    foreach (ISkillEffect effect in a.Skill.Effects)
                        if (c.IsAlive && SkillEffects.HitsTarget(effect)) effect.Apply(chained);
                }
            }

            // Hacks (A-21): jede Ausführung eines Trägers hackt die gegnerische Platine.
            foreach (string id in a.Skill.CircuitEffects)
                if (c.IsAlive && IsHack(id)) Hack(c, target, id, a.RowIndex);
            EndActor();
        }

        /// <summary>
        /// Nach der Wirkung: Wiederholungen aus «Multicast» vormerken, Auslöser-Module der Komponente feuern und
        /// «Repeat while true» prüfen (das Relais gilt noch: die Komponente kommt erneut in die Warteschlange).
        /// </summary>
        private void AfterExecution(Combatant c, ActionState a)
        {
            if (!c.IsAlive || a.HijackedBy != null) return;
            if (a.RepeatsLeft > 0 && !a.IsThread)
            {
                c.Pending.Insert(0, new PendingAction
                {
                    Skill = a.Skill, Target = a.Target, Row = a.RowIndex, Cause = ActionCause.Repeat, RepeatsLeft = a.RepeatsLeft - 1,
                    BonusTier = a.BonusTier, Relay = a.Relay,
                });
            }
            if (a.IsRepeat || a.RowIndex < 0 || a.RowIndex >= c.Board.Rows.Count) return;
            RequeueFromCharge(c, a);

            LogicRelay relay = a.Relay >= 0 && a.Relay < c.Board.Relays.Count ? c.Board.Relays[a.Relay] : null;
            LogicRow row = c.Board.Rows[a.RowIndex];
            CircuitEffectConfig fx = c.Board.EffectConfig;
            // Der Bonus und die Grenze wandern mit: ausgelöste Ziele laufen mit Stufe und Grenze dieser Ausführung.
            FireEdges(c, GraphNode.Skill(a.RowIndex), a.BonusTier, a.Relay, relay?.MaxCells ?? int.MaxValue);
            // A-20: die Komponente schickt Pulse über ihre Verbindungen; Ziele zählen als versorgt vom ursprünglichen Relais.
            // A-21: ein Verstärker als eigener Effekt der Komponente verstärkt ihre Pulse.
            int pulsePower = row.Has(CircuitEffectIds.Amplifier) ? fx.AmplifierPowerPercent : 0;
            SendPulses(c, PulseNode.Component(a.RowIndex), a.RowIndex, a.Relay, a.BonusTier, relay?.MaxCells ?? int.MaxValue, pulsePower);

            // Overclock (A-21): berührte Komponenten bekommen Hitze.
            if (row.Has(CircuitEffectIds.Overclock))
                foreach (int n in row.Neighbours) AddHeat(c, n, fx.OverclockHeatPerExecution, a.RowIndex);

            bool holds = relay != null && relay.Gate == null && relay.Condition.IsMet(new ConditionContext(this, c, _relays[c][a.Relay]), out _);
            // Recursion (A-21): gilt die Bedingung noch, ruft sich die Komponente erneut auf, eine Tiefe weiter.
            if (row.Has(CircuitEffectIds.Recursion) && holds)
            {
                int depth = a.Depth + 1;
                if (depth > Math.Max(0, fx.RecursionMaxDepth))
                {
                    Emit(new BattleEvent(Tick, BattleEventKind.RecursionLimit, c, null, a.Depth, row.Skill.Id, a.RowIndex) { Relay = a.Relay });
                }
                else
                {
                    Emit(new BattleEvent(Tick, BattleEventKind.RecursionCall, c, null, depth, row.Skill.Id, a.RowIndex) { Relay = a.Relay, Depth = depth });
                    Enqueue(c, a.RowIndex, a.Relay, ActionCause.Recursion, a.RowIndex, a.BonusTier, relay.MaxCells, null,
                        depth * fx.RecursionPowerPercentPerDepth, depth);
                }
                return;
            }

            if (relay != null && relay.RepeatWhileTrue && relay.Condition.IsMet(new ConditionContext(this, c, _relays[c][a.Relay]), out Combatant target))
                Enqueue(c, a.RowIndex, a.Relay, ActionCause.Board, -1, a.BonusTier, relay.MaxCells, target);
        }

        private void BeginActor(Combatant c, int row, int powerPercent = 0)
        {
            _actor = c;
            _actorRow = row;
            _actorPower = powerPercent;
        }

        private void EndActor()
        {
            _actor = null;
            _actorRow = -1;
            _actorPower = 0;
        }

        /// <summary>Anteil des Wirkungsbonus an einer Menge: bei +50 % ist es ein Drittel des Ergebnisses.</summary>
        private static int BonusPart(int amount, int powerPercent) =>
            powerPercent <= 0 || amount <= 0 ? 0 : amount - (int)((long)amount * 100 / (100 + powerPercent));

        private void Finish(Combatant c, ActionState a)
        {
            c.Action = null;
            c.LastActionRow = a.RowIndex;
            c.LastActionEndTick = Tick;
            StartPending(c);
        }

        /// <summary>Höchstens so viele Wiederholungen warten hinter der laufenden Aktion.</summary>
        public const int MaxPendingActions = 4;

        /// <summary>Startet die nächste vorgemerkte Wiederholung.</summary>
        private void StartPending(Combatant c)
        {
            while (c.Pending.Count > 0 && c.Action == null)
            {
                PendingAction p = c.Pending[0];
                c.Pending.RemoveAt(0);
                if (!c.IsAlive || c.IsStunned || OpponentsOf(c).Count == 0)
                {
                    c.Pending.Clear();
                    return;
                }
                StartAction(c, p.Skill, p.Target, p.Row, p.Cause, p.CauseRow, p.RepeatsLeft, p.BonusTier, -1, p.Relay);
            }
        }

        /// <summary>
        /// Merkt eine Wiederholung für die laufende Aktion vor (Echo). Sie startet nach deren Erholung als eigene Ausführung
        /// mit voller Cast-Zeit. Keine Ausführung ohne Cast.
        /// </summary>
        public void QueueRepeat(Combatant c, SkillDefinition skill)
        {
            if (c?.Action == null || skill == null || c.Pending.Count >= MaxPendingActions) return;
            c.Pending.Add(new PendingAction
            {
                Skill = skill, Target = c.Action.Target, Row = c.Action.RowIndex, Cause = ActionCause.Repeat, BonusTier = c.Action.BonusTier,
                Relay = c.Action.Relay,
            });
        }

        // ------------------------------------------------------------------ Relais und Auslöser

        /// <summary>
        /// Jeden Tick: Relais beobachten und auslösen. Ein ausgelöstes Relais reiht alle Komponenten ein, die es versorgt;
        /// berührte, aber zu grosse Komponenten zählen als «Missed Trigger». Auslöser-Module am Relais feuern mit.
        /// </summary>
        private void TriggerRelays()
        {
            foreach (Combatant c in _all)
            {
                if (!c.IsAlive || OpponentsOf(c).Count == 0) continue;
                RowRuntime[] states = _relays[c];
                int hold = c.Board.ChipConfig.GateHoldTicks;

                // Erst die Relais mit eigener Bedingung, dann die Gatter (A-20), die deren Zustand lesen.
                for (int i = 0; i < states.Length; i++)
                {
                    LogicRelay relay = c.Board.Relays[i];
                    states[i].TriggeredNow = false;
                    if (relay.Gate != null) continue;
                    var context = new ConditionContext(this, c, states[i]);
                    if (relay.Condition is IObservingCondition observing) observing.Observe(context);
                    bool flipped = _fx[c].FlippedUntil[i] > Tick;
                    Combatant target;
                    bool fired;
                    if (flipped)
                    {
                        // Bit Flip (A-21): die Bedingung gilt umgekehrt, mit steigender Flanke.
                        bool met = !relay.Condition.IsMet(context, out target);
                        fired = met && !states[i].WasMet;
                        states[i].WasMet = met;
                    }
                    else
                    {
                        fired = Triggers(relay.Condition, context, states[i], out target);
                        // Pulsierende Zustands-Runen: solange der Zustand gilt, erneut nach jedem Takt.
                        if (!fired && relay.PulseTicks > 0 && states[i].FireCount > 0 && Tick - states[i].LastFiredTick >= relay.PulseTicks)
                            fired = relay.Condition.IsMet(context, out target);
                    }
                    if (fired) Fire(c, i, target);
                    bool active = relay.IsEventTrigger && !flipped ? states[i].FireCount > 0 && Tick - states[i].LastFiredTick < hold : states[i].WasMet;
                    SetActive(c, i, active);
                }
                for (int i = 0; i < states.Length; i++)
                {
                    LogicRelay gate = c.Board.Relays[i];
                    if (gate.Gate == null || !gate.IsGateReady) continue;
                    if (GateTriggers(c, gate, states, out bool level)) Fire(c, i, null);
                    SetActive(c, i, level);
                }
                ReleaseCapacitorsOnTimeout(c);
            }
        }

        /// <summary>Ein Relais oder Gatter löst aus: versorgte Komponenten einreihen, zu grosse verpassen, Auslöser und Kondensatoren.</summary>
        private void Fire(Combatant c, int i, Combatant target)
        {
            RowRuntime state = _relays[c][i];
            LogicRelay relay = c.Board.Relays[i];
            // Jam (A-21): das Relais ignoriert dieses Auslösen. Es gilt trotzdem als verbraucht (Takt und Zähler beginnen neu).
            int[] jam = _fx[c].JamLeft;
            if (jam[i] > 0)
            {
                jam[i]--;
                state.LastFiredTick = Tick;
                Emit(new BattleEvent(Tick, BattleEventKind.RelayJammed, c, null, jam[i], relay.Label) { Extra = i, Relay = i });
                return;
            }
            state.LastFiredTick = Tick;
            state.FireCount++;
            state.TriggeredNow = true;
            Emit(new BattleEvent(Tick, BattleEventKind.RelayTriggered, c, target, i, relay.Label) { Extra = relay.Powered.Count, Relay = i });
            if (relay.OncePerFight) Emit(new BattleEvent(Tick, BattleEventKind.FuseBlown, c, null, i, relay.Label) { Extra = relay.ChipIndex, Relay = i });
            PowerFromRelay(c, i, relay, target);
            FireEdges(c, GraphNode.Block(i), relay.Difficulty, i, relay.MaxCells);
            foreach (LogicChip chip in c.Board.Chips)
                if (chip.Kind == ChipKind.Capacitor && chip.ReleaseList.Contains(i)) ReleaseCapacitor(c, chip.Index);
        }

        private void SetActive(Combatant c, int i, bool active)
        {
            RowRuntime state = _relays[c][i];
            if (state.Active == active) return;
            state.Active = active;
            Emit(new BattleEvent(Tick, BattleEventKind.RelayState, c, null, active ? 1 : 0, c.Board.Relays[i].Label) { Extra = i, Relay = i });
        }

        /// <summary>
        /// Gatter (A-20): UND löst bei der steigenden Flanke von «beide an» aus, ODER bei jedem Auslösen eines Eingangs,
        /// NICHT bei der steigenden Flanke von «Eingang aus», die Sicherung beim ersten Auslösen ihres Eingangs.
        /// </summary>
        private bool GateTriggers(Combatant c, LogicRelay gate, RowRuntime[] states, out bool level)
        {
            RowRuntime own = states[gate.Index];
            bool rising;
            switch (gate.Gate.Value)
            {
                case ChipKind.And:
                    level = true;
                    foreach (int input in gate.Inputs) level &= states[input].Active;
                    rising = level && !own.WasMet;
                    own.WasMet = level;
                    return rising;
                case ChipKind.Or:
                    level = false;
                    bool any = false;
                    foreach (int input in gate.Inputs)
                    {
                        level |= states[input].Active;
                        any |= states[input].TriggeredNow;
                    }
                    return any;
                case ChipKind.Not:
                    level = !states[gate.Inputs[0]].Active;
                    rising = level && !own.WasMet;
                    own.WasMet = level;
                    return rising;
                case ChipKind.Fuse:
                    level = own.FireCount == 0;
                    return own.FireCount == 0 && states[gate.Inputs[0]].TriggeredNow;
                case ChipKind.Watchdog:
                    // Watchdog (A-21): hat seit der letzten Ausführung (oder seinem letzten Auslösen) nichts gefeuert?
                    int since = Math.Max(_fx[c].LastComponentTick, own.FireCount > 0 ? own.LastFiredTick : 0);
                    level = Tick - since >= Math.Max(1, c.Board.EffectConfig.WatchdogIdleTicks);
                    return level;
                default:
                    level = false;
                    return false;
            }
        }

        // ------------------------------------------------------------------ Pulse (A-20)

        /// <summary>Schickt einen Puls über jede Verbindung des Knotens; er kommt nach der Laufzeit der Verbindung an.</summary>
        /// <param name="power">Zusätzliche Wirkung, die der Puls schon trägt (A-21); Verstärker auf dem Weg legen dazu.</param>
        private void SendPulses(Combatant c, PulseNode from, int sourceRow, int relay, int tier, int maxCells, int power = 0)
        {
            IReadOnlyList<PulseLink> links = c.Board.Links;
            for (int i = 0; i < links.Count; i++)
            {
                PulseLink link = links[i];
                if (!link.From.Equals(from)) continue;
                int carried = power + link.Amplifiers * c.Board.EffectConfig.AmplifierPowerPercent;
                _pulses[c].Add(new PulseInFlight
                {
                    Link = i, ArriveTick = Tick + link.Delay, SourceRow = sourceRow, Relay = relay, Tier = tier, MaxCells = maxCells, Power = carried,
                });
                Emit(new BattleEvent(Tick, BattleEventKind.PulseSent, c, null, link.Delay, null, sourceRow)
                    { Extra = i, Relay = relay, Tier = tier, Power = carried });
            }
        }

        /// <summary>Angekommene Pulse reihen ihr Ziel ein oder laden einen Kondensator.</summary>
        private void DeliverPulses()
        {
            foreach (Combatant c in _all)
            {
                List<PulseInFlight> flying = _pulses[c];
                if (flying.Count == 0) continue;
                if (!c.IsAlive || OpponentsOf(c).Count == 0)
                {
                    flying.Clear();
                    continue;
                }
                var arrived = new List<PulseInFlight>();
                for (int i = flying.Count - 1; i >= 0; i--)
                {
                    if (flying[i].ArriveTick > Tick) continue;
                    arrived.Add(flying[i]);
                    flying.RemoveAt(i);
                }
                arrived.Reverse();
                foreach (PulseInFlight p in arrived)
                {
                    PulseLink link = c.Board.Links[p.Link];
                    if (link.To.IsCapacitor) Store(c, link.To.Index, p);
                    else Enqueue(c, link.To.Index, p.Relay, ActionCause.Pulse, p.SourceRow, p.Tier, p.MaxCells, null, p.Power);
                }
            }
        }

        private void Store(Combatant c, int chip, PulseInFlight pulse)
        {
            CapacitorState cap = _capacitors[c][chip];
            int capacity = Math.Max(1, c.Board.ChipConfig.CapacitorCapacity);
            if (cap.Stored.Count >= capacity)
            {
                Emit(new BattleEvent(Tick, BattleEventKind.PulseLost, c, null, cap.Stored.Count) { Extra = chip, Relay = pulse.Relay });
                return;
            }
            if (cap.Stored.Count == 0) cap.FirstStoredTick = Tick;
            cap.Stored.Add(pulse);
            Emit(new BattleEvent(Tick, BattleEventKind.CapacitorStored, c, null, cap.Stored.Count) { Extra = chip, Relay = pulse.Relay });
        }

        private void ReleaseCapacitorsOnTimeout(Combatant c)
        {
            CapacitorState[] caps = _capacitors[c];
            int after = Math.Max(1, c.Board.ChipConfig.CapacitorReleaseTicks);
            for (int i = 0; i < caps.Length; i++)
                if (caps[i].Stored.Count > 0 && Tick - caps[i].FirstStoredTick >= after) ReleaseCapacitor(c, i);
        }

        /// <summary>Der Kondensator gibt alle gespeicherten Pulse ab, jeder mit Grenze und Bonus seines ursprünglichen Relais.</summary>
        private void ReleaseCapacitor(Combatant c, int chip)
        {
            CapacitorState cap = _capacitors[c][chip];
            if (cap.Stored.Count == 0) return;
            var stored = new List<PulseInFlight>(cap.Stored);
            cap.Stored.Clear();
            Emit(new BattleEvent(Tick, BattleEventKind.CapacitorReleased, c, null, stored.Count) { Extra = chip });
            foreach (PulseInFlight p in stored) SendPulses(c, PulseNode.Capacitor(chip), p.SourceRow, p.Relay, p.Tier, p.MaxCells, p.Power);
        }

        /// <summary>Gespeicherte Pulse eines Kondensators (Anzeige, Tests).</summary>
        public int CapacitorCharge(Combatant c, int chip) => _capacitors[c][chip].Stored.Count;

        /// <summary>Pulse, die gerade unterwegs sind.</summary>
        public int PulsesInFlight(Combatant c) => _pulses[c].Count;

        private sealed class PulseInFlight
        {
            public int Link;
            public int ArriveTick;
            public int SourceRow;
            public int Relay;
            public int Tier;
            public int MaxCells;
            public int Power;
        }

        /// <summary>Laufzeit der eigenen Effekte (A-21) eines Kämpfers.</summary>
        private sealed class EffectRuntime
        {
            public readonly int[] Heat;
            public readonly int[] FlippedUntil;
            public readonly int[] JamLeft;
            public readonly Dictionary<int, Combatant> Hijacked = new Dictionary<int, Combatant>();

            // Ladung je Komponente: alle Relais, Charge Links und Spillover füllen denselben Speicher.
            public readonly Dictionary<int, int> Charge = new Dictionary<int, int>();
            public int Firewall;
            public int LastComponentTick;

            public EffectRuntime(int rows, int relays)
            {
                Heat = new int[rows];
                FlippedUntil = new int[relays];
                JamLeft = new int[relays];
            }
        }

        private sealed class CapacitorState
        {
            public readonly List<PulseInFlight> Stored = new List<PulseInFlight>();
            public int FirstStoredTick;
        }

        /// <summary>
        /// Löst die Bedingung jetzt aus? Ereignisse (Ereignis, Zähler, Clock) bei jedem Erfüllen, Zustände nur bei der
        /// steigenden Flanke. Bei ODER zählt jedes Teil für sich.
        /// </summary>
        private static bool Triggers(ICondition condition, in ConditionContext context, RowRuntime state, out Combatant target)
        {
            if (condition is AnyCondition any)
            {
                if (state.PartWasMet == null || state.PartWasMet.Length != any.Parts.Count) state.PartWasMet = new bool[any.Parts.Count];
                bool fired = false;
                target = null;
                for (int p = 0; p < any.Parts.Count; p++)
                {
                    bool was = state.PartWasMet[p];
                    if (PartTriggers(any.Parts[p], context, ref was, out Combatant t) && !fired)
                    {
                        fired = true;
                        target = t;
                    }
                    state.PartWasMet[p] = was;
                }
                return fired;
            }

            bool wasMet = state.WasMet;
            bool result = PartTriggers(condition, context, ref wasMet, out target);
            state.WasMet = wasMet;
            return result;
        }

        private static bool PartTriggers(ICondition condition, in ConditionContext context, ref bool wasMet, out Combatant target)
        {
            bool met = condition.IsMet(context, out target);
            bool rising = met && !wasMet;
            wasMet = met;
            return TriggerKinds.IsEvent(condition) ? met : rising;
        }

        /// <summary>Feuert alle Auslöser-Kanten eines Knotens mit Stufe und Grenze der auslösenden Ausführung.</summary>
        private void FireEdges(Combatant c, GraphNode from, int sourceTier, int sourceRelay, int maxCells)
        {
            foreach (GraphEdge edge in c.Board.Graph.From(from))
            {
                int causeRow = from.Kind == GraphNodeKind.Skill ? from.Row : -1;
                if (edge.Kind == GraphEdgeKind.Trigger)
                {
                    Enqueue(c, edge.To.Row, sourceRelay, ActionCause.Trigger, causeRow, sourceTier, maxCells, null);
                }
                else if (edge.Kind == GraphEdgeKind.Charge)
                {
                    // Charge Link: lädt das Ziel um die Grösse der Quelle (Komponente) bzw. die Grenze des Relais auf.
                    int row = edge.To.Row;
                    if (row < 0 || row >= c.Board.Rows.Count) continue;
                    int amount = from.Kind == GraphNodeKind.Skill && from.Row >= 0 && from.Row < c.Board.Rows.Count ? c.Board.Rows[from.Row].Cells
                        : from.Kind == GraphNodeKind.Block && from.Row >= 0 && from.Row < c.Board.Relays.Count ? c.Board.Relays[from.Row].MaxCells : 0;
                    int stored = amount > 0 ? AddCharge(c, row, amount) : 0;
                    if (amount > 0 && stored >= c.Board.Rows[row].Cells)
                        Enqueue(c, row, sourceRelay, ActionCause.Trigger, causeRow, sourceTier, int.MaxValue, null, chargeCost: c.Board.Rows[row].Cells);
                    else if (amount > 0)
                        Charged(c, row, sourceRelay, stored, amount);
                    else
                        Missed(c, row, sourceRelay, MissReason.TooLarge);
                }
            }
        }

        // ------------------------------------------------------------------ Warteschlange

        private void Missed(Combatant c, int row, int relay, MissReason reason)
        {
            LogicRow r = row >= 0 && row < c.Board.Rows.Count ? c.Board.Rows[row] : null;
            Emit(new BattleEvent(Tick, BattleEventKind.TriggerMissed, c, null, (int)reason, r?.Skill?.Id, row) { Relay = relay });
        }

        /// <summary>
        /// Reiht eine Komponente ein. Steht sie schon so oft wie erlaubt, ist das ein «Missed Trigger»; ein Auslösen mit
        /// höherer Stufe hebt nur den Bonus der wartenden Ausführung (nicht stapelnd).
        /// </summary>
        /// <param name="chargeCost">Ladung, die beim Einreihen verbraucht wird. Kommt die Komponente nicht in die Warteschlange
        /// (schon eingereiht, eingefroren), bleibt die Ladung gespeichert.</param>
        private void Enqueue(Combatant c, int row, int relay, ActionCause cause, int causeRow, int tier, int maxCells, Combatant target,
            int power = 0, int depth = 0, int chargeCost = 0)
        {
            if (row < 0 || row >= c.Board.Rows.Count) return;
            LogicRow r = c.Board.Rows[row];
            if (r.IsOrphaned)
            {
                Missed(c, row, relay, MissReason.Orphaned);
                return;
            }
            if (r.Cells > maxCells)
            {
                // Zu gross für die Quelle: sie lädt um ihre Grenze auf, läuft erst mit voller Ladung.
                int stored = AddCharge(c, row, maxCells);
                if (stored < r.Cells)
                {
                    Charged(c, row, relay, stored, maxCells);
                    return;
                }
                chargeCost = r.Cells;
            }
            if (c.IsFrozen(row, Tick))
            {
                Missed(c, row, relay, MissReason.Frozen);
                return;
            }

            CircuitEffectConfig fx = c.Board.EffectConfig;

            // Parallel Thread (A-21): läuft gerade eine andere Ausführung, startet die Komponente sofort daneben.
            if (r.Has(CircuitEffectIds.ParallelThread) && c.Action != null && !c.Action.Skill.IsBasicAttack && c.Action.RowIndex != row
                && !c.ThreadList.Exists(a => a.RowIndex == row) && !c.IsStunned)
            {
                Emit(new BattleEvent(Tick, BattleEventKind.ParallelThread, c, target, 0, r.Skill.Id, row) { Cause = cause, CauseRow = causeRow, Relay = relay });
                StartAction(c, r.Skill, target, row, cause, causeRow, null, tier, -1, relay, power, depth, thread: true);
                SpendCharge(c, row, chargeCost);
                return;
            }

            QueuedRow existing = null;
            int entries = 0;
            foreach (QueuedRow q in c.QueueList)
            {
                if (q.Row != row) continue;
                entries++;
                existing = existing ?? q;
            }
            int allowed = r.Has(CircuitEffectIds.Buffer) ? Math.Max(1, fx.BufferEntries) : Math.Max(1, _queue.MaxEntriesPerComponent);
            if (entries >= allowed)
            {
                if (tier > existing.BonusTier)
                {
                    existing.BonusTier = tier;
                    existing.Relay = relay;
                }
                existing.PowerPercent = Math.Max(existing.PowerPercent, power);
                // Mit Ladung: sie bleibt gespeichert und reiht die Komponente nach ihrer Ausführung erneut ein.
                if (chargeCost > 0) Charged(c, row, relay, _fx[c].Charge.TryGetValue(row, out int banked) ? banked : 0, 0);
                else Missed(c, row, relay, MissReason.AlreadyQueued);
                return;
            }

            // Overflow (A-21): ist die Warteschlange voll, wird der Eintrag sofort zum Schock gegen alle Gegner.
            if (c.Board.HasBoardEffect(CircuitEffectIds.Overflow) && c.QueueList.Count >= Math.Max(1, fx.OverflowQueueLimit))
            {
                Overflow(c, row, relay);
                SpendCharge(c, row, chargeCost);
                return;
            }

            c.QueueList.Add(new QueuedRow
            {
                Row = row, SinceTick = Tick, Cause = cause, CauseRow = causeRow, Relay = relay, BonusTier = DifficultyBonusConfig.Clamp(tier),
                Target = target, PowerPercent = power, Depth = depth,
            });
            SpendCharge(c, row, chargeCost);
            Emit(new BattleEvent(Tick, BattleEventKind.RowQueued, c, target, 0, r.Skill.Id, row)
                { Cause = cause, CauseRow = causeRow, Tier = tier, Relay = relay, Power = power, Depth = depth });
            if (r.Has(CircuitEffectIds.Interrupt) && c.QueueList.Count > 1)
                Emit(new BattleEvent(Tick, BattleEventKind.QueueJump, c, null, c.QueueList.Count - 1, r.Skill.Id, row) { Relay = relay });
        }

        /// <summary>
        /// Ein Relais löst aus: seine Grenze ist ein Topf Ladung für alle berührten Komponenten (versorgte und zu grosse).
        /// Was am wenigsten braucht, wird zuerst gefüllt und läuft; bei Gleichstand, der nicht für alle reicht, entscheidet
        /// der Zufall. Reicht der Topf nicht für alle, wird der Rest gleichmässig auf die übrigen verteilt (sie laden auf).
        /// Reicht er für alle, ist der Rest Überladung: +<see cref="CircuitEffectConfig.OverchargePowerPercentPerCell"/> %
        /// Wirkung je Feld, gleichmässig auf die laufenden verteilt (Spillover gibt sie stattdessen an die Nachbarn weiter).
        /// </summary>
        private void PowerFromRelay(Combatant c, int i, LogicRelay relay, Combatant target)
        {
            if (relay.MaxCells == int.MaxValue)
            {
                // Relais ohne Grenze (frei gebaute Tafeln, Tests): alle versorgten laufen, ohne Topf und ohne Überladung.
                foreach (int row in relay.Powered) Enqueue(c, row, i, ActionCause.Board, -1, relay.Difficulty, relay.MaxCells, target);
                return;
            }
            var rows = new List<int>();
            foreach (int row in relay.Powered) AddCandidate(c, rows, row, i);
            foreach (int row in relay.TooLarge) AddCandidate(c, rows, row, i);
            Distribute(c, rows, relay.MaxCells, i, ActionCause.Board, -1, relay.Difficulty, target, spill: true);
        }

        private void AddCandidate(Combatant c, List<int> rows, int row, int relay)
        {
            if (row < 0 || row >= c.Board.Rows.Count || rows.Contains(row)) return;
            if (c.Board.Rows[row].IsOrphaned) Missed(c, row, relay, MissReason.Orphaned);
            else rows.Add(row);
        }

        /// <summary>Verteilt <paramref name="pool"/> Ladung auf <paramref name="rows"/> (Regeln siehe <see cref="PowerFromRelay"/>).</summary>
        private void Distribute(Combatant c, List<int> rows, int pool, int relay, ActionCause cause, int causeRow, int tier, Combatant target, bool spill)
        {
            if (rows.Count == 0 || pool <= 0) return;
            Dictionary<int, int> charge = _fx[c].Charge;
            int Stored(int row) => charge.TryGetValue(row, out int v) ? v : 0;
            int Need(int row) => Math.Max(0, c.Board.Rows[row].Cells - Stored(row));

            var fired = new List<int>();
            var waiting = new List<int>();
            foreach (IGrouping<int, int> group in rows.OrderBy(Need).ThenBy(r => r).GroupBy(Need))
            {
                List<int> members = group.ToList();
                if (waiting.Count > 0)
                {
                    waiting.AddRange(members);
                    continue;
                }
                int need = group.Key;
                if (need == 0 || pool >= need * members.Count)
                {
                    fired.AddRange(members);
                    pool -= need * members.Count;
                    continue;
                }
                // Gleichstand, der nicht für alle reicht: so viele wie möglich, zufällig gewählt.
                int k = pool / need;
                for (int n = 0; n < k; n++)
                {
                    int pick = Random.Next(members.Count);
                    fired.Add(members[pick]);
                    members.RemoveAt(pick);
                }
                pool -= k * need;
                waiting.AddRange(members);
            }

            if (waiting.Count > 0)
            {
                // Rest gleichmässig auf die wartenden; was nicht aufgeht, zufällig je 1.
                int each = pool / waiting.Count;
                var gain = new Dictionary<int, int>();
                foreach (int row in waiting) gain[row] = each;
                var extra = new List<int>(waiting);
                for (int n = 0; n < pool % waiting.Count; n++)
                {
                    int pick = Random.Next(extra.Count);
                    gain[extra[pick]]++;
                    extra.RemoveAt(pick);
                }
                pool = 0;
                foreach (int row in waiting)
                {
                    if (gain[row] > 0) Charged(c, row, relay, AddCharge(c, row, gain[row]), gain[row]);
                    else Missed(c, row, relay, MissReason.TooLarge);
                }
            }

            // Überladung: gleichmässig in Feldern auf die laufenden, Rest in Lesereihenfolge.
            fired.Sort();
            int share = fired.Count > 0 ? pool / fired.Count : 0;
            int rest = fired.Count > 0 ? pool % fired.Count : 0;
            int perCell = Math.Max(0, c.Board.EffectConfig.OverchargePowerPercentPerCell);
            for (int n = 0; n < fired.Count; n++)
            {
                int row = fired[n];
                int cells = c.Board.Rows[row].Cells;
                charge[row] = Math.Max(Stored(row), cells);
                int over = share + (n < rest ? 1 : 0);
                bool spills = spill && over > 0 && c.Board.Rows[row].Has(CircuitEffectIds.Spillover);
                Enqueue(c, row, relay, cause, causeRow, tier, int.MaxValue, target, spills ? 0 : over * perCell, chargeCost: cells);
                if (spills) Spill(c, row, over, tier);
            }
        }

        /// <summary>Spillover: die Überladung einer Komponente lädt ihre Nachbarn (gleiche Regeln, ohne weiteren Überschlag).</summary>
        private void Spill(Combatant c, int row, int amount, int tier)
        {
            var neighbours = new List<int>();
            foreach (int n in c.Board.Rows[row].Neighbours)
                if (n != row && !c.Board.Rows[n].IsOrphaned && c.Board.Rows[n].Skill != null && !neighbours.Contains(n)) neighbours.Add(n);
            Emit(new BattleEvent(Tick, BattleEventKind.Spillover, c, null, amount, c.Board.Rows[row].Skill?.Id, row));
            Distribute(c, neighbours, amount, -1, ActionCause.Trigger, row, tier, null, spill: false);
        }

        /// <summary>
        /// Lädt eine Komponente auf und gibt ihre Ladung zurück. Die Ladung gehört der Komponente: Relais, Pulse, Charge Link und
        /// Spillover füllen denselben Speicher. Verbraucht wird sie erst beim Einreihen (<see cref="SpendCharge"/>); was übrig
        /// bleibt, reiht die Komponente nach ihrer Ausführung erneut ein. Jeder Kampf startet bei 0.
        /// </summary>
        private int AddCharge(Combatant c, int row, int amount)
        {
            Dictionary<int, int> charge = _fx[c].Charge;
            charge.TryGetValue(row, out int stored);
            if (amount > 0) stored = (int)Math.Min(int.MaxValue, (long)stored + amount);
            charge[row] = stored;
            return stored;
        }

        private void SpendCharge(Combatant c, int row, int cost)
        {
            if (cost <= 0) return;
            Dictionary<int, int> charge = _fx[c].Charge;
            charge.TryGetValue(row, out int stored);
            charge[row] = Math.Max(0, stored - cost);
            Emit(new BattleEvent(Tick, BattleEventKind.ChargeSpent, c, null, charge[row], c.Board.Rows[row].Skill?.Id, row)
                { Extra = c.Board.Rows[row].Cells });
        }

        /// <summary>
        /// Charge Coil: jede berührte Komponente bekommt <paramref name="amount"/> Ladung; wer damit voll ist, wird eingereiht.
        /// </summary>
        public void ChargeNeighbours(Combatant c, int row, int amount)
        {
            if (c == null || amount <= 0 || row < 0 || row >= c.Board.Rows.Count) return;
            foreach (int n in c.Board.Rows[row].Neighbours)
            {
                LogicRow r = c.Board.Rows[n];
                if (n == row || r.IsOrphaned || r.Skill == null) continue;
                int stored = AddCharge(c, n, amount);
                if (stored >= r.Cells) Enqueue(c, n, -1, ActionCause.Trigger, row, 0, int.MaxValue, null, chargeCost: r.Cells);
                else Charged(c, n, -1, stored, amount);
            }
        }

        /// <summary>Ein Auslösen hat Ladung gespeichert, die Komponente läuft aber (noch) nicht. Kein «Missed Trigger».</summary>
        private void Charged(Combatant c, int row, int relay, int stored, int gained)
        {
            Emit(new BattleEvent(Tick, BattleEventKind.Charged, c, null, stored, c.Board.Rows[row].Skill?.Id, row)
                { Extra = c.Board.Rows[row].Cells, Power = gained, Relay = relay });
        }

        /// <summary>
        /// Nach der Ausführung: reicht die gespeicherte Ladung noch für eine weitere, kommt die Komponente gleich wieder in die
        /// Warteschlange (mit Stufe und Relais der eben beendeten Ausführung).
        /// </summary>
        private void RequeueFromCharge(Combatant c, ActionState a)
        {
            int row = a.RowIndex;
            if (row < 0 || row >= c.Board.Rows.Count || c.IsQueued(row)) return;
            int cells = c.Board.Rows[row].Cells;
            if (!_fx[c].Charge.TryGetValue(row, out int stored) || stored < cells) return;
            Enqueue(c, row, a.Relay, ActionCause.Board, -1, a.BonusTier, int.MaxValue, null, chargeCost: cells);
        }

        /// <summary>Overflow (A-21): Schock an alle Gegner, Schaden nach Grösse der Komponente (Grössen-Wucht × Anteil).</summary>
        private void Overflow(Combatant c, int row, int relay)
        {
            LogicRow r = c.Board.Rows[row];
            CircuitEffectConfig fx = c.Board.EffectConfig;
            Missed(c, row, relay, MissReason.Overflow);
            Emit(new BattleEvent(Tick, BattleEventKind.OverflowShock, c, null, c.QueueList.Count, r.Skill.Id, row) { Relay = relay });
            int percent = SkillBudgetConfig.Default.PowerPercent(r.Cells) * Math.Max(0, fx.OverflowDamagePercent) / 100;
            int amount = Math.Max(1, BasisPoints.Of(c.GetStat(StatKind.Damage), BasisPoints.Percent(percent)));
            BeginActor(c, row);
            foreach (Combatant enemy in OpponentsOf(c))
                ResolveHit(new HitInfo { Source = c, Target = enemy, Amount = amount, SkillId = "overflow", IsArea = true, IgnoreArmor = true });
            EndActor();
        }

        /// <summary>Overclock (A-21): Hitze für eine Komponente. Volle Hitze lässt die nächste Ausführung ausfallen.</summary>
        private void AddHeat(Combatant c, int row, int amount, int sourceRow)
        {
            if (amount <= 0 || row < 0 || row >= c.Board.Rows.Count || c.Board.Rows[row].IsOrphaned) return;
            int[] heat = _fx[c].Heat;
            heat[row] += amount;
            Emit(new BattleEvent(Tick, BattleEventKind.HeatChanged, c, null, heat[row], c.Board.Rows[row].Skill.Id, row) { Extra = sourceRow });
        }

        /// <summary>Hitze einer Komponente (Anzeige, Tests).</summary>
        public int Heat(Combatant c, int row) => row >= 0 && row < _fx[c].Heat.Length ? _fx[c].Heat[row] : 0;

        /// <summary>Freie Kämpfer starten die erste wartende Komponente in Lesereihenfolge, sonst den Basisangriff.</summary>
        private void Decide()
        {
            foreach (Combatant c in _all)
            {
                if (!c.IsAlive || c.IsStunned || OpponentsOf(c).Count == 0) continue;
                bool basicWindup = c.Action != null && c.Action.InWindup && c.Action.Skill.IsBasicAttack;
                if (c.Action != null && !basicWindup) continue;

                QueuedRow next = NextQueued(c);
                while (next != null && SkipsForHeat(c, next)) next = NextQueued(c);
                if (next != null)
                {
                    if (c.Action != null) Interrupt(c);
                    StartQueued(c, next);
                }
                else if (c.Action == null)
                {
                    StartAction(c, c.Board.Fallback.Skill, null, c.Board.FallbackIndex);
                }
            }
        }

        /// <summary>
        /// Die wartende Komponente mit der höchsten Priorität, eingefrorene warten weiter: Interrupt zuerst, dann wer schon
        /// <see cref="QueueConfig.StarvationTicks"/> wartet (die älteste zuerst), sonst Lesereihenfolge.
        /// </summary>
        private QueuedRow NextQueued(Combatant c)
        {
            QueuedRow best = null;
            int bestRank = int.MaxValue;
            int starve = Math.Max(0, _queue.StarvationTicks);
            foreach (QueuedRow q in c.QueueList)
            {
                if (c.IsFrozen(q.Row, Tick)) continue;
                // Interrupt (A-21): springt an die Spitze.
                bool jumps = c.Board.Rows[q.Row].Has(CircuitEffectIds.Interrupt);
                bool starved = starve > 0 && Tick - q.SinceTick >= starve;
                int rank = jumps ? 0 : starved ? 1 : 2;
                bool better = best == null || rank < bestRank
                    || (rank == bestRank && (rank == 1 ? q.SinceTick < best.SinceTick || (q.SinceTick == best.SinceTick && q.Row < best.Row) : q.Row < best.Row));
                if (!better) continue;
                best = q;
                bestRank = rank;
            }
            return best;
        }

        /// <summary>Hitze (A-21): bei voller Hitze fällt diese Ausführung aus, danach ist die Hitze wieder 0.</summary>
        private bool SkipsForHeat(Combatant c, QueuedRow q)
        {
            int[] heat = _fx[c].Heat;
            int limit = c.Board.EffectConfig.HeatSkipAt;
            if (limit <= 0 || heat[q.Row] < limit) return false;
            c.QueueList.Remove(q);
            Emit(new BattleEvent(Tick, BattleEventKind.HeatSkip, c, null, heat[q.Row], c.Board.Rows[q.Row].Skill.Id, q.Row) { Relay = q.Relay });
            Missed(c, q.Row, q.Relay, MissReason.Overheated);
            heat[q.Row] = 0;
            Emit(new BattleEvent(Tick, BattleEventKind.HeatChanged, c, null, 0, c.Board.Rows[q.Row].Skill.Id, q.Row) { Extra = -1 });
            return true;
        }

        /// <summary>Startet eine wartende Komponente ohne erneute Prüfung, mit dem Bonus, den sie verdient hat.</summary>
        private void StartQueued(Combatant c, QueuedRow q)
        {
            c.QueueList.Remove(q);
            LogicRow row = c.Board.Rows[q.Row];
            Combatant target = q.Target != null && q.Target.IsAlive ? q.Target : null;
            StartAction(c, row.Skill, target, q.Row, q.Cause, q.CauseRow, null, q.BonusTier, q.WaitedTicks(Tick), q.Relay, q.PowerPercent, q.Depth);
        }

        /// <summary>Haste: −<paramref name="percent"/> % Cast-Zeit für <paramref name="ticks"/> (A-19, ersetzt «senkt Cooldowns»).</summary>
        public void Haste(Combatant c, int percent, int ticks)
        {
            if (c == null || !c.IsAlive || percent == 0) return;
            ApplyStatus(c, new StatModifierStatus(StatusIds.Haste, StatKind.CastPercent, -percent, ticks), c);
        }
        /// <summary>
        /// Freeze: die grösste Komponente des Ziels (bei Gleichstand die erste in Lesereihenfolge) kann eine Weile nicht
        /// feuern. Holt sie gerade aus, bricht das ab.
        /// </summary>
        public void Freeze(Combatant target, int ticks, Combatant source)
        {
            if (target == null || !target.IsAlive || ticks <= 0) return;
            int best = -1;
            for (int i = 0; i < target.Board.Rows.Count; i++)
            {
                LogicRow r = target.Board.Rows[i];
                if (r.IsOrphaned || !r.IsPowered) continue;
                if (best < 0 || r.Cells > target.Board.Rows[best].Cells) best = i;
            }
            if (best < 0) return;
            target.Freeze(best, Tick + ticks);
            Emit(new BattleEvent(Tick, BattleEventKind.Frozen, source, target, ticks, target.Board.Rows[best].Skill.Id) { Extra = best });
            if (target.Action != null && target.Action.RowIndex == best && target.Action.InWindup) Interrupt(target);
        }

        // ------------------------------------------------------------------ Hacks (A-21)

        private static bool IsHack(string id) => CircuitEffectCatalog.Shared.TryGet(id, out CircuitEffectDefinition e) && e.IsHack;

        /// <summary>
        /// Ein Hack gegen die gegnerische Platine: Bit Flip, Jam, Hijack, Short Circuit oder Latency. Die Firewall des Opfers
        /// blockt ihn, solange sie Ladungen hat. Gegner hacken mit denselben Regeln.
        /// </summary>
        public void Hack(Combatant hacker, Combatant victim, string id, int sourceRow = -1)
        {
            if (hacker == null || !hacker.IsAlive) return;
            if (victim == null || !victim.IsAlive || victim.Side == hacker.Side) victim = DefaultTarget(hacker);
            if (victim == null) return;
            CircuitEffectConfig fx = hacker.Board.EffectConfig;

            if (id == CircuitEffectIds.Latency)
            {
                foreach (Combatant enemy in OpponentsOf(hacker))
                {
                    if (Blocked(hacker, enemy, id, sourceRow)) continue;
                    ApplyStatus(enemy, new StatModifierStatus(StatusIds.Latency, StatKind.CastPercent, fx.LatencyCastPercent, fx.LatencyTicks), hacker);
                    Emit(new BattleEvent(Tick, BattleEventKind.Hacked, hacker, enemy, fx.LatencyTicks, id, sourceRow) { Extra = -1 });
                }
                return;
            }

            if (Blocked(hacker, victim, id, sourceRow)) return;
            EffectRuntime v = _fx[victim];
            switch (id)
            {
                case CircuitEffectIds.BitFlip:
                {
                    int relay = FlipTarget(victim);
                    if (relay < 0) break;
                    v.FlippedUntil[relay] = Tick + Math.Max(1, fx.BitFlipTicks);
                    Emit(new BattleEvent(Tick, BattleEventKind.Hacked, hacker, victim, fx.BitFlipTicks, id, sourceRow) { Extra = relay });
                    return;
                }
                case CircuitEffectIds.Jam:
                {
                    int relay = MostImportantRelay(victim);
                    if (relay < 0) break;
                    v.JamLeft[relay] += Math.Max(1, fx.JamTriggers);
                    Emit(new BattleEvent(Tick, BattleEventKind.Hacked, hacker, victim, v.JamLeft[relay], id, sourceRow) { Extra = relay });
                    return;
                }
                case CircuitEffectIds.Hijack:
                {
                    int row = LargestPowered(victim);
                    if (row < 0) break;
                    v.Hijacked[row] = hacker;
                    Emit(new BattleEvent(Tick, BattleEventKind.Hacked, hacker, victim, 1, id, sourceRow) { Extra = row });
                    return;
                }
                case CircuitEffectIds.ShortCircuit:
                {
                    int row = LargestPowered(victim);
                    if (row < 0) break;
                    Emit(new BattleEvent(Tick, BattleEventKind.Hacked, hacker, victim, 0, id, sourceRow) { Extra = row });
                    ShortCircuit(victim, row);
                    return;
                }
                default:
                    return;
            }
            Emit(new BattleEvent(Tick, BattleEventKind.HackFailed, hacker, victim, 0, id, sourceRow));
        }

        /// <summary>Firewall: blockt einen Hack, solange Ladungen da sind.</summary>
        private bool Blocked(Combatant hacker, Combatant victim, string id, int sourceRow)
        {
            EffectRuntime v = _fx[victim];
            if (v.Firewall <= 0) return false;
            v.Firewall--;
            Emit(new BattleEvent(Tick, BattleEventKind.HackBlocked, hacker, victim, v.Firewall, id, sourceRow));
            return true;
        }

        /// <summary>Verbleibende Firewall-Ladungen (Anzeige, Tests).</summary>
        public int FirewallCharges(Combatant c) => _fx[c].Firewall;

        /// <summary>Ist ein Relais gerade umgekehrt (Bit Flip)?</summary>
        public bool IsFlipped(Combatant c, int relay) => relay >= 0 && relay < _fx[c].FlippedUntil.Length && _fx[c].FlippedUntil[relay] > Tick;

        /// <summary>Wie viele Auslösungen ein Relais noch ignoriert (Jam).</summary>
        public int JamLeft(Combatant c, int relay) => relay >= 0 && relay < _fx[c].JamLeft.Length ? _fx[c].JamLeft[relay] : 0;

        /// <summary>Ist die nächste Ausführung einer Komponente gekapert (Hijack)?</summary>
        public bool IsHijacked(Combatant c, int row) => _fx[c].Hijacked.ContainsKey(row);

        /// <summary>Zellen, die ein Relais versorgt (Wichtigkeit für Hacks).</summary>
        private static int PoweredCells(Combatant c, LogicRelay relay)
        {
            int cells = 0;
            foreach (int row in relay.Powered) cells += c.Board.Rows[row].Cells;
            return cells;
        }

        /// <summary>Wichtigstes Relais: versorgt die meisten Zellen (bei Gleichstand das erste in Lesereihenfolge).</summary>
        private static int MostImportantRelay(Combatant c)
        {
            int best = -1, bestCells = 0;
            for (int i = 0; i < c.Board.Relays.Count; i++)
            {
                int cells = PoweredCells(c, c.Board.Relays[i]);
                if (cells > bestCells)
                {
                    best = i;
                    bestCells = cells;
                }
            }
            return best;
        }

        /// <summary>
        /// Bit Flip: ein Zustands-Relais, das gerade gilt (umgekehrt hört es auf zu gelten), das wichtigste davon.
        /// Ereignis-Relais (Clock, On Hit …) haben keinen Zustand zum Umkehren.
        /// </summary>
        private int FlipTarget(Combatant c)
        {
            int best = -1, bestCells = 0;
            for (int i = 0; i < c.Board.Relays.Count; i++)
            {
                LogicRelay relay = c.Board.Relays[i];
                if (relay.Gate != null || relay.IsEventTrigger || IsFlipped(c, i)) continue;
                int cells = PoweredCells(c, relay);
                if (cells <= bestCells || !relay.Condition.IsMet(new ConditionContext(this, c, _relays[c][i]), out _)) continue;
                best = i;
                bestCells = cells;
            }
            return best;
        }

        /// <summary>Grösste versorgte Komponente (bei Gleichstand die erste in Lesereihenfolge).</summary>
        private static int LargestPowered(Combatant c)
        {
            int best = -1;
            for (int i = 0; i < c.Board.Rows.Count; i++)
            {
                LogicRow r = c.Board.Rows[i];
                if (r.IsOrphaned || !r.IsPowered) continue;
                if (best < 0 || r.Cells > c.Board.Rows[best].Cells) best = i;
            }
            return best;
        }

        /// <summary>Short Circuit: die Komponente feuert sofort, ihre gezielten Wirkungen treffen die eigene Seite.</summary>
        private void ShortCircuit(Combatant victim, int row)
        {
            LogicRow r = victim.Board.Rows[row];
            Combatant ally = victim;
            for (int k = 1; k < _all.Count; k++)
            {
                Combatant other = _all[(victim.Index + k) % _all.Count];
                if (other.Side == victim.Side && other.IsAlive)
                {
                    ally = other;
                    break;
                }
            }
            Emit(new BattleEvent(Tick, BattleEventKind.ShortCircuit, victim, ally, 0, r.Skill.Id, row) { Extra = row });
            var context = new SkillContext(this, victim, ally, r.Skill, row);
            BeginActor(victim, row);
            foreach (ISkillEffect effect in r.Skill.Effects)
                if (victim.IsAlive && SkillEffects.HitsTarget(effect)) effect.Apply(context);
            EndActor();
        }

        /// <summary>Hijack: die gekaperte Ausführung wirkt für den Hacker, gegen dessen Gegner.</summary>
        private void ExecuteHijacked(Combatant c, ActionState a)
        {
            Combatant hacker = a.HijackedBy;
            Combatant target = DefaultTarget(hacker);
            Emit(new BattleEvent(Tick, BattleEventKind.HijackedExecution, hacker, c, 0, a.Skill.Id) { Extra = a.RowIndex });
            var context = new SkillContext(this, hacker, target, a.Skill, -1);
            BeginActor(hacker, -1);
            foreach (ISkillEffect effect in a.Skill.Effects)
            {
                if (!hacker.IsAlive) break;
                effect.Apply(context);
            }
            EndActor();
        }

        /// <summary>
        /// Cast-Zeit und Erholung einer Aktion. Der Basisangriff teilt sein Intervall 2:1 auf, andere Skills nutzen ihre
        /// Cast-Zeit mit allen Änderungen (auch Haste/Slow des Kämpfers). Beide nie unter <paramref name="minCastTicks"/>.
        /// </summary>
        public static void ActionTiming(SkillDefinition skill, int attackIntervalTicks, out int windup, out int recovery,
            int minCastTicks = CastTime.DefaultMinTicks, int castPercent = 0)
        {
            minCastTicks = Math.Max(1, minCastTicks);
            if (skill.IsBasicAttack)
            {
                int interval = Math.Max(1, attackIntervalTicks);
                windup = Math.Max(minCastTicks, interval * 2 / 3);
                recovery = Math.Max(0, interval - windup);
            }
            else
            {
                windup = CastTime.Apply(skill.WindupTicks, skill.CastBonusPercent + castPercent, minCastTicks);
                recovery = skill.RecoveryTicks;
            }
        }

        /// <summary>
        /// Startet eine Aktion mit voller Cast-Zeit. Öffentlich für Effekte, die Aktionen auslösen. Die Stufe
        /// <paramref name="bonusTier"/> stammt vom auslösenden Relais (Wiederholungen bringen ihre mit).
        /// </summary>
        public void StartAction(Combatant c, SkillDefinition skill, Combatant target, int rowIndex,
            ActionCause cause = ActionCause.Board, int causeRow = -1, int? repeatsLeft = null, int bonusTier = 0, int queuedTicks = -1,
            int relay = -1, int power = 0, int depth = 0, bool thread = false)
        {
            bool repeat = cause == ActionCause.Repeat;

            int tier = DifficultyBonusConfig.Clamp(bonusTier);
            int castBefore = 0;
            if (tier > 0 && skill.DifficultyTier == 0)
            {
                if (!skill.IsBasicAttack)
                {
                    ActionTiming(skill, c.AttackIntervalTicks, out castBefore, out _, _setup.MinCastTicks, c.CastPercent);
                }
                LogicRow own = rowIndex >= 0 && rowIndex < c.Board.Rows.Count ? c.Board.Rows[rowIndex] : null;
                skill = own != null && own.Skill == skill ? own.SkillAt(tier, c.Board.Bonus) : c.Board.Bonus.Apply(skill, tier);
            }
            if (skill.DifficultyTier == 0) tier = 0;
            // Verstärker und Recursion (A-21): zusätzliche Wirkung dieser Ausführung.
            if (power != 0 && !skill.IsBasicAttack) skill = skill.WithBonus(power);
            int castPercent = skill.IsBasicAttack ? 0 : c.CastPercent + ThermalCastPercent;
            ActionTiming(skill, c.AttackIntervalTicks, out int windup, out int recovery, _setup.MinCastTicks, castPercent);

            var action = new ActionState
            {
                Skill = skill,
                Target = target ?? DefaultTarget(c),
                RowIndex = rowIndex,
                StartTick = Tick,
                WindupLeft = windup,
                RecoveryLeft = recovery,
                Cause = cause,
                CauseRow = causeRow,
                Relay = relay,
                RepeatsLeft = thread ? 0 : repeatsLeft ?? (repeat ? 0 : skill.ExtraCasts),
                BonusTier = tier,
                Depth = depth,
                PowerPercent = skill.IsBasicAttack ? 0 : power,
                IsThread = thread,
            };
            // Hijack (A-21): die nächste Ausführung dieser Komponente geschieht für den Hacker.
            if (!repeat && rowIndex >= 0 && _fx[c].Hijacked.TryGetValue(rowIndex, out Combatant hijacker))
            {
                _fx[c].Hijacked.Remove(rowIndex);
                action.HijackedBy = hijacker;
            }
            if (thread) c.ThreadList.Add(action);
            else c.Action = action;

            Emit(new BattleEvent(Tick, BattleEventKind.ActionStarted, c, action.Target, windup, skill.Id, rowIndex)
            {
                Cause = cause, CauseRow = causeRow, Tier = tier, QueuedTicks = queuedTicks, Relay = relay,
                Bonus = tier > 0 ? Math.Max(0, castBefore - windup) : 0, Depth = depth, Power = action.PowerPercent,
            });
            foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnActionStarted(this, c, skill, rowIndex);

            // Modul «Blood Toll»: kostet bei jedem Start HP.
            if (!repeat && skill.HpCostBp > 0)
                ResolveHit(HitInfo.SelfDamage(c, Math.Max(1, BasisPoints.Of(c.MaxHp, skill.HpCostBp)), "hp_cost"));
        }

        private BattleOutcome? CheckEnd()
        {
            if (!Player.IsAlive) return BattleOutcome.Defeat;
            bool anyEnemy = false;
            foreach (Combatant e in _enemies) if (e.IsAlive) anyEnemy = true;
            if (!anyEnemy) return BattleOutcome.Victory;
            if (_setup.SurviveTicks > 0 && Tick >= _setup.SurviveTicks) return BattleOutcome.Escaped;
            return null;
        }

        // ------------------------------------------------------------------ Werkzeuge für Effekte, Bedingungen, Modifikatoren

        public void Emit(BattleEvent e)
        {
            if (e.RowIndex < 0 && _actor != null && e.Source == _actor) e.RowIndex = _actorRow;
            _events.Add(e);
            _pending.Add(e);
        }

        public List<Combatant> OpponentsOf(Combatant c)
        {
            var result = new List<Combatant>();
            foreach (Combatant other in _all)
                if (other.Side != c.Side && other.IsAlive) result.Add(other);
            return result;
        }

        public Combatant DefaultTarget(Combatant c)
        {
            foreach (Combatant other in _all)
                if (other.Side != c.Side && other.IsAlive) return other;
            return null;
        }

        /// <summary>Bricht die laufende Aktion ab.</summary>
        public void Interrupt(Combatant c)
        {
            if (c.Action == null) return;
            Emit(new BattleEvent(Tick, BattleEventKind.ActionInterrupted, c, null, 0, c.Action.Skill.Id, c.Action.RowIndex));
            c.Action = null;
        }

        /// <summary>Schadensberechnung: Modifikatoren, Ausweichen, Krit, Block, Rüstung, dann Abzug.</summary>
        public void ResolveHit(HitInfo hit)
        {
            if (hit?.Target == null || !hit.Target.IsAlive || hit.Amount <= 0) return;

            if (hit.Source != null && hit.Source != hit.Target)
                foreach (BattleModifier m in hit.Source.ModifierList.ToArray()) m.ModifyHit(this, hit.Source, hit);
            foreach (BattleModifier m in hit.Target.ModifierList.ToArray()) m.ModifyHit(this, hit.Target, hit);
            if (!hit.IsSelfDamage)
                foreach (StatusEffect s in hit.Target.StatusList.ToArray())
                    if (s.IsActive) s.ModifyIncomingHit(this, hit.Target, hit);

            int amount = hit.Amount;
            if (!hit.IsSelfDamage)
            {
                if (hit.CanBeDodged && (hit.ForceDodge || Defense.RollDodge(this, hit.Source, hit.Target)))
                {
                    hit.Dodged = true;
                    Emit(new BattleEvent(Tick, BattleEventKind.Dodged, hit.Source, hit.Target, hit.Amount, hit.SkillId));
                    return;
                }

                if (hit.CanCrit && hit.Source != null && Defense.RollCrit(this, hit.Source))
                {
                    hit.Crit = true;
                    amount = (int)System.Math.Min(int.MaxValue, (long)amount * Defense.CritDamageBp / BasisPoints.Full);
                    Emit(new BattleEvent(Tick, BattleEventKind.Crit, hit.Source, hit.Target, amount, hit.SkillId));
                }

                if (hit.CanBeBlocked && Defense.RollBlock(this, hit.Target))
                {
                    hit.Blocked = true;
                    amount = BasisPoints.Of(amount, Defense.BlockedDamageBp);
                    Emit(new BattleEvent(Tick, BattleEventKind.Blocked, hit.Source, hit.Target, amount, hit.SkillId));
                }

                if (!hit.IgnoreArmor) amount = Defense.ApplyArmor(amount, hit.Target.EffectiveArmor);
                // Thermal Throttling (A-21): nach der Rüstung, jede Stufe mehr Schaden für beide Seiten.
                if (ThermalDamagePercent > 0) amount = (int)Math.Min(int.MaxValue / 2, (long)amount * (100 + ThermalDamagePercent) / 100);
            }

            hit.Final = Math.Max(1, amount);
            foreach (BattleModifier m in hit.Target.ModifierList.ToArray()) m.ModifyFinalDamage(this, hit.Target, hit);
            if (hit.Source != null && hit.Source != hit.Target)
                foreach (BattleModifier m in hit.Source.ModifierList.ToArray()) m.ModifyFinalDamage(this, hit.Source, hit);

            ApplyDamage(hit);
        }

        private void ApplyDamage(HitInfo hit)
        {
            Combatant target = hit.Target;
            int dealt = Math.Max(0, Math.Min(hit.Final, target.Hp));
            target.Hp -= dealt;

            if (hit.IsAttack && hit.Source != null)
                Emit(new BattleEvent(Tick, BattleEventKind.Hit, hit.Source, target, dealt, hit.SkillId));
            int bonus = hit.Source != null && hit.Source == _actor && !hit.IsSelfDamage ? BonusPart(dealt, _actorPower) : 0;
            Emit(new BattleEvent(Tick, hit.IsSelfDamage ? BattleEventKind.SelfDamage : BattleEventKind.Damage, hit.Source, target, dealt, hit.SkillId)
                { Bonus = bonus });

            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.Action = null;
                target.ThreadList.Clear();
                target.StatusList.Clear();
                target.QueueList.Clear();
                Emit(new BattleEvent(Tick, BattleEventKind.Death, hit.Source, target, 0, hit.SkillId));
            }
        }

        /// <summary>Heilt bis zum Maximum. Gibt die geheilte Menge zurück.</summary>
        public int Heal(Combatant target, int amount, Combatant source = null, string detail = null)
        {
            if (target == null || !target.IsAlive || amount <= 0) return 0;
            int healed = Math.Min(amount, target.MaxHp - target.Hp);
            if (healed <= 0) return 0;
            target.Hp += healed;
            int bonus = (source ?? target) == _actor ? BonusPart(healed, _actorPower) : 0;
            Emit(new BattleEvent(Tick, BattleEventKind.Healed, source ?? target, target, healed, detail) { Bonus = bonus });
            return healed;
        }

        public void ApplyStatus(Combatant target, StatusEffect status, Combatant source)
        {
            if (target == null || !target.IsAlive || status == null || status.TicksLeft <= 0) return;
            status.Source = source;
            status.SourceRow = source != null && source == _actor ? _actorRow : -1;
            status.SourcePowerPercent = source != null && source == _actor ? _actorPower : 0;

            // Erleichterung «Betäubungen dauern länger».
            if (status.Stuns && source != null && source.Side != target.Side) status.TicksLeft += source.Relief(ReliefIds.StunLonger);

            if (!status.Stacks) target.StatusList.RemoveAll(s => s.Id == status.Id);
            target.StatusList.Add(status);
            int stacks = 0;
            foreach (StatusEffect s in target.StatusList) if (s.Id == status.Id) stacks++;
            Emit(new BattleEvent(Tick, BattleEventKind.StatusApplied, source, target, status.TicksLeft, status.Id) { Extra = stacks });

            if (status.Stuns)
            {
                Interrupt(target);
                target.Pending.Clear();
                target.ThreadList.Clear();
            }
        }

        /// <summary>Ändert einen Ressourcen-Zähler innerhalb von [min, max]. Gibt den neuen Wert zurück.</summary>
        public int ChangeResource(Combatant c, string id, int delta, int max = int.MaxValue, int min = 0)
        {
            int before = c.GetResource(id);
            int after = Math.Max(min, Math.Min(max, before + delta));
            if (after == before) return after;
            c.SetResourceRaw(id, after);
            Emit(new BattleEvent(Tick, BattleEventKind.ResourceChanged, c, c, after, id) { Extra = max == int.MaxValue ? 0 : max });
            return after;
        }

        public void AddBonusGold(int amount)
        {
            if (amount > 0) BonusGold += amount;
        }

        // ------------------------------------------------------------------ Abfragen fürs Protokoll

        /// <summary>
        /// Gibt es ein passendes Ereignis in [fromTick, toTick]? Durchsucht das Protokoll von hinten.
        /// </summary>
        public bool AnyEvent(int fromTick, int toTick, Func<BattleEvent, bool> match)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                BattleEvent e = _events[i];
                if (e.Tick > toTick) continue;
                if (e.Tick < fromTick) break;
                if (match(e)) return true;
            }
            return false;
        }

        /// <summary>Zählt passende Ereignisse in [fromTick, toTick].</summary>
        public int CountEvents(int fromTick, int toTick, Func<BattleEvent, bool> match)
        {
            int count = 0;
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                BattleEvent e = _events[i];
                if (e.Tick > toTick) continue;
                if (e.Tick < fromTick) break;
                if (match(e)) count++;
            }
            return count;
        }
    }
}
