using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Domain
{
    public static partial class LocalEnvironmentGenerator
    {
        /// <summary>
        /// The explorable environment of a settlement that has a persisted plan (Phase 10, ADR-0047): the plan's own
        /// roads, buildings (keeping their site keys), ground, water and dressing, with the day's people placed on it.
        /// Nothing is invented here that the plan does not say; the plan is never changed.
        /// </summary>
        public static LocalEnvironmentLayout FromPlan(SettlementLayoutData plan, long worldSeed, SettlementId settlementId, RegionId regionId, SettlementType type,
            IReadOnlyList<ResidentInput> residents, IReadOnlyDictionary<string, ShopFit> shops = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (settlementId.IsEmpty) throw new ArgumentException("A settlement ID is required.", nameof(settlementId));
            residents = residents ?? Array.Empty<ResidentInput>();
            var rng = new LayoutRandom(worldSeed, settlementId);
            var arrival = new LocalPoint(plan.Arrival.X, plan.Arrival.Z);
            var layout = new LocalEnvironmentLayout
            {
                SettlementId = settlementId, RegionId = regionId, Type = type, Plan = plan,
                HalfExtent = plan.HalfExtent, PlazaRadius = plan.PlazaRadius, HasWalls = plan.HasWalls,
                ArrivalPose = new LocalPose(arrival.X, plan.HeightAt(arrival.X, arrival.Z), arrival.Z, plan.ArrivalYaw),
            };

            // The gate: a few metres in from where travellers arrive, beside the road.
            var inward = Math.Max(1f, (float)Math.Sqrt(arrival.X * arrival.X + arrival.Z * arrival.Z));
            var ux = -arrival.X / inward;
            var uz = -arrival.Z / inward;
            layout.Gate = new LocalPoint(arrival.X + ux * 6f - uz * 2.5f, arrival.Z + uz * 6f + ux * 2.5f);

            foreach (var r in plan.Roads)
                for (var i = 0; i + 1 < r.Line.Count; i++)
                    layout.Streets.Add(new LayoutStreet { From = new LocalPoint(r.Line[i].X, r.Line[i].Z), To = new LocalPoint(r.Line[i + 1].X, r.Line[i + 1].Z), Width = r.Width });

            // The hall first (residents and interiors look for it there), then everything else in plan order.
            var ordered = new List<BuildingData>();
            var hallData = plan.FindBuilding(SettlementSites.Hall);
            if (hallData != null) ordered.Add(hallData);
            foreach (var b in plan.Buildings) if (b != hallData) ordered.Add(b);
            foreach (var b in ordered)
            {
                layout.Buildings.Add(new LayoutBuilding
                {
                    Key = b.Key, Kind = KindOf(b), Center = new LocalPoint(b.Center.X, b.Center.Z), Width = b.Width, Depth = b.Depth, Height = b.Height, Yaw = b.Yaw,
                    Style = b.Palette % 3, Function = b.Function, FloorHeight = b.FloorHeight, Foundation = b.Foundation, Storeys = b.Storeys, Roof = b.Roof, Walls = b.Walls,
                    Chimney = b.Chimney, DistrictKey = b.DistrictKey,
                });
            }

            foreach (var f in plan.Features)
            {
                PropKind kind;
                switch (f.Kind)
                {
                    case "well": kind = PropKind.Well; break;
                    case "stall": kind = PropKind.MarketStall; break;
                    case "lamp": kind = PropKind.LampPost; break;
                    case "tree": case "great_tree": kind = PropKind.Tree; break;
                    default: continue;
                }

                layout.Props.Add(new LayoutProp { Kind = kind, Position = new LocalPoint(f.Position.X, f.Position.Z), Yaw = f.Yaw });
            }

            var hall = LocalPaths.Find(layout, SettlementSites.Hall);
            if (hall != null)
            {
                // The hall's furniture in its own frame (its door on local −Z).
                layout.Props.Add(new LayoutProp { Kind = PropKind.Table, Position = LocalPaths.ToWorld(hall, -2f, 0f), Yaw = hall.Yaw });
                layout.Props.Add(new LayoutProp { Kind = PropKind.Bench, Position = LocalPaths.ToWorld(hall, -2f, -1.4f), Yaw = hall.Yaw });
                layout.Props.Add(new LayoutProp { Kind = PropKind.Hearth, Position = LocalPaths.ToWorld(hall, 4f, 3.6f), Yaw = hall.Yaw });
            }

            var granary = LocalPaths.Find(layout, SettlementSites.Granary);
            if (granary != null)
            {
                for (var i = 0; i < 6; i++)
                    layout.Props.Add(new LayoutProp
                    {
                        Kind = i % 2 == 0 ? PropKind.Barrel : PropKind.Crate,
                        Position = LocalPaths.ToWorld(granary, granary.Width / 2f + 1.2f, -granary.Depth / 2f + 1f + i * 0.9f + rng.Range(-0.2f, 0.2f)), Yaw = rng.Range(0f, 90f),
                    });
                layout.Interactables.Add(new LayoutInteractable { Kind = InteractableKind.GranaryLedger, Position = LocalPaths.DoorOutside(granary, 2.2f), Yaw = granary.Yaw });
            }

            // The notice board at the square's edge, toward the way in, facing those who arrive.
            var toward = (float)Math.Atan2(arrival.Z, arrival.X);
            var board = new LocalPoint((float)Math.Cos(toward + 0.3f) * (plan.PlazaRadius - 2f), (float)Math.Sin(toward + 0.3f) * (plan.PlazaRadius - 2f));
            layout.Interactables.Add(new LayoutInteractable { Kind = InteractableKind.LabourNoticeBoard, Position = board, Yaw = SettlementLayoutGenerator.FaceYaw(new PlanPoint(board.X, board.Z), new PlanPoint(board.X * 2f, board.Z * 2f)) });

            if (shops != null)
                foreach (var b in layout.Buildings)
                    if (b.Kind == BuildingKind.House && shops.TryGetValue(b.Key, out var fit)) b.Fit = fit;

            PlaceResidents(layout, residents, rng);
            return layout;
        }

        private static BuildingKind KindOf(BuildingData b)
        {
            if (b.Key == SettlementSites.Hall) return BuildingKind.Hall;
            if (b.Key == SettlementSites.Granary) return BuildingKind.Granary;
            if (b.Key == SettlementSites.Landmark) return BuildingKind.Landmark;
            return BuildingKind.House;
        }
    }
}
