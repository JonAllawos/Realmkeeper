using System.Collections.Generic;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 8: the settlement's structures in the street. Each building the world records as a structure (other than
    /// homes) gets a placeholder sign above its door naming it and saying whether it is functional, informational or
    /// decorative. Informational and decorative structures without a site of their own are listed in the settlement
    /// inspector only (no building is invented for them).
    /// </summary>
    public sealed partial class ExplorationController
    {
        private readonly List<GameObject> _signs = new List<GameObject>();

        public IReadOnlyList<GameObject> StructureSigns => _signs;

        /// <summary>A world-space label (the built-in runtime font; placeholder presentation).</summary>
        internal static TextMesh WorldText(GameObject go, string value, Color colour, float size)
        {
            var text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            text.text = value;
            text.characterSize = size;
            text.fontSize = 56;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = colour;
            return text;
        }

        private void SetupStructureSigns(LocalEnvironmentLayout layout)
        {
            _signs.Clear();
            if (_session == null || EnvironmentRoot == null) return;
            var byBuilding = new Dictionary<string, List<string>>();
            foreach (var s in _session.GetStructures(layout.SettlementId))
            {
                if (string.IsNullOrEmpty(s.LayoutKey) || s.Category == StructureCategory.Housing) continue;
                if (!byBuilding.TryGetValue(s.LayoutKey, out var names)) byBuilding[s.LayoutKey] = names = new List<string>();
                names.Add(s.Name);
            }

            var root = new GameObject("Structure signs").transform;
            foreach (var pair in byBuilding)
            {
                var b = LocalPaths.Find(layout, pair.Key);
                if (b == null) continue;
                var door = LocalPaths.DoorOutside(b);
                var sign = new GameObject("Sign " + pair.Key);
                sign.transform.SetParent(root, false);
                sign.transform.position = new Vector3(door.X, 3.1f, door.Z);
                sign.transform.rotation = Quaternion.Euler(0f, LocalPaths.FacingOut(b) + 180f, 0f);
                WorldText(sign, string.Join(" · ", pair.Value), new Color(0.95f, 0.86f, 0.62f), 0.05f);
                _signs.Add(sign);
            }
        }
    }
}
