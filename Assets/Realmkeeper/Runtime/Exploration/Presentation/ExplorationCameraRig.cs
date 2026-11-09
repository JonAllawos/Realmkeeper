using System;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>Tunable settings of one exploration camera perspective.</summary>
    [Serializable]
    public sealed class PerspectiveSettings
    {
        public float FieldOfView = 60f;
        public float MinPitch = -30f;
        public float MaxPitch = 65f;

        /// <summary>Height of the look pivot (third person) or the eye (first person) above the feet.</summary>
        public float PivotHeight = 1.6f;

        /// <summary>Orbit distance behind the pivot (0 in first person).</summary>
        public float Distance = 4f;

        /// <summary>Sideways shoulder offset (third person).</summary>
        public float ShoulderOffset = 0.35f;

        public float NearClip = 0.1f;

        public static PerspectiveSettings ThirdPerson() => new PerspectiveSettings();

        public static PerspectiveSettings FirstPerson() => new PerspectiveSettings
        {
            FieldOfView = 70f, MinPitch = -80f, MaxPitch = 80f, PivotHeight = 1.65f, Distance = 0f, ShoulderOffset = 0f, NearClip = 0.05f,
        };
    }

    /// <summary>
    /// The exploration camera: one Unity camera with separate third- and first-person settings
    /// (the strategic map keeps its own isometric camera). Mouse look sets yaw and pitch; the
    /// character's movement follows the yaw. The third-person camera is pulled in front of walls
    /// and roofs by a sphere cast so the character stays visible. Switching perspective changes
    /// only the camera, never the character's position.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class ExplorationCameraRig : MonoBehaviour
    {
        public const float ObstructionRadius = 0.25f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _currentDistance;

        public Camera Camera { get; private set; }
        public PlayerCharacter Target { get; private set; }
        public IExplorationInput Input { get; set; }

        public PerspectiveSettings ThirdPerson { get; } = PerspectiveSettings.ThirdPerson();
        public PerspectiveSettings FirstPerson { get; } = PerspectiveSettings.FirstPerson();

        /// <summary>Degrees per pointer pixel.</summary>
        public float LookSensitivity { get; set; } = 0.12f;

        /// <summary>When false (cursor released for the HUD), mouse movement does not turn the view.</summary>
        public bool LookEnabled { get; set; } = true;

        public CameraPerspective Perspective { get; private set; } = CameraPerspective.ThirdPerson;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; } = 12f;

        /// <summary>The distance the third-person camera is actually at after obstruction handling.</summary>
        public float CurrentDistance => _currentDistance;

        public PerspectiveSettings Active => Perspective == CameraPerspective.FirstPerson ? FirstPerson : ThirdPerson;

        public static ExplorationCameraRig Create(Transform parent, PlayerCharacter target)
        {
            var go = new GameObject("Exploration Camera");
            go.transform.SetParent(parent, false);
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.66f, 0.74f, 0.82f);
            camera.farClipPlane = 600f;
            camera.depth = 10f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; // shares the global Volume (tonemapping, grading)
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<ExplorationCameraRig>();
            rig.Camera = camera;
            rig.Target = target;
            rig.Yaw = target.Heading;
            rig._currentDistance = rig.ThirdPerson.Distance;
            rig.SetPerspective(CameraPerspective.ThirdPerson);
            rig.Snap();
            return rig;
        }

        public void SetPerspective(CameraPerspective perspective)
        {
            Perspective = perspective;
            var s = Active;
            Camera.fieldOfView = s.FieldOfView;
            Camera.nearClipPlane = s.NearClip;
            Pitch = Mathf.Clamp(Pitch, s.MinPitch, s.MaxPitch);
            if (Target != null)
            {
                if (Target.Body != null) Target.Body.SetActive(perspective == CameraPerspective.ThirdPerson);
                Target.AlignToHeading = perspective == CameraPerspective.FirstPerson;
            }
        }

        /// <summary>Sets the view direction (e.g. to the saved pose's yaw on arrival).</summary>
        public void SetView(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, Active.MinPitch, Active.MaxPitch);
            if (Target != null) Target.Heading = Yaw;
        }

        private void Update()
        {
            if (Input != null && LookEnabled)
            {
                var look = Input.Look * LookSensitivity;
                Yaw = Mathf.Repeat(Yaw + look.x, 360f);
                Pitch = Mathf.Clamp(Pitch - look.y, Active.MinPitch, Active.MaxPitch);
            }

            if (Target != null) Target.Heading = Yaw;
        }

        private void LateUpdate()
        {
            Snap();
        }

        /// <summary>Places the camera for the current target, perspective and view (public for tests).</summary>
        public void Snap()
        {
            if (Target == null) return;
            var s = Active;
            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var pivot = Target.transform.position + Vector3.up * s.PivotHeight;

            if (Perspective == CameraPerspective.FirstPerson)
            {
                transform.SetPositionAndRotation(pivot, rotation);
                _currentDistance = 0f;
                return;
            }

            pivot += rotation * Vector3.right * s.ShoulderOffset;
            var back = rotation * Vector3.back;
            _currentDistance = UnobstructedDistance(pivot, back, s.Distance);
            transform.SetPositionAndRotation(pivot + back * _currentDistance, rotation);
        }

        /// <summary>How far the camera can sit behind the pivot without passing through scenery.</summary>
        public float UnobstructedDistance(Vector3 pivot, Vector3 direction, float desired)
        {
            var count = Physics.SphereCastNonAlloc(pivot, ObstructionRadius, direction, _hits, desired, ~0, QueryTriggerInteraction.Ignore);
            var distance = desired;
            for (var i = 0; i < count; i++)
            {
                var hit = _hits[i];
                if (Target != null && hit.collider.transform.IsChildOf(Target.transform)) continue;
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue; // started inside: ignore, the pivot is above the body
                distance = Mathf.Min(distance, Mathf.Max(0.3f, hit.distance - 0.05f));
            }

            return distance;
        }
    }
}
