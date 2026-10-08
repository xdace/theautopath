using System;
using System.Collections.Generic;
using Betaknight.Core.Run;

namespace Betaknight.Core.Encounters
{
    /// <summary>Eine Wahlmöglichkeit eines Events. Kleine Events haben genau eine, die automatisch gewählt wird.</summary>
    public sealed class EncounterOption
    {
        public string Text { get; }

        /// <summary>Gold, das vor der Wirkung bezahlt werden muss.</summary>
        public int GoldCost { get; }

        public IReadOnlyList<EncounterEffect> Effects { get; }

        public EncounterOption(string text, int goldCost, params EncounterEffect[] effects)
        {
            if (goldCost < 0) throw new ArgumentOutOfRangeException(nameof(goldCost));
            Text = text ?? string.Empty;
            GoldCost = goldCost;
            Effects = effects ?? Array.Empty<EncounterEffect>();
        }

        public EncounterOption(string text, params EncounterEffect[] effects) : this(text, 0, effects) { }

        public bool IsAvailable(PlayerStats stats) => stats.Gold >= GoldCost;
    }
}
