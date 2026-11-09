#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Development helper: F12 saves the Game view to Documentation/Screenshots (in the
    /// editor) or persistentDataPath/Screenshots (development builds). Optionally captures
    /// once, automatically, a few seconds after start.
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
