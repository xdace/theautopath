using System;
using Betaknight.Core.Hex;

namespace Betaknight.Core.Autoplay
{
    /// <summary>Wo eine Bot-Aktion in der Darstellung stattfindet (welches Fenster dafür offen sein soll).</summary>
    public enum BotActionKind
    {
        /// <summary>Nichts zu tun (z. B. Game Over); läuft das zu lange, ist es ein Hänger.</summary>
        None,

        /// <summary>Volles Inventar: überzähliges Teil oder überzählige Rune.</summary>
        Overflow,

        /// <summary>Mittleres Event (Lagerfeuer, Schrein, …).</summary>
        Encounter,

        /// <summary>Belohnungsangebot (Runen, Teile, Skills, Module, Tafel-Erweiterung).</summary>
        Offer,

        /// <summary>Kauf oder Verlassen im Shop.</summary>
        Shop,

        /// <summary>Durchs Fluchtportal in den nächsten Akt.</summary>
        Portal,

        /// <summary>Tafel: Skills, Runen, Module, Auslöser (Fenster «Build»).</summary>
        Build,

        /// <summary>Ausrüstung aus dem Inventar anlegen (Fenster «Inventar»).</summary>
        Inventory,

        /// <summary>Ein Schritt auf der Karte.</summary>
        Move,
    }

    /// <summary>
    /// Eine Entscheidung des Testspielers. Ausgeführt wird sie über dieselben Session-Methoden wie die UI;
    /// der Bot selbst rechnet keine Spiellogik.
    /// </summary>
    public sealed class BotAction
    {
        public BotActionKind Kind { get; }

        /// <summary>Kurztext für Log und Bericht, z. B. «Angebot: Modul Auslöser».</summary>
        public string Description { get; }

        /// <summary>Art der Belohnung, falls die Aktion eine bringt (Rune, Teil, Skill, Modul, Tafel-Erweiterung, Gold, Heilung, …).</summary>
        public string RewardKind { get; }

        /// <summary>Bei <see cref="BotActionKind.Move"/>: das Zielfeld des Schritts (die Darstellung animiert ihn selbst).</summary>
        public HexCoord? Step { get; }

        /// <summary>Hat die Aktion einen Auslöser auf ein Ziel gelegt?</summary>
        public bool SetsTrigger { get; }

        private readonly Func<bool> _execute;

        public BotAction(BotActionKind kind, string description, Func<bool> execute, string rewardKind = null,
            HexCoord? step = null, bool setsTrigger = false)
        {
            Kind = kind;
            Description = description ?? string.Empty;
            _execute = execute;
            RewardKind = rewardKind;
            Step = step;
            SetsTrigger = setsTrigger;
        }

        public static BotAction Nothing(string why) => new BotAction(BotActionKind.None, why, null);

        /// <summary>Führt die Aktion aus. False, wenn die Session sie abgelehnt hat.</summary>
        public bool Execute() => _execute != null && _execute();

        public override string ToString() => $"{Kind}: {Description}";
    }
}
