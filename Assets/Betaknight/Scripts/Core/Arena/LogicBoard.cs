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

        /// <summary>Schwierigkeit, wenn das Relais umgekehrt wird (für das NICHT-Gatter, aus der Rune).</summary>
        public int InvertedDifficulty { get; }

        /// <summary>
        /// Gatter oder Sicherung (A-20): kein eigener Zustand, sondern gespeist von den Relais in <see cref="Inputs"/>.
        /// null bei einem gewöhnlichen Relais.
        /// </summary>
        public ChipKind? Gate { get; }

        internal readonly List<int> InputList = new List<int>();

        /// <summary>Eingänge eines Gatters (Index in <see cref="LogicBoard.Relays"/>), in Lesereihenfolge.</summary>
        public IReadOnlyList<int> Inputs => InputList;

        /// <summary>Gatter mit zu wenigen Eingängen lösen nie aus.</summary>
        public bool IsGateReady => Gate == null || InputList.Count >= LogicBoard.InputsNeeded(Gate.Value);

        /// <summary>Sicherung: höchstens einmal pro Kampf.</summary>
        public bool OncePerFight => Gate == ChipKind.Fuse;

        /// <summary>Chip des Gatters (Index in <see cref="LogicBoard.Chips"/>), sonst -1.</summary>
        public int ChipIndex { get; internal set; } = -1;

        /// <summary>Position in <see cref="LogicBoard.Relays"/>.</summary>
        public int Index { get; internal set; } = -1;

        internal readonly List<int> PoweredList = new List<int>();
        internal readonly List<int> TooLargeList = new List<int>();

        /// <summary>Versorgte Komponenten (Index in <see cref="LogicBoard.Rows"/>).</summary>
        public IReadOnlyList<int> Powered => PoweredList;

        /// <summary>Berührte Komponenten, die für dieses Relais zu gross sind («not powered (too large)»).</summary>
        public IReadOnlyList<int> TooLarge => TooLargeList;

        public LogicRelay(ICondition condition, string label, int difficulty = 0, int maxCells = int.MaxValue, CellRect? rect = null,
            bool repeatWhileTrue = false, string runeId = null, int? invertedDifficulty = null)
        {
            Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            Label = label ?? condition.GetType().Name;
            Difficulty = DifficultyBonusConfig.Clamp(difficulty);
            MaxCells = Math.Max(1, maxCells);
            Rect = rect;
            RepeatWhileTrue = repeatWhileTrue;
            RuneId = runeId;
            IsEventTrigger = TriggerKinds.IsEvent(condition);
            InvertedDifficulty = DifficultyBonusConfig.Clamp(invertedDifficulty ?? Difficulty);
        }

        /// <summary>Ein Gatter als Relais: Schwierigkeit und Grenze stehen fest, die Bedingung kommt aus den Eingängen.</summary>
        internal LogicRelay(ChipKind gate, string label, int difficulty, int maxCells, CellRect rect)
            : this(AlwaysCondition.Instance.Not(), label, difficulty, maxCells, rect)
        {
            Gate = gate;
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

        /// <summary>Pins auf der Platine (A-20), leer bei direkter Verdrahtung.</summary>
        public IReadOnlyList<PlacedPin> Pins { get; }

        /// <summary>Typisierte Pins mit passendem Nachbarn (je einer gibt den Pin-Bonus, schon im Skill eingerechnet).</summary>
        public int MatchedPins { get; internal set; }

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
        public LogicRow(SkillDefinition skill, CellRect? rect, string label = null, IReadOnlyList<PlacedPin> pins = null)
        {
            Skill = skill;
            Rect = rect;
            Label = label;
            Pins = pins ?? Array.Empty<PlacedPin>();
        }

        /// <summary>
        /// Direkt verdrahtet: eine Komponente mit eigenem Relais aus dieser Bedingung, ohne Grössen-Grenze.
        /// Für Tests, Werkzeuge und einfache Gegner; Platinen des Spielers entstehen über <see cref="LogicBoard.Compile"/>.
        /// </summary>
        public LogicRow(ICondition condition, SkillDefinition skill, string label = null, int difficulty = 0, bool repeatWhileTrue = false)
        {
            Skill = skill;
            Pins = Array.Empty<PlacedPin>();
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

        private readonly List<LogicChip> _chips = new List<LogicChip>();
        private readonly List<PulseLink> _links = new List<PulseLink>();

        /// <summary>Logik-Chips (A-20) in Lesereihenfolge.</summary>
        public IReadOnlyList<LogicChip> Chips => _chips;

        /// <summary>Pulsverbindungen zwischen Komponenten und Kondensatoren (A-20).</summary>
        public IReadOnlyList<PulseLink> Links => _links;

        /// <summary>Werte der Chips (Kondensator, Gatter) für diesen Kampf.</summary>
        public ChipConfig ChipConfig { get; private set; } = ChipConfig.Default;

        /// <summary>Verbindungen, die von diesem Knoten ausgehen (Reihenfolge der Ziele: kürzester Weg, dann Fundort).</summary>
        public IEnumerable<PulseLink> LinksFrom(PulseNode node)
        {
            foreach (PulseLink l in _links)
                if (l.From.Equals(node)) yield return l;
        }

        /// <summary>Eingänge, die ein Gatter braucht: UND und ODER zwei, NICHT und Sicherung eines.</summary>
        public static int InputsNeeded(ChipKind gate) => gate == ChipKind.And || gate == ChipKind.Or ? 2 : 1;

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
            Func<IReadOnlyList<LogicRow>, IReadOnlyList<LogicRelay>, LogicGraph> graph = null,
            IEnumerable<LogicChip> chips = null, ChipConfig chipConfig = null, PinConfig pinConfig = null)
        {
            bonus = bonus ?? DifficultyBonusConfig.Default;
            chipConfig = chipConfig ?? ChipConfig.Default;
            pinConfig = pinConfig ?? PinConfig.Default;
            var raw = new List<LogicRow>();
            foreach (LogicRow c in components ?? Array.Empty<LogicRow>())
            {
                if (c == null || !c.Rect.HasValue) throw new ArgumentException("Components on a board need cells.");
                raw.Add(c);
            }
            raw.Sort((a, b) => Cell.CompareReading(a.Rect.Value.Origin, b.Rect.Value.Origin));

            // Kern-Bonus und Pin-Bonus (typisierte Pins mit passendem Nachbarn) gehen in den Skill ein.
            var rows = new List<LogicRow>();
            for (int i = 0; i < raw.Count; i++)
            {
                LogicRow c = raw[i];
                bool core = layout?.Core != null && c.Rect.Value.Touches(new CellRect(layout.Core.Value, Shape.One));
                int matched = CircuitWiring.MatchedTypedPins(raw, i);
                int percent = (core ? coreBonusPercent : 0) + matched * pinConfig.TypedPinBonusPercent;
                LogicRow row = percent != 0 && c.Skill != null ? new LogicRow(c.Skill.WithBonus(percent, 0), c.Rect, c.Label, c.Pins) : c;
                row.TouchesCore = core;
                row.MatchedPins = matched;
                rows.Add(row);
            }

            var chipList = new List<LogicChip>(chips ?? Array.Empty<LogicChip>());
            chipList.Sort((a, b) => Cell.CompareReading(a.Rect.Origin, b.Rect.Origin));
            for (int i = 0; i < chipList.Count; i++) chipList[i].Index = i;

            var relayList = new List<LogicRelay>(relays ?? Array.Empty<LogicRelay>());
            var plainRelays = new List<LogicRelay>(relayList);
            plainRelays.Sort((a, b) => Cell.CompareReading(a.Rect?.Origin ?? default, b.Rect?.Origin ?? default));
            var gateInputs = new Dictionary<LogicRelay, List<LogicRelay>>();
            foreach (LogicChip chip in chipList)
            {
                if (!chip.Definition.IsGate) continue;
                var inputs = new List<LogicRelay>();
                foreach (LogicRelay r in plainRelays)
                    if (r.Rect.HasValue && r.Rect.Value.Touches(chip.Rect) && inputs.Count < InputsNeeded(chip.Kind)) inputs.Add(r);
                int difficulty = GateDifficulty(chip.Kind, inputs, chipConfig);
                var gate = new LogicRelay(chip.Kind, GateLabel(chip, inputs), difficulty, bonus.MaxCells(difficulty), chip.Rect) { ChipIndex = chip.Index };
                gateInputs[gate] = inputs;
                relayList.Add(gate);
            }
            relayList.Sort((a, b) => Cell.CompareReading(a.Rect?.Origin ?? default, b.Rect?.Origin ?? default));
            for (int i = 0; i < relayList.Count; i++) relayList[i].Index = i;
            foreach (KeyValuePair<LogicRelay, List<LogicRelay>> g in gateInputs)
            {
                foreach (LogicRelay input in g.Value) g.Key.InputList.Add(input.Index);
                chipList[g.Key.ChipIndex].RelayIndex = g.Key.Index;
            }
            foreach (LogicChip chip in chipList)
            {
                if (chip.Kind != ChipKind.Capacitor) continue;
                foreach (LogicRelay r in relayList)
                    if (r.Rect.HasValue && r.Rect.Value.Touches(chip.Rect)) chip.ReleaseList.Add(r.Index);
            }
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
            List<PulseLink> links = CircuitWiring.Links(rows, chipList);
            var board = new LogicBoard(rows, relayList, fallbackSkill, LogicGraph.WithCircuit(edges, rows, relayList, chipList, links), bonus, layout);
            board._chips.AddRange(chipList);
            board._links.AddRange(links);
            board.ChipConfig = chipConfig;
            return board;
        }

        /// <summary>Schwierigkeit eines Gatters: UND = höhere + Bonus (höchstens 3), ODER = niedrigere, NICHT = umgekehrter Wert, Sicherung fest.</summary>
        public static int GateDifficulty(ChipKind gate, IReadOnlyList<LogicRelay> inputs, ChipConfig config)
        {
            config = config ?? ChipConfig.Default;
            if (gate == ChipKind.Fuse) return DifficultyBonusConfig.Clamp(config.FuseDifficulty);
            if (inputs == null || inputs.Count == 0) return 0;
            switch (gate)
            {
                case ChipKind.And:
                    int high = 0;
                    foreach (LogicRelay r in inputs) high = Math.Max(high, r.Difficulty);
                    return DifficultyBonusConfig.Clamp(high + config.AndDifficultyBonus);
                case ChipKind.Or:
                    int low = DifficultyBonusConfig.MaxTier;
                    foreach (LogicRelay r in inputs) low = Math.Min(low, r.Difficulty);
                    return low;
                case ChipKind.Not:
                    return inputs[0].InvertedDifficulty;
                default:
                    return 0;
            }
        }

        private static string GateLabel(LogicChip chip, IReadOnlyList<LogicRelay> inputs)
        {
            var names = new List<string>();
            foreach (LogicRelay r in inputs) names.Add(r.Label);
            return ArenaTexts.GateLabel(chip.Name, names);
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
