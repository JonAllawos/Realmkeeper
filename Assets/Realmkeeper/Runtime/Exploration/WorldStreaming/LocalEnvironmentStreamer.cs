using System;
using Realmkeeper.Exploration.Domain;
using UnityEngine.SceneManagement;

namespace Realmkeeper.Exploration.WorldStreaming
{
    /// <summary>
    /// Loads and unloads a settlement's local environment as its own runtime scene, created with
    /// Unity's built-in <see cref="SceneManager"/> (ADR-0009 point 7: no streaming packages yet). At
    /// most one environment is loaded at a time; loading the same settlement again is refused,
    /// so visuals and residents can never be duplicated. The strategic scene is never unloaded,
    /// only hidden by its presentation.
    /// </summary>
    public sealed class LocalEnvironmentStreamer
    {
        private Scene _previousActive;
        private int _loadCount;

        /// <summary>How many environments have been loaded so far (diagnostics and tests).</summary>
        public int LoadCount => _loadCount;

        /// <summary>The loaded environment's layout, or null when none is loaded.</summary>
        public LocalEnvironmentLayout Current { get; private set; }

        public Scene Scene { get; private set; }

        public bool IsLoaded => Current != null;

        /// <summary>Raised after the scene exists and is active; presentation builds visuals into it.</summary>
        public event Action<Scene, LocalEnvironmentLayout> Loaded;

        /// <summary>Raised just before the scene is unloaded.</summary>
        public event Action<LocalEnvironmentLayout> Unloading;

        public void Load(LocalEnvironmentLayout layout, string displayName)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (IsLoaded)
            {
                if (Current.EnvironmentKey == layout.EnvironmentKey)
                    throw new InvalidOperationException($"Environment {layout.EnvironmentKey} is already loaded.");
                throw new InvalidOperationException($"Unload {Current.EnvironmentKey} before loading {layout.EnvironmentKey}.");
            }

            _previousActive = SceneManager.GetActiveScene();
            // A counter keeps the name unique while a previous copy is still unloading asynchronously.
            _loadCount++;
            Scene = SceneManager.CreateScene($"Exploration - {displayName} ({layout.SettlementId.ToString().Substring(0, 8)}) #{_loadCount}");
            SceneManager.SetActiveScene(Scene);
            Current = layout;
            Loaded?.Invoke(Scene, layout);
        }

        /// <summary>Unloads the environment (asynchronously; its objects disappear this frame) and restores the previous active scene.</summary>
        public void Unload()
        {
            if (!IsLoaded) return;
            var layout = Current;
            Unloading?.Invoke(layout);
            var scene = Scene;
            Current = null;
            Scene = default;
            if (_previousActive.IsValid() && _previousActive.isLoaded) SceneManager.SetActiveScene(_previousActive);
            foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.Destroy(root);
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
