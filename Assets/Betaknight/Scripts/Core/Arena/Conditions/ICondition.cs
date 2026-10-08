namespace Betaknight.Core.Arena
{
    /// <summary>Laufzeit-Daten einer Zeile im Kampf.</summary>
    public sealed class RowRuntime
    {
        public int Index { get; }

        /// <summary>Tick, in dem die Zeile zuletzt eine Aktion gestartet hat. 0 = noch nie (Kampfbeginn).</summary>
        public int LastFiredTick { get; internal set; }
        public int FireCount { get; internal set; }

        public RowRuntime(int index) => Index = index;
    }

    /// <summary>Was eine Bedingung zur Prüfung bekommt.</summary>
    public readonly struct ConditionContext
    {
        public readonly Battle Battle;
        public readonly Combatant Self;
        public readonly RowRuntime Row;

        public ConditionContext(Battle battle, Combatant self, RowRuntime row)
        {
            Battle = battle;
            Self = self;
            Row = row;
        }

        public int Tick => Battle.Tick;
    }

    /// <summary>
    /// Das "Wann" einer Zeile. Bedingungen sind zustandslos: Zähler und Fenster leiten sie aus dem
    /// Kampfprotokoll und <see cref="RowRuntime"/> ab. Dadurch kann dieselbe Instanz in vielen Kämpfen stecken.
    /// Neue Bedingungen sind neue Klassen, registriert in der <see cref="ConditionRegistry"/>.
    /// </summary>
    public interface ICondition
    {
        /// <summary>
        /// Ist die Bedingung jetzt erfüllt? <paramref name="target"/> darf ein bevorzugtes Ziel liefern
        /// (z. B. der Gegner, der auflädt), sonst null.
        /// </summary>
        bool IsMet(in ConditionContext context, out Combatant target);
    }

    /// <summary>[Immer]</summary>
    public sealed class AlwaysCondition : ICondition
    {
        public static AlwaysCondition Instance { get; } = new AlwaysCondition();

        public bool IsMet(in ConditionContext context, out Combatant target)
        {
            target = null;
            return true;
        }
    }
}
