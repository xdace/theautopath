using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Gear;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>
    /// Kompakte Stat-Leiste für «Build» und «Inventar»: HP, Waffenschaden, Angriffe pro Sekunde, Rüstung, Ausweichen,
    /// Block, Krit, Präzision, Flächenschaden und aktive Boni. Mit einer Vorschau werden geänderte Werte als
    /// «Rüstung 6 → 9» grün (besser) oder rot (schlechter) gezeigt. Werte kommen aus der Session (wie im Kampf).
    /// </summary>
    public sealed class StatBar
    {
        private BuildStats _now;
        private int _frame = -1;

        /// <summary>Aktuelle Werte, einmal pro Frame berechnet.</summary>
        public BuildStats Now(OverworldSession session)
        {
            if (_frame != Time.frameCount || _now == null)
            {
                _now = session.StatsNow();
                _frame = Time.frameCount;
            }
            return _now;
        }

        /// <summary>Zeichnet die Leiste. <paramref name="preview"/> sind Änderungen (leer = keine Vorschau).</summary>
        public void Draw(OverworldSession session, IReadOnlyList<StatChange> preview, string previewTitle = null)
        {
            BuildStats now = Now(session);
            var changed = new Dictionary<string, StatChange>();
            if (preview != null) foreach (StatChange c in preview) changed[c.Label] = c;

            var parts = new List<string>();
            foreach (StatLine line in now.Lines())
            {
                if (changed.TryGetValue(line.Label, out StatChange c))
                {
                    string color = UiTheme.Hex(c.Sign > 0 ? UiTheme.Good : UiTheme.Bad);
                    parts.Add($"{line.Label} <color={color}><b>{c.Before} → {c.After}</b></color>");
                }
                else
                {
                    parts.Add($"<color={UiTheme.Hex(UiTheme.MutedColor)}>{line.Label}</color> <b>{line.Text}</b>");
                }
            }

            GUILayout.BeginVertical(UiTheme.Section);
            GUILayout.Label(string.Join("   ", parts), UiTheme.Small);

            var bonuses = new List<string>();
            foreach (string b in now.Bonuses)
            {
                bool lost = changed.ContainsKey(b);
                bonuses.Add(lost ? $"<color={UiTheme.Hex(UiTheme.Bad)}><b>{b} entfällt</b></color>" : $"<color={UiTheme.Hex(UiTheme.Accent)}>{b}</color>");
            }
            if (preview != null)
                foreach (StatChange c in preview)
                    if (c.Sign > 0 && c.Before == "–") bonuses.Add($"<color={UiTheme.Hex(UiTheme.Good)}><b>+ {c.Label}</b></color>");
            string bonusText = bonuses.Count > 0 ? string.Join(", ", bonuses) : $"<color={UiTheme.Hex(UiTheme.MutedColor)}>keine aktiven Set-Boni oder Tag-Stufen</color>";
            string title = string.IsNullOrEmpty(previewTitle) ? string.Empty : $"<b>Vorschau {previewTitle}:</b>  ";
            GUILayout.Label($"{title}<color={UiTheme.Hex(UiTheme.MutedColor)}>Boni</color> {bonusText}", UiTheme.Small);
            GUILayout.EndVertical();
        }
    }
}
