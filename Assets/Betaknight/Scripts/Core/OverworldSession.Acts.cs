using System;
using Betaknight.Core.Exploration;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Movement;
using Betaknight.Core.Turns;

namespace Betaknight.Core
{
    /// <summary>
    /// Akte: Nach dem Boss öffnet sich ein Fluchtportal. Es führt auf eine neue, härtere Karte;
    /// Ritter, Ausrüstung, Inventar, Runen-Tafel, Gold und Splitter kommen mit. Minen und Raids bleiben zurück.
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Gegnerstufen, die jeder weitere Akt auf die Entfernung zur Kartenmitte aufschlägt.</summary>
        public const int ActTierBonus = 2;

        /// <summary>So viel Prozent der Max-HP heilt der Weg durchs Portal.</summary>
        public const int PortalHealPercent = 50;

        /// <summary>Aktueller Akt, beginnt bei 1.</summary>
        public int Act { get; private set; } = 1;

        /// <summary>Das Fluchtportal ist offen und wartet auf <see cref="EnterPortal"/>. Solange gesetzt, ist Bewegung gesperrt.</summary>
        public bool PendingPortal { get; private set; }

        /// <summary>Kann der Ritter jetzt durchs Portal (keine Runenwahl, kein Event, kein Shop offen)?</summary>
        public bool CanEnterPortal =>
            PendingPortal && !IsGameOver && PendingEncounter == null && PendingRuneOffer == null && PendingShop == null;

        /// <summary>Das Portal wurde betreten; die Darstellung baut daraufhin die Karte des nächsten Akts.</summary>
        public event Action<OverworldSession> ActCompleted;

        /// <summary>Gegnerstufe auf einem Feld: Entfernung zur Mitte plus Aufschlag des Akts.</summary>
        public int TierAt(HexCoord coord) => coord.DistanceTo(Map.Center) + (Act - 1) * ActTierBonus;

        private void OpenPortal()
        {
            if (IsGameOver) return;
            PendingPortal = true;
        }

        /// <summary>Betritt das offene Portal (heilt teilweise) und meldet den abgeschlossenen Akt.</summary>
        public bool EnterPortal()
        {
            if (!CanEnterPortal) return false;
            PendingPortal = false;
            Stats.Heal(Stats.MaxHp * PortalHealPercent / 100);
            ExpandBoard(Progression.BoardExpansionsOnNewAct);
            ActCompleted?.Invoke(this);
            return true;
        }

        /// <summary>
        /// Erzeugt den nächsten Akt: neue Karte (Seed aus Akt und vorheriger Karte abgeleitet, falls
        /// <paramref name="seed"/> fehlt), Ritter in der Mitte, Zugzähler läuft weiter.
        /// </summary>
        public static OverworldSession CreateNextAct(MapGenerationConfig config, OverworldSession previous, int? seed = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (previous == null) throw new ArgumentNullException(nameof(previous));

            int act = previous.Act + 1;
            int nextSeed = seed ?? unchecked(previous.Map.Seed * 31 + act * 7919);
            HexMap map = MapGenerator.Generate(config.WithSeed(nextSeed));

            var session = new OverworldSession(
                map,
                new PlayerModel(map.Center),
                new TurnSystem(previous.Turns.CurrentTurn),
                new ExplorationService(map, previous.Exploration.SightRadius),
                previous.Stats,
                previous.Encounters,
                previous.Board,
                previous.RuneCatalog,
                previous._combat,
                previous.Gear,
                previous.Items,
                previous.Inventory,
                previous.RuneInventory,
                previous.Skills);
            session.Kit = previous.Kit;
            session.Act = act;
            session.Progression = previous.Progression;
            session.BoardExpansionsBought = previous.BoardExpansionsBought;
            session.CarryRecipeBook(previous);
            session.CarryModules(previous);
            session.CarryChips(previous);
            session.CarryShopLocks(previous);
            return session;
        }
    }
}
