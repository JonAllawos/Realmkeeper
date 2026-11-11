using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Living
{
    /// <summary>
    /// Phase 10 (ADR-0047): each settlement has a persisted identity and plan — terrain first, water, roads that form
    /// one network and cross water only by bridges, districts, buildings on buildable ground keeping the world's site
    /// keys, farms, landmarks — made once, saved exactly, never regenerated, and walked along its own roads.
    /// </summary>
    public sealed class SettlementLayoutTests
    {
        private string _dir;

        [TearDown]
        public void TearDown()
        {
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static JToken LayoutsOf(string saveJson) => JObject.Parse(saveJson)["world"]["settlementLayouts"];

        [Test]
        public void EverySettlement_HasOnePlan_AndItIsSound()
        {
            var w = Phase2Worlds.Generated(123456789);
            Assert.That(w.SettlementLayouts.Count, Is.EqualTo(w.Settlements.Count));
            foreach (var s in w.Settlements)
            {
                var p = w.FindLayout(s.SettlementId);
                Assert.That(p, Is.Not.Null, s.Name);
                Assert.That(SettlementLayoutValidator.Check(p, s.SettlementType), Is.Empty, s.Name);
                Assert.That(p.Identity.Summary, Is.Not.Empty);
                Assert.That(p.GeneratorVersion, Is.EqualTo(SettlementLayoutData.CurrentGeneratorVersion));
            }

            Assert.That(WorldValidator.Validate(w).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(w).Errors));
        }

        [Test]
        public void ThePlan_IsDeterministic_ForTheSameWorld()
        {
            var a = Phase2Worlds.Generated(424242);
            var b = Phase2Worlds.Generated(424242);
            var serializer = new JsonWorldSerializer();
            Assert.That(LayoutsOf(serializer.Serialize(b)).ToString(), Is.EqualTo(LayoutsOf(serializer.Serialize(a)).ToString()));
            var c = Phase2Worlds.Generated(424243);
            Assert.That(LayoutsOf(serializer.Serialize(c)).ToString(), Is.Not.EqualTo(LayoutsOf(serializer.Serialize(a)).ToString()), "Another seed, another plan.");
        }

        [Test]
        public void AcrossManyWorlds_EveryPlanIsValid_AndSizedToItsSettlement()
        {
            var counts = new Dictionary<SettlementType, List<int>>();
            for (long seed = 1; seed <= 25; seed++)
            {
                var g = Phase2Worlds.Generate(seed * 7919 + 3);
                foreach (var s in g.World.Settlements)
                {
                    var p = g.World.FindLayout(s.SettlementId);
                    var errors = SettlementLayoutValidator.Check(p, s.SettlementType);
                    Assert.That(errors, Is.Empty, $"seed {seed} {s.Name}: {string.Join("; ", errors)}");
                    if (!counts.TryGetValue(s.SettlementType, out var list)) counts[s.SettlementType] = list = new List<int>();
                    list.Add(p.Buildings.Count);
                    Assert.That(p.Landmarks.Count, Is.GreaterThanOrEqualTo(2), $"{s.Name} has landmarks to find its way by.");
                }
            }

            Assert.That(counts[SettlementType.Town].Average(), Is.GreaterThan(counts[SettlementType.Village].Average()));
            Assert.That(counts[SettlementType.Village].Average(), Is.GreaterThan(counts[SettlementType.Hamlet].Average()));
            Assert.That(counts[SettlementType.Town].Min(), Is.GreaterThanOrEqualTo(40), "A town is a town.");
            Assert.That(counts[SettlementType.Hamlet].Min(), Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void TheWorldsSites_KeepTheirKeys_AndFunctions()
        {
            var w = Phase2Worlds.Generated(123456789);
            foreach (var s in w.Settlements)
            {
                var p = w.FindLayout(s.SettlementId);
                foreach (var l in w.Locations.Where(l => l.SettlementId == s.SettlementId && l.LayoutKey != null && l.LayoutKey != SettlementSites.Plaza && l.LayoutKey != SettlementSites.Gate))
                {
                    var b = p.FindBuilding(l.LayoutKey);
                    Assert.That(b, Is.Not.Null, $"{s.Name}: {l.Name} ({l.LayoutKey}) has its building.");
                    Assert.That(b.IsSite, Is.True);
                    if (l.Kind == LocationKind.Shop) Assert.That(b.Function, Is.EqualTo(BuildingFunction.Shop).Or.EqualTo(BuildingFunction.Smithy));
                }

                Assert.That(p.FindBuilding(SettlementSites.Hall).Function, Is.EqualTo(BuildingFunction.Tavern));
            }
        }

        [Test]
        public void Water_IsOnlyCrossedByBridges_AndBuildingsStandOnBuildableGround()
        {
            var w = Phase2Worlds.Generated(123456789);
            foreach (var p in w.SettlementLayouts)
            {
                foreach (var b in p.Buildings.Where(b => b.Function != BuildingFunction.Gatehouse && b.Function != BuildingFunction.Mill))
                {
                    Assert.That(SettlementTerrain.InWater(p, b.Center.X, b.Center.Z, 1f), Is.False, b.Key);
                    Assert.That(b.Foundation, Is.LessThanOrEqualTo(SettlementLayoutValidator.MaxPlinth + 0.5f), b.Key);
                    Assert.That(b.FloorHeight, Is.GreaterThanOrEqualTo(p.HeightAt(b.Center.X, b.Center.Z) - 0.01f), $"{b.Key}'s floor is not below its ground.");
                }

                foreach (var br in p.Bridges)
                {
                    Assert.That(SettlementTerrain.InWater(p, br.From.X, br.From.Z), Is.False);
                    Assert.That(SettlementTerrain.InWater(p, br.To.X, br.To.Z), Is.False);
                    var mid = new PlanPoint((br.From.X + br.To.X) / 2, (br.From.Z + br.To.Z) / 2);
                    Assert.That(br.DeckHeight, Is.GreaterThan(p.HeightAt(mid.X, mid.Z)), "The deck is above the water.");
                }

                Assert.That(SettlementLayoutValidator.Connected(p), Is.True);
            }

            Assert.That(w.SettlementLayouts.Sum(p => p.Bridges.Count), Is.GreaterThan(0), "This world's settlements have water to cross.");
        }

        [Test]
        public void ThePlan_SavesExactly_AndIsNeverRegenerated()
        {
            var w = Phase2Worlds.Generated(123456789);
            // A plan that differs from what the generator would make now: it must come back as saved.
            var plan = w.SettlementLayouts[0];
            plan.Buildings.First(b => !b.IsSite).Palette = 5;
            plan.Identity.Summary = "Edited to prove the plan is read, not regenerated.";
            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(w);
            var loaded = serializer.Deserialize(json);
            Assert.That(loaded.Status, Is.EqualTo(LoadStatus.Success), string.Join("; ", loaded.Errors));
            Assert.That(LayoutsOf(serializer.Serialize(loaded.World)).ToString(), Is.EqualTo(LayoutsOf(json).ToString()));

            _dir = Phase2Worlds.TempDirectory();
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "edited" + JsonWorldStore.Extension), json);
            var session = Phase2Worlds.Session(_dir);
            Assert.That(session.Execute(new LoadWorldCommand("edited")).IsSuccess, Is.True);
            Assert.That(session.GetSettlementLayout(plan.SettlementId).Identity.Summary, Is.EqualTo("Edited to prove the plan is read, not regenerated."));
        }

        [Test]
        public void TheValidator_NamesEachKindOfProblem()
        {
            var w = Phase2Worlds.Generated(123456789);
            var town = w.Settlements.First(s => s.SettlementType == SettlementType.Town);
            var good = w.FindLayout(town.SettlementId);
            Assert.That(SettlementLayoutValidator.Check(good, town.SettlementType), Is.Empty);
            List<string> Broken(System.Action<SettlementLayoutData> breakIt)
            {
                var p = good.Clone();
                breakIt(p);
                return SettlementLayoutValidator.Check(p, town.SettlementType);
            }

            var water = good.Waterways[0];
            var wet = water.Line[water.Line.Count / 2];
            var house = good.Buildings.First(b => b.Key == "house-0");
            var road = good.Roads.First(r => r.Kind == RoadKind.Lane);
            Assert.That(Broken(p => p.Buildings.RemoveAll(b => b.Key == SettlementSites.Granary)), Has.Some.Contains("'granary' has no building"));
            Assert.That(Broken(p => p.FindBuilding("house-0").Center = road.Line[road.Line.Count / 2]), Has.Some.Contains("stands on road"));
            Assert.That(Broken(p => p.FindBuilding("house-0").Center = wet), Has.Some.Contains("stands in the water"));
            Assert.That(Broken(p => p.FindBuilding("house-1").Center = house.Center), Has.Some.Contains("overlap"));
            Assert.That(Broken(p => p.FindBuilding("house-0").Foundation = 6f), Has.Some.Contains("too steep"));
            Assert.That(Broken(p => p.Buildings.Add(house.Clone())), Has.Some.Contains("appears twice"));
            Assert.That(Broken(p => p.FindBuilding("house-0").DistrictKey = "nowhere"), Has.Some.Contains("unknown district"));
            if (good.Bridges.Count > 0)
            {
                Assert.That(Broken(p => p.Bridges[0].From = wet), Has.Some.Contains("ends in the water"));
                Assert.That(Broken(p => p.Bridges.Clear()), Has.Some.Contains("with no crossing"));
                Assert.That(Broken(p => p.Bridges[0].RoadKey = "no-such-road"), Has.Some.Contains("carries no road"));
            }

            Assert.That(Broken(p => p.Roads.Add(new RoadData { Key = "island", Width = 3f, Line = { new PlanPoint(-150, 150), new PlanPoint(-140, 150) } })), Has.Some.Contains("one network"));
            Assert.That(Broken(p => p.Fields[0].Center = wet), Has.Some.Contains("is in the water"));
            Assert.That(Broken(p => p.Landmarks.Add(new LandmarkData { Key = "lm-x", Kind = "x", FeatureKey = "nothing" })), Has.Some.Contains("points at nothing"));
            Assert.That(Broken(p => p.GeneratorVersion = 99), Has.Some.Contains("Unknown generator version"));
            Assert.That(Broken(p => p.Waterways[0].Line = new List<PlanPoint> { new PlanPoint(-30, 0), new PlanPoint(30, 0) }), Has.Some.Contains("square is in the water"));
        }

        [Test]
        public void ACorruptPlan_IsRefused_NotRepaired()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(Phase2Worlds.Generated(123456789)));
            var building = (JObject)doc["world"]["settlementLayouts"][0]["buildings"][3];
            building["x"] = 0f; // onto the square's well and roads
            building["z"] = 0f;
            Assert.That(serializer.Deserialize(doc.ToString()).Status, Is.Not.EqualTo(LoadStatus.Success));

            var odd = JObject.Parse(serializer.Serialize(Phase2Worlds.Generated(123456789)));
            ((JArray)odd["world"]["settlementLayouts"][0]["roads"][0]["line"]).Add(1f);
            Assert.That(serializer.Deserialize(odd.ToString()).Status, Is.Not.EqualTo(LoadStatus.Success));
        }

        [Test]
        public void AnOlderSave_GainsItsPlansOnce_WhenLoaded()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(Phase2Worlds.GeneratedBeforeLivingWorld(123456789)));
            Phase2Worlds.StripToV11(doc);
            _dir = Phase2Worlds.TempDirectory();
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "v11" + JsonWorldStore.Extension), doc.ToString());
            var session = Phase2Worlds.Session(_dir);
            var load = session.Execute(new LoadWorldCommand("v11"));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors ?? new List<string>()));
            var world = session.CurrentWorldSnapshotForTests();
            Assert.That(world.SettlementLayouts.Count, Is.EqualTo(world.Settlements.Count));
            Assert.That(world.Livestock.Where(h => h.PastureKey != null), Is.Not.Empty, "Herds are given pastures.");

            Assert.That(session.Execute(new SaveWorldCommand("v12")).IsSuccess, Is.True);
            var first = LayoutsOf(File.ReadAllText(Path.Combine(_dir, "v12" + JsonWorldStore.Extension))).ToString();
            Assert.That(session.Execute(new LoadWorldCommand("v12")).IsSuccess, Is.True);
            Assert.That(session.Execute(new SaveWorldCommand("again")).IsSuccess, Is.True);
            Assert.That(LayoutsOf(File.ReadAllText(Path.Combine(_dir, "again" + JsonWorldStore.Extension))).ToString(), Is.EqualTo(first), "Loading again changes nothing.");
        }

        [Test]
        public void TheExploredSettlement_IsThePlan_AndPeopleWalkItsRoads()
        {
            var w = Phase2Worlds.Generated(123456789);
            foreach (var s in w.Settlements)
            {
                var layout = ExplorationModule.LayoutFor(w, s);
                var plan = w.FindLayout(s.SettlementId);
                Assert.That(layout.Plan, Is.Not.Null);
                Assert.That(layout.Buildings.Count, Is.EqualTo(plan.Buildings.Count));
                Assert.That(layout.Buildings[0].Key, Is.EqualTo(SettlementSites.Hall));
                Assert.That(layout.Streets.Count, Is.EqualTo(plan.Roads.Sum(r => r.Line.Count - 1)));
                Assert.That(layout.ArrivalPose.Y, Is.EqualTo(plan.HeightAt(plan.Arrival.X, plan.Arrival.Z)).Within(0.01f), "The traveller arrives on the ground.");
                Assert.That(SettlementTerrain.InWater(plan, layout.ArrivalPose.X, layout.ArrivalPose.Z), Is.False);

                // From the way in to every site's door, along the roads, never through a building.
                var gate = layout.Gate;
                foreach (var b in layout.Buildings.Where(b => b.Key == SettlementSites.Hall || b.Key == SettlementSites.Granary || b.Key.StartsWith("house-")))
                {
                    var route = LocalPaths.Route(layout, gate, null, LocalPaths.DoorOutside(b), null);
                    Assert.That(route.First().X, Is.EqualTo(gate.X));
                    var length = LocalPaths.Length(route);
                    Assert.That(length, Is.LessThan(plan.HalfExtent * 6f), $"{s.Name}: the way to {b.Key} is not absurd.");
                    for (var i = 1; i + 1 < route.Count; i++)
                    {
                        // Every metre of each leg (not just its corners), except the last few metres to the door.
                        var a = route[i];
                        var c = route[i + 1];
                        var n = System.Math.Max(1, (int)System.Math.Ceiling(LocalPaths.Distance(a, c)));
                        for (var k = 0; k <= n; k++)
                        {
                            var p = new LocalPoint(a.X + (c.X - a.X) * k / n, a.Z + (c.Z - a.Z) * k / n);
                            if (LocalPaths.Distance(p, route[route.Count - 1]) < 4f) continue;
                            foreach (var other in plan.Buildings.Where(o => o.Function != BuildingFunction.Gatehouse))
                                Assert.That(Inside(other, p), Is.False, $"{s.Name}: the way to {b.Key} passes through {other.Key} at ({p.X:0.0}, {p.Z:0.0}).");
                        }
                    }
                }
            }
        }

        /// <summary>True when a point is inside a building's footprint (shrunk a little: a road may graze a corner).</summary>
        private static bool Inside(BuildingData b, LocalPoint p)
        {
            var r = b.Yaw * System.Math.PI / 180.0;
            var dx = p.X - b.Center.X;
            var dz = p.Z - b.Center.Z;
            var lx = dx * System.Math.Cos(r) + dz * System.Math.Sin(r);
            var lz = -dx * System.Math.Sin(r) + dz * System.Math.Cos(r);
            return System.Math.Abs(lx) < b.Width / 2 - 0.5 && System.Math.Abs(lz) < b.Depth / 2 - 0.5;
        }

        [Test]
        public void Herds_GrazeTheirSettlementsPastures()
        {
            var w = Phase2Worlds.Generated(123456789);
            Assert.That(w.Livestock, Is.Not.Empty);
            foreach (var h in w.Livestock.Where(h => h.PastureKey != null))
            {
                var field = w.FindLayout(h.SettlementId).Fields.FirstOrDefault(f => f.Key == h.PastureKey);
                Assert.That(field, Is.Not.Null, h.HerdId);
                Assert.That(field.Kind, Is.EqualTo(h.CreatureId == "chicken" ? FieldKind.Yard : FieldKind.Pasture).Or.EqualTo(FieldKind.Yard));
            }

            var broken = w.DeepClone();
            broken.Livestock.First(h => h.PastureKey != null).PastureKey = "no-such-field";
            Assert.That(WorldValidator.Validate(broken).IsValid, Is.False, "A herd on a pasture that does not exist is refused.");
        }

        [Test]
        public void Generating_IsQuickEnough()
        {
            var g = Phase2Worlds.Generate(123456789);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var s in g.World.Settlements) SettlementLayoutGenerator.Generate(g.World, g.Map, s);
            sw.Stop();
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(2000), "All three plans in well under the time a load takes.");
        }
    }
}
