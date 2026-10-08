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

        public static SkillCatalog CreateDefault()
        {
            var c = new SkillCatalog();
            c.Register(SkillDefinition.BasicAttack);

            c.Register(new SkillDefinition(SkillIds.ArmorBreak, "Rüstungsbruch", 8, 4, Ticks.FromSeconds(8), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Full),
                new StatModifierEffect(StatusIds.ArmorBreak, StatKind.ArmorMultiplier, -BasisPoints.Percent(50), Ticks.FromSeconds(6), onTarget: true),
            }, "Schaden, Gegner-Rüstung −50 % für 6 s.", countsAsAttack: true, kinds: SkillKind.Attack));

            c.Register(new SkillDefinition(SkillIds.Ignite, "Entzünden", 6, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new BurnEffect(Ticks.FromSeconds(5), BasisPoints.Percent(50)),
            }, "Gegner brennt 5 s (50 % Waffenschaden pro Sekunde).", kinds: SkillKind.Fire));

            c.Register(new SkillDefinition(SkillIds.ShieldBash, "Schildschlag", 6, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(80)),
                new InterruptChargeEffect(),
                new StunEffect(Ticks.FromTenths(15)),
            }, "Schaden, betäubt 1,5 s, bricht Aufladung ab.", countsAsAttack: true, kinds: SkillKind.Shield));

            c.Register(new SkillDefinition(SkillIds.EmpBash, "EMP-Schildschlag", 8, 4, Ticks.FromSeconds(10), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(80), allEnemies: true),
                new StunEffect(Ticks.FromSeconds(3), allEnemies: true),
            }, "Schaden an allen Gegnern, betäubt alle 3 s.", countsAsAttack: true, kinds: SkillKind.Shield | SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Repair, "Not-Reparatur", 10, 4, Ticks.FromSeconds(15), new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(25)),
            }, "Heilt 25 % Max-HP.", kinds: SkillKind.Healing));

            c.Register(new SkillDefinition(SkillIds.Coolant, "Kühlmittel-Injektion", 6, 4, Ticks.FromSeconds(6), new ISkillEffect[]
            {
                new HealEffect(BasisPoints.Percent(15)),
                new SetResourceEffect(ResourceIds.Heat, 0),
            }, "Heilt 15 % Max-HP, entfernt Hitze.", kinds: SkillKind.Healing | SkillKind.Fire));

            c.Register(new SkillDefinition(SkillIds.ShieldWall, "Notfall-Schildwall", 4, 4, Ticks.FromSeconds(12), new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.ShieldWall, StatKind.Block, BasisPoints.Full, Ticks.FromSeconds(4)),
            }, "+100 % Block für 4 s (Obergrenze 75 %).", kinds: SkillKind.Shield));

            c.Register(new SkillDefinition(SkillIds.Flashbang, "Blendgranate", 6, 4, Ticks.FromSeconds(8), new ISkillEffect[]
            {
                new StatModifierEffect(StatusIds.Blinded, StatKind.Accuracy, -BasisPoints.Percent(40), Ticks.FromSeconds(4), onTarget: true),
            }, "Gegner −40 % Präzision für 4 s.", kinds: SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Drill, "Bohrstoß", 10, 6, Ticks.FromSeconds(5), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(120), allEnemies: true),
            }, "120 % Waffenschaden an allen Gegnern.", countsAsAttack: true, kinds: SkillKind.Attack));

            c.Register(new SkillDefinition(SkillIds.Anchor, "Bodenanker", 4, 4, Ticks.FromSeconds(10), new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new AnchorStatus(Ticks.FromSeconds(3))),
            }, "3 s: Rüstung ×2, kein Ausweichen.", kinds: SkillKind.Shield | SkillKind.Movement));

            c.Register(new SkillDefinition(SkillIds.Thrusters, "Schubdüsen", 2, 2, Ticks.FromSeconds(7), new ISkillEffect[]
            {
                new ApplyStatusEffect(() => new ThrustersStatus(Ticks.FromSeconds(7))),
            }, "Der nächste gegnerische Treffer verfehlt sicher.", kinds: SkillKind.Movement));

            c.Register(new SkillDefinition(SkillIds.ShockStab, "Schockstich", 2, 2, Ticks.FromSeconds(3), new ISkillEffect[]
            {
                new DamageEffect(BasisPoints.Percent(60)),
                new ChanceEffect(BasisPoints.Percent(20), new StunEffect(Ticks.FromSeconds(1))),
            }, "Schneller Treffer, 20 % Chance auf 1 s Betäubung.", countsAsAttack: true, kinds: SkillKind.Attack | SkillKind.Shock));

            c.Register(new SkillDefinition(SkillIds.Echo, "Echo-Protokoll", 6, 4, Ticks.FromSeconds(12), new ISkillEffect[]
            {
                new RepeatLastSkillEffect(),
            }, "Wiederholt den zuletzt ausgeführten eigenen Skill.", canBeRepeated: false, kinds: SkillKind.Attack));

            return c;
        }
    }
}
