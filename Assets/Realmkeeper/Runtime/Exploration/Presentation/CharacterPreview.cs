using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The Player Hub's live 3D view of the character (Phase 5): the same body the player has in the
    /// street (built by <see cref="PlayerBodyBuilder"/> from world data), on a small lit stage high above
    /// the settlement on its own layer, filmed by its own camera into a render texture. Drag rotates
    /// the character; the wheel zooms. Presentation only.
    /// </summary>
    public sealed class CharacterPreview : MonoBehaviour
    {
        /// <summary>The layer the stage is drawn on (excluded from the street camera).</summary>
        public const int Layer = 31;

        private static readonly Vector3 StagePosition = new Vector3(0f, 400f, 0f);

        private Transform _turntable;
        private Camera _camera;
        private float _yaw = 180f;
        private float _distance = 3.1f;
        private string _signature;

        public RenderTexture Texture { get; private set; }
        public CharacterAnimationDriver Animation { get; private set; }
        public PlayerBodyReport Report { get; private set; }
        public float Yaw => _yaw;
        public float Distance => _distance;

        public static CharacterPreview Create(Transform parent)
        {
            var go = new GameObject("Character preview");
            go.transform.SetParent(parent, false);
            go.transform.position = StagePosition;
            var preview = go.AddComponent<CharacterPreview>();
            preview.Texture = new RenderTexture(640, 880, 24) { name = "Character preview", antiAliasing = 4 };

            preview._turntable = new GameObject("Turntable").transform;
            preview._turntable.SetParent(go.transform, false);

            var camGo = new GameObject("Preview camera");
            camGo.transform.SetParent(go.transform, false);
            preview._camera = camGo.AddComponent<Camera>();
            preview._camera.clearFlags = CameraClearFlags.SolidColor;
            preview._camera.backgroundColor = new Color(0.11f, 0.1f, 0.09f, 1f);
            preview._camera.cullingMask = 1 << Layer;
            preview._camera.fieldOfView = 30f;
            preview._camera.nearClipPlane = 0.1f;
            preview._camera.farClipPlane = 20f;
            preview._camera.targetTexture = preview.Texture;
            var data = preview._camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            AddLight(go.transform, "Key", new Vector3(1.6f, 2.4f, -2.2f), new Color(1f, 0.92f, 0.8f), 3.2f);
            AddLight(go.transform, "Fill", new Vector3(-2f, 1.4f, -1.5f), new Color(0.7f, 0.78f, 0.95f), 1.6f);
            AddLight(go.transform, "Rim", new Vector3(0f, 2.2f, 2.2f), new Color(1f, 0.85f, 0.65f), 2.2f);
            preview.PlaceCamera();
            return preview;
        }

        private static void AddLight(Transform parent, string name, Vector3 at, Color colour, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.range = 8f;
            light.color = colour;
            light.intensity = intensity;
            light.cullingMask = 1 << Layer;
            light.shadows = LightShadows.None;
        }

        /// <summary>Rebuilds the body if the look changed.</summary>
        public void Show(PlayerLook look, ArtCatalog catalog, GeneratedAssets assets)
        {
            if (look == null || look.Signature == _signature) return;
            _signature = look.Signature;
            foreach (Transform child in _turntable)
            {
                if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            var holder = new GameObject("Body");
            holder.transform.SetParent(_turntable, false);
            Animation = PlayerBodyBuilder.Build(holder.transform, look, catalog, assets, out var report);
            Report = report;
            foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        public void Rotate(float degrees)
        {
            _yaw = Mathf.Repeat(_yaw + degrees, 360f);
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public void Zoom(float delta)
        {
            _distance = Mathf.Clamp(_distance - delta, 1.4f, 4.5f);
            PlaceCamera();
        }

        public void SetActive(bool active)
        {
            if (_camera != null) _camera.enabled = active;
        }

        private void PlaceCamera()
        {
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            var lookAt = new Vector3(0f, _distance < 2.2f ? 1.45f : 1.0f, 0f);
            _camera.transform.localPosition = lookAt + new Vector3(0f, 0.15f, -_distance);
            _camera.transform.LookAt(transform.TransformPoint(lookAt));
        }

        /// <summary>Renders now (the hub calls it so the picture is current when opened or changed).</summary>
        public void RenderNow()
        {
            if (_camera != null) _camera.Render();
        }

        private void OnDestroy()
        {
            if (Texture != null)
            {
                Texture.Release();
                if (UnityEngine.Application.isPlaying) Destroy(Texture);
                else DestroyImmediate(Texture);
            }
        }
    }
}
