using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Small coloured diamonds above settlements on the strategic map, one per condition in
    /// <see cref="SettlementSummary.Indicators"/>: food shortage (red), strong production (green),
    /// active trade (blue), declining stability (amber). They are read from query snapshots
    /// (authoritative simulation data) and hold no state of their own. They face the camera and
    /// keep a constant size on screen. They have no colliders, so they never block selection.
    /// </summary>
    public sealed class MapIndicatorLayer : MonoBehaviour
    {
        public static readonly Color FoodShortageColor = new Color(0.86f, 0.24f, 0.18f);
        public static readonly Color StrongProductionColor = new Color(0.42f, 0.74f, 0.32f);
        public static readonly Color ActiveTradeColor = new Color(0.30f, 0.56f, 0.86f);
        public static readonly Color DecliningStabilityColor = new Color(0.92f, 0.66f, 0.18f);

        private static readonly (SettlementIndicators flag, Color color)[] Order =
        {
            (SettlementIndicators.FoodShortage, FoodShortageColor),
            (SettlementIndicators.DecliningStability, DecliningStabilityColor),
            (SettlementIndicators.StrongProduction, StrongProductionColor),
            (SettlementIndicators.ActiveTrade, ActiveTradeColor),
        };

        private readonly Dictionary<SettlementId, List<Transform>> _markers = new Dictionary<SettlementId, List<Transform>>();
        private readonly Dictionary<SettlementId, SettlementIndicators> _shown = new Dictionary<SettlementId, SettlementIndicators>();
        private readonly Dictionary<SettlementIndicators, Material> _materials = new Dictionary<SettlementIndicators, Material>();
        private WorldView _worldView;
        private Camera _camera;

        /// <summary>Marker size as a fraction of the orthographic camera's half-height.</summary>
        public float ScreenFraction { get; set; } = 0.022f;

        public int MarkerCount
        {
            get
            {
                var n = 0;
                foreach (var list in _markers.Values) n += list.Count;
                return n;
            }
        }

        public static MapIndicatorLayer Create(Transform parent, WorldView worldView, Camera camera)
        {
            var go = new GameObject("Map Indicators");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<MapIndicatorLayer>();
            layer._worldView = worldView;
            layer._camera = camera;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            foreach (var entry in Order)
            {
                var m = new Material(shader) { name = "Indicator " + entry.flag };
                m.SetColor("_BaseColor", entry.color);
                layer._materials[entry.flag] = m;
            }

            return layer;
        }

        /// <summary>The conditions currently drawn for a settlement.</summary>
        public SettlementIndicators ShownFor(SettlementId id) => _shown.TryGetValue(id, out var flags) ? flags : SettlementIndicators.None;

        /// <summary>Redraws markers from the latest settlement snapshots (call after each world change).</summary>
        public void Refresh(IReadOnlyList<SettlementSummary> settlements)
        {
            var seen = new HashSet<SettlementId>();
            if (settlements != null)
            {
                foreach (var s in settlements)
                {
                    seen.Add(s.SettlementId);
                    if (ShownFor(s.SettlementId) == s.Indicators && _markers.ContainsKey(s.SettlementId)) continue;
                    Rebuild(s.SettlementId, s.Indicators);
                }
            }

            var stale = new List<SettlementId>();
            foreach (var id in _markers.Keys)
            {
                if (!seen.Contains(id)) stale.Add(id);
            }

            foreach (var id in stale) Remove(id);
        }

        public void Clear()
        {
            foreach (var id in new List<SettlementId>(_markers.Keys)) Remove(id);
        }

        private void Rebuild(SettlementId id, SettlementIndicators flags)
        {
            Remove(id);
            var view = _worldView != null ? _worldView.FindSettlementView(id) : null;
            var list = new List<Transform>();
            if (view != null)
            {
                foreach (var entry in Order)
                {
                    if ((flags & entry.flag) == 0) continue;
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    marker.name = $"Indicator {entry.flag} {id}";
                    UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>()); // immediately: never in the way of a selection raycast
                    marker.transform.SetParent(transform, false);
                    var renderer = marker.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = _materials[entry.flag];
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    list.Add(marker.transform);
                }
            }

            _markers[id] = list;
            _shown[id] = view != null ? flags : SettlementIndicators.None;
            Place(id, list);
        }

        private void Remove(SettlementId id)
        {
            if (_markers.TryGetValue(id, out var list))
            {
                foreach (var t in list)
                {
                    if (t != null) PresentationMaterials.DestroyObject(t.gameObject);
                }
            }

            _markers.Remove(id);
            _shown.Remove(id);
        }

        private void LateUpdate()
        {
            foreach (var pair in _markers) Place(pair.Key, pair.Value);
        }

        /// <summary>Lines markers up above the settlement, facing the camera, at a constant screen size.</summary>
        private void Place(SettlementId id, List<Transform> list)
        {
            if (list.Count == 0 || _camera == null || _worldView == null) return;
            var view = _worldView.FindSettlementView(id);
            if (view == null) return;

            var size = (_camera.orthographic ? _camera.orthographicSize : 40f) * ScreenFraction * 2f;
            var top = view.GetComponent<Collider>() is Collider c ? c.bounds.max.y : view.Center.y;
            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var anchor = new Vector3(view.Center.x, top, view.Center.z) + up * (size * 1.6f);
            var rotation = Quaternion.LookRotation(_camera.transform.forward, up) * Quaternion.Euler(0f, 0f, 45f);
            for (var i = 0; i < list.Count; i++)
            {
                var offset = (i - (list.Count - 1) / 2f) * size * 1.5f;
                list[i].SetPositionAndRotation(anchor + right * offset, rotation);
                list[i].localScale = Vector3.one * size;
            }
        }

        private void OnDestroy()
        {
            Clear();
            foreach (var m in _materials.Values) PresentationMaterials.DestroyObject(m);
            _materials.Clear();
        }
    }
}
