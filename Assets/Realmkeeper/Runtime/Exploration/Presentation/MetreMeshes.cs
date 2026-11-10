using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Procedural meshes whose UVs are in metres, so a tiling material (tiling = 1 / tile size)
    /// keeps the same texel density on any size: ground, streets, the plaza, stone walls, simple
    /// furniture. Every mesh made here is registered with the owning <see cref="GeneratedAssets"/>
    /// so it is destroyed with the environment.
    /// </summary>
    public static class MetreMeshes
    {
        /// <summary>An axis-aligned box centred on the origin, UVs in metres on every face.</summary>
        public static Mesh Box(Vector3 size, GeneratedAssets owner)
        {
            var h = size * 0.5f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();

            void Face(Vector3 normal, Vector3 u, Vector3 w, float uLen, float wLen)
            {
                var start = v.Count;
                var centre = Vector3.Scale(normal, h);
                var du = u * (uLen / 2f);
                var dw = w * (wLen / 2f);
                v.Add(centre - du - dw); uv.Add(new Vector2(0f, 0f));
                v.Add(centre + du - dw); uv.Add(new Vector2(uLen, 0f));
                v.Add(centre + du + dw); uv.Add(new Vector2(uLen, wLen));
                v.Add(centre - du + dw); uv.Add(new Vector2(0f, wLen));
                for (var i = 0; i < 4; i++) n.Add(normal);
                t.Add(start); t.Add(start + 2); t.Add(start + 1);
                t.Add(start); t.Add(start + 3); t.Add(start + 2);
            }

            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
            return Finish("Box", v, n, uv, t, owner);
        }

        /// <summary>A flat rectangle on the XZ plane centred on the origin, facing up.</summary>
        public static Mesh Plane(float width, float length, GeneratedAssets owner, float uvOffsetX = 0f, float uvOffsetZ = 0f)
        {
            var w = width / 2f;
            var l = length / 2f;
            var v = new List<Vector3> { new Vector3(-w, 0f, -l), new Vector3(w, 0f, -l), new Vector3(w, 0f, l), new Vector3(-w, 0f, l) };
            var uv = new List<Vector2>
            {
                new Vector2(uvOffsetX, uvOffsetZ), new Vector2(uvOffsetX + width, uvOffsetZ),
                new Vector2(uvOffsetX + width, uvOffsetZ + length), new Vector2(uvOffsetX, uvOffsetZ + length),
            };
            var n = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            return Finish("Plane", v, n, uv, new List<int> { 0, 2, 1, 0, 3, 2 }, owner);
        }

        /// <summary>A flat disc on the XZ plane, planar UVs in metres.</summary>
        public static Mesh Disc(float radius, int segments, GeneratedAssets owner)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { Vector2.zero };
            var n = new List<Vector3> { Vector3.up };
            var t = new List<int>();
            for (var i = 0; i <= segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                var p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                v.Add(p);
                uv.Add(new Vector2(p.x, p.z));
                n.Add(Vector3.up);
                if (i > 0)
                {
                    t.Add(0);
                    t.Add(i + 1);
                    t.Add(i);
                }
            }

            return Finish("Disc", v, n, uv, t, owner);
        }

        /// <summary>
        /// A closed solid of revolution about +Y (barrels, well rings, posts): <paramref name="profile"/>
        /// lists (radius, height) from bottom to top; UVs wrap in metres around and up.
        /// </summary>
        public static Mesh Lathe(IReadOnlyList<Vector2> profile, int segments, GeneratedAssets owner, bool capTop = true)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            var maxR = 0f;
            foreach (var p in profile) maxR = Mathf.Max(maxR, p.x);
            var circumference = Mathf.PI * 2f * maxR;
            for (var k = 0; k < profile.Count - 1; k++)
            {
                var a = profile[k];
                var b = profile[k + 1];
                var slope = new Vector2(b.y - a.y, -(b.x - a.x)).normalized; // outward normal in (r, y)
                var start = v.Count;
                for (var i = 0; i <= segments; i++)
                {
                    var ang = i * Mathf.PI * 2f / segments;
                    var dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    v.Add(dir * a.x + Vector3.up * a.y);
                    v.Add(dir * b.x + Vector3.up * b.y);
                    var normal = (dir * slope.x + Vector3.up * slope.y).normalized;
                    n.Add(normal);
                    n.Add(normal);
                    var u = circumference * i / segments;
                    uv.Add(new Vector2(u, a.y));
                    uv.Add(new Vector2(u, b.y));
                }

                for (var i = 0; i < segments; i++)
                {
                    var s = start + i * 2;
                    t.Add(s); t.Add(s + 1); t.Add(s + 3);
                    t.Add(s); t.Add(s + 3); t.Add(s + 2);
                }
            }

            if (capTop)
            {
                var top = profile[profile.Count - 1];
                var centre = v.Count;
                v.Add(Vector3.up * top.y);
                n.Add(Vector3.up);
                uv.Add(Vector2.zero);
                for (var i = 0; i <= segments; i++)
                {
                    var ang = i * Mathf.PI * 2f / segments;
                    v.Add(new Vector3(Mathf.Cos(ang) * top.x, top.y, Mathf.Sin(ang) * top.x));
                    n.Add(Vector3.up);
                    uv.Add(new Vector2(Mathf.Cos(ang) * top.x, Mathf.Sin(ang) * top.x));
                    if (i > 0)
                    {
                        t.Add(centre);
                        t.Add(centre + i + 1);
                        t.Add(centre + i);
                    }
                }
            }

            return Finish("Lathe", v, n, uv, t, owner);
        }

        private static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, GeneratedAssets owner)
        {
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            owner?.Track(mesh);
            return mesh;
        }
    }

    /// <summary>Destroys meshes and materials made for one environment when it is unloaded.</summary>
    public sealed class GeneratedAssets : MonoBehaviour
    {
        private readonly List<Object> _owned = new List<Object>();

        public int Count => _owned.Count;

        public T Track<T>(T asset) where T : Object
        {
            if (asset != null) _owned.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            foreach (var o in _owned)
            {
                if (o == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(o);
                else DestroyImmediate(o);
            }

            _owned.Clear();
        }
    }
}
