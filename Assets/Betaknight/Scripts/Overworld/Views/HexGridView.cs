using System.Collections.Generic;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Overworld.Config;
using UnityEngine;

namespace Betaknight.Overworld.Views
{
    /// <summary>
    /// Baut für jedes Feld der Karte eine <see cref="HexCellView"/> und hält sie über
    /// <see cref="HexMap.CellChanged"/> automatisch aktuell.
    /// </summary>
    public sealed class HexGridView : MonoBehaviour
    {
        private readonly Dictionary<HexCoord, HexCellView> _views = new Dictionary<HexCoord, HexCellView>();
        private readonly List<HexCoord> _highlighted = new List<HexCoord>();

        private HexMap _map;
        private HexLayout _layout;

        public HexLayout Layout => _layout;

        public void Initialize(HexMap map, HexLayout layout, OverworldSettings settings)
        {
            Unsubscribe();
            Clear();

            _map = map;
            _layout = layout;

            foreach (HexCell cell in map.Cells)
            {
                HexCellView view = HexCellView.Create(transform, cell, ToWorld(cell.Coord), settings);
                _views.Add(cell.Coord, view);
            }

            _map.CellChanged += OnCellChanged;
        }

        public Vector3 ToWorld(HexCoord coord)
        {
            HexPoint p = _layout.ToWorld(coord);
            return new Vector3(p.X, p.Y, 0f);
        }

        public HexCoord FromWorld(Vector3 world) => _layout.FromWorld(world.x, world.y);

        public void ShowRoute(IReadOnlyList<HexCoord> route)
        {
            ClearHighlight();
            if (route == null) return;

            foreach (HexCoord coord in route)
            {
                if (_views.TryGetValue(coord, out HexCellView view))
                {
                    view.SetHighlight(CellHighlight.Route);
                    _highlighted.Add(coord);
                }
            }
        }

        public void ShowInvalid(HexCoord coord)
        {
            ClearHighlight();
            if (_views.TryGetValue(coord, out HexCellView view))
            {
                view.SetHighlight(CellHighlight.Invalid);
                _highlighted.Add(coord);
            }
        }

        public void ClearHighlight()
        {
            foreach (HexCoord coord in _highlighted)
            {
                if (_views.TryGetValue(coord, out HexCellView view)) view.SetHighlight(CellHighlight.None);
            }
            _highlighted.Clear();
        }

        private void OnCellChanged(HexCell cell)
        {
            if (_views.TryGetValue(cell.Coord, out HexCellView view)) view.Refresh();
        }

        private void Clear()
        {
            foreach (HexCellView view in _views.Values)
            {
                if (view != null) Destroy(view.gameObject);
            }
            _views.Clear();
            _highlighted.Clear();
        }

        private void Unsubscribe()
        {
            if (_map != null) _map.CellChanged -= OnCellChanged;
        }

        private void OnDestroy() => Unsubscribe();
    }
}
