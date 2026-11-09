using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Accumulates flat-shaded, vertex-coloured geometry and turns it into a Mesh.
    /// Faces are given as convex polygons plus an outward hint; winding is fixed up
    /// automatically so callers never have to reason about Unity's clockwise convention.
    /// </summary>
    public sealed class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        public int VertexCount => _vertices.Count;

        public int TriangleCount => _triangles.Count / 3;

        /// <summary>Adds a convex polygon with one flat normal facing the side of <paramref name="outward"/>.</summary>
        public void AddFace(IReadOnlyList<Vector3> polygon, Vector3 outward, Color color)
        {
            if (polygon.Count < 3) return;
            var normal = Vector3.Cross(polygon[1] - polygon[0], polygon[2] - polygon[0]);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            var flip = Vector3.Dot(normal, outward) < 0f;
            if (flip) normal = -normal;

            var start = _vertices.Count;
            for (var i = 0; i < polygon.Count; i++)
            {
                _vertices.Add(polygon[flip ? polygon.Count - 1 - i : i]);
                _normals.Add(normal);
                _colors.Add(color);
            }

            for (var i = 1; i < polygon.Count - 1; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + i);
                _triangles.Add(start + i + 1);
            }
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color color)
        {
            AddFace(new[] { a, b, c, d }, outward, color);
        }

        /// <summary>Axis-aligned box (after yaw rotation) with its base centred on <paramref name="baseCenter"/>.</summary>
        public void AddBox(Vector3 baseCenter, Vector3 size, float yawDegrees, Color sides, Color top)
        {
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var hx = size.x * 0.5f;
            var hz = size.z * 0.5f;
            Vector3 P(float x, float y, float z) => baseCenter + rotation * new Vector3(x, y, z);

            var b0 = P(-hx, 0, -hz); var b1 = P(hx, 0, -hz); var b2 = P(hx, 0, hz); var b3 = P(-hx, 0, hz);
            var t0 = P(-hx, size.y, -hz); var t1 = P(hx, size.y, -hz); var t2 = P(hx, size.y, hz); var t3 = P(-hx, size.y, hz);
            var center = baseCenter + Vector3.up * (size.y * 0.5f);

            AddQuad(t0, t1, t2, t3, Vector3.up, top);
            AddQuad(b0, b1, t1, t0, Mid(b0, t1) - center, sides);
            AddQuad(b1, b2, t2, t1, Mid(b1, t2) - center, sides);
            AddQuad(b2, b3, t3, t2, Mid(b2, t3) - center, sides);
            AddQuad(b3, b0, t0, t3, Mid(b3, t0) - center, sides);
        }

        /// <summary>
        /// Gable roof: a triangular prism whose ridge runs along local Z.
        /// <paramref name="baseCenter"/> is the centre of the eaves rectangle.
        /// </summary>
        public void AddGableRoof(Vector3 baseCenter, float width, float length, float height, float yawDegrees, Color roof, Color gable)
        {
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var hx = width * 0.5f;
            var hz = length * 0.5f;
            Vector3 P(float x, float y, float z) => baseCenter + rotation * new Vector3(x, y, z);

            var l0 = P(-hx, 0, -hz); var l1 = P(-hx, 0, hz);
            var r0 = P(hx, 0, -hz); var r1 = P(hx, 0, hz);
            var k0 = P(0, height, -hz); var k1 = P(0, height, hz);
            var center = baseCenter + Vector3.up * (height * 0.3f);

            AddQuad(l0, l1, k1, k0, Mid(l0, k1) - center, roof);
            AddQuad(r0, r1, k1, k0, Mid(r0, k1) - center, roof);
            AddFace(new[] { l0, r0, k0 }, rotation * Vector3.back, gable);
            AddFace(new[] { l1, r1, k1 }, rotation * Vector3.forward, gable);
            AddQuad(l0, r0, r1, l1, Vector3.down, gable);
        }

        public void AddPyramid(Vector3 baseCenter, float halfSize, float height, float yawDegrees, Color color)
        {
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var apex = baseCenter + Vector3.up * height;
            var corners = new Vector3[4];
            for (var i = 0; i < 4; i++)
            {
                var angle = (45f + 90f * i) * Mathf.Deg2Rad;
                corners[i] = baseCenter + rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (halfSize * 1.41421356f);
            }

            var center = baseCenter + Vector3.up * (height * 0.25f);
            for (var i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                AddFace(new[] { a, b, apex }, (a + b + apex) / 3f - center, color);
            }

            AddFace(corners, Vector3.down, color);
        }

        public void AddCone(Vector3 baseCenter, float radius, float height, int segments, Color color)
        {
            var apex = baseCenter + Vector3.up * height;
            var ring = new Vector3[segments];
            for (var i = 0; i < segments; i++)
            {
                var angle = 2f * Mathf.PI * i / segments;
                ring[i] = baseCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            var center = baseCenter + Vector3.up * (height * 0.25f);
            for (var i = 0; i < segments; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % segments];
                AddFace(new[] { a, b, apex }, (a + b + apex) / 3f - center, color);
            }

            AddFace(ring, Vector3.down, color);
        }

        /// <summary>Low-poly faceted sphere (latitude/longitude), used for broadleaf canopies.</summary>
        public void AddBlob(Vector3 center, Vector3 radii, int segments, int rings, Color color)
        {
            Vector3 Point(int ring, int seg)
            {
                var v = Mathf.PI * ring / rings;
                var u = 2f * Mathf.PI * seg / segments;
                return center + new Vector3(
                    Mathf.Sin(v) * Mathf.Cos(u) * radii.x,
                    Mathf.Cos(v) * radii.y,
                    Mathf.Sin(v) * Mathf.Sin(u) * radii.z);
            }

            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var a = Point(r, s);
                    var b = Point(r, s + 1);
                    var c = Point(r + 1, s + 1);
                    var d = Point(r + 1, s);
                    var outward = (a + b + c + d) * 0.25f - center;
                    if (r == 0) AddFace(new[] { a, c, d }, outward, color);
                    else if (r == rings - 1) AddFace(new[] { a, b, c }, outward, color);
                    else AddQuad(a, b, c, d, outward, color);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Mid(Vector3 a, Vector3 b) => (a + b) * 0.5f;
    }
}
