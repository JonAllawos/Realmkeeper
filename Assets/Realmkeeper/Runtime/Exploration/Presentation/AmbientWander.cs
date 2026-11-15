using System;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 27: an animal grazing about its pasture. It walks slowly to a point inside its field (in the field's own frame,
    /// so it never leaves the fence), stands a while, and goes again. It does nothing beyond 90 m of the camera and checks
    /// that once a second, so a pasture full of animals costs nothing while nobody is near.
    /// </summary>
    public sealed class AmbientWander : MonoBehaviour
    {
        private Vector3 _centre;
        private float _yaw;
        private float _halfW;
        private float _halfD;
        private float _speed;
        private Func<float, float, float> _ground;
        private System.Random _rng;
        private Vector3 _target;
        private float _wait;
        private float _near;
        private bool _awake;

        public const float AwakeRange = 90f;

        /// <summary>Movement so far (metres), for tests.</summary>
        public float Travelled { get; private set; }

        public void Setup(Vector3 fieldCentre, float fieldYaw, float halfWidth, float halfDepth, float speed, Func<float, float, float> ground, int seed)
        {
            _centre = fieldCentre;
            _yaw = fieldYaw;
            _halfW = Mathf.Max(0.5f, halfWidth);
            _halfD = Mathf.Max(0.5f, halfDepth);
            _speed = speed;
            _ground = ground;
            _rng = new System.Random(seed);
            _wait = (float)_rng.NextDouble() * 6f;
            _target = transform.localPosition;
        }

        /// <summary>Whether a local point lies inside the field (its own frame), for tests.</summary>
        public bool InsideField(Vector3 local)
        {
            var r = _yaw * Mathf.Deg2Rad;
            var dx = local.x - _centre.x;
            var dz = local.z - _centre.z;
            var fx = dx * Mathf.Cos(r) + dz * Mathf.Sin(r);
            var fz = -dx * Mathf.Sin(r) + dz * Mathf.Cos(r);
            return Mathf.Abs(fx) <= _halfW + 0.01f && Mathf.Abs(fz) <= _halfD + 0.01f;
        }

        private void Update()
        {
            if (_rng == null) return;
            _near -= Time.deltaTime;
            if (_near <= 0f)
            {
                _near = 1f;
                var cam = Camera.main;
                _awake = cam == null || (cam.transform.position - transform.position).sqrMagnitude < AwakeRange * AwakeRange;
            }

            if (!_awake) return;
            if (_wait > 0f) { _wait -= Time.deltaTime; return; }
            var here = transform.localPosition;
            var to = _target - here;
            to.y = 0f;
            if (to.magnitude < 0.1f)
            {
                // A new spot inside the field, not far from here.
                var r = _yaw * Mathf.Deg2Rad;
                var lx = Mathf.Clamp((float)(_rng.NextDouble() * 2 - 1) * _halfW, -_halfW, _halfW);
                var lz = Mathf.Clamp((float)(_rng.NextDouble() * 2 - 1) * _halfD, -_halfD, _halfD);
                _target = new Vector3(_centre.x + lx * Mathf.Cos(r) - lz * Mathf.Sin(r), 0f, _centre.z + lx * Mathf.Sin(r) + lz * Mathf.Cos(r));
                _wait = 3f + (float)_rng.NextDouble() * 9f;
                return;
            }

            var step = Mathf.Min(to.magnitude, _speed * Time.deltaTime);
            var next = here + to.normalized * step;
            next.y = _ground != null ? _ground(next.x, next.z) : here.y;
            transform.localPosition = next;
            transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.LookRotation(to.normalized, Vector3.up), 4f * Time.deltaTime);
            Travelled += step;
        }
    }
}
