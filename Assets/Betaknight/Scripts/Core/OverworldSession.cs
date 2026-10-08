using System;
using System.Collections.Generic;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
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
    /// Fassade der Oberwelt-Logik: Karte, Spieler, Fog of War und Züge.
    /// Komplett Unity-frei und dadurch per Unit-Test prüfbar.
    /// Die Darstellung beobachtet nur Events und ruft <see cref="TryStep"/> auf.
    /// </summary>
    public sealed class OverworldSession
    {
        public HexMap Map { get; }
        public PlayerModel Player { get; }
        public TurnSystem Turns { get; }
        public ExplorationService Exploration { get; }

        /// <summary>Wird nach jedem erfolgreichen Schritt ausgelöst. Einstiegspunkt für spätere Feld-Events (Kampf, Shop, Loot).</summary>
        public event Action<StepResult> CellEntered;

        public OverworldSession(HexMap map, PlayerModel player, TurnSystem turns, ExplorationService exploration)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Turns = turns ?? throw new ArgumentNullException(nameof(turns));
            Exploration = exploration ?? throw new ArgumentNullException(nameof(exploration));

            if (!Map.TryGetCell(Player.Position, out HexCell startCell))
                throw new ArgumentException("Der Spieler muss auf einem Feld der Karte starten.");

            Map.RegisterVisit(startCell);
            Exploration.RevealAround(Player.Position);
        }

        /// <summary>Erzeugt Karte, setzt den Spieler in die Mitte und deckt die Startumgebung auf.</summary>
        public static OverworldSession Create(MapGenerationConfig config, int sightRadius = 1)
        {
            HexMap map = MapGenerator.Generate(config);
            return new OverworldSession(
                map,
                new PlayerModel(map.Center),
                new TurnSystem(),
                new ExplorationService(map, sightRadius));
        }

        public HexCell CurrentCell => Map.GetCell(Player.Position);

        public bool CanStepTo(HexCoord target) => MovementRules.CheckStep(Map, Player.Position, target) == MoveFailure.None;

        /// <summary>
        /// Ein Schritt auf ein Nachbarfeld: bewegt den Spieler, deckt auf und beendet den Zug.
        /// </summary>
        public StepResult TryStep(HexCoord target)
        {
            MoveFailure failure = MovementRules.CheckStep(Map, Player.Position, target);
            if (failure != MoveFailure.None) return StepResult.Fail(failure);

            HexCell cell = Map.GetCell(target);
            bool firstVisit = cell.VisitCount == 0;

            Player.MoveTo(target);
            Map.RegisterVisit(cell);
            Exploration.RevealAround(target);
            Turns.EndTurn();

            StepResult result = StepResult.Ok(cell, firstVisit);
            CellEntered?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Route zu einem entfernten Feld über bekanntes Gelände.
        /// Null, wenn das Ziel nicht erreichbar ist. Ausgeführt wird sie Schritt für Schritt per <see cref="TryStep"/>.
        /// </summary>
        public List<HexCoord> PlanRoute(HexCoord target) => Pathfinder.FindPath(Map, Player.Position, target);
    }
}
