using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Ein aktiver Zustand in der Wiedergabe: Restdauer und Stapel.</summary>
    public sealed class StatusView
    {
        public string Id { get; }
        public int Stacks { get; internal set; }
        public int StartTick { get; internal set; }
        public int EndTick { get; internal set; }

        internal StatusView(string id) => Id = id;

        public int TicksLeft(int tick) => Math.Max(0, EndTick - tick);

        /// <summary>Verbleibender Anteil 0–1 für den Balken.</summary>
        public float Remaining(int tick) => EndTick <= StartTick ? 0f : Math.Min(1f, TicksLeft(tick) / (float)(EndTick - StartTick));
    }

    /// <summary>Ein Ressourcen-Zähler in der Wiedergabe. Ohne feste Obergrenze dient der Höchststand als Massstab.</summary>
    public sealed class ResourceView
    {
        public string Id { get; }
        public int Value { get; internal set; }

        /// <summary>Feste Obergrenze, 0 = offen.</summary>
        public int Max { get; internal set; }
        public int Peak { get; internal set; }

        internal ResourceView(string id) => Id = id;

        public float Fill => Value <= 0 ? 0f : Math.Min(1f, Value / (float)Math.Max(1, Max > 0 ? Max : Math.Max(Peak, 5)));
    }

    public enum PopupKind
    {
        Damage,
        Crit,
        Heal,
        Blocked,
        Dodged,
        SelfDamage,
    }

    /// <summary>Schwebende Zahl oder schwebendes Wort am Ziel. Row = auslösende Zeile des Spielers, sonst -1.</summary>
    public readonly struct Popup
    {
        public readonly int Tick;
        public readonly int TargetIndex;
        public readonly PopupKind Kind;
        public readonly int Amount;
        public readonly int Row;
        public readonly bool FromPlayer;

        public Popup(int tick, int targetIndex, PopupKind kind, int amount, int row, bool fromPlayer)
        {
            Tick = tick;
            TargetIndex = targetIndex;
            Kind = kind;
            Amount = amount;
            Row = row;
            FromPlayer = fromPlayer;
        }

        public string Text
        {
            get
            {
                switch (Kind)
                {
                    case PopupKind.Heal: return $"+{Amount}";
                    case PopupKind.Blocked: return ArenaTexts.PopupBlocked;
                    case PopupKind.Dodged: return ArenaTexts.PopupDodged;
                    case PopupKind.Crit: return $"{Amount}!";
                    default: return Amount.ToString();
                }
            }
        }
    }

    [Flags]
    public enum LogCategory
    {
        None = 0,

        /// <summary>Aktionen, Abbrüche, Zustände und Heilung, die vom Spieler ausgehen.</summary>
        Mine = 1,

        /// <summary>Schaden, Krit, Block, Ausweichen.</summary>
        Damage = 2,
    }

    public enum LogFilter
    {
        All,
        Mine,
        Damage,
    }

    /// <summary>Eine Protokollzeile mit Kategorie für den Filter.</summary>
    public readonly struct LogEntry
    {
        public readonly int Tick;
        public readonly string Text;
        public readonly LogCategory Category;
        public readonly int Row;

        public LogEntry(int tick, string text, LogCategory category, int row)
        {
            Tick = tick;
            Text = text;
            Category = category;
            Row = row;
        }

        public bool Matches(LogFilter filter) =>
            filter == LogFilter.All || (filter == LogFilter.Mine && (Category & LogCategory.Mine) != 0)
            || (filter == LogFilter.Damage && (Category & LogCategory.Damage) != 0);
    }

    /// <summary>Live-Zustand einer Komponente der Platine in der Wiedergabe (A-19).</summary>
    public enum RowDisplay
    {
        /// <summary>Versorgt, wartet auf das Auslösen ihres Relais.</summary>
        Idle,

        /// <summary>Eingereiht: ihr Relais hat ausgelöst, sie wartet, bis sie dran ist.</summary>
        Queued,

        /// <summary>Feuert gerade (Cast läuft).</summary>
        Firing,

        /// <summary>Eingefroren (Freeze).</summary>
        Frozen,

        /// <summary>Kein Relais berührt sie.</summary>
        Unpowered,

        /// <summary>Berührt nur Relais, für die sie zu gross ist.</summary>
        TooLarge,

        /// <summary>Kein Skill.</summary>
        Orphaned,
    }

    /// <summary>Eine wartende Komponente des Spielers in der Wiedergabe.</summary>
    public sealed class QueueView
    {
        public int Row { get; }
        public string Skill { get; }

        /// <summary>Tick des Einreihens.</summary>
        public int SinceTick { get; }

        /// <summary>Eingereiht über ein Auslöser-Modul.</summary>
        public bool Triggered { get; }

        /// <summary>Relais, das ausgelöst hat (-1 bei Auslöser-Modulen).</summary>
        public int Relay { get; }

        internal QueueView(int row, string skill, int sinceTick, bool triggered, int relay)
        {
            Row = row;
            Skill = skill;
            SinceTick = sinceTick;
            Triggered = triggered;
            Relay = relay;
        }

        public int WaitedTicks(int tick) => Math.Max(0, tick - SinceTick);
    }

    /// <summary>Zustand eines Kämpfers zu einem Zeitpunkt der Wiedergabe.</summary>
    public sealed class FighterView
    {
        internal readonly List<StatusView> StatusList = new List<StatusView>();
        internal readonly List<ResourceView> ResourceList = new List<ResourceView>();
        internal int PendingCritTick = -1;

        /// <summary>Aktive Zustände, älteste zuerst.</summary>
        public IReadOnlyList<StatusView> Statuses => StatusList;

        /// <summary>Ressourcen (Hitze, Ladung, Tempo ...), sobald sie einmal über 0 waren.</summary>
        public IReadOnlyList<ResourceView> Resources => ResourceList;

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

        /// <summary>Warum die laufende Aktion startete: Tafel, Wiederholung oder Auslöser (dann <see cref="ActionCauseRow"/>).</summary>
        public ActionCause ActionCause { get; internal set; }
        public int ActionCauseRow { get; internal set; } = -1;

        /// <summary>Tick, an dem zuletzt eine Wirkung eintraf (für Treffer-Blitze).</summary>
        public int LastHitTick { get; internal set; } = -1000;

        internal FighterView(FighterInfo info)
        {
            Info = info;
            Hp = info.StartHp;
            var ids = new List<string>(info.StartResources.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) SetResource(id, info.StartResources[id], 0);
        }

        internal void SetResource(string id, int value, int max)
        {
            ResourceView view = ResourceList.Find(r => r.Id == id);
            if (view == null)
            {
                if (value == 0) return;
                view = new ResourceView(id);
                ResourceList.Add(view);
            }
            view.Value = value;
            if (max > 0) view.Max = max;
            view.Peak = Math.Max(view.Peak, value);
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
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly List<Popup> _popups = new List<Popup>();
        private int _next;
        private readonly string[] _skipReasons;
        private readonly int[] _skipTicks;
        private readonly int[] _frozenUntil;
        private readonly int[] _relayTicks;
        private readonly int[] _relayCounts;
        private readonly List<QueueView> _queue = new List<QueueView>();

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

        /// <summary>Protokoll mit Kategorien (inklusive Schadenszeilen) für den Filter.</summary>
        public IReadOnlyList<LogEntry> Entries => _entries;

        public BattlePlayback(BattleResult result)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
            foreach (FighterInfo f in result.Fighters)
            {
                var view = new FighterView(f);
                _fighters.Add(view);
                _byCombatant[f.Combatant] = view;
            }
            _skipReasons = new string[result.PlayerRowLabels.Count];
            _skipTicks = new int[result.PlayerRowLabels.Count];
            _frozenUntil = new int[result.PlayerRowLabels.Count];
            _relayTicks = new int[result.PlayerBoard?.Relays.Count ?? 0];
            _relayCounts = new int[_relayTicks.Length];
            for (int i = 0; i < _skipTicks.Length; i++) _skipTicks[i] = -1;
            for (int i = 0; i < _relayTicks.Length; i++) _relayTicks[i] = -1000;
        }

        /// <summary>Holt die schwebenden Zahlen, die seit dem letzten Aufruf entstanden sind.</summary>
        public List<Popup> TakePopups()
        {
            var taken = new List<Popup>(_popups);
            _popups.Clear();
            return taken;
        }

        /// <summary>Wartende Komponenten des Spielers in Lesereihenfolge der Platine (oben links zuerst).</summary>
        public IReadOnlyList<QueueView> Queue => _queue;

        public bool IsRowQueued(int row) => _queue.Exists(q => q.Row == row);

        /// <summary>«Waiting: #2 Shield Bash ⏳0.4 s · #4 Drill Thrust» oder leer. <paramref name="hourglass"/> für Schriften ohne ⏳.</summary>
        public string QueueText(string hourglass = "⏳")
        {
            if (_queue.Count == 0) return string.Empty;
            var parts = new List<string>();
            foreach (QueueView q in _queue)
            {
                int waited = q.WaitedTicks(Tick);
                parts.Add(ArenaTexts.ComponentName(q.Row, q.Skill) + (waited > 0 ? $" {hourglass}{RowStateText.Seconds(waited)}" : string.Empty));
            }
            return ArenaTexts.QueuePrefix + string.Join(" · ", parts);
        }

        /// <summary>Live-Zustand einer Komponente des Spielers.</summary>
        public RowDisplay RowStateAt(int row)
        {
            LogicBoard board = _result.PlayerBoard;
            if (board != null && row >= 0 && row < board.Rows.Count)
            {
                LogicRow r = board.Rows[row];
                if (r.IsOrphaned) return RowDisplay.Orphaned;
                if (!r.IsPowered) return r.TooLargeFor.Count > 0 ? RowDisplay.TooLarge : RowDisplay.Unpowered;
            }
            FighterView player = Player;
            if (player != null && player.ActionSkill != null && player.ActionRow == row) return RowDisplay.Firing;
            if (FrozenLeft(row) > 0) return RowDisplay.Frozen;
            if (IsRowQueued(row)) return RowDisplay.Queued;
            return RowDisplay.Idle;
        }

        /// <summary>Restdauer eines Freeze auf einer Komponente des Spielers in Ticks.</summary>
        public int FrozenLeft(int row) => row >= 0 && row < _frozenUntil.Length ? Math.Max(0, _frozenUntil[row] - Tick) : 0;

        /// <summary>Leuchtet das Relais gerade (hat eben ausgelöst)?</summary>
        public bool IsRelayLit(int relay) => relay >= 0 && relay < _relayTicks.Length && Tick - _relayTicks[relay] < RowHighlightTicks;

        /// <summary>Wie oft hat ein Relais des Spielers bis jetzt ausgelöst?</summary>
        public int RelayCount(int relay) => relay >= 0 && relay < _relayCounts.Length ? _relayCounts[relay] : 0;

        /// <summary>Letzter «Missed Trigger» einer Komponente mit Zeitpunkt und Grund, oder null.</summary>
        public string LastSkipReason(int row) =>
            row >= 0 && row < _skipReasons.Length && _skipReasons[row] != null
                ? $"{BattleLogText.Time(_skipTicks[row])}: {_skipReasons[row]}"
                : null;

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
            {
                if (f.StunnedUntil > 0 && Tick >= f.StunnedUntil) f.StunnedUntil = 0;
                f.StatusList.RemoveAll(st => st.EndTick <= Tick);
            }
        }

        public void SkipToEnd() => Advance(int.MaxValue / 2);

        private void Log(BattleEvent e, LogCategory category, bool classic = true)
        {
            string text = BattleLogText.Describe(e, _result);
            if (classic) _lines.Add(text);
            if (e.Source != null && e.Source.Side == Side.Player && e.Kind != BattleEventKind.Damage) category |= LogCategory.Mine;
            _entries.Add(new LogEntry(e.Tick, text, category, e.Source != null && e.Source.Side == Side.Player ? e.RowIndex : -1));
        }

        private void AddPopup(BattleEvent e, FighterView target, PopupKind kind)
        {
            if (target == null) return;
            bool fromPlayer = e.Source != null && e.Source.Side == Side.Player;
            _popups.Add(new Popup(e.Tick, _fighters.IndexOf(target), kind, e.Amount, fromPlayer ? e.RowIndex : -1, fromPlayer));
        }

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
                    source.ActionCause = e.Cause;
                    source.ActionCauseRow = e.CauseRow;
                    if (source.Info.Side == Side.Player)
                    {
                        if (e.FromQueue)
                        {
                            int at = _queue.FindIndex(q => q.Row == e.RowIndex);
                            if (at >= 0) _queue.RemoveAt(at);
                        }
                        LastPlayerRow = e.RowIndex;
                        LastPlayerRowTick = e.Tick;
                    }
                    if (e.Detail != SkillDefinition.BasicAttackId) Log(e, LogCategory.None);
                    else if (source.Info.Side == Side.Player) Log(e, LogCategory.None, classic: false);
                    break;

                case BattleEventKind.ActionExecuted:
                    if (source != null) source.ActionWindupTicks = 0;
                    break;

                case BattleEventKind.TriggerMissed:
                    if (source == null || source.Info.Side != Side.Player) break;
                    if (e.RowIndex >= 0 && e.RowIndex < _skipReasons.Length)
                    {
                        _skipReasons[e.RowIndex] = RowStateText.Reason((MissReason)e.Amount);
                        _skipTicks[e.RowIndex] = e.Tick;
                    }
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.RelayTriggered:
                    if (source == null || source.Info.Side != Side.Player) break;
                    if (e.Relay >= 0 && e.Relay < _relayTicks.Length)
                    {
                        _relayTicks[e.Relay] = e.Tick;
                        _relayCounts[e.Relay]++;
                    }
                    break;

                case BattleEventKind.Frozen:
                    if (target != null && target.Info.Side == Side.Player && e.Extra >= 0 && e.Extra < _frozenUntil.Length)
                        _frozenUntil[e.Extra] = Math.Max(_frozenUntil[e.Extra], e.Tick + e.Amount);
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.RowQueued:
                    // Nur die Warteschlange des Spielers wird gezeigt; Gegner haben auch eine, das Protokoll bliebe sonst unlesbar.
                    if (source == null || source.Info.Side != Side.Player) break;
                    string name = e.RowIndex >= 0 && e.RowIndex < _result.PlayerRowSkills.Count ? _result.PlayerRowSkills[e.RowIndex] : BattleLogText.SkillName(e.Detail);
                    var entry = new QueueView(e.RowIndex, name, e.Tick, e.IsTriggered, e.Relay);
                    int index = _queue.FindIndex(q => q.Row > e.RowIndex);
                    if (index < 0) _queue.Add(entry);
                    else _queue.Insert(index, entry);
                    Log(e, LogCategory.None, classic: false);
                    break;

                case BattleEventKind.ActionInterrupted:
                    if (source != null) source.ActionSkill = null;
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.Damage:
                case BattleEventKind.SelfDamage:
                    if (target == null) break;
                    target.Hp = Math.Max(0, target.Hp - e.Amount);
                    target.LastHitTick = e.Tick;
                    bool crit = e.Kind == BattleEventKind.Damage && target.PendingCritTick == e.Tick;
                    target.PendingCritTick = -1;
                    AddPopup(e, target, e.Kind == BattleEventKind.SelfDamage ? PopupKind.SelfDamage : crit ? PopupKind.Crit : PopupKind.Damage);
                    Log(e, LogCategory.Damage, classic: false);
                    break;

                case BattleEventKind.Healed:
                    if (target != null) target.Hp = Math.Min(target.Info.MaxHp, target.Hp + e.Amount);
                    AddPopup(e, target, PopupKind.Heal);
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.StatusApplied:
                    if (target != null)
                    {
                        if (e.Detail == StatusIds.Stun) target.StunnedUntil = e.Tick + e.Amount;
                        StatusView status = target.StatusList.Find(st => st.Id == e.Detail);
                        if (status == null)
                        {
                            status = new StatusView(e.Detail);
                            target.StatusList.Add(status);
                        }
                        status.Stacks = Math.Max(1, e.Extra);
                        status.StartTick = e.Tick;
                        status.EndTick = Math.Max(status.EndTick, e.Tick + e.Amount);
                    }
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.StatusExpired:
                    if (target != null)
                    {
                        StatusView status = target.StatusList.Find(st => st.Id == e.Detail);
                        if (status != null && --status.Stacks <= 0) target.StatusList.Remove(status);
                    }
                    break;

                case BattleEventKind.ResourceChanged:
                    source?.SetResource(e.Detail, e.Amount, e.Extra);
                    break;

                case BattleEventKind.Death:
                    if (target != null && target.Info.Side == Side.Player) _queue.Clear();
                    if (target != null)
                    {
                        target.Hp = 0;
                        target.ActionSkill = null;
                        target.StatusList.Clear();
                    }
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.Crit:
                    if (target != null) target.PendingCritTick = e.Tick;
                    Log(e, LogCategory.Damage);
                    break;

                case BattleEventKind.Dodged:
                    AddPopup(e, target, PopupKind.Dodged);
                    Log(e, LogCategory.Damage);
                    break;

                case BattleEventKind.Blocked:
                    AddPopup(e, target, PopupKind.Blocked);
                    Log(e, LogCategory.Damage);
                    break;

                case BattleEventKind.BattleEnd:
                    _queue.Clear();
                    Log(e, LogCategory.None);
                    break;

                case BattleEventKind.Overheat:
                    Log(e, LogCategory.None);
                    break;
            }
        }
    }
}
