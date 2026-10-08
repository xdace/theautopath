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

        private static SkillPassive Faster(SkillKind tag, int seconds) => SkillPassive.Cooldown(tag, -Ticks.FromSeconds(seconds));

        public static EquipmentCatalog CreateDefault() => new EquipmentCatalog(new[]
        {
            // Startausrüstung der Kits (nie angeboten).
            new EquipmentDefinition("short_blade", "Kurzklinge", EquipmentSlot.Weapon, S((StatKind.Damage, 2), (StatKind.AttackInterval, -2)),
                null, weight: 0, description: "Leichte Klinge des Klingenritters.", tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("short_sword", "Kurzschwert", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                weight: 0, description: "Solides Schwert ohne Kniffe.", tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("round_shield", "Rundschild", EquipmentSlot.Shield, S((StatKind.Armor, 2), (StatKind.Block, BasisPoints.Percent(10))),
                null, weight: 0, description: "Jeder Schild kann zuschlagen.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("spark_staff", "Funkenstab", EquipmentSlot.Weapon, S((StatKind.Damage, 1), (StatKind.AttackInterval, -2)),
                null, weight: 0, description: "Zündet Gegner an.", tags: T(SynergyTagIds.Heat)),

            // Einzelteile.
            new EquipmentDefinition("titan_hammer", "Titanhammer", EquipmentSlot.Weapon, S((StatKind.Damage, 3), (StatKind.AttackInterval, 4)),
                P(Power(SkillKind.Attack, 25)), description: "Langsam, Angriff-Skills treffen härter.", tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("flamethrower", "Flammenwerfer", EquipmentSlot.Weapon, S((StatKind.Damage, 1)),
                P(Power(SkillKind.Fire, 25)), tags: T(SynergyTagIds.Heat, SynergyTagIds.Toxin)),
            new EquipmentDefinition("tower_shield", "Turmschild", EquipmentSlot.Shield, S((StatKind.Armor, 4), (StatKind.Block, BasisPoints.Percent(15))),
                P(Faster(SkillKind.Shield, 1)), tags: T(SynergyTagIds.Charge, SynergyTagIds.Scrap)),
            new EquipmentDefinition("echo_helm", "Echo-Helm", EquipmentSlot.Helmet, null, P(Faster(SkillKind.Attack, 1)), weight: 2,
                description: "Selten. Angriff-Skills laden schneller.", tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("incendiary_gloves", "Brandhandschuhe", EquipmentSlot.Gloves, null, P(Power(SkillKind.Fire, 15), Faster(SkillKind.Fire, 1)), weight: 6,
                description: "Für Feuer-Builds.", tags: T(SynergyTagIds.Heat)),
            new EquipmentDefinition("leather_gloves", "Lederhandschuhe", EquipmentSlot.Gloves, S((StatKind.Crit, BasisPoints.Percent(10))), tags: T(SynergyTagIds.Tempo)),
            new EquipmentDefinition("iron_greaves", "Eisenbeinschienen", EquipmentSlot.Legs, S((StatKind.Armor, 2)), tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("padded_vest", "Gepolsterte Weste", EquipmentSlot.Chest, S((StatKind.MaxHp, 5)), tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("light_boots", "Leichte Stiefel", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5))), tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),

            // Überlast-Protokoll.
            new EquipmentDefinition("overload_chassis", "Defektes Kühlkörper-Chassis", EquipmentSlot.Chest, S((StatKind.MaxHp, 6)),
                P(Faster(SkillKind.Healing, 1)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat, SynergyTagIds.Tempo)),
            new EquipmentDefinition("thermo_blade", "Rotglühende Thermo-Klinge", EquipmentSlot.Weapon, S((StatKind.Damage, 2)),
                P(Power(SkillKind.Fire, 20)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat)),
            new EquipmentDefinition("warning_visor", "Warnleuchten-Visier", EquipmentSlot.Helmet, null,
                P(Power(SkillKind.Healing, 20)), SetIds.Overload, weight: 4, tags: T(SynergyTagIds.Heat, SynergyTagIds.Charge)),

            // Aegis-Firewall.
            new EquipmentDefinition("holo_barrier", "Holographische Barriere", EquipmentSlot.Shield, S((StatKind.Armor, 3), (StatKind.Block, BasisPoints.Percent(15))),
                P(Power(SkillKind.Shock, 20)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge, SynergyTagIds.Phantom)),
            new EquipmentDefinition("shock_absorber", "Schock-Absorber", EquipmentSlot.Gloves, S((StatKind.Block, BasisPoints.Percent(5))),
                P(Faster(SkillKind.Shield, 2)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition("mag_anchors", "Magnetische Bodenanker", EquipmentSlot.Boots, S((StatKind.Armor, 2)),
                P(Faster(SkillKind.Movement, 1)), SetIds.Aegis, weight: 4, tags: T(SynergyTagIds.Charge, SynergyTagIds.Scrap)),

            // Schrott-Ernter.
            new EquipmentDefinition("plasma_drill", "Zweihändiger Plasma-Bohrer", EquipmentSlot.Weapon, S((StatKind.Damage, 4), (StatKind.AttackInterval, 6)),
                P(Power(SkillKind.Attack, 25)), SetIds.Scrap, twoHanded: true, weight: 4, tags: T(SynergyTagIds.Scrap, SynergyTagIds.Heat)),
            new EquipmentDefinition("crawler_tracks", "Schweres Kettenlaufwerk", EquipmentSlot.Legs, S((StatKind.Armor, 4), (StatKind.Dodge, -BasisPoints.Percent(10))),
                null, SetIds.Scrap, weight: 4, tags: T(SynergyTagIds.Scrap)),
            new EquipmentDefinition("resource_compactor", "Ressourcen-Kompaktor", EquipmentSlot.Chest, S((StatKind.MaxHp, 4)),
                null, SetIds.Scrap, weight: 4, tags: T(SynergyTagIds.Scrap, SynergyTagIds.Toxin)),

            // Phantom-Signal.
            new EquipmentDefinition("gyro_thrusters", "Gyroskopische Schubdüsen", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(5))),
                P(Faster(SkillKind.Movement, 1)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),
            new EquipmentDefinition("holo_projector", "Holo-Projektor", EquipmentSlot.Helmet, null,
                P(Faster(SkillKind.Shock, 1)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition("shock_dagger", "Leichter Schock-Dolch", EquipmentSlot.Weapon, S((StatKind.AttackInterval, -4)),
                P(Power(SkillKind.Shock, 20)), SetIds.Phantom, weight: 4, tags: T(SynergyTagIds.Phantom, SynergyTagIds.Tempo)),
            // Toxin und Ergänzungen, damit jeder Tag in mehreren Plätzen vorkommt.
            new EquipmentDefinition("toxin_injector", "Gift-Injektor", EquipmentSlot.Gloves, S((StatKind.Damage, 1)),
                description: "Spritzt bei jedem Griff nach.", tags: T(SynergyTagIds.Toxin)),
            new EquipmentDefinition("filter_mask", "Filtermaske", EquipmentSlot.Helmet, S((StatKind.MaxHp, 3)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Scrap)),
            new EquipmentDefinition("venom_blade", "Toxin-Klinge", EquipmentSlot.Weapon, S((StatKind.Damage, 1), (StatKind.AttackInterval, -2)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Tempo)),
            new EquipmentDefinition("bio_plating", "Bio-Panzerung", EquipmentSlot.Chest, S((StatKind.MaxHp, 4)),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Heat)),
            new EquipmentDefinition("acid_greaves", "Säure-Beinschienen", EquipmentSlot.Legs, S((StatKind.Armor, 1), (StatKind.Dodge, BasisPoints.Percent(3))),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Phantom)),
            new EquipmentDefinition("corroded_shield", "Zerfressener Schild", EquipmentSlot.Shield, S((StatKind.Armor, 2), (StatKind.Block, BasisPoints.Percent(8))),
                tags: T(SynergyTagIds.Toxin, SynergyTagIds.Scrap)),
            new EquipmentDefinition("leaking_boots", "Leckende Stiefel", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(3))),
                tags: T(SynergyTagIds.Toxin)),
            new EquipmentDefinition("servo_greaves", "Servo-Beinschienen", EquipmentSlot.Legs, S((StatKind.Dodge, BasisPoints.Percent(4))),
                P(SkillPassive.Cast(SkillKind.Movement, -20)), tags: T(SynergyTagIds.Tempo, SynergyTagIds.Phantom)),
            new EquipmentDefinition("capacitor_vest", "Kondensator-Weste", EquipmentSlot.Chest, S((StatKind.MaxHp, 3), (StatKind.Block, BasisPoints.Percent(5))),
                P(SkillPassive.Cast(SkillKind.Shock, -20)), tags: T(SynergyTagIds.Charge, SynergyTagIds.Tempo)),

            // Erleichterer (A-11): machen schwere Bausteine leichter erfüllbar, ihr Bonus bleibt (siehe ReliefCatalog).
            new EquipmentDefinition(ReliefCarrierIds.NumbingGloves, "Lähmhandschuhe", EquipmentSlot.Gloves, S((StatKind.Damage, 1)), weight: 5,
                description: "Eigene Betäubungen dauern +1 s.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition(ReliefCarrierIds.AfterimageVisor, "Nachbild-Visier", EquipmentSlot.Helmet, S((StatKind.MaxHp, 2)), weight: 5,
                description: "Gegner gilt 0,5 s nach einer Betäubung noch als betäubt.", tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition(ReliefCarrierIds.PrechargedCell, "Vorgeladene Zelle", EquipmentSlot.Chest, S((StatKind.MaxHp, 3)), weight: 5,
                description: "Ladung startet bei 3.", tags: T(SynergyTagIds.Charge)),
            new EquipmentDefinition(ReliefCarrierIds.PhantomStep, "Phantomschritt-Stiefel", EquipmentSlot.Boots, S((StatKind.Dodge, BasisPoints.Percent(4))), weight: 5,
                description: "Ausweicher-Serie bricht erst beim 2. Treffer.", tags: T(SynergyTagIds.Phantom)),
            new EquipmentDefinition(ReliefCarrierIds.CounterShield, "Konterschild", EquipmentSlot.Shield, S((StatKind.Armor, 1), (StatKind.Block, BasisPoints.Percent(10))),
                weight: 5, description: "Krit-Chance +15 % für 2 s nach einem Block.", tags: T(SynergyTagIds.Charge, SynergyTagIds.Tempo)),
            new EquipmentDefinition(ReliefCarrierIds.VenomTorch, "Giftbrenner", EquipmentSlot.Weapon, S((StatKind.Damage, 1)), weight: 5,
                description: "«Gegner brennt» gilt auch bei Gift.", tags: T(SynergyTagIds.Toxin, SynergyTagIds.Heat)),
            new EquipmentDefinition(ReliefCarrierIds.PainConductor, "Schmerzleiter-Beinschienen", EquipmentSlot.Legs, S((StatKind.Armor, 1)), weight: 5,
                description: "«Schwerer Treffer» gilt 5 Prozentpunkte früher.", tags: T(SynergyTagIds.Scrap)),
        });
    }
}
