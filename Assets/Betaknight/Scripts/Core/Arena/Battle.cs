using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Ein laufender Kampf in festen Ticks (20 pro Sekunde). Ablauf pro Tick:
    /// 1. Zeit: Ereignisse des Vortags an Modifikatoren, Cooldowns, Zustände, Überhitzung.
    /// 2. Laufende Aktionen weiterschalten, Wirkungen anwenden.
    /// 3. (Ereignisse dieses Ticks sind gesammelt; Bedingungen sehen sie ab dem nächsten Tick.)
    /// 4. Freie Kämpfer werten ihre Logik-Tafel aus (Spieler zuerst, dann Gegner).
    /// 5. Siegprüfung.
    /// </summary>
    public sealed class Battle
    {
        private readonly BattleSetup _setup;
        private readonly List<Combatant> _all = new List<Combatant>();
        private readonly List<Combatant> _enemies = new List<Combatant>();
        private readonly List<BattleEvent> _events = new List<BattleEvent>();
        private List<BattleEvent> _pending = new List<BattleEvent>();
        private readonly Dictionary<Combatant, RowRuntime[]> _rows = new Dictionary<Combatant, RowRuntime[]>();
        private readonly List<BattleDecision> _decisions = new List<BattleDecision>();

        // Wer gerade wirkt und aus welcher Zeile: Ereignisse dieses Kämpfers bekommen die Zeile angeheftet.
        private Combatant _actor;
        private int _actorRow = -1;

        // Zeilen, die während der laufenden Aktion des Spielers schon als «Aktion läuft» festgehalten sind.
        private ActionState _busyAction;
        private readonly HashSet<int> _busyRows = new HashSet<int>();

        public int Tick { get; private set; }
        public Random Random { get; }
        public BattleContext Context { get; }
        public Combatant Player { get; }
        public IReadOnlyList<Combatant> Enemies => _enemies;
        public IReadOnlyList<Combatant> All => _all;
        public IReadOnlyList<BattleEvent> Events => _events;

        /// <summary>Entscheidungen des Spielers mit Gründen je Zeile (nur bei Entscheidungen, nicht jeden Tick).</summary>
        public IReadOnlyList<BattleDecision> Decisions => _decisions;

        public int TimeLimitTicks => _setup.TimeLimitTicks;
        public bool IsOverheated => Tick > _setup.TimeLimitTicks;
        public int OverheatLevel { get; private set; }
        public int BonusGold { get; private set; }

        public Battle(BattleSetup setup)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            if (setup.Player == null) throw new ArgumentException("Spieler fehlt.");
            if (setup.Enemies == null || setup.Enemies.Count == 0) throw new ArgumentException("Mindestens ein Gegner.");

            Random = new Random(setup.Seed);
            Context = setup.Context ?? new BattleContext();

            Player = Add(setup.Player, Side.Player);
            foreach (CombatantSetup enemy in setup.Enemies) _enemies.Add(Add(enemy, Side.Enemy));
        }

        private Combatant Add(CombatantSetup s, Side side)
        {
            var c = new Combatant(s, side, _all.Count) { Battle = this };
            _all.Add(c);

            var rows = new RowRuntime[c.Board.Rows.Count + 1];
            for (int i = 0; i < rows.Length; i++) rows[i] = new RowRuntime(i);
            _rows.Add(c, rows);
            return c;
        }

        public RowRuntime RowState(Combatant c, int index) => _rows[c][index];

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
                Decide();
                outcome = CheckEnd();
                if (outcome.HasValue) break;
            }

            if (!outcome.HasValue)
            {
                Tick = _setup.MaxTicks;
                outcome = BattleOutcome.Timeout;
            }

            Emit(new BattleEvent(Tick, BattleEventKind.BattleEnd, null, null, (int)outcome.Value));
            foreach (Combatant c in _all)
                foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnBattleEnd(this, c, outcome.Value);

            int defeated = 0;
            foreach (Combatant e in _enemies) if (!e.IsAlive) defeated++;

            var labels = new List<string>();
            var skills = new List<string>();
            for (int i = 0; i <= Player.Board.Rows.Count; i++)
            {
                LogicRow row = Player.Board.RowAt(i);
                labels.Add(row.Label);
                skills.Add(row.Skill?.Name ?? "—");
            }

            return new BattleResult(outcome.Value, Tick, Math.Max(0, Player.Hp), Player.MaxHp, defeated, BonusGold, _events, labels,
                skills, fighters) { SurviveTicks = _setup.SurviveTicks, Decisions = _decisions };
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
                c.ReduceCooldowns(1);

                for (int i = c.StatusList.Count - 1; i >= 0; i--)
                {
                    if (i >= c.StatusList.Count) continue;
                    StatusEffect s = c.StatusList[i];
                    BeginActor(s.Source, s.SourceRow);
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
            Emit(new BattleEvent(Tick, BattleEventKind.ActionExecuted, c, target, a.Skill.CountsAsAttack ? 1 : 0, a.Skill.Id, a.RowIndex) { IsRepeat = a.IsRepeat });

            if (!a.Skill.IsBasicAttack && a.Skill.CanBeRepeated) c.LastRepeatableSkill = a.Skill;

            var context = new SkillContext(this, c, target, a.Skill, a.RowIndex);
            BeginActor(c, a.RowIndex);
            foreach (ISkillEffect effect in a.Skill.Effects)
            {
                if (!c.IsAlive) break;
                effect.Apply(context);
            }
            EndActor();
        }

        private void BeginActor(Combatant c, int row)
        {
            _actor = c;
            _actorRow = row;
        }

        private void EndActor()
        {
            _actor = null;
            _actorRow = -1;
        }

        private void Finish(Combatant c, ActionState a)
        {
            c.Action = null;
            c.LastActionRow = a.RowIndex;
            c.LastActionEndTick = Tick;

            // Wiederholung (Echo): eigene Ausführung mit eigener Cast-Zeit, direkt im Anschluss.
            if (a.FollowUp != null && c.IsAlive && !c.IsStunned && OpponentsOf(c).Count > 0)
                StartAction(c, a.FollowUp, a.Target, a.RowIndex, repeat: true);
        }

        /// <summary>
        /// Merkt eine Wiederholung für die laufende Aktion vor. Sie startet nach deren Erholung als eigene Ausführung
        /// mit voller Cast-Zeit (ohne Cooldown). Keine Ausführung ohne Cast.
        /// </summary>
        public void QueueRepeat(Combatant c, SkillDefinition skill)
        {
            if (c?.Action == null || skill == null) return;
            c.Action.FollowUp = skill;
        }

        private void Decide()
        {
            foreach (Combatant c in _all)
            {
                if (!c.IsAlive || c.IsStunned) continue;
                bool basicWindup = c.Action != null && c.Action.InWindup && c.Action.Skill.IsBasicAttack;
                if (c.Action != null && !basicWindup)
                {
                    if (c == Player && OpponentsOf(c).Count > 0) RecordBusy(c);
                    continue;
                }
                if (OpponentsOf(c).Count == 0) continue;

                DecideFor(c, basicWindup);
            }
        }

        private void DecideFor(Combatant c, bool basicWindup)
        {
            LogicBoard board = c.Board;
            RowCheckState[] states = c == Player ? new RowCheckState[board.Rows.Count + 1] : null;
            for (int i = 0; i <= board.Rows.Count; i++)
            {
                LogicRow row = board.RowAt(i);
                if (row.IsOrphaned || !c.IsReady(row.Skill))
                {
                    if (states != null) states[i] = row.IsOrphaned ? RowCheckState.Orphaned : RowCheckState.Cooldown;
                    continue;
                }

                RowRuntime state = _rows[c][i];
                if (!row.Condition.IsMet(new ConditionContext(this, c, state), out Combatant target))
                {
                    if (states != null) states[i] = RowCheckState.ConditionFalse;
                    continue;
                }

                // Ein laufender Basisangriff wird nicht durch einen neuen Basisangriff ersetzt.
                if (basicWindup && row.Skill.IsBasicAttack) return;

                if (c.Action != null) Interrupt(c);
                StartAction(c, row.Skill, target, i);
                if (states != null) RecordDecision(c, i, states);
                return;
            }
        }

        // ------------------------------------------------------------------ Entscheidungs-Protokoll (nur Spieler, ändert nichts am Kampf)

        /// <summary>
        /// Hält eine Entscheidung fest: Gründe der Zeilen darüber stehen schon in <paramref name="states"/>, die Zeilen
        /// darunter werden zur Anzeige nur geprüft. Bedingungen sind zustandslos, das Prüfen ändert also nichts.
        /// </summary>
        private void RecordDecision(Combatant c, int chosen, RowCheckState[] states)
        {
            for (int i = chosen + 1; i < states.Length; i++) states[i] = Probe(c, i);
            _decisions.Add(new BattleDecision(Tick, chosen, Checks(c, states)));
        }

        /// <summary>
        /// Während eine Aktion läuft, die keine Zeile abbrechen darf: Zeilen über der laufenden, die bereit und erfüllt
        /// sind, einmal pro Aktion als «Aktion läuft» festhalten. Zeilen darunter hätten ohnehin nicht Vorrang.
        /// </summary>
        private void RecordBusy(Combatant c)
        {
            if (c.IsStunned || c.Action == null) return;
            if (_busyAction != c.Action)
            {
                _busyAction = c.Action;
                _busyRows.Clear();
            }

            RowCheckState[] states = null;
            int above = Math.Min(c.Action.RowIndex, c.Board.Rows.Count + 1);
            for (int i = 0; i < above; i++)
            {
                if (_busyRows.Contains(i) || Probe(c, i) != RowCheckState.Ready) continue;
                if (states == null)
                {
                    states = new RowCheckState[c.Board.Rows.Count + 1];
                    for (int j = 0; j < states.Length; j++) states[j] = Probe(c, j);
                }
                states[i] = RowCheckState.ActionRunning;
                _busyRows.Add(i);
            }
            if (states == null) return;
            // Schon gemeldete Zeilen stehen hier als bereit und zählen nicht doppelt.
            _decisions.Add(new BattleDecision(Tick, -1, Checks(c, states), c.Action.RowIndex));
        }

        private RowCheckState Probe(Combatant c, int index)
        {
            LogicRow row = c.Board.RowAt(index);
            if (row.IsOrphaned) return RowCheckState.Orphaned;
            if (!c.IsReady(row.Skill)) return RowCheckState.Cooldown;
            return row.Condition.IsMet(new ConditionContext(this, c, _rows[c][index]), out _) ? RowCheckState.Ready : RowCheckState.ConditionFalse;
        }

        private RowCheck[] Checks(Combatant c, RowCheckState[] states)
        {
            var checks = new RowCheck[states.Length];
            for (int i = 0; i < states.Length; i++)
            {
                LogicRow row = c.Board.RowAt(i);
                if (row.IsOrphaned)
                {
                    checks[i] = new RowCheck(states[i], 0, 0);
                    continue;
                }
                bool met = row.Condition.IsMet(new ConditionContext(this, c, _rows[c][i]), out _);
                checks[i] = new RowCheck(states[i], c.Cooldown(row.Skill.Id), row.Skill.CooldownTicks, met);
            }
            return checks;
        }

        /// <summary>
        /// Cast-Zeit und Erholung einer Aktion. Der Basisangriff teilt sein Intervall 2:1 auf, andere Skills nutzen ihre
        /// Cast-Zeit mit allen Änderungen. Beide nie unter <paramref name="minCastTicks"/>.
        /// </summary>
        public static void ActionTiming(SkillDefinition skill, int attackIntervalTicks, out int windup, out int recovery,
            int minCastTicks = CastTime.DefaultMinTicks)
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
                windup = skill.CastTicks(minCastTicks);
                recovery = skill.RecoveryTicks;
            }
        }

        /// <summary>
        /// Startet eine Aktion mit voller Cast-Zeit. Öffentlich für Effekte, die Aktionen auslösen.
        /// <paramref name="repeat"/>: Wiederholung (Echo), setzt keinen Cooldown und zählt nicht als Feuern der Zeile.
        /// </summary>
        public void StartAction(Combatant c, SkillDefinition skill, Combatant target, int rowIndex, bool repeat = false)
        {
            ActionTiming(skill, c.AttackIntervalTicks, out int windup, out int recovery, _setup.MinCastTicks);

            c.Action = new ActionState
            {
                Skill = skill,
                Target = target ?? DefaultTarget(c),
                RowIndex = rowIndex,
                StartTick = Tick,
                WindupLeft = windup,
                RecoveryLeft = recovery,
                IsRepeat = repeat,
            };
            if (!repeat) c.SetCooldown(skill.Id, skill.CooldownTicks);

            if (!repeat && rowIndex >= 0 && rowIndex < _rows[c].Length)
            {
                RowRuntime state = _rows[c][rowIndex];
                state.LastFiredTick = Tick;
                state.FireCount++;
            }

            Emit(new BattleEvent(Tick, BattleEventKind.ActionStarted, c, c.Action.Target, windup, skill.Id, rowIndex) { IsRepeat = repeat });
            foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnActionStarted(this, c, skill, rowIndex);
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

        /// <summary>Bricht die laufende Aktion ab (kein Cooldown-Rückerstatten).</summary>
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
            Emit(new BattleEvent(Tick, hit.IsSelfDamage ? BattleEventKind.SelfDamage : BattleEventKind.Damage, hit.Source, target, dealt, hit.SkillId));

            if (target.Hp <= 0)
            {
                target.Hp = 0;
                target.Action = null;
                target.StatusList.Clear();
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
            Emit(new BattleEvent(Tick, BattleEventKind.Healed, source ?? target, target, healed, detail));
            return healed;
        }

        public void ApplyStatus(Combatant target, StatusEffect status, Combatant source)
        {
            if (target == null || !target.IsAlive || status == null || status.TicksLeft <= 0) return;
            status.Source = source;
            status.SourceRow = source != null && source == _actor ? _actorRow : -1;

            if (!status.Stacks) target.StatusList.RemoveAll(s => s.Id == status.Id);
            target.StatusList.Add(status);
            int stacks = 0;
            foreach (StatusEffect s in target.StatusList) if (s.Id == status.Id) stacks++;
            Emit(new BattleEvent(Tick, BattleEventKind.StatusApplied, source, target, status.TicksLeft, status.Id) { Extra = stacks });

            if (status.Stuns) Interrupt(target);
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
