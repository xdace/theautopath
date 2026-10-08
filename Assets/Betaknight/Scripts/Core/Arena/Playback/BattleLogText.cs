namespace Betaknight.Core.Arena
{
    /// <summary>Lesbare Zeilen für das Kampfprotokoll (Texte in <see cref="ArenaTexts"/>).</summary>
    public static class BattleLogText
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        public static string Time(int tick) => ArenaTexts.LogTime(tick);

        public static string SkillName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            if (Skills.TryGet(id, out SkillDefinition s)) return s.Name;
            return ArenaTexts.DamageSourceName(id) ?? id;
        }

        public static string StatusName(string id) => ArenaTexts.StatusName(id) ?? id;

        public static string Describe(BattleEvent e, BattleResult result = null)
        {
            string who = e.Source?.Name ?? "";
            string whom = e.Target?.Name ?? "";
            string text;
            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted:
                    string row = RowLabel(e, result);
                    text = row != null ? $"{who}: [{row}] → {Component(e.RowIndex, e.Detail)}" : $"{who}: {SkillName(e.Detail)}";
                    if (e.IsTriggered) text += ArenaTexts.LogTriggeredBy(e.CauseRow);
                    else if (e.IsPulse && e.CauseRow >= 0) text += ArenaTexts.LogPulsedBy(e.CauseRow);
                    else if (e.IsRepeat) text += ArenaTexts.LogRepeat;
                    if (e.FromQueue) text += ArenaTexts.LogFromQueue(Time(e.QueuedTicks));
                    break;
                case BattleEventKind.RowQueued:
                    text = e.IsPulse && e.CauseRow >= 0
                        ? ArenaTexts.LogPulseQueued(who, Component(e.RowIndex, e.Detail), ComponentOf(e.Source, e.CauseRow), RelayOf(e.Source, e.Relay))
                        : ArenaTexts.LogQueued(who, Component(e.RowIndex, e.Detail));
                    if (e.IsTriggered) text += ArenaTexts.LogTriggeredBy(e.CauseRow);
                    break;
                case BattleEventKind.TriggerMissed:
                    text = ArenaTexts.LogMissed(who, Component(e.RowIndex, e.Detail), RowStateText.Reason((MissReason)e.Amount));
                    break;
                case BattleEventKind.RelayTriggered:
                    text = ArenaTexts.LogRelay(who, ArenaTexts.RelayName(e.Relay, e.Detail));
                    break;
                case BattleEventKind.Frozen:
                    text = ArenaTexts.LogFrozen(whom, Component(e.Extra, e.Detail), ArenaTexts.Seconds(e.Amount));
                    break;
                case BattleEventKind.PulseSent:
                    text = PulseText(e, who);
                    break;
                case BattleEventKind.CapacitorStored:
                    text = ArenaTexts.LogCapacitorStored(who, ArenaTexts.CapacitorName(e.Extra), e.Amount,
                        e.Source?.Board.ChipConfig.CapacitorCapacity ?? e.Amount);
                    break;
                case BattleEventKind.CapacitorReleased:
                    text = ArenaTexts.LogCapacitorReleased(who, ArenaTexts.CapacitorName(e.Extra), e.Amount);
                    break;
                case BattleEventKind.PulseLost:
                    text = ArenaTexts.LogPulseLost(who, ArenaTexts.CapacitorName(e.Extra));
                    break;
                case BattleEventKind.FuseBlown:
                    text = ArenaTexts.LogFuseBlown(who, ArenaTexts.RelayName(e.Relay, e.Detail));
                    break;
                case BattleEventKind.RelayState:
                    text = ArenaTexts.LogRelayState(who, ArenaTexts.RelayName(e.Relay, e.Detail), e.Amount == 1);
                    break;
                case BattleEventKind.ActionInterrupted: text = ArenaTexts.LogInterrupted(who, SkillName(e.Detail)); break;
                case BattleEventKind.Healed: text = ArenaTexts.LogHealed(whom, e.Amount); break;
                case BattleEventKind.StatusApplied: text = $"{whom} {StatusName(e.Detail)}"; break;
                case BattleEventKind.Death: text = ArenaTexts.LogDeath(whom); break;
                case BattleEventKind.Dodged: text = ArenaTexts.LogDodged(whom); break;
                case BattleEventKind.Blocked: text = ArenaTexts.LogBlocked(whom); break;
                case BattleEventKind.Crit: text = ArenaTexts.LogCrit(who, e.Amount); break;
                case BattleEventKind.Overheat: text = ArenaTexts.LogOverheat(e.Amount); break;
                case BattleEventKind.BattleEnd: text = OutcomeText((BattleOutcome)e.Amount); break;
                case BattleEventKind.Damage:
                case BattleEventKind.SelfDamage: text = $"{whom} −{e.Amount} ({SkillName(e.Detail)})"; break;
                default: text = $"{e.Kind} {who} {whom}".Trim(); break;
            }
            return $"{Time(e.Tick)}  {text}";
        }

        public static string OutcomeText(BattleOutcome outcome)
        {
            switch (outcome)
            {
                case BattleOutcome.Victory: return ArenaTexts.Victory;
                case BattleOutcome.Defeat: return ArenaTexts.Defeat;
                case BattleOutcome.Escaped: return ArenaTexts.Escaped;
                default: return ArenaTexts.TimeUp;
            }
        }

        private static string Component(int index, string skillId) => ArenaTexts.ComponentName(index, SkillName(skillId));

        private static string ComponentOf(Combatant c, int row)
        {
            LogicRow r = c != null && row >= 0 && row < c.Board.Rows.Count ? c.Board.Rows[row] : null;
            return ArenaTexts.ComponentName(row, r?.Skill?.Name ?? "?");
        }

        private static string RelayOf(Combatant c, int relay) =>
            c != null && relay >= 0 && relay < c.Board.Relays.Count ? ArenaTexts.RelayName(relay, c.Board.Relays[relay].Label) : "?";

        private static string NodeName(Combatant c, PulseNode node) =>
            node.IsCapacitor ? ArenaTexts.CapacitorName(node.Index) : ComponentOf(c, node.Index);

        /// <summary>«pulse #1 Shock Stab → #3 Drill via 2 traces».</summary>
        private static string PulseText(BattleEvent e, string who)
        {
            LogicBoard board = e.Source?.Board;
            if (board == null || e.Extra < 0 || e.Extra >= board.Links.Count) return $"{who}: pulse";
            PulseLink link = board.Links[e.Extra];
            return ArenaTexts.LogPulse(who, NodeName(e.Source, link.From), NodeName(e.Source, link.To), link.Delay);
        }

        private static string RowLabel(BattleEvent e, BattleResult result)
        {
            if (result == null || e.Source == null || e.Source.Side != Side.Player) return null;
            if (e.RowIndex < 0 || e.RowIndex >= result.PlayerRowLabels.Count) return null;
            return result.PlayerRowLabels[e.RowIndex];
        }
    }
}
