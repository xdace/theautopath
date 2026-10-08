namespace Betaknight.Core.Arena
{
    /// <summary>Lesbare deutsche Zeilen für das Kampfprotokoll.</summary>
    public static class BattleLogText
    {
        private static readonly SkillCatalog Skills = SkillCatalog.CreateDefault();

        public static string Time(int tick) => $"{tick / Ticks.PerSecond},{tick % Ticks.PerSecond * 10 / Ticks.PerSecond}s";

        public static string SkillName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            if (Skills.TryGet(id, out SkillDefinition s)) return s.Name;
            switch (id)
            {
                case StatusIds.Burn: return "Brennen";
                case "overheat": return "Überhitzung";
                case "heat": return "Hitze";
                case "discharge": return "Entladung";
                case "hp_cost": return "HP-Kosten";
                case StatusIds.Poison: return "Gift";
                default: return id;
            }
        }

        public static string StatusName(string id)
        {
            switch (id)
            {
                case StatusIds.Stun: return "betäubt";
                case StatusIds.Burn: return "brennt";
                case StatusIds.ArmorBreak: return "Rüstung gebrochen";
                case StatusIds.ShieldWall: return "Schildwall";
                case StatusIds.Blinded: return "geblendet";
                case StatusIds.Anchor: return "verankert";
                case StatusIds.Thrusters: return "Schubdüsen bereit";
                case StatusIds.Poison: return "vergiftet";
                default: return id;
            }
        }

        public static string Describe(BattleEvent e, BattleResult result = null)
        {
            string who = e.Source?.Name ?? "";
            string whom = e.Target?.Name ?? "";
            string text;
            switch (e.Kind)
            {
                case BattleEventKind.ActionStarted:
                    string row = RowLabel(e, result);
                    text = row != null ? $"{who}: [{row}] → {SkillName(e.Detail)}" : $"{who}: {SkillName(e.Detail)}";
                    if (e.IsTriggered) text += $"  ↪ ausgelöst von Zeile {e.CauseRow + 1}";
                    else if (e.IsRepeat) text += "  ↻ Wiederholung";
                    break;
                case BattleEventKind.TriggerExpired:
                    text = $"{who}: Auslöser von Zeile {e.Amount + 1} verfällt, Zeile {e.RowIndex + 1} ({SkillName(e.Detail)}) nicht bereit";
                    break;
                case BattleEventKind.ActionInterrupted: text = $"{who}: {SkillName(e.Detail)} abgebrochen"; break;
                case BattleEventKind.Healed: text = $"{whom} heilt {e.Amount}"; break;
                case BattleEventKind.StatusApplied: text = $"{whom} {StatusName(e.Detail)}"; break;
                case BattleEventKind.Death: text = $"{whom} fällt"; break;
                case BattleEventKind.Dodged: text = $"{whom} weicht aus"; break;
                case BattleEventKind.Blocked: text = $"{whom} blockt"; break;
                case BattleEventKind.Crit: text = $"{who}: Krit! {e.Amount}"; break;
                case BattleEventKind.Overheat: text = $"Überhitzung Stufe {e.Amount}"; break;
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
                case BattleOutcome.Victory: return "Sieg";
                case BattleOutcome.Defeat: return "Niederlage";
                case BattleOutcome.Escaped: return "Durchs Portal entkommen";
                default: return "Zeit abgelaufen";
            }
        }

        private static string RowLabel(BattleEvent e, BattleResult result)
        {
            if (result == null || e.Source == null || e.Source.Side != Side.Player) return null;
            if (e.RowIndex < 0 || e.RowIndex >= result.PlayerRowLabels.Count) return null;
            return result.PlayerRowLabels[e.RowIndex];
        }
    }
}
