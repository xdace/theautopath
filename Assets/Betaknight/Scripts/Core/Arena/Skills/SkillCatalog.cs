using System;
using System.Collections.Generic;

namespace Betaknight.Core.Arena
{
    /// <summary>Bekannte Skill-Ids, damit Ausrüstung, Tests und Gegner dieselben Schlüssel nutzen.</summary>
    public static class SkillIds
    {
        public const string BasicAttack = SkillDefinition.BasicAttackId;
        public const string ArmorBreak = "armor_break";
        public const string Ignite = "ignite";
        public const string ShieldBash = "shield_bash";
        public const string EmpBash = "emp_bash";
        public const string Repair = "repair";
        public const string Coolant = "coolant";
        public const string ShieldWall = "shield_wall";
        public const string Flashbang = "flashbang";
        public const string Drill = "drill";
        public const string Anchor = "anchor";
        public const string Thrusters = "thrusters";
        public const string ShockStab = "shock_stab";
        public const string Echo = "echo";

        // Erleichterer (A-11): ihre Wirkung führt schwere Bedingungen herbei.
        public const string ChargeCoil = "charge_coil";
        public const string NumbingMist = "numbing_mist";

        // Evolutionsformen (nie angeboten, siehe EvolutionCatalog).
        public const string Inferno = "inferno";
        public const string LightningLance = "lightning_lance";
        public const string Resonance = "resonance";
        public const string AcidDrill = "acid_drill";
        public const string ScrapRam = "scrap_ram";
    }

    /// <summary>
    /// Alle Skills nach Id. Ein neuer Skill ist ein neuer Eintrag aus vorhandenen oder neuen
    /// <see cref="ISkillEffect"/>-Bausteinen; der Simulator bleibt unverändert.
    /// </summary>
    public sealed class SkillCatalog
    {
        private readonly Dictionary<string, SkillDefinition> _byId = new Dictionary<string, SkillDefinition>();
        private readonly List<SkillDefinition> _all = new List<SkillDefinition>();

        public IReadOnlyList<SkillDefinition> All => _all;

        public void Register(SkillDefinition skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (_byId.ContainsKey(skill.Id)) _all.Remove(_byId[skill.Id]);
            _byId[skill.Id] = skill;
            _all.Add(skill);
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        public bool TryGet(string id, out SkillDefinition skill)
        {
            skill = null;
            return id != null && _byId.TryGetValue(id, out skill);
        }

        public SkillDefinition Get(string id)
        {
            if (!TryGet(id, out SkillDefinition skill)) throw new KeyNotFoundException($"Unbekannter Skill {id}.");
            return skill;
        }

        /// <summary>Obergrenze der Ladung (wie beim Aegis-Set).</summary>
        public const int ChargeMax = 5;

        public static SkillCatalog CreateDefault()
        {
            var c = new SkillCatalog();
            c.Register(SkillDefinition.BasicAttack);

            c.Register(new SkillDefinition(SkillIds.ArmorBreak, "Rüstungsbruch", CastTime.Medium, 4, Ticks.FromSeconds(8), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Full),
                new StatModifierEffect(StatusIds.ArmorBreak, StatKind.ArmorMultiplier, -BasisPoints.Percent(50), Ticks.FromSeconds(6), onTarget: true),
            }, "Schaden, Gegner-Rüstung −50 % für 6 s.", countsAsAttack: true, kinds: SkillKind.Attack));

            c.Register(new SkillDefinition(SkillIds.Ignite, "Entzünden", CastTime.Fast, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new BurnEffect(Ticks.FromSeconds(5), BasisPoints.Percent(50)),
            }, "Gegner brennt 5 s (50 % Waffenschaden pro Sekunde).", kinds: SkillKind.Fire));

            c.Register(new SkillDefinition(SkillIds.ShieldBash, "Schildschlag", CastTime.Medium, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(80)),
                new InterruptChargeEffect(),
                new StunEffect(Ticks.FromTenths(15)),
            }, "Schaden, betäubt 1,5 s, bricht Aufladung ab.", countsAsAttack: true, kinds: SkillKind.Shield));

            c.Register(new SkillDefinition(SkillIds.EmpBash, "EMP-Schildschlag", CastTime.Heavy, 4, Ticks.FromSeconds(10), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(80), allEnemies: true),
                new StunEffect(Ticks.FromSeconds(3), allEnemies: true),
            }, "Schaden an allen Gegnern, betäubt alle 3 s.", countsAsAttack: true, kinds: SkillKind.Shield | SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Repair, "Not-Reparatur", CastTime.Heavy, 4, Ticks.FromSeconds(15), new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(25)),
            }, "Heilt 25 % Max-HP.", kinds: SkillKind.Healing));

            c.Register(new SkillDefinition(SkillIds.Coolant, "Kühlmittel-Injektion", CastTime.Medium, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(15)),
                new SetResourceEffect(ResourceIds.Heat, 0),
            }, "Heilt 15 % Max-HP, entfernt Hitze.", kinds: SkillKind.Healing | SkillKind.Fire));

            c.Register(new SkillDefinition(SkillIds.ShieldWall, "Notfall-Schildwall", CastTime.Fast, 4, Ticks.FromSeconds(12), new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.ShieldWall, StatKind.Block, BasisPoints.Full, Ticks.FromSeconds(4)),
            }, "+100 % Block für 4 s (Obergrenze 75 %).", kinds: SkillKind.Shield));

            c.Register(new SkillDefinition(SkillIds.Flashbang, "Blendgranate", CastTime.Medium, 4, Ticks.FromSeconds(8), new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.Blinded, StatKind.Accuracy, -BasisPoints.Percent(40), Ticks.FromSeconds(4), onTarget: true),
            }, "Gegner −40 % Präzision für 4 s.", kinds: SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Drill, "Bohrstoß", CastTime.Heavy, 6, Ticks.FromSeconds(5), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(120), allEnemies: true),
            }, "120 % Waffenschaden an allen Gegnern.", countsAsAttack: true, kinds: SkillKind.Attack));

            c.Register(new SkillDefinition(SkillIds.Anchor, "Bodenanker", CastTime.Fast, 4, Ticks.FromSeconds(10), new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new AnchorStatus(Ticks.FromSeconds(3))),
            }, "3 s: Rüstung ×2, kein Ausweichen.", kinds: SkillKind.Shield | SkillKind.Movement));

            c.Register(new SkillDefinition(SkillIds.Thrusters, "Schubdüsen", CastTime.Fast, 2, Ticks.FromSeconds(7), new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new ThrustersStatus(Ticks.FromSeconds(7))),
            }, "Der nächste gegnerische Treffer verfehlt sicher.", kinds: SkillKind.Movement));

            c.Register(new SkillDefinition(SkillIds.ShockStab, "Schockstich", CastTime.Fast, 2, Ticks.FromSeconds(3), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(60)),
                new ChanceEffect(BasisPoints.Percent(20), new StunEffect(Ticks.FromSeconds(1))),
            }, "Schneller Treffer, 20 % Chance auf 1 s Betäubung.", countsAsAttack: true, kinds: SkillKind.Attack | SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Echo, "Echo-Protokoll", CastTime.Medium, 4, Ticks.FromSeconds(12), new ISkillEffect[]
            {
                new RepeatLastSkillEffect(),
            }, "Wiederholt den zuletzt ausgeführten eigenen Skill.", canBeRepeated: false, kinds: SkillKind.Attack));

            // Erleichterer: Ladung für «Ladung voll», kurze Massen-Betäubung für «Gegner betäubt».
            c.Register(new SkillDefinition(SkillIds.ChargeCoil, "Ladungsspule", CastTime.Fast, 2, Ticks.FromSeconds(8), new ISkillEffect[]
            {
                new ChangeResourceEffect(ResourceIds.Charge, 3, ChargeMax),
            }, "+3 Ladung (höchstens 5). Erleichtert «Ladung voll».", kinds: SkillKind.Shield));

            c.Register(new SkillDefinition(SkillIds.NumbingMist, "Lähmnebel", CastTime.Medium, 4, Ticks.FromSeconds(9), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(20), allEnemies: true),
                new StunEffect(Ticks.FromTenths(6), allEnemies: true),
            }, "Wenig Schaden, betäubt alle Gegner 0,6 s. Erleichtert «Gegner betäubt».", kinds: SkillKind.Shock));

            RegisterEvolutions(c);
            return c;
        }

        /// <summary>Evolutionsformen: stärkere Fassungen bestehender Skills, entstehen nur über ein Rezept.</summary>
        private static void RegisterEvolutions(SkillCatalog c)
        {
            c.Register(new SkillDefinition(SkillIds.Inferno, "Feuersturm", CastTime.Fast, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(60), allEnemies: true),
                new AllEnemiesEffect(new BurnEffect(Ticks.FromSeconds(5), BasisPoints.Percent(70))),
            }, "Evolution von Entzünden: Schaden an allen Gegnern, alle brennen 5 s.", kinds: SkillKind.Fire, isEvolution: true));

            c.Register(new SkillDefinition(SkillIds.LightningLance, "Blitzlanze", CastTime.Fast, 2, Ticks.FromSeconds(3), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Full),
                new ChanceEffect(BasisPoints.Percent(50), new StunEffect(Ticks.FromTenths(15))),
            }, "Evolution von Schockstich: starker Stich, 50 % Chance auf 1,5 s Betäubung.", countsAsAttack: true,
                kinds: SkillKind.Attack | SkillKind.Shock, isEvolution: true));

            c.Register(new SkillDefinition(SkillIds.Resonance, "Resonanz", CastTime.Medium, 4, Ticks.FromSeconds(10), new ISkillEffect[]
            {
                new RepeatLastSkillEffect(),
                new RepeatLastSkillEffect(),
            }, "Evolution von Echo: wiederholt den letzten eigenen Skill zweimal.", canBeRepeated: false, kinds: SkillKind.Attack, isEvolution: true));

            c.Register(new SkillDefinition(SkillIds.AcidDrill, "Säurebohrer", CastTime.Heavy, 6, Ticks.FromSeconds(5), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(120), allEnemies: true),
                new AllEnemiesEffect(new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 2), onTarget: true)),
                new AllEnemiesEffect(new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 2), onTarget: true)),
            }, "Evolution von Bohrstoß: Schaden an allen Gegnern, je 2 Gift-Stapel.", countsAsAttack: true, kinds: SkillKind.Attack, isEvolution: true));

            c.Register(new SkillDefinition(SkillIds.ScrapRam, "Schrottramme", CastTime.Medium, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(120), ignoreArmor: true),
                new InterruptChargeEffect(),
                new StunEffect(Ticks.FromSeconds(2)),
            }, "Evolution von Schildschlag: Schaden ohne Rüstung, betäubt 2 s, bricht Aufladung ab.", countsAsAttack: true,
                kinds: SkillKind.Shield, isEvolution: true));
        }
    }
}
