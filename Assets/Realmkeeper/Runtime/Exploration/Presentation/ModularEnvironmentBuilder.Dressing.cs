using UnityEngine;
using UnityEngine.Rendering;
using Realmkeeper.Exploration.Domain;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 27 (ADR-0119): the street dressing <see cref="StreetDressingPlanner"/> placed, drawn from the owned CC0 kits
    /// (Quaternius Fantasy Props and Medieval Village, the KayKit-style banners already in the art catalog). The settlement
    /// owner's banner takes the colour the strategic map gives that faction (its place in the world's faction list).
    /// </summary>
    public static partial class ModularEnvironmentBuilder
    {
        /// <summary>
        /// Banner models by the strategic map's faction colour order (crimson, blue, gold, green, violet, teal, orange,
        /// silver): the nearest banner cloth in the kit. Independent settlements fly the plain brown.
        /// </summary>
        public static readonly string[] BannerByFaction =
        {
            "banner_patternA_red", "banner_patternA_blue", "banner_patternA_yellow", "banner_patternA_green",
            "banner_patternB_blue", "banner_patternB_green", "banner_patternB_yellow", "banner_patternA_white",
        };

        public const string IndependentBanner = "banner_brown";

        public static string BannerFor(int ownerIndex) => ownerIndex < 0 ? IndependentBanner : BannerByFaction[ownerIndex % BannerByFaction.Length];

        // The banner list follows Realmkeeper.UI.RkFactionColours (the strategic map's order and colours).

        private static LayoutBuilding Owner(LocalEnvironmentLayout layout, string key) { foreach (var b in layout.Buildings) if (b.Key == key) return b; return null; }

        private static void BuildDressing(Context ctx)
        {
            var layout = ctx.Layout;
            if (layout.Dressing.Count == 0) return;
            var root = new GameObject("Phase 27 dressing").transform;
            root.SetParent(ctx.Static, false);
            var plan = layout.Plan;
            foreach (var d in layout.Dressing)
            {
                var y = plan != null ? GroundAt(plan, d.Position.X, d.Position.Z) : 0f;
                var at = new Vector3(d.Position.X, y, d.Position.Z);
                var yaw = -d.Yaw; // the layout's building convention, as buildings are turned
                switch (d.Kind)
                {
                    case DressingKind.Barrel: Kit(ctx, root, "Barrel", at, yaw, 1.0f, true); break;
                    case DressingKind.Crate: Kit(ctx, root, "Crate_Wooden", at, yaw, 0.85f, true); break;
                    case DressingKind.Bench: Kit(ctx, root, "Bench", at, yaw, 1.7f, true); break;
                    case DressingKind.Anvil: Kit(ctx, root, "Anvil", at, yaw, 0.9f, true); break;
                    case DressingKind.Workbench: Kit(ctx, root, "Workbench", at, yaw, 1.9f, true); break;
                    case DressingKind.Sacks: Kit(ctx, root, "Bag_2", at, yaw, 0.75f, true); break;
                    case DressingKind.Cart: Kit(ctx, root, "town_cart", at, yaw, 3.2f, true); break;
                    case DressingKind.ForgeYard: Kit(ctx, root, "Stall_Blacksmith", at, yaw, 3.6f, true); break;
                    case DressingKind.MarketStall:
                        var fit = Owner(layout, d.For)?.Fit ?? ShopFit.General;
                        Kit(ctx, root, fit == ShopFit.Provisions ? "Stall_Vegetables_Full" : fit == ShopFit.Apothecary ? "Stall_Potions" : "Stall_Cart_Empty", at, yaw, 3.2f, true);
                        break;
                    case DressingKind.Banner:
                        // Back to back: the kit's cloth is one-sided, and a banner is seen from both ways along a road.
                        Kit(ctx, root, BannerFor(layout.OwnerIndex), at, yaw, 3.4f, !d.Raised);
                        Kit(ctx, root, BannerFor(layout.OwnerIndex), at, yaw + 180f, 3.4f, false);
                        if (!d.Raised) Mesh(ctx, root, "Banner pole", Shared(ctx, "BannerPole", () => MetreMeshes.Box(new Vector3(0.12f, 3.8f, 0.12f), ctx.Assets)), ctx.Mat("Bark"), at + Vector3.up * 1.9f); // a free-standing banner on its pole
                        break;
                    case DressingKind.WoodPile:
                        for (var i = 0; i < 3; i++)
                        {
                            var log = Mesh(ctx, root, "Log", Shared(ctx, "LogPile", () => MetreMeshes.Box(new Vector3(1.4f, 0.22f, 0.22f), ctx.Assets)), ctx.Mat("Bark"),
                                at + Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, 0.12f + (i == 2 ? 0.2f : 0f), (i - (i == 2 ? 1.5f : 0.5f)) * 0.24f));
                            log.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        }

                        break;
                    case DressingKind.Hay:
                        var hay = Mesh(ctx, root, "Hay", Shared(ctx, "HayBale", () => MetreMeshes.Box(new Vector3(1.1f, 0.6f, 0.7f), ctx.Assets)), Tinted(ctx, "Plaster", new Color(0.78f, 0.66f, 0.36f), "Hay"),
                            at + Vector3.up * 0.3f);
                        hay.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                        break;
                }
            }
        }

        /// <summary>
        /// A kit model at a stated size (its largest side, in metres), stood on <paramref name="at"/>: a scenery model when the
        /// catalog has one, else a kit module at its own scale. Null when neither is installed (nothing is placed).
        /// </summary>
        private static GameObject Kit(Context ctx, Transform parent, string id, Vector3 at, float yaw, float size, bool solid)
        {
            if (ctx.Catalog == null) return null;
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var go = KitProps.Place(holder, ctx.Catalog, id, at, yaw, size, solid);
            if (go != null) return go;
            Object.Destroy(holder.gameObject);
            return ctx.Catalog.Module(id) != null ? Module(ctx, parent, id, at, yaw, Vector3.one) : null;
        }

        /// <summary>A farm animal from the kit (chicken, cow, horse), or null where the kit has none (the box figure is drawn).</summary>
        private static GameObject AnimalModel(Context ctx, Transform t, string kind)
        {
            string id;
            float size;
            switch (kind)
            {
                case "chicken": id = "Chicken"; size = 0.5f; break;
                case "cattle": id = "Cow"; size = 2.0f; break;
                case "draft_horse": case "riding_horse": id = "Horse"; size = 2.3f; break;
                default: return null;
            }

            // The kit's animated animal (as the bestiary's beasts are made), scaled to its size; it walks as it moves.
            var prefab = ctx.Catalog != null ? ctx.Catalog.Animal(id) : null;
            if (prefab == null) return null;
            var body = Object.Instantiate(prefab, t, false);
            body.name = "Body " + id;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = prefab.transform.localRotation; // the import's own root turn
            if (KitProps.Measure(t, body, out var b) && b.size.sqrMagnitude > 1e-6f)
            {
                body.transform.localScale *= size / Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                KitProps.Measure(t, body, out b);
                body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            }

            var controller = ctx.Catalog.AnimalController(id);
            var anim = body.AddComponent<CombatantAnimator>();
            if (controller != null)
            {
                var a = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
                a.runtimeAnimatorController = controller;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.CullCompletely; // off-screen animals cost nothing
                anim.Bind(a, drivesSpeed: true);
            }
            else anim.Bind(null, drivesSpeed: false);

            foreach (var r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            return body;
        }
    }
}
