using System;
using System.Collections.Generic;

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

        public int Tick { get; private set; }
        public Random Random { get; }
        public BattleContext Context { get; }
        public Combatant Player { get; }
        public IReadOnlyList<Combatant> Enemies => _enemies;
        public IReadOnlyList<Combatant> All => _all;
        public IReadOnlyList<BattleEvent> Events => _events;

        public int TimeLimitTicks => _setup.TimeLimitTicks;
        public bool IsOverheated => Tick > _setup.TimeLimitTicks;
        public int OverheatLevel { get; private set; }
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
                Decide();
                outcome = CheckEnd();
                if (outcome.HasValue) break;
            }

            if (!outcome.HasValue)
            {
                Tick = _setup.MaxTicks;
                outcome = BattleOutcome.Timeout;
            }

            // Die Warteschlange leert sich am Kampfende.
            foreach (Combatant c in _all) c.QueueList.Clear();
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

            if (IsOverheated && (Tick - _setup.TimeLimitTicks) % Ticks.PerSecond == 0)
            {
                OverheatLevel++;
                Emit(new BattleEvent(Tick, BattleEventKind.Overheat, null, null, OverheatLevel));
                foreach (Combatant c in _all.ToArray())
                {
                    if (!c.IsAlive) continue;
                    int amount = Math.Max(1, c.MaxHp * OverheatLevel / 100);
                    ResolveHit(HitInfo.True(c, amount, "overheat"));
                }
            }
        }

        private void AdvanceActions()
        {
            foreach (Combatant c in _all)
            {
                ActionState a = c.Action;
                if (a == null || !c.IsAlive || c.IsStunned) continue;

                if (a.InWindup)
                {
                    a.WindupLeft--;
                    if (a.WindupLeft > 0) continue;

                    Execute(c, a);
                    if (c.Action != a) continue; // durch Tod oder Betäubung abgebrochen
                    a.EffectApplied = true;
                    AfterExecution(c, a);
                    if (a.RecoveryLeft <= 0) Finish(c, a);
                }
                else
                {
                    a.RecoveryLeft--;
                    if (a.RecoveryLeft <= 0) Finish(c, a);
                }
            }
        }

        private void Execute(Combatant c, ActionState a)
        {
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
            EndActor();
        }

        /// <summary>
        /// Nach der Wirkung: Wiederholungen aus «Multicast» vormerken, Auslöser-Module der Komponente feuern und
        /// «Repeat while true» prüfen (das Relais gilt noch: die Komponente kommt erneut in die Warteschlange).
        /// </summary>
        private void AfterExecution(Combatant c, ActionState a)
        {
            if (!c.IsAlive) return;
            if (a.RepeatsLeft > 0)
            {
                c.Pending.Insert(0, new PendingAction
                {
                    Skill = a.Skill, Target = a.Target, Row = a.RowIndex, Cause = ActionCause.Repeat, RepeatsLeft = a.RepeatsLeft - 1,
                    BonusTier = a.BonusTier, Relay = a.Relay,
                });
            }
            if (a.IsRepeat || a.RowIndex < 0 || a.RowIndex >= c.Board.Rows.Count) return;

            LogicRelay relay = a.Relay >= 0 && a.Relay < c.Board.Relays.Count ? c.Board.Relays[a.Relay] : null;
            // Der Bonus und die Grenze wandern mit: ausgelöste Ziele laufen mit Stufe und Grenze dieser Ausführung.
            FireEdges(c, GraphNode.Skill(a.RowIndex), a.BonusTier, a.Relay, relay?.MaxCells ?? int.MaxValue);

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
                for (int i = 0; i < states.Length; i++)
                {
                    LogicRelay relay = c.Board.Relays[i];
                    var context = new ConditionContext(this, c, states[i]);
                    if (relay.Condition is IObservingCondition observing) observing.Observe(context);
                    if (!Triggers(relay.Condition, context, states[i], out Combatant target)) continue;

                    states[i].LastFiredTick = Tick;
                    states[i].FireCount++;
                    Emit(new BattleEvent(Tick, BattleEventKind.RelayTriggered, c, target, i, relay.Label) { Extra = relay.Powered.Count, Relay = i });
                    foreach (int row in relay.Powered) Enqueue(c, row, i, ActionCause.Board, -1, relay.Difficulty, relay.MaxCells, target);
                    foreach (int row in relay.TooLarge) Missed(c, row, i, MissReason.TooLarge);
                    FireEdges(c, GraphNode.Block(i), relay.Difficulty, i, relay.MaxCells);
                }
            }
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
                if (edge.Kind != GraphEdgeKind.Trigger) continue;
                int causeRow = from.Kind == GraphNodeKind.Skill ? from.Row : -1;
                Enqueue(c, edge.To.Row, sourceRelay, ActionCause.Trigger, causeRow, sourceTier, maxCells, null);
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
        private void Enqueue(Combatant c, int row, int relay, ActionCause cause, int causeRow, int tier, int maxCells, Combatant target)
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
                Missed(c, row, relay, MissReason.TooLarge);
                return;
            }
            if (c.IsFrozen(row, Tick))
            {
                Missed(c, row, relay, MissReason.Frozen);
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
            if (entries >= Math.Max(1, _queue.MaxEntriesPerComponent))
            {
                if (tier > existing.BonusTier)
                {
                    existing.BonusTier = tier;
                    existing.Relay = relay;
                }
                Missed(c, row, relay, MissReason.AlreadyQueued);
                return;
            }

            c.QueueList.Add(new QueuedRow
            {
                Row = row, SinceTick = Tick, Cause = cause, CauseRow = causeRow, Relay = relay, BonusTier = DifficultyBonusConfig.Clamp(tier),
                Target = target,
            });
            Emit(new BattleEvent(Tick, BattleEventKind.RowQueued, c, target, 0, r.Skill.Id, row)
                { Cause = cause, CauseRow = causeRow, Tier = tier, Relay = relay });
        }

        /// <summary>Freie Kämpfer starten die erste wartende Komponente in Lesereihenfolge, sonst den Basisangriff.</summary>
        private void Decide()
        {
            foreach (Combatant c in _all)
            {
                if (!c.IsAlive || c.IsStunned || OpponentsOf(c).Count == 0) continue;
                bool basicWindup = c.Action != null && c.Action.InWindup && c.Action.Skill.IsBasicAttack;
                if (c.Action != null && !basicWindup) continue;

                QueuedRow next = NextQueued(c);
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

        /// <summary>Die wartende Komponente mit der höchsten Priorität (Lesereihenfolge), eingefrorene warten weiter.</summary>
        private QueuedRow NextQueued(Combatant c)
        {
            QueuedRow best = null;
            foreach (QueuedRow q in c.QueueList)
            {
                if (c.IsFrozen(q.Row, Tick)) continue;
                if (best == null || q.Row < best.Row) best = q;
            }
            return best;
        }

        /// <summary>Startet eine wartende Komponente ohne erneute Prüfung, mit dem Bonus, den sie verdient hat.</summary>
        private void StartQueued(Combatant c, QueuedRow q)
        {
            c.QueueList.Remove(q);
            LogicRow row = c.Board.Rows[q.Row];
            Combatant target = q.Target != null && q.Target.IsAlive ? q.Target : null;
            StartAction(c, row.Skill, target, q.Row, q.Cause, q.CauseRow, null, q.BonusTier, q.WaitedTicks(Tick), q.Relay);
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
            int relay = -1)
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
            ActionTiming(skill, c.AttackIntervalTicks, out int windup, out int recovery, _setup.MinCastTicks, skill.IsBasicAttack ? 0 : c.CastPercent);

            c.Action = new ActionState
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
                RepeatsLeft = repeatsLeft ?? (repeat ? 0 : skill.ExtraCasts),
                BonusTier = tier,
            };

            Emit(new BattleEvent(Tick, BattleEventKind.ActionStarted, c, c.Action.Target, windup, skill.Id, rowIndex)
            {
                Cause = cause, CauseRow = causeRow, Tier = tier, QueuedTicks = queuedTicks, Relay = relay,
                Bonus = tier > 0 ? Math.Max(0, castBefore - windup) : 0,
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
