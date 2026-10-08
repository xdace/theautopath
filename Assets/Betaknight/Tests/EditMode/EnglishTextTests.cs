using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Betaknight.Core.Arena;
using Betaknight.Core.Combat;
using Betaknight.Core.Encounters;
using Betaknight.Core.Evolution;
using Betaknight.Core.Gear;
using Betaknight.Core.Growth;
using Betaknight.Core.Modules;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    /// <summary>
    /// A-18: Alle Katalogtexte sind Englisch. Sucht per Reflection alle Text-Eigenschaften der Katalogeinträge
    /// (auch verschachtelt, z. B. Event-Optionen oder Gegner-Tafeln) nach deutschen Umlauten ab.
    /// </summary>
    public class EnglishTextTests
    {
        private static readonly char[] GermanLetters = { 'ä', 'ö', 'ü', 'ß', 'Ä', 'Ö', 'Ü' };

        private static IEnumerable<TestCaseData> Catalogs()
        {
            yield return new TestCaseData(SkillCatalog.CreateDefault().All).SetName("Skills");
            yield return new TestCaseData(RuneCatalog.CreateDefault().All).SetName("Runes");
            yield return new TestCaseData(EquipmentCatalog.CreateDefault().All).SetName("Items");
            yield return new TestCaseData(SetBonusRegistry.CreateDefault().All).SetName("Sets");
            yield return new TestCaseData(SynergyRegistry.CreateDefault().Tags).SetName("Tags");
            yield return new TestCaseData(SynergyRegistry.CreateDefault().Duos).SetName("Duos");
            yield return new TestCaseData(ReliefCatalog.CreateDefault().All).SetName("Easers");
            yield return new TestCaseData(ModuleCatalog.CreateDefault().All).SetName("Modules");
            yield return new TestCaseData(EnemyCatalog.CreateDefault().All).SetName("Enemies");
            yield return new TestCaseData(EnemyCatalog.CreateDefault().All.SelectMany(e => e.Create()).ToList()).SetName("EnemyFighters");
            yield return new TestCaseData(EncounterCatalog.CreateDefault().All).SetName("Events");
            yield return new TestCaseData(EvolutionCatalog.CreateDefault().All).SetName("Evolutions");
            yield return new TestCaseData(GrowthCatalog.CreateDefault()).SetName("Growth");
            yield return new TestCaseData(KnightKit.Defaults).SetName("Kits");
        }

        [TestCaseSource(nameof(Catalogs))]
        public void CatalogTextsHaveNoGermanUmlauts(object catalog)
        {
            var texts = new List<string>();
            Collect(catalog, texts, new HashSet<object>(new ByReference()), 0);

            Assert.IsNotEmpty(texts, "Der Scan findet Texte");
            List<string> german = texts.Where(t => t.IndexOfAny(GermanLetters) >= 0).Distinct().ToList();
            Assert.IsEmpty(german, "German text left:\n" + string.Join("\n", german));
        }

        [Test]
        public void TheScanFindsUmlautsInNestedTexts()
        {
            var texts = new List<string>();
            Collect(new[] { new Sample { Inner = new Sample { Name = "Schildwächter" } } }, texts, new HashSet<object>(new ByReference()), 0);
            Assert.Contains("Schildwächter", texts);
        }

        public class Sample
        {
            public string Name { get; set; } = "Sample";
            public Sample Inner { get; set; }
        }

        private sealed class ByReference : IEqualityComparer<object>
        {
            public new bool Equals(object a, object b) => ReferenceEquals(a, b);
            public int GetHashCode(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }

        /// <summary>Sammelt Strings aus öffentlichen Eigenschaften und Feldern; steigt in Listen und eigene Typen ab.</summary>
        private static void Collect(object value, List<string> texts, HashSet<object> seen, int depth)
        {
            if (value == null || depth > 6) return;
            if (value is string s) { texts.Add(s); return; }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is Delegate) return;
            if (!type.IsValueType && !seen.Add(value)) return;

            if (value is IDictionary dict)
            {
                foreach (object v in dict.Values) Collect(v, texts, seen, depth + 1);
                return;
            }
            if (value is IEnumerable items)
            {
                foreach (object item in items) Collect(item, texts, seen, depth + 1);
                return;
            }
            if (type.Namespace == null || !type.Namespace.StartsWith("Betaknight")) return;

            foreach (PropertyInfo p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.GetIndexParameters().Length > 0) continue;
                object v;
                try { v = p.GetValue(value); }
                catch (TargetInvocationException) { continue; }
                Collect(v, texts, seen, depth + 1);
            }
            foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Collect(f.GetValue(value), texts, seen, depth + 1);
        }
    }
}
