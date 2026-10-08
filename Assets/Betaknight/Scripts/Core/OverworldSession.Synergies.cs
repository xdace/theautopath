using System;
using System.Collections.Generic;
using Betaknight.Core.Evolution;
using Betaknight.Core.Gear;

namespace Betaknight.Core
{
    /// <summary>
    /// Synergie-Tags der getragenen Ausrüstung: Zähler, Schwellen, Duos und Rezeptbuch. Ein Duo bleibt eine Silhouette,
    /// bis es einmal in einem Kampf aktiv war; danach steht es mit Namen und Wirkung im Rezeptbuch.
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Rezeptbuch (Duos und Evolutionen): reines Wissen, über Akte und mit <see cref="UseRecipeStore"/> über Runs.</summary>
        public RecipeBook RecipeBook { get; private set; } = new RecipeBook();

        /// <summary>Verbindet das Rezeptbuch mit einem Speicher (in Unity PlayerPrefs). Gespeichertes Wissen kommt dazu.</summary>
        public void UseRecipeStore(IRecipeBookStore store) => RecipeBook.Attach(store);

        public SynergyRegistry Synergies { get; } = SynergyRegistry.CreateDefault();

        /// <summary>Ein Duo wurde zum ersten Mal ausgelöst (Kampfbeginn mit aktivem Duo).</summary>
        public event Action<SynergyDuo> DuoDiscovered;

        public bool IsDuoDiscovered(string duoId) => RecipeBook.HasDuo(duoId);

        public IReadOnlyCollection<string> DiscoveredDuos => RecipeBook.Duos;

        /// <summary>Zähler aller getragenen Tags, z. B. «Ladung 3/4».</summary>
        public List<SynergyCounter> TagCounters() => Synergies.Counters(Gear);

        public List<SynergyDuo> ActiveDuos() => Synergies.ActiveDuos(Gear);

        /// <summary>Name eines Duos, oder «???» solange es nicht entdeckt ist (Silhouette).</summary>
        public string DuoName(SynergyDuo duo) => IsDuoDiscovered(duo.Id) ? duo.Name : "???";

        /// <summary>Was ein Teil beim Anlegen an Tags bewirken würde: «→ Ladung 4/6: Schwelle!».</summary>
        public List<string> TagPreview(EquipmentDefinition item) => Synergies.Preview(Gear, item, DuoName);

        /// <summary>Vor jedem Kampf: aktive Duos gelten als ausgelöst und kommen ins Rezeptbuch.</summary>
        private void DiscoverDuos()
        {
            foreach (SynergyDuo duo in Synergies.ActiveDuos(Gear))
            {
                if (!RecipeBook.DiscoverDuo(duo.Id)) continue;
                DuoDiscovered?.Invoke(duo);
                BuildImproved?.Invoke($"Duo entdeckt: {duo.Name} ({Synergies.NameOf(duo.TagA)} + {Synergies.NameOf(duo.TagB)})");
            }
        }

        private void CarryRecipeBook(OverworldSession previous) => RecipeBook = previous.RecipeBook;

        /// <summary>Hinweis für ein unentdecktes Duo: einer der beiden Tags, der zweite bleibt offen.</summary>
        public string DuoHint(SynergyDuo duo) => $"{Synergies.NameOf(duo.TagA)} und ein zweiter Tag, beide auf 4";
    }
}
