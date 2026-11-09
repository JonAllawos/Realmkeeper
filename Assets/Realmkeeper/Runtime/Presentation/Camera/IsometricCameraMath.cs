using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>One frame of camera intent, produced by an input reader or by tests.</summary>
    public struct CameraInput
    {
        /// <summary>Keyboard or edge pan, each axis in -1..1 (x = screen right, y = screen up).</summary>
        public Vector2 Pan;

        /// <summary>Pointer drag this frame in screen pixels (positive x = right, positive y = up).</summary>
        public Vector2 DragPixels;

        /// <summary>Scroll notches this frame (positive = zoom in).</summary>
        public float Zoom;

        /// <summary>Quarter turns requested this frame (+1 = clockwise, -1 = counter-clockwise).</summary>
        public int RotateSteps;

        public static CameraInput None => default;
    }

    public sealed class IsometricCameraSettings
    {
        public float Pitch { get; set; } = 35f;
        public float Distance { get; set; } = 200f;
        public float PanSpeed { get; set; } = 1.1f;          // ortho-size units per second
        public float MinOrthoSize { get; set; } = 8f;
        public float MaxOrthoSize { get; set; } = 110f;
        public float ZoomStep { get; set; } = 1.15f;          // size factor per scroll notch
        public float ZoomSharpness { get; set; } = 12f;       // exponential smoothing rate
        public float RotateDuration { get; set; } = 0.35f;    // seconds per quarter-turn ease
    }

    /// <summary>
    /// Camera motion maths, separate from input reading and from Unity components so it can
    /// be tested with synthetic input. Yaw is in degrees; 0 means the camera looks north (+Z).
    /// </summary>
    public sealed class IsometricCameraMath
    {
        private float _rotateFrom;
        private float _rotateProgress = 1f;

        public IsometricCameraMath(IsometricCameraSettings settings, Rect bounds, Vector3 pivot, float orthoSize, float yaw = 45f)
        {
            Settings = settings ?? new IsometricCameraSettings();
            Bounds = bounds;
            Pivot = pivot;
            OrthoSize = TargetOrthoSize = Mathf.Clamp(orthoSize, Settings.MinOrthoSize, Settings.MaxOrthoSize);
            Yaw = TargetYaw = yaw;
            Pivot = ClampToBounds(Pivot);
        }

        public IsometricCameraSettings Settings { get; }

        /// <summary>World-space XZ rectangle the pivot is kept inside.</summary>
        public Rect Bounds { get; private set; }

        public Vector3 Pivot { get; private set; }

        public float Yaw { get; private set; }

        public float TargetYaw { get; private set; }

        public float OrthoSize { get; private set; }

        public float TargetOrthoSize { get; private set; }

        public bool IsRotating => _rotateProgress < 1f;

        public Quaternion Rotation => Quaternion.Euler(Settings.Pitch, Yaw, 0f);

        /// <summary>Camera position: back from the pivot along the view direction.</summary>
        public Vector3 CameraPosition => Pivot - Rotation * Vector3.forward * Settings.Distance;

        public void SetBounds(Rect bounds)
        {
            Bounds = bounds;
            Pivot = ClampToBounds(Pivot);
        }

        public void JumpTo(Vector3 pivot, float orthoSize)
        {
            Pivot = ClampToBounds(pivot);
            OrthoSize = TargetOrthoSize = Mathf.Clamp(orthoSize, Settings.MinOrthoSize, Settings.MaxOrthoSize);
        }

        /// <param name="unitsPerPixel">World units per screen pixel (2 * orthoSize / screenHeight).</param>
        public void Step(CameraInput input, float deltaTime, float unitsPerPixel)
        {
            if (deltaTime < 0f) deltaTime = 0f;

            // Rotation: each request adds a quarter turn and restarts the ease from where we are.
            if (input.RotateSteps != 0)
            {
                _rotateFrom = Yaw;
                TargetYaw += 90f * input.RotateSteps;
                _rotateProgress = 0f;
            }

            if (_rotateProgress < 1f)
            {
                _rotateProgress = Settings.RotateDuration <= 0f ? 1f : Mathf.Min(1f, _rotateProgress + deltaTime / Settings.RotateDuration);
                var t = _rotateProgress * _rotateProgress * (3f - 2f * _rotateProgress);
                Yaw = Mathf.Lerp(_rotateFrom, TargetYaw, t);
                if (_rotateProgress >= 1f)
                {
                    TargetYaw = Normalize(TargetYaw);
                    Yaw = TargetYaw;
                }
            }

            // Pan in the camera's ground frame.
            var yawRad = Yaw * Mathf.Deg2Rad;
            var forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            var right = new Vector3(forward.z, 0f, -forward.x);
            var move = (right * input.Pan.x + forward * input.Pan.y) * (Settings.PanSpeed * OrthoSize * deltaTime);

            // Drag: content follows the pointer. Ground is foreshortened by sin(pitch) vertically.
            if (input.DragPixels != Vector2.zero && unitsPerPixel > 0f)
            {
                var sinPitch = Mathf.Max(0.1f, Mathf.Sin(Settings.Pitch * Mathf.Deg2Rad));
                move -= right * (input.DragPixels.x * unitsPerPixel);
                move -= forward * (input.DragPixels.y * unitsPerPixel / sinPitch);
            }

            Pivot = ClampToBounds(Pivot + move);

            // Zoom: scroll changes the target; the actual size eases towards it.
            if (input.Zoom != 0f)
            {
                TargetOrthoSize = Mathf.Clamp(TargetOrthoSize * Mathf.Pow(Settings.ZoomStep, -input.Zoom), Settings.MinOrthoSize, Settings.MaxOrthoSize);
            }

            var blend = 1f - Mathf.Exp(-Settings.ZoomSharpness * deltaTime);
            OrthoSize = Mathf.Abs(TargetOrthoSize - OrthoSize) < 0.001f ? TargetOrthoSize : Mathf.Lerp(OrthoSize, TargetOrthoSize, blend);
        }

        public Vector3 ClampToBounds(Vector3 p)
        {
            if (Bounds.width <= 0f || Bounds.height <= 0f) return p;
            return new Vector3(Mathf.Clamp(p.x, Bounds.xMin, Bounds.xMax), p.y, Mathf.Clamp(p.z, Bounds.yMin, Bounds.yMax));
        }

        private static float Normalize(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }
    }
}
