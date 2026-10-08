using System;
using System.Collections.Generic;
using UnityEngine;

namespace Betaknight.Overworld.UI
{
    /// <summary>Was gezogen wird.</summary>
    public enum DragKind
    {
        /// <summary>Skill-Exemplar aus dem Skill-Inventar (A = Instanz-Id).</summary>
        Skill,

        /// <summary>Der Basisangriff (kein Exemplar).</summary>
        BasicAttack,

        /// <summary>Skill einer Tafel-Zeile (A = Zeile).</summary>
        RowSkill,

        /// <summary>Rune einer Tafel-Zeile (A = Zeile).</summary>
        RowRune,

        /// <summary>Ganze Tafel-Zeile zum Umsortieren (A = Zeile).</summary>
        Row,

        /// <summary>Rune aus dem Runen-Inventar (A = Index).</summary>
        Rune,

        /// <summary>Modul-Exemplar (A = Instanz-Id).</summary>
        Module,

        /// <summary>Gegenstand im Item-Raster (A = Zelle).</summary>
        Item,

        /// <summary>Angelegtes Teil an der Figur (A = Platz).</summary>
        Equipped,
    }

    /// <summary>Ein gezogenes Element mit Anzeigetext für das Mitzieh-Bild.</summary>
    public readonly struct DragItem : IEquatable<DragItem>
    {
        public DragKind Kind { get; }
        public int A { get; }
        public string Label { get; }

        public DragItem(DragKind kind, int a, string label)
        {
            Kind = kind;
            A = a;
            Label = label ?? string.Empty;
        }

        public bool Equals(DragItem other) => Kind == other.Kind && A == other.A;
        public override bool Equals(object obj) => obj is DragItem other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ A;
    }

    /// <summary>
    /// Drag &amp; Drop für IMGUI: Quellen und Ziele melden sich mit ihrem Rechteck an, nachdem sie gezeichnet wurden.
    /// Ziehen beginnt nach ein paar Pixeln Mausweg; ein kurzer Klick bleibt ein Klick, Doppelklick und Rechtsklick sind
    /// Kurzwege. Beim Ziehen leuchten gültige Ziele grün, ungültige werden ausgegraut (oder rot, wenn gewünscht).
    /// Drop ausserhalb eines Ziels bricht ab. Alle Aktionen laufen erst am Ende von OnGUI (<see cref="Finish"/>),
    /// damit sich das Layout nicht mitten in einem Ereignis ändert. Logik steckt keine drin: die Aktionen rufen die Session.
    /// </summary>
    public sealed class DragDrop
    {
        private const float StartDistance = 6f;

        private DragItem? _pending;
        private Vector2 _downScreen;
        private readonly List<Action> _deferred = new List<Action>();

        /// <summary>Darf gezogen werden (sonst nur lesen; Klicks zum Auswählen gehen weiter).</summary>
        public bool Enabled { get; set; } = true;

        public DragItem? Dragging { get; private set; }
        public bool IsDragging => Dragging.HasValue;

        /// <summary>
        /// Meldet ein ziehbares Element an. <paramref name="click"/> bei einfachem Klick, <paramref name="shortcut"/> bei
        /// Doppelklick oder Rechtsklick (nur wenn <see cref="Enabled"/>).
        /// </summary>
        public void Source(Rect rect, DragItem item, Action click = null, Action shortcut = null)
        {
            Event e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when rect.Contains(e.mousePosition):
                    if (e.button == 1 || (e.button == 0 && e.clickCount >= 2))
                    {
                        _pending = null;
                        if (Enabled && shortcut != null) Defer(shortcut);
                        e.Use();
                    }
                    else if (e.button == 0)
                    {
                        _pending = item;
                        _downScreen = GUIUtility.GUIToScreenPoint(e.mousePosition);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag when Enabled && !IsDragging && _pending.HasValue && _pending.Value.Equals(item):
                    if (Vector2.Distance(GUIUtility.GUIToScreenPoint(e.mousePosition), _downScreen) >= StartDistance)
                    {
                        Dragging = item;
                        e.Use();
                    }
                    break;

                case EventType.MouseUp when !IsDragging && _pending.HasValue && _pending.Value.Equals(item) && rect.Contains(e.mousePosition):
                    _pending = null;
                    if (click != null) Defer(click);
                    e.Use();
                    break;
            }
        }

        /// <summary>
        /// Meldet ein Ziel an. Beim Ziehen wird es markiert; beim Loslassen darüber läuft <paramref name="drop"/>, wenn
        /// <paramref name="accepts"/> zustimmt. Ziele innerhalb anderer Ziele vorher anmelden (das erste nimmt den Drop).
        /// </summary>
        public void Target(Rect rect, Func<DragItem, bool> accepts, Action<DragItem> drop, bool redWhenInvalid = false)
        {
            if (!IsDragging) return;
            DragItem item = Dragging.Value;
            bool ok = Enabled && accepts(item);
            Event e = Event.current;

            if (e.type == EventType.Repaint)
            {
                bool over = rect.Contains(e.mousePosition);
                if (ok)
                {
                    UiTheme.Fill(rect, new Color(UiTheme.Good.r, UiTheme.Good.g, UiTheme.Good.b, over ? 0.32f : 0.14f));
                    UiTheme.Outline(rect, UiTheme.Good, over ? 3f : 1f);
                }
                else if (redWhenInvalid && over)
                {
                    UiTheme.Fill(rect, new Color(UiTheme.Bad.r, UiTheme.Bad.g, UiTheme.Bad.b, 0.35f));
                    UiTheme.Outline(rect, UiTheme.Bad, 3f);
                }
                else
                {
                    UiTheme.Fill(rect, new Color(0.02f, 0.02f, 0.03f, 0.55f));
                }
            }
            else if (e.type == EventType.MouseUp && rect.Contains(e.mousePosition))
            {
                if (ok) Defer(() => drop(item));
                End();
                e.Use();
            }
        }

        /// <summary>Zeigt an, ob gerade über diesem Rechteck gezogen wird (z. B. für eine Vorschau).</summary>
        public bool IsOver(Rect rect) => IsDragging && rect.Contains(Event.current.mousePosition);

        /// <summary>Bricht das Ziehen ab, z. B. wenn das Fenster schliesst.</summary>
        public void Cancel()
        {
            End();
            _deferred.Clear();
        }

        /// <summary>
        /// Am Ende von OnGUI: Drop ins Leere bricht ab, das Mitzieh-Bild folgt der Maus, gesammelte Aktionen laufen.
        /// </summary>
        public void Finish()
        {
            Event e = Event.current;
            if (e.type == EventType.MouseUp) End();
            if (!Enabled && IsDragging) End();

            if (IsDragging && e.type == EventType.Repaint)
            {
                Vector2 mouse = e.mousePosition;
                var content = new GUIContent(Dragging.Value.Label);
                Vector2 size = UiTheme.CellSelected.CalcSize(content);
                var rect = new Rect(mouse.x + 12f, mouse.y + 8f, Mathf.Min(320f, size.x + 8f), Mathf.Max(26f, size.y));
                GUI.Label(rect, content, UiTheme.CellSelected);
            }
            if (IsDragging && e.type == EventType.MouseDrag) e.Use();

            if (_deferred.Count == 0) return;
            var run = new List<Action>(_deferred);
            _deferred.Clear();
            foreach (Action action in run) action();
        }

        private void End()
        {
            Dragging = null;
            _pending = null;
        }

        private void Defer(Action action) => _deferred.Add(action);
    }
}
