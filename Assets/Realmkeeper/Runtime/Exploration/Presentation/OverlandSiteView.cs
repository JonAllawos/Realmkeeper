using System;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 23 (ADR-0110): a site in the country — a settlement's silhouette and way in, a place's entrance, a gateway, the
    /// signpost of a road on — and the interaction at it. The controller performs what is chosen (through a command, behind
    /// the loading screen); the view holds no state.
    /// </summary>
    public sealed class OverlandSiteView : MonoBehaviour, IInteractable, IConditionalInteractable
    {
        public OverlandSite Site { get; private set; }

        /// <summary>What happens when the player interacts (the controller's transition).</summary>
        public Action<OverlandSiteView> Handler { get; set; }

        public string Prompt
        {
            get
            {
                switch (Site.Kind)
                {
                    case OverlandSiteKind.Settlement: return $"Enter {Site.Name} ({Site.KindText})";
                    case OverlandSiteKind.Place: return Site.Known ? $"Go into {Site.Name} ({Site.KindText})" : "Something is here: look closer";
                    case OverlandSiteKind.Gateway: return Site.Usable ? $"Pass through {Site.Name}" : $"{Site.Name}: {Site.Why}";
                    default: return Site.Usable ? Site.Name + " (" + Site.KindText + ")" : Site.Name + ": " + Site.Why;
                }
            }
        }

        public InteractionKind Kind => InteractionKind.StateChanging;
        public Vector3 InteractionPoint => transform.position + Vector3.up * 1.2f;
        public bool IsAvailable => Site.Kind != OverlandSiteKind.Place || Site.Known;

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            Handler?.Invoke(this);
            return new InteractionResponse(Site.Name, string.Empty, Kind);
        }

        public void Refresh(OverlandSite site) => Site = site;

        public static OverlandSiteView Create(Transform parent, OverlandSite site, OverlandTerrain terrain, LocalEnvironmentLayout layout, ArtCatalog catalog, Material stone, Material wood, Material glow, Material roof)
        {
            var go = new GameObject("Site — " + site.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(site.X, terrain.HeightAt(site.X, site.Z), site.Z);
            var view = go.AddComponent<OverlandSiteView>();
            view.Site = site;
            switch (site.Kind)
            {
                case OverlandSiteKind.Settlement: Settlement(go.transform, site, terrain, layout, wood, roof, stone); break;
                case OverlandSiteKind.Place: Entrance(go.transform, catalog, stone, wood); break;
                case OverlandSiteKind.Gateway: Gateway(go.transform, site, stone, glow); break;
                default: Signpost(go.transform, wood, stone); break;
            }

            return view;
        }

        private static GameObject Box(Transform parent, Vector3 local, Vector3 size, Material m, string name = "Part", float yaw = 0f)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            UnityEngine.Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = local;
            b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = m;
            return b;
        }

        /// <summary>
        /// The settlement as seen from the country: each building of its real layout as a block with a roof, at its true
        /// place and size, on the ground (a low-detail silhouette; entering loads the town itself), and a banner at the way in.
        /// </summary>
        private static void Settlement(Transform root, OverlandSite site, OverlandTerrain terrain, LocalEnvironmentLayout layout, Material walls, Material roof, Material stone)
        {
            var centre = OverlandScale.CellCentre(site.Cell);
            if (layout != null)
            {
                var town = new GameObject("Silhouette").transform;
                town.SetParent(root.parent, false);
                foreach (var b in layout.Buildings)
                {
                    var x = centre.x + b.Center.X;
                    var z = centre.z + b.Center.Z;
                    if ((x - site.X) * (x - site.X) + (z - site.Z) * (z - site.Z) < 64f) continue; // keep the way in clear
                    var h = Mathf.Max(3f, b.Height);
                    var y = terrain.HeightAt(x, z);
                    var house = new GameObject("Building").transform;
                    house.SetParent(town, false);
                    house.position = new Vector3(x, y, z);
                    house.rotation = Quaternion.Euler(0, b.Yaw, 0);
                    Box(house, new Vector3(0, h / 2 - 0.5f, 0), new Vector3(b.Width, h, b.Depth), walls, "Walls");
                    var r = Box(house, new Vector3(0, h + 0.9f, 0), new Vector3(b.Width * 0.72f, b.Width * 0.72f, b.Depth * 1.02f), roof, "Roof");
                    r.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                }

                if (layout.HasWalls)
                {
                    var ring = Mathf.Max(30f, layout.HalfExtent * 0.85f);
                    for (var a = 0; a < 360; a += 6)
                    {
                        var rad = a * Mathf.Deg2Rad;
                        var x = centre.x + Mathf.Cos(rad) * ring;
                        var z = centre.z + Mathf.Sin(rad) * ring;
                        if ((x - site.X) * (x - site.X) + (z - site.Z) * (z - site.Z) < 400f) continue;
                        var post = new GameObject("Palisade").transform;
                        post.SetParent(town, false);
                        post.position = new Vector3(x, terrain.HeightAt(x, z), z);
                        Box(post, new Vector3(0, 2.5f, 0), new Vector3(1.2f, 5f, ring * 0.11f), walls, "Wall", -a);
                    }
                }
            }

            Box(root, new Vector3(0, 4f, 0), new Vector3(0.3f, 8f, 0.3f), walls, "Banner pole");
            Box(root, new Vector3(0.9f, 6.6f, 0), new Vector3(1.6f, 2.2f, 0.06f), roof, "Banner");
        }

        private static void Entrance(Transform root, ArtCatalog catalog, Material stone, Material wood)
        {
            var model = catalog?.SceneryModel("Rock_Medium_2") ?? catalog?.SceneryModel("Rock_Medium_1");
            if (model != null)
            {
                foreach (var side in new[] { -2.6f, 2.6f })
                {
                    var rock = UnityEngine.Object.Instantiate(model, root, false);
                    rock.transform.localPosition = new Vector3(side, 0f, 0f);
                    rock.transform.localScale = Vector3.one * 2.2f;
                    foreach (var c in rock.GetComponentsInChildren<Collider>()) UnityEngine.Object.Destroy(c);
                }
            }
            else
            {
                Box(root, new Vector3(-2.4f, 1.6f, 0), new Vector3(1.6f, 3.2f, 2f), stone);
                Box(root, new Vector3(2.4f, 1.6f, 0), new Vector3(1.6f, 3.2f, 2f), stone);
            }

            Box(root, new Vector3(0, 3.6f, 0), new Vector3(6.4f, 0.8f, 2f), stone, "Lintel");
            Box(root, new Vector3(1.6f, 1.2f, -1.6f), new Vector3(0.15f, 2.4f, 0.15f), wood, "Torch post");
        }

        private static void Gateway(Transform root, OverlandSite site, Material stone, Material glow)
        {
            for (var i = 0; i < 8; i++)
            {
                var a = i * 45f * Mathf.Deg2Rad;
                Box(root, new Vector3(Mathf.Cos(a) * 7f, 3.2f, Mathf.Sin(a) * 7f), new Vector3(1.1f, 6.4f + (i % 2) * 1.4f, 1.1f), stone, "Standing stone", i * 45f);
            }

            if (site.Usable)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                UnityEngine.Object.Destroy(disc.GetComponent<Collider>());
                disc.name = "Open gateway";
                disc.transform.SetParent(root, false);
                disc.transform.localPosition = new Vector3(0, 0.15f, 0);
                disc.transform.localScale = new Vector3(11f, 0.08f, 11f);
                disc.GetComponent<MeshRenderer>().sharedMaterial = glow;
                var light = new GameObject("Gateway light").AddComponent<Light>();
                light.transform.SetParent(root, false);
                light.transform.localPosition = new Vector3(0, 3f, 0);
                light.type = LightType.Point;
                light.range = 22f;
                light.intensity = 3f;
                light.color = new Color(0.62f, 0.45f, 0.95f);
            }
        }

        private static void Signpost(Transform root, Material wood, Material stone)
        {
            Box(root, new Vector3(0, 1.6f, 0), new Vector3(0.18f, 3.2f, 0.18f), wood, "Post");
            Box(root, new Vector3(0.7f, 2.8f, 0), new Vector3(1.5f, 0.35f, 0.06f), wood, "Arm");
            Box(root, new Vector3(-1.4f, 0.6f, 0.4f), new Vector3(0.9f, 1.2f, 0.6f), stone, "Waystone");
        }
    }
}
