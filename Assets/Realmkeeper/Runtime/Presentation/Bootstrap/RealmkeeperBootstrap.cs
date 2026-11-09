using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Hosting;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Entry point of the strategic presentation. It does not own the world: it asks the one
    /// <see cref="RealmkeeperRuntime"/> (which survives scene changes) for the active session,
    /// starting a new world only when none is active, then builds the map view, camera,
    /// selection and HUD on that session. Visuals are rebuilt when the session reports changed
    /// geography. While the player character is inside a settlement (exploration view) the
    /// strategic view is hidden; it returns unchanged when the character comes back.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class RealmkeeperBootstrap : MonoBehaviour
    {
        [SerializeField] private string _worldName = "Realmkeeper";
        [SerializeField] private long _seed = 123456789;
        [SerializeField] private bool _playOnStart;
        [SerializeField] private Shader _vertexColorShader;
        [SerializeField] private Shader _waterShader;

        [Tooltip("Editor only: seconds after start to save a Game view screenshot under Documentation/Screenshots (0 = off). F12 always captures.")]
        [SerializeField] private float _autoScreenshotSeconds;

        private PresentationMaterials _materials;
        private bool _strategicVisible = true;
        private WorldSessionBinding _binding;

        public RealmkeeperRuntime Runtime { get; private set; }

        /// <summary>The runtime's active session (the same object exploration uses); null while none is active.</summary>
        public WorldSession Session => _binding?.Session;

        public SimulationPlayback Playback => Runtime != null ? Runtime.Playback : null;

        public WorldView WorldView { get; private set; }

        public IsometricCameraController CameraController { get; private set; }

        public SceneEnvironment Environment { get; private set; }

        public SettlementSelection Selection { get; private set; }

        /// <summary>Condition markers above settlements (food shortage, strong production, trade, declining stability).</summary>
        public MapIndicatorLayer Indicators { get; private set; }

        public RealmkeeperHud Hud { get; private set; }

        public CommandResult LastCreateResult { get; private set; }

        /// <summary>True while the strategic map (camera, world, HUD) is shown.</summary>
        public bool StrategicVisible => _strategicVisible;

        public long Seed
        {
            get => _seed;
            set => _seed = value;
        }

        private void Awake()
        {
            Runtime = RealmkeeperRuntime.FindOrCreate();

            _materials = new PresentationMaterials(_vertexColorShader, _waterShader);
            Environment = SceneEnvironment.Create(transform);
            CameraController = IsometricCameraController.Create(transform);

            var worldObject = new GameObject("World");
            worldObject.transform.SetParent(transform, false);
            WorldView = worldObject.AddComponent<WorldView>();
            WorldView.Initialize(_materials);
            Indicators = MapIndicatorLayer.Create(transform, WorldView, CameraController.Camera);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var screenshots = gameObject.AddComponent<DevScreenshot>();
            screenshots.AutoCaptureSeconds = _autoScreenshotSeconds;
#endif

            // Rebuild visuals first; selection and HUD subscribe afterwards so they see the new views.
            _binding = new WorldSessionBinding(OnWorldChanged);
            Selection = gameObject.AddComponent<SettlementSelection>();
            Selection.Initialize(null, WorldView, CameraController.Camera, _materials.Highlight);
            Hud = RealmkeeperHud.Create(transform);
            Hud.Initialize(null, null, Selection, CameraController, WorldView);
            Hud.SaveHandler = () => Runtime.Save();
            Hud.LoadHandler = () => Runtime.Load();
            Selection.IsPointerOverUi = Hud.IsPointerOverUi;
            CameraController.IsPointerOverUi = Hud.IsPointerOverUi;

            // Follow every session the runtime starts, not just the first (restart, start from a save).
            Runtime.Host.SessionStarted += BindSession;
            Runtime.Host.SessionEnded += UnbindSession;
            Runtime.ModeChanged += OnModeChanged;

            // Uses the world that is already running (for example after a scene change), or starts one.
            LastCreateResult = Runtime.EnsureWorld(_worldName, _seed);
            if (!LastCreateResult.IsSuccess || !Runtime.Host.IsActive)
            {
                Debug.LogError($"Realmkeeper: {LastCreateResult.Message} {string.Join("; ", LastCreateResult.Errors)}");
                ApplyMode(Runtime.Mode);
                return;
            }

            BindSession(Runtime.Session); // no-op when SessionStarted already bound it
            ApplyMode(Runtime.Mode);

            if (_playOnStart) Playback.Play();
        }

        /// <summary>
        /// Points the whole strategic view (world visuals, selection, HUD and its clock) at a
        /// session. The previous session's subscriptions are removed first; binding the same
        /// session again does nothing, so repeated restarts never stack subscriptions.
        /// </summary>
        private void BindSession(WorldSession session)
        {
            if (!_binding.Bind(session)) return;
            Selection.Bind(session);
            Hud.Bind(session, Runtime.Playback);
            RebuildWorld();
        }

        /// <summary>The runtime shut its session down: let go of it everywhere and show no world.</summary>
        private void UnbindSession()
        {
            _binding.Unbind();
            Selection.Bind(null);
            Hud.Bind(null, null);
            Indicators.Clear();
            WorldView.Clear();
        }

        private void OnDestroy()
        {
            _binding?.Dispose();
            if (Runtime != null)
            {
                Runtime.ModeChanged -= OnModeChanged;
                if (Runtime.Host != null)
                {
                    Runtime.Host.SessionStarted -= BindSession;
                    Runtime.Host.SessionEnded -= UnbindSession;
                }
            }

            if (WorldView != null) WorldView.Clear();
            _materials?.Dispose();
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (change.Reason == WorldChangeReason.Closed)
            {
                Indicators.Clear();
                WorldView.Clear();
                return;
            }

            if (change.GeographyChanged) RebuildWorld(); // rebuilds the views, then the indicators
            else Indicators.Refresh(Session.GetSettlements()); // ratings, shortages and trade change daily
        }

        private void RebuildWorld()
        {
            if (Session == null || !Session.HasWorld) return;
            var map = Session.Map;
            WorldView.Rebuild(map, Session.GetSettlements());
            Indicators.Clear();
            Indicators.Refresh(Session.GetSettlements());
            CameraController.FrameRegion(WorldView.Bounds, map.WaterWorldHeight);
            Debug.Log($"Realmkeeper: built '{Session.GetOverview().WorldName}' (seed {Session.GetOverview().Seed}): " +
                      $"{WorldView.TerrainChunkCount} terrain chunks, {WorldView.Settlements.Count} settlements, " +
                      $"{WorldView.TreeCount} trees, {WorldView.BridgeCount} bridge cells; camera at {CameraController.Camera.transform.position}, " +
                      $"ortho size {CameraController.Camera.orthographicSize:0.#}.");
        }

        private void OnModeChanged(ViewMode previous, ViewMode current)
        {
            ApplyMode(current);
            if (current != ViewMode.Strategic || Session == null || !Session.HasWorld) return;

            // Back on the map: select and focus the settlement the character just left.
            var player = Session.GetPlayer();
            var last = player != null ? player.LastSettlementId : default(SettlementId);
            if (last.IsEmpty) return;
            Selection.Select(last);
            var view = WorldView.FindSettlementView(last);
            if (view != null) CameraController.FocusOn(view.Center);
        }

        /// <summary>
        /// Shows or hides the strategic view. Hidden objects keep their state (camera position,
        /// selection, built terrain), so returning restores the map exactly as it was. The
        /// global Volume stays on: exploration uses the same colour grading.
        /// </summary>
        private void ApplyMode(ViewMode mode)
        {
            var visible = mode == ViewMode.Strategic;
            _strategicVisible = visible;
            CameraController.gameObject.SetActive(visible);
            WorldView.gameObject.SetActive(visible);
            Indicators.gameObject.SetActive(visible);
            if (Environment.Sun != null) Environment.Sun.gameObject.SetActive(visible);
            if (Hud != null) Hud.SetVisible(visible); // hidden by style: re-enabling a UIDocument would rebuild its tree
            if (Selection != null)
            {
                Selection.enabled = visible;
                Selection.SetHighlightShown(visible);
            }
        }
    }
}
