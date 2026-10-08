using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;

namespace Betaknight.Core.Combat
{
    /// <summary>
    /// Eine Gegnergruppe für die Arena: Werte und feste Platine jedes Gegners (A-19, siehe <see cref="EnemyBoard"/>).
    /// Grössere Gegner-Skills haben eine sichtbare Aufladung (Ausholen ≥ 1 s), damit "Enemy Charging" etwas zu kontern hat.
    /// </summary>
    public sealed class EnemyDefinition
    {
        private readonly Func<List<CombatantSetup>> _create;

        public string Id { get; }
        public string Name { get; }
        public int MinTier { get; }
        public int MaxTier { get; }
        public bool IsBoss { get; }
        public int Weight { get; }

        public EnemyDefinition(string id, string name, int minTier, int maxTier, Func<List<CombatantSetup>> create,
            int weight = 10, bool isBoss = false)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Id fehlt.", nameof(id));
            Id = id;
            Name = name ?? id;
            MinTier = minTier;
            MaxTier = maxTier;
            _create = create ?? throw new ArgumentNullException(nameof(create));
            Weight = Math.Max(0, weight);
            IsBoss = isBoss;
        }

        public bool FitsTier(int tier) => tier >= MinTier && tier <= MaxTier;

        /// <summary>Neue Kämpfer-Baupläne für einen Kampf.</summary>
        public List<CombatantSetup> Create() => _create();
    }

    /// <summary>Alle Gegner nach Stufe (Entfernung vom Start). Neue Gegner sind neue Einträge.</summary>
    public sealed class EnemyCatalog
    {
        private readonly List<EnemyDefinition> _all = new List<EnemyDefinition>();

        public IReadOnlyList<EnemyDefinition> All => _all;

        public EnemyCatalog(IEnumerable<EnemyDefinition> enemies)
        {
            var ids = new HashSet<string>();
            foreach (EnemyDefinition e in enemies ?? Array.Empty<EnemyDefinition>())
            {
                if (e == null) continue;
                if (!ids.Add(e.Id)) throw new ArgumentException($"Doppelte Gegner-Id {e.Id}.");
                _all.Add(e);
            }
        }

        /// <summary>Würfelt einen passenden Gegner. Ohne Treffer für die Stufe: der nächstschwächere.</summary>
        public EnemyDefinition Pick(int tier, bool boss, Random random)
        {
            var pool = _all.FindAll(e => e.IsBoss == boss && e.Weight > 0 && e.FitsTier(tier));
            if (pool.Count == 0) pool = _all.FindAll(e => e.IsBoss == boss && e.Weight > 0 && e.MinTier <= tier);
            if (pool.Count == 0) pool = _all.FindAll(e => e.IsBoss == boss);
            if (pool.Count == 0) throw new InvalidOperationException("Keine Gegner im Katalog.");

            int total = 0;
            foreach (EnemyDefinition e in pool) total += Math.Max(1, e.Weight);
            int roll = random.Next(total);
            foreach (EnemyDefinition e in pool)
            {
                roll -= Math.Max(1, e.Weight);
                if (roll < 0) return e;
            }
            return pool[pool.Count - 1];
        }

        // ------------------------------------------------------------------ Standard-Gegner

        /// <summary>Ein Takt-Relais (Clock): löst alle <paramref name="seconds"/> s aus. Ersetzt das frühere «Always» mit Cooldown.</summary>
        private static EnemyPart Every(int seconds, SkillDefinition skill) =>
            new EnemyPart(new ClockCondition(Ticks.FromSeconds(seconds)), CatalogTexts.EveryLabel(seconds), skill);

        private static EnemyPart When(ICondition condition, SkillDefinition skill, string label) => new EnemyPart(condition, label, skill);

        /// <summary>Form eines Gegner-Skills nach Wucht: wie beim Ritter wirken grössere Komponenten stärker.</summary>
        private static Shape ShapeFor(int damagePercent) =>
            damagePercent >= 400 ? new Shape(2, 2) : damagePercent >= 250 ? new Shape(2, 1) : Shape.One;

        private static SkillDefinition Charge(string id, string name, int windupTicks, int damagePercent, params ISkillEffect[] extra)
        {
            var effects = new List<ISkillEffect> { new DamageEffect(BasisPoints.Percent(damagePercent)) };
            effects.AddRange(extra);
            return new SkillDefinition(id, name, windupTicks, 6, effects,
                CatalogTexts.EnemyChargeDescription(windupTicks * 10 / Ticks.PerSecond / 10.0, damagePercent), countsAsAttack: true,
                shape: ShapeFor(damagePercent));
        }

        /// <summary>
        /// Leben aller Gegner in Prozent der Werte unten (A-12: der Basisangriff macht nur noch 60 %, Skills tragen den
        /// Schaden; frühe Kämpfe mit 1–2 Start-Skills sollen gut schaffbar bleiben).
        /// </summary>
        public const int HpPercent = 65;

        private static CombatantSetup Enemy(string name, int hp, int damage, int interval, int armor, params EnemyPart[] parts) =>
            new CombatantSetup
            {
                Name = name, Stats = new CombatStats(Math.Max(1, hp * HpPercent / 100), damage, interval, armor), Board = EnemyBoard.Build(parts),
            };

        private static List<CombatantSetup> One(CombatantSetup s) => new List<CombatantSetup> { s };

        public static EnemyCatalog CreateDefault() => new EnemyCatalog(new[]
        {
            // Stufe 1–2: lernen, was eine Aufladung ist.
            new EnemyDefinition("scrap_rat", "Scrap Rat", 0, 2, () => One(Enemy("Scrap Rat", 16, 1, 14, 0))),
            new EnemyDefinition("rust_warden", "Rust Warden", 0, 3, () => One(Enemy("Rust Warden", 22, 1, 20, 1,
                Every(7, Charge("ram", "Ram", 30, 300))))),

            // Stufe 3: Ausweichen und Panzerung.
            new EnemyDefinition("spark_drone", "Spark Drone", 3, 4, () =>
            {
                CombatantSetup drone = Enemy("Spark Drone", 24, 2, 20, 0,
                    Every(6, Charge("zap", "Spark Strike", 24, 150, new StunEffect(Ticks.FromSeconds(1)))));
                drone.Stats[StatKind.Dodge] = BasisPoints.Percent(15);
                return One(drone);
            }),
            new EnemyDefinition("armor_beetle", "Armor Beetle", 3, 5, () => One(Enemy("Armor Beetle", 30, 2, 26, 4,
                Every(7, Charge("crush", "Crush", 30, 300))))),
            new EnemyDefinition("rat_pack", "Rat Pack", 3, 6, () => new List<CombatantSetup>
            {
                Enemy("Scrap Rat", 12, 1, 16, 0),
                Enemy("Scrap Rat", 12, 1, 16, 0),
            }, weight: 6),

            // Stufe 4+: Brennen, Heilung, harte Treffer.
            new EnemyDefinition("smelter", "Smelter", 4, 99, () => One(Enemy("Smelter", 40, 1, 16, 2,
                When(new EnemyHasStatusCondition(StatusIds.Burn).Not(), new SkillDefinition("smelt", "Smelt Beam", 20, 6,
                    new ISkillEffect[] { new BurnEffect(Ticks.FromSeconds(4), BasisPoints.Percent(60)) }, shape: new Shape(1, 2)), "Knight not burning"),
                Every(8, Charge("melt", "Ember Strike", 30, 250))))),
            new EnemyDefinition("siege_golem", "Siege Golem", 5, 99, () => One(Enemy("Siege Golem", 60, 2, 28, 3,
                When(new HpBelowCondition(BasisPoints.Percent(40)), new SkillDefinition("patch", "Self-Repair", 30, 6,
                    new ISkillEffect[] { new HealEffect(BasisPoints.Percent(20)) }, shape: new Shape(2, 2)), "HP below 40 %"),
                Every(7, Charge("slam", "Hammer Slam", 36, 400))))),

            // Boss alle 25 Züge: unbesiegbar, der Ritter muss bis zum Fluchtportal überleben.
            new EnemyDefinition("overseer", "The Overseer", 0, 99, () => One(Enemy("The Overseer", 99999, 1, 20, 4,
                When(new ClockCondition(Ticks.FromSeconds(15)), new SkillDefinition("pulse", "System Pulse", 30, 10,
                    new ISkillEffect[] { new StunEffect(Ticks.FromSeconds(2)), new DamageEffect(BasisPoints.Percent(100)) }, countsAsAttack: true,
                    shape: new Shape(2, 1)), CatalogTexts.EveryLabel(15)),
                Every(8, Charge("beam", "Annihilation Beam", 40, 500)))), isBoss: true),
        });
    }
}
