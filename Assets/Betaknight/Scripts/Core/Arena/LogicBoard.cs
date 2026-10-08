using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Ein Relais der Platine (A-19): eine Bedingung als 1×1-Chip (früher Rune/Logikbaustein). Es versorgt jede Komponente,
    /// die es berührt, solange diese höchstens <see cref="MaxCells"/> Zellen gross ist. Löst es aus, reiht es alle
    /// versorgten Komponenten ein. Ereignis-Bedingungen lösen bei jedem Ereignis aus, Zustände bei der steigenden Flanke.
    /// </summary>
    public sealed class LogicRelay
    {
        public ICondition Condition { get; }
        public string Label { get; }

        /// <summary>Grundschwierigkeit 0–3: bestimmt Bonus und Grössen-Grenze; Erleichterungen ändern sie nicht.</summary>
        public int Difficulty { get; }

        /// <summary>Grösste Komponente (Zellen), die dieses Relais versorgen kann.</summary>
        public int MaxCells { get; }

        /// <summary>Modul «Repeat while true»: nach jeder Ausführung erneut auslösen, solange der Zustand gilt.</summary>
        public bool RepeatWhileTrue { get; }

        /// <summary>Zelle auf der Platine, null bei direkter Verdrahtung (Tests, Werkzeuge).</summary>
        public CellRect? Rect { get; }

        /// <summary>Rune, aus der das Relais stammt (für Anzeige und Wachstum), sonst null.</summary>
        public string RuneId { get; }

        /// <summary>Löst bei jedem Ereignis aus (statt nur bei der steigenden Flanke eines Zustands).</summary>
        public bool IsEventTrigger { get; }

        /// <summary>Position in <see cref="LogicBoard.Relays"/>.</summary>
        public int Index { get; internal set; } = -1;

        internal readonly List<int> PoweredList = new List<int>();
        internal readonly List<int> TooLargeList = new List<int>();

        /// <summary>Versorgte Komponenten (Index in <see cref="LogicBoard.Rows"/>).</summary>
        public IReadOnlyList<int> Powered => PoweredList;

        /// <summary>Berührte Komponenten, die für dieses Relais zu gross sind («not powered (too large)»).</summary>
        public IReadOnlyList<int> TooLarge => TooLargeList;

        public LogicRelay(ICondition condition, string label, int difficulty = 0, int maxCells = int.MaxValue, CellRect? rect = null,
            bool repeatWhileTrue = false, string runeId = null)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Label = label ?? condition.GetType().Name;
            Difficulty = DifficultyBonusConfig.Clamp(difficulty);
            MaxCells = Math.Max(1, maxCells);
            Rect = rect;
            RepeatWhileTrue = repeatWhileTrue;
            RuneId = runeId;
            IsEventTrigger = TriggerKinds.IsEvent(condition);
        }

        public override string ToString() => $"[{Label}]";
    }

    /// <summary>
    /// Eine Komponente der Platine (A-19): ein Skill mit Form. Sie feuert nur, wenn ein Relais auslöst, das sie versorgt;
    /// ohne Versorgung feuert sie nie. Priorität in der Warteschlange = Lesereihenfolge (oben links zuerst).
    /// Der Name «Row» bleibt aus der Zeit der Logik-Tafel: <see cref="LogicBoard.Rows"/> sind die Komponenten in Lesereihenfolge.
    /// </summary>
    public sealed class LogicRow
    {
        public SkillDefinition Skill { get; }

        /// <summary>Anzeige der versorgenden Relais, z. B. «On Hit», oder der vorgegebene Name.</summary>
        public string Label { get; private set; }

        /// <summary>Ohne Skill: verwaist, feuert nie.</summary>
        public bool IsOrphaned => Skill == null;

        /// <summary>Zellen auf der Platine, null bei direkter Verdrahtung.</summary>
        public CellRect? Rect { get; }

        /// <summary>Grösse in Zellen (Platine oder Form des Skills).</summary>
        public int Cells => Rect?.Shape.Cells ?? Skill?.Shape.Cells ?? 1;

        /// <summary>Berührt den Kern (Wirkungsbonus schon im Skill eingerechnet).</summary>
        public bool TouchesCore { get; internal set; }

        /// <summary>Position in <see cref="LogicBoard.Rows"/>.</summary>
        public int Index { get; internal set; } = -1;

        internal readonly List<LogicRelay> RelayList = new List<LogicRelay>();
        internal readonly List<LogicRelay> TooLargeList = new List<LogicRelay>();

        /// <summary>Relais, die diese Komponente versorgen.</summary>
        public IReadOnlyList<LogicRelay> Relays => RelayList;

        /// <summary>Berührende Relais, deren Grenze für diese Komponente zu klein ist.</summary>
        public IReadOnlyList<LogicRelay> TooLargeFor => TooLargeList;

        public bool IsPowered => RelayList.Count > 0;

        /// <summary>Bedingung des ersten versorgenden Relais (Anzeige), sonst null.</summary>
        public ICondition Condition => RelayList.Count > 0 ? RelayList[0].Condition : null;

        /// <summary>Höchste Schwierigkeit der versorgenden Relais (Anzeige); im Kampf zählt das auslösende Relais.</summary>
        public int Difficulty
        {
            get
            {
                int d = 0;
                foreach (LogicRelay r in RelayList) d = Math.Max(d, r.Difficulty);
                return d;
            }
        }

        private readonly SkillDefinition[] _byTier = new SkillDefinition[DifficultyBonusConfig.MaxTier + 1];

        /// <summary>Komponente auf der Platine; Relais verbindet <see cref="LogicBoard.Compile"/>.</summary>
        public LogicRow(SkillDefinition skill, CellRect? rect, string label = null)
        {
            Skill = skill;
            Rect = rect;
            Label = label;
        }

        /// <summary>
        /// Direkt verdrahtet: eine Komponente mit eigenem Relais aus dieser Bedingung, ohne Grössen-Grenze.
        /// Für Tests, Werkzeuge und einfache Gegner; Platinen des Spielers entstehen über <see cref="LogicBoard.Compile"/>.
        /// </summary>
        public LogicRow(ICondition condition, SkillDefinition skill, string label = null, int difficulty = 0, bool repeatWhileTrue = false)
        {
            Skill = skill;
            var relay = new LogicRelay(condition, label, difficulty, repeatWhileTrue: repeatWhileTrue);
            RelayList.Add(relay);
            Label = relay.Label;
        }

        internal void FinishLabel()
        {
            if (Label != null) return;
            var names = new List<string>();
            foreach (LogicRelay r in RelayList) names.Add(r.Label);
            Label = names.Count > 0 ? string.Join(" / ", names) : ArenaTexts.NotPowered;
        }

        /// <summary>Der Skill mit dem Bonus einer Stufe (zwischengespeichert, je Komponente eine Konfiguration).</summary>
        public SkillDefinition SkillAt(int tier, DifficultyBonusConfig config)
        {
            if (Skill == null) return null;
            tier = DifficultyBonusConfig.Clamp(tier);
            return _byTier[tier] ?? (_byTier[tier] = (config ?? DifficultyBonusConfig.Default).Apply(Skill, tier));
        }

        public override string ToString() => $"[{Label}] -> [{Skill?.Name ?? "—"}]";
    }

    /// <summary>Was die Anzeige über die Platine wissen muss: Grösse und Kern.</summary>
    public sealed class BoardLayout
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Kern-Zelle oder null (Gegner, direkte Verdrahtung).</summary>
        public Cell? Core { get; }

        public BoardLayout(int width, int height, Cell? core)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            Core = core;
        }
    }

    /// <summary>
    /// Die Platine im Kampf (A-19): Komponenten in Lesereihenfolge, Relais, die sie versorgen, und darunter immer der
    /// Basisangriff als Lückenfüller (<see cref="Fallback"/>). Es gibt keine Cooldowns: Komponenten feuern, wenn ihr
    /// Relais auslöst; die Cast-Zeit ist die einzige Schleifen-Regel.
    /// </summary>
    public sealed class LogicBoard
    {
        private readonly List<LogicRow> _rows;
        private readonly List<LogicRelay> _relays = new List<LogicRelay>();

        /// <summary>Komponenten in Lesereihenfolge (Index = Priorität in der Warteschlange).</summary>
        public IReadOnlyList<LogicRow> Rows => _rows;

        public IReadOnlyList<LogicRelay> Relays => _relays;

        /// <summary>Basisangriff, füllt Lücken, wenn die Warteschlange leer ist.</summary>
        public LogicRow Fallback { get; }

        /// <summary>Index, mit dem der Basisangriff im Protokoll erscheint.</summary>
        public int FallbackIndex => _rows.Count;

        /// <summary>Auslöser-Kanten (Module) zwischen Relais und Komponenten.</summary>
        public LogicGraph Graph { get; }

        /// <summary>Schwierigkeits-Bonus und Grössen-Grenze je Stufe für diese Platine.</summary>
        public DifficultyBonusConfig Bonus { get; }

        /// <summary>Grösse und Kern für die Anzeige, null bei direkter Verdrahtung.</summary>
        public BoardLayout Layout { get; }

        /// <summary>Direkt verdrahtete Komponenten (jede mit eigenem Relais), Reihenfolge wie übergeben.</summary>
        public LogicBoard(IEnumerable<LogicRow> rows, SkillDefinition fallbackSkill = null, LogicGraph graph = null,
            DifficultyBonusConfig bonus = null)
            : this(rows, null, fallbackSkill, graph, bonus, null)
        {
        }

        private LogicBoard(IEnumerable<LogicRow> rows, IEnumerable<LogicRelay> relays, SkillDefinition fallbackSkill, LogicGraph graph,
            DifficultyBonusConfig bonus, BoardLayout layout)
        {
            _rows = new List<LogicRow>(rows ?? Array.Empty<LogicRow>());
            Graph = graph ?? LogicGraph.Empty;
            Bonus = bonus ?? DifficultyBonusConfig.Default;
            Layout = layout;
            Fallback = new LogicRow(AlwaysCondition.Instance, fallbackSkill ?? SkillDefinition.BasicAttack, ArenaTexts.FallbackLabel);
            Fallback.Index = _rows.Count;

            for (int i = 0; i < _rows.Count; i++) _rows[i].Index = i;
            if (relays != null)
            {
                foreach (LogicRelay r in relays) AddRelay(r);
            }
            else
            {
                // Direkt verdrahtet: jedes eigene Relais versorgt nur seine Komponente.
                foreach (LogicRow row in _rows)
                    foreach (LogicRelay r in row.RelayList)
                    {
                        AddRelay(r);
                        r.PoweredList.Add(row.Index);
                    }
            }
            foreach (LogicRow row in _rows) row.FinishLabel();
        }

        private void AddRelay(LogicRelay relay)
        {
            relay.Index = _relays.Count;
            _relays.Add(relay);
        }

        public static LogicBoard FallbackOnly { get; } = new LogicBoard(null);

        /// <summary>
        /// Baut eine Platine aus Teilen mit Zellen: Komponenten werden in Lesereihenfolge sortiert; jedes Relais versorgt die
        /// berührten Komponenten bis zu seiner Grenze (sonst «zu gross»). Komponenten am Kern bekommen dessen Wirkungsbonus.
        /// </summary>
        public static LogicBoard Compile(BoardLayout layout, IEnumerable<LogicRow> components, IEnumerable<LogicRelay> relays,
            SkillDefinition fallbackSkill = null, DifficultyBonusConfig bonus = null, int coreBonusPercent = 0,
            Func<IReadOnlyList<LogicRow>, IReadOnlyList<LogicRelay>, LogicGraph> graph = null)
        {
            var rows = new List<LogicRow>();
            foreach (LogicRow c in components ?? Array.Empty<LogicRow>())
            {
                if (c == null || !c.Rect.HasValue) throw new ArgumentException("Components on a board need cells.");
                if (layout?.Core != null && c.Rect.Value.Touches(new CellRect(layout.Core.Value, Shape.One)) && coreBonusPercent != 0 && c.Skill != null)
                {
                    var boosted = new LogicRow(c.Skill.WithBonus(coreBonusPercent, 0), c.Rect, c.Label) { TouchesCore = true };
                    rows.Add(boosted);
                }
                else
                {
                    if (layout?.Core != null && c.Rect.Value.Touches(new CellRect(layout.Core.Value, Shape.One))) c.TouchesCore = true;
                    rows.Add(c);
                }
            }
            rows.Sort((a, b) => Cell.CompareReading(a.Rect.Value.Origin, b.Rect.Value.Origin));

            var relayList = new List<LogicRelay>(relays ?? Array.Empty<LogicRelay>());
            relayList.Sort((a, b) => Cell.CompareReading(a.Rect?.Origin ?? default, b.Rect?.Origin ?? default));
            for (int i = 0; i < rows.Count; i++) rows[i].Index = i;
            foreach (LogicRelay relay in relayList)
            {
                if (!relay.Rect.HasValue) continue;
                foreach (LogicRow row in rows)
                {
                    if (!relay.Rect.Value.Touches(row.Rect.Value)) continue;
                    if (row.Cells <= relay.MaxCells)
                    {
                        relay.PoweredList.Add(row.Index);
                        row.RelayList.Add(relay);
                    }
                    else
                    {
                        relay.TooLargeList.Add(row.Index);
                        row.TooLargeList.Add(relay);
                    }
                }
            }

            LogicGraph edges = graph?.Invoke(rows, relayList);
            return new LogicBoard(rows, relayList, fallbackSkill, edges, bonus, layout);
        }

        /// <summary>Gibt es Bedingungen mit Gedächtnis? Nur dann beobachtet der Kampf sie jeden Tick.</summary>
        public bool NeedsObservation
        {
            get
            {
                if (_needsObservation.HasValue) return _needsObservation.Value;
                bool needed = false;
                foreach (LogicRelay r in _relays) needed |= r.Condition is IObservingCondition;
                _needsObservation = needed;
                return needed;
            }
        }

        private bool? _needsObservation;

        /// <summary>Komponente nach Index inklusive Basisangriff.</summary>
        public LogicRow RowAt(int index) => index == FallbackIndex ? Fallback : _rows[index];
    }
}
