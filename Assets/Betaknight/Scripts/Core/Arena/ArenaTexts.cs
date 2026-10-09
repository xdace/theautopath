using System;
using System.Collections.Generic;

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
        public const string DefaultFighterName = "Fighter";
        public const string PlayerName = "Knight";
        public const string DummyTargetName = "Target";
        public static string EliteName(string enemy) => $"Elite: {enemy}";
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
                case StatKind.CastPercent: return "Cast Time";
                default: return kind.ToString();
            }
        }

        /// <summary>Anzeigename einer Ressource, null für unbekannte Ids.</summary>
        public static string ResourceName(string id)
        {
            switch (id)
            {
                case ResourceIds.Heat: return "Heat";
                case ResourceIds.Charge: return "Static";
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
                case "overflow": return "Overflow";
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
                case StatusIds.Haste: return "hasted";
                case StatusIds.Slow: return "slowed";
                case StatusIds.Freeze: return "frozen";
                case StatusIds.Latency: return "lagging";
                default: return null;
            }
        }

        // ------------------------------------------------------------------ Kampfprotokoll

        public static string LogCharged(string who, string component, int stored, int cells, int gained) =>
            gained > 0 ? $"{who}: {component} charged +{gained} → {stored}/{cells}" : $"{who}: {component} keeps its charge {stored}/{cells} (already queued, runs again after its cast)";
        public static string ChargeStats(int times, int gained, int left, int cells) =>
            times > 0 ? $"{times}× (+{gained})" + (left > 0 ? $" · {left}/{cells} left" : string.Empty) : $"{left}/{cells} left";
        public static string ChargeBadge(int stored, int cells) => $"⚡{stored}/{cells}";
        public static string LogSpillover(string who, string component, int cells) => $"{who}: {component} spills {cells} overcharge to its neighbours";
        public static string LogTriggeredBy(int causeRow) => $"  ↪ triggered by #{causeRow + 1}";
        public static string LogPulsedBy(int causeRow) => $"  ⚡ pulse from #{causeRow + 1}";
        public const string LogRepeat = "  ↻ Repeat";
        public static string LogFromQueue(string waited) => $"  ⏳ from the queue after {waited}";
        public static string LogQueued(string who, string component) => $"{who}: {component} queued";
        public static string LogMissed(string who, string component, string reason) => $"{who}: missed trigger on {component} ({reason})";
        public static string LogRelay(string who, string relay) => $"{who}: {relay} triggers";
        public static string LogFrozen(string who, string component, string time) => $"{who}: {component} frozen for {time}";

        // ------------------------------------------------------------------ Pulse und Chips (A-20)

        /// <summary>«AND (On Hit + HP Full)», ohne Eingänge «AND (no relay)».</summary>
        public static string GateLabel(string gate, IReadOnlyList<string> inputs) =>
            inputs == null || inputs.Count == 0 ? $"{gate} (no relay)" : $"{gate} ({string.Join(" + ", inputs)})";

        public static string LogPulse(string who, string from, string to, int hops) =>
            hops <= 1 ? $"{who}: pulse {from} → {to}" : $"{who}: pulse {from} → {to} via {hops - 1} trace{(hops - 1 == 1 ? "" : "s")}";
        public static string LogPulseQueued(string who, string component, string from, string relay) =>
            $"{who}: {component} queued by pulse from {from} (powered by {relay})";
        public static string LogCapacitorStored(string who, string capacitor, int stored, int capacity) =>
            $"{who}: {capacitor} stores a pulse ({stored}/{capacity})";
        public static string LogCapacitorReleased(string who, string capacitor, int pulses) =>
            $"{who}: {capacitor} releases {pulses} pulse{(pulses == 1 ? "" : "s")}";
        public static string LogPulseLost(string who, string capacitor) => $"{who}: {capacitor} is full, pulse lost";
        public static string LogFuseBlown(string who, string fuse) => $"{who}: {fuse} blows (once per fight)";
        public static string LogRelayState(string who, string relay, bool on) => $"{who}: {relay} {(on ? "on" : "off")}";
        public const string PulseCause = "pulse";
        public static string CapacitorName(int index) => $"Capacitor {index + 1}";
        public static string LogInterrupted(string who, string skill) => $"{who}: {skill} interrupted";
        public static string LogHealed(string whom, int amount) => $"{whom} heals {amount}";
        public static string LogDeath(string whom) => $"{whom} falls";
        public static string LogDodged(string whom) => $"{whom} dodges";
        public static string LogBlocked(string whom) => $"{whom} blocks";
        public static string LogCrit(string who, int amount) => $"{who}: Crit! {amount}";
        public static string LogOverheat(int level) => $"Thermal Throttling step {level}: computing time and damage up";

        // ------------------------------------------------------------------ Eigene Effekte (A-21)

        public const string MissOverflow = "queue overflow, turned into a shock";
        public const string MissOverheated = "overheated, skipped";

        /// <summary>Name eines eigenen Effekts (Overclock, Bit Flip …).</summary>
        public static string EffectName(string id) =>
            Circuit.CircuitEffectCatalog.Shared.TryGet(id, out Circuit.CircuitEffectDefinition e) ? e.Name : id;

        public static string LogHeat(string who, string component, int heat, int skipAt, string from) =>
            from != null ? $"{who}: {component} Heat {heat}/{skipAt} (Overclock {from})" : $"{who}: {component} Heat {heat}/{skipAt}";
        public static string LogHeatReset(string who, string component) => $"{who}: {component} cooled down (Heat 0)";
        public static string LogHeatSkip(string who, string component, int heat) => $"{who}: {component} overheated at {heat} Heat and skips this execution";
        public static string LogRecursion(string who, string component, int depth, int percent) =>
            $"{who}: {component} calls itself again (Recursion depth {depth}, +{percent} % effect)";
        public static string LogRecursionLimit(string who, string component, int depth) => $"{who}: {component} hits the stack limit at depth {depth}";
        public static string LogParallel(string who, string component) => $"{who}: {component} starts on a Parallel Thread";
        public static string LogQueueJump(string who, string component) => $"{who}: {component} jumps to the front of the queue (Interrupt)";
        public static string LogOverflow(string who, string component, int entries) =>
            $"{who}: queue full ({entries}), {component} overflows into a shock on all enemies";
        public static string LogHack(string who, string whom, string hack, string what) => $"{who} hacks {whom}: {hack}{(what != null ? " → " + what : "")}";
        public static string LogHackFailed(string who, string hack) => $"{who}: {hack} finds no target";
        public static string LogHackBlocked(string whom, string hack, int left) => $"{whom}'s Firewall blocks {hack} ({left} left)";
        public static string LogJammed(string who, string relay, int left) => $"{who}: {relay} is jammed and ignores this trigger ({left} left)";
        public static string LogFlipEnded(string who, string relay) => $"{who}: {relay} flips back";
        public static string LogHijacked(string who, string whom, string component) => $"{who} runs {whom}'s {component} (Hijack)";
        public static string LogShortCircuit(string who, string whom, string component) => $"{who}: {component} short-circuits and hits {whom}";
        public static string HackFlip(string relay, string time) => $"{relay} inverted for {time}";
        public static string HackJam(string relay, int left) => $"{relay} ignores the next {left} triggers";
        public static string HackHijack(string component) => $"next {component} runs for the hacker";
        public static string HackShort(string component) => $"{component} fires at once";
        public static string HackLatency(string time, int percent) => $"computing time +{percent} % for {time}";
        public static string LogAmplified(int percent) => $" (+{percent} % amplified)";

        public const string Victory = "Victory";
        public const string Defeat = "Defeat";
        public const string Escaped = "Escaped through the portal";
        public const string TimeUp = "Time's up";

        // ------------------------------------------------------------------ Arena-Ansicht

        public const string PopupBlocked = "Block";
        public const string PopupDodged = "Dodged";
        public const string Ready = "ready";
        public const string QueuePrefix = "Waiting: ";


        // ------------------------------------------------------------------ Platine (A-19)

        /// <summary>Regel der Warteschlange (A-19) für Tooltips.</summary>
        public const string QueueRule = "Triggered components wait their turn in the order they were triggered (first in, first out); Interrupt jumps ahead. The Basic Attack fills the gaps.";
        public const string FallbackLabel = "fills the gaps";
        public const string NotPowered = "not powered";
        public const string NotPoweredTooLarge = "charging (too large)";
        public const string CoreName = "Core";
        public static string CoreBonus(int percent) => $"Touching components +{percent} % effect";
        public static string ComponentName(int index, string skill) => $"#{index + 1} {skill}";
        public static string RelayName(int index, string label) => $"Relay {index + 1} ({label})";

        public const string MissAlreadyQueued = "already queued";
        public const string MissTooLarge = "no charge left for it (smaller ones were filled first)";
        public const string MissFrozen = "frozen";
        public const string MissOrphaned = "no skill";

        // ------------------------------------------------------------------ Auswertung

        public static string TriggeredByPart(int component, int count) => $"#{component + 1} ×{count}";
        public static string TriggeredCount(int count) => $"Triggered {count}×";
        public static string QueueStats(int queued) => $"{queued}× queued";
        public static string QueueStats(int queued, string averageWait) => $"{queued}× queued, Ø {averageWait} wait";
        public static string BonusExecutions(int count) => $"{count} executions";
        public static string BonusDamage(int amount) => $"+{amount} damage";
        public static string BonusHealing(int amount) => $"+{amount} healing";
        public static string CastSaved(string time) => $"{time} cast time saved";
        public const string BonusPrefix = "Bonus: ";
        public static string DamageSplit(string basic, string skills) => $"Basic Attack {basic} · Skills {skills}";

        public static string HintRarelyTriggered(string component, int triggered, string difficultyName)
        {
            string how = triggered == 0 ? "never triggered" : $"triggered only {triggered}×";
            string relay = difficultyName != null ? $" ({difficultyName} relay)" : string.Empty;
            return $"{component}: {how}{relay} – try an easer or a different relay.";
        }

        public static string HintNeverFired(string component, string reason) => $"{component} never fired: {reason}.";
        public static string HintTopDamage(string component, string share) => $"{component} deals {share} of the damage.";
        public static string HintMissedQueued(string component, int count) =>
            $"{component} missed {count} triggers because it was still queued. A faster cast or a less eager relay helps.";
        public static string HintTriggered(string component, int count, string by) => $"{component} was triggered {count}× by {by}.";
        public static string HintPulsed(string component, int count, string by) => $"{component} fired {count}× from pulses ({by}).";
        public static string HintBonus(string component, string symbol, int damage, string share) =>
            $"{component}: Bonus {symbol} added +{damage} damage ({share} of its damage).";
        public static string HintLongWait(string component, string wait) =>
            $"{component} waited {wait} in the queue on average. Components triggered before it or long casts hold it up.";
        public static string HintOtherDamage(int amount) => $"{amount} damage came from no component (set bonuses, recoil).";

        public const string NeverFiredOrphaned = "no skill placed";
        public const string NeverFiredTooLarge = "too large for every touching relay";
        public const string NeverFiredUnpowered = "no relay touches it";
        public const string NeverFiredOther = "its relay never triggered";
        public const string NeverFiredStuckInQueue = "it waited in the queue until the end (other components kept going first)";

        // ------------------------------------------------------------------ Skill-Infos

        public const string NoDamage = "no damage";
        public static string BasicAttackInterval(string time) => $"Interval {time}";
        public static string Size(string shape, int cells) => $"{shape} ({cells} {(cells == 1 ? "cell" : "cells")})";
        public static string Recovery(string time) => $"Recovery {time}";
        public static string Cast(string time) => $"Cast {time}";
        public static string CastWithBase(string cast, string baseTime) => $"{cast} (base {baseTime})";
        public static string PowerBonus(string sign, int percent) => $"{sign}{percent} % power";
        public static string CastBonus(string sign, int percent) => $"{sign}{percent} % Cast Time";
        public static string DifficultyLine(string symbol, string name, string text) => $"Relay {symbol} {name}: {text} (included)";
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
        public static string FreezeEffect(bool allEnemies, string time) =>
            $"freezes the largest component of {(allEnemies ? "all enemies" : "the enemy")} for {time}";
        public static string StatChangeEffect(bool onTarget, string change, string time) => $"{(onTarget ? "Enemy " : string.Empty)}{change} for {time}";
        public static string BurnEffect(string percent, int dps, int total, string time) =>
            $"Burn {percent} Weapon Damage/s ≈ {dps}/s, {total} over {time}";
        public static string StatusEffect(bool onTarget, string summary, string time) => $"{(onTarget ? "Enemy: " : string.Empty)}{summary} ({time})";
        public static string ResourceSet(string resource, int value) => $"{resource} set to {value}";
        public const string RepeatEffect = "repeats your last skill with its cast time";

        public static string BasicAttackDescription(string percent) => percent == null ? "Weapon Damage." : $"{percent} Weapon Damage.";

        /// <summary>Balance-Zeile eines Skills (Budget-Prüfung).</summary>
        public static string Budget(string skill, string perTarget, bool area, string required, string shape, int cells) =>
            $"{skill}: {perTarget} per {(area ? "target" : "execution")}, "
            + $"Budget {required} ({(area ? "area" : "single target")}, {shape} = {cells} {(cells == 1 ? "cell" : "cells")})";
    }
}
