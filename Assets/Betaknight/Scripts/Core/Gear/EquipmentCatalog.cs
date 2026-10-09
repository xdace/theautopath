using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Gear
{
    /// <summary>Alle Ausrüstungsteile nach Id. Neue Teile kommen als Einträge in <see cref="CreateDefault"/> dazu.</summary>
    public sealed class EquipmentCatalog
    {
        private readonly Dictionary<string, EquipmentDefinition> _byId = new Dictionary<string, EquipmentDefinition>();
        private readonly List<EquipmentDefinition> _all = new List<EquipmentDefinition>();

        public IReadOnlyList<EquipmentDefinition> All => _all;

        public EquipmentCatalog(IEnumerable<EquipmentDefinition> items)
        {
            foreach (EquipmentDefinition item in items ?? Array.Empty<EquipmentDefinition>())
            {
                if (item == null) continue;
                if (_byId.ContainsKey(item.Id)) throw new ArgumentException($"Doppelte Ausrüstungs-Id {item.Id}.");
                _byId.Add(item.Id, item);
                _all.Add(item);
            }
        }

        public bool TryGet(string id, out EquipmentDefinition item)
        {
            item = null;
            return id != null && _byId.TryGetValue(id, out item);
        }

        public EquipmentDefinition Get(string id)
        {
            if (!TryGet(id, out EquipmentDefinition item)) throw new KeyNotFoundException($"Unbekanntes Teil {id}.");
            return item;
        }

        /// <summary>Alle Teile eines Sets.</summary>
        public IReadOnlyList<EquipmentDefinition> SetItems(string setId) => _all.FindAll(i => i.SetId == setId);

        private static Dictionary<StatKind, int> S(params (StatKind kind, int value)[] stats)
        {
            var d = new Dictionary<StatKind, int>();
            foreach ((StatKind kind, int value) in stats) d[kind] = value;
            return d;
        }

        private static SkillPassive[] P(params SkillPassive[] passives) => passives;

        private static string[] T(params string[] tags) => tags;

        private static SkillPassive Power(SkillKind tag, int percent) => SkillPassive.Power(tag, percent);

        /// <summary>Früher «−1 s Cooldown» je Stufe, seit A-19 (keine Cooldowns) −15 % Cast-Zeit je Stufe.</summary>
        private static SkillPassive Faster(SkillKind tag, int steps) => SkillPassive.Cast(tag, -15 * steps);

        public static EquipmentCatalog CreateDefault() => new EquipmentCatalog(new[]
        {
            // Startausrüstung der Kits (nie angeboten).
            new EquipmentDefinition("short_blade", "Short Blade", EquipmentSlot.Weapon, S((StatKind.Damage, 2), (StatKind.AttackInterval, -2)),
                null, weight: 0, description: "Light blade of the Blade Knight.", tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("short_sword", "Short Sword", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                weight: 0, description: "A solid sword, no tricks.", tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("round_shield", "Round Shield", EquipmentSlot.Shield, S((StatKind.Armor, 2), (StatKind.Block, BasisPoints.Percent(10))),
                null, weight: 0, description: "Any shield can strike.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("spark_staff", "Spark Staff", EquipmentSlot.Weapon, S((StatKind.Damage, 1), (StatKind.AttackInterval, -2)),
                null, weight: 0, description: "Sets enemies on fire.", tags: T(SynergyTagIds.Heat)),

            // Einzelteile.
            new EquipmentDefinition("titan_hammer", "Titan Hammer", EquipmentSlot.Weapon, S((StatKind.Damage, 3), (StatKind.AttackInterval, 4)),
                P(Power(SkillKind.Attack, 25)), description: "Slow; Attack Skills hit harder.", tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("flamethrower", "Flamethrower", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                P(Power(SkillKind.Fire, 25)), tags: T(SynergyTagIds.Heat, SynergyTagIds.Toxin)),
            new EquipmentDefinition("tower_shield", "Tower Shield", EquipmentSlot.Shield, S((StatKind.Armor, 4), (StatKind.Block, BasisPoints.Percent(15))),
                P(Faster(SkillKind.Shield, 1)), tags: T(SynergyTagIds.Charge, SynergyTagIds.Scrap)),
            new EquipmentDefinition("echo_helm", "Echo Helm", EquipmentSlot.Helmet, null, P(Faster(SkillKind.Attack, 1)), weight: 2,
                description: "Rare. Attack Skills recharge faster.", tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("incendiary_gloves", "Incendiary Gloves", EquipmentSlot.Gloves, null, P(Power(SkillKind.Fire, 15), Faster(SkillKind.Fire, 1)), weight: 6,
                description: "For Fire builds.", tags: T(SynergyTagIds.Heat)),
            new EquipmentDefinition("leather_gloves", "Leather Gloves", EquipmentSlot.Gloves, S((StatKind.Crit, BasisPoints.Percent(10))), tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("iron_greaves", "Iron Greaves", EquipmentSlot.Legs, S((StatKind.Armor, 2)), tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("padded_vest", "Padded Vest", EquipmentSlot.Chest, S((StatKind.MaxHp, 5)), tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("light_boots", "Light Boots", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5))), tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),

            // Überlast-Protokoll.
            new EquipmentDefinition("overload_chassis", "Faulty Heatsink Chassis", EquipmentSlot.Chest, S((StatKind.MaxHp, 6)),
                P(Faster(SkillKind.Healing, 1)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat, SynergyTagIds.Tempo)),
            new EquipmentDefinition("thermo_blade", "Red-Hot Thermo Blade", EquipmentSlot.Weapon, S((StatKind.Damage, 2)),
                P(Power(SkillKind.Fire, 20)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat)),
            new EquipmentDefinition("warning_visor", "Warning Light Visor", EquipmentSlot.Helmet, null,
                P(Power(SkillKind.Healing, 20)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat, SynergyTagIds.Charge)),

            // Aegis-Firewall.
            new EquipmentDefinition("holo_barrier", "Holographic Barrier", EquipmentSlot.Shield, S((StatKind.Armor, 3), (StatKind.Block, BasisPoints.Percent(15))),
                P(Power(SkillKind.Shock, 20)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge, SynergyTagIds.Phantom)),
            new EquipmentDefinition("shock_absorber", "Shock Absorber", EquipmentSlot.Gloves, S((StatKind.Block, BasisPoints.Percent(5))),
                P(Faster(SkillKind.Shield, 2)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("mag_anchors", "Magnetic Ground Anchors", EquipmentSlot.Boots, S((StatKind.Armor, 2)),
                P(Faster(SkillKind.Movement, 1)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge, SynergyTagIds.Scrap)),

            // Schrott-Ernter.
            new EquipmentDefinition("plasma_drill", "Two-Handed Plasma Drill", EquipmentSlot.Weapon, S((StatKind.Damage, 4), (StatKind.AttackInterval, 6)),
                P(Power(SkillKind.Attack, 25)), SetIds.Scrap, twoHanded: true, weight: 4, tags: T(SynergyTagIds.Scrap, SynergyTagIds.Heat)),
            new EquipmentDefinition("crawler_tracks", "Heavy Crawler Tracks", EquipmentSlot.Legs, S((StatKind.Armor, 4), (StatKind.Dodge, -BasisPoints.Percent(10))),
                null, SetIds.Scrap, weight: 4, tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("resource_compactor", "Resource Compactor", EquipmentSlot.Chest, S((StatKind.MaxHp, 4)),
                null, SetIds.Scrap, weight: 4, tags: T(SynergyTagIds.Scrap, SynergyTagIds.Toxin)),

            // Phantom-Signal.
            new EquipmentDefinition("gyro_thrusters", "Gyroscopic Thrusters", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5))),
                P(Faster(SkillKind.Movement, 1)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),
            new EquipmentDefinition("holo_projector", "Holo Projector", EquipmentSlot.Helmet, null,
                P(Faster(SkillKind.Shock, 1)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition("shock_dagger", "Light Shock Dagger", EquipmentSlot.Weapon, S((StatKind.AttackInterval, -4)),
                P(Power(SkillKind.Shock, 20)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),
            // Toxin und Ergänzungen, damit jeder Tag in mehreren Plätzen vorkommt.
            new EquipmentDefinition("toxin_injector", "Toxin Injector", EquipmentSlot.Gloves, S((StatKind.Damage, 1)),
                description: "Injects a fresh dose with every grip.", tags: T(SynergyTagIds.Toxin)),
            new EquipmentDefinition("filter_mask", "Filter Mask", EquipmentSlot.Helmet, S((StatKind.MaxHp, 3)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Scrap)),
            new EquipmentDefinition("venom_blade", "Venom Blade", EquipmentSlot.Weapon, S((StatKind.Damage, 1), (StatKind.AttackInterval, -2)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Tempo)),
            new EquipmentDefinition("bio_plating", "Bio Plating", EquipmentSlot.Chest, S((StatKind.MaxHp, 4)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Heat)),
            new EquipmentDefinition("acid_greaves", "Acid Greaves", EquipmentSlot.Legs, S((StatKind.Armor, 1), (StatKind.Dodge, BasisPoints.Percent(3))),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Phantom)),
            new EquipmentDefinition("corroded_shield", "Corroded Shield", EquipmentSlot.Shield, S((StatKind.Armor, 2), (StatKind.Block, BasisPoints.Percent(8))),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Scrap)),
            new EquipmentDefinition("leaking_boots", "Leaking Boots", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(3))),
                tags: T(SynergyTagIds.Toxin)),
            new EquipmentDefinition("servo_greaves", "Servo Greaves", EquipmentSlot.Legs, S((StatKind.Dodge, BasisPoints.Percent(4))),
                P(SkillPassive.Cast(SkillKind.Movement, -20)), tags: T(SynergyTagIds.Tempo, SynergyTagIds.Phantom)),
            new EquipmentDefinition("capacitor_vest", "Capacitor Vest", EquipmentSlot.Chest, S((StatKind.MaxHp, 3), (StatKind.Block, BasisPoints.Percent(5))),
                P(SkillPassive.Cast(SkillKind.Shock, -20)), tags: T(SynergyTagIds.Charge, SynergyTagIds.Tempo)),

            // Erleichterer (A-11): machen schwere Bausteine leichter erfüllbar, ihr Bonus bleibt (siehe ReliefCatalog).
            new EquipmentDefinition(ReliefCarrierIds.NumbingGloves, "Numbing Gloves", EquipmentSlot.Gloves, S((StatKind.Damage, 1)), weight: 5,
                description: "Own stuns last +1 s.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition(ReliefCarrierIds.AfterimageVisor, "Afterimage Visor", EquipmentSlot.Helmet, S((StatKind.MaxHp, 2)), weight: 5,
                description: "Enemy still counts as stunned for 0.5 s after a stun.", tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition(ReliefCarrierIds.PrechargedCell, "Precharged Cell", EquipmentSlot.Chest, S((StatKind.MaxHp, 3)), weight: 5,
                description: "Static starts at 3.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition(ReliefCarrierIds.PhantomStep, "Phantom Step Boots", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(4))), weight: 5,
                description: "Dodge streak only breaks on the 2nd hit.", tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition(ReliefCarrierIds.CounterShield, "Counter Shield", EquipmentSlot.Shield, S((StatKind.Armor, 1), (StatKind.Block, BasisPoints.Percent(10))),
                weight: 5, description: "Crit chance +15 % for 2 s after a Block.", tags: T(SynergyTagIds.Charge, SynergyTagIds.Tempo)),
            new EquipmentDefinition(ReliefCarrierIds.VenomTorch, "Venom Torch", EquipmentSlot.Weapon, S((StatKind.Damage, 1)), weight: 5,
                description: "\"Enemy Burning\" also counts Poison.", tags: T(SynergyTagIds.Toxin, SynergyTagIds.Heat)),
            new EquipmentDefinition(ReliefCarrierIds.PainConductor, "Pain Conductor Greaves", EquipmentSlot.Legs, S((StatKind.Armor, 1)), weight: 5,
                description: "\"HP Below x %\" triggers 5 percentage points earlier.", tags: T(SynergyTagIds.Scrap)),
        });
    }
}
