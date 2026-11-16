using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Tests.EditMode.Atlas;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 27 below the screen (ADR-0119). It covers:
    /// <list type="bullet">
    /// <item>the street population: drawn from the authoritative population and the hour, the settlement's own labour,
    /// deterministic, busier at the market than in the lanes, walking the streets;</item>
    /// <item>street dressing: placed by rule, clear of roads, doors, buildings, water and fences;</item>
    /// <item>doors: the one door position the kit, paths and people share;</item>
    /// <item>interiors: their size follows the building's.</item>
    /// </list>
    /// </summary>
    public sealed class Phase27SettlementLifeTests : Phase16Fixture
    {
        private List<(SettlementData s, LocalEnvironmentLayout layout)> Layouts()
        {
            var list = new List<(SettlementData, LocalEnvironmentLayout)>();
            foreach (var s in W.Settlements)
            {
                var layout = ExplorationModule.LayoutFor(W, s);
                if (layout.Plan != null) list.Add((s, layout));
            }

            Assume.That(list, Is.Not.Empty, "Planned settlements.");
            return list;
        }

        private static TownsfolkInput Input(SettlementData s, int minute, IReadOnlyList<(ProductionSourceType, int)> labour = null) => new TownsfolkInput
        {
            Population = s.Population, Type = s.SettlementType, Prosperity = s.Prosperity, MinuteOfDay = minute, Seed = 77UL,
            Labour = labour ?? s.Economy.Production.Select(p => (p.SourceType, p.Workers)).ToList(),
        };

        [Test]
        public void TheStreets_ShowAShareOfTheRealPopulation_ByTheHour()
        {
            var lines = new List<string>();
            foreach (var (s, layout) in Layouts())
            {
                var named = W.Npcs.Count(n => n.HomeSettlementId == s.SettlementId && !n.IsDeceased);
                var noon = TownsfolkPlanner.OnStreets(Input(s, 12 * 60));
                var night = TownsfolkPlanner.OnStreets(Input(s, 3 * 60));
                lines.Add($"{s.Name} ({s.SettlementType}): population {s.Population}, named {named}; unnamed out at noon {noon}, at 03:00 {night}; watch {TownsfolkPlanner.Watch(Input(s, 12 * 60))}");
                Assert.That(named, Is.LessThan(Math.Max(1, s.Population)), "The named are a subset of the population.");
                Assert.That(noon, Is.GreaterThanOrEqualTo(night), "Busier by day.");
                Assert.That(noon, Is.LessThanOrEqualTo(TownsfolkPlanner.MaxPlanned));
                if (s.Population >= 1000) Assert.That(noon, Is.GreaterThan(40), "A town of a thousand or more has dozens in its streets at noon.");
            }

            TestContext.WriteLine(string.Join("\n", lines));
            // Larger places have more people about.
            var bySize = Layouts().OrderBy(x => x.s.Population).ToList();
            Assert.That(TownsfolkPlanner.OnStreets(Input(bySize.Last().s, 720)), Is.GreaterThanOrEqualTo(TownsfolkPlanner.OnStreets(Input(bySize.First().s, 720))));
        }

        [Test]
        public void ThePlan_IsDeterministic_FollowsTheLabour_AndKeepsToTheStreets()
        {
            var (s, layout) = Layouts().OrderByDescending(x => x.s.Population).First();
            var a = TownsfolkPlanner.Plan(layout, Input(s, 11 * 60));
            var b = TownsfolkPlanner.Plan(layout, Input(s, 11 * 60));
            Assert.That(a.Select(x => x.Key + x.Look + x.Activity + x.Route.Count), Is.EqualTo(b.Select(x => x.Key + x.Look + x.Activity + x.Route.Count)), "Same settlement, day and hour: same people.");
            var unnamed = a.Count(x => !x.Key.StartsWith("watch", StringComparison.Ordinal));
            Assert.That(unnamed, Is.GreaterThanOrEqualTo((int)(TownsfolkPlanner.OnStreets(Input(s, 11 * 60)) * 0.9)), "Nearly everyone planned finds a way.");
            Assert.That(a.Select(x => x.Key).Distinct().Count(), Is.EqualTo(a.Count), "No one twice.");

            // Labour: a farming settlement sends more farmers through the gates than a trading one.
            var farming = TownsfolkPlanner.Plan(layout, Input(s, 11 * 60, new[] { (ProductionSourceType.Farming, 400), (ProductionSourceType.Trade, 20) }));
            var trading = TownsfolkPlanner.Plan(layout, Input(s, 11 * 60, new[] { (ProductionSourceType.Farming, 20), (ProductionSourceType.Trade, 400) }));
            Assert.That(farming.Count(x => x.Activity == TownsfolkActivity.Farming), Is.GreaterThan(trading.Count(x => x.Activity == TownsfolkActivity.Farming)));
            Assert.That(trading.Count(x => x.Activity == TownsfolkActivity.Carrying), Is.GreaterThan(farming.Count(x => x.Activity == TownsfolkActivity.Carrying)));
            Assert.That(a.Select(x => x.Activity).Distinct().Count(), Is.GreaterThanOrEqualTo(4), "Varied activity, not everyone the same.");
            Assert.That(a.Count(x => x.Look == Realmkeeper.Domain.World.NpcRole.Guard), Is.EqualTo(TownsfolkPlanner.UnnamedWatch(layout, Input(s, 11 * 60))), "The watch (Phase 28: less the named gate guards at their posts).");
            Assert.That(TownsfolkPlanner.UnnamedWatch(layout, Input(s, 11 * 60)) + TownsfolkPlanner.NamedPosted(layout), Is.EqualTo(Math.Max(TownsfolkPlanner.Watch(Input(s, 11 * 60)), TownsfolkPlanner.NamedPosted(layout))), "Named and unnamed together are the watch.");

            // Busy versus quiet: more people near the square than an equal area of the outskirts.
            int near = 0, far = 0;
            var r = layout.PlazaRadius * 2.5f;
            foreach (var spec in a)
                for (var t = 0; t < 600; t += 30)
                {
                    var p = TownsfolkPlanner.PositionAt(spec, t, out _, out _);
                    var d = Math.Sqrt(p.X * p.X + p.Z * p.Z);
                    if (d < r) near++;
                    else if (d < r * Math.Sqrt(2)) far++; // a ring of the same area
                }

            TestContext.WriteLine($"Near the square {near}, in a ring of equal area beyond it {far}.");
            Assert.That(near, Is.GreaterThan(far), "The market is busier than the streets beyond.");

            // On the streets: every sampled position is on a road, in the square, or at a door or gate it walks to.
            foreach (var spec in a.Where(x => x.Route.Count > 1))
                for (var t = 0; t < 400; t += 7)
                {
                    var p = TownsfolkPlanner.PositionAt(spec, t, out _, out _);
                    var onRoad = layout.Streets.Any(st => SettlementTerrain.SegmentDistance(st.From.X, st.From.Z, st.To.X, st.To.Z, p.X, p.Z, out _, out _) < st.Width / 2f + 2.5f);
                    var inSquare = Math.Sqrt(p.X * p.X + p.Z * p.Z) < layout.PlazaRadius + 2f;
                    // The short walk between a door or the gate and the street (a route's first and last legs) is off the paving by nature.
                    var n = spec.Route.Count;
                    var atEnd = SettlementTerrain.SegmentDistance(spec.Route[0].X, spec.Route[0].Z, spec.Route[1].X, spec.Route[1].Z, p.X, p.Z, out _, out _) < 1f
                        || SettlementTerrain.SegmentDistance(spec.Route[n - 2].X, spec.Route[n - 2].Z, spec.Route[n - 1].X, spec.Route[n - 1].Z, p.X, p.Z, out _, out _) < 1f;
                    Assert.That(onRoad || inSquare || atEnd, Is.True, $"{spec.Key} ({spec.Activity}) off the streets at ({p.X:0.0}, {p.Z:0.0}); nearest road {layout.Streets.Min(st => SettlementTerrain.SegmentDistance(st.From.X, st.From.Z, st.To.X, st.To.Z, p.X, p.Z, out _, out _)):0.0} m; plaza {layout.PlazaRadius:0.0}; route " + string.Join(" ", spec.Route.Select(q => $"({q.X:0},{q.Z:0})")));
                }

            // Walking is continuous: no jumps between moments a tenth of a second apart.
            foreach (var spec in a.Where(x => x.Route.Count > 1).Take(30))
                for (var t = 0.0; t < 300; t += 0.1)
                {
                    var p0 = TownsfolkPlanner.PositionAt(spec, t, out _, out _);
                    var p1 = TownsfolkPlanner.PositionAt(spec, t + 0.1, out _, out _);
                    Assert.That(Dist(p0, p1), Is.LessThanOrEqualTo(spec.Speed * 0.1f + 0.01f), spec.Key);
                }
        }

        [Test]
        public void StreetDressing_BlocksNoRoadDoorBuildingWaterOrFence()
        {
            int items = 0, smithies = 0, anvils = 0, gateBanners = 0;
            foreach (var (s, layout) in Layouts())
            {
                foreach (var d in layout.Dressing)
                {
                    items++;
                    if (d.Raised) continue;
                    foreach (var st in layout.Streets)
                        Assert.That(SettlementTerrain.SegmentDistance(st.From.X, st.From.Z, st.To.X, st.To.Z, d.Position.X, d.Position.Z, out _, out _), Is.GreaterThanOrEqualTo(st.Width / 2f + StreetDressingPlanner.RoadMargin), $"{s.Name}: {d.Kind} for {d.For} on a road.");
                    foreach (var b in layout.Buildings)
                    {
                        var (lx, lz) = StreetDressingPlanner.Local(b, d.Position);
                        var doorX = LocalPaths.DoorwayX(b);
                        Assert.That(Math.Abs(lx - doorX) < StreetDressingPlanner.DoorClearHalf && lz < -b.Depth / 2f && lz > -b.Depth / 2f - StreetDressingPlanner.DoorClearDepth, Is.False, $"{s.Name}: {d.Kind} for {d.For} in front of the door of {b.Key}.");
                        Assert.That(Math.Abs(lx) < b.Width / 2f && Math.Abs(lz) < b.Depth / 2f, Is.False, $"{s.Name}: {d.Kind} inside {b.Key}.");
                    }

                    Assert.That(SettlementTerrain.InWater(layout.Plan, d.Position.X, d.Position.Z), Is.False);
                }

                smithies += layout.Buildings.Count(b => b.Fit == ShopFit.Smithy);
                anvils += layout.Dressing.Count(d => d.Kind == DressingKind.Anvil);
                gateBanners += layout.Dressing.Count(d => d.Kind == DressingKind.Banner && d.For.StartsWith("gate", StringComparison.Ordinal));
                if (layout.Plan.Gates.Count > 0) Assert.That(layout.Dressing.Any(d => d.Kind == DressingKind.Banner && d.For.StartsWith("gate", StringComparison.Ordinal)), Is.True, s.Name + ": the owner's banner at a gate.");
            }

            TestContext.WriteLine($"Dressing items {items}; smithies {smithies}, anvils {anvils}; gate banners {gateBanners}.");
            Assert.That(items, Is.GreaterThan(0));
            if (smithies > 0) Assert.That(anvils, Is.GreaterThan(0), "A smithy shows an anvil by its door.");
        }

        [Test]
        public void EveryDoor_IsWhereTheKitBuildsIt_AndPathsLeadThere()
        {
            var checkedDoors = 0;
            foreach (var (s, layout) in Layouts())
                foreach (var b in layout.Buildings)
                {
                    var plan = BuildingKitPlanner.Plan(b, s.SettlementType, b.Key);
                    var front = plan.Pieces.Where(p => p.Z < -0.5f && (p.ModuleId.Contains("_Door_") || p.ModuleId.Contains("Archway") || p.ModuleId.Contains("Door_Round"))).ToList();
                    if (front.Count == 0) continue;
                    var doorX = LocalPaths.DoorwayX(b);
                    Assert.That(front.Min(p => Math.Abs(p.X - doorX)), Is.LessThan(0.05f), $"{s.Name}: {b.Key} ({b.Kind}, {b.Function}, {b.Fit}): the door is at x {string.Join("/", front.Select(p => p.X.ToString("0.00")))}, paths go to {doorX:0.00}.");
                    var outside = LocalPaths.DoorOutside(b);
                    var (lx, lz) = StreetDressingPlanner.Local(b, outside);
                    Assert.That(Math.Abs(lx - doorX), Is.LessThan(0.01f), "The point outside the door is square in front of it.");
                    Assert.That(lz, Is.LessThan(-b.Depth / 2f), "Outside.");
                    checkedDoors++;
                }

            TestContext.WriteLine($"Doors checked: {checkedDoors}.");
            Assert.That(checkedDoors, Is.GreaterThan(50));
        }

        [Test]
        public void Interiors_FollowTheBuildingsSize_AndNeverExceedIt()
        {
            var lines = new List<string>();
            foreach (var (w, d, f) in new[] { (6f, 6f, BuildingFunction.Cottage), (9f, 8f, BuildingFunction.Home), (14f, 11f, BuildingFunction.LargeHome), (10f, 9f, BuildingFunction.Smithy), (18f, 12f, BuildingFunction.Warehouse), (24f, 16f, BuildingFunction.Civic) })
            {
                var layout = InteriorGenerator.Generate(new InteriorRequest { Function = f, WidthMetres = w, DepthMetres = d }, 5UL);
                var rooms = layout.Rooms.Where(r => r.Floor == 0).ToList();
                var maxX = rooms.Max(r => r.Rect.X + r.Rect.W) * InteriorGenerator.CellMetres;
                var maxZ = rooms.Max(r => r.Rect.Z + r.Rect.D) * InteriorGenerator.CellMetres;
                var furniture = layout.Features.Count;
                lines.Add($"{f} {w}×{d} m: ground floor {maxX:0.0}×{maxZ:0.0} m, {layout.Rooms.Count} rooms, {furniture} furnishings");
                Assert.That(maxX, Is.LessThanOrEqualTo(w + 0.01f), f + ": inside its walls (width).");
                Assert.That(maxZ, Is.LessThanOrEqualTo(d + 0.01f), f + ": inside its walls (depth).");
                Assert.That(maxX, Is.GreaterThanOrEqualTo(w - 3.01f), f + ": uses its width.");
            }

            TestContext.WriteLine(string.Join("\n", lines));
            var small = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.Home, WidthMetres = 7f, DepthMetres = 7f }, 9UL);
            var large = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.LargeHome, WidthMetres = 16f, DepthMetres = 12f }, 9UL);
            Assert.That(large.Rooms.Count, Is.GreaterThan(small.Rooms.Count), "A large house has more rooms.");
            Assert.That(large.Features.Count, Is.GreaterThan(small.Features.Count), "And more furnishings.");
            var again = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.LargeHome, WidthMetres = 16f, DepthMetres = 12f }, 9UL);
            Assert.That(again.Rooms.Select(r => r.Id + r.Rect), Is.EqualTo(large.Rooms.Select(r => r.Id + r.Rect)), "Deterministic.");
        }


        [Test]
        public void TheSmallestHomes_CanBeEntered_AndTheirRoomsAreBigEnoughToStandIn()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            var town = W.Settlements.First(s => s.SettlementType == SettlementType.Town);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, town);
            var smallest = layout.Buildings.Where(x => (x.Function == BuildingFunction.Cottage || x.Function == BuildingFunction.Home) && x.Fit == ShopFit.None).OrderBy(x => x.Width * x.Depth).Take(4).ToList();
            Assume.That(smallest, Is.Not.Empty);
            foreach (var b in smallest)
            {
                var r = S.Dispatch(new EnterBuildingCommand(b.Key));
                Assert.That(r.IsSuccess, Is.True, $"{b.Key} ({b.Width:0.0}×{b.Depth:0.0} m): {r.Message} {string.Join(" ", r.Errors)}");
                Ok(S.Dispatch(new Realmkeeper.Application.Places.ExitPlaceCommand()));
            }
        }

        [Test]
        public void Children_PlayByDay_AndAreNeverOutAtNight()
        {
            var (s, layout) = Layouts().OrderByDescending(x => x.s.Population).First();
            var noon = TownsfolkPlanner.Plan(layout, Input(s, 11 * 60));
            var night = TownsfolkPlanner.Plan(layout, Input(s, 23 * 60));
            var kids = noon.Count(x => x.Child);
            TestContext.WriteLine($"{s.Name} at 11:00: {kids} children of {noon.Count} out ({noon.Count(x => x.Child && x.Activity == TownsfolkActivity.Playing)} playing); at 23:00: {night.Count(x => x.Child)}.");
            Assert.That(kids, Is.GreaterThan(0), "Children are out by day.");
            Assert.That(kids, Is.LessThan(noon.Count / 3), "A share of the street, not most of it.");
            Assert.That(noon.Any(x => x.Child && x.Activity == TownsfolkActivity.Playing), Is.True, "Some are at play.");
            Assert.That(night.Any(x => x.Child), Is.False, "None at night.");
            Assert.That(noon.Where(x => x.Child).All(x => x.Look != Realmkeeper.Domain.World.NpcRole.Guard), Is.True);
        }

        [Test]
        public void SmithiesHaveForgeYards_AndSomeShopsTradeFromStalls_TheirKeepersOutside()
        {
            int smithies = 0, yards = 0, stalls = 0;
            foreach (var (s, layout) in Layouts())
            {
                foreach (var b in layout.Buildings.Where(x => x.Fit == ShopFit.Smithy))
                {
                    smithies++;
                    if (layout.Dressing.Any(d => d.Kind == DressingKind.ForgeYard && d.For == b.Key)) yards++;
                }

                foreach (var b in layout.Buildings.Where(x => x.StallAt.HasValue))
                {
                    stalls++;
                    var spot = LocalPaths.SpotAt(layout, b.Key, null, 0, true);
                    Assert.That(spot.HasValue && !spot.Value.indoors, Is.True, $"{s.Name}: {b.Key}'s keeper serves outside, at the stall.");
                    Assert.That(Dist(spot.Value.spot, b.StallAt.Value), Is.LessThan(2f), "Behind the stall.");
                    Assert.That(StreetDressingPlanner.Clear(layout, b.StallKeeperAt.Value, 0.2f, b.Key) || Dist(b.StallKeeperAt.Value, b.StallAt.Value) < 1.5f, Is.True);
                }
            }

            TestContext.WriteLine($"Smithies {smithies}, with forge yards {yards}; open-air stall shops {stalls}.");
            Assert.That(yards, Is.GreaterThan(smithies / 2), "Most smithies have room for an outdoor forge yard.");
            Assert.That(stalls, Is.GreaterThan(0), "Some shops trade from stalls.");
        }
        private static float Dist(LocalPoint a, LocalPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
    }
}
