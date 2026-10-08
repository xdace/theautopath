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
                    else if (e.IsRepeat) text += ArenaTexts.LogRepeat;
                    if (e.FromQueue) text += ArenaTexts.LogFromQueue(Time(e.QueuedTicks));
                    break;
                case BattleEventKind.RowQueued:
                    text = ArenaTexts.LogQueued(who, Component(e.RowIndex, e.Detail));
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

        private static string RowLabel(BattleEvent e, BattleResult result)
        {
            if (result == null || e.Source == null || e.Source.Side != Side.Player) return null;
            if (e.RowIndex < 0 || e.RowIndex >= result.PlayerRowLabels.Count) return null;
            return result.PlayerRowLabels[e.RowIndex];
        }
    }
}
