using System;
using System.Collections.Generic;
using Betaknight.Core.Combat;
using Betaknight.Core.Hex;
using Betaknight.Core.Runes;

namespace Betaknight.Core
{
    /// <summary>Bergen nach einem Sieg: Teile, die der Gegner im Kampf benutzt hat. Elite lässt mehr bergen.</summary>
    public sealed class SalvageOffer
    {
        private readonly List<EnemyLoot> _parts;

        public string EnemyName { get; }
        public IReadOnlyList<EnemyLoot> Parts => _parts;

        /// <summary>Wie viele Teile noch geborgen werden dürfen.</summary>
        public int PicksLeft { get; internal set; }

        /// <summary>Belohnungsquelle der Runenwahl, die nach dem Bergen kommt.</summary>
        internal string RewardSource { get; }

        public SalvageOffer(string enemyName, IEnumerable<EnemyLoot> parts, int picks, string rewardSource)
        {
            EnemyName = enemyName;
            _parts = new List<EnemyLoot>(parts ?? Array.Empty<EnemyLoot>());
            PicksLeft = Math.Max(0, Math.Min(picks, _parts.Count));
            RewardSource = rewardSource;
        }

        internal void RemoveAt(int index) => _parts.RemoveAt(index);
    }

    /// <summary>
    /// Bergen (Gegner-Platinen): nach einem Sieg wählt der Ritter Teile aus der Platine des Gegners: Skills, Runen, Module
    /// und Chips, die dieser im Kampf benutzt hat. Danach folgt die gewohnte Belohnungswahl.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public SalvageOffer PendingSalvage { get; private set; }

        public event Action<SalvageOffer> SalvageStarted;

        /// <summary>Gegner und Platine eines Kampffeldes sind fest: aus Karten-Seed und Feld (jeder Akt hat eine eigene Karte).</summary>
        public int EncounterSeed(HexCoord coord) => unchecked(Map.Seed * 486187739 + coord.Q * 92821 + coord.R * 68917 + 17);

        /// <summary>Startet das Bergen. Ohne Teile geht es direkt zur Belohnungswahl.</summary>
        private void StartSalvage(CombatResult result, string rewardSource)
        {
            if (result.Loot == null || result.Loot.Count == 0 || result.LootPicks <= 0)
            {
                OfferRunes(rewardSource);
                return;
            }
            PendingSalvage = new SalvageOffer(result.EnemyName, result.Loot, result.LootPicks, rewardSource);
            SalvageStarted?.Invoke(PendingSalvage);
        }

        public bool CanTakeSalvage(int index) =>
            PendingSalvage != null && PendingSalvage.PicksLeft > 0 && index >= 0 && index < PendingSalvage.Parts.Count;

        /// <summary>Birgt ein Teil. Sind alle Wahlen verbraucht, schliesst das Bergen und die Belohnungswahl folgt.</summary>
        public bool TakeSalvage(int index)
        {
            if (!CanTakeSalvage(index)) return false;
            SalvageOffer offer = PendingSalvage;
            EnemyLoot part = offer.Parts[index];
            offer.RemoveAt(index);
            offer.PicksLeft--;

            // Während des Erhalts darf nichts anderes warten (z. B. volle Inventare melden sich danach).
            PendingSalvage = null;
            Grant(part);
            if (offer.PicksLeft > 0 && offer.Parts.Count > 0 && !IsBusy)
            {
                PendingSalvage = offer;
                return true;
            }
            FinishSalvage(offer);
            return true;
        }

        /// <summary>Verzichtet auf die restlichen Teile.</summary>
        public bool SkipSalvage()
        {
            SalvageOffer offer = PendingSalvage;
            if (offer == null) return false;
            PendingSalvage = null;
            FinishSalvage(offer);
            return true;
        }

        private void FinishSalvage(SalvageOffer offer)
        {
            if (!IsBusy) OfferRunes(offer.RewardSource);
        }

        private void Grant(EnemyLoot part)
        {
            switch (part.Kind)
            {
                case EnemyLootKind.Skill:
                    if (SkillCatalog.TryGet(part.Id, out _)) GainSkill(part.Id);
                    break;
                case EnemyLootKind.Module:
                    if (ModuleCatalog.Contains(part.Id)) GainModule(part.Id);
                    break;
                case EnemyLootKind.Chip:
                    GrantChip(part.Id);
                    break;
                case EnemyLootKind.Rune:
                    if (RuneCatalog.TryGet(part.Id, out RuneDefinition rune)) PlaceNewRune(rune, -1);
                    break;
            }
        }
    }
}
