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

            int defeated = 0;
            foreach (Combatant e in _enemies) if (!e.IsAlive) defeated++;

            var labels = new List<string>();
            foreach (LogicRow row in Player.Board.Rows) labels.Add(row.Label);
            labels.Add(Player.Board.Fallback.Label);

            return new BattleResult(outcome.Value, Tick, Math.Max(0, Player.Hp), Player.MaxHp, defeated, BonusGold, _events, labels);
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
                    s.OnTick(this, c);
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
            Emit(new BattleEvent(Tick, BattleEventKind.ActionExecuted, c, target, a.Skill.CountsAsAttack ? 1 : 0, a.Skill.Id, a.RowIndex));

            if (!a.Skill.IsBasicAttack && a.Skill.CanBeRepeated) c.LastRepeatableSkill = a.Skill;

            var context = new SkillContext(this, c, target, a.Skill, a.RowIndex);
            foreach (ISkillEffect effect in a.Skill.Effects)
            {
                if (!c.IsAlive) break;
                effect.Apply(context);
            }
        }

        private void Finish(Combatant c, ActionState a)
        {
            c.Action = null;
            c.LastActionRow = a.RowIndex;
            c.LastActionEndTick = Tick;
        }

        private void Decide()
        {
            foreach (Combatant c in _all)
            {
                if (!c.IsAlive || c.IsStunned) continue;
                bool basicWindup = c.Action != null && c.Action.InWindup && c.Action.Skill.IsBasicAttack;
                if (c.Action != null && !basicWindup) continue;
                if (OpponentsOf(c).Count == 0) continue;

                DecideFor(c, basicWindup);
            }
        }

        private void DecideFor(Combatant c, bool basicWindup)
        {
            LogicBoard board = c.Board;
            for (int i = 0; i <= board.Rows.Count; i++)
            {
                LogicRow row = board.RowAt(i);
                if (row.IsOrphaned || !c.IsReady(row.Skill)) continue;

                RowRuntime state = _rows[c][i];
                if (!row.Condition.IsMet(new ConditionContext(this, c, state), out Combatant target)) continue;

                // Ein laufender Basisangriff wird nicht durch einen neuen Basisangriff ersetzt.
                if (basicWindup && row.Skill.IsBasicAttack) return;

                if (c.Action != null) Interrupt(c);
                StartAction(c, row.Skill, target, i);
                return;
            }
        }

        /// <summary>Startet eine Aktion. Öffentlich für Effekte, die Aktionen auslösen (z. B. Wiederholen).</summary>
        public void StartAction(Combatant c, SkillDefinition skill, Combatant target, int rowIndex)
        {
            int windup, recovery;
            if (skill.IsBasicAttack)
            {
                int interval = c.AttackIntervalTicks;
                windup = Math.Max(1, interval * 2 / 3);
                recovery = Math.Max(0, interval - windup);
            }
            else
            {
                windup = Math.Max(1, skill.WindupTicks);
                recovery = skill.RecoveryTicks;
            }

            c.Action = new ActionState
            {
                Skill = skill,
                Target = target ?? DefaultTarget(c),
                RowIndex = rowIndex,
                StartTick = Tick,
                WindupLeft = windup,
                RecoveryLeft = recovery,
            };
            c.SetCooldown(skill.Id, skill.CooldownTicks);

            if (rowIndex >= 0 && rowIndex < _rows[c].Length)
            {
                RowRuntime state = _rows[c][rowIndex];
                state.LastFiredTick = Tick;
                state.FireCount++;
            }

            Emit(new BattleEvent(Tick, BattleEventKind.ActionStarted, c, c.Action.Target, windup, skill.Id, rowIndex));
            foreach (BattleModifier m in c.ModifierList.ToArray()) m.OnActionStarted(this, c, skill, rowIndex);
        }

        private BattleOutcome? CheckEnd()
        {
            if (!Player.IsAlive) return BattleOutcome.Defeat;
            foreach (Combatant e in _enemies) if (e.IsAlive) return null;
            return BattleOutcome.Victory;
        }

        // ------------------------------------------------------------------ Werkzeuge für Effekte, Bedingungen, Modifikatoren

        public void Emit(BattleEvent e)
        {
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

            if (!status.Stacks) target.StatusList.RemoveAll(s => s.Id == status.Id);
            target.StatusList.Add(status);
            Emit(new BattleEvent(Tick, BattleEventKind.StatusApplied, source, target, status.TicksLeft, status.Id));

            if (status.Stuns) Interrupt(target);
        }

        /// <summary>Ändert einen Ressourcen-Zähler innerhalb von [min, max]. Gibt den neuen Wert zurück.</summary>
        public int ChangeResource(Combatant c, string id, int delta, int max = int.MaxValue, int min = 0)
        {
            int before = c.GetResource(id);
            int after = Math.Max(min, Math.Min(max, before + delta));
            if (after == before) return after;
            c.SetResourceRaw(id, after);
            Emit(new BattleEvent(Tick, BattleEventKind.ResourceChanged, c, c, after, id));
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
