using System.Collections.Generic;
using System.Linq;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Skills;

namespace Betaknight.Core
{
    /// <summary>Ein möglicher Gegner eines Kartenfeldes mit seiner lesbaren Platine (eine Zeile pro Komponente).</summary>
    public sealed class EnemyBoardPreview
    {
        public string Name { get; }
        public IReadOnlyList<string> Lines { get; }

        public EnemyBoardPreview(string name, IReadOnlyList<string> lines)
        {
            Name = name;
            Lines = lines ?? new List<string>();
        }
    }

    /// <summary>
    /// Hilfen für die Platinen-Ansicht (A-19): beste Lage für einen Skill aus der Sammlung (Kurzweg im Build-Fenster) und
    /// die Gegner-Platinen, die auf einem Kartenfeld warten können (Hover auf der Karte). Rechnet nur, ändert nichts.
    /// </summary>
    public sealed partial class OverworldSession
    {
        private static readonly EnemyCatalog PreviewEnemies = EnemyCatalog.CreateDefault();
        private static readonly Dictionary<string, IReadOnlyList<string>> EnemyLinesCache = new Dictionary<string, IReadOnlyList<string>>();

        /// <summary>
        /// Beste freie Lage für ein Exemplar: zuerst an einem Relais, das gross genug ist, es zu versorgen (Kern-Nähe bevorzugt),
        /// dann am Kern, dann die erste freie Lage. False, wenn nirgends Platz ist.
        /// </summary>
        public bool BestSpotFor(SkillInstance skill, out Cell origin, out bool rotated)
        {
            origin = default;
            rotated = false;
            if (skill == null) return false;
            ComponentSlot own = Board.ComponentOf(skill);
            Shape shape = Board.ShapeOfSkill(skill.SkillId);

            bool found = false;
            int bestScore = int.MinValue;
            foreach (bool turn in shape.IsSquare ? new[] { false } : new[] { false, true })
            {
                Shape s = shape.Turned(turn);
                for (int y = 0; y < Board.Height; y++)
                    for (int x = 0; x < Board.Width; x++)
                    {
                        var rect = new CellRect(new Cell(x, y), s);
                        if (!rect.FitsIn(Board.Width, Board.Height) || !Board.IsFree(rect, own)) continue;
                        int score = 0;
                        if (Board.Relays.Any(r => r.Rect.Touches(rect) && shape.Cells <= RelayMaxCells(r))) score += 100;
                        else if (Board.Relays.Any(r => r.Rect.Touches(rect))) score += 10;
                        if (rect.Touches(Board.CoreRect)) score += 20;
                        if (turn) score -= 1;
                        if (score <= bestScore) continue;
                        bestScore = score;
                        origin = rect.Origin;
                        rotated = turn;
                        found = true;
                    }
            }
            return found;
        }

        /// <summary>
        /// Gegner, die auf diesem Feld warten können (gleiche Auswahl wie beim Würfeln im Kampf: Stufe, Boss, Elite), jeweils mit
        /// ihrer Platine. Leer für Felder ohne Kampf. Gewürfelt wird erst beim Betreten; das hier ist die Liste der Kandidaten.
        /// </summary>
        public List<EnemyBoardPreview> EnemyBoardsAt(HexCoord coord)
        {
            var result = new List<EnemyBoardPreview>();
            if (!Map.TryGetCell(coord, out HexCell cell)) return result;

            bool boss = cell.Content == CellContent.Boss;
            bool elite = cell.Content == CellContent.Elite;
            int tier;
            if (cell.Content == CellContent.Enemy || boss) tier = TierAt(coord);
            else if (elite) tier = TierAt(coord) + Progression.EliteTierBonus;
            else if (cell.Content == CellContent.GoldMine && cell.IsUnderAttack) tier = TierAt(coord) + MineRaidTierBonus;
            else return result;

            // Wie EnemyCatalog.Pick: passende Stufe, sonst die schwächeren, sonst alle derselben Art.
            List<EnemyDefinition> pool = PreviewEnemies.All.Where(e => e.IsBoss == boss && e.Weight > 0 && e.FitsTier(tier)).ToList();
            if (pool.Count == 0) pool = PreviewEnemies.All.Where(e => e.IsBoss == boss && e.Weight > 0 && e.MinTier <= tier).ToList();
            if (pool.Count == 0) pool = PreviewEnemies.All.Where(e => e.IsBoss == boss).ToList();

            foreach (EnemyDefinition enemy in pool)
            {
                string name = elite ? ArenaTexts.EliteName(enemy.Name) : enemy.Name;
                result.Add(new EnemyBoardPreview(name, EnemyLines(enemy)));
            }
            return result;
        }

        /// <summary>Platinen-Zeilen eines Gegners (bei Gruppen: «Name: Zeile» je Kämpfer), einmal gebaut und gemerkt.</summary>
        private static IReadOnlyList<string> EnemyLines(EnemyDefinition enemy)
        {
            lock (EnemyLinesCache)
            {
                if (EnemyLinesCache.TryGetValue(enemy.Id, out IReadOnlyList<string> cached)) return cached;
                var lines = new List<string>();
                List<CombatantSetup> fighters = enemy.Create();
                foreach (CombatantSetup fighter in fighters)
                foreach (string line in EnemyBoard.Lines(fighter.Board ?? LogicBoard.FallbackOnly))
                    lines.Add(fighters.Count > 1 ? $"{fighter.Name}: {line}" : line);
                EnemyLinesCache[enemy.Id] = lines;
                return lines;
            }
        }
    }
}
