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
            if (InPlace) return; // Phase 13
            if (_session == null || EnvironmentRoot == null) return;
            var byBuilding = new Dictionary<string, List<string>>();
            var categories = new Dictionary<string, StructureCategory>();
            foreach (var s in _session.GetStructures(layout.SettlementId))
            {
                if (string.IsNullOrEmpty(s.LayoutKey) || s.Category == StructureCategory.Housing) continue;
                if (!byBuilding.TryGetValue(s.LayoutKey, out var names)) byBuilding[s.LayoutKey] = names = new List<string>();
                names.Add(s.Name);
                if (!categories.ContainsKey(s.LayoutKey)) categories[s.LayoutKey] = s.Category;
            }

            var root = new GameObject("Structure signs").transform;
            foreach (var pair in byBuilding)
            {
                var b = LocalPaths.Find(layout, pair.Key);
                if (b == null) continue;
                // Phase 27 (owner: no floating lettered names): a painted signboard on the wall beside the door. Shops and the
                // tavern carry their trade's sign from the building kit; every other public building shows its emblem here.
                var sign = new GameObject("Sign " + pair.Key);
                sign.transform.SetParent(root, false);
                sign.transform.position = EnvironmentRoot.transform.TransformPoint(new Vector3(b.Center.X, b.FloorHeight, b.Center.Z));
                sign.transform.rotation = EnvironmentRoot.transform.rotation * Quaternion.Euler(0f, -b.Yaw, 0f);
                if (b.Fit == ShopFit.None && b.Kind != BuildingKind.Hall)
                    PaintedSign.Hang(sign.transform, new Vector3(LocalPaths.DoorwayX(b) + 1.35f, 2.6f, -b.Depth / 2f), PaintedSign.EmblemFor(categories[pair.Key]), Catalog != null ? Catalog.Material("Planks") : null, Catalog != null ? Catalog.Material("Metal") : null);
                _signs.Add(sign);
            }
        }
    }
}

namespace Realmkeeper.Exploration.Presentation
{
    using Realmkeeper.Application;
    using Realmkeeper.Domain.Rpg;

    public sealed partial class ExplorationController
    {
        /// <summary>Phase 9: a short or long rest from the Hub. The world clock moves (an hour or eight), then recovery applies.</summary>
        public RestResult Rest(RestKind kind, int hitDice)
        {
            if (_session == null) return new RestResult { Message = "No world session is active." };
            Hud?.HideResponse();
            TalkingTo = null;
            var r = kind == RestKind.Short ? _session.ShortRest(hitDice) : _session.LongRest();
            LastRest = r;
            RefreshLiving(snap: true);
            ShowHub(r.IsSuccess ? r.Message + " " + string.Join(" ", r.Lines) : r.Message, !r.IsSuccess);
            HubWindow?.Select("Spells & rest");
            return r;
        }

        /// <summary>The last rest's outcome (tests and captures read it).</summary>
        public RestResult LastRest { get; private set; }
    }
}
