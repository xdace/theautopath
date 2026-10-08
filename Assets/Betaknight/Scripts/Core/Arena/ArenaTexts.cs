using System;

namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Alle Spieler-Texte der Arena an einem Ort: Kampfprotokoll, Auswertung und Hinweise, Entscheidungsgründe,
    /// Skill-Infozeilen, Wirkungsbeschreibungen und Zahlenformate (englisch, Punkt als Dezimaltrenner).
    /// Grundlage für eine spätere Lokalisierung.
    /// </summary>
    public static class ArenaTexts
    {
        // ------------------------------------------------------------------ Zahlen

        /// <summary>Ticks als Sekunden ohne unnötige Nullen: 0.5 s, 1.5 s, 5 s, 0.35 s.</summary>
        public static string Seconds(int ticks)
        {
            int hundredths = (int)((long)ticks * 100 / Ticks.PerSecond);
            int whole = hundredths / 100;
            int frac = hundredths % 100;
            if (frac == 0) return $"{whole} s";
            return frac % 10 == 0 ? $"{whole}.{frac / 10} s" : $"{whole}.{frac:00} s";
        }

        /// <summary>Sekunden mit genau einer Nachkommastelle: «1.5 s».</summary>
        public static string SecondsOneDecimal(int ticks) => $"{ticks / Ticks.PerSecond}.{ticks % Ticks.PerSecond * 10 / Ticks.PerSecond} s";

        /// <summary>Zeitstempel im Protokoll: «4.2s».</summary>
        public static string LogTime(int tick) => $"{tick / Ticks.PerSecond}.{tick % Ticks.PerSecond * 10 / Ticks.PerSecond}s";

        /// <summary>Basispunkte als Prozent: 12000 → "120 %", 2500 → "25 %", 1250 → "12.5 %".</summary>
        public static string Percent(int bp)
        {
            int abs = Math.Abs(bp);
            string sign = bp < 0 ? "−" : string.Empty;
            int whole = abs / 100;
            int frac = abs % 100;
            if (frac == 0) return $"{sign}{whole} %";
            return frac % 10 == 0 ? $"{sign}{whole}.{frac / 10} %" : $"{sign}{whole}.{frac:00} %";
        }

        /// <summary>125 → «1.25».</summary>
        public static string Hundredths(int value) => $"{value / 100}.{value % 100:00}";

        // ------------------------------------------------------------------ Namen

        public const string BasicAttack = "Basic Attack";
        public const string AlwaysLabel = "Always";
        public const string DefaultFighterName = "Fighter";
        public const string PlayerName = "Knight";
        public const string DummyTargetName = "Target";
        public static string EliteName(string enemy) => $"Elite: {enemy}";
        public static string RowName(int index) => $"Row {index + 1}";
        public const string BasicAttackRow = "Basic Attack Row";
        public static string InvertedLabel(string label) => $"NOT {label}";

        public static string StatName(StatKind kind)
        {
            switch (kind)
            {
                case StatKind.MaxHp: return "Max HP";
                case StatKind.Damage: return "Weapon Damage";
                case StatKind.AttackInterval: return "Attack Interval";
                case StatKind.AttackSpeed: return "Attack Speed";
                case StatKind.Armor: return "Armor";
                case StatKind.ArmorMultiplier: return "Armor";
                case StatKind.Dodge: return "Dodge";
                case StatKind.DodgeCap: return "Dodge Cap";
                case StatKind.Block: return "Block";
                case StatKind.Crit: return "Crit";
                case StatKind.Accuracy: return "Accuracy";
                case StatKind.AreaDamage: return "Area Damage";
                case StatKind.BlockCap: return "Block Cap";
                default: return kind.ToString();
            }
        }

        /// <summary>Anzeigename einer Ressource, null für unbekannte Ids.</summary>
        public static string ResourceName(string id)
        {
            switch (id)
            {
                case ResourceIds.Heat: return "Heat";
                case ResourceIds.Charge: return "Charge";
                case ResourceIds.Tempo: return "Haste stacks";
                default: return null;
            }
        }

        /// <summary>Namen von Schadensquellen im Protokoll, die keine Skills sind; null für unbekannte Ids.</summary>
        public static string DamageSourceName(string id)
        {
            switch (id)
            {
                case StatusIds.Burn: return "Burn";
                case "overheat": return "Overheat";
                case "heat": return "Heat";
                case "discharge": return "Discharge";
                case "hp_cost": return "HP Cost";
                case StatusIds.Poison: return "Poison";
                default: return null;
            }
        }

        /// <summary>Zustand als Zusatz hinter dem Namen («Gegner stunned»); null für unbekannte Ids.</summary>
        public static string StatusName(string id)
        {
            switch (id)
            {
                case StatusIds.Stun: return "stunned";
                case StatusIds.Burn: return "burning";
                case StatusIds.ArmorBreak: return "armor broken";
                case StatusIds.ShieldWall: return "Shield Wall";
                case StatusIds.Blinded: return "blinded";
                case StatusIds.Anchor: return "anchored";
                case StatusIds.Thrusters: return "thrusters ready";
                case StatusIds.Poison: return "poisoned";
                default: return null;
            }
        }

        // ------------------------------------------------------------------ Kampfprotokoll

        public static string LogTriggeredBy(int causeRow) => $"  ↪ triggered by Row {causeRow + 1}";
        public const string LogRepeat = "  ↻ Repeat";
        public static string LogFromQueue(string waited) => $"  ⏳ from the queue after {waited}";
        public static string LogQueued(string who, int row, string skill, string cooldownLeft) =>
            $"{who}: Row {row + 1} ({skill}) queued, "
            + (cooldownLeft != null ? $"waiting for cooldown ({cooldownLeft} left)" : "waiting (action running)");
        public static string LogTriggerExpired(string who, int causeRow, int row, string skill) =>
            $"{who}: trigger from Row {causeRow + 1} expired, Row {row + 1} ({skill}) not ready";
        public static string LogInterrupted(string who, string skill) => $"{who}: {skill} interrupted";
        public static string LogHealed(string whom, int amount) => $"{whom} heals {amount}";
        public static string LogDeath(string whom) => $"{whom} falls";
        public static string LogDodged(string whom) => $"{whom} dodges";
        public static string LogBlocked(string whom) => $"{whom} blocks";
        public static string LogCrit(string who, int amount) => $"{who}: Crit! {amount}";
        public static string LogOverheat(int level) => $"Overheat level {level}";

        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string Escaped = "Escaped through the portal";
        public const string TimeUp = "Time's up";

        // ------------------------------------------------------------------ Arena-Ansicht

        public const string PopupBlocked = "Block";
        public const string PopupDodged = "Dodged";
        public const string Ready = "ready";
        public const string QueuePrefix = "Waiting: ";

        /// <summary>Regel der Warteschlange (A-13) für Tooltips.</summary>
        public const string QueueRule = "Met rows wait their turn – higher rows first.";

        // ------------------------------------------------------------------ Entscheidungsgründe (H-04)

        public const string ReasonMissedTrigger = "missed trigger (condition not met)";
        public const string ReasonCooldown = "skill on cooldown";
        public static string ReasonCooldownLeft(string left) => $"skill on cooldown ({left} left)";
        public const string ReasonOrphaned = "skipped (no skill)";
        public const string ReasonActionRunning = "condition met, but an action is running";
        public static string ReasonQueuedCooldown(string left) => $"queued, waiting for cooldown ({left} left)";
        public const string ReasonQueuedRunning = "queued, waiting (action running)";

        // ------------------------------------------------------------------ Auswertung

        public static string TriggeredByPart(int row, int count) => $"Row {row + 1} ×{count}";
        public static string QueueStats(int queued) => $"{queued}× queued";
        public static string QueueStats(int queued, string averageWait) => $"{queued}× queued, Ø {averageWait} wait";
        public static string ConditionMetCount(int count) => $"Condition met {count}×";
        public static string BonusExecutions(int count) => $"{count} executions";
        public static string BonusDamage(int amount) => $"+{amount} damage";
        public static string BonusHealing(int amount) => $"+{amount} healing";
        public static string CooldownSaved(string time) => $"{time} cooldown saved";
        public const string BonusPrefix = "Bonus: ";
        public static string DamageSplit(string basic, string skills) => $"Basic Attack {basic} · Skills {skills}";

        public static string HintRarelyTriggered(int row, int conditionMet, string difficultyName)
        {
            string how = conditionMet == 0 ? "never triggered" : $"triggered only {conditionMet}×";
            string block = difficultyName != null ? $" ({difficultyName} block)" : string.Empty;
            return $"Row {row + 1}: {how}{block} – try an easer or a different block.";
        }

        public static string HintNeverFired(string row, string reason) => $"{row} never fired: {reason}.";
        public static string HintTopDamage(string row, string skill, string share) => $"{row} ({skill}) deals {share} of the damage.";
        public static string HintBlockedByAction(string row, int count) =>
            $"{row} was ready {count}× while another action was running. Shorter actions below help.";
        public static string HintTriggered(string row, string skill, int count, string by) => $"{row} ({skill}) was triggered {count}×, by {by}.";
        public static string HintTriggersExpired(int count, string row) =>
            $"{count} triggers on {row} expired: without the queue the skill was not ready, or the target was orphaned.";
        public static string HintBonus(string row, string symbol, int damage, string share) =>
            $"{row}: Bonus {symbol} added +{damage} damage ({share} of the row's damage).";
        public static string HintLongWait(string row, string skill, string wait) =>
            $"{row} ({skill}) waited {wait} in the queue on average. Higher rows or long casts hold it up.";
        public static string HintOtherDamage(int amount) => $"{amount} damage came from no row (set bonuses, recoil).";

        public const string NeverFiredNoDecision = "there was no decision";
        public const string NeverFiredOrphaned = "orphaned, no skill assigned";
        public const string NeverFiredConditionFalse = "condition never met";
        public const string NeverFiredCooldown = "skill was always on cooldown";
        public const string NeverFiredActionRunning = "condition was met, but another action was running every time";
        public const string NeverFiredPriority = "condition was met, but higher rows had priority";
        public const string NeverFiredConditionWhenReady = "condition never met while the skill was ready";
        public const string NeverFiredMostlyCooldown = "skill mostly on cooldown";

        // ------------------------------------------------------------------ Skill-Infos

        public const string NoDamage = "no damage";
        public static string BasicAttackInterval(string time) => $"Interval {time}";
        public static string Cooldown(string time) => $"CD {time}";
        public const string NoCooldown = "no CD";
        public static string Recovery(string time) => $"Recovery {time}";
        public static string Cast(string time) => $"Cast {time}";
        public static string CastWithBase(string cast, string baseTime) => $"{cast} (base {baseTime})";
        public static string PowerBonus(string sign, int percent) => $"{sign}{percent} % power";
        public static string CooldownBonus(string sign, string time) => $"{sign}{time} CD";
        public static string CastBonus(string sign, int percent) => $"{sign}{percent} % Cast Time";
        public static string DifficultyLine(string symbol, string name, string text) => $"Rune {symbol} {name}: {text} (included)";
        public const string DetailsKinds = "\nType: ";
        public const string DetailsGear = "\nGear: ";

        // ------------------------------------------------------------------ Wirkungen

        public const string ToAllEnemies = " to all enemies";
        public static string WithChance(string chance, string text) => $"{chance} chance: {text}";
        public static string DamageEffect(string percent, string flat, int amount, bool allEnemies, bool ignoreArmor) =>
            $"{percent} Weapon Damage{flat} ≈ {amount}{(allEnemies ? ToAllEnemies : string.Empty)}" + (ignoreArmor ? " (ignores Armor)" : string.Empty);
        public static string HealEffect(string percent, int amount) => $"heals {percent} Max HP ≈ {amount}";
        public static string StunEffect(bool allEnemies, string time) => $"stuns {(allEnemies ? "all enemies " : string.Empty)}for {time}";
        public const string InterruptEffect = "interrupts charging";
        public static string StatChangeEffect(bool onTarget, string change, string time) => $"{(onTarget ? "Enemy " : string.Empty)}{change} for {time}";
        public static string BurnEffect(string percent, int dps, int total, string time) =>
            $"Burn {percent} Weapon Damage/s ≈ {dps}/s, {total} over {time}";
        public static string StatusEffect(bool onTarget, string summary, string time) => $"{(onTarget ? "Enemy: " : string.Empty)}{summary} ({time})";
        public static string ResourceSet(string resource, int value) => $"{resource} set to {value}";
        public const string RepeatEffect = "repeats your last skill with its cast time, no cooldown";

        public static string BasicAttackDescription(string percent) => percent == null ? "Weapon Damage." : $"{percent} Weapon Damage.";
        public static string BasicAttackCooldownCut(string time) => $" Each hit shortens running skill cooldowns by {time}.";

        /// <summary>Balance-Zeile eines Skills (Budget-Prüfung).</summary>
        public static string Budget(string skill, string perTarget, bool area, string required, string action, string cooldown) =>
            $"{skill}: {perTarget} per {(area ? "target" : "execution")}, "
            + $"Budget {required} ({(area ? "area" : "single target")}, {action} action, {cooldown} CD)";
    }
}
