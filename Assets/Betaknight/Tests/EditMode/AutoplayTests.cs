using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Autoplay;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using NUnit.Framework;

namespace Betaknight.Tests.EditMode
{
    public class AutoplayTests
    {
        private static readonly int[] Seeds = { 11, 2024, 777 };

        [Test]
        public void BotSpieltVollenRunOhneFehler()
        {
            foreach (int seed in Seeds)
            {
                AutoplayReport report = HeadlessAutoplay.Run(seed, targetAct: 2);
                TestContext.WriteLine(report.Summary());

                Assert.That(report.ExceptionCount, Is.EqualTo(0), $"Seed {seed}: {string.Join("\n", report.Exceptions)}");
                Assert.That(report.HangCount, Is.EqualTo(0), $"Seed {seed}: {string.Join("\n", report.Hangs)}");
                Assert.That(report.EndReason, Is.EqualTo("Game Over").Or.EqualTo("Akt 2 erreicht"), $"Seed {seed}");
                Assert.That(report.Turns, Is.GreaterThan(0), $"Seed {seed}");
                Assert.That(report.Actions, Is.GreaterThan(report.Turns), $"Seed {seed}: der Bot soll mehr tun als laufen");
                Assert.That(report.FightsWon + report.FightsLost, Is.GreaterThan(0), $"Seed {seed}");
                Assert.That(report.Ok, Is.True);
            }
        }

        [Test]
        public void BotNutztAlleBausteineDesSpiels()
        {
            // Über mehrere Seeds soll die Strategie jede Art von Entscheidung mindestens einmal treffen
            // (Seed 1 bekommt bis Akt 3 einen Auslöser angeboten).
            var summary = new AutoplaySummary();
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 }) summary.Runs.Add(HeadlessAutoplay.Run(seed, targetAct: seed == 1 ? 3 : 2));

            Assert.That(summary.Ok, Is.True, summary.Summary());
            var rewards = summary.Runs.SelectMany(r => r.Rewards.Keys).Distinct().ToList();
            Assert.That(rewards, Does.Contain("Heilung").Or.Contain("Runen-Stufe"), "Lagerfeuer oder Shop-Heilung");
            Assert.That(rewards.Count, Is.GreaterThanOrEqualTo(4), string.Join(", ", rewards));
            Assert.That(summary.Runs.Sum(r => r.Bosses), Is.GreaterThan(0), "mindestens ein Boss");
            Assert.That(summary.Runs.Any(r => r.Act >= 2), Is.True, "mindestens ein Run erreicht Akt 2");
            Assert.That(summary.Runs.Sum(r => r.TriggersSet), Is.GreaterThan(0), "mindestens ein Auslöser gelegt");
            Assert.That(summary.Runs.All(r => r.BoardRows.Count > 0), Is.True);
        }

        [Test]
        public void TheBotMeasuresHowOftenEveryBlockFires()
        {
            AutoplayReport a = HeadlessAutoplay.Run(5, targetAct: 2);
            AutoplayReport b = HeadlessAutoplay.Run(5, targetAct: 2);
            Assert.IsNotEmpty(a.RuneStats);
            Assert.IsTrue(a.RuneStats.Values.All(s => s.Fights > 0 && s.FightsMet <= s.Fights));
            Assert.Greater(a.RuneStats.Values.Sum(s => s.Fired), 0);
            Assert.AreEqual(RuneFireStats.Table(new[] { a }), RuneFireStats.Table(new[] { b }));
            StringAssert.Contains("\"runeStats\"", a.ToJson());
        }

        [Test]
        public void GleicherSeedSpieltGleichenRun()
        {
            AutoplayReport a = HeadlessAutoplay.Run(5, targetAct: 2);
            AutoplayReport b = HeadlessAutoplay.Run(5, targetAct: 2);
            Assert.That(b.Turns, Is.EqualTo(a.Turns));
            Assert.That(b.Actions, Is.EqualTo(a.Actions));
            Assert.That(b.EndReason, Is.EqualTo(a.EndReason));
            Assert.That(b.BoardRows, Is.EqualTo(a.BoardRows));
        }

        [Test]
        public void KitWirdNachSeedGewaehlt()
        {
            var kits = KnightKit.Defaults;
            Assert.That(AutoplayBot.ChooseKit(kits, 0), Is.SameAs(kits[0]));
            Assert.That(AutoplayBot.ChooseKit(kits, 1), Is.SameAs(kits[1 % kits.Count]));
            Assert.That(AutoplayBot.ChooseKit(kits, -1), Is.Not.Null);
        }

        [Test]
        public void KommandozeileWirdGelesen()
        {
            AutoplayOptions o = AutoplayOptions.Parse(new[]
                { "Betaknight.exe", "-autoplay", "-seed", "42", "-runs", "5", "-speed", "4.5", "-report", "out/bericht.json", "-quit" });
            Assert.That(o.Enabled, Is.True);
            Assert.That(o.Seed, Is.EqualTo(42));
            Assert.That(o.Runs, Is.EqualTo(5));
            Assert.That(o.Speed, Is.EqualTo(4.5f));
            Assert.That(o.ReportPath, Is.EqualTo("out/bericht.json"));
            Assert.That(o.Quit, Is.True);
            Assert.That(o.SeedFor(2, () => 0), Is.EqualTo(44));
            Assert.That(o.Warnings, Is.Empty);

            AutoplayOptions normal = AutoplayOptions.Parse(new[] { "Betaknight.exe" });
            Assert.That(normal.Enabled, Is.False);
            Assert.That(normal.Runs, Is.EqualTo(1));
            Assert.That(normal.Quit, Is.False);

            AutoplayOptions broken = AutoplayOptions.Parse(new[] { "-autoplay", "-seed", "abc", "-speed", "0" });
            Assert.That(broken.Seed, Is.Null);
            Assert.That(broken.Speed, Is.EqualTo(1f));
            Assert.That(broken.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void BerichtIstGueltigesJson()
        {
            var summary = new AutoplaySummary();
            summary.Runs.Add(HeadlessAutoplay.Run(3, targetAct: 2));
            var broken = new AutoplayReport { Seed = 9, Kit = "Test \"Ritter\"", EndReason = "Hänger" };
            broken.AddException("Zeile 1\nZeile 2");
            broken.AddHang("keine Aktion");
            broken.FpsAverage = 59.5;
            broken.FpsMin = 12;
            summary.Runs.Add(broken);

            string json = summary.ToJson();
            Assert.That(summary.ExitCode, Is.EqualTo(1));
            Assert.That(json, Does.Contain("\"exitCode\": 1"));
            Assert.That(json, Does.Contain("\"kit\": \"Test \\\"Ritter\\\"\""));
            Assert.That(json, Does.Contain("Zeile 1\\nZeile 2"));
            Assert.That(json, Does.Contain("\"fpsAverage\": null").Or.Contain("\"fpsAverage\": 59.5"));
            Assert.That(JsonCheck.IsBalanced(json), Is.True, json);

            AutoplayReport single = summary.Runs[0];
            string one = single.ToJson();
            foreach (string field in new[] { "seed", "kit", "turns", "act", "fightsWon", "fightsLost", "elitesWon", "bosses", "rewards",
                         "boardRows", "modules", "triggersSet", "duos", "durationSeconds", "fpsAverage", "fpsMin", "exceptionCount",
                         "errorLogCount", "errorLogs", "hangCount", "endReason" })
                Assert.That(one, Does.Contain($"\"{field}\":"), field);
        }

        [Test]
        public void ZusammenfassungNenntEndgrundUndStatus()
        {
            var report = new AutoplayReport { Seed = 1, Kit = "Bastion", EndReason = "Game Over", Act = 1, Turns = 30 };
            Assert.That(report.Summary(), Does.Contain("Game Over").And.Contain("OK"));
            report.AddHang("x");
            Assert.That(report.Summary(), Does.Contain("FEHLER"));
            Assert.That(report.Ok, Is.False);
        }

        /// <summary>Minimale Prüfung: Klammern und Anführungszeichen sind ausgeglichen.</summary>
        private static class JsonCheck
        {
            public static bool IsBalanced(string json)
            {
                int depth = 0;
                bool inString = false;
                for (int i = 0; i < json.Length; i++)
                {
                    char c = json[i];
                    if (inString)
                    {
                        if (c == '\\') i++;
                        else if (c == '"') inString = false;
                        continue;
                    }
                    if (c == '"') inString = true;
                    else if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;
                    if (depth < 0) return false;
                }
                return depth == 0 && !inString;
            }
        }
    }
}
