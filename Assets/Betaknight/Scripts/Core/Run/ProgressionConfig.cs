using Betaknight.Core.Arena;

namespace Betaknight.Core.Run
{
    /// <summary>
    /// Alle Werte, mit denen ein Build über den Run wächst: Tafel-Erweiterungen, Elite-Gegner, Stufen für
    /// Ausrüstung und Skills, Preis des Runenplatzes im Shop. An einer Stelle, damit Balance-Änderungen
    /// nicht im Code verstreut sind.
    /// </summary>
    public sealed class ProgressionConfig
    {
        // ------------------------------------------------------------------ Tafel-Erweiterung

        /// <summary>Obergrenze der Zeilen auf der Logik-Tafel.</summary>
        public int MaxBoardRows = 8;

        /// <summary>Garantierte Erweiterung bei jeder Boss-Flucht durchs Portal.</summary>
        public int BoardRowsOnBossEscape = 1;

        /// <summary>Garantierte Erweiterung beim Akt-Wechsel (Portal betreten).</summary>
        public int BoardRowsOnNewAct = 1;

        /// <summary>Chance in Prozent, dass eine Elite-Belohnung die Tafel-Erweiterung anbietet.</summary>
        public int EliteBoardExpansionChance = 50;

        /// <summary>Chance in Prozent für eine seltene Truhe mit Tafel-Erweiterung im Angebot.</summary>
        public int TreasureBoardExpansionChance = 10;

        /// <summary>Preis des Runenplatzes im Shop: Basis + Schritt × bereits gekaufte (20, 35, 50 …).</summary>
        public int SlotPriceBase = 20;
        public int SlotPriceStep = 15;

        public int SlotPrice(int boughtThisRun) => SlotPriceBase + SlotPriceStep * System.Math.Max(0, boughtThisRun);

        // ------------------------------------------------------------------ Elite

        /// <summary>Elite-Gegner kämpfen so viele Stufen über ihrem Ring.</summary>
        public int EliteTierBonus = 2;

        /// <summary>Elite-Gegner haben so viel Prozent Leben und Schaden.</summary>
        public int EliteHpPercent = 150;
        public int EliteDamagePercent = 125;

        /// <summary>Zusätzliches Gold für einen Elite-Sieg.</summary>
        public int EliteGoldBonus = 4;

        // ------------------------------------------------------------------ Stufen

        /// <summary>Höchste Stufe eines Ausrüstungsteils (+3).</summary>
        public int MaxItemLevel = 3;

        /// <summary>Jede Stufe gibt so viel Prozent der Grundwerte dazu (nur vorteilhafte Werte, mindestens 1).</summary>
        public int ItemStatPercentPerLevel = 50;

        /// <summary>Wie stark die Skills eines Teils pro Stufe werden.</summary>
        public SkillLevelRules SkillLevels = new SkillLevelRules();
    }
}
