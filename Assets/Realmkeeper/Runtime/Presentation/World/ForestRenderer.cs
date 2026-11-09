using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Draws every tree with GPU instancing. Instances are grouped by mesh and by terrain
    /// chunk so each batch has tight bounds and off-screen batches are culled.
    /// No GameObjects or colliders per tree.
    /// </summary>
    public sealed class ForestRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerBatch = 1023;

        private readonly List<Batch> _batches = new List<Batch>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private Material _material;

        public int InstanceCount { get; private set; }

        public int BatchCount => _batches.Count;

        public void Build(IReadOnlyList<TreeInstance> trees, Material material, float chunkSize)
        {
            Clear();
            _material = material;

            var meshByKey = new Dictionary<int, Mesh>();
            foreach (TreeSpecies species in System.Enum.GetValues(typeof(TreeSpecies)))
            {
                for (var variant = 0; variant < 2; variant++)
                {
                    var mesh = TreeMeshes.Build(species, variant);
                    _meshes.Add(mesh);
                    meshByKey[MeshKey(species, variant)] = mesh;
                }
            }

            var groups = new Dictionary<(int mesh, int cx, int cz), List<Matrix4x4>>();
            foreach (var tree in trees)
            {
                var key = (MeshKey(tree.Species, tree.Variant), Mathf.FloorToInt(tree.Position.x / chunkSize), Mathf.FloorToInt(tree.Position.z / chunkSize));
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<Matrix4x4>();
                list.Add(tree.Matrix);
            }

            foreach (var pair in groups)
            {
                var mesh = meshByKey[pair.Key.mesh];
                var matrices = pair.Value;
                for (var start = 0; start < matrices.Count; start += MaxInstancesPerBatch)
                {
                    var count = Mathf.Min(MaxInstancesPerBatch, matrices.Count - start);
                    var array = matrices.GetRange(start, count).ToArray();
                    _batches.Add(new Batch(mesh, array, ComputeBounds(array)));
                }
            }

            InstanceCount = trees.Count;
        }

        public void Clear()
        {
            foreach (var mesh in _meshes) PresentationMaterials.DestroyObject(mesh);
            _meshes.Clear();
            _batches.Clear();
            InstanceCount = 0;
        }

        private void Update()
        {
            if (_material == null) return;
            foreach (var batch in _batches)
            {
                var rp = new RenderParams(_material)
                {
                    worldBounds = batch.Bounds,
                    shadowCastingMode = ShadowCastingMode.On,
                    receiveShadows = true,
                    layer = gameObject.layer,
                };
                Graphics.RenderMeshInstanced(rp, batch.Mesh, 0, batch.Matrices);
            }
        }

        private void OnDestroy() => Clear();

        private static int MeshKey(TreeSpecies species, int variant) => (int)species * 2 + variant;

        private static Bounds ComputeBounds(Matrix4x4[] matrices)
        {
            var bounds = new Bounds(matrices[0].GetColumn(3), Vector3.zero);
            foreach (var m in matrices) bounds.Encapsulate((Vector3)m.GetColumn(3));
            bounds.Expand(new Vector3(4f, 8f, 4f));
            bounds.center += Vector3.up * 2f;
            return bounds;
        }

        private sealed class Batch
        {
            public Batch(Mesh mesh, Matrix4x4[] matrices, Bounds bounds)
            {
                Mesh = mesh;
                Matrices = matrices;
                Bounds = bounds;
            }

            public Mesh Mesh { get; }
            public Matrix4x4[] Matrices { get; }
            public Bounds Bounds { get; }
        }
    }
}
