using Betaknight.Core;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
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
        private EncounterWindow _encounterWindow;
        private RuneOfferWindow _runeWindow;
        private ShopWindow _shopWindow;
        private GameOverWindow _gameOverWindow;
        private KitSelectionWindow _kitWindow;
        private ArenaWindow _arenaWindow;
        private BoardEditorWindow _boardWindow;
        private PortalWindow _portalWindow;
        private InventoryWindow _inventoryWindow;
        private InventoryFullWindow _inventoryFullWindow;
        private MapGenerationConfig _config;
        private KnightKit _kit;

        public OverworldSession Session { get; private set; }

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<OverworldSettings>();
                settings.name = "OverworldSettings (Standard)";
            }

            _hud = gameObject.AddComponent<OverworldHud>();
            _encounterWindow = gameObject.AddComponent<EncounterWindow>();
            _runeWindow = gameObject.AddComponent<RuneOfferWindow>();
            _shopWindow = gameObject.AddComponent<ShopWindow>();
            _gameOverWindow = gameObject.AddComponent<GameOverWindow>();
            _kitWindow = gameObject.AddComponent<KitSelectionWindow>();
            _arenaWindow = gameObject.AddComponent<ArenaWindow>();
            _boardWindow = gameObject.AddComponent<BoardEditorWindow>();
            _portalWindow = gameObject.AddComponent<PortalWindow>();
            _inventoryWindow = gameObject.AddComponent<InventoryWindow>();
            _inventoryFullWindow = gameObject.AddComponent<InventoryFullWindow>();

            // Die Arena spielt zuerst ab; Runenwahl, Events, Shop und Game Over warten so lange.
            System.Func<bool> arenaOpen = () => _arenaWindow.IsOpen;
            _encounterWindow.Hidden = arenaOpen;
            _runeWindow.Hidden = arenaOpen;
            _shopWindow.Hidden = arenaOpen;
            _gameOverWindow.Hidden = arenaOpen;
            _portalWindow.Hidden = arenaOpen;
            _inventoryFullWindow.Hidden = arenaOpen;
            _hud.OnEditBoard = () => _boardWindow.Toggle();
            _hud.OnOpenInventory = () => _inventoryWindow.Toggle();
        }

        private void Start() => StartNewRun();

        /// <summary>Neuer Run: Welt abbauen und das Ritter-Kit wählen lassen. Danach wird die Karte erzeugt.</summary>
        public void StartNewRun()
        {
            if (_root != null) Destroy(_root);
            _root = null;
            Session = null;
            _hud.Initialize(null, null, null, null);
            _arenaWindow.Initialize(null);
            _boardWindow.Initialize(null);
            _portalWindow.Initialize(null);
            _inventoryWindow.Initialize(null);
            _inventoryFullWindow.Initialize(null);
            SetRunWindowsEnabled(false);

            _kitWindow.Open(KnightKit.Defaults, RuneCatalog.CreateDefault(), kit =>
            {
                _kit = kit;
                BuildWorld();
            });
        }

        /// <summary>Erzeugt eine komplett neue Karte mit dem gewählten Kit (Akt 1).</summary>
        public void BuildWorld()
        {
            int seed = settings.seed != 0 ? settings.seed : Random.Range(1, int.MaxValue);
            _config = settings.ToGenerationConfig(seed);
            BuildWorld(OverworldSession.Create(_config, settings.sightRadius, _kit));
        }

        /// <summary>Das Fluchtportal wurde betreten: neue Karte für den nächsten Akt, der Ritter kommt mit.</summary>
        private void OnActCompleted(OverworldSession previous)
        {
            previous.ActCompleted -= OnActCompleted;
            BuildWorld(OverworldSession.CreateNextAct(_config, previous));
        }

        /// <summary>Baut die Darstellung für eine fertige Session und verdrahtet alle Fenster.</summary>
        private void BuildWorld(OverworldSession session)
        {
            if (_root != null) Destroy(_root);
            SetRunWindowsEnabled(true);

            Session = session;
            Session.ActCompleted += OnActCompleted;

            _root = new GameObject($"Overworld (Akt {Session.Act})");
            var layout = new HexLayout(settings.hexSize);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(_root.transform, false);
            HexGridView grid = gridGo.AddComponent<HexGridView>();
            grid.Initialize(Session.Map, layout, settings, _config.Encounters);

            PlayerView player = PlayerView.Create(_root.transform, grid.ToWorld(Session.Player.Position), settings);

            Camera cam = SetupCamera(player.transform);

            OverworldController controller = _root.AddComponent<OverworldController>();
            controller.Initialize(Session, grid, player, cam);
            controller.InputBlocked = () => _arenaWindow.IsOpen || _boardWindow.IsOpen || _inventoryWindow.IsOpen;

            _hud.Initialize(Session, controller, _config.Encounters, StartNewRun);
            _encounterWindow.Initialize(Session, keepMessages: Session.Act > 1);
            _runeWindow.Initialize(Session);
            _shopWindow.Initialize(Session);
            _gameOverWindow.Initialize(Session, StartNewRun);
            _arenaWindow.Initialize(Session);
            _boardWindow.Initialize(Session);
            _portalWindow.Initialize(Session);
            _inventoryWindow.Initialize(Session);
            _inventoryFullWindow.Initialize(Session);

            Debug.Log($"[Betaknight] Akt {Session.Act}: {Session.Map.Count} Felder, Seed {Session.Map.Seed}, Kit {_kit?.Name ?? "keins"}.");
        }

        private void SetRunWindowsEnabled(bool enabled)
        {
            _encounterWindow.enabled = enabled;
            _runeWindow.enabled = enabled;
            _shopWindow.enabled = enabled;
            _gameOverWindow.enabled = enabled;
            _portalWindow.enabled = enabled;
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
