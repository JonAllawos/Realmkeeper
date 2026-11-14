using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Overland;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.World
{
    /// <summary>
    /// Phase 23 (ADR-0110, ADR-0111): the walkable country of each region, its rules, the realms and gateways, and the
    /// commands that move the player through them, with saving and loading in between. Prints the world-scale survey.
    /// </summary>
    public sealed class Phase23OverlandTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = NewGame(_dir);
        }

        /// <summary>A new game's world: the standard systems and six connected regions (as the game makes it).</summary>
        private static WorldSession NewGame(string dir)
        {
            var s = Realmkeeper.Infrastructure.Composition.RealmkeeperComposition.CreateSession(dir);
            Realmkeeper.Exploration.Application.ExplorationModule.Register(s); // settlements entered and left, as the game registers them
            var made = s.Execute(new CreateWorldCommand("Phase23land", 123456789, WorldSession.NewGameSettings()));
            Assert.That(made.IsSuccess, Is.True, made.Message);
            return s;
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();
        private PlayerCharacterData P => W.Player;

        private void SaveAndLoad(string slot)
        {
            Assert.That(_s.Execute(new SaveWorldCommand(slot)).IsSuccess, Is.True);
            Assert.That(_s.Execute(new LoadWorldCommand(slot)).IsSuccess, Is.True);
        }

        private void Ok<T>(T c) where T : ICommand
        {
            var r = _s.Dispatch(c);
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors ?? Array.Empty<string>()));
        }

        /// <summary>Walks (records) the player to a site's standing point.</summary>
        private void WalkTo(OverlandSite site)
        {
            var t = _s.GetOverlandTerrain(P.RealmId, P.RegionId);
            Ok(new WalkOverlandCommand(new LocalPose(site.X, t.HeightAt(site.X, site.Z), site.Z, 0f)));
        }

        // ----- Scale ----------------------------------------------------------------------------------------

        [Test]
        public void TheWorld_IsMeasured_AndMeetsTheScaleTargets()
        {
            var survey = WorldScaleSurvey.Measure(_s);
            TestContext.Out.WriteLine(WorldScaleSurvey.Describe(survey));
            var surface = survey.Where(r => r.Realm == RealmIds.Surface).ToList();
            Assert.That(surface.Count, Is.EqualTo(9), "Nine surface regions in a new world (the whole atlas).");
            Assert.That(survey.Count(r => r.Realm == RealmIds.Ancient), Is.EqualTo(1), "The ancient realm's prototype region.");
            Assert.That(surface.Sum(r => r.AreaKm2), Is.GreaterThan(230f), "Over 230 km² of walkable surface country.");
            foreach (var r in surface)
            {
                Assert.That(r.SizeKm, Is.EqualTo(5.12f).Within(0.01f), r.Name);
                Assert.That(r.WalkablePercent, Is.InRange(55f, 98f), r.Name + ": mostly walkable, with real barriers.");
                Assert.That(r.Settlements, Is.GreaterThanOrEqualTo(2), r.Name);
                Assert.That(r.Destinations, Is.GreaterThanOrEqualTo(4), r.Name + ": settlements and places to walk to.");
                Assert.That(r.RoadKm, Is.GreaterThan(2f), r.Name + ": roads between them.");
                Assert.That(r.Exits, Is.GreaterThanOrEqualTo(1), r.Name + ": a way on into a neighbour.");
                Assert.That(r.Biomes.Count, Is.GreaterThanOrEqualTo(5), r.Name + ": varied ground.");
            }
        }

        // ----- Determinism ---------------------------------------------------------------------------------

        [Test]
        public void TheSameSeed_MakesTheSameCountry_RealmsAndGateway()
        {
            var dir = Path.Combine(_dir, "again");
            Directory.CreateDirectory(dir);
            var other = NewGame(dir);
            var a = W;
            var b = other.CurrentWorldSnapshotForTests();
            Assert.That(b.Realms.Select(r => r.RealmId + r.Seed), Is.EqualTo(a.Realms.Select(r => r.RealmId + r.Seed)));
            Assert.That(b.Realms[1].Regions[0].RegionId, Is.EqualTo(a.Realms[1].Regions[0].RegionId), "Stable identity from the seed.");
            Assert.That(b.Gateways[0].A.Cell, Is.EqualTo(a.Gateways[0].A.Cell));
            Assert.That(b.Gateways[0].B.Cell, Is.EqualTo(a.Gateways[0].B.Cell));
            foreach (var region in a.Regions)
            {
                var ta = _s.GetOverlandTerrain(RealmIds.Surface, region.RegionId);
                var tb = other.GetOverlandTerrain(RealmIds.Surface, region.RegionId);
                for (var i = 0; i < 400; i++)
                {
                    var x = i * 97 % 5120 + 3.5f;
                    var z = i * 53 % 5120 + 7.25f;
                    Assert.That(tb.HeightAt(x, z), Is.EqualTo(ta.HeightAt(x, z)), region.Name);
                    Assert.That(tb.CellAt(x, z).Biome, Is.EqualTo(ta.CellAt(x, z).Biome));
                }
            }

            var ancientA = _s.GetOverlandTerrain(RealmIds.Ancient, a.Realms[1].Regions[0].RegionId);
            var ancientB = other.GetOverlandTerrain(RealmIds.Ancient, b.Realms[1].Regions[0].RegionId);
            Assert.That(ancientB.HeightAt(2000f, 2500f), Is.EqualTo(ancientA.HeightAt(2000f, 2500f)));
        }

        // ----- The ground's rules ---------------------------------------------------------------------------

        [Test]
        public void MountainsAndDeepWater_BarTheWay_RoadsBridgesAndFordsCrossThem()
        {
            foreach (var region in W.Regions)
            {
                var t = _s.GetOverlandTerrain(RealmIds.Surface, region.RegionId);
                var m = t.Map;
                int riverCells = 0, crossings = 0;
                for (var z = 0; z < m.Depth; z++)
                    for (var x = 0; x < m.Width; x++)
                    {
                        var tt = m.GetTerrain(x, z);
                        var c = t.Cell(x, z);
                        if (tt == TerrainType.Mountain && !m.IsRoad(x, z)) Assert.That(c.Passable, Is.False, "A mountain off the road.");
                        if (tt == TerrainType.DeepWater && !m.IsRoad(x, z)) Assert.That(c.Passable, Is.False, "Deep water.");
                        if (m.IsRoad(x, z)) Assert.That(c.Passable && c.Cost <= 1.6f, Is.True, "A road is walkable and quick.");
                        if (tt == TerrainType.River) { riverCells++; if (c.Passable) crossings++; }
                    }

                if (riverCells > 0) Assert.That(crossings, Is.GreaterThan(0).And.LessThan(riverCells), region.Name + ": a river has crossings, and is not crossable everywhere.");
                foreach (var s in W.Settlements.Where(s => s.RegionId == region.RegionId))
                {
                    var (x, z) = OverlandQueries.Approach(t, s.Position, 3);
                    Assert.That(t.Walkable(x, z), Is.True, s.Name + "'s approach is walkable.");
                }
            }
        }

        [Test]
        public void WalkingTime_FollowsTheTravelRules()
        {
            var t = _s.GetOverlandTerrain(RealmIds.Surface, W.Regions[0].RegionId);
            var road = Enumerable.Range(0, t.Map.Width * t.Map.Depth).Select(i => new MapCoord(i % t.Map.Width, i / t.Map.Width)).First(c => t.Cell(c.X, c.Z).Road && t.Cell(c.X, c.Z).Cost == 1f);
            var (x, z) = OverlandScale.CellCentre(road);
            var minutes = t.WalkMinutes(x, z, x + 0.01f, z, false) / 0.01 * OverlandScale.MetresPerCell;
            TestContext.Out.WriteLine($"Road cell {road}: {minutes:0.000} minutes per cell; cost {t.Cell(road.X, road.Z).Cost}.");
            Assert.That(minutes, Is.EqualTo(15.0).Within(0.05), "A cell of good road (1.2 km) is 15 minutes on foot at 4.8 km/h.");
            Assert.That(t.WalkMinutes(x, z, x + 1, z, true), Is.EqualTo(2 * t.WalkMinutes(x, z, x + 1, z, false)).Within(1e-9), "Night halves the pace.");
        }

        // ----- Into the country and back -------------------------------------------------------------------

        [Test]
        public void TheCountry_IsEnteredFromASettlement_Walked_AndASettlementEnteredFromIt()
        {
            var town = W.Settlements.First(s => s.RegionId == W.Regions[0].RegionId && s.SettlementType == SettlementType.Town);
            Ok(new Realmkeeper.Exploration.Application.EnterSettlementCommand(town.SettlementId));
            Ok(new EnterOverlandCommand());
            Assert.That(P.LocationKind, Is.EqualTo(CharacterLocationKind.Overland));
            Assert.That(P.RegionId, Is.EqualTo(town.RegionId));
            Assert.That(P.SettlementId.IsEmpty, Is.True);
            var view = _s.GetOverland();
            Assert.That(view.Sites.Count(s => s.Kind == OverlandSiteKind.Settlement), Is.GreaterThanOrEqualTo(2));
            var t = view.Terrain;
            Assert.That(t.Walkable(P.Pose.X, P.Pose.Z), Is.True, "Standing on walkable ground by the town.");

            var blocked = Enumerable.Range(0, t.Map.Width * t.Map.Depth).Select(i => new MapCoord(i % t.Map.Width, i / t.Map.Width)).First(c => !t.Cell(c.X, c.Z).Passable);
            var (bx, bz) = OverlandScale.CellCentre(blocked);
            Assert.That(_s.Dispatch(new WalkOverlandCommand(new LocalPose(bx, 0, bz, 0))).IsSuccess, Is.False, "No one stands on a mountain or in a lake.");
            Assert.That(_s.Dispatch(new WalkOverlandCommand(new LocalPose(-5, 0, 10, 0))).IsSuccess, Is.False, "Nor outside the region.");

            var other = view.Sites.First(s => s.Kind == OverlandSiteKind.Settlement && s.Id != town.SettlementId.ToString());
            Assert.That(_s.Dispatch(new EnterSettlementFromOverlandCommand(W.FindSettlement(new SettlementIdParse(other.Id).Id).SettlementId)).IsSuccess, Is.False, "Too far away.");
            WalkTo(other);
            Ok(new EnterSettlementFromOverlandCommand(new SettlementIdParse(other.Id).Id));
            Assert.That(P.IsInSettlement && P.ReturnToOverland, Is.True);
            SaveAndLoad("in-town");
            Assert.That(P.ReturnToOverland, Is.True, "Remembered across saving.");
            Ok(new EnterOverlandCommand());
            Assert.That(P.IsOverland, Is.True);
        }

        [Test]
        public void APlace_IsFoundByWalkingNearIt_EnteredFromTheCountry_AndLeftBackIntoIt()
        {
            Ok(new CreateCharacterCommand(Realmkeeper.Tests.EditMode.Character.ProgressionTests.Choices("fighter"))); // places are for a created character
            Ok(new EnterOverlandCommand());
            var place = W.Places.FirstOrDefault(p => p.RegionId == P.RegionId && p.ParentKind == PlaceParentKind.Region && p.Category != PlaceCategory.Interior && p.Discovery < PlaceDiscovery.Discovered);
            Assume.That(place, Is.Not.Null, "This seed's home region has an undiscovered place.");
            var site = _s.GetOverland().Sites.Single(s => s.Kind == OverlandSiteKind.Place && s.Id == place.PlaceId);
            Assert.That(site.Known, Is.False, "Not on the map before it is found.");
            Assert.That(_s.Dispatch(new EnterPlaceCommand(place.PlaceId)).IsSuccess, Is.False, "Unknown: cannot be entered.");
            WalkTo(site);
            Assert.That(W.FindPlace(place.PlaceId).Discovery, Is.EqualTo(PlaceDiscovery.Discovered), "Walking up to it finds it.");
            Ok(new EnterPlaceCommand(place.PlaceId));
            Assert.That(P.IsInPlace && P.ReturnToOverland, Is.True);
            SaveAndLoad("in-place");
            Assert.That(W.FindPlace(place.PlaceId).Discovery, Is.GreaterThanOrEqualTo(PlaceDiscovery.Discovered), "Found, and now visited.");
            Ok(new ExitPlaceCommand());
            Assert.That(P.IsOverland, Is.True, "Back into the country it was entered from.");
            Assert.That((new Vector2(P.Pose.X, P.Pose.Z) - new Vector2(site.X, site.Z)).Length(), Is.LessThan(150f), "Beside the way in.");
        }

        // ----- Regions and realms --------------------------------------------------------------------------

        [Test]
        public void TheWayOn_CrossesIntoTheNeighbour_AndBack_WithEverythingKept()
        {
            Ok(new EnterOverlandCommand());
            var home = Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W);
            var purse = P.Purse;
            var stacks = P.Inventory.Count;
            var exit = _s.GetOverland().Sites.First(s => s.Kind == OverlandSiteKind.RegionExit && s.Usable);
            Assert.That(_s.Dispatch(new CrossRegionCommand(new Realmkeeper.Domain.Identifiers.RegionId(Guid.Parse(exit.Id)))).IsSuccess, Is.False, "Walk to it first.");
            WalkTo(exit);
            Ok(new CrossRegionCommand(new Realmkeeper.Domain.Identifiers.RegionId(Guid.Parse(exit.Id))));
            Assert.That(P.RegionId.ToString(), Is.EqualTo(exit.Id));
            Assert.That(_s.GetOverland().Terrain.Walkable(P.Pose.X, P.Pose.Z), Is.True, "Arrived on walkable ground.");
            Assert.That(W.FindRegion(P.RegionId).Discovery, Is.EqualTo(RegionDiscovery.Visited));
            Assert.That(P.Purse, Is.EqualTo(purse));
            Assert.That(P.Inventory.Count, Is.EqualTo(stacks));
            SaveAndLoad("next-region");
            var back = _s.GetOverland().Sites.First(s => s.Kind == OverlandSiteKind.RegionExit && s.Id == home.ToString());
            WalkTo(back);
            Ok(new CrossRegionCommand(home));
            Assert.That(P.RegionId, Is.EqualTo(home));
            Assert.That(W.Regions.Count, Is.EqualTo(9), "No region duplicated.");
        }

        [Test]
        public void TheGateway_IsDormantUntilOpened_ThenLeadsIntoTheAncientRealm_AndBack()
        {
            Ok(new EnterOverlandCommand());
            var gate = W.FindGateway(RealmGenesis.PrototypeGatewayId);
            Assert.That(gate.State, Is.EqualTo(GatewayState.Dormant));
            var site = _s.GetOverland().Sites.Single(s => s.Kind == OverlandSiteKind.Gateway);
            WalkTo(site);
            Assert.That(W.FindGateway(gate.GatewayId).Discovered, Is.True, "Found by walking up to it.");
            var refused = _s.Dispatch(new UseGatewayCommand(gate.GatewayId));
            Assert.That(refused.IsSuccess, Is.False);
            Assert.That(refused.Message, Does.Contain("dormant"));

            Ok(new SetGatewayStateCommand(gate.GatewayId, GatewayState.Open)); // the campaign's hook
            var date = W.CurrentSimulationDate;
            Ok(new UseGatewayCommand(gate.GatewayId));
            Assert.That(P.RealmId, Is.EqualTo(RealmIds.Ancient));
            Assert.That(P.IsOverland, Is.True);
            Assert.That(W.FindRealm(RealmIds.Ancient).Visited, Is.True);
            Assert.That(W.CurrentSimulationDate, Is.EqualTo(date), "Passing takes no hidden time.");
            var view = _s.GetOverland();
            Assert.That(view.Profile, Is.EqualTo(RealmProfiles.AncientPrototype));
            Assert.That(view.Sites.Count(s => s.Kind == OverlandSiteKind.Settlement), Is.EqualTo(0), "Its inhabitants are undecided: none are invented.");
            Assert.That(view.Terrain.Walkable(P.Pose.X, P.Pose.Z), Is.True);
            Assert.That(_s.Dispatch(new SetGatewayStateCommand(gate.GatewayId, GatewayState.Sealed)).IsSuccess, Is.False, "Not sealed behind the player.");
            Assert.That(Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W), Is.EqualTo(W.Regions[0].RegionId), "The surface is still simulated from where the player left it.");

            SaveAndLoad("ancient");
            Assert.That(P.RealmId, Is.EqualTo(RealmIds.Ancient), "Saved in the other realm.");
            Assert.That(WorldValidator.Validate(W).IsValid, Is.True);
            Assert.That(_s.Execute(new AdvanceTimeCommand(24 * 60)).IsSuccess, Is.True, "The world goes on while the player is away.");

            var back = _s.GetOverland().Sites.Single(s => s.Kind == OverlandSiteKind.Gateway);
            WalkTo(back);
            Ok(new UseGatewayCommand(gate.GatewayId));
            Assert.That(RealmIds.IsSurface(P.RealmId), Is.True);
            Assert.That(P.RegionId, Is.EqualTo(W.Regions[0].RegionId));
            Assert.That(W.Realms.Count, Is.EqualTo(2), "No realm duplicated.");
        }

        [Test]
        public void AVersion24Save_LoadsWithTheRealmsMadeOnce_AndNothingElseChanged()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["schemaVersion"] = 24;
            ((JObject)doc["world"]).Remove("realms");
            ((JObject)doc["world"]).Remove("gateways");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "old" + JsonWorldStore.Extension), doc.ToString());
            var settlements = W.Settlements.Count;
            var npcs = W.Npcs.Count;
            Assert.That(_s.Execute(new LoadWorldCommand("old")).IsSuccess, Is.True);
            Assert.That(W.Realms.Select(r => r.RealmId), Is.EqualTo(new[] { RealmIds.Surface, RealmIds.Ancient }));
            Assert.That(W.Gateways.Count, Is.EqualTo(1));
            Assert.That(W.Settlements.Count, Is.EqualTo(settlements));
            Assert.That(W.Npcs.Count, Is.EqualTo(npcs));
            SaveAndLoad("again");
            Assert.That(W.Realms.Count, Is.EqualTo(2), "Made once.");
        }

        // ----- Follow-up: density, and the country from the map ---------------------------------------------

        [Test]
        public void TheFurtherRegions_HaveCountryPlaces_NamedByWhereTheyLie_MadeOnce()
        {
            var home = W.Regions[0].RegionId;
            var before = W.Places.Count;
            foreach (var region in W.Regions.Where(r => r.RegionId != home))
            {
                var country = W.Places.Where(p => p.RegionId == region.RegionId && PlaceCatalog.Country.Contains(p.DefinitionId)).ToList();
                Assert.That(country.Count, Is.GreaterThanOrEqualTo(3), region.Name + ": a hollow, a steading, a barrow or workings to find.");
                var t = _s.GetOverlandTerrain(RealmIds.Surface, region.RegionId);
                foreach (var p in country)
                {
                    StringAssert.Contains(" near ", p.Name, "Named after the settlement it lies near.");
                    Assert.That(p.Discovery, Is.EqualTo(PlaceDiscovery.Unknown), p.Name + " is found by walking, not given.");
                    var (x, z) = OverlandQueries.Approach(t, p.Cell, 2);
                    Assert.That(t.Walkable(x, z), Is.True, p.Name + " can be walked up to.");
                    Assert.That(p.Layout, Is.Not.Null, p.Name + " has its own layout to explore.");
                }

                var survey = WorldScaleSurvey.Measure(_s).First(r => r.Name == region.Name);
                Assert.That(survey.Destinations, Is.GreaterThanOrEqualTo(9), region.Name + ": more to walk to than its settlements and quest places.");
            }

            SaveAndLoad("density");
            Assert.That(W.Places.Count, Is.EqualTo(before), "Made once: loading adds nothing.");
        }

        [Test]
        public void TheCountry_IsEnteredFromTheMap_ByASettlementOfThePlayersRegion_AndNoOther()
        {
            var home = Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W);
            var here = W.Settlements.Where(s => s.RegionId == home).OrderBy(s => s.SettlementId).Last();
            var away = W.Settlements.First(s => s.RegionId != home);
            var refused = _s.Dispatch(new EnterOverlandCommand(away.SettlementId));
            Assert.That(refused.IsSuccess, Is.False, "Another region is reached by travelling there.");
            StringAssert.Contains("travel there first", refused.Message);
            Assert.That(P.LocationKind, Is.Not.EqualTo(CharacterLocationKind.Overland), "A refusal changes nothing.");
            Ok(new EnterOverlandCommand(here.SettlementId));
            Assert.That(P.LocationKind, Is.EqualTo(CharacterLocationKind.Overland));
            Assert.That(P.RegionId, Is.EqualTo(home));
            var t = _s.GetOverlandTerrain(RealmIds.Surface, home);
            var gate = OverlandQueries.Gate(t, here.Position);
            Assert.That(Math.Abs(P.Pose.X - gate.x) + Math.Abs(P.Pose.Z - gate.z), Is.LessThan(0.01f), "Out by the chosen settlement's way in.");
        }

        private readonly struct SettlementIdParse
        {
            public SettlementIdParse(string s) { Id = new Realmkeeper.Domain.Identifiers.SettlementId(Guid.Parse(s)); }
            public Realmkeeper.Domain.Identifiers.SettlementId Id { get; }
        }

        private readonly struct Vector2
        {
            public Vector2(float x, float z) { X = x; Z = z; }
            public float X { get; }
            public float Z { get; }
            public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.X - b.X, a.Z - b.Z);
            public float Length() => (float)Math.Sqrt(X * X + Z * Z);
        }
    }
}
