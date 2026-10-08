using System;
using System.Collections.Generic;
using Betaknight.Core.Arena;

namespace Betaknight.Core.Circuit
{
    /// <summary>Kante einer Zelle. Drehen (90° im Uhrzeigersinn) schiebt jede Seite eine Stelle weiter.</summary>
    public enum Edge
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    public static class Edges
    {
        public static IReadOnlyList<Edge> All { get; } = new[] { Edge.Up, Edge.Right, Edge.Down, Edge.Left };

        public static Edge Opposite(this Edge side) => (Edge)(((int)side + 2) % 4);

        /// <summary>Im Uhrzeigersinn um <paramref name="quarterTurns"/> Viertel gedreht.</summary>
        public static Edge Turn(this Edge side, int quarterTurns) => (Edge)((((int)side + quarterTurns) % 4 + 4) % 4);

        /// <summary>Die Nachbarzelle auf dieser Seite.</summary>
        public static Cell Step(this Cell cell, Edge side)
        {
            switch (side)
            {
                case Edge.Up: return new Cell(cell.X, cell.Y - 1);
                case Edge.Right: return new Cell(cell.X + 1, cell.Y);
                case Edge.Down: return new Cell(cell.X, cell.Y + 1);
                default: return new Cell(cell.X - 1, cell.Y);
            }
        }
    }

    /// <summary>
    /// Ein Pin an der Form eines Skills (A-20), als Daten: Seite und Stelle entlang dieser Kante (in der ungedrehten Form;
    /// bei Oben/Unten die Spalte, bei Links/Rechts die Zeile). Ein typisierter Pin («Shock pin») verlangt eine Skill-Art:
    /// liegt dort ein passender Nachbar, bekommt die Komponente den Pin-Bonus (wie Sterne in Backpack Battles).
    /// </summary>
    public readonly struct PinSpec
    {
        public readonly Edge Side;
        public readonly int Offset;
        public readonly SkillKind Kind;

        public PinSpec(Edge side, int offset = 0, SkillKind kind = SkillKind.None)
        {
            Side = side;
            Offset = Math.Max(0, offset);
            Kind = kind;
        }

        public bool IsTyped => Kind != SkillKind.None;
    }

    /// <summary>Ein Pin auf der Platine: Zelle der Komponente, nach aussen zeigende Seite und verlangte Art.</summary>
    public readonly struct PlacedPin
    {
        public readonly Cell Cell;
        public readonly Edge Side;
        public readonly SkillKind Kind;

        public PlacedPin(Cell cell, Edge side, SkillKind kind)
        {
            Cell = cell;
            Side = side;
            Kind = kind;
        }

        /// <summary>Die Zelle vor dem Pin (dort muss der Partner liegen).</summary>
        public Cell Facing => Cell.Step(Side);

        public bool IsTyped => Kind != SkillKind.None;

        public override string ToString() => $"{Cell} {Side}{(IsTyped ? " " + Kind : "")}";
    }

    /// <summary>Pin-Regeln als Daten.</summary>
    public sealed class PinConfig
    {
        /// <summary>Wirkungsbonus je typisiertem Pin mit passendem Nachbarn.</summary>
        public int TypedPinBonusPercent { get; set; } = 15;

        public static PinConfig Default { get; } = new PinConfig();
    }

    /// <summary>
    /// Pins je Skill als Daten (A-20). Berühren sich zwei Pins (Zelle an Zelle, Seiten gegenüber), sind die Komponenten
    /// verbunden: feuert die eine, schickt sie einen Puls zur anderen. Leiterbahnen verbinden entfernte Pins.
    /// </summary>
    public sealed class PinCatalog
    {
        private readonly Dictionary<string, IReadOnlyList<PinSpec>> _bySkill = new Dictionary<string, IReadOnlyList<PinSpec>>();

        public PinConfig Config { get; }

        public PinCatalog(PinConfig config = null) => Config = config ?? PinConfig.Default;

        public void Register(string skillId, params PinSpec[] pins) => _bySkill[skillId] = pins ?? Array.Empty<PinSpec>();

        public IReadOnlyList<PinSpec> For(string skillId) =>
            skillId != null && _bySkill.TryGetValue(skillId, out IReadOnlyList<PinSpec> pins) ? pins : Array.Empty<PinSpec>();

        /// <summary>Pins eines Skills an seiner Lage auf der Platine (Drehung = 90° im Uhrzeigersinn).</summary>
        public List<PlacedPin> Place(string skillId, Shape baseShape, Cell origin, bool rotated)
        {
            var result = new List<PlacedPin>();
            foreach (PinSpec pin in For(skillId))
            {
                if (!TryEdgeCell(pin, baseShape, out Cell local)) continue;
                Edge side = pin.Side;
                if (rotated)
                {
                    local = new Cell(baseShape.Height - 1 - local.Y, local.X);
                    side = side.Turn(1);
                }
                result.Add(new PlacedPin(new Cell(origin.X + local.X, origin.Y + local.Y), side, pin.Kind));
            }
            return result;
        }

        private static bool TryEdgeCell(PinSpec pin, Shape shape, out Cell cell)
        {
            int along = pin.Side == Edge.Up || pin.Side == Edge.Down ? shape.Width : shape.Height;
            cell = default;
            if (pin.Offset >= along) return false;
            switch (pin.Side)
            {
                case Edge.Up: cell = new Cell(pin.Offset, 0); break;
                case Edge.Down: cell = new Cell(pin.Offset, shape.Height - 1); break;
                case Edge.Left: cell = new Cell(0, pin.Offset); break;
                default: cell = new Cell(shape.Width - 1, pin.Offset); break;
            }
            return true;
        }

        private static PinSpec P(Edge side, int offset = 0, SkillKind kind = SkillKind.None) => new PinSpec(side, offset, kind);

        /// <summary>Pins aller Skills (ungedrehte Form). Startwerte, bewusst klein gehalten.</summary>
        public static PinCatalog CreateDefault()
        {
            var c = new PinCatalog();
            // 1×1
            c.Register(SkillIds.ShockStab, P(Edge.Left), P(Edge.Right, 0, SkillKind.Shock));
            c.Register(SkillIds.Thrusters, P(Edge.Left), P(Edge.Right, 0, SkillKind.Movement));
            c.Register(SkillIds.ChargeCoil, P(Edge.Up), P(Edge.Down, 0, SkillKind.Shield));
            c.Register(SkillIds.LightningLance, P(Edge.Left), P(Edge.Right, 0, SkillKind.Shock));
            // 1×2 (hoch)
            c.Register(SkillIds.Ignite, P(Edge.Up), P(Edge.Down), P(Edge.Right, 1, SkillKind.Fire));
            c.Register(SkillIds.Inferno, P(Edge.Up), P(Edge.Down), P(Edge.Right, 1, SkillKind.Fire));
            c.Register(SkillIds.ShieldBash, P(Edge.Up), P(Edge.Down), P(Edge.Left, 0, SkillKind.Shield));
            c.Register(SkillIds.ScrapRam, P(Edge.Up), P(Edge.Down), P(Edge.Left, 0, SkillKind.Shield));
            c.Register(SkillIds.Coolant, P(Edge.Up), P(Edge.Down, 0, SkillKind.Fire));
            c.Register(SkillIds.ShieldWall, P(Edge.Up), P(Edge.Right, 0, SkillKind.Shield));
            c.Register(SkillIds.Flashbang, P(Edge.Down), P(Edge.Right, 1, SkillKind.Shock));
            c.Register(SkillIds.Anchor, P(Edge.Up), P(Edge.Left, 1, SkillKind.Shield));
            c.Register(SkillIds.NumbingMist, P(Edge.Up), P(Edge.Down), P(Edge.Right, 0, SkillKind.Shock));
            // 2×1 (flach)
            c.Register(SkillIds.CryoGrenade, P(Edge.Left), P(Edge.Right, 0, SkillKind.Shock));
            c.Register(SkillIds.Echo, P(Edge.Left), P(Edge.Right), P(Edge.Up, 1));
            c.Register(SkillIds.Resonance, P(Edge.Left), P(Edge.Right), P(Edge.Up, 1));
            // 2×2
            c.Register(SkillIds.ArmorBreak, P(Edge.Left), P(Edge.Down), P(Edge.Right, 1, SkillKind.Attack));
            c.Register(SkillIds.Drill, P(Edge.Up, 1), P(Edge.Left, 1), P(Edge.Right, 0, SkillKind.Attack));
            c.Register(SkillIds.AcidDrill, P(Edge.Up, 1), P(Edge.Left, 1), P(Edge.Right, 0, SkillKind.Attack));
            c.Register(SkillIds.EmpBash, P(Edge.Left), P(Edge.Right, 0, SkillKind.Shock), P(Edge.Down, 1, SkillKind.Shield));
            c.Register(SkillIds.Repair, P(Edge.Up), P(Edge.Left, 1), P(Edge.Right, 1, SkillKind.Healing));
            // 2×3
            c.Register(SkillIds.RailCannon, P(Edge.Left), P(Edge.Left, 2), P(Edge.Down), P(Edge.Right, 1, SkillKind.Attack));
            return c;
        }
    }
}
