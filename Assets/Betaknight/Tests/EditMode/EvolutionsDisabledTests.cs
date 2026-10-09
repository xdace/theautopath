using Betaknight.Core.Arena;
using Betaknight.Core.Evolution;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>Evolutionen (Rezepte) sind vorübergehend aus dem Spiel: der Katalog wirkt leer, nichts entwickelt sich.</summary>
    public class EvolutionsDisabledTests
    {
        [Test]
        public void EvolutionsAreOffByDefault()
        {
            Assert.IsFalse(EvolutionCatalog.Enabled);
            EvolutionCatalog catalog = EvolutionCatalog.CreateDefault();
            Assert.IsEmpty(catalog.All);
            Assert.IsEmpty(catalog.From(EvolutionSubject.Skill, SkillIds.Ignite));
            Assert.IsNull(catalog.Get("evo_inferno"));
            Assert.IsNotEmpty(catalog.Registered, "die Rezepte bleiben im Code");
        }
    }
}
