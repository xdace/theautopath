using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Combat;
using Betaknight.Core.Encounters;
using Betaknight.Core.Exploration;
using Betaknight.Core.Gear;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;
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
        public bool InterruptsTravel => !Success || FirstVisit || Cell.Content.IsHostile() || Cell.IsUnderAttack;

        public static StepResult Ok(HexCell cell, bool firstVisit) => new StepResult(true, MoveFailure.None, cell, firstVisit);
        public static StepResult Fail(MoveFailure failure) => new StepResult(false, failure, null, false);
    }

    /// <summary>
    /// Fassade der Oberwelt-Logik: Karte, Spieler, Fog of War, Züge und Feld-Events.
    /// Komplett Unity-frei und dadurch per Unit-Test prüfbar.
    /// Die Darstellung beobachtet nur Events und ruft <see cref="TryStep"/> auf.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public HexMap Map { get; }
        public PlayerModel Player { get; }
        public TurnSystem Turns { get; }
        public ExplorationService Exploration { get; }
        public PlayerStats Stats { get; }
        public EncounterCatalog Encounters { get; }
        public RuneLoadout Runes { get; }
        public RuneCatalog RuneCatalog { get; }

        /// <summary>So viele Runensplitter ergeben eine Runenwahl.</summary>
        public const int ShardsPerRune = 3;

        /// <summary>Gold als Trost, wenn der Spieler auf eine Runenwahl verzichtet.</summary>
        public const int SkipRuneGold = 3;

        /// <summary>Mittleres Event, das auf <see cref="ChooseEncounterOption"/> wartet. Solange gesetzt, ist Bewegung gesperrt.</summary>
        public EncounterPrompt PendingEncounter { get; private set; }

        /// <summary>Runenwahl, die auf <see cref="TakeRune"/> oder <see cref="SkipRuneOffer"/> wartet.</summary>
        public RuneOffer PendingRuneOffer { get; private set; }

        /// <summary>Wartet die Session auf eine Entscheidung des Spielers?</summary>
        public bool IsBusy => PendingEncounter != null || PendingRuneOffer != null || PendingShop != null || PendingPortal
            || PendingItem != null || PendingRune != null;

        /// <summary>Wird nach jedem erfolgreichen Schritt ausgelöst, nachdem kleine Events bereits gewirkt haben.</summary>
        public event Action<StepResult> CellEntered;

        /// <summary>Ein mittleres Event öffnet eine Entscheidung.</summary>
        public event Action<EncounterPrompt> EncounterStarted;

        /// <summary>Ein Event hat gewirkt (kleines sofort, mittleres nach der Wahl).</summary>
        public event Action<EncounterOutcome> EncounterResolved;

        /// <summary>Eine Runenwahl wird angeboten.</summary>
        public event Action<RuneOffer> RuneOfferStarted;

        /// <summary>Eine Rune wurde aus einem Angebot genommen.</summary>
        public event Action<RuneDefinition> RuneTaken;

        private readonly Random _random;
        private readonly EncounterResolver _resolver;

        public OverworldSession(HexMap map, PlayerModel player, TurnSystem turns, ExplorationService exploration,
            PlayerStats stats = null, EncounterCatalog encounters = null, RuneLoadout runes = null, RuneCatalog runeCatalog = null,
            ICombatResolver combat = null, Equipment equipment = null, EquipmentCatalog items = null,
            Inventory inventory = null, RuneInventory runeInventory = null, SkillCollection skills = null)
        {
            Skills = skills ?? new SkillCollection();
            Inventory = inventory ?? new Inventory();
            RuneInventory = runeInventory ?? new RuneInventory();
            Gear = equipment ?? new Equipment();
            Items = items ?? EquipmentCatalog.CreateDefault();
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Player = player ?? throw new ArgumentNullException(nameof(player));
            Turns = turns ?? throw new ArgumentNullException(nameof(turns));
            Exploration = exploration ?? throw new ArgumentNullException(nameof(exploration));
            Stats = stats ?? new PlayerStats();
            Encounters = encounters ?? EncounterCatalog.CreateDefault();
            Runes = runes ?? new RuneLoadout();
            RuneCatalog = runeCatalog ?? RuneCatalog.CreateDefault();

            // Exemplare, die beim Aufbau schon an Zeilen sitzen, gehören zur Sammlung; ebenso später direkt gesetzte.
            AdoptRowSkills();
            Runes.Changed += AdoptRowSkills;
            Runes.Changed += ReleaseOrphanModules;
            Skills.Changed += ReleaseOrphanModules;

            // Eigener Zufall für Events und Angebote, abgeleitet vom Karten-Seed: gleicher Seed, gleiche Beute.
            _random = new Random(unchecked(Map.Seed * 31 + 7));
            _resolver = new EncounterResolver(Map, Exploration, Stats, _random, Runes);
            _combat = combat ?? new ArenaCombatResolver(synergies: Synergies);
            Turns.TurnEnded += OnTurnEnded;

            if (!Map.TryGetCell(Player.Position, out HexCell startCell))
                throw new ArgumentException("Der Spieler muss auf einem Feld der Karte starten.");

            Map.RegisterVisit(startCell);
            Exploration.RevealAround(Player.Position);
        }

        /// <summary>Erzeugt Karte, setzt den Spieler in die Mitte und deckt die Startumgebung auf.</summary>
        public static OverworldSession Create(MapGenerationConfig config, int sightRadius = 1, KnightKit kit = null)
        {
            HexMap map = MapGenerator.Generate(config);
            RuneCatalog runeCatalog = RuneCatalog.CreateDefault();
            EquipmentCatalog items = EquipmentCatalog.CreateDefault();
            var runes = new RuneLoadout();
            var gear = new Equipment();
            var skills = new SkillCollection();
            if (kit != null)
            {
                foreach (string id in kit.StartItemIds)
                    if (items.TryGet(id, out EquipmentDefinition item)) gear.Equip(item);

                // Start-Skills liegen in der Sammlung; der erste sitzt an der Start-Rune.
                foreach (string id in kit.StartSkillIds) skills.Add(id);
                SkillInstance first = kit.StartSkillId != null ? skills.All.FirstOrDefault(s => s.SkillId == kit.StartSkillId) : null;
                if (runeCatalog.TryGet(kit.StartRuneId, out RuneDefinition startRune))
                    runes.TryAdd(startRune, first ?? SkillInstance.BasicAttack());
            }

            var session = new OverworldSession(
                map,
                new PlayerModel(map.Center),
                new TurnSystem(),
                new ExplorationService(map, sightRadius),
                kit?.CreateStats(),
                config.Encounters,
                runes,
                runeCatalog,
                null,
                gear,
                items,
                skills: skills);
            session.Kit = kit;
            return session;
        }

        /// <summary>Gewähltes Start-Kit, falls der Run mit einem gestartet wurde.</summary>
        public KnightKit Kit { get; private set; }

        public HexCell CurrentCell => Map.GetCell(Player.Position);

        public bool CanStepTo(HexCoord target) => CheckStep(target) == MoveFailure.None;

        private MoveFailure CheckStep(HexCoord target)
        {
            if (IsGameOver) return MoveFailure.GameOver;
            if (IsBusy) return MoveFailure.Busy;
            return MovementRules.CheckStep(Map, Player.Position, target);
        }

        /// <summary>
        /// Ein Schritt auf ein Nachbarfeld: bewegt den Spieler, deckt auf, beendet den Zug und löst das Feld-Event aus
        /// (kleine/mittlere Events hier, grosse in OverworldSession.MajorEvents.cs).
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
            TriggerMajorEvent(cell, firstVisit);
            TriggerBossIfDue();

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
            CheckShards();
            return outcome;
        }

        /// <summary>
        /// Öffnet eine Runenwahl, z. B. als Belohnung. Gibt null zurück, wenn schon etwas wartet
        /// oder keine Rune mehr verfügbar ist.
        /// </summary>
        public RuneOffer OfferRunes(string source)
        {
            if (IsBusy) return null;
            RuneOffer offer = RuneOffer.Create(source, RuneCatalog, Runes, _random, isUnlocked: IsRuneUnlocked, isOwned: RuneInventory.Contains);
            offer = ShapeOffer(offer.WithItems(RollRewardItems(source)).WithSkills(RollRewardSkills(source)))
                .WithModules(RollRewardModules(source));
            if (offer.Count == 0) return null;

            PendingRuneOffer = offer;
            RuneOfferStarted?.Invoke(offer);
            return offer;
        }

        /// <summary>
        /// Nimmt eine Rune aus dem wartenden Angebot. Mit freier Zeile kommt sie auf die Tafel, sonst ins Runen-Inventar.
        /// Mit <paramref name="replaceSlot"/> kommt sie in diese Zeile und die bisherige Rune samt Stufe ins Inventar
        /// (der Skill bleibt an der Zeile). Gibt false zurück, wenn die Wahl ungültig ist.
        /// </summary>
        public bool TakeRune(int optionIndex, int replaceSlot = -1)
        {
            RuneOffer offer = PendingRuneOffer;
            if (offer == null || optionIndex < 0 || optionIndex >= offer.Options.Count) return false;

            RuneDefinition rune = offer.Options[optionIndex];
            PendingRuneOffer = null;
            if (!PlaceNewRune(rune, replaceSlot))
            {
                PendingRuneOffer = offer;
                return false;
            }

            RuneTaken?.Invoke(rune);
            CheckShards();
            return true;
        }

        /// <summary>Verzichtet auf das wartende Angebot und gibt dafür etwas Gold.</summary>
        public bool SkipRuneOffer()
        {
            if (PendingRuneOffer == null) return false;
            PendingRuneOffer = null;
            Stats.AddGold(SkipRuneGold);
            CheckShards();
            return true;
        }

        /// <summary>Genug Splitter gesammelt? Dann direkt eine Runenwahl anbieten.</summary>
        private void CheckShards()
        {
            if (IsBusy || Stats.Shards < ShardsPerRune) return;
            if (!Stats.TrySpendShards(ShardsPerRune)) return;

            if (OfferRunes("Runensplitter") == null)
            {
                // Nichts mehr anzubieten: Splitter zurückgeben statt sie zu verlieren.
                Stats.AddShards(ShardsPerRune);
            }
        }

        private void TriggerEncounter(HexCell cell)
        {
            if (cell.Content != CellContent.Encounter || cell.IsResolved) return;
            if (!Encounters.TryGet(cell.EncounterId, out EncounterDefinition definition)) return;

            if (definition.Size == EncounterSize.Minor)
            {
                EncounterOutcome outcome = Resolve(cell, definition, definition.Options[0]);
                if (outcome != null) EncounterResolved?.Invoke(outcome);
                CheckShards();
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
