using Betaknight.Core.Map;

namespace Betaknight.Core.Encounters
{
    public enum EffectKind
    {
        Gold,
        Heal,

        /// <summary>Schaden durch ein Event. Lässt immer mindestens 1 HP übrig.</summary>
        Damage,
        MaxHp,
        Shards,

        /// <summary>Kundschaftet alle Felder im Umkreis <see cref="EncounterEffect.Min"/> aus (Inhalt sichtbar).</summary>
        ScoutAround,

        /// <summary>Kundschaftet das nächste noch unbekannte Feld mit <see cref="EncounterEffect.Target"/> aus.</summary>
        ScoutNearest,
    }

    /// <summary>Eine einzelne Wirkung. Die Menge wird beim Auslösen zwischen Min und Max (inklusive) gewürfelt.</summary>
    public readonly struct EncounterEffect
    {
        public readonly EffectKind Kind;
        public readonly int Min;
        public readonly int Max;
        public readonly CellContent Target;

        public EncounterEffect(EffectKind kind, int min, int max, CellContent target = CellContent.Empty)
        {
            Kind = kind;
            Min = min;
            Max = max < min ? min : max;
            Target = target;
        }

        public static EncounterEffect Gold(int min, int max = 0) => new EncounterEffect(EffectKind.Gold, min, max);
        public static EncounterEffect Heal(int min, int max = 0) => new EncounterEffect(EffectKind.Heal, min, max);
        public static EncounterEffect Damage(int min, int max = 0) => new EncounterEffect(EffectKind.Damage, min, max);
        public static EncounterEffect MaxHp(int amount) => new EncounterEffect(EffectKind.MaxHp, amount, amount);
        public static EncounterEffect Shards(int amount) => new EncounterEffect(EffectKind.Shards, amount, amount);
        public static EncounterEffect ScoutAround(int radius) => new EncounterEffect(EffectKind.ScoutAround, radius, radius);
        public static EncounterEffect ScoutNearest(CellContent target) => new EncounterEffect(EffectKind.ScoutNearest, 1, 1, target);
    }
}
