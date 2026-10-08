using Betaknight.Core.Evolution;
using UnityEngine;

namespace Betaknight.Overworld.Persistence
{
    /// <summary>Speichert das Rezeptbuch in den PlayerPrefs, damit entdeckte Duos und Evolutionen über Runs erhalten bleiben.</summary>
    public sealed class PlayerPrefsRecipeBookStore : IRecipeBookStore
    {
        private const string Key = "betaknight.recipebook";

        public string Load() => PlayerPrefs.GetString(Key, string.Empty);

        public void Save(string data)
        {
            PlayerPrefs.SetString(Key, data ?? string.Empty);
            PlayerPrefs.Save();
        }
    }
}
