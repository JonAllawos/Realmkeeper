using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Merges the static geometry under a root into one mesh per (spatial cell, material, shadow mode):
    /// the kit assembles a town from thousands of small pieces, and merging turns them into a few
    /// hundred draws (the budget is 1,500 in a town scene). Cells keep culling useful. Deterministic:
    /// renderers are visited in hierarchy order and cells in key order. Lights, colliders and other
    /// components stay where they were; only the merged renderers are removed.
    /// </summary>
    public static class MeshMerger
    {
        // Visual pass (2026-11-16): 48 m, not 24. Planned, walled towns (Phases 26–28) are far larger than the towns this was
        // tuned for, and 24 m cells left about 1,250 merged renderers and 3,000–4,700 draws a frame against the 1,500 budget.
        public const float CellSize = 48f;

        public static int Merge(Transform root, GeneratedAssets owner)
        {
            var groups = new SortedDictionary<string, Group>(StringComparer.Ordinal);
            var merged = new List<MeshRenderer>();
            var toLocal = root.worldToLocalMatrix;

            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable || !renderer.enabled) continue;
                var materials = renderer.sharedMaterials;
                var centre = renderer.bounds.center;
                var cell = $"{Mathf.FloorToInt(centre.x / CellSize)}:{Mathf.FloorToInt(centre.z / CellSize)}";
                var matrix = toLocal * renderer.transform.localToWorldMatrix;
                for (var sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
                {
                    var material = materials[sub];
                    if (material == null) continue;
                    var key = $"{cell}|{material.name}#{material.GetInstanceID()}|{(int)renderer.shadowCastingMode}";
                    if (!groups.TryGetValue(key, out var group))
                    {
                        group = new Group { Material = material, Shadows = renderer.shadowCastingMode, Name = cell + " " + material.name };
                        groups.Add(key, group);
                    }

                    group.Parts.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = matrix });
                }

                merged.Add(renderer);
            }

            var container = new GameObject("Merged").transform;
            container.SetParent(root, false);
            foreach (var group in groups.Values)
            {
                var mesh = new Mesh { name = "Merged " + group.Name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Parts.ToArray(), true, true, false);
                mesh.RecalculateBounds();
                owner.Track(mesh);
                var go = new GameObject(group.Name);
                go.transform.SetParent(container, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = group.Material;
                r.shadowCastingMode = group.Shadows;
            }

            foreach (var r in merged)
            {
                var filter = r.GetComponent<MeshFilter>();
                Destroy(r);
                Destroy(filter);
            }

            return groups.Count;
        }

        private static void Destroy(Object o)
        {
            if (o == null) return;
            if (UnityEngine.Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }

        private sealed class Group
        {
            public string Name;
            public Material Material;
            public ShadowCastingMode Shadows;
            public readonly List<CombineInstance> Parts = new List<CombineInstance>();
        }
    }
}
