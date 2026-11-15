using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    /// <summary>Phase 27: an everyday object outside a building or in the square (the presentation chooses its model).</summary>
    public enum DressingKind
    {
        Barrel = 0,
        Crate = 1,
        Bench = 2,
        Anvil = 3,
        Workbench = 4,
        Sacks = 5,
        Cart = 6,

        /// <summary>The settlement's owner's banner (the presentation colours it from the faction, ADR-0119).</summary>
        Banner = 7,
        WoodPile = 8,
        Hay = 9,
    }

    /// <summary>Phase 27: one dressing item, where it stands and why (the building or place it belongs to).</summary>
    public sealed class LayoutDressing
    {
        public DressingKind Kind { get; set; }
        public LocalPoint Position { get; set; }

        /// <summary>Facing in degrees (as buildings: 0 = its front toward −Z).</summary>
        public float Yaw { get; set; }

        /// <summary>The building key, "square" or "gate:n".</summary>
        public string For { get; set; }

        /// <summary>Hung on a wall or a pole above the ground (no footprint on the street).</summary>
        public bool Raised { get; set; }
    }

    /// <summary>
    /// Phase 27 (ADR-0119): places everyday objects by rule, never at random over the town. Every item belongs to a building
    /// or a public place, by that building's purpose:
    /// <list type="bullet">
    /// <item>a smithy's anvil and bench by its door;</item>
    /// <item>barrels and crates at shops, workshops and stores;</item>
    /// <item>a bench or a barrel at some homes;</item>
    /// <item>benches round the square where no road enters;</item>
    /// <item>a cart at the granary;</item>
    /// <item>the owner's banners at the gates and on the hall, the keep and the guard posts.</item>
    /// </list>
    /// Every item on the ground keeps clear of every road (half its width and 0.4 m), of every door's approach (1.3 m either
    /// side of the door, 3.5 m out), of other buildings, of water and of fenced plots, so nothing blocks a way or a door.
    /// </summary>
    public static class StreetDressingPlanner
    {
        public const float DoorClearHalf = 1.3f;
        public const float DoorClearDepth = 3.5f;
        public const float RoadMargin = 0.4f;

        public static void Plan(LocalEnvironmentLayout layout, ulong seed)
        {
            if (layout?.Plan == null) return;
            var plan = layout.Plan;
            var rng = new System.Random((int)(seed & 0x7FFFFFFF));
            foreach (var b in layout.Buildings)
            {
                var doorX = LocalPaths.DoorwayX(b);
                var front = -b.Depth / 2f;
                void Beside(DressingKind kind, float along, float outward, float yaw = 0f)
                {
                    // Where asked, by the front wall; failing that, alongside a side wall near the front (away from the street).
                    var spots = new[]
                    {
                        LocalPaths.ToWorld(b, doorX + along, front - outward),
                        LocalPaths.ToWorld(b, (along >= 0 ? 1 : -1) * (b.Width / 2f + 0.9f), front + 1.2f + Math.Abs(along) * 0.3f),
                        LocalPaths.ToWorld(b, (along >= 0 ? -1 : 1) * (b.Width / 2f + 0.9f), front + 1.2f + Math.Abs(along) * 0.3f),
                    };
                    for (var k = 0; k < spots.Length; k++)
                        if (TryAdd(layout, new LayoutDressing { Kind = kind, Position = spots[k], Yaw = b.Yaw + yaw + (k == 0 ? 0f : 90f), For = b.Key }, 0.55f)) return;
                }

                var function = b.Function;
                if (b.Fit == ShopFit.Smithy || function == BuildingFunction.Smithy)
                {
                    Beside(DressingKind.Anvil, 2.3f, 1.0f);
                    Beside(DressingKind.Workbench, -2.6f, 0.9f);
                    Beside(DressingKind.WoodPile, 3.6f, 0.7f);
                }
                else if (b.Fit != ShopFit.None)
                {
                    Beside(DressingKind.Barrel, 2.0f, 0.6f);
                    Beside(DressingKind.Crate, 2.8f, 0.6f, 15f);
                    if (b.Fit == ShopFit.Provisions) Beside(DressingKind.Sacks, -2.2f, 0.6f);
                }
                else if (function == BuildingFunction.Workshop || function == BuildingFunction.Warehouse || function == BuildingFunction.Mill)
                {
                    Beside(DressingKind.Crate, 2.2f, 0.7f);
                    Beside(DressingKind.Crate, 2.2f, 1.5f, 30f);
                    Beside(DressingKind.Sacks, -2.3f, 0.7f);
                }
                else if (function == BuildingFunction.Barn || function == BuildingFunction.Stable)
                {
                    Beside(DressingKind.Hay, 2.6f, 1.0f);
                }
                else if (function == BuildingFunction.Farmhouse)
                {
                    Beside(DressingKind.WoodPile, -2.4f, 0.7f);
                }
                else if (function == BuildingFunction.Home || function == BuildingFunction.Cottage || function == BuildingFunction.LargeHome)
                {
                    var pick = rng.Next(100);
                    if (pick < 18) Beside(DressingKind.Bench, -2.0f, 0.5f);
                    else if (pick < 34) Beside(DressingKind.Barrel, 1.9f, 0.5f);
                    else if (pick < 42) Beside(DressingKind.WoodPile, -2.2f, 0.6f);
                }

                // The owner's banners either side of the door of the buildings that speak for them.
                if (function == BuildingFunction.Civic || function == BuildingFunction.Keep || function == BuildingFunction.GuardPost || b.Kind == BuildingKind.Hall && function == null)
                    foreach (var side in new[] { -1.6f, 1.6f })
                        layout.Dressing.Add(new LayoutDressing { Kind = DressingKind.Banner, Position = LocalPaths.ToWorld(b, doorX + side, front - 0.12f), Yaw = b.Yaw, For = b.Key, Raised = true });
            }

            // The square: benches facing in, where no road comes in.
            var benches = 0;
            for (var k = 0; k < 16 && benches < 6; k++)
            {
                var a = (k + 0.5) * Math.PI * 2 / 16;
                var r = layout.PlazaRadius - 1.3f;
                var p = new LocalPoint((float)Math.Cos(a) * r, (float)Math.Sin(a) * r);
                var faceIn = SettlementLayoutGenerator.FaceYaw(new PlanPoint(p.X, p.Z), new PlanPoint(0f, 0f));
                if (TryAdd(layout, new LayoutDressing { Kind = DressingKind.Bench, Position = p, Yaw = faceIn, For = "square" }, 2.5f)) benches++;
            }

            // A cart by the granary.
            var granary = LocalPaths.Find(layout, SettlementSites.Granary);
            if (granary != null)
                TryAdd(layout, new LayoutDressing { Kind = DressingKind.Cart, Position = LocalPaths.ToWorld(granary, -granary.Width / 2f - 2.2f, 0f), Yaw = granary.Yaw + 90f, For = granary.Key }, 1.2f);

            // Banners at each gate, either side of the way in, on poles.
            for (var g = 0; g < plan.Gates.Count; g++)
            {
                var gate = plan.Gates[g];
                var len = Math.Max(1f, (float)Math.Sqrt(gate.X * gate.X + gate.Z * gate.Z));
                var ux = gate.X / len;
                var uz = gate.Z / len;
                foreach (var s in new[] { -1f, 1f })
                {
                    // The first clear spot beside the way in: 4–8 m to the side, 2–6 m inside.
                    var placed = false;
                    for (var side = 4f; side <= 8f && !placed; side += 1f)
                        for (var inward = 2f; inward <= 6f && !placed; inward += 2f)
                        {
                            var p = new LocalPoint(gate.X - ux * inward - uz * s * side, gate.Z - uz * inward + ux * s * side);
                            // Facing the road (the cloth is one-sided): seen by everyone passing through the gate, either way.
                            var yaw = SettlementLayoutGenerator.FaceYaw(new PlanPoint(p.X, p.Z), new PlanPoint(gate.X - ux * inward, gate.Z - uz * inward));
                            placed = TryAdd(layout, new LayoutDressing { Kind = DressingKind.Banner, Position = p, Yaw = yaw, For = "gate:" + g }, 0.6f);
                        }
                }
            }
        }

        /// <summary>Adds an item on the ground only where it blocks nothing (see the class summary); true when added.</summary>
        public static bool TryAdd(LocalEnvironmentLayout layout, LayoutDressing d, float radius = 0.7f)
        {
            if (!Clear(layout, d.Position, radius, d.For)) return false;
            foreach (var o in layout.Dressing)
                if (!o.Raised && Dist(o.Position, d.Position) < radius + 0.5f) return false;
            layout.Dressing.Add(d);
            return true;
        }

        /// <summary>Whether a footprint of <paramref name="radius"/> at <paramref name="p"/> blocks no road, door, building, water or fenced plot.</summary>
        public static bool Clear(LocalEnvironmentLayout layout, LocalPoint p, float radius, string owner = null)
        {
            var plan = layout.Plan;
            foreach (var s in layout.Streets)
                if (SettlementTerrain.SegmentDistance(s.From.X, s.From.Z, s.To.X, s.To.Z, p.X, p.Z, out _, out _) < s.Width / 2f + RoadMargin + radius) return false;
            foreach (var b in layout.Buildings)
            {
                var (lx, lz) = Local(b, p);
                if (b.Key != owner && Math.Abs(lx) < b.Width / 2f + radius && Math.Abs(lz) < b.Depth / 2f + radius) return false; // in or against another building
                if (b.Key == owner && Math.Abs(lx) < b.Width / 2f && Math.Abs(lz) < b.Depth / 2f) return false; // never inside its own
                var doorX = LocalPaths.DoorwayX(b);
                if (Math.Abs(lx - doorX) < DoorClearHalf + radius && lz < -b.Depth / 2f && lz > -b.Depth / 2f - DoorClearDepth - radius) return false; // a door's approach
            }

            if (plan != null)
            {
                if (SettlementTerrain.InWater(plan, p.X, p.Z, radius + 0.5f)) return false;
                foreach (var f in plan.Fields)
                {
                    if (!f.Fenced) continue;
                    var r = f.Yaw * Math.PI / 180.0;
                    var dx = p.X - f.Center.X;
                    var dz = p.Z - f.Center.Z;
                    var fx = dx * (float)Math.Cos(r) + dz * (float)Math.Sin(r);
                    var fz = -dx * (float)Math.Sin(r) + dz * (float)Math.Cos(r);
                    if (Math.Abs(fx) < f.Width / 2f + radius + 0.3f && Math.Abs(fz) < f.Depth / 2f + radius + 0.3f) return false;
                }
            }

            return Math.Abs(p.X) < layout.HalfExtent - 3f && Math.Abs(p.Z) < layout.HalfExtent - 3f;
        }

        /// <summary>A point in a building's own frame (its door on local −Z).</summary>
        public static (float x, float z) Local(LayoutBuilding b, LocalPoint p)
        {
            var r = b.Yaw * Math.PI / 180.0;
            var cos = (float)Math.Cos(r);
            var sin = (float)Math.Sin(r);
            var dx = p.X - b.Center.X;
            var dz = p.Z - b.Center.Z;
            return (dx * cos + dz * sin, -dx * sin + dz * cos);
        }

        private static float Dist(LocalPoint a, LocalPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
