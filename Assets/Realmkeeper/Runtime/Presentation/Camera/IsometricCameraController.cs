using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Orthographic isometric camera on a pivot: pitched about 35°, rotated in 90° steps
    /// with easing, panned and zoomed within the region bounds. Input reading lives in
    /// <see cref="CameraInputReader"/>; motion maths in <see cref="IsometricCameraMath"/>.
    /// This component only connects them to the Transform and Camera.
    /// </summary>
    public sealed class IsometricCameraController : MonoBehaviour
    {
        public const float DefaultOrthoSize = 74f;

        [SerializeField] private bool _readInput = true;
        [SerializeField] private bool _edgePan;

        private readonly CameraInputReader _reader = new CameraInputReader();

        public Camera Camera { get; private set; }

        public IsometricCameraMath Motion { get; private set; }

        /// <summary>When false, Update ignores devices; tests drive <see cref="Motion"/> directly.</summary>
        public bool ReadInput
        {
            get => _readInput;
            set => _readInput = value;
        }

        public static IsometricCameraController Create(Transform parent)
        {
            var rig = new GameObject("Camera Rig");
            rig.transform.SetParent(parent, false);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Haze;
            cameraObject.AddComponent<AudioListener>();

            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var controller = rig.AddComponent<IsometricCameraController>();
            controller.Initialize(camera);
            return controller;
        }

        public void Initialize(Camera targetCamera)
        {
            Camera = targetCamera;
            Motion = new IsometricCameraMath(new IsometricCameraSettings(), new Rect(), Vector3.zero, DefaultOrthoSize);
            Camera.nearClipPlane = 1f;
            Camera.farClipPlane = Motion.Settings.Distance + 450f;
            SceneEnvironment.ConfigureFog(Motion.Settings.Distance);
            Apply();
        }

        /// <summary>Optional: returns true when the pointer is over UI, so wheel and drag go to the UI instead.</summary>
        public System.Func<Vector2, bool> IsPointerOverUi { get; set; }

        public const float FocusOrthoSize = 28f;

        /// <summary>Centres the view on a world point, zooming in to at least <see cref="FocusOrthoSize"/>.</summary>
        public void FocusOn(Vector3 point)
        {
            Motion.JumpTo(new Vector3(point.x, Motion.Pivot.y, point.z), Mathf.Min(Motion.TargetOrthoSize, FocusOrthoSize));
            Apply();
        }

        /// <summary>Frames the whole region and restricts panning to it.</summary>
        public void FrameRegion(Rect bounds, float groundHeight)
        {
            Motion.SetBounds(bounds);
            Motion.JumpTo(new Vector3(bounds.center.x, groundHeight, bounds.center.y), DefaultOrthoSize);
            Apply();
        }

        private void Update()
        {
            if (Motion == null) return;
            _reader.EdgePanEnabled = _edgePan;
            var input = _readInput ? _reader.Read() : CameraInput.None;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (_readInput && mouse != null && IsPointerOverUi != null && IsPointerOverUi(mouse.position.ReadValue()))
            {
                input.Zoom = 0f;
                input.DragPixels = Vector2.zero;
            }

            var unitsPerPixel = Screen.height > 0 ? 2f * Motion.OrthoSize / Screen.height : 0f;
            Motion.Step(input, Time.unscaledDeltaTime, unitsPerPixel);
            Apply();
        }

        public void Apply()
        {
            if (Motion == null || Camera == null) return;
            transform.SetPositionAndRotation(Motion.Pivot, Quaternion.Euler(0f, Motion.Yaw, 0f));
            Camera.transform.localRotation = Quaternion.Euler(Motion.Settings.Pitch, 0f, 0f);
            Camera.transform.localPosition = Camera.transform.localRotation * new Vector3(0f, 0f, -Motion.Settings.Distance);
            Camera.orthographicSize = Motion.OrthoSize;
        }
    }
}
