using System.Collections;
using System.Collections.Generic;
using Betaknight.Core;
using Betaknight.Core.Hex;
using Betaknight.Overworld.Input;
using Betaknight.Overworld.UI;
using Betaknight.Overworld.Views;
using UnityEngine;

namespace Betaknight.Overworld.Controllers
{
    /// <summary>
    /// Verbindet Eingabe, Logik und Darstellung:
    /// Mouseover zeigt die geplante Route, Klick führt sie Schritt für Schritt aus.
    /// Jeder Schritt wird erst animiert und dann in der Logik ausgeführt (1 Schritt = 1 Zug).
    /// </summary>
    public sealed class OverworldController : MonoBehaviour
    {
        private OverworldSession _session;
        private HexGridView _grid;
        private PlayerView _player;
        private Camera _camera;

        private HexCoord? _hovered;
        private List<HexCoord> _previewRoute;
        private Coroutine _travel;

        public bool IsTravelling => _travel != null;

        /// <summary>Zusätzliche Sperre von aussen, z. B. Arena oder Tafel-Editor offen.</summary>
        public System.Func<bool> InputBlocked;

        /// <summary>Für HUD und Debugging: das Feld unter dem Mauszeiger.</summary>
        public HexCoord? HoveredCoord => _hovered;

        public void Initialize(OverworldSession session, HexGridView grid, PlayerView player, Camera cam)
        {
            _session = session;
            _grid = grid;
            _player = player;
            _camera = cam;
        }

        private void Update()
        {
            if (_session == null || _camera == null) return;
            if (IsTravelling) return;

            // Ein offenes Fenster (Event, Runenwahl, Shop, Game Over) hat Vorrang vor der Karte.
            if (_session.IsBusy || _session.IsGameOver || (InputBlocked != null && InputBlocked()))
            {
                SetHovered(null);
                return;
            }

            UpdateHover();

            if (PointerInput.PressedThisFrame() && _hovered.HasValue && _previewRoute != null && _previewRoute.Count > 0)
            {
                _travel = StartCoroutine(Travel(new List<HexCoord>(_previewRoute)));
            }
        }

        /// <summary>
        /// Läuft eine Route ab wie nach einem Klick (Animation, Schritt für Schritt über die Session). Für den
        /// Testspieler; false, wenn gerade gelaufen wird oder die Route leer ist.
        /// </summary>
        public bool TravelTo(List<HexCoord> route)
        {
            if (IsTravelling || route == null || route.Count == 0 || _session == null) return false;
            _travel = StartCoroutine(Travel(new List<HexCoord>(route)));
            return true;
        }

        private void UpdateHover()
        {
            if (!PointerInput.TryGetScreenPosition(out Vector2 screen) || OverworldHud.ContainsScreenPoint(screen))
            {
                SetHovered(null);
                return;
            }

            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            HexCoord coord = _grid.FromWorld(world);
            SetHovered(_session.Map.Contains(coord) ? coord : (HexCoord?)null);
        }

        private void SetHovered(HexCoord? coord)
        {
            if (_hovered == coord) return;
            _hovered = coord;

            if (!coord.HasValue || coord.Value == _session.Player.Position)
            {
                _previewRoute = null;
                _grid.ClearHighlight();
                return;
            }

            _previewRoute = _session.PlanRoute(coord.Value);
            if (_previewRoute != null) _grid.ShowRoute(_previewRoute);
            else _grid.ShowInvalid(coord.Value);
        }

        private IEnumerator Travel(List<HexCoord> route)
        {
            _grid.ShowRoute(route);

            // Die ganze Reise kostet einen Zug (Schritte über besuchte Felder zählen nicht einzeln).
            for (int i = 0; i < route.Count; i++)
            {
                HexCoord step = route[i];
                if (!_session.CanStepTo(step)) break;

                yield return _player.AnimateStep(_grid.ToWorld(step));

                StepResult result = _session.TryTravelStep(step, i == route.Count - 1);
                if (result.InterruptsTravel || _session.IsBusy) break;
            }
            _session.FinishTravel();

            // Sicherheitsnetz: Darstellung exakt auf die logische Position setzen.
            _player.SnapTo(_grid.ToWorld(_session.Player.Position));
            _grid.ClearHighlight();
            _hovered = null;
            _previewRoute = null;
            _travel = null;
        }

        private void OnDisable()
        {
            if (_travel != null)
            {
                StopCoroutine(_travel);
                _travel = null;
            }
        }
    }
}
