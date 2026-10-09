using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

namespace Betaknight.Core
{
    /// <summary>Art einer Journal-Meldung; die UI färbt danach (Gefahr rot, Belohnung gold, Verbesserung grün, sonst grau).</summary>
    public enum JournalKind
    {
        Info,
        Good,
        Reward,
        Danger,
    }

    /// <summary>Eine Meldung im Verlauf: Akt, Zug, Art, Titel und Text (ohne Farb-Tags).</summary>
    public sealed class JournalEntry
    {
        public int Act { get; }
        public int Turn { get; }
        public JournalKind Kind { get; }
        public string Title { get; }
        public string Text { get; }

        /// <summary>Wichtige Meldungen (Mine angegriffen) bleiben als Toast stehen, bis man sie wegklickt.</summary>
        public bool Sticky => Kind == JournalKind.Danger;

        public JournalEntry(int act, int turn, JournalKind kind, string title, string text)
        {
            Act = act;
            Turn = turn;
            Kind = kind;
            Title = title ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>
    /// Verlauf aller Meldungen eines Runs (Events, Kämpfe, Minen, Verbesserungen). Wird beim Aktwechsel an die neue Session
    /// weitergereicht, damit nichts verloren geht; die UI zeigt daraus Toasts und das Journal (Taste J).
    /// </summary>
    public sealed class RunJournal
    {
        public const int Limit = 200;

        private readonly List<JournalEntry> _entries = new List<JournalEntry>();

        /// <summary>Älteste zuerst.</summary>
        public IReadOnlyList<JournalEntry> Entries => _entries;

        public event Action<JournalEntry> Added;

        public JournalEntry Add(int act, int turn, JournalKind kind, string title, string text)
        {
            var entry = new JournalEntry(act, turn, kind, title, text);
            _entries.Add(entry);
            if (_entries.Count > Limit) _entries.RemoveAt(0);
            Added?.Invoke(entry);
            return entry;
        }
    }

    public sealed partial class OverworldSession
    {
        /// <summary>Verlauf des Runs; überlebt den Aktwechsel.</summary>
        public RunJournal Journal { get; private set; } = new RunJournal();

        private void Note(JournalKind kind, string title, string text) => Journal.Add(Act, Turns.CurrentTurn, kind, title, text);

        /// <summary>Hängt die eigenen Ereignisse an das Journal (im Konstruktor aufgerufen).</summary>
        private void WireJournal()
        {
            EncounterResolved += o => Note(JournalKind.Info, o.Definition.Title, o.Summary);
            MajorEventResolved += o => Note(JournalKind.Reward, o.Title, o.Summary);
            MineRaidStarted += r => Note(JournalKind.Danger, SessionTexts.JournalMineRaid(r.Coord.ToString()), SessionTexts.JournalMineRaidText(MineRaidTurns));
            MineLost += r => Note(JournalKind.Danger, SessionTexts.JournalMineLost(r.Coord.ToString()), SessionTexts.JournalMineLostText);
            BuildImproved += t => Note(JournalKind.Good, SessionTexts.JournalImproved, t);
            ChipGained += c => Note(JournalKind.Reward, SessionTexts.JournalChip, c.Name);
        }

        private void CarryJournal(OverworldSession previous) => Journal = previous.Journal;
    }
}
