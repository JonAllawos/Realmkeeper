using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Visual for one settlement, keyed by <see cref="SettlementId"/> (never by name).
    /// Holds a combined building mesh and one box collider so selection can raycast to it.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
    public sealed class SettlementView : MonoBehaviour
    {
        private Mesh _mesh;

        public SettlementId SettlementId { get; private set; }

        public SettlementType SettlementType { get; private set; }

        public int BuildingCount { get; private set; }

        /// <summary>World-space centre of the settlement on the ground.</summary>
        public Vector3 Center { get; private set; }

        public void Build(RegionMap map, SettlementSummary settlement, Material material)
        {
            SettlementId = settlement.SettlementId;
            SettlementType = settlement.SettlementType;
            Center = MapSampling.CellToWorld(map, settlement.Position);
            gameObject.name = $"Settlement {settlement.Name} ({settlement.SettlementType})";

            var result = SettlementBuilder.Build(map, settlement);
            PresentationMaterials.DestroyObject(_mesh);
            _mesh = result.Mesh;
            BuildingCount = result.BuildingCount;

            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

            var box = GetComponent<BoxCollider>();
            box.center = result.Footprint.center - transform.position;
            box.size = result.Footprint.size;
        }

        private void OnDestroy() => PresentationMaterials.DestroyObject(_mesh);
    }
}
