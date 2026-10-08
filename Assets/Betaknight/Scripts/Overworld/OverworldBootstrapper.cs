using Betaknight.Core;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Overworld.Config;
using Betaknight.Overworld.Controllers;
using Betaknight.Overworld.UI;
using Betaknight.Overworld.Views;
using UnityEngine;

namespace Betaknight.Overworld
{
    /// <summary>
    /// Einstiegspunkt der Oberwelt. Auf ein leeres GameObject in einer leeren Szene ziehen und Play drücken.
    /// Baut Logik (OverworldSession) und Darstellung auf und verdrahtet beides.
    /// Bewusst ohne Singletons: alle Abhängigkeiten werden hier explizit übergeben.
    /// </summary>
    public sealed class OverworldBootstrapper : MonoBehaviour
    {
        [Tooltip("Optional. Ohne Asset werden die Standardwerte verwendet.")]
        [SerializeField] private OverworldSettings settings;

        [Tooltip("Optional. Ohne Zuweisung wird Camera.main verwendet bzw. eine Kamera erzeugt.")]
        [SerializeField] private Camera targetCamera;

        private GameObject _root;
        private OverworldHud _hud;

        public OverworldSession Session { get; private set; }

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<OverworldSettings>();
                settings.name = "OverworldSettings (Standard)";
            }

            _hud = gameObject.AddComponent<OverworldHud>();
        }

        private void Start() => BuildWorld();

        /// <summary>Erzeugt eine komplett neue Karte. Wird auch vom HUD-Button "Neue Karte" genutzt.</summary>
        public void BuildWorld()
        {
            if (_root != null) Destroy(_root);

            int seed = settings.seed != 0 ? settings.seed : Random.Range(1, int.MaxValue);
            MapGenerationConfig config = settings.ToGenerationConfig(seed);
            Session = OverworldSession.Create(config, settings.sightRadius);

            _root = new GameObject("Overworld");
            var layout = new HexLayout(settings.hexSize);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(_root.transform, false);
            HexGridView grid = gridGo.AddComponent<HexGridView>();
            grid.Initialize(Session.Map, layout, settings, config.Encounters);

            PlayerView player = PlayerView.Create(_root.transform, grid.ToWorld(Session.Player.Position), settings);

            Camera cam = SetupCamera(player.transform);

            OverworldController controller = _root.AddComponent<OverworldController>();
            controller.Initialize(Session, grid, player, cam);

            _hud.Initialize(Session, controller, config.Encounters, BuildWorld);

            Debug.Log($"[Betaknight] Oberwelt erzeugt: {Session.Map.Count} Felder, Seed {seed}.");
        }

        private Camera SetupCamera(Transform followTarget)
        {
            Camera cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 0f, -10f);
            }

            cam.orthographic = true;
            cam.orthographicSize = settings.cameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = settings.backgroundColor;
            if (cam.transform.position.z > -1f)
            {
                Vector3 p = cam.transform.position;
                cam.transform.position = new Vector3(p.x, p.y, -10f);
            }

            CameraFollow2D follow = cam.GetComponent<CameraFollow2D>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow2D>();
            follow.Configure(followTarget, settings.cameraFollowSpeed);

            return cam;
        }
    }
}
