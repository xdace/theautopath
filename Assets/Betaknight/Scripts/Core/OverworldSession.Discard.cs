using Betaknight.Core.Circuit;
using Betaknight.Core.Modules;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>
    /// Verwerfen im Build-Fenster: Skills, Module, Runen und Chips endgültig loswerden (Ausrüstung siehe <see cref="DiscardItem"/>).
    /// Nur ausserhalb von Kampf und offenen Entscheidungen. Was eingesetzt ist, wird vorher abgenommen; Module an einem
    /// verworfenen Skill oder Relais werden frei.
    /// </summary>
    public sealed partial class OverworldSession
    {
        /// <summary>Verwirft ein Skill-Exemplar; liegt es auf der Platine, wird es vorher abgenommen.</summary>
        public bool DiscardSkill(int instanceId)
        {
            if (!CanChangeLoadout) return false;
            SkillInstance skill = Skills.Get(instanceId);
            if (skill == null) return false;
            ComponentSlot slot = Board.ComponentOf(skill);
            if (slot != null) Board.Remove(slot);
            Modules.Release(skill);
            return Skills.Remove(skill);
        }

        /// <summary>Verwirft ein Modul (frei oder eingesetzt).</summary>
        public bool DiscardModule(int moduleId) => CanChangeLoadout && Modules.Remove(Modules.Get(moduleId));

        /// <summary>Verwirft das Relais einer Platinen-Zeile samt Rune; seine Module werden frei.</summary>
        public bool DiscardRelay(int relay)
        {
            if (!CanChangeLoadout || relay < 0 || relay >= Board.Relays.Count) return false;
            RelayChip removed = Board.Relays[relay];
            Modules.Release(removed);
            return Board.RemoveRelay(removed);
        }

        /// <summary>Verwirft einen Chip aus dem Chip-Inventar.</summary>
        public bool DiscardChip(int inventoryIndex)
        {
            if (!CanEditChips || inventoryIndex < 0 || inventoryIndex >= _chipInventory.Count) return false;
            _chipInventory.RemoveAt(inventoryIndex);
            return true;
        }

        /// <summary>Verwirft einen Chip, der auf der Platine liegt.</summary>
        public bool DiscardBoardChip(int chipIndex)
        {
            if (!CanEditChips || chipIndex < 0 || chipIndex >= Board.Chips.Count) return false;
            return Board.RemoveChip(Board.Chips[chipIndex]);
        }
    }
}
