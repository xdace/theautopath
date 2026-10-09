using System.Collections.Generic;
using System.Text;

namespace Betaknight.Core.Arena
{
    /// <summary>Eine Wirkung einer Aktion auf ein Ziel (für die «JETZT»-Karte).</summary>
    public sealed class StoryEffect
    {
        public string Target { get; internal set; }
        public int Damage { get; internal set; }
        public int Healing { get; internal set; }
        public bool Crit { get; internal set; }
        public bool Dodged { get; internal set; }
        public bool Blocked { get; internal set; }
        internal readonly List<string> StatusList = new List<string>();
        public IReadOnlyList<string> Statuses => StatusList;
    }

    /// <summary>
    /// Eine Aktion als Satz: Ursache (Relais, Auslöser, Puls) → Komponente → Ziele → Wirkung. Wird beim Start angelegt und
    /// füllt sich mit Schaden, Heilung und Zuständen derselben Quelle, bis die Aktion ausgeführt ist.
    /// </summary>
    public sealed class ActionStory
    {
        public int Fighter { get; internal set; }
        public string FighterName { get; internal set; }
        public bool IsPlayer { get; internal set; }
        public string Skill { get; internal set; }
        public string SkillId { get; internal set; }
        public bool IsBasicAttack { get; internal set; }
        public int Row { get; internal set; } = -1;
        public ActionCause Cause { get; internal set; }
        public int CauseRow { get; internal set; } = -1;

        /// <summary>Name des auslösenden Relais (Spieler), leer sonst.</summary>
        public string Relay { get; internal set; } = string.Empty;

        /// <summary>Gespeicherte Ladung beim Einreihen, z. B. «4/4», leer ohne.</summary>
        public string Charge { get; internal set; } = string.Empty;

        public int StartTick { get; internal set; }
        public int WindupTicks { get; internal set; }
        public int ExecuteTick { get; internal set; } = -1;
        public bool Interrupted { get; internal set; }
        public bool Done => ExecuteTick >= 0 || Interrupted;

        internal readonly List<StoryEffect> EffectList = new List<StoryEffect>();
        public IReadOnlyList<StoryEffect> Effects => EffectList;

        internal StoryEffect On(string target)
        {
            foreach (StoryEffect e in EffectList)
                if (e.Target == target) return e;
            var effect = new StoryEffect { Target = target };
            EffectList.Add(effect);
            return effect;
        }

        /// <summary>«On Hit → #2 Shock Stab → Scrap Rat −12 crit». Nur sichere Zeichen (→ ist in der Standardschrift).</summary>
        public string Sentence()
        {
            var sb = new StringBuilder();
            if (!IsPlayer) sb.Append(FighterName).Append(": ");
            string cause = CauseText();
            if (cause.Length > 0) sb.Append(cause).Append(" → ");
            sb.Append(Row >= 0 && IsPlayer ? ArenaTexts.ComponentName(Row, Skill) : Skill);
            if (Charge.Length > 0) sb.Append(" (").Append(Charge).Append(')');
            if (Interrupted) return sb.Append(" → ").Append(ArenaTexts.StoryInterrupted).ToString();
            if (EffectList.Count == 0) return sb.Append(ExecuteTick >= 0 ? string.Empty : " …").ToString();
            sb.Append(" → ");
            for (int i = 0; i < EffectList.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                StoryEffect e = EffectList[i];
                sb.Append(e.Target);
                if (e.Dodged) sb.Append(' ').Append(ArenaTexts.StoryDodged);
                if (e.Blocked) sb.Append(' ').Append(ArenaTexts.StoryBlocked);
                if (e.Damage > 0) sb.Append(" −").Append(e.Damage);
                if (e.Crit) sb.Append(' ').Append(ArenaTexts.StoryCrit);
                if (e.Healing > 0) sb.Append(" +").Append(e.Healing).Append(" HP");
                foreach (string s in e.StatusList) sb.Append(' ').Append(BattleLogText.StatusName(s));
            }
            return sb.ToString();
        }

        private string CauseText()
        {
            if (!IsPlayer) return string.Empty;
            switch (Cause)
            {
                case ActionCause.Trigger: return CauseRow >= 0 ? ArenaTexts.StoryTriggeredBy(CauseRow) : Relay;
                case ActionCause.Pulse: return CauseRow >= 0 ? ArenaTexts.StoryPulsedBy(CauseRow) : Relay;
                case ActionCause.Repeat: return ArenaTexts.StoryRepeat;
                default: return Relay;
            }
        }
    }
}
