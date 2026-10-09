using System;
using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Gear;
using Betaknight.Core.Map;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;

namespace Betaknight.Core.Combat
{
    /// <summary>Was man von einer Gegner-Platine bergen kann.</summary>
    public enum EnemyLootKind
    {
        Skill,
        Module,
        Chip,
        Rune,
    }

    /// <summary>Ein Teil, das ein Gegner im Kampf benutzt hat und das nach dem Sieg geborgen werden kann.</summary>
    public readonly struct EnemyLoot : IEquatable<EnemyLoot>
    {
        public readonly EnemyLootKind Kind;
        public readonly string Id;
        public readonly string Name;

        public EnemyLoot(EnemyLootKind kind, string id, string name)
        {
            Kind = kind;
            Id = id;
            Name = name ?? id;
        }

        public bool Equals(EnemyLoot other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is EnemyLoot l && Equals(l);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Id?.GetHashCode() ?? 0);
        public override string ToString() => $"{Kind} {Id}";
    }

    /// <summary>Eine Ausrüstungs-Komponente eines Gegners: Rune als Relais, Skill aus dem Katalog des Ritters, Module am Skill.</summary>
    public sealed class EnemyGearComponent
    {
        public string RuneId { get; }
        public string SkillId { get; }
        public List<string> Modules { get; } = new List<string>();

        public EnemyGearComponent(string runeId, string skillId)
        {
            RuneId = runeId;
            SkillId = skillId;
        }
    }

    /// <summary>Was ein Gegner zusätzlich zu seinen festen Teilen trägt: Komponenten und Chips aus den Katalogen des Ritters.</summary>
    public sealed class EnemyGear
    {
        public List<EnemyGearComponent> Components { get; } = new List<EnemyGearComponent>();
        public List<string> Chips { get; } = new List<string>();

        public bool IsEmpty => Components.Count == 0 && Chips.Count == 0;
    }

    /// <summary>
    /// Regeln für die Ausrüstung der Gegner als Daten: wie viele Komponenten, Module und Chips je Stufe, Elite trägt mehr.
    /// Alles, was ein Gegner aus den Katalogen des Ritters trägt, kann nach dem Sieg geborgen werden.
    /// </summary>
    public sealed class EnemyLoadoutConfig
    {
        /// <summary>Ab dieser Stufe trägt ein normaler Gegner eine Ausrüstungs-Komponente, ab der zweiten Schwelle zwei.</summary>
        public int FirstGearTier = 2;
        public int SecondGearTier = 5;

        /// <summary>Elite trägt so viele Komponenten mehr.</summary>
        public int EliteExtraGear = 1;
        public int MaxGear = 3;

        /// <summary>Chance je Ausrüstungs-Komponente auf ein Modul (normale Gegner), in Prozent.</summary>
        public int ModuleChance = 25;

        /// <summary>Elite: Chance je Komponente (auch die festen) auf ein Modul; mindestens eines ist garantiert.</summary>
        public int EliteModuleChance = 50;

        /// <summary>Elite trägt so viele Chips, ab <see cref="EliteSecondChipTier"/> einen mehr.</summary>
        public int EliteChips = 1;
        public int EliteSecondChipTier = 7;

        /// <summary>Grösste Komponente nach Stufe: bis Stufe 3 zwei Zellen, bis 6 vier, danach sechs.</summary>
        public int MaxCells(int tier) => tier <= 3 ? 2 : tier <= 6 ? 4 : 6;

        /// <summary>Wie viele Teile man nach dem Sieg bergen darf.</summary>
        public int LootPicks = 1;
        public int EliteLootPicks = 2;

        /// <summary>Runen, mit denen Gegner sinnvoll kämpfen (aus ihrer Sicht ist der Ritter der Gegner).</summary>
        public List<string> RunePool = new List<string>
            { "battle_start", "every_5s", "when_hit", "on_hit", "every_nth_hit_taken", "hp_low", "enemy_low", "every_20s" };

        /// <summary>Chips, die bei Gegnern ohne Leiterbahnen etwas tun (Platine oder berührte Komponente).</summary>
        public List<string> ChipPool = new List<string> { CircuitEffectIds.Firewall, CircuitEffectIds.Overflow, CircuitEffectIds.Watchdog };

        public static EnemyLoadoutConfig Default { get; } = new EnemyLoadoutConfig();
    }

    /// <summary>
    /// Rüstet Gegner aus (deterministisch aus einem Zufall) und baut ihre Platinen: feste Teile plus Ausrüstung aus den
    /// Katalogen des Ritters, gebaut wie die Platine des Ritters. Liefert auch die Beute, die man danach bergen kann.
    /// </summary>
    public sealed class EnemyLoadout
    {
        public EnemyLoadoutConfig Config { get; }
        public BoardFactory Factory { get; }
        public RuneCatalog Runes { get; }
        public SkillCatalog Skills { get; }
        public ModuleCatalog Modules { get; }
        public ChipCatalog Chips { get; }
        public CircuitEffectCatalog Effects { get; }

        public EnemyLoadout(EnemyLoadoutConfig config = null, CircuitEffectCatalog effects = null)
        {
            Config = config ?? EnemyLoadoutConfig.Default;
            Effects = effects ?? CircuitEffectCatalog.Shared;
            Runes = RuneCatalog.CreateDefault();
            Skills = SkillCatalog.CreateDefault(Effects);
            Modules = ModuleCatalog.CreateDefault(Effects);
            Chips = ChipCatalog.CreateDefault(null, Effects);
            Factory = new BoardFactory(Runes, null, Skills, Modules, null, null, null, Chips, Effects);
        }

        private static EnemyLoadout _default;
        public static EnemyLoadout Default => _default ?? (_default = new EnemyLoadout());

        public string ModuleName(string id) => Modules.TryGet(id, out ModuleDefinition d) ? d.Name : id;

        // ------------------------------------------------------------------ Ausrüsten

        /// <summary>Würfelt die Ausrüstung eines Gegners. Fügt Elite-Module an feste Teile direkt an <paramref name="fighter"/> an.</summary>
        public EnemyGear Equip(EnemyFighter fighter, int tier, bool elite, Random random)
        {
            var gear = new EnemyGear();
            int count = tier >= Config.SecondGearTier ? 2 : tier >= Config.FirstGearTier ? 1 : 0;
            if (elite) count += Config.EliteExtraGear;
            count = Math.Min(count, Config.MaxGear);

            var usedSkills = new HashSet<string>(fighter.Parts.Select(p => p.Skill.Id));
            for (int i = 0; i < count; i++)
            {
                EnemyGearComponent component = RollComponent(tier, usedSkills, random);
                if (component == null) break;
                usedSkills.Add(component.SkillId);
                gear.Components.Add(component);
            }

            // Module: normale Gegner nur an der Ausrüstung, Elite an jeder Komponente und mindestens eines.
            int modules = 0;
            foreach (EnemyGearComponent c in gear.Components)
            {
                if (random.Next(100) >= (elite ? Config.EliteModuleChance : Config.ModuleChance)) continue;
                string m = RollModule(random);
                if (m != null) { c.Modules.Add(m); modules++; }
            }
            if (elite)
            {
                var parts = fighter.Parts.ToList();
                for (int i = 0; i < parts.Count; i++)
                {
                    if (random.Next(100) >= Config.EliteModuleChance) continue;
                    string m = RollModule(random);
                    if (m != null) { parts[i] = parts[i].WithModule(m); modules++; }
                }
                if (modules == 0)
                {
                    string m = RollModule(random);
                    if (m != null)
                    {
                        if (gear.Components.Count > 0) gear.Components[0].Modules.Add(m);
                        else if (parts.Count > 0) parts[0] = parts[0].WithModule(m);
                    }
                }
                fighter.Parts = parts;

                int chips = Config.EliteChips + (tier >= Config.EliteSecondChipTier ? 1 : 0);
                var pool = Config.ChipPool.Where(Chips.Contains).ToList();
                for (int i = 0; i < chips && pool.Count > 0; i++)
                {
                    string chip = pool[random.Next(pool.Count)];
                    pool.Remove(chip);
                    gear.Chips.Add(chip);
                }
            }
            return gear;
        }

        private EnemyGearComponent RollComponent(int tier, ICollection<string> used, Random random)
        {
            int maxCells = Config.MaxCells(tier);
            var runes = Config.RunePool.Where(id => Runes.TryGet(id, out _)).Select(Runes.Get).ToList();
            int largest = runes.Count == 0 ? 0 : runes.Max(r => Factory.Bonus.MaxCells(r.DifficultyFor(false)));
            var skills = Skills.All.Where(s => !s.IsBasicAttack && !s.IsEvolution && !used.Contains(s.Id)
                && s.Shape.Cells <= Math.Min(maxCells, largest)).ToList();
            if (skills.Count == 0) return null;
            SkillDefinition skill = skills[random.Next(skills.Count)];
            var fitting = runes.Where(r => Factory.Bonus.MaxCells(r.DifficultyFor(false)) >= skill.Shape.Cells).ToList();
            RuneDefinition rune = fitting[random.Next(fitting.Count)];
            return new EnemyGearComponent(rune.Id, skill.Id);
        }

        /// <summary>Ein Skill-Modul nach Gewicht (keine Auslöser, keine Relais-Module).</summary>
        private string RollModule(Random random)
        {
            var pool = Modules.All.Where(m => m.Kind == ModuleKind.Skill && m.Weight > 0).ToList();
            int total = pool.Sum(m => m.Weight);
            if (total <= 0) return null;
            int roll = random.Next(total);
            foreach (ModuleDefinition m in pool)
            {
                roll -= m.Weight;
                if (roll < 0) return m.Id;
            }
            return pool[pool.Count - 1].Id;
        }

        // ------------------------------------------------------------------ Beute

        /// <summary>
        /// Alles, was ein Gegner aus den Katalogen des Ritters trägt (ohne Doppelte): Skills (auch eigene wie Hacks, wenn es sie
        /// für den Ritter gibt), Runen, Module, Chips.
        /// </summary>
        public List<EnemyLoot> LootOf(EnemyFighter fighter, EnemyGear gear)
        {
            var loot = new List<EnemyLoot>();
            void Add(EnemyLoot l)
            {
                if (!loot.Contains(l)) loot.Add(l);
            }

            if (gear != null)
                foreach (EnemyGearComponent c in gear.Components)
                {
                    Add(new EnemyLoot(EnemyLootKind.Skill, c.SkillId, Skills.TryGet(c.SkillId, out SkillDefinition s) ? s.Name : c.SkillId));
                    Add(new EnemyLoot(EnemyLootKind.Rune, c.RuneId, Runes.TryGet(c.RuneId, out RuneDefinition r) ? r.Name : c.RuneId));
                    foreach (string m in c.Modules) Add(new EnemyLoot(EnemyLootKind.Module, m, ModuleName(m)));
                }
            foreach (EnemyPart p in fighter.Parts)
            {
                // Eigene Skills, die es auch für den Ritter gibt (z. B. Hacks), lassen sich ebenfalls bergen.
                if (Skills.TryGet(p.Skill.Id, out SkillDefinition own) && !own.IsBasicAttack && !own.IsEvolution)
                    Add(new EnemyLoot(EnemyLootKind.Skill, own.Id, own.Name));
                foreach (string m in p.Modules) Add(new EnemyLoot(EnemyLootKind.Module, m, ModuleName(m)));
            }
            if (gear != null)
                foreach (string c in gear.Chips) Add(new EnemyLoot(EnemyLootKind.Chip, c, Chips.TryGet(c, out ChipDefinition d) ? d.Name : c));
            return loot;
        }
    }

    /// <summary>
    /// Ein gewürfelter Gegner mit fertiger Platine und Beute. Mit demselben Zufall (z. B. aus Karten-Seed und Feld) ist er
    /// immer gleich: die Karte zeigt vor dem Kampf genau die Platine, gegen die man kämpft.
    /// </summary>
    public sealed class EnemyEncounter
    {
        public EnemyDefinition Definition { get; }
        public string Name { get; }
        public bool IsElite { get; }
        public List<CombatantSetup> Fighters { get; }
        public IReadOnlyList<EnemyLoot> Loot { get; }

        /// <summary>Wie viele Teile nach dem Sieg geborgen werden dürfen.</summary>
        public int LootPicks { get; }

        private EnemyEncounter(EnemyDefinition definition, string name, bool elite, List<CombatantSetup> fighters, IReadOnlyList<EnemyLoot> loot,
            int picks)
        {
            Definition = definition;
            Name = name;
            IsElite = elite;
            Fighters = fighters;
            Loot = loot;
            LootPicks = picks;
        }

        /// <summary>Würfelt Gegner und Ausrüstung. Nur der erste Kämpfer einer Gruppe wird ausgerüstet; der Boss nie.</summary>
        public static EnemyEncounter Roll(EnemyCatalog catalog, EnemyLoadout loadout, int tier, CellContent content, Random random)
        {
            bool boss = content == CellContent.Boss;
            bool elite = content == CellContent.Elite;
            EnemyDefinition definition = catalog.Pick(tier, boss, random);
            loadout = loadout ?? EnemyLoadout.Default;

            var fighters = new List<CombatantSetup>();
            var loot = new List<EnemyLoot>();
            List<EnemyFighter> blueprints = definition.Fighters();
            for (int i = 0; i < blueprints.Count; i++)
            {
                EnemyFighter f = blueprints[i];
                if (boss || i > 0)
                {
                    fighters.Add(f.ToSetup());
                    continue;
                }
                EnemyGear gear = loadout.Equip(f, tier, elite, random);
                fighters.Add(f.ToSetup(EnemyBoard.Build(f.Parts, gear, loadout)));
                foreach (EnemyLoot l in loadout.LootOf(f, gear))
                    if (!loot.Contains(l)) loot.Add(l);
            }
            string name = elite ? ArenaTexts.EliteName(definition.Name) : definition.Name;
            int picks = loot.Count == 0 ? 0 : Math.Min(loot.Count, elite ? loadout.Config.EliteLootPicks : loadout.Config.LootPicks);
            return new EnemyEncounter(definition, name, elite, fighters, loot, picks);
        }
    }
}
