#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Development helper: F12 saves the Game view to Documentation/Screenshots (in the
    /// editor) or persistentDataPath/Screenshots (development builds). F10 runs the
    /// documentation screenshot tour. Optionally captures once, automatically, a few
    /// seconds after start.
    /// </summary>
    public sealed class DevScreenshot : MonoBehaviour
    {
        private float _elapsed;
        private bool _autoCaptured;

        public float AutoCaptureSeconds { get; set; }

        public static string Directory
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Documentation", "Screenshots"));
#else
                return Path.Combine(UnityEngine.Application.persistentDataPath, "Screenshots");
#endif
            }
        }

        public static string Capture(string prefix = "dev")
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"Realmkeeper: screenshot saved to {path}");
            return path;
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (!_autoCaptured && AutoCaptureSeconds > 0f && _elapsed >= AutoCaptureSeconds && !ShadersCompiling())
            {
                _autoCaptured = true;
                Capture("dev-auto");
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f12Key.wasPressedThisFrame) Capture();
            if (keyboard != null && keyboard.f10Key.wasPressedThisFrame && !_touring) StartCoroutine(Tour());
        }

        private bool _touring;

        /// <summary>
        /// F10: captures the curated documentation shots under fixed names: a region overview, then
        /// the town selected with the inspector open. Set the Game view to 1920x1080 first.
        /// </summary>
        private IEnumerator Tour()
        {
            _touring = true;
            var bootstrap = GetComponent<RealmkeeperBootstrap>();
            if (Screen.width != 1920 || Screen.height != 1080)
            {
                Debug.Log($"Realmkeeper: screenshot tour running at {Screen.width}x{Screen.height}; set the Game view to 1920x1080 for documentation shots.");
            }

            bootstrap.Selection.Clear();
            bootstrap.CameraController.FrameRegion(bootstrap.WorldView.Bounds, bootstrap.Session.Map.WaterWorldHeight);
            yield return WaitFrames(10);
            CaptureNamed("realmkeeper-overview");
            yield return WaitFrames(5);

            foreach (var view in bootstrap.WorldView.Settlements.Values)
            {
                if (view.SettlementType != Domain.World.SettlementType.Town) continue;
                bootstrap.Selection.Select(view.SettlementId);
                bootstrap.CameraController.FocusOn(view.Center);
                break;
            }

            yield return WaitFrames(10);
            CaptureNamed("realmkeeper-settlement-selected");
            yield return WaitFrames(5);
            _touring = false;
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static void CaptureNamed(string name)
        {
            System.IO.Directory.CreateDirectory(Directory);
            var path = Path.Combine(Directory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"Realmkeeper: screenshot saved to {path}");
        }

        // The editor draws a cyan placeholder while shader variants compile asynchronously;
        // wait for them so captures show the real materials.
        private static bool ShadersCompiling()
        {
#if UNITY_EDITOR
            return UnityEditor.ShaderUtil.anythingCompiling;
#else
            return false;
#endif
        }
    }
}
#endif
