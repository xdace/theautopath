using System;
using System.Collections.Generic;
using Betaknight.Core.Encounters;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Turns;

namespace Betaknight.Core
{
    /// <summary>Ergebnis eines einzelnen Schritts.</summary>
    public readonly struct StepResult
    {
        public readonly bool Success;
        public readonly MoveFailure Failure;
        public readonly HexCell Cell;

        /// <summary>True, wenn das Feld vor diesem Schritt noch nie betreten wurde.</summary>
        public readonly bool FirstVisit;

        private StepResult(bool success, MoveFailure failure, HexCell cell, bool firstVisit)
        {
            Success = success;
            Failure = failure;
            Cell = cell;
            FirstVisit = firstVisit;
        }

        /// <summary>Soll eine laufende Mehrfeld-Reise nach diesem Schritt anhalten?</summary>
        public bool InterruptsTravel => !Success || FirstVisit || Cell.Content.IsHostile();

        public static StepResult Ok(HexCell cell, bool firstVisit) => new StepResult(true, MoveFailure.None, cell, firstVisit);
        public static StepResult Fail(MoveFailure failure) => new StepResult(false, failure, null, false);
    }

    /// <summary>
    /// Fassade der Oberwelt-Logik: Karte, Spieler, Fog of War, Züge und Feld-Events.
    /// Komplett Unity-frei und dadurch per Unit-Test prüfbar.
    /// Die Darstellung beobachtet nur Events und ruft <see cref="TryStep"/> auf.
    /// </summary>
    public sealed class OverworldSession
    {
        public HexMap Map { get; }
        public PlayerModel Player { get; }
        public TurnSystem Turns { get; }
        public ExplorationService Exploration { get; }
        public PlayerStats Stats { get; }
        public EncounterCatalog Encounters { get; }

        /// <summary>Mittleres Event, das auf <see cref="ChooseEncounterOption"/> wartet. Solange gesetzt, ist Bewegung gesperrt.</summary>
        public EncounterPrompt PendingEncounter { get; private set; }

        /// <summary>Wartet die Session auf eine Entscheidung des Spielers?</summary>
        public bool IsBusy => PendingEncounter != null;

        /// <summary>Wird nach jedem erfolgreichen Schritt ausgelöst, nachdem kleine Events bereits gewirkt haben.</summary>
        public event Action<StepResult> CellEntered;

        /// <summary>Ein mittleres Event öffnet eine Entscheidung.</summary>
        public event Action<EncounterPrompt> EncounterStarted;

        /// <summary>Ein Event hat gewirkt (kleines sofort, mittleres nach der Wahl).</summary>
        public event Action<EncounterOutcome> EncounterResolved;

        private readonly EncounterResolver _resolver;

        public OverworldSession(HexMap map, PlayerModel player, TurnSystem turns, ExplorationService exploration,
            PlayerStats stats = null, EncounterCatalog encounters = null)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Turns = turns ?? throw new ArgumentNullException(nameof(turns));
            Exploration = exploration ?? throw new ArgumentNullException(nameof(exploration));
            Stats = stats ?? new PlayerStats();
            Encounters = encounters ?? EncounterCatalog.CreateDefault();

            // Eigener Zufall für Events, abgeleitet vom Karten-Seed: gleicher Seed, gleiche Beute.
            _resolver = new EncounterResolver(Map, Exploration, Stats, new Random(unchecked(Map.Seed * 31 + 7)));

            if (!Map.TryGetCell(Player.Position, out HexCell startCell))
                throw new ArgumentException("Der Spieler muss auf einem Feld der Karte starten.");

            Map.RegisterVisit(startCell);
            Exploration.RevealAround(Player.Position);
        }

        /// <summary>Erzeugt Karte, setzt den Spieler in die Mitte und deckt die Startumgebung auf.</summary>
        public static OverworldSession Create(MapGenerationConfig config, int sightRadius = 1, PlayerStats stats = null)
        {
            HexMap map = MapGenerator.Generate(config);
            return new OverworldSession(
                map,
                new PlayerModel(map.Center),
                new TurnSystem(),
                new ExplorationService(map, sightRadius),
                stats,
                config.Encounters);
        }

        public HexCell CurrentCell => Map.GetCell(Player.Position);

        public bool CanStepTo(HexCoord target) => CheckStep(target) == MoveFailure.None;

        private MoveFailure CheckStep(HexCoord target) =>
            IsBusy ? MoveFailure.Busy : MovementRules.CheckStep(Map, Player.Position, target);

        /// <summary>
        /// Ein Schritt auf ein Nachbarfeld: bewegt den Spieler, deckt auf, beendet den Zug und löst das Feld-Event aus.
        /// </summary>
        public StepResult TryStep(HexCoord target)
        {
            MoveFailure failure = CheckStep(target);
            if (failure != MoveFailure.None) return StepResult.Fail(failure);

            HexCell cell = Map.GetCell(target);
            bool firstVisit = cell.VisitCount == 0;

            Player.MoveTo(target);
            Map.RegisterVisit(cell);
            Exploration.RevealAround(target);
            Turns.EndTurn();

            TriggerEncounter(cell);

            StepResult result = StepResult.Ok(cell, firstVisit);
            CellEntered?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Wählt eine Option des wartenden mittleren Events. Gibt null zurück, wenn kein Event wartet,
        /// der Index ungültig ist oder die Option nicht bezahlbar ist.
        /// </summary>
        public EncounterOutcome ChooseEncounterOption(int index)
        {
            EncounterPrompt prompt = PendingEncounter;
            if (prompt == null) return null;

            IReadOnlyList<EncounterOption> options = prompt.Definition.Options;
            if (index < 0 || index >= options.Count) return null;

            EncounterOutcome outcome = Resolve(prompt.Cell, prompt.Definition, options[index]);
            if (outcome == null) return null;

            PendingEncounter = null;
            EncounterResolved?.Invoke(outcome);
            return outcome;
        }

        private void TriggerEncounter(HexCell cell)
        {
            if (cell.Content != CellContent.Encounter || cell.IsResolved) return;
            if (!Encounters.TryGet(cell.EncounterId, out EncounterDefinition definition)) return;

            if (definition.Size == EncounterSize.Minor)
            {
                EncounterOutcome outcome = Resolve(cell, definition, definition.Options[0]);
                if (outcome != null) EncounterResolved?.Invoke(outcome);
                return;
            }

            PendingEncounter = new EncounterPrompt(cell, definition);
            EncounterStarted?.Invoke(PendingEncounter);
        }

        private EncounterOutcome Resolve(HexCell cell, EncounterDefinition definition, EncounterOption option)
        {
            List<string> lines = _resolver.Apply(option, cell.Coord);
            if (lines == null) return null;

            Map.MarkResolved(cell.Coord);
            return new EncounterOutcome(cell, definition, option, lines);
        }

        /// <summary>
        /// Route zu einem entfernten Feld über bekanntes Gelände.
        /// Null, wenn das Ziel nicht erreichbar ist. Ausgeführt wird sie Schritt für Schritt per <see cref="TryStep"/>.
        /// </summary>
        public List<HexCoord> PlanRoute(HexCoord target) => Pathfinder.FindPath(Map, Player.Position, target);
    }
}
