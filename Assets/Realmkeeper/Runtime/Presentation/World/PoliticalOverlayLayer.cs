using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Optional political overlay for the strategic map. Above each settlement it draws a square
    /// banner in the colour of the governing faction (grey when independent), then one smaller
    /// marker per open territorial claim in the claimant's colour: a square for an unresolved or
    /// recognized claim, a diamond for a contested one. Read from query snapshots only. No
    /// colliders, so selection and the camera are unaffected. Off by default; the economic
    /// markers (<see cref="MapIndicatorLayer"/>) are separate and keep working.
    /// </summary>
    public sealed class PoliticalOverlayLayer : MonoBehaviour
    {
        /// <summary>What the overlay shows for one settlement (for tests and tooltips).</summary>
        public readonly struct Shown
        {
            public Shown(FactionId governor, int claims, int contested)
            {
                Governor = governor;
                Claims = claims;
                Contested = contested;
            }

            public FactionId Governor { get; }
            public int Claims { get; }
            public int Contested { get; }
        }

        private readonly Dictionary<SettlementId, List<Transform>> _markers = new Dictionary<SettlementId, List<Transform>>();
        private readonly Dictionary<SettlementId, List<bool>> _diamond = new Dictionary<SettlementId, List<bool>>();
        private readonly Dictionary<SettlementId, Shown> _shown = new Dictionary<SettlementId, Shown>();
        private readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private WorldView _worldView;
        private Camera _camera;
        private Shader _shader;

        /// <summary>Banner size as a fraction of the orthographic camera's half-height.</summary>
        public float ScreenFraction { get; set; } = 0.03f;

        public bool IsOn { get; private set; }

        public int MarkerCount
        {
            get
            {
                var n = 0;
                foreach (var list in _markers.Values) n += list.Count;
                return n;
            }
        }

        public static PoliticalOverlayLayer Create(Transform parent, WorldView worldView, Camera camera)
        {
            var go = new GameObject("Political Overlay");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<PoliticalOverlayLayer>();
            layer._worldView = worldView;
            layer._camera = camera;
            layer._shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit");
            return layer;
        }

        /// <summary>What is drawn for a settlement; false when nothing is (overlay off, or unknown settlement).</summary>
        public bool TryGetShown(SettlementId id, out Shown shown) => _shown.TryGetValue(id, out shown);

        /// <summary>The colour used for a faction's banner.</summary>
        public static Color ColorOf(IReadOnlyList<FactionSummary> factions, FactionId id)
        {
            for (var i = 0; i < factions.Count; i++)
            {
                if (factions[i].FactionId == id) return FactionPalette.ColorFor(i);
            }

            return FactionPalette.Independent;
        }

        /// <summary>Switches the overlay on or off and redraws it from the session.</summary>
        public void SetOn(bool on, WorldSession session)
        {
            IsOn = on;
            Refresh(session);
        }

        /// <summary>Redraws from the latest snapshots (call after each world change). Draws nothing while off.</summary>
        public void Refresh(WorldSession session)
        {
            Clear();
            if (!IsOn || session == null || !session.HasWorld || _worldView == null) return;
            var factions = session.GetFactions();
            foreach (var s in session.GetSettlements())
            {
                var view = _worldView.FindSettlementView(s.SettlementId);
                var politics = session.GetSettlementPolitics(s.SettlementId);
                if (view == null || politics == null) continue;

                var list = new List<Transform>();
                var diamonds = new List<bool>();
                list.Add(Marker($"Governor {s.Name}", ColorOf(factions, politics.GoverningFactionId)));
                diamonds.Add(false);
                var contested = 0;
                foreach (var c in politics.OpenClaims)
                {
                    var isContested = c.Status == ClaimStatus.Contested;
                    if (isContested) contested++;
                    list.Add(Marker($"Claim {c.ClaimantName} on {s.Name}", ColorOf(factions, c.ClaimantId)));
                    diamonds.Add(isContested);
                }

                _markers[s.SettlementId] = list;
                _diamond[s.SettlementId] = diamonds;
                _shown[s.SettlementId] = new Shown(politics.GoverningFactionId, politics.OpenClaims.Count, contested);
                Place(s.SettlementId);
            }
        }

        public void Clear()
        {
            foreach (var list in _markers.Values)
            {
                foreach (var t in list)
                {
                    if (t != null) PresentationMaterials.DestroyObject(t.gameObject);
                }
            }

            _markers.Clear();
            _diamond.Clear();
            _shown.Clear();
        }

        private Transform Marker(string name, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
            marker.name = name;
            Object.DestroyImmediate(marker.GetComponent<Collider>()); // never in the way of a selection raycast
            marker.transform.SetParent(transform, false);
            var renderer = marker.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return marker.transform;
        }

        private Material MaterialFor(Color color)
        {
            if (_materials.TryGetValue(color, out var m)) return m;
            m = new Material(_shader) { name = "Political " + ColorUtility.ToHtmlStringRGB(color) };
            m.SetColor("_BaseColor", color);
            _materials[color] = m;
            return m;
        }

        private void LateUpdate()
        {
            foreach (var id in _markers.Keys) Place(id);
        }

        /// <summary>Banner first, claims to its right, all facing the camera at a constant screen size, below the economic markers.</summary>
        private void Place(SettlementId id)
        {
            if (_camera == null || _worldView == null || !_markers.TryGetValue(id, out var list) || list.Count == 0) return;
            var view = _worldView.FindSettlementView(id);
            if (view == null) return;

            var size = (_camera.orthographic ? _camera.orthographicSize : 40f) * ScreenFraction * 2f;
            var top = view.GetComponent<Collider>() is Collider c ? c.bounds.max.y : view.Center.y;
            var right = _camera.transform.right;
            var up = _camera.transform.up;
            var anchor = new Vector3(view.Center.x, top, view.Center.z) + up * (size * 0.45f) - right * (size * 1.6f);
            var facing = Quaternion.LookRotation(_camera.transform.forward, up);
            var diamonds = _diamond[id];
            for (var i = 0; i < list.Count; i++)
            {
                var scale = i == 0 ? size : size * 0.6f;
                var offset = i == 0 ? 0f : size * (0.35f + 0.75f * i);
                list[i].SetPositionAndRotation(anchor + right * offset, diamonds[i] ? facing * Quaternion.Euler(0f, 0f, 45f) : facing);
                list[i].localScale = Vector3.one * scale;
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
