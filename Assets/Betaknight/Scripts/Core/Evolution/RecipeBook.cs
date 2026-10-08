using System;
using System.Collections.Generic;
using System.Text;

namespace Betaknight.Core.Evolution
{
    /// <summary>Speicherort des Rezeptbuchs über Runs hinweg (z. B. PlayerPrefs in Unity, Speicher in Tests).</summary>
    public interface IRecipeBookStore
    {
        /// <summary>Gespeicherter Text oder null/leer, wenn es noch nichts gibt.</summary>
        string Load();

        void Save(string data);
    }

    /// <summary>Hält das Rezeptbuch nur im Speicher (Tests, Läufe ohne Speicherstand).</summary>
    public sealed class MemoryRecipeBookStore : IRecipeBookStore
    {
        public string Data { get; private set; }

        public string Load() => Data;

        public void Save(string data) => Data = data;
    }

    /// <summary>
    /// Rezeptbuch: welche Duos und Evolutionen der Spieler schon einmal ausgelöst hat. Nur Wissen, keine Werte: daraus
    /// wird nie ein Bonus. Gespeichert als Text, eine Zeile pro Eintrag («duo:hitze_takt», «evo:evo_inferno»).
    /// </summary>
    public sealed class RecipeBook
    {
        private const string DuoPrefix = "duo:";
        private const string EvolutionPrefix = "evo:";

        private readonly HashSet<string> _duos = new HashSet<string>();
        private readonly HashSet<string> _evolutions = new HashSet<string>();
        private IRecipeBookStore _store;

        public IReadOnlyCollection<string> Duos => _duos;
        public IReadOnlyCollection<string> Evolutions => _evolutions;

        public event Action Changed;

        public bool HasDuo(string id) => id != null && _duos.Contains(id);
        public bool HasEvolution(string id) => id != null && _evolutions.Contains(id);

        /// <summary>Trägt ein Duo ein. True, wenn es neu war (dann wird gespeichert).</summary>
        public bool DiscoverDuo(string id) => Add(_duos, id);

        /// <summary>Trägt eine Evolution ein. True, wenn sie neu war (dann wird gespeichert).</summary>
        public bool DiscoverEvolution(string id) => Add(_evolutions, id);

        private bool Add(HashSet<string> set, string id)
        {
            if (string.IsNullOrEmpty(id) || !set.Add(id)) return false;
            _store?.Save(Serialize());
            Changed?.Invoke();
            return true;
        }

        /// <summary>Verbindet das Buch mit einem Speicher: Gespeichertes kommt dazu, danach wird jede Entdeckung gespeichert.</summary>
        public void Attach(IRecipeBookStore store)
        {
            _store = store;
            if (store == null) return;
            Merge(Parse(store.Load()));
            store.Save(Serialize());
        }

        public void Merge(RecipeBook other)
        {
            if (other == null) return;
            bool changed = false;
            foreach (string d in other._duos) changed |= _duos.Add(d);
            foreach (string e in other._evolutions) changed |= _evolutions.Add(e);
            if (changed) Changed?.Invoke();
        }

        public string Serialize()
        {
            var lines = new List<string>();
            foreach (string d in _duos) lines.Add(DuoPrefix + d);
            foreach (string e in _evolutions) lines.Add(EvolutionPrefix + e);
            lines.Sort(StringComparer.Ordinal);
            var text = new StringBuilder();
            foreach (string line in lines) text.Append(line).Append('\n');
            return text.ToString();
        }

        /// <summary>Liest ein gespeichertes Buch. Unbekannte oder kaputte Zeilen werden übersprungen.</summary>
        public static RecipeBook Parse(string data)
        {
            var book = new RecipeBook();
            if (string.IsNullOrEmpty(data)) return book;
            foreach (string raw in data.Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith(DuoPrefix, StringComparison.Ordinal) && line.Length > DuoPrefix.Length)
                    book._duos.Add(line.Substring(DuoPrefix.Length));
                else if (line.StartsWith(EvolutionPrefix, StringComparison.Ordinal) && line.Length > EvolutionPrefix.Length)
                    book._evolutions.Add(line.Substring(EvolutionPrefix.Length));
            }
            return book;
        }
    }
}
