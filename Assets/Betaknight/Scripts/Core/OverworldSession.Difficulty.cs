using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>
    /// Schwierigkeits-Bonus und Versorgung für die Anzeige: Stufe und Grenze eines Relais, Skill-Werte inklusive Bonus und welche Relais ein
    /// Teil, Modul oder Skill leichter macht. Die Regeln selbst stehen in <see cref="DifficultyBonusConfig"/> und
    /// <see cref="ReliefCatalog"/>.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public DifficultyBonusConfig DifficultyBonus => DifficultyBonusConfig.Default;
        public ReliefCatalog Reliefs => ReliefCatalog.Default;

        /// <summary>Grundschwierigkeit eines Relais (0–3); mit Modul «Umkehren» die eigene Stufe der Umkehrung.</summary>
        public int RelayDifficulty(RelayChip relay)
        {
            if (relay?.Rune == null) return 0;
            return relay.Rune.DifficultyFor(relay.Modules.Any(m => m.ModuleId == ModuleIds.Invert));
        }

        /// <summary>Grössen-Grenze eines Relais in Zellen (Schwierigkeit 0 → 1, 1 → 2, 2 → 4, 3 → 6).</summary>
        public int RelayMaxCells(RelayChip relay) => DifficultyBonus.MaxCells(RelayDifficulty(relay));

        /// <summary>Relais, die diese Komponente berühren und gross genug sind, sie zu versorgen.</summary>
        public List<RelayChip> PoweringRelays(ComponentSlot component) =>
            Board.RelaysTouching(component).Where(r => component.Cells <= RelayMaxCells(r)).ToList();

        /// <summary>Wird die Komponente versorgt?</summary>
        public bool IsPowered(ComponentSlot component) => component != null && PoweringRelays(component).Count > 0;

        /// <summary>«not powered (too large)»: Relais berühren sie, aber keines ist schwer genug.</summary>
        public bool IsTooLarge(ComponentSlot component) =>
            component != null && !IsPowered(component) && Board.RelaysTouching(component).Count > 0;

        /// <summary>Höchste Schwierigkeit der versorgenden Relais (sie bestimmt den Bonus der besten Ausführung).</summary>
        public int ComponentDifficulty(ComponentSlot component)
        {
            int d = 0;
            foreach (RelayChip r in PoweringRelays(component)) d = System.Math.Max(d, RelayDifficulty(r));
            return d;
        }

        /// <summary>
        /// Skill-Werte einer Komponente so, wie sie im Kampf wirkt: Wachstum, Kern-Bonus und der Schwierigkeits-Bonus des
        /// schwersten versorgenden Relais.
        /// </summary>
        public SkillInfo DescribeComponentSkill(ComponentSlot component, SkillUserStats stats = null)
        {
            SkillInstance skill = component?.Skill;
            if (skill == null) return null;
            SkillDefinition grown = GrownSkill(skill.SkillId, skill.Growth);
            if (grown == null) return null;
            if (Board.TouchesCore(component)) grown = grown.WithBonus(Board.Config.CoreBonusPercent);
            return SkillInfo.Create(DifficultyBonus.Apply(grown, ComponentDifficulty(component)), stats ?? SkillUserStats());
        }

        /// <summary>Erleichterer an einem Teil, Modul oder Skill (Id).</summary>
        public List<ReliefDefinition> ReliefsOf(string carrierId) => Reliefs.ForCarrier(carrierId);

        /// <summary>«Erleichtert: «Gegner betäubt» ◆◆ (…)» oder leer, wenn der Träger nichts erleichtert.</summary>
        public string EasesText(string carrierId) => Reliefs.EasesText(carrierId, RuneCatalog);
    }
}
