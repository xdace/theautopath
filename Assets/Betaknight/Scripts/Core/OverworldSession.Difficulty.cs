using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Modules;
using Betaknight.Core.Runes;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>
    /// Schwierigkeits-Bonus für die Anzeige: Stufe einer Zeile, Skill-Werte inklusive Bonus und welche Bausteine ein
    /// Teil, Modul oder Skill leichter macht. Die Regeln selbst stehen in <see cref="DifficultyBonusConfig"/> und
    /// <see cref="ReliefCatalog"/>.
    /// </summary>
    public sealed partial class OverworldSession
    {
        public DifficultyBonusConfig DifficultyBonus => DifficultyBonusConfig.Default;
        public ReliefCatalog Reliefs => ReliefCatalog.Default;

        /// <summary>Grundschwierigkeit einer Zeile (0–3); mit Modul «Umkehren» die eigene Stufe der Umkehrung.</summary>
        public int RowDifficulty(RuneSlot row)
        {
            if (row?.Rune == null) return 0;
            return row.Rune.DifficultyFor(row.Modules.Any(m => m.ModuleId == ModuleIds.Invert));
        }

        /// <summary>Skill-Werte einer Zeile so, wie sie im Kampf von dieser Zeile aus wirken (inklusive Schwierigkeits-Bonus).</summary>
        public SkillInfo DescribeRowSkill(RuneSlot row, SkillUserStats stats = null)
        {
            SkillInstance skill = row?.Skill;
            if (skill == null) return null;
            if (skill.IsBasicAttack) return DescribeSkill(skill, stats);
            SkillDefinition grown = GrownSkill(skill.SkillId, skill.Growth);
            if (grown == null) return null;
            return SkillInfo.Create(DifficultyBonus.Apply(grown, RowDifficulty(row)), stats ?? SkillUserStats());
        }

        /// <summary>Erleichterer an einem Teil, Modul oder Skill (Id).</summary>
        public List<ReliefDefinition> ReliefsOf(string carrierId) => Reliefs.ForCarrier(carrierId);

        /// <summary>«Erleichtert: «Gegner betäubt» ◆◆ (…)» oder leer, wenn der Träger nichts erleichtert.</summary>
        public string EasesText(string carrierId) => Reliefs.EasesText(carrierId, RuneCatalog);
    }
}
