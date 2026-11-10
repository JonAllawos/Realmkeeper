using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Optional political overlay for the strategic map, drawn on the ground in the map's own
    /// vertex-coloured style:
    /// - a thin ring around each governed settlement, in its governor's colour (none when independent);
    /// - a dashed ring just outside it for each open territorial claim, in the claimant's colour.
    ///   Contested claims use short, tight dashes; unresolved or recognized ones use long dashes.
    /// The rings follow the terrain, have no colliders (selection and the camera are unaffected) and
    /// are read from query snapshots only. Off by default; the economic markers are separate.
    /// </summary>
    public sealed class PoliticalOverlayLayer : MonoBehaviour
    {
        public const int Segments = 72;
        public const float RingWidth = 0.45f;
        public const float ClaimGap = 1.1f;
        public const float HeightOffset = 0.3f;

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

        private readonly List<GameObject> _rings = new List<GameObject>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Dictionary<SettlementId, Shown> _shown = new Dictionary<SettlementId, Shown>();
        private WorldView _worldView;
        private Material _material;

        public bool IsOn { get; private set; }

        /// <summary>Number of ring objects drawn (governor rings plus claim rings).</summary>
        public int MarkerCount => _rings.Count;

        /// <param name="material">A vertex-colour material (the map's opaque material), so rings match the terrain's lighting.</param>
        public static PoliticalOverlayLayer Create(Transform parent, WorldView worldView, Material material)
        {
            var go = new GameObject("Political Overlay");
            go.transform.SetParent(parent, false);
            var layer = go.AddComponent<PoliticalOverlayLayer>();
            layer._worldView = worldView;
            layer._material = material;
            return layer;
        }

        /// <summary>What is drawn for a settlement; false when nothing is (overlay off, or unknown settlement).</summary>
        public bool TryGetShown(SettlementId id, out Shown shown) => _shown.TryGetValue(id, out shown);

        /// <summary>The colour used for a faction (by its position in the world's faction list).</summary>
        public static Color ColorOf(IReadOnlyList<FactionSummary> factions, FactionId id)
        {
            for (var i = 0; i < factions.Count; i++)
            {
                if (factions[i].FactionId == id) return FactionPalette.ColorFor(i);
            }

            return FactionPalette.Independent;
        }

        public void SetOn(bool on, WorldSession session)
        {
            IsOn = on;
            Refresh(session);
        }

        /// <summary>Redraws from the latest snapshots (call after each world change). Draws nothing while off.</summary>
        public void Refresh(WorldSession session)
        {
            Clear();
            var map = _worldView != null ? _worldView.Map : null;
            if (!IsOn || session == null || !session.HasWorld || map == null) return;
            var factions = session.GetFactions();
            foreach (var s in session.GetSettlements())
            {
                var view = _worldView.FindSettlementView(s.SettlementId);
                var politics = session.GetSettlementPolitics(s.SettlementId);
                if (view == null || politics == null) continue;

                var radius = view.HighlightRadius + 0.9f;
                if (!politics.IsIndependent) Ring($"Governed {s.Name}", map, view.Center, radius, ColorOf(factions, politics.GoverningFactionId), 0);

                var contested = 0;
                var index = 0;
                foreach (var c in politics.OpenClaims)
                {
                    index++;
                    var isContested = c.Status == ClaimStatus.Contested;
                    if (isContested) contested++;
                    Ring($"Claim {c.ClaimantName} on {s.Name}", map, view.Center, radius + ClaimGap * index, ColorOf(factions, c.ClaimantId), isContested ? 2 : 4);
                }

                _shown[s.SettlementId] = new Shown(politics.GoverningFactionId, politics.OpenClaims.Count, contested);
            }
        }

        public void Clear()
        {
            foreach (var go in _rings)
            {
                if (go != null) PresentationMaterials.DestroyObject(go);
            }

            foreach (var mesh in _meshes) PresentationMaterials.DestroyObject(mesh);
            _rings.Clear();
            _meshes.Clear();
            _shown.Clear();
        }

        /// <param name="dash">0: solid. Otherwise segments drawn per gap (2 = short, 4 = long dashes).</param>
        private void Ring(string name, RegionMap map, Vector3 center, float radius, Color color, int dash)
        {
            var builder = new MeshBuilder();
            var c = color.linear;
            Vector3 Point(int i, float r)
            {
                var angle = 2f * Mathf.PI * i / Segments;
                var x = center.x + Mathf.Cos(angle) * r;
                var z = center.z + Mathf.Sin(angle) * r;
                return new Vector3(x, MapSampling.HeightAt(map, x, z) + HeightOffset, z);
            }

            var inner = radius - RingWidth * 0.5f;
            var outer = radius + RingWidth * 0.5f;
            for (var i = 0; i < Segments; i++)
            {
                if (dash > 0 && i % (dash + 1) == dash) continue; // the gaps
                builder.AddQuad(Point(i, inner), Point(i, outer), Point(i + 1, outer), Point(i + 1, inner), Vector3.up, c);
            }

            var mesh = builder.ToMesh(name);
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            _rings.Add(go);
            _meshes.Add(mesh);
        }

        private void OnDestroy() => Clear();
    }
}
