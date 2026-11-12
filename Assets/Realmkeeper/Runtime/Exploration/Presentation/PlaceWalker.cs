using System.Collections.Generic;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 14 (ADR-0070): a person who has just moved between rooms walks the route the world gave them — through the
    /// doorways, never through a wall — at a walking pace, turning to face the way. Presentation only: the world already
    /// says where they are; this shows how they got there.
    /// </summary>
    public sealed class PlaceWalker : MonoBehaviour
    {
        public const float Speed = 1.4f;

        private List<Vector3> _points;
        private int _next;

        public bool Arrived => _points == null || _next >= _points.Count;

        public static PlaceWalker Walk(GameObject body, List<Vector3> points)
        {
            if (body == null || points == null || points.Count < 2) return null;
            var w = body.AddComponent<PlaceWalker>();
            w._points = points;
            w._next = 1;
            body.transform.position = points[0];
            return w;
        }

        private void Update()
        {
            if (Arrived) return;
            var target = _points[_next];
            var pos = transform.position;
            var flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            var step = Speed * Time.deltaTime;
            if (flat.magnitude <= step)
            {
                transform.position = target;
                _next++;
                return;
            }

            transform.position = pos + flat.normalized * step + Vector3.up * (target.y - pos.y) * Mathf.Clamp01(step / Mathf.Max(0.01f, flat.magnitude));
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized, Vector3.up), Mathf.Clamp01(Time.deltaTime * 8f));
        }
    }
}
