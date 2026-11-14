using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 16 (ADR-0076, ADR-0077): the connected world. Six regions made by the one generator; coherent borders;
    /// crossings and roads in one persistent route network; every settlement reachable; deterministic; saved exactly.
    /// </summary>
    public sealed class Phase16WorldTests : Phase16Fixture
    {
        [Test]
        public void ANewGame_IsNineConnectedRegions_EachWithItsOwnSettlementsPeopleAndFactions()
        {
            var w = W;
            Assert.That(w.Regions.Count, Is.EqualTo(9)); // six until Phase 23 (ADR-0110): the whole 3 × 3 atlas
            Assert.That(w.Regions.Select(r => (r.AtlasX, r.AtlasZ)).Distinct().Count(), Is.EqualTo(9), "Each has its own place on the atlas.");
            foreach (var r in w.Regions)
            {
                var here = w.Settlements.Where(s => s.RegionId == r.RegionId).ToList();
                Assert.That(here.Count, Is.EqualTo(3), r.Name + " has its own settlements.");
                Assert.That(w.Npcs.Count(n => here.Any(s => s.SettlementId == n.HomeSettlementId)), Is.GreaterThan(10), r.Name + " has its own people.");
                Assert.That(here.All(s => w.FindLayout(s.SettlementId) != null), Is.True, "Every settlement has its plan, for local exploration.");
                Assert.That(r.Neighbours.Count, Is.InRange(2, 4), r.Name + " borders its neighbours on the atlas (the centre of the 3 × 3 atlas has four, since Phase 23).");
                Assert.That(r.Seed, Is.Not.EqualTo(0));
            }

            Assert.That(w.Settlements.Select(s => s.Name).Distinct().Count(), Is.EqualTo(w.Settlements.Count), "Settlement names are unique across the world.");
            Assert.That(w.Factions.Count, Is.GreaterThan(6));
            for (var i = 0; i < w.Factions.Count; i++)
                for (var j = i + 1; j < w.Factions.Count; j++)
                    Assert.That(w.FindRelation(w.Factions[i].FactionId, w.Factions[j].FactionId), Is.Not.Null, "Every pair of factions has one relationship.");
            Assert.That(w.Places.Where(p => !Phase18Places.All.Contains(p.DefinitionId)).All(p => w.FindSettlement(p.SettlementId)?.RegionId == Home.RegionId || p.SettlementId.IsEmpty), Is.True,
                "The Phase 13 and 14 places stay in the home vale.");
            foreach (var r in w.Regions)
            {
                var own = w.Places.Where(p => Phase18Places.All.Contains(p.DefinitionId) && p.RegionId == r.RegionId).ToList();
                Assert.That(own.Count, Is.InRange(3, 4), r.Name + " has its own Phase 18 places (ADR-0089).");
                Assert.That(own.All(p => w.FindSettlement(p.SettlementId)?.RegionId == r.RegionId), Is.True, r.Name + "'s places lie by its own settlements.");
            }
            Assert.That(Home.Discovery, Is.EqualTo(RegionDiscovery.Visited));
            foreach (var r in w.Regions.Skip(1))
                Assert.That(r.Discovery, Is.EqualTo(Home.Neighbours.Contains(r.RegionId) ? RegionDiscovery.Discovered : RegionDiscovery.Unknown), "The home's neighbours are known by name; the far lands uncharted.");
            var opening = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(w);
            Assert.That(opening.RegionId, Is.EqualTo(Home.RegionId), "The opening town is at home.");
            Valid();
        }

        [Test]
        public void Borders_AreCoherent_ARangeRunsOn_AndCrossingsMatchTheTerrain()
        {
            var w = W;
            foreach (var a in w.Regions)
                foreach (var b in w.Regions.Where(b => a.Neighbours.Contains(b.RegionId)))
                {
                    var crossing = w.Routes.SingleOrDefault(r => r.IsCrossing && (r.RegionA == a.RegionId && r.RegionB == b.RegionId || r.RegionA == b.RegionId && r.RegionB == a.RegionId));
                    Assert.That(crossing, Is.Not.Null, a.Name + " and " + b.Name + " are joined by one crossing.");
                    Assert.That(w.FindSettlement(crossing.A).RegionId != w.FindSettlement(crossing.B).RegionId, Is.True);
                    if (crossing.Kind == RouteKind.Pass) Assert.That(crossing.TerrainPercent, Is.GreaterThan(150), "A pass is slow going.");
                    if (crossing.Kind == RouteKind.Bridge) Assert.That(crossing.HasBridge, Is.True);
                }

            // The second region continues (or keeps clear of) the home region's range at their shared border.
            var east = w.Regions.Single(r => r.AtlasX == 1 && r.AtlasZ == 0);
            var homeEast = Home.MountainDirection == 0 || Home.MountainDirection == 1 || Home.MountainDirection == 7;
            if (homeEast) Assert.That(new[] { 3, 4, 5, Home.MountainDirection }.Contains(east.MountainDirection), Is.True, "The range runs on across the border.");
        }

        [Test]
        public void EverySettlement_IsReachable_ByPassableRoutes()
        {
            var w = W;
            var reach = new System.Collections.Generic.HashSet<Realmkeeper.Domain.Identifiers.SettlementId> { w.Settlements[0].SettlementId };
            var frontier = new System.Collections.Generic.Queue<Realmkeeper.Domain.Identifiers.SettlementId>(reach);
            while (frontier.Count > 0)
            {
                var at = frontier.Dequeue();
                foreach (var r in w.Routes.Where(r => r.Joins(at) && r.Passable))
                    if (reach.Add(r.Other(at))) frontier.Enqueue(r.Other(at));
            }

            Assert.That(reach.Count, Is.EqualTo(w.Settlements.Count));
            Assert.That(w.Routes.Select(r => r.RouteId).Distinct().Count(), Is.EqualTo(w.Routes.Count), "One record per segment.");
            Assert.That(w.TradeRoutes.Count(t => w.FindSettlement(t.OriginId).RegionId != w.FindSettlement(t.DestinationId).RegionId), Is.GreaterThan(0), "Trade crosses borders.");
        }

        [Test]
        public void TheSameSeed_MakesTheSameWorld_AndSaveAndLoadAreExact()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var other = RealmkeeperComposition.CreateSession(dir);
                var sw = Stopwatch.StartNew();
                Ok(other.Execute(new CreateWorldCommand("Atlasland", Seed, WorldSession.NewGameSettings())));
                sw.Stop();
                TestContext.WriteLine($"Connected world (6 regions) generated in {sw.ElapsedMilliseconds} ms.");
                var a = JObject.Parse(new JsonWorldSerializer().Serialize(other.CurrentWorldSnapshotForTests()))["world"];
                var b = JObject.Parse(new JsonWorldSerializer().Serialize(W))["world"];
                a["worldId"] = b["worldId"];
                Assert.That(JToken.DeepEquals(a, b), Is.True, "Identical seeds, identical worlds.");
            }
            finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }

            var before = WorldJson();
            Edit(_ => { });
            Assert.That(WorldJson(), Is.EqualTo(before));
        }

        [Test]
        public void AVersion17Save_Loads_KeepsEverything_GainsItsRouteNetwork_AndCanDiscoverTheLandsBeyond()
        {
            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var single = Phase2Worlds.Session(dir); // a single-vale world, as Phase 15 made them
                var doc = JObject.Parse(new JsonWorldSerializer().Serialize(single.CurrentWorldSnapshotForTests()));
                Phase2Worlds.StripToV17(doc);
                Assert.That(doc["world"]["routes"], Is.Null);
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(new JsonWorldStore(dir).PathFor("v17"), doc.ToString());
                var s = RealmkeeperComposition.CreateSession(dir);
                Ok(s.Execute(new LoadWorldCommand("v17")));
                var w = s.CurrentWorldSnapshotForTests();
                Assert.That(w.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
                Assert.That(w.Regions.Count, Is.EqualTo(1), "An existing world keeps its single vale.");
                Assert.That(w.Routes.Count, Is.EqualTo(3), "Its three roads became the route network.");
                Assert.That(w.WorldSettings.EnabledSimulationSystems.Contains(Realmkeeper.Simulation.Atlas.WorldEventSystem.Id), Is.True);
                var settlements = w.Settlements.Select(x => x.SettlementId).ToList();
                var people = w.Npcs.Select(x => x.NpcId).ToList();

                Ok(s.DiscoverLandsBeyond());
                var grown = s.CurrentWorldSnapshotForTests();
                Assert.That(grown.Regions.Count, Is.EqualTo(6));
                Assert.That(grown.Settlements.Take(settlements.Count).Select(x => x.SettlementId), Is.EqualTo(settlements), "Nothing that existed was regenerated.");
                Assert.That(people.All(id => grown.FindNpc(id) != null), Is.True);
                Assert.That(WorldValidator.Validate(grown).IsValid, Is.True, string.Join("; ", WorldValidator.Validate(grown).Errors));
            }
            finally { if (System.IO.Directory.Exists(dir)) System.IO.Directory.Delete(dir, true); }
        }

        [Test]
        public void TheValidator_CatchesBrokenGeography()
        {
            var w = W;
            w.Routes.Add(w.Routes[0].Clone());
            Assert.That(WorldValidator.Validate(w).Errors.Any(e => e.Contains("duplicated")), Is.True);
            w = W;
            w.Regions[1].Neighbours.Clear();
            Assert.That(WorldValidator.Validate(w).Errors.Any(e => e.Contains("neighbours")), Is.True, "Neighbours agree.");
            w = W;
            var crossing = w.Routes.First(r => r.IsCrossing);
            var far = w.Regions.First(r => r.RegionId != crossing.RegionA && !w.FindRegion(crossing.RegionA).Neighbours.Contains(r.RegionId));
            crossing.RegionB = far.RegionId;
            Assert.That(WorldValidator.Validate(w).IsValid, Is.False, "A crossing between regions that do not border each other is refused.");
        }

        [Test]
        public void DisconnectedLands_CannotBeTravelledTo()
        {
            var w = W;
            var island = w.Regions.Last();
            w.Routes.RemoveAll(r => r.IsCrossing && (r.RegionA == island.RegionId || r.RegionB == island.RegionId));
            var from = In(Home).First().SettlementId;
            var to = w.Settlements.First(s => s.RegionId == island.RegionId).SettlementId;
            Assert.That(Realmkeeper.Simulation.Atlas.RoutePlanner.Plan(w, from, to), Is.Null, "No crossing, no way: nearness on the map is not a road.");
        }

        [Test]
        public void EachRegionsMap_IsRegeneratedFromItsOwnSeed_AndMatchesItsSettlements()
        {
            foreach (var r in W.Regions)
            {
                var map = S.MapFor(r.RegionId);
                Assert.That(map.RegionId, Is.EqualTo(r.RegionId));
                var check = Realmkeeper.Domain.Geography.RegionMapValidator.Validate(map, In(r).ToList());
                Assert.That(check.IsValid, Is.True, r.Name + ": " + string.Join("; ", check.Errors));
            }
        }
    }
}
