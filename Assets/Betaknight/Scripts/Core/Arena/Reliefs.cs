using System;
using System.Collections.Generic;
using System.Linq;

namespace Betaknight.Core.Arena
{
    /// <summary>Ids der Erleichterungen. Der Wert einer Erleichterung steht in <see cref="ReliefDefinition.Value"/>.</summary>
    public static class ReliefIds
    {
        /// <summary>Eigene Betäubungen dauern länger (Wert in Ticks).</summary>
        public const string StunLonger = "stun_longer";

        /// <summary>Ein Gegner gilt nach einer Betäubung noch kurz als betäubt (Wert in Ticks).</summary>
        public const string StunAfterglow = "stun_afterglow";

        /// <summary>Ladung startet mit diesem Wert.</summary>
        public const string ChargeStart = "charge_start";

        /// <summary>Die Ausweicher-Serie übersteht so viele Treffer.</summary>
        public const string DodgeTolerance = "dodge_tolerance";

        /// <summary>Nach einem Block steigt die Krit-Chance kurz (Wert in Basispunkten).</summary>
        public const string CritAfterBlock = "crit_after_block";

        /// <summary>«Gegner brennt» gilt auch bei Gift.</summary>
        public const string BurnCountsPoison = "burn_counts_poison";

        /// <summary>Eigene HP-Schwellen («HP unter x %») gelten so viele Prozentpunkte früher.</summary>
        public const string HpThresholdUp = "hp_threshold_up";

        /// <summary>«Gegner unter x %» gilt so viele Prozentpunkte früher.</summary>
        public const string EnemyLowUp = "enemy_low_up";
    }

    /// <summary>Womit ein Erleichterer kommt.</summary>
    public enum ReliefKind
    {
        /// <summary>Passiv eines Ausrüstungsteils.</summary>
        Passive,

        /// <summary>Modul am Logikbaustein; wirkt, solange es eingesetzt ist.</summary>
        Module,

        /// <summary>Ein Skill, dessen Wirkung die Bedingung herbeiführt.</summary>
        Skill,
    }

    /// <summary>
    /// Ein Erleichterer als Daten: macht bestimmte Bausteine leichter erfüllbar, ohne ihre Schwierigkeits-Stufe (und damit
    /// den Bonus) zu ändern. <see cref="ReliefId"/> ist leer bei Skills: deren Wirkung selbst erleichtert.
    /// </summary>
    public sealed class ReliefDefinition
    {
        /// <summary>Teil-, Modul- oder Skill-Id, an der der Erleichterer hängt.</summary>
        public string CarrierId { get; }
        public ReliefKind Kind { get; }

        /// <summary>Erleichterung im Kampf (siehe <see cref="ReliefIds"/>), null bei Skills.</summary>
        public string ReliefId { get; }
        public int Value { get; }

        /// <summary>Anzeigetext, z. B. «Betäubungen dauern +1 s».</summary>
        public string Text { get; }

        /// <summary>Synergie-Tag (bei Teilen) bzw. Skill-Art (bei Skills) als Text, für Anzeige und Angebote.</summary>
        public string Tag { get; }

        /// <summary>Bausteine (Runen-Ids), die leichter werden.</summary>
        public IReadOnlyList<string> EasedRuneIds { get; }

        public ReliefDefinition(string carrierId, ReliefKind kind, string reliefId, int value, string text, string tag, params string[] easedRuneIds)
        {
            if (string.IsNullOrEmpty(carrierId)) throw new ArgumentException("Träger fehlt.", nameof(carrierId));
            CarrierId = carrierId;
            Kind = kind;
            ReliefId = reliefId;
            Value = value;
            Text = text ?? string.Empty;
            Tag = tag ?? string.Empty;
            EasedRuneIds = easedRuneIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Alle Erleichterer. Neue sind neue Einträge plus (bei neuer Art) eine Abfrage in der Bedingung. Teile, Module und
    /// Skills selbst stehen in ihren Katalogen; hier steht, was sie erleichtern.
    /// </summary>
    public sealed class ReliefCatalog
    {
        /// <summary>Wie lange die Krit-Chance nach einem Block erhöht ist.</summary>
        public static readonly int CritAfterBlockTicks = Ticks.FromSeconds(2);

        private readonly List<ReliefDefinition> _all = new List<ReliefDefinition>();

        public IReadOnlyList<ReliefDefinition> All => _all;

        public ReliefCatalog(IEnumerable<ReliefDefinition> reliefs)
        {
            if (reliefs != null) _all.AddRange(reliefs);
        }

        /// <summary>Erleichterer, die an diesem Teil, Modul oder Skill hängen.</summary>
        public List<ReliefDefinition> ForCarrier(string carrierId) => _all.FindAll(r => r.CarrierId == carrierId);

        /// <summary>Erleichterer, die diesen Baustein leichter machen.</summary>
        public List<ReliefDefinition> ForRune(string runeId) => _all.FindAll(r => r.EasedRuneIds.Contains(runeId));

        /// <summary>
        /// «Erleichtert: «Gegner betäubt» ◆◆ (Eigene Betäubungen dauern +1 s)», eine Zeile pro Erleichterung; leer,
        /// wenn der Träger nichts erleichtert. Runen-Namen und Stufen kommen aus <paramref name="runes"/>.
        /// </summary>
        public string EasesText(string carrierId, Runes.RuneCatalog runes)
        {
            var lines = new List<string>();
            foreach (ReliefDefinition r in ForCarrier(carrierId))
            {
                var names = new List<string>();
                foreach (string runeId in r.EasedRuneIds)
                    names.Add(runes != null && runes.TryGet(runeId, out Runes.RuneDefinition rune)
                        ? CatalogTexts.EasedRune(rune.Name, DifficultyText.Symbol(rune.Difficulty))
                        : runeId);
                lines.Add(CatalogTexts.Eases(string.Join(", ", names), r.Text));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Summiert die Erleichterungen der Träger (Teile, Module) zu Id → Wert.</summary>
        public Dictionary<string, int> Collect(IEnumerable<string> carrierIds)
        {
            var result = new Dictionary<string, int>();
            if (carrierIds == null) return result;
            foreach (string id in carrierIds)
            {
                foreach (ReliefDefinition r in ForCarrier(id))
                {
                    if (r.ReliefId == null) continue;
                    result[r.ReliefId] = (result.TryGetValue(r.ReliefId, out int v) ? v : 0) + r.Value;
                }
            }
            return result;
        }

        private static readonly string[] HpRunes = { "hp_low", Runes.EvolvedRuneIds.PhantomReflex };

        public static ReliefCatalog CreateDefault() => new ReliefCatalog(new[]
        {
            // Passive an Ausrüstungsteilen (Ids aus dem EquipmentCatalog).
            new ReliefDefinition(ReliefCarrierIds.NumbingGloves, ReliefKind.Passive, ReliefIds.StunLonger, Ticks.PerSecond,
                "Own stuns last +1 s", "Static", "enemy_stunned"),
            new ReliefDefinition(ReliefCarrierIds.AfterimageVisor, ReliefKind.Passive, ReliefIds.StunAfterglow, Ticks.FromTenths(5),
                "Enemy still counts as stunned for 0.5 s after a stun", "Phantom", "enemy_stunned"),
            new ReliefDefinition(ReliefCarrierIds.PrechargedCell, ReliefKind.Passive, ReliefIds.ChargeStart, 3,
                "Static starts at 3", "Static", "charge_full"),
            new ReliefDefinition(ReliefCarrierIds.PhantomStep, ReliefKind.Passive, ReliefIds.DodgeTolerance, 1,
                "Dodge streak only breaks on the 2nd hit", "Phantom", "dodge_streak"),
            new ReliefDefinition(ReliefCarrierIds.CounterShield, ReliefKind.Passive, ReliefIds.CritAfterBlock, BasisPoints.Percent(15),
                "Crit chance +15 % for 2 s after a Block", "Static", "on_crit"),
            new ReliefDefinition(ReliefCarrierIds.VenomTorch, ReliefKind.Passive, ReliefIds.BurnCountsPoison, 1,
                "\"Enemy Burning\" also counts Poison", "Toxin", "enemy_burning"),
            new ReliefDefinition(ReliefCarrierIds.PainConductor, ReliefKind.Passive, ReliefIds.HpThresholdUp, 5,
                "HP threshold runes trigger 5 percentage points earlier", "Scrap", HpRunes),

            // Module am Logikbaustein (Ids aus dem ModuleCatalog).
            new ReliefDefinition(ReliefCarrierIds.AlarmSensor, ReliefKind.Module, ReliefIds.HpThresholdUp, 10,
                "HP threshold runes trigger 10 percentage points earlier", "Module", HpRunes),
            new ReliefDefinition(ReliefCarrierIds.ScentModule, ReliefKind.Module, ReliefIds.EnemyLowUp, 10,
                "\"Enemy Below x %\" triggers 10 percentage points earlier", "Module", "enemy_low"),

            // Skills, deren Wirkung die Bedingung herbeiführt (Ids aus dem SkillCatalog).
            new ReliefDefinition(SkillIds.NumbingMist, ReliefKind.Skill, null, 0, "briefly stuns all enemies", CatalogTexts.KindShock, "enemy_stunned"),
        });

        /// <summary>Gemeinsamer Standard-Katalog (unveränderlich nach dem Erstellen).</summary>
        public static ReliefCatalog Default { get; } = CreateDefault();
    }

    /// <summary>Ids der Teile und Module, die Erleichterer tragen.</summary>
    public static class ReliefCarrierIds
    {
        public const string NumbingGloves = "numbing_gloves";
        public const string AfterimageVisor = "afterimage_visor";
        public const string PrechargedCell = "precharged_cell";
        public const string PhantomStep = "phantom_step";
        public const string CounterShield = "counter_shield";
        public const string VenomTorch = "venom_torch";
        public const string PainConductor = "pain_conductor";
        public const string AlarmSensor = "alarm_sensor";
        public const string ScentModule = "scent";
    }

    /// <summary>Erleichterung «Krit-Chance nach Block»: legt nach jedem Block kurz einen Krit-Bonus auf den Träger.</summary>
    public sealed class CritAfterBlockModifier : BattleModifier
    {
        public const string StatusId = "counter_crit";

        public int CritBp { get; }
        public CritAfterBlockModifier(int critBp) => CritBp = critBp;

        public override string Name => CatalogTexts.CounterModifierName;

        public override void OnEvent(Battle battle, Combatant owner, BattleEvent e)
        {
            if (e.Kind == BattleEventKind.Blocked && e.Target == owner && owner.IsAlive)
                battle.ApplyStatus(owner, new StatModifierStatus(StatusId, StatKind.Crit, CritBp, ReliefCatalog.CritAfterBlockTicks), owner);
        }
    }
}
