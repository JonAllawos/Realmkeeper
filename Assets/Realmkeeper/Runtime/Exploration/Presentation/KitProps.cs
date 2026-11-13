using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 21: places a CC0 kit model at a stated size, whatever its import scale — measured from its meshes in the
    /// parent's axes, scaled so its largest side is the given metres, stood on the given point. Solid ones get a box
    /// collider of their measured size. Null when the kit is not installed.
    /// </summary>
    public static class KitProps
    {
        public static GameObject Place(Transform parent, ArtCatalog catalog, string id, Vector3 at, float yaw, float size, bool solid)
        {
            var prefab = catalog != null ? catalog.SceneryModel(id) : null;
            if (prefab == null || parent == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.name = id;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            if (!Measure(parent, go, out var b) || b.size.sqrMagnitude < 1e-6f)
            {
                Object.Destroy(go);
                return null;
            }

            go.transform.localScale *= size / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            Measure(parent, go, out b);
            go.transform.localPosition += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            if (solid)
            {
                Measure(parent, go, out b);
                var col = new GameObject(id + " collider");
                col.transform.SetParent(parent, false);
                col.transform.localPosition = b.center;
                col.AddComponent<BoxCollider>().size = b.size;
            }

            return go;
        }

        /// <summary>A model's bounds in <paramref name="space"/>'s axes, from its meshes' corners.</summary>
        public static bool Measure(Transform space, GameObject go, out Bounds bounds)
        {
            bounds = default;
            var any = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = space.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }

            return any;
        }
    }
}
