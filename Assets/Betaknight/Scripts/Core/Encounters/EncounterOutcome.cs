using System.Collections.Generic;
using Betaknight.Core.Map;

namespace Betaknight.Core.Encounters
{
    /// <summary>Ein Event, das auf eine Entscheidung des Spielers wartet.</summary>
    public sealed class EncounterPrompt
    {
        public HexCell Cell { get; }
        public EncounterDefinition Definition { get; }

        public EncounterPrompt(HexCell cell, EncounterDefinition definition)
        {
            Cell = cell;
            Definition = definition;
        }
    }

    /// <summary>Ergebnis eines ausgelösten Events, mit lesbaren Zeilen für Hinweis oder Fenster.</summary>
    public sealed class EncounterOutcome
    {
        public HexCell Cell { get; }
        public EncounterDefinition Definition { get; }
        public EncounterOption Option { get; }

        /// <summary>Zum Beispiel "+3 Gold", "-2 HP", "Shop entdeckt".</summary>
        public IReadOnlyList<string> Lines { get; }

        public EncounterOutcome(HexCell cell, EncounterDefinition definition, EncounterOption option, IReadOnlyList<string> lines)
        {
            Cell = cell;
            Definition = definition;
            Option = option;
            Lines = lines;
        }

        public string Summary => Lines.Count == 0 ? SessionTexts.NothingHappened : string.Join(", ", Lines);
    }
}
