using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Circuit;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Autoplay
{
    /// <summary>
    /// Füllt einen <see cref="AutoplayReport"/> aus den Ereignissen der Session (Kämpfe, Boss, Duos) und den
    /// ausgeführten Bot-Aktionen. Über Akte hinweg wird die jeweils neue Session per <see cref="Attach"/> verbunden.
    /// </summary>
    public sealed class AutoplayRecorder
    {
        private enum FightKind { Normal, Elite, Mine, Boss }

        private OverworldSession _session;
        private readonly List<(FightKind kind, bool victory)> _pending = new List<(FightKind, bool)>();
        private readonly List<string> _discoveredDuos = new List<string>();

        /// <summary>Nach <see cref="OverworldSession.BossEncountered"/>: das nächste grosse Ereignis ist das Boss-Ergebnis.</summary>
        private bool _awaitingBossOutcome;

        public AutoplayReport Report { get; }

        public AutoplayRecorder(AutoplayReport report)
        {
            Report = report ?? new AutoplayReport();
        }

        public void Attach(OverworldSession session)
        {
            Flush();
            Detach();
            _session = session;
            _awaitingBossOutcome = false;
            if (_session == null) return;
            _session.CombatFinished += OnCombat;
            _session.BossEncountered += OnBoss;
            _session.MajorEventResolved += OnMajorEvent;
            _session.DuoDiscovered += OnDuo;
            Report.Act = _session.Act;
        }

        public void Detach()
        {
            if (_session == null) return;
            _session.CombatFinished -= OnCombat;
            _session.BossEncountered -= OnBoss;
            _session.MajorEventResolved -= OnMajorEvent;
            _session.DuoDiscovered -= OnDuo;
            _session = null;
        }

        /// <summary>Nach jeder ausgeführten Aktion.</summary>
        public void Record(BotAction action, bool ok)
        {
            Report.Actions++;
            if (ok && action != null)
            {
                Report.AddReward(action.RewardKind);
                if (action.SetsTrigger) Report.TriggersSet++;
            }
            Flush();
            Snapshot();
        }

        /// <summary>Schliesst den Bericht: Endgrund, Tafel, Module, Auslöser, Duos.</summary>
        public void Finish(string reason)
        {
            Flush();
            Snapshot();
            Report.EndReason = reason ?? string.Empty;
            OverworldSession s = _session;
            if (s != null)
            {
                Report.BoardRows.Clear();
                for (int i = 0; i < s.Board.Components.Count; i++) Report.BoardRows.Add(ComponentText(s, i));
                for (int i = 0; i < s.Board.Relays.Count; i++) Report.BoardRows.Add(RelayText(s, i));

                Report.Modules.Clear();
                foreach (ModuleInstance m in s.Modules.All.Where(m => !m.IsFree))
                {
                    string text = $"{m.NameFrom(s.ModuleCatalog)} @ {s.ModuleWhere(m)}";
                    if (m.ModuleId == ModuleIds.Trigger) text += $" ({s.DescribeTrigger(m)})";
                    Report.Modules.Add(text);
                }
                Report.TriggerLinks = s.TriggerLinks().Count;

                Report.Duos.Clear();
                foreach (string name in _discoveredDuos) Report.Duos.Add(name);
                foreach (SynergyDuo duo in s.ActiveDuos())
                    if (!Report.Duos.Contains(duo.Name)) Report.Duos.Add(duo.Name);
            }
            Detach();
        }

        /// <summary>«#1 Shock Stab 1×1 @(1, 0) ← On Hit [Area]» bzw. «… not powered».</summary>
        private static string ComponentText(OverworldSession s, int i)
        {
            ComponentSlot c = s.Board.Components[i];
            string skill = c.Skill == null ? AutoplayTexts.EmptyRow : c.Skill.NameFrom(s.SkillCatalog);
            List<RelayChip> powering = s.PoweringRelays(c);
            string power = powering.Count > 0 ? string.Join(" / ", powering.Select(r => r.Name))
                : s.IsTooLarge(c) ? ArenaTexts.NotPoweredTooLarge : ArenaTexts.NotPowered;
            var modules = c.Skill != null ? new List<ModuleInstance>(c.Skill.Modules) : new List<ModuleInstance>();
            string extra = modules.Count == 0 ? string.Empty : " [" + string.Join(", ", modules.Select(m => m.NameFrom(s.ModuleCatalog))) + "]";
            return $"#{i + 1} {skill} {c.Shape} @{c.Origin} ← {power}{extra}";
        }

        /// <summary>«Relay 1: On Hit @(0, 0), max 2 cells [Invert]».</summary>
        private static string RelayText(OverworldSession s, int i)
        {
            RelayChip r = s.Board.Relays[i];
            string extra = r.Modules.Count == 0 ? string.Empty : " [" + string.Join(", ", r.Modules.Select(m => m.NameFrom(s.ModuleCatalog))) + "]";
            return $"{CatalogTexts.RelayHolder(i)}: {r.Name} @{r.Position}, {CatalogTexts.Cells(s.RelayMaxCells(r))}{extra}";
        }

        private void Snapshot()
        {
            if (_session == null) return;
            Report.Act = _session.Act;
            Report.Turns = _session.Turns.CurrentTurn;
            Report.Hp = _session.Stats.Hp;
            Report.MaxHp = _session.Stats.MaxHp;
            Report.Gold = _session.Stats.Gold;
        }

        private void OnCombat(CombatResult result)
        {
            FightKind kind = FightKind.Normal;
            HexCell cell = _session?.CurrentCell;
            if (cell != null && cell.Content == CellContent.Elite) kind = FightKind.Elite;
            else if (cell != null && cell.Content == CellContent.GoldMine) kind = FightKind.Mine;
            _pending.Add((kind, result.Victory));

            if (result.Battle != null && _session != null)
            {
                BattleReport report = BattleReport.Create(result.Battle);
                RuneCatalog catalog = _session.RuneCatalog;
                RuneFireStats.Record(Report.RuneStats, result.Battle,
                    id => catalog.TryGet(id, out RuneDefinition rune) ? (rune.Name, rune.Difficulty) : (id, 0));
                Report.AddDamage(_session.Act, report.BasicAttackDamage, report.TotalDamage);
            }
        }

        private void OnBoss(CombatResult result)
        {
            Report.Bosses++;
            _awaitingBossOutcome = true;
            if (_pending.Count > 0) _pending[_pending.Count - 1] = (FightKind.Boss, _pending[_pending.Count - 1].victory);
        }

        private void OnMajorEvent(MajorEventOutcome outcome)
        {
            // Unabhängig vom (übersetzten) Titel: das erste Ereignis nach dem Boss-Kampf ist sein Ergebnis;
            // überlebt hat der Ritter, wenn er danach nicht tot ist (Sieg oder durchs Portal entkommen).
            if (!_awaitingBossOutcome) return;
            _awaitingBossOutcome = false;
            if (_session != null && !_session.Stats.IsDead) Report.BossesSurvived++;
        }

        private void OnDuo(SynergyDuo duo)
        {
            if (!_discoveredDuos.Contains(duo.Name)) _discoveredDuos.Add(duo.Name);
        }

        /// <summary>Ein Kampf zählt als verloren, wenn der Resolver verlor oder der Ritter danach tot ist (letzter Kampf).</summary>
        private void Flush()
        {
            if (_pending.Count == 0) return;
            bool dead = _session != null && _session.IsGameOver;
            for (int i = 0; i < _pending.Count; i++)
            {
                (FightKind kind, bool victory) = _pending[i];
                bool won = victory && !(dead && i == _pending.Count - 1);
                switch (kind)
                {
                    case FightKind.Boss: break;
                    case FightKind.Elite:
                        if (won) Report.ElitesWon++;
                        else Report.ElitesLost++;
                        break;
                }
                if (kind == FightKind.Boss) continue;
                if (won) Report.FightsWon++;
                else Report.FightsLost++;
            }
            _pending.Clear();
        }
    }
}
