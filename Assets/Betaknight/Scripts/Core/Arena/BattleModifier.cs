namespace Betaknight.Core.Arena
{
    /// <summary>
    /// Passive Regel auf einem Kämpfer für den ganzen Kampf: Set-Boni, später auch Relikte, Flüche oder
    /// Gegner-Eigenschaften. Alle Hooks sind optional. Ereignisse kommen einen Tick verzögert an,
    /// damit Reaktionen keine Endlosschleife im selben Tick bilden können.
    /// </summary>
    public abstract class BattleModifier
    {
        public virtual string Name => GetType().Name;

        public virtual void OnBattleStart(Battle battle, Combatant owner) { }

        /// <summary>Nach dem Ende, bevor das Ergebnis gebaut wird (z. B. Gold pro besiegtem Gegner). Kein Schaden mehr.</summary>
        public virtual void OnBattleEnd(Battle battle, Combatant owner, BattleOutcome outcome) { }

        /// <summary>Ein Ereignis aus dem vorherigen Tick.</summary>
        public virtual void OnEvent(Battle battle, Combatant owner, BattleEvent e) { }

        public virtual int StatBonus(Battle battle, Combatant owner, StatKind kind) => 0;

        /// <summary>Vor der Abwehrberechnung; owner ist Angreifer oder Ziel.</summary>
        public virtual void ModifyHit(Battle battle, Combatant owner, HitInfo hit) { }

        /// <summary>Nach der Berechnung, bevor der Schaden abgezogen wird (z. B. Schaden begrenzen).</summary>
        public virtual void ModifyFinalDamage(Battle battle, Combatant owner, HitInfo hit) { }

        /// <summary>Wenn der Kämpfer einen Skill beginnt (z. B. Cooldown anpassen).</summary>
        public virtual void OnActionStarted(Battle battle, Combatant owner, SkillDefinition skill, int rowIndex) { }
    }
}
