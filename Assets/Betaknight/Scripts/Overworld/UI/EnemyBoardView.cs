using System.Collections.Generic;
using System.Linq;
using Betaknight.Core;
using Betaknight.Core.Arena;
using Betaknight.Core.Circuit;
using Betaknight.Core.Combat;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Gegner-Platine als kompaktes Raster, gemeinsam für Arena, Karten-Hover und Bergen: Relais, Komponenten mit Modulen
    /// (goldenes Schild «●n» mit den Namen im Tooltip) und Effekt-Symbolen, Logik-Chips, Pins und Pulsverbindungen.
    /// Mit <see cref="Live"/> (Arena) zeigt sie denselben Zustand wie die Platine des Ritters: Relais leuchten beim Auslösen,
    /// die feuernde Komponente ist hervorgehoben, Pulse laufen, dazu Hitze, Hacks, Rekursions-Tiefe und Verstärkung (A-21).
    /// Ohne Live-Zustand eine ruhige Vorschau. Ein Teil im Fokus (Beute beim Hovern) wird golden umrandet. Gerechnet wird nichts.
    /// </summary>
    public static class EnemyBoardView
    {
        /// <summary>Ein Puls in Echtzeit: Verbindung, Anteil des Wegs (0–1) und Verstärkung in Prozent.</summary>
        public readonly struct LivePulse
        {
            public readonly int Link;
            public readonly float Progress;
            public readonly int Power;

            public LivePulse(int link, float progress, int power)
            {
                Link = link;
                Progress = progress;
                Power = power;
            }
        }

        /// <summary>Live-Zustand eines Kämpfers in der Arena.</summary>
        public sealed class Live
        {
            public BoardWatch Watch;
            public ArenaEffects Fx;
            public int Fighter;
            public FighterView View;

            /// <summary>Leuchtet die Komponente gerade (Echtzeit-Mindestdauer wie beim Ritter)?</summary>
            public System.Func<int, bool> RowLit;

            public List<LivePulse> Pulses = new List<LivePulse>();
        }

        /// <summary>Farben der Beute-Arten (auch für die Karten im Bergen-Fenster).</summary>
        public static readonly Color SkillColour = new Color(0.50f, 0.84f, 1.00f);
        public static readonly Color ModuleColour = new Color(1.00f, 0.84f, 0.37f);
        public static readonly Color ChipColour = new Color(0.49f, 0.86f, 0.44f);
        public static readonly Color RuneColour = new Color(0.69f, 0.55f, 1.00f);

        private static readonly Color FocusColour = new Color(1.00f, 0.84f, 0.37f, 1f);

        private static GUIStyle _micro;

        /// <summary>Sehr kleiner Text für kleine Zellen (Karte, mehrere Gegner).</summary>
        private static GUIStyle Micro
        {
            get
            {
                if (_micro == null) _micro = new GUIStyle(CircuitGrid.Tiny) { fontSize = 9, padding = new RectOffset(1, 1, 0, 0) };
                return _micro;
            }
        }

        /// <summary>Hat die Platine eine Lage (Raster zeichnenbar)? Sonst bleiben nur die Zeilen.</summary>
        public static bool CanDraw(LogicBoard board) =>
            board?.Layout != null && board.Rows.All(r => r.Rect.HasValue) && board.Relays.All(r => r.Rect.HasValue);

        /// <summary>Zellgrösse, mit der die Platine in den Platz passt (höchstens <paramref name="max"/>).</summary>
        public static float CellSize(LogicBoard board, float maxWidth, float maxHeight, float max = 44f, float min = 12f) =>
            board?.Layout == null ? min : CircuitGrid.CellSize(board.Layout.Width, board.Layout.Height, maxWidth, maxHeight, max, min);

        /// <summary>Höhe des Rasters für eine Breite (Karte, Bergen: Platz per GUILayout reservieren).</summary>
        public static float Height(LogicBoard board, float maxWidth, float maxHeight, float max = 44f) =>
            board?.Layout == null ? 0f : board.Layout.Height * CellSize(board, maxWidth, maxHeight, max) + 4f;

        /// <summary>
        /// Zeichnet die Platine oben mittig in <paramref name="area"/>. Gibt das Raster zurück (für Blitze der Arena);
        /// <paramref name="size"/> ist die Zellgrösse.
        /// </summary>
        public static Rect Draw(Rect area, LogicBoard board, out float size, Live live = null, EnemyLoot? focus = null, float max = 44f)
        {
            size = CellSize(board, area.width, area.height, max);
            if (!CanDraw(board)) return new Rect(area.x, area.y, 0f, 0f);
            BoardLayout layout = board.Layout;
            var grid = new Rect(area.x + Mathf.Max(0f, (area.width - layout.Width * size) * 0.5f), area.y + 2f, layout.Width * size, layout.Height * size);
            GUIStyle style = size < 34f ? Micro : CircuitGrid.Tiny;

            CircuitGrid.DrawBackground(grid, size, layout.Width, layout.Height);
            DrawTraces(board, grid, size, live);

            for (int i = 0; i < board.Chips.Count; i++) DrawChip(board, grid, size, i, live, focus, style);
            DrawLinks(board, grid, size, live);

            for (int i = 0; i < board.Relays.Count; i++)
                if (!board.Relays[i].Gate.HasValue) DrawRelay(board, grid, size, i, live, focus, style);
            for (int i = 0; i < board.Rows.Count; i++) DrawRow(board, grid, size, i, live, focus, style);

            DrawPins(board, grid, size);
            if (live != null) DrawPulses(board, grid, size, live);
            return grid;
        }

        public static Rect Draw(Rect area, LogicBoard board, Live live = null, EnemyLoot? focus = null, float max = 44f) =>
            Draw(area, board, out _, live, focus, max);

        // ------------------------------------------------------------------ Teile

        private static void DrawTraces(LogicBoard board, Rect grid, float size, Live live)
        {
            for (int i = 0; i < board.Relays.Count; i++)
            {
                LogicRelay relay = board.Relays[i];
                Rect from = CircuitGrid.RectOf(grid, size, relay.Rect.Value);
                Color trace = live != null && live.Watch.IsRelayLit(i) ? CircuitGrid.RelayLit : CircuitGrid.TraceColor;
                foreach (int row in relay.Powered)
                    if (row >= 0 && row < board.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, board.Rows[row].Rect.Value), trace);
                foreach (int row in relay.TooLarge)
                    if (row >= 0 && row < board.Rows.Count) CircuitGrid.DrawTrace(from, CircuitGrid.RectOf(grid, size, board.Rows[row].Rect.Value), CircuitGrid.TooLargeBorder);
                foreach (int input in relay.Inputs)
                    if (input >= 0 && input < board.Relays.Count && board.Relays[input].Rect.HasValue)
                        CircuitGrid.DrawTrace(CircuitGrid.RectOf(grid, size, board.Relays[input].Rect.Value), from,
                            live != null && live.Watch.IsRelayOn(input) ? CircuitGrid.GateOpen : CircuitGrid.GateBorder);
            }
            if (board.Layout.Core.HasValue)
            {
                Rect core = CircuitGrid.RectOf(grid, size, board.Layout.Core.Value.X, board.Layout.Core.Value.Y);
                foreach (LogicRow row in board.Rows)
                    if (row.TouchesCore) CircuitGrid.DrawTrace(core, CircuitGrid.RectOf(grid, size, row.Rect.Value), CircuitGrid.CoreColor);
                CircuitGrid.DrawCore(core, UiTexts.Build.CoreName, UiTexts.Build.CoreTip(CircuitConfig.Default.CoreBonusPercent), size < 34f ? Micro : CircuitGrid.Tiny);
            }
        }

        private static void DrawRelay(LogicBoard board, Rect grid, float size, int index, Live live, EnemyLoot? focus, GUIStyle style)
        {
            LogicRelay relay = board.Relays[index];
            Rect rect = CircuitGrid.RectOf(grid, size, relay.Rect.Value);
            bool lit = live != null && live.Watch.IsRelayLit(index);
            Color fill = lit ? CircuitGrid.RelayLit : CircuitGrid.RelayColor;
            string label = lit ? $"<color=#0b1a1c><b>{relay.Label}</b></color>" : relay.Label;
            string text = size >= 34f ? $"{RuneText.Difficulty(relay.Difficulty)}\n{label}" : label;
            if (live != null && size >= 34f) text += $"\n<color={(lit ? "#0b1a1c" : "#9aa4b2")}>×{live.Watch.RelayCount(index)}</color>";
            CircuitGrid.DrawChip(rect, fill, lit ? Color.white : CircuitGrid.RelayLit, lit ? 2f : 1f, text, RelayTooltip(board, index, live), style);
            if (live != null)
            {
                float lamp = Mathf.Clamp(size * 0.12f, 3f, 7f);
                var lampRect = new Rect(rect.xMax - lamp - 2f, rect.y + 2f, lamp, lamp);
                UiTheme.Fill(lampRect, live.Watch.IsRelayOn(index) ? CircuitGrid.GateOpen : new Color(0f, 0f, 0f, 0.55f));
                UiTheme.Outline(lampRect, CircuitGrid.GateOpen, 1f);
                DrawRelayHack(board, rect, index, live);
            }
            if (focus.HasValue && focus.Value.Kind == EnemyLootKind.Rune && relay.RuneId == focus.Value.Id) UiTheme.Outline(rect, FocusColour, 3f);
        }

        private static void DrawRow(LogicBoard board, Rect grid, float size, int index, Live live, EnemyLoot? focus, GUIStyle style)
        {
            LogicRow row = board.Rows[index];
            Rect rect = CircuitGrid.RectOf(grid, size, row.Rect.Value);
            RowDisplay state = StateOf(row, index, live);
            bool lit = live != null && (state == RowDisplay.Firing || (live.RowLit != null && live.RowLit(index)));
            Color fill = lit ? new Color(0.45f, 0.38f, 0.12f) : state == RowDisplay.Queued ? new Color(0.13f, 0.27f, 0.36f)
                : state == RowDisplay.Frozen ? new Color(0.20f, 0.30f, 0.42f) : new Color(0.24f, 0.15f, 0.16f);
            Color border = lit ? UiTheme.Accent : ArenaWindow.StateColor(state);

            string name = row.Skill?.Name ?? "—";
            string text = $"<b>{name}</b>";
            if (row.Skill != null && row.Skill.Modules.Count > 0 && size >= 34f)
                text += $"\n<color=#ffd75e>{string.Join(" ", row.Skill.Modules.Select(m => $"+{m}"))}</color>";
            if (live != null && size >= 34f && state != RowDisplay.Idle)
            {
                string stateText = state == RowDisplay.Frozen ? $"{UiTexts.Arena.StateFrozen} {RowStateText.Seconds(live.Watch.FrozenLeft(index))}" : ArenaWindow.StateName(state);
                text += $"\n<color={UiTheme.Hex(ArenaWindow.StateColor(state))}>{stateText}</color>";
            }
            if (lit) text = $"<color=#ffd75e>{text}</color>";
            CircuitGrid.DrawChip(rect, fill, border, lit ? 3f : 1f, text, RowTooltip(board, index, state, live), style);

            // A-21: Effekt-Symbole oben rechts, Module als goldenes Schild unten links.
            CircuitGrid.DrawEffectBadges(rect, EffectText.Of(row), size);
            DrawModuleBadge(row, rect, size);
            if (live != null) DrawRowEffects(board, rect, index, state, live);

            if (focus.HasValue && Matches(row, focus.Value)) UiTheme.Outline(rect, FocusColour, 3f);
        }

        /// <summary>«●2» unten links, Tooltip mit den Modulnamen.</summary>
        private static void DrawModuleBadge(LogicRow row, Rect rect, float size)
        {
            if (row.Skill == null || row.Skill.Modules.Count == 0) return;
            float h = Mathf.Clamp(size * 0.3f, 11f, 16f);
            var tag = new Rect(rect.x + 2f, rect.yMax - h - 2f, Mathf.Min(rect.width - 4f, h * 2.2f), h);
            UiTheme.Fill(tag, new Color(0f, 0f, 0f, 0.75f));
            UiTheme.Outline(tag, ModuleColour, 1f);
            GUI.Label(tag, new GUIContent($"<b><color=#ffd75e>●{row.Skill.Modules.Count}</color></b>",
                UiTexts.Arena.EnemyModules(string.Join(", ", row.Skill.Modules))), Micro);
        }

        /// <summary>A-21: Hitze, Rekursions-Tiefe, Verstärkung und Hijack an einer Komponente des Gegners.</summary>
        private static void DrawRowEffects(LogicBoard board, Rect rect, int row, RowDisplay state, Live live)
        {
            if (live.Fx == null) return;
            int max = board.EffectConfig.HeatSkipAt;
            int heat = live.Fx.Heat(live.Fighter, row);
            CircuitGrid.DrawHeatBar(rect, heat, max, false, UiTexts.Effects.HeatTip(heat, max));
            if (state == RowDisplay.Firing)
            {
                int depth = live.Fx.Depth(live.Fighter, row);
                int power = live.Fx.Power(live.Fighter, row);
                CircuitGrid.DrawDepth(rect, depth, UiTexts.Effects.DepthTip(depth, depth * board.EffectConfig.RecursionPowerPercentPerDepth));
                if (power > 0)
                {
                    var tag = new Rect(rect.x + (depth > 0 ? 40f : 8f), rect.y + 2f, 40f, 16f);
                    Color c = EffectText.ColourOf(CircuitEffectIds.Amplifier);
                    UiTheme.Fill(tag, new Color(0f, 0f, 0f, 0.75f));
                    UiTheme.Outline(tag, c, 1f);
                    GUI.Label(tag, new GUIContent($"<b><color={UiTheme.Hex(c)}>{UiTexts.Effects.Power(power)}</color></b>", UiTexts.Effects.PowerTip(power)), CircuitGrid.Tiny);
                }
            }
            if (live.Fx.IsHijacked(live.Fighter, row))
                CircuitGrid.DrawHack(rect, CircuitEffectIds.Hijack, UiTexts.Effects.HijackMark, UiTexts.Effects.HijackedTip(ComponentName(board, row)));
        }

        private static void DrawRelayHack(LogicBoard board, Rect rect, int relay, Live live)
        {
            if (live.Fx == null) return;
            int flipped = live.Fx.FlippedLeft(live.Fighter, relay);
            int jam = live.Fx.JamLeft(live.Fighter, relay);
            if (flipped > 0)
            {
                string left = RowStateText.Seconds(flipped);
                CircuitGrid.DrawHack(rect, CircuitEffectIds.BitFlip, UiTexts.Effects.Flipped(left), UiTexts.Effects.FlippedTip(RelayName(board, relay), left));
            }
            else if (jam > 0)
            {
                CircuitGrid.DrawHack(rect, CircuitEffectIds.Jam, UiTexts.Effects.Jammed(jam), UiTexts.Effects.JammedTip(RelayName(board, relay), jam));
            }
        }

        private static void DrawChip(LogicBoard board, Rect grid, float size, int index, Live live, EnemyLoot? focus, GUIStyle style)
        {
            LogicChip chip = board.Chips[index];
            Rect rect = CircuitGrid.RectOf(grid, size, chip.Rect);
            int relay = chip.RelayIndex;
            bool gate = chip.Definition.IsGate && relay >= 0;
            var look = new CircuitGrid.ChipLook
            {
                ShowState = gate && live != null,
                Open = gate && live != null && (live.Watch.IsRelayOn(relay) || live.Watch.IsRelayLit(relay)),
                Blown = gate && live != null && live.Watch.IsFuseBlown(relay),
                Charge = live?.Watch.CapacitorCharge(index) ?? 0,
                Capacity = chip.Kind == ChipKind.Capacitor && live != null ? board.ChipConfig.CapacitorCapacity : 0,
                Border = gate && live != null && live.Watch.IsRelayLit(relay) ? Color.white : (Color?)null,
            };
            string tip = chip.Kind == ChipKind.Effect && EffectText.TryGet(chip.Definition.EffectId, out CircuitEffectDefinition fx)
                ? EffectText.Tip(fx) : UiTexts.Circuit.ChipTip(chip.Name, chip.Definition.Description);
            CircuitGrid.DrawLogicChip(rect, chip.Definition, chip.Turns, look, tip, style);
            if (focus.HasValue && focus.Value.Kind == EnemyLootKind.Chip && chip.Definition.Id == focus.Value.Id) UiTheme.Outline(rect, FocusColour, 3f);
        }

        private static void DrawLinks(LogicBoard board, Rect grid, float size, Live live)
        {
            if (Event.current.type != EventType.Repaint) return;
            float thickness = Mathf.Clamp(size * 0.05f, 1.5f, 3f);
            for (int i = 0; i < board.Links.Count; i++)
            {
                PulseLink link = board.Links[i];
                bool active = live != null && live.Pulses.Exists(p => p.Link == i);
                Color color = CircuitGrid.LinkColor;
                color.a = active ? 0.95f : 0.4f;
                CircuitGrid.DrawLink(CircuitGrid.LinkPoints(grid, size, link), color, active ? thickness + 1f : thickness);
            }
        }

        private static void DrawPins(LogicBoard board, Rect grid, float size)
        {
            int percent = PinConfig.Default.TypedPinBonusPercent;
            foreach (LogicRow row in board.Rows)
                foreach (PlacedPin pin in row.Pins)
                {
                    bool matched = CircuitGrid.IsPinMatched(board, row, pin);
                    bool linked = CircuitGrid.IsPinLinked(board, pin);
                    string kind = pin.IsTyped ? SkillKinds.DisplayName(pin.Kind) : null;
                    CircuitGrid.DrawPin(grid, size, pin, matched, linked, UiTexts.Circuit.PinTip(kind, matched, linked, percent));
                }
        }

        private static void DrawPulses(LogicBoard board, Rect grid, float size, Live live)
        {
            if (Event.current.type != EventType.Repaint) return;
            foreach (LivePulse p in live.Pulses)
            {
                if (p.Link < 0 || p.Link >= board.Links.Count) continue;
                Vector2 at = CircuitGrid.PointOnLink(CircuitGrid.LinkPoints(grid, size, board.Links[p.Link]), p.Progress);
                CircuitGrid.DrawPulse(at, size, CircuitGrid.PulseColor);
                CircuitGrid.DrawPulseGain(at, size, p.Power);
            }
        }

        // ------------------------------------------------------------------ Zustand und Texte

        private static RowDisplay StateOf(LogicRow row, int index, Live live)
        {
            if (row.IsOrphaned) return RowDisplay.Orphaned;
            if (!row.IsPowered && row.TooLargeFor.Count == 0) return RowDisplay.Unpowered;
            // Zu gross: lädt auf, läuft aber wie jede Komponente, sobald die Ladung reicht.
            RowDisplay rest = row.IsPowered ? RowDisplay.Idle : RowDisplay.TooLarge;
            if (live == null) return rest;
            if (live.View != null && live.View.Alive && live.View.ActionSkill != null && live.View.ActionRow == index) return RowDisplay.Firing;
            if (live.Watch.FrozenLeft(index) > 0) return RowDisplay.Frozen;
            if (live.Watch.IsQueued(index)) return RowDisplay.Queued;
            return rest;
        }

        /// <summary>Gehört die Beute zu diesem Teil? Skill nach Id, Modul nach Namen am Skill.</summary>
        private static bool Matches(LogicRow row, EnemyLoot loot)
        {
            if (row.Skill == null) return false;
            if (loot.Kind == EnemyLootKind.Skill) return row.Skill.Id == loot.Id;
            if (loot.Kind == EnemyLootKind.Module) return row.Skill.Modules.Contains(loot.Name);
            return false;
        }

        private static string RelayName(LogicBoard board, int relay) =>
            relay >= 0 && relay < board.Relays.Count ? ArenaTexts.RelayName(relay, board.Relays[relay].Label) : $"Relay {relay + 1}";

        private static string ComponentName(LogicBoard board, int row) =>
            ArenaTexts.ComponentName(row, row >= 0 && row < board.Rows.Count ? board.Rows[row].Skill?.Name ?? "?" : "?");

        private static string RelayTooltip(LogicBoard board, int index, Live live)
        {
            LogicRelay r = board.Relays[index];
            string powers = r.Powered.Count > 0
                ? UiTexts.Arena.Powers(string.Join(", ", r.Powered.Where(i => i >= 0 && i < board.Rows.Count).Select(i => board.Rows[i].Skill?.Name ?? $"#{i + 1}")))
                : UiTexts.Arena.PowersNothing;
            string head = live != null ? UiTexts.Arena.RelayTip(r.Label, live.Watch.RelayCount(index), powers) : UiTexts.Arena.EnemyRelayTip(r.Label, powers);
            return r.Difficulty > 0 ? $"{head}\n{DifficultyText.Tooltip(r.Difficulty)}" : head;
        }

        private static string RowTooltip(LogicBoard board, int index, RowDisplay state, Live live)
        {
            LogicRow r = board.Rows[index];
            string relays = r.Relays.Count > 0 ? UiTexts.Arena.PoweredBy(string.Join(", ", r.Relays.Select(x => x.Label)))
                : r.TooLargeFor.Count > 0 ? UiTexts.NotPoweredTooLarge : UiTexts.NotPowered;
            string text = UiTexts.Arena.ComponentHead(r.Skill?.Name ?? "—", r.Rect?.Shape.ToString() ?? r.Skill?.Shape.ToString() ?? "?", relays);
            if (r.Skill != null)
            {
                if (r.Skill.Modules.Count > 0) text += $"\n<color=#ffd75e>{UiTexts.Arena.EnemyModules(string.Join(", ", r.Skill.Modules))}</color>";
                if (!string.IsNullOrEmpty(r.Skill.Description)) text += $"\n<size=12>{r.Skill.Description}</size>";
            }
            if (live != null)
            {
                string now;
                switch (state)
                {
                    case RowDisplay.Queued: now = UiTexts.Arena.NowQueued; break;
                    case RowDisplay.Firing: now = UiTexts.Arena.NowFiring; break;
                    case RowDisplay.Frozen: now = UiTexts.Arena.NowFrozen(RowStateText.Seconds(live.Watch.FrozenLeft(index))); break;
                    case RowDisplay.Unpowered: now = UiTexts.Arena.NowUnpowered; break;
                    case RowDisplay.TooLarge: now = UiTexts.Arena.NowTooLarge; break;
                    case RowDisplay.Orphaned: now = UiTexts.Arena.NowOrphaned; break;
                    default: now = UiTexts.Arena.NowIdle; break;
                }
                text += "\n" + UiTexts.Arena.Now(now);
            }
            foreach (string tip in EffectText.Tips(EffectText.Of(r))) text += "\n" + tip;
            if (live?.Fx != null)
            {
                int heat = live.Fx.Heat(live.Fighter, index);
                if (heat > 0) text += "\n" + UiTexts.Effects.HeatTip(heat, board.EffectConfig.HeatSkipAt);
            }
            return text;
        }

        /// <summary>Farbe einer Beute-Art.</summary>
        public static Color KindColour(EnemyLootKind kind)
        {
            switch (kind)
            {
                case EnemyLootKind.Skill: return SkillColour;
                case EnemyLootKind.Module: return ModuleColour;
                case EnemyLootKind.Chip: return ChipColour;
                default: return RuneColour;
            }
        }

        /// <summary>Name einer Beute-Art («Skill», «Module», «Chip», «Rune»).</summary>
        public static string KindName(EnemyLootKind kind)
        {
            switch (kind)
            {
                case EnemyLootKind.Skill: return UiTexts.Salvage.KindSkill;
                case EnemyLootKind.Module: return UiTexts.Salvage.KindModule;
                case EnemyLootKind.Chip: return UiTexts.Salvage.KindChip;
                default: return UiTexts.Salvage.KindRune;
            }
        }

        /// <summary>«[Module] Chain» in der Farbe der Art, für Listen.</summary>
        public static string LootText(EnemyLoot loot) =>
            $"<color={UiTheme.Hex(KindColour(loot.Kind))}>[{KindName(loot.Kind)}]</color> {loot.Name}";
    }
}
