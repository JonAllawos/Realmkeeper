using System.IO;
using Realmkeeper.Application;
using Realmkeeper.Infrastructure.Composition;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Entry point of the Realmkeeper scene. Creates the <see cref="WorldSession"/> through the
    /// composition root, creates the world, owns the <see cref="SimulationPlayback"/> and feeds
    /// it frame time. This is the only connection between Unity's frame loop and the simulation.
    /// Visuals are rebuilt when the session reports changed geography.
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

        public WorldSession Session { get; private set; }

        public SimulationPlayback Playback { get; private set; }

        public WorldView WorldView { get; private set; }

        public IsometricCameraController CameraController { get; private set; }

        public SceneEnvironment Environment { get; private set; }

        public CommandResult LastCreateResult { get; private set; }

        public long Seed
        {
            get => _seed;
            set => _seed = value;
        }

        private void Awake()
        {
            var saveDirectory = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves");
            Session = RealmkeeperComposition.CreateSession(saveDirectory);
            Playback = new SimulationPlayback(Session);

            _materials = new PresentationMaterials(_vertexColorShader, _waterShader);
            Environment = SceneEnvironment.Create(transform);
            CameraController = IsometricCameraController.Create(transform);

            var worldObject = new GameObject("World");
            worldObject.transform.SetParent(transform, false);
            WorldView = worldObject.AddComponent<WorldView>();
            WorldView.Initialize(_materials);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var screenshots = gameObject.AddComponent<DevScreenshot>();
            screenshots.AutoCaptureSeconds = _autoScreenshotSeconds;
#endif

            Session.WorldChanged += OnWorldChanged;
            LastCreateResult = Session.Execute(new CreateWorldCommand(_worldName, _seed));
            if (!LastCreateResult.IsSuccess)
            {
                Debug.LogError($"Realmkeeper: {LastCreateResult.Message} {string.Join("; ", LastCreateResult.Errors)}");
            }

            if (_playOnStart) Playback.Play();
        }

        private void Update()
        {
            Playback?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (Session != null) Session.WorldChanged -= OnWorldChanged;
            if (WorldView != null) WorldView.Clear();
            _materials?.Dispose();
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (!change.GeographyChanged) return;
            var map = Session.Map;
            WorldView.Rebuild(map, Session.GetSettlements());
            CameraController.FrameRegion(WorldView.Bounds, map.WaterWorldHeight);
            Debug.Log($"Realmkeeper: built '{Session.GetOverview().WorldName}' (seed {Session.GetOverview().Seed}): " +
                      $"{WorldView.TerrainChunkCount} terrain chunks, {WorldView.Settlements.Count} settlements, " +
                      $"{WorldView.TreeCount} trees, {WorldView.BridgeCount} bridge cells; camera at {CameraController.Camera.transform.position}, " +
                      $"ortho size {CameraController.Camera.orthographicSize:0.#}.");
        }
    }
}
