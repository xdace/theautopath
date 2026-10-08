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

        private static string[] Skills(params string[] ids) => ids;

        public static EquipmentCatalog CreateDefault() => new EquipmentCatalog(new[]
        {
            // Startausrüstung der Kits (nie angeboten).
            new EquipmentDefinition("short_blade", "Kurzklinge", EquipmentSlot.Weapon, S((StatKind.Damage, 2), (StatKind.AttackInterval, -2)),
                Skills(SkillIds.ArmorBreak), weight: 0, description: "Leichte Klinge des Klingenritters."),
            new EquipmentDefinition("short_sword", "Kurzschwert", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                weight: 0, description: "Solides Schwert ohne Kniffe."),
            new EquipmentDefinition("round_shield", "Rundschild", EquipmentSlot.Shield, S((StatKind.Armor, 2), (StatKind.Block, BasisPoints.Percent(10))),
                Skills(SkillIds.ShieldBash), weight: 0, description: "Jeder Schild kann zuschlagen."),
            new EquipmentDefinition("spark_staff", "Funkenstab", EquipmentSlot.Weapon, S((StatKind.Damage, 1), (StatKind.AttackInterval, -2)),
                Skills(SkillIds.Ignite), weight: 0, description: "Zündet Gegner an."),

            // Einzelteile.
            new EquipmentDefinition("titan_hammer", "Titanhammer", EquipmentSlot.Weapon, S((StatKind.Damage, 3), (StatKind.AttackInterval, 4)),
                Skills(SkillIds.ArmorBreak), description: "Langsam, bricht Rüstung."),
            new EquipmentDefinition("flamethrower", "Flammenwerfer", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                Skills(SkillIds.Ignite)),
            new EquipmentDefinition("tower_shield", "Turmschild", EquipmentSlot.Shield, S((StatKind.Armor, 4), (StatKind.Block, BasisPoints.Percent(15))),
                Skills(SkillIds.ShieldBash)),
            new EquipmentDefinition("echo_helm", "Echo-Helm", EquipmentSlot.Helmet, null, Skills(SkillIds.Echo), weight: 2,
                description: "Selten. Wiederholt den letzten Skill."),
            new EquipmentDefinition("incendiary_gloves", "Brandhandschuhe", EquipmentSlot.Gloves, null, Skills(SkillIds.Ignite), weight: 6,
                description: "Entzünden ohne Waffe, für Ketten-Builds."),
            new EquipmentDefinition("leather_gloves", "Lederhandschuhe", EquipmentSlot.Gloves, S((StatKind.Crit, BasisPoints.Percent(10)))),
            new EquipmentDefinition("iron_greaves", "Eisenbeinschienen", EquipmentSlot.Legs, S((StatKind.Armor, 2))),
            new EquipmentDefinition("padded_vest", "Gepolsterte Weste", EquipmentSlot.Chest, S((StatKind.MaxHp, 5))),
            new EquipmentDefinition("light_boots", "Leichte Stiefel", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5)))),

            // Überlast-Protokoll.
            new EquipmentDefinition("overload_chassis", "Defektes Kühlkörper-Chassis", EquipmentSlot.Chest, S((StatKind.MaxHp, 6)),
                Skills(SkillIds.Coolant), SetIds.Overload, weight: 4),
            new EquipmentDefinition("thermo_blade", "Rotglühende Thermo-Klinge", EquipmentSlot.Weapon, S((StatKind.Damage, 2)),
                Skills(SkillIds.Ignite), SetIds.Overload, weight: 4),
            new EquipmentDefinition("warning_visor", "Warnleuchten-Visier", EquipmentSlot.Helmet, null,
                Skills(SkillIds.Repair), SetIds.Overload, weight: 4),

            // Aegis-Firewall.
            new EquipmentDefinition("holo_barrier", "Holographische Barriere", EquipmentSlot.Shield, S((StatKind.Armor, 3), (StatKind.Block, BasisPoints.Percent(15))),
                Skills(SkillIds.EmpBash), SetIds.Aegis, weight: 4),
            new EquipmentDefinition("shock_absorber", "Schock-Absorber", EquipmentSlot.Gloves, S((StatKind.Block, BasisPoints.Percent(5))),
                Skills(SkillIds.ShieldWall), SetIds.Aegis, weight: 4),
            new EquipmentDefinition("mag_anchors", "Magnetische Bodenanker", EquipmentSlot.Boots, S((StatKind.Armor, 2)),
                Skills(SkillIds.Anchor), SetIds.Aegis, weight: 4),

            // Schrott-Ernter.
            new EquipmentDefinition("plasma_drill", "Zweihändiger Plasma-Bohrer", EquipmentSlot.Weapon, S((StatKind.Damage, 4), (StatKind.AttackInterval, 6)),
                Skills(SkillIds.Drill), SetIds.Scrap, twoHanded: true, weight: 4),
            new EquipmentDefinition("crawler_tracks", "Schweres Kettenlaufwerk", EquipmentSlot.Legs, S((StatKind.Armor, 4), (StatKind.Dodge, -BasisPoints.Percent(10))),
                null, SetIds.Scrap, weight: 4),
            new EquipmentDefinition("resource_compactor", "Ressourcen-Kompaktor", EquipmentSlot.Chest, S((StatKind.MaxHp, 4)),
                null, SetIds.Scrap, weight: 4),

            // Phantom-Signal.
            new EquipmentDefinition("gyro_thrusters", "Gyroskopische Schubdüsen", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5))),
                Skills(SkillIds.Thrusters), SetIds.Phantom, weight: 4),
            new EquipmentDefinition("holo_projector", "Holo-Projektor", EquipmentSlot.Helmet, null,
                Skills(SkillIds.Flashbang), SetIds.Phantom, weight: 4),
            new EquipmentDefinition("shock_dagger", "Leichter Schock-Dolch", EquipmentSlot.Weapon, S((StatKind.AttackInterval, -4)),
                Skills(SkillIds.ShockStab), SetIds.Phantom, weight: 4),
        });
    }
}
