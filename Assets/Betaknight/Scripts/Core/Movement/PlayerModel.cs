using System;
using Betaknight.Core.Hex;

namespace Betaknight.Core.Movement
{
    /// <summary>
    /// Zustand des Betaknight auf der Oberwelt. Ausrüstung und Skills kommen in späteren Meilensteinen dazu.
    /// </summary>
    public sealed class PlayerModel
    {
        public HexCoord Position { get; private set; }

        /// <summary>Parameter: (von, nach).</summary>
        public event Action<HexCoord, HexCoord> Moved;

        public PlayerModel(HexCoord startPosition)
        {
            Position = startPosition;
        }

        internal void MoveTo(HexCoord target)
        {
            HexCoord from = Position;
            Position = target;
            Moved?.Invoke(from, target);
        }
    }
}
