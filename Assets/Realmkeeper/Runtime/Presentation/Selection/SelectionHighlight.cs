using Realmkeeper.Domain.Geography;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// A warm, gently pulsing ring that follows the terrain around the selected settlement.
    /// Its HDR colour is above the bloom threshold, so it glows slightly.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SelectionHighlight : MonoBehaviour
    {
        public const int Segments = 72;
        public const float Width = 0.7f;
        public const float HeightOffset = 0.35f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color RingColor = new Color(1.0f, 0.78f, 0.42f);

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;
        private Mesh _mesh;

        public bool IsVisible => _renderer != null && _renderer.enabled;

        public void Initialize(Material material)
        {
            _renderer = GetComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            _block = new MaterialPropertyBlock();
        }

        public void Show(RegionMap map, SettlementView view)
        {
            PresentationMaterials.DestroyObject(_mesh);
            _mesh = BuildRing(map, view.Center, view.HighlightRadius);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer.enabled = true;
        }

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private void Update()
        {
            if (!IsVisible) return;
            var pulse = 2.2f + 0.6f * Mathf.Sin(Time.unscaledTime * 3f);
            _block.SetColor(BaseColorId, new Color(pulse, pulse, pulse, 1f));
            _renderer.SetPropertyBlock(_block);
        }

        private void OnDestroy() => PresentationMaterials.DestroyObject(_mesh);

        private static Mesh BuildRing(RegionMap map, Vector3 center, float radius)
        {
            var builder = new MeshBuilder();
            var color = RingColor.linear;
            Vector3 Point(int i, float r)
            {
                var angle = 2f * Mathf.PI * i / Segments;
                var x = center.x + Mathf.Cos(angle) * r;
                var z = center.z + Mathf.Sin(angle) * r;
                return new Vector3(x, MapSampling.HeightAt(map, x, z) + HeightOffset, z);
            }

            for (var i = 0; i < Segments; i++)
            {
                builder.AddQuad(Point(i, radius - Width * 0.5f), Point(i, radius + Width * 0.5f),
                    Point(i + 1, radius + Width * 0.5f), Point(i + 1, radius - Width * 0.5f), Vector3.up, color);
            }

            return builder.ToMesh("Selection Ring");
        }
    }
}
