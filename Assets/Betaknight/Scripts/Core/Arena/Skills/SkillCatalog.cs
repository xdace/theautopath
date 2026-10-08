using System;
using System.Collections.Generic;
using Betaknight.Core.Circuit;

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
        public const string CryoGrenade = "cryo_grenade";
        public const string RailCannon = "rail_cannon";

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

        // Formen (A-19): mehr Zellen = mehr Wirkung pro Ausführung, aber nur schwere Relais versorgen grosse Komponenten.
        private static readonly Shape S1x1 = new Shape(1, 1);
        private static readonly Shape S1x2 = new Shape(1, 2);
        private static readonly Shape S2x1 = new Shape(2, 1);
        private static readonly Shape S2x2 = new Shape(2, 2);
        private static readonly Shape S2x3 = new Shape(2, 3);

        /// <summary>
        /// Skills als Komponenten (A-19): keine Cooldowns, die Form bestimmt die Wirkung (siehe <see cref="SkillBudgetConfig"/>:
        /// 1 Zelle ≈ 60 %, 2 ≈ 150 %, 4 ≈ 350 %, 6 ≈ 600 % Waffenschaden). Nutzen-Skills tauschen Schaden gegen Wirkung.
        /// Evolutionen behalten die Form ihres Grund-Skills, damit sie auf der Platine an ihrem Platz bleiben.
        /// </summary>
        /// <param name="effects">Eigene Effekte der Platine (A-21): die in Form «Skill» kommen als Skills dazu.</param>
        public static SkillCatalog CreateDefault(CircuitEffectCatalog effects = null)
        {
            var c = new SkillCatalog();
            // Der Basisangriff des Ritters: füllt die Lücken der Warteschlange (60 %), siehe SkillBudgetConfig.
            c.Register(SkillBudgetConfig.Default.CreateKnightBasicAttack());

            c.Register(new SkillDefinition(SkillIds.ShockStab, "Shock Stab", CastTime.Fast, 2, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(60)),
                new ChanceEffect(BasisPoints.Percent(20), new StunEffect(Ticks.FromSeconds(1))),
            }, "Quick hit (60 %), 20 % chance to stun for 1 s.", countsAsAttack: true, kinds: SkillKind.Attack | SkillKind.Shock, shape: S1x1));

            c.Register(new SkillDefinition(SkillIds.Thrusters, "Thrusters", CastTime.Fast, 2, new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new ThrustersStatus(Ticks.FromSeconds(7))),
            }, "The next enemy hit is sure to miss.", kinds: SkillKind.Movement, shape: S1x1));

            // Erleichterer: Ladung für «Charge Full», Betäubung und Verlangsamung für «Enemy Stunned».
            c.Register(new SkillDefinition(SkillIds.ChargeCoil, "Charge Coil", CastTime.Fast, 2, new ISkillEffect[]
            {
                new ChangeResourceEffect(ResourceIds.Charge, 3, ChargeMax),
            }, "+3 Charge (max. 5). Eases \"Charge Full\".", kinds: SkillKind.Shield, shape: S1x1));

            c.Register(new SkillDefinition(SkillIds.Ignite, "Ignite", CastTime.Fast, 4, new ISkillEffect[]
            {
                new BurnEffect(Ticks.FromSeconds(5), BasisPoints.Percent(32)),
            }, "Enemy burns for 5 s (32 % Weapon Damage per second).", kinds: SkillKind.Fire, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.ShieldBash, "Shield Bash", CastTime.Medium, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(70)),
                new InterruptChargeEffect(),
                new StunEffect(Ticks.FromSeconds(2)),
            }, "Low Damage, stuns for 2 s, interrupts charging.", countsAsAttack: true, kinds: SkillKind.Shield, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.Coolant, "Coolant Injection", CastTime.Medium, 4, new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(12)),
                new SetResourceEffect(ResourceIds.Heat, 0),
            }, "Heals 12 % Max HP, removes Heat.", kinds: SkillKind.Healing | SkillKind.Fire, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.ShieldWall, "Emergency Shield Wall", CastTime.Fast, 4, new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.ShieldWall, StatKind.Block, BasisPoints.Full, Ticks.FromSeconds(4)),
            }, "+100 % Block for 4 s (cap 75 %).", kinds: SkillKind.Shield, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.Flashbang, "Flashbang", CastTime.Medium, 4, new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.Blinded, StatKind.Accuracy, -BasisPoints.Percent(40), Ticks.FromSeconds(4), onTarget: true),
            }, "Enemy −40 % Accuracy for 4 s.", kinds: SkillKind.Shock, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.Anchor, "Ground Anchor", CastTime.Fast, 4, new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new AnchorStatus(Ticks.FromSeconds(3))),
            }, "3 s: Armor ×2, no Dodge.", kinds: SkillKind.Shield | SkillKind.Movement, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.NumbingMist, "Numbing Mist", CastTime.Medium, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(20), allEnemies: true),
                new StunEffect(Ticks.FromTenths(6), allEnemies: true),
                new AllEnemiesEffect(new StatModifierEffect(StatusIds.Slow, StatKind.CastPercent, 30, Ticks.FromSeconds(3), onTarget: true)),
            }, "Low Damage, stuns all enemies for 0.6 s and slows them (+30 % Cast Time, 3 s). Eases \"Enemy Stunned\".",
                kinds: SkillKind.Shock, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.CryoGrenade, "Cryo Grenade", CastTime.Medium, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(60)),
                new FreezeEffect(Ticks.FromSeconds(3)),
            }, "60 % Damage, freezes the enemy's largest component for 3 s.", countsAsAttack: true, kinds: SkillKind.Shock, shape: S2x1));

            c.Register(new SkillDefinition(SkillIds.Echo, "Echo Protocol", CastTime.Medium, 4, new ISkillEffect[]
            {
                new RepeatLastSkillEffect(),
            }, "Repeats your most recently used skill.", canBeRepeated: false, kinds: SkillKind.Attack, shape: S2x1));

            c.Register(new SkillDefinition(SkillIds.ArmorBreak, "Armor Break", CastTime.Medium, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(300)),
                new StatModifierEffect(StatusIds.ArmorBreak, StatKind.ArmorMultiplier, -BasisPoints.Percent(50), Ticks.FromSeconds(6), onTarget: true),
            }, "300 % Damage, enemy Armor −50 % for 6 s.", countsAsAttack: true, kinds: SkillKind.Attack, shape: S2x2));

            c.Register(new SkillDefinition(SkillIds.Drill, "Drill Strike", CastTime.Heavy, 6, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(210), allEnemies: true),
            }, "210 % Weapon Damage to all enemies.", countsAsAttack: true, kinds: SkillKind.Attack, shape: S2x2));

            c.Register(new SkillDefinition(SkillIds.EmpBash, "EMP Shield Bash", CastTime.Heavy, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(110), allEnemies: true),
                new StunEffect(Ticks.FromSeconds(3), allEnemies: true),
            }, "Damage to all enemies, stuns all for 3 s.", countsAsAttack: true, kinds: SkillKind.Shield | SkillKind.Shock, shape: S2x2));

            c.Register(new SkillDefinition(SkillIds.Repair, "Emergency Repair", CastTime.Heavy, 4, new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(25)),
            }, "Heals 25 % Max HP.", kinds: SkillKind.Healing, shape: S2x2));

            c.Register(new SkillDefinition(SkillIds.RailCannon, "Rail Cannon", CastTime.Heavy, 6, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(600)),
            }, "600 % Weapon Damage. Needs a very hard relay.", countsAsAttack: true, kinds: SkillKind.Attack, shape: S2x3));

            RegisterCircuitEffects(c, effects);
            RegisterEvolutions(c);
            return c;
        }

        /// <summary>
        /// Eigene Effekte der Platine in Form «Skill» (A-21, meist Hacks): ein Skill mit Form und Cast-Zeit aus den Daten,
        /// dessen Ausführung den Effekt auslöst.
        /// </summary>
        private static void RegisterCircuitEffects(SkillCatalog c, CircuitEffectCatalog effects)
        {
            foreach (CircuitEffectDefinition e in (effects ?? CircuitEffectCatalog.Shared).InForm(CircuitEffectForm.Skill))
                c.Register(new SkillDefinition(e.Id, e.Name, e.SkillCastTicks, 4, Array.Empty<ISkillEffect>(), e.Description,
                    kinds: SkillKind.Shock, shape: e.SkillShape).WithCircuitEffect(e.Id));
        }

        /// <summary>Evolutionsformen: stärkere Fassungen bestehender Skills, entstehen nur über ein Rezept. Gleiche Form wie der Grund-Skill.</summary>
        private static void RegisterEvolutions(SkillCatalog c)
        {
            c.Register(new SkillDefinition(SkillIds.Inferno, "Firestorm", CastTime.Fast, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(30), allEnemies: true),
                new AllEnemiesEffect(new BurnEffect(Ticks.FromSeconds(5), BasisPoints.Percent(20))),
            }, "Evolution of Ignite: Damage to all enemies, all burn for 5 s.", kinds: SkillKind.Fire, isEvolution: true, shape: S1x2));

            c.Register(new SkillDefinition(SkillIds.LightningLance, "Lightning Lance", CastTime.Fast, 2, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(80)),
                new ChanceEffect(BasisPoints.Percent(50), new StunEffect(Ticks.FromTenths(15))),
            }, "Evolution of Shock Stab: strong stab (80 %), 50 % chance to stun for 1.5 s.", countsAsAttack: true,
                kinds: SkillKind.Attack | SkillKind.Shock, isEvolution: true, shape: S1x1));

            c.Register(new SkillDefinition(SkillIds.Resonance, "Resonance", CastTime.Medium, 4, new ISkillEffect[]
            {
                new RepeatLastSkillEffect(),
                new RepeatLastSkillEffect(),
            }, "Evolution of Echo: repeats your last skill twice.", canBeRepeated: false, kinds: SkillKind.Attack, isEvolution: true, shape: S2x1));

            c.Register(new SkillDefinition(SkillIds.AcidDrill, "Acid Drill", CastTime.Heavy, 6, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(230), allEnemies: true),
                new AllEnemiesEffect(new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 2), onTarget: true)),
                new AllEnemiesEffect(new ApplyStatusEffect(() => new PoisonStatus(Ticks.FromSeconds(6), 2), onTarget: true)),
            }, "Evolution of Drill Strike: 230 % to all enemies, 2 Poison stacks each.", countsAsAttack: true, kinds: SkillKind.Attack,
                isEvolution: true, shape: S2x2));

            c.Register(new SkillDefinition(SkillIds.ScrapRam, "Scrap Ram", CastTime.Medium, 4, new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(90), ignoreArmor: true),
                new InterruptChargeEffect(),
                new StunEffect(Ticks.FromTenths(25)),
            }, "Evolution of Shield Bash: Damage ignores Armor, stuns for 2.5 s, interrupts charging.", countsAsAttack: true,
                kinds: SkillKind.Shield, isEvolution: true, shape: S1x2));
        }
    }
}
