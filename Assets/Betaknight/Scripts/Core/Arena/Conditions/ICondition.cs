namespace Betaknight.Core.Arena
{
    /// <summary>Laufzeit-Daten einer Zeile im Kampf.</summary>
    public sealed class RowRuntime
    {
        public int Index { get; }

        /// <summary>Tick, in dem die Zeile zuletzt eine Aktion gestartet hat. 0 = noch nie (Kampfbeginn).</summary>
        public int LastFiredTick { get; internal set; }
        public int FireCount { get; internal set; }

        /// <summary>Letzter Tick, in dem die Bedingung erfüllt war (nur für beobachtete Bedingungen wie «Verlängern»).</summary>
        public int LastMetTick { get; internal set; } = int.MinValue / 2;

        /// <summary>War die Bedingung im letzten beobachteten Tick erfüllt? Für Auslöser «wenn erfüllt».</summary>
        public bool WasMet { get; internal set; }

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

    /// <summary>
    /// Bedingung mit Gedächtnis über <see cref="RowRuntime"/>: wird jeden Tick vor den Entscheidungen beobachtet,
    /// damit <see cref="ICondition.IsMet"/> selbst rein bleibt und Protokoll oder Anzeige nichts verändern.
    /// </summary>
    public interface IObservingCondition : ICondition
    {
        void Observe(in ConditionContext context);
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

namespace Betaknight.Core.Arena
{
    /// <summary>Kehrt eine Bedingung um (Grundlage für eine spätere Inverter-Rune). Ziel ist der Standardgegner.</summary>
    public sealed class NotCondition : IObservingCondition
    {
        public ICondition Inner { get; }

        public NotCondition(ICondition inner) => Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));

        public void Observe(in ConditionContext context)
        {
            if (Inner is IObservingCondition observing) observing.Observe(context);
        }

        public bool IsMet(in ConditionContext context, out Combatant target)
        {
            bool met = Inner.IsMet(context, out _);
            target = null;
            return !met;
        }
    }

    /// <summary>Modul «Verlängern»: die Bedingung gilt nach dem letzten Erfülltsein noch eine Weile weiter.</summary>
    public sealed class ExtendedCondition : IObservingCondition
    {
        public ICondition Inner { get; }
        public int Ticks { get; }

        public ExtendedCondition(ICondition inner, int ticks)
        {
            Inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            Ticks = System.Math.Max(0, ticks);
        }

        public void Observe(in ConditionContext context)
        {
            if (Inner is IObservingCondition observing) observing.Observe(context);
            if (Inner.IsMet(context, out _)) context.Row.LastMetTick = context.Tick;
        }

        public bool IsMet(in ConditionContext context, out Combatant target)
        {
            if (Inner.IsMet(context, out target)) return true;
            target = null;
            return context.Row != null && context.Tick - context.Row.LastMetTick <= Ticks;
        }
    }

    public static class ConditionExtensions
    {
        public static ICondition Not(this ICondition condition) => new NotCondition(condition);
    }
}
