using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// A shop's interior (Phase 5): counter, shelves, a rack for long goods and, in a smithy, a forge and
    /// anvil; and its wares. Every unit in the shop's stock is shown as its own model (up to
    /// <see cref="MaxShownPerItem"/> of one item), rebuilt whenever the stock changes, so what stands on
    /// the shelves is what the shop has. The counter is where the player trades: interacting with it
    /// opens the trade window (the keeper must be there for any trade to go through).
    /// </summary>
    public sealed class ShopInteriorView : MonoBehaviour, IInteractable
    {
        public const int MaxShownPerItem = 15;

        private LayoutBuilding _building;
        private BuildingPlan _plan;
        private ArtCatalog _catalog;
        private GeneratedAssets _assets;
        private Transform _wares;
        private readonly Dictionary<string, int> _shown = new Dictionary<string, int>(StringComparer.Ordinal);

        public ShopId ShopId { get; private set; }
        public string ShopName { get; private set; } = "Shop";
        public string Status { get; private set; } = string.Empty;
        public bool IsOpen { get; private set; }
        public LayoutBuilding Building => _building;

        /// <summary>Units of each item standing in the shop now.</summary>
        public IReadOnlyDictionary<string, int> Shown => _shown;

        public int ShownTotal
        {
            get
            {
                var n = 0;
                foreach (var v in _shown.Values) n += v;
                return n;
            }
        }

        /// <summary>Raised when the player opens trade at the counter.</summary>
        public event Action<ShopInteriorView> TradeRequested;

        public string Prompt => $"Trade at {ShopName} ({(IsOpen ? "open" : "closed")})";
        public InteractionKind Kind => InteractionKind.Informational;
        public Vector3 InteractionPoint => transform.TransformPoint(new Vector3(0f, 0f, _plan.Depth / 2f - 2.45f));

        public static ShopInteriorView Create(Transform parent, LayoutBuilding building, LocalEnvironmentLayout layout, ShopId shopId, ArtCatalog catalog, GeneratedAssets assets)
        {
            var go = new GameObject("Shop interior " + building.Key);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(building.Center.X, building.FloorHeight, building.Center.Z); // Phase 21: on the shop's own floor (raised foundations sank the fittings)
            go.transform.localRotation = Quaternion.Euler(0f, -building.Yaw, 0f);
            var view = go.AddComponent<ShopInteriorView>();
            view._building = building;
            view._plan = BuildingKitPlanner.Plan(building, layout.Type, layout.SettlementId.ToString());
            view._catalog = catalog;
            view._assets = assets;
            view.ShopId = shopId;
            view.Furnish();
            return view;
        }

        public InteractionResponse Interact(WorldSession session, Transform player)
        {
            TradeRequested?.Invoke(this);
            return new InteractionResponse(ShopName, IsOpen ? "You step up to the counter." : Status, Kind, CommandResult.Ok());
        }

        // ----- Furniture ---------------------------------------------------------------------------------

        private float W => _plan.Width / 2f;
        private float D => _plan.Depth / 2f;
        private bool Smithy => _building.Fit == ShopFit.Smithy;

        private void Furnish()
        {
            var t = transform;
            var wood = _catalog.Material("Wood");
            var planks = _catalog.Material("Planks");
            var counterZ = D - 1.75f;
            var counterW = Mathf.Min(_plan.Width - 2.4f, 3.4f);
            Block(t, "Counter", new Vector3(counterW, 1.0f, 0.55f), planks, new Vector3(0f, 0.5f, counterZ), true);
            Block(t, "Counter top", new Vector3(counterW + 0.12f, 0.06f, 0.68f), wood, new Vector3(0f, 1.03f, counterZ), false);

            // Shelves along the back wall (three boards) and the east wall (two boards).
            for (var row = 0; row < 3; row++)
                Block(t, "Back shelf", new Vector3(_plan.Width - 0.8f, 0.05f, 0.42f), wood, new Vector3(0f, 0.55f + row * 0.6f, D - 0.36f), false);
            for (var side = 0; side < 2; side++)
            foreach (var x in new[] { -W + 0.45f, W - 0.45f })
                Block(t, "Shelf post", new Vector3(0.06f, 1.95f, 0.42f), wood, new Vector3(x, 0.975f, D - 0.36f), false);
            var sideLength = Mathf.Max(1f, _plan.Depth - 3.9f);
            var sideMid = (-D + 0.9f + (D - 2.4f)) / 2f;
            for (var row = 0; row < 2; row++)
                Block(t, "Side shelf", new Vector3(0.42f, 0.05f, sideLength), wood, new Vector3(W - 0.36f, 0.7f + row * 0.6f, sideMid), false);
            Block(t, "Rack rail", new Vector3(0.08f, 0.06f, sideLength), wood, new Vector3(-W + 0.25f, 1.6f, sideMid), false);

            if (Smithy)
            {
                // A forge in the front-west corner; the anvil beside it (the kit's own when installed).
                Block(t, "Forge", new Vector3(1.2f, 0.9f, 1.0f), _catalog.Material("RockTrim"), new Vector3(-W + 0.95f, 0.45f, -D + 0.9f), true);
                Block(t, "Embers", new Vector3(0.8f, 0.08f, 0.6f), _catalog.Material("Ember"), new Vector3(-W + 0.95f, 0.93f, -D + 0.9f), false);
                if (Prop("Anvil", new Vector3(-W + 2.3f, 0f, -D + 1.0f), 90f, 0.75f) == null)
                    Block(t, "Anvil", new Vector3(0.5f, 0.75f, 0.28f), _catalog.Material("Metal"), new Vector3(-W + 2.3f, 0.375f, -D + 1.0f), true);
                Glow("Forge light", new Vector3(-W + 0.95f, 1.3f, -D + 0.9f), new Color(1f, 0.52f, 0.24f), 4.5f, 2.2f);
            }

            Dress(); // Phase 21: the trade's own furniture and tools from the CC0 kits

            // Phase 21 (ADR-0107): the trade's work station, in the front-west corner where its forge, bench or oven stands.
            var station = StationFor(_building.Fit);
            if (station != StationKind.None)
                _stations.Add(WorkStationView.Create(t, new Vector3(-W + 1.3f, 0.9f, -D + 1.2f), ShopId, station));

            if (!Smithy && _dressed == 0)
                Block(t, "Crate", new Vector3(0.6f, 0.6f, 0.6f), _catalog.Material("WoodTrimWear"), new Vector3(-W + 0.8f, 0.3f, -D + 0.9f), true);

            var lamp = new GameObject("Shop lamp").AddComponent<Light>();
            lamp.transform.SetParent(t, false);
            lamp.transform.localPosition = new Vector3(0f, _plan.InteriorHeight - 0.5f, 0.3f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.82f, 0.6f);
            lamp.range = Mathf.Max(_plan.Width, _plan.Depth) + 1.5f;
            lamp.intensity = 2.0f;

            _wares = new GameObject("Wares").transform;
            _wares.SetParent(t, false);
        }

        // ----- Phase 21: each trade's furniture (ADR-0106) ---------------------------------------------------
        //
        // The room's working floor is the front half: the counter, the shelves (back and east walls) and the long-goods rack
        // (west wall) hold the wares, so the trade's furniture stands in the two front corners and beside the counter, clear
        // of the middle path from the door. Every model is measured and scaled to a stated size, so a kit's import scale
        // does not matter. Nothing here is a ware: wares are only ever the shop's stock (Refresh).

        private int _dressed;
        private readonly List<WorkStationView> _stations = new List<WorkStationView>();

        /// <summary>Phase 21: the work stations in this shop (to register for interaction).</summary>
        public IReadOnlyList<WorkStationView> Stations => _stations;

        public static StationKind StationFor(ShopFit fit)
        {
            switch (fit)
            {
                case ShopFit.Smithy: return StationKind.Forge;
                case ShopFit.Leatherworks: return StationKind.LeatherBench;
                case ShopFit.Tailor: return StationKind.SewingTable;
                case ShopFit.Bowyer:
                case ShopFit.Carpentry: return StationKind.Workbench;
                case ShopFit.Apothecary: return StationKind.Still;
                case ShopFit.Provisions: return StationKind.Oven;
                default: return StationKind.None;
            }
        }

        /// <summary>Props placed for the trade (for tests and the walkthrough).</summary>
        public int Dressed => _dressed;

        private void Dress()
        {
            var fw = new Vector3(-W + 1.05f, 0f, -D + 1.05f);  // front-west corner
            var fe = new Vector3(W - 1.25f, 0f, -D + 1.0f);    // front-east corner
            var cw = new Vector3(-Mathf.Min(_plan.Width - 2.4f, 3.4f) / 2f - 0.55f, 0f, D - 1.75f); // west end of the counter
            switch (_building.Fit)
            {
                case ShopFit.Smithy:
                    Prop("WeaponStand", fe, -90f, 1.6f);
                    Prop("Barrel", fe + new Vector3(-0.2f, 0f, 1.1f), 0f, 0.9f);          // the quench
                    Prop("Whetstone", cw, 0f, 0.7f);
                    Prop("Bucket_Metal", new Vector3(-W + 0.45f, 0f, -D + 2.0f), 0f, 0.4f, false);
                    break;
                case ShopFit.Leatherworks:
                    Prop("Workbench", fw, 90f, 1.7f);                                     // the cutting and stitching bench
                    Prop("Peg_Rack", new Vector3(W - 0.12f, 1.6f, -D + 1.2f), -90f, 1.2f, false); // hides and straps hung to dry
                    Prop("Barrel", fe, 0f, 0.95f);                                         // the tanning tub
                    Prop("Stool", fw + new Vector3(0.9f, 0f, 0.1f), 30f, 0.5f);
                    Prop("Rope_1", fw + new Vector3(0f, 0.95f, 0f), 0f, 0.4f, false);
                    break;
                case ShopFit.Tailor:
                    Prop("Table_Large", fw + new Vector3(0.25f, 0f, 0.1f), 90f, 1.8f);   // the cutting table
                    Prop("Dummy", fe, 180f, 1.75f);                                         // a dress form
                    Prop("Stool", fw + new Vector3(1.2f, 0f, 0.2f), -20f, 0.5f);
                    Prop("Bag", cw, 0f, 0.5f, false);
                    break;
                case ShopFit.Bowyer:
                    Prop("Workbench", fw, 90f, 1.7f);
                    Prop("WeaponStand", fe, -90f, 1.6f);
                    Prop("Peg_Rack", new Vector3(W - 0.12f, 1.6f, -D + 1.2f), -90f, 1.2f, false);
                    Prop("Stool", fw + new Vector3(0.9f, 0f, 0.1f), 10f, 0.5f);
                    break;
                case ShopFit.Apothecary:
                    Prop("Cauldron", fw, 0f, 0.9f);                                        // the still and simmering pot
                    Prop("Shelf_Small_Bottles", new Vector3(W - 0.15f, 1.3f, -D + 1.1f), -90f, 1.0f, false);
                    Prop("Bookcase_2", fe + new Vector3(0.6f, 0f, 1.4f), -90f, 1.9f);
                    Prop("CandleStick_Triple", new Vector3(-Mathf.Min(_plan.Width - 2.4f, 3.4f) / 2f + 0.35f, 1.06f, D - 1.75f), 0f, 0.4f, false); // on the counter
                    Glow("Still fire", fw + new Vector3(0f, 0.35f, 0f), new Color(1f, 0.55f, 0.3f), 2.5f, 1.2f);
                    break;
                case ShopFit.Provisions:
                    // The bread oven (built, with its fire), sacks and baskets of the day's goods.
                    Block(transform, "Oven", new Vector3(1.3f, 1.1f, 1.1f), _catalog.Material("RockTrim"), fw + new Vector3(0f, 0.55f, 0f), true);
                    Block(transform, "Oven mouth", new Vector3(0.5f, 0.32f, 0.06f), _catalog.Material("Ember"), fw + new Vector3(0f, 0.45f, -0.56f), false); // its front face
                    Glow("Oven fire", fw + new Vector3(0f, 0.6f, -0.65f), new Color(1f, 0.55f, 0.28f), 3.0f, 1.6f);
                    Prop("Barrel_Apples", fe, 0f, 0.95f);
                    Prop("FarmCrate_Carrot", fe + new Vector3(-0.1f, 0f, 1.0f), 15f, 0.6f);
                    Prop("Table_Large", cw + new Vector3(-0.3f, 0f, -1.5f), 0f, 1.4f);
                    break;
                case ShopFit.Carpentry:
                    Prop("Workbench_Drawers", fw, 90f, 1.7f);
                    Prop("Workbench", fe, -90f, 1.6f);
                    Prop("Stool", fw + new Vector3(0.9f, 0f, 0.1f), 15f, 0.5f);
                    break;
                case ShopFit.General:
                    Prop("Barrel", fw, 0f, 0.9f);
                    Prop("Crate_Wooden", fw + new Vector3(0.9f, 0f, 0f), 12f, 0.6f);
                    Prop("Chest_Wood", fe, -90f, 0.9f);
                    break;
            }

            // A wall lantern by the door for every shop.
            Prop("Lantern_Wall", new Vector3(-W + 0.1f, 2.1f, -D + 0.6f), 90f, 0.45f, false);
        }

        /// <summary>
        /// A CC0 kit model, measured and scaled so its largest side is <paramref name="size"/> metres, standing at
        /// <paramref name="at"/> (its base at at.y). Solid ones get a box collider of their measured size. Null when the
        /// kit is not installed (the room then keeps its built pieces).
        /// </summary>
        private GameObject Prop(string id, Vector3 at, float yaw, float size, bool solid = true)
        {
            var prefab = _catalog != null ? _catalog.SceneryModel(id) : null;
            if (prefab == null) return null;
            var go = Instantiate(prefab, transform, false);
            go.name = id;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            if (!Measure(go, out var b) || b.size.sqrMagnitude < 1e-6f)
            {
                Destroy(go);
                return null;
            }

            var largest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            go.transform.localScale *= size / largest;
            Measure(go, out b);
            go.transform.localPosition += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            if (solid)
            {
                Measure(go, out b);
                var col = new GameObject(id + " collider");
                col.transform.SetParent(transform, false);
                col.transform.localPosition = b.center;
                col.AddComponent<BoxCollider>().size = b.size;
            }

            _dressed++;
            return go;
        }

        /// <summary>A model's bounds in this room's own axes (from its meshes' corners).</summary>
        private bool Measure(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var any = false;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var local = transform.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { bounds = new Bounds(local, Vector3.zero); any = true; }
                    else bounds.Encapsulate(local);
                }
            }

            return any;
        }

        private void Glow(string name, Vector3 at, Color colour, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = at;
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void Block(Transform parent, string name, Vector3 size, Material material, Vector3 at, bool solid)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.AddComponent<MeshFilter>().sharedMesh = MetreMeshes.Box(size, _assets);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.On;
            if (solid) go.AddComponent<BoxCollider>().size = size;
        }

        // ----- Wares -------------------------------------------------------------------------------------

        /// <summary>Shows the shop as world data has it now: its name, whether it trades, and every unit of its stock.</summary>
        public void Refresh(ShopDetail detail)
        {
            if (detail == null) return;
            ShopName = detail.Summary.Name;
            foreach (var st in _stations) st.ShopName = ShopName;
            IsOpen = detail.Summary.IsOpen;
            Status = detail.Summary.Status;
            foreach (Transform child in _wares)
            {
                if (UnityEngine.Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }

            _shown.Clear();
            var lines = new List<PriceLine>(detail.ForSale);
            lines.Sort((a, b) => string.CompareOrdinal(a.ItemId, b.ItemId));
            var shelfGroup = 0;
            var rackSlot = 0;
            var wallSlot = 0;
            foreach (var line in lines)
            {
                var def = ItemCatalog.Find(line.ItemId);
                if (def == null || line.Quantity <= 0) continue;
                var count = Mathf.Min(line.Quantity, MaxShownPerItem);
                var placed = 0;
                for (var i = 0; i < count; i++)
                {
                    Vector3 at;
                    Quaternion rot;
                    switch (Placement(def))
                    {
                        case Display.Upright:
                            at = new Vector3(-W + 0.32f, def.Id == "timber_plank" ? 0.6f : 0.05f, D - 2.6f - rackSlot * 0.22f);
                            rot = def.Id == "timber_plank" ? Quaternion.Euler(0f, 90f, 80f) : Quaternion.Euler(0f, 90f, -6f);
                            rackSlot++;
                            if (at.z < -D + 2.2f) continue;
                            break;
                        case Display.Hung:
                            at = new Vector3(-W + 1.0f + wallSlot * 0.9f, 2.45f, D - 0.12f);
                            rot = Quaternion.Euler(0f, 180f, 0f);
                            wallSlot++;
                            if (at.x > W - 0.6f) continue;
                            break;
                        default:
                            if (!ShelfSpot(shelfGroup, i, def, out at, out rot)) continue;
                            break;
                    }

                    var model = ItemModels.Build(_wares, def.Id, _catalog, _assets);
                    if (model == null) continue;
                    model.transform.localPosition = at;
                    model.transform.localRotation = rot;
                    placed++;
                }

                if (Placement(def) == Display.Shelf) shelfGroup++;
                if (placed > 0) _shown[def.Id] = placed;
            }
        }

        private enum Display
        {
            Shelf,
            Upright,
            Hung,
        }

        private static Display Placement(ItemDefinition d)
        {
            if (d.Id == "spear_ash" || d.Id == "staff_quarter" || d.Id == "bow_hunting" || d.Id == "timber_plank") return Display.Upright;
            if (d.Category == ItemCategory.Shield) return Display.Hung;
            return Display.Shelf;
        }

        /// <summary>
        /// The i-th unit of the g-th item on the shelves: back wall first (three boards, 1 m groups), then the
        /// east wall (two boards). Within a group units sit in rows of three, two deep, then stack.
        /// </summary>
        private bool ShelfSpot(int group, int i, ItemDefinition d, out Vector3 at, out Quaternion rot)
        {
            rot = Quaternion.identity;
            var weapon = d.Category == ItemCategory.Weapon;
            if (weapon) rot = Quaternion.Euler(0f, 0f, 90f); // laid along the shelf
            var unitHeight = d.Category == ItemCategory.Clothing || d.Category == ItemCategory.Armour ? 0.13f
                : d.Category == ItemCategory.TradeGood ? 0.2f : d.Category == ItemCategory.Food ? 0.1f : 0.16f;
            var col = weapon ? 0 : i % 3;
            var row = weapon ? i % 4 : i / 3 % 2;
            var layer = weapon ? i / 4 : i / 6;
            var groupsPerBack = Mathf.Max(1, Mathf.FloorToInt((_plan.Width - 1.0f) / 1.0f));
            if (group < groupsPerBack * 3)
            {
                // Eye level first, then the top board, then the bottom (behind the counter).
                var board = new[] { 1, 2, 0 }[group % 3];
                var slot = group / 3;
                slot = (slot % 2 == 0 ? 1 : -1) * ((slot + 1) / 2) + (groupsPerBack - 1) / 2; // from the middle outward
                if (slot < 0 || slot >= groupsPerBack) { at = default; return false; }
                var x = -(_plan.Width - 1.0f) / 2f + 0.5f + slot * 1.0f;
                at = new Vector3(x + (weapon ? 0f : (col - 1) * 0.28f), 0.58f + board * 0.6f + layer * unitHeight, D - 0.45f + (weapon ? (row - 1.5f) * 0.07f : (row - 0.5f) * 0.16f));
                return layer < 3;
            }

            var sideGroup = group - groupsPerBack * 3;
            var perSide = Mathf.Max(1, Mathf.FloorToInt((_plan.Depth - 3.9f) / 0.9f));
            if (sideGroup >= perSide * 2) { at = default; return false; }
            var sideBoard = sideGroup / perSide;
            var z = D - 2.4f - 0.45f - sideGroup % perSide * 0.9f;
            rot = Quaternion.Euler(0f, 90f, 0f) * rot;
            at = new Vector3(W - 0.42f + (weapon ? (row - 1.5f) * 0.07f : (row - 0.5f) * 0.16f), 0.73f + sideBoard * 0.6f + layer * unitHeight, z + (weapon ? 0f : (col - 1) * 0.26f));
            return layer < 2;
        }
    }
}
