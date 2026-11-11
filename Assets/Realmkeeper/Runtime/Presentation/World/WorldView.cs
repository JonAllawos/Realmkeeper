using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Root of all world visuals. Rebuilds terrain, water, roads, forests and settlements
    /// from a <see cref="RegionMap"/> and settlement snapshots; owns every mesh it creates.
    /// Reads data only; it never changes world state.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const float SkirtDepth = 6f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Dictionary<SettlementId, SettlementView> _settlements = new Dictionary<SettlementId, SettlementView>();
        private readonly List<GameObject> _children = new List<GameObject>();
        private PresentationMaterials _materials;
        private ForestRenderer _forest;

        public RegionMap Map { get; private set; }

        public IReadOnlyDictionary<SettlementId, SettlementView> Settlements => _settlements;

        public int TerrainChunkCount { get; private set; }

        public int BridgeCount { get; private set; }

        public int TreeCount => _forest != null ? _forest.InstanceCount : 0;

        public MeshCollider TerrainCollider { get; private set; }

        /// <summary>World-space XZ rectangle covered by the region.</summary>
        public Rect Bounds => Map == null ? new Rect() : new Rect(0f, 0f, MapSampling.WorldWidth(Map), MapSampling.WorldDepth(Map));

        public void Initialize(PresentationMaterials materials)
        {
            _materials = materials;
        }

        public void Rebuild(RegionMap map, IReadOnlyList<SettlementSummary> settlements, System.Func<Realmkeeper.Domain.Identifiers.SettlementId, Realmkeeper.Domain.Settlements.SettlementLayoutData> plans = null)
        {
            Clear();
            Map = map;
            if (map == null) return;

            var terrainRoot = CreateChild("Terrain", transform).transform;
            var chunks = TerrainMeshBuilder.BuildChunks(map);
            foreach (var chunk in chunks)
            {
                AddRenderer($"Chunk {chunk.ChunkX},{chunk.ChunkZ}", terrainRoot, Track(chunk.Mesh), _materials.Opaque, true);
            }

            TerrainChunkCount = chunks.Count;
            AddRenderer("Skirt", terrainRoot, Track(TerrainMeshBuilder.BuildSkirt(map, -SkirtDepth)), _materials.Opaque, false);

            var colliderObject = CreateChild("Terrain Collider", terrainRoot);
            TerrainCollider = colliderObject.AddComponent<MeshCollider>();
            TerrainCollider.sharedMesh = Track(TerrainMeshBuilder.BuildColliderMesh(map));

            var waterRoot = CreateChild("Water", transform).transform;
            AddRenderer("Lake", waterRoot, Track(WaterMeshBuilder.BuildLakePlane(map)), _materials.Water, false, false);
            var rivers = WaterMeshBuilder.BuildRivers(map);
            if (rivers != null) AddRenderer("Rivers", waterRoot, Track(rivers), _materials.Water, false, false);

            var roads = RoadMeshBuilder.Build(map, out var bridgeCount);
            BridgeCount = bridgeCount;
            if (roads != null) AddRenderer("Roads", transform, Track(roads), _materials.Opaque, true);

            var forestObject = CreateChild("Forests", transform);
            _forest = forestObject.AddComponent<ForestRenderer>();
            _forest.Build(ForestPlacement.Place(map, settlements), _materials.Opaque, TerrainMeshBuilder.ChunkCells * map.CellSize);

            var settlementRoot = CreateChild("Settlements", transform).transform;
            foreach (var settlement in settlements)
            {
                var go = CreateChild("Settlement", settlementRoot);
                var view = go.AddComponent<SettlementView>();
                view.Build(map, settlement, _materials.Opaque, plans?.Invoke(settlement.SettlementId));
                _settlements[settlement.SettlementId] = view;
            }
        }

        public SettlementView FindSettlementView(SettlementId id) => _settlements.TryGetValue(id, out var view) ? view : null;

        public void Clear()
        {
            foreach (var child in _children)
            {
                if (child != null) PresentationMaterials.DestroyObject(child);
            }

            _children.Clear();
            foreach (var mesh in _meshes) PresentationMaterials.DestroyObject(mesh);
            _meshes.Clear();
            _settlements.Clear();
            _forest = null;
            TerrainCollider = null;
            TerrainChunkCount = 0;
            BridgeCount = 0;
            Map = null;
        }

        private void OnDestroy() => Clear();

        private Mesh Track(Mesh mesh)
        {
            _meshes.Add(mesh);
            return mesh;
        }

        private GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (parent == transform) _children.Add(go);
            return go;
        }

        private void AddRenderer(string name, Transform parent, Mesh mesh, Material material, bool castShadows, bool receiveShadows = true)
        {
            var go = CreateChild(name, parent);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = receiveShadows;
        }
    }
}
