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

        /// <summary>Höchste Stufe eines Skill-Exemplars (+3).</summary>
        public int MaxSkillLevel = 3;

        /// <summary>Wie stark ein Skill-Exemplar pro Stufe wird.</summary>
        public SkillLevelRules SkillLevels = new SkillLevelRules();

        // ------------------------------------------------------------------ Skills als Belohnung

        /// <summary>Chance in Prozent, dass ein Angebot einen Skill statt der letzten Rune enthält, je Quelle.</summary>
        public int SkillOfferChanceVictory = 35;
        public int SkillOfferChanceElite = 60;
        public int SkillOfferChanceTreasure = 50;
        public int SkillOfferChanceMine = 35;

        /// <summary>Angebote aus Runensplittern (Ereignisse, Kämpfe, Truhen) enthalten so oft einen Skill.</summary>
        public int SkillOfferChanceShards = 30;

        /// <summary>So viele Skills liegen in jedem Shop.</summary>
        public int ShopSkillCount = 1;

        /// <summary>Angebotsgewicht eines Skills: Grundwert, ×Faktor bei passenden Skill-Arten im Build.</summary>
        public int SkillOfferWeight = 10;
        public int SkillKindMatchFactor = 3;

        // ------------------------------------------------------------------ Module (selten)

        /// <summary>Chance in Prozent auf ein Modul als zusätzliche Wahl, je Quelle. Boss-Flucht gibt immer eines.</summary>
        public int ModuleOfferChanceElite = 35;
        public int ModuleOfferChanceTreasure = 15;

        /// <summary>Chance, dass ein Shop einen (teuren) Modul-Platz hat.</summary>
        public int ShopModuleChance = 50;

        public int ModuleOfferChance(string source)
        {
            switch (source)
            {
                case RewardSources.Elite: return ModuleOfferChanceElite;
                case RewardSources.Treasure: return ModuleOfferChanceTreasure;
                default: return 0;
            }
        }

        public int SkillOfferChance(string source)
        {
            switch (source)
            {
                case RewardSources.Victory: return SkillOfferChanceVictory;
                case RewardSources.Elite: return SkillOfferChanceElite;
                case RewardSources.Treasure: return SkillOfferChanceTreasure;
                case RewardSources.MineDefended: return SkillOfferChanceMine;
                case RewardSources.Shards: return SkillOfferChanceShards;
                default: return 0;
            }
        }
    }
}
