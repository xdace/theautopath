using Betaknight.Core;
using Betaknight.Core.Autoplay;
using Betaknight.Core.Hex;
using Betaknight.Core.Map;
using Betaknight.Core.Run;
using Betaknight.Core.Runes;
using Betaknight.Overworld.Autoplay;
using Betaknight.Overworld.Config;
using Betaknight.Overworld.Controllers;
using Betaknight.Overworld.Persistence;
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
        [Tooltip("Optional. Without an asset the default values are used.")]
        [SerializeField] private OverworldSettings settings;

        [Tooltip("Optional. If unassigned, Camera.main is used or a camera is created.")]
        [SerializeField] private Camera targetCamera;

        private GameObject _root;
        private OverworldHud _hud;
        private ToastLayer _toasts;
        private EncounterWindow _encounterWindow;
        private RuneOfferWindow _runeWindow;
        private SalvageWindow _salvageWindow;
        private ShopWindow _shopWindow;
        private GameOverWindow _gameOverWindow;
        private KitSelectionWindow _kitWindow;
        private ArenaWindow _arenaWindow;
        private BuildWindow _buildWindow;
        private PortalWindow _portalWindow;
        private InventoryWindow _inventoryWindow;
        private InventoryFullWindow _inventoryFullWindow;
        private MapGenerationConfig _config;
        private KnightKit _kit;
        private OverworldController _controller;
        private AutoplayRunner _autoplay;
        private readonly PlayerPrefsRecipeBookStore _recipeStore = new PlayerPrefsRecipeBookStore();

        public OverworldSession Session { get; private set; }

        // Zugriff für den Testspieler (-autoplay): er bedient dieselben Fenster wie ein Mensch.
        internal KitSelectionWindow KitWindow => _kitWindow;
        internal ArenaWindow Arena => _arenaWindow;
        internal BuildWindow BuildWin => _buildWindow;
        internal InventoryWindow InventoryWin => _inventoryWindow;
        internal OverworldController Controller => _controller;

        /// <summary>Seed für den nächsten Run statt Einstellung oder Zufall (Testspieler mit -seed).</summary>
        internal int? SeedOverride { get; set; }

        private void Awake()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<OverworldSettings>();
                settings.name = "OverworldSettings (Default)";
            }

            _hud = gameObject.AddComponent<OverworldHud>();
            _encounterWindow = gameObject.AddComponent<EncounterWindow>();
            _toasts = gameObject.AddComponent<ToastLayer>();
            _runeWindow = gameObject.AddComponent<RuneOfferWindow>();
            _salvageWindow = gameObject.AddComponent<SalvageWindow>();
            _shopWindow = gameObject.AddComponent<ShopWindow>();
            _gameOverWindow = gameObject.AddComponent<GameOverWindow>();
            _kitWindow = gameObject.AddComponent<KitSelectionWindow>();
            _arenaWindow = gameObject.AddComponent<ArenaWindow>();
            _buildWindow = gameObject.AddComponent<BuildWindow>();
            _portalWindow = gameObject.AddComponent<PortalWindow>();
            _inventoryWindow = gameObject.AddComponent<InventoryWindow>();
            _inventoryFullWindow = gameObject.AddComponent<InventoryFullWindow>();

            // Die Arena spielt zuerst ab; Bergen, Runenwahl, Events, Shop und Game Over warten so lange. Das Bergen kommt vor
            // der Belohnung (die Session bietet sie erst danach an), beide öffnen sich, sobald die Arena geschlossen ist.
            System.Func<bool> arenaOpen = () => _arenaWindow.IsOpen;
            _encounterWindow.Hidden = arenaOpen;
            _toasts.Hidden = arenaOpen;
            _runeWindow.Hidden = arenaOpen;
            _salvageWindow.Hidden = arenaOpen;
            _shopWindow.Hidden = arenaOpen;
            _gameOverWindow.Hidden = arenaOpen;
            _portalWindow.Hidden = arenaOpen;
            _inventoryFullWindow.Hidden = arenaOpen;
            // «Build» und «Inventar» sind nie gleichzeitig offen: das eine schliesst das andere.
            _hud.OnOpenBuild = ToggleBuild;
            _arenaWindow.OnEditBoard = () =>
            {
                _inventoryWindow.Close();
                _buildWindow.Open();
            };
            _hud.OnOpenInventory = ToggleInventory;
            _hud.OnOpenJournal = _toasts.ToggleJournal;

            AutoplayOptions autoplay = AutoplayOptions.Parse(System.Environment.GetCommandLineArgs());
            if (autoplay.Enabled)
            {
                _autoplay = gameObject.AddComponent<AutoplayRunner>();
                _autoplay.Initialize(this, autoplay);
            }
        }

        private void Start()
        {
            if (_autoplay != null) _autoplay.Begin();
            else StartNewRun();
        }

        /// <summary>Öffnet «Build» (schliesst «Inventar»), wie Taste B bei geschlossenem Fenster.</summary>
        internal void OpenBuild()
        {
            _inventoryWindow.Close();
            _buildWindow.Open();
        }

        /// <summary>Öffnet «Inventar» (schliesst «Build»), wie Taste I bei geschlossenem Fenster.</summary>
        internal void OpenInventory()
        {
            _buildWindow.Close();
            _inventoryWindow.Open();
        }

        internal void CloseLoadoutWindows()
        {
            _buildWindow.Close();
            _inventoryWindow.Close();
        }

        private void ToggleBuild()
        {
            _inventoryWindow.Close();
            _buildWindow.Toggle();
        }

        private void ToggleInventory()
        {
            _buildWindow.Close();
            _inventoryWindow.Toggle();
        }

        /// <summary>Tasten B (Build) und I (Inventar); nicht während die Arena läuft.</summary>
        private void OnGUI()
        {
            Event e = Event.current;
            if (Session == null || _arenaWindow.IsOpen || e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.B)
            {
                ToggleBuild();
                e.Use();
            }
            else if (e.keyCode == KeyCode.I)
            {
                ToggleInventory();
                e.Use();
            }
        }

        /// <summary>Neuer Run: Welt abbauen und das Ritter-Kit wählen lassen. Danach wird die Karte erzeugt.</summary>
        public void StartNewRun()
        {
            if (_root != null) Destroy(_root);
            _root = null;
            Session = null;
            _controller = null;
            _hud.Initialize(null, null, null, null);
            _toasts.Initialize(null);
            _arenaWindow.Initialize(null);
            _buildWindow.Initialize(null);
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
            int seed = SeedOverride ?? (settings.seed != 0 ? settings.seed : Random.Range(1, int.MaxValue));
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
            Session.UseRecipeStore(_recipeStore);
            Session.ActCompleted += OnActCompleted;

            _root = new GameObject($"Overworld (Act {Session.Act})");
            var layout = new HexLayout(settings.hexSize);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(_root.transform, false);
            HexGridView grid = gridGo.AddComponent<HexGridView>();
            grid.Initialize(Session.Map, layout, settings, _config.Encounters);

            PlayerView player = PlayerView.Create(_root.transform, grid.ToWorld(Session.Player.Position), settings);

            Camera cam = SetupCamera(player.transform);

            OverworldController controller = _root.AddComponent<OverworldController>();
            controller.Initialize(Session, grid, player, cam);
            controller.InputBlocked = () => _arenaWindow.IsOpen || _buildWindow.IsOpen || _inventoryWindow.IsOpen;
            _controller = controller;

            _hud.Initialize(Session, controller, _config.Encounters, StartNewRun);
            _encounterWindow.Initialize(Session);
            _toasts.Initialize(Session, keep: Session.Act > 1);
            _runeWindow.Initialize(Session);
            _salvageWindow.Initialize(Session);
            _shopWindow.Initialize(Session);
            _gameOverWindow.Initialize(Session, StartNewRun);
            _arenaWindow.Initialize(Session);
            _buildWindow.Initialize(Session);
            _portalWindow.Initialize(Session);
            _inventoryWindow.Initialize(Session);
            _inventoryFullWindow.Initialize(Session);

            Debug.Log($"[Betaknight] Act {Session.Act}: {Session.Map.Count} tiles, seed {Session.Map.Seed}, kit {_kit?.Name ?? "none"}.");
        }

        private void SetRunWindowsEnabled(bool enabled)
        {
            _encounterWindow.enabled = enabled;
            _toasts.enabled = enabled;
            _runeWindow.enabled = enabled;
            _salvageWindow.enabled = enabled;
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
