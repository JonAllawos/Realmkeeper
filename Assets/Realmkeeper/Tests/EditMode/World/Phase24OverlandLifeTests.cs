using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.World
{
    /// <summary>
    /// Phase 24 (ADR-0113, ADR-0114, ADR-0115): the living country — landmarks, the world's own travellers on its roads, the
    /// map position and waypoints, stepping off and back onto a journey, encounters with real sources and lasting outcomes,
    /// and ground weather — with saving and loading in between.
    /// </summary>
    public sealed class Phase24OverlandLifeTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = NewGame(_dir);
        }

        private static WorldSession NewGame(string dir)
        {
            var s = Realmkeeper.Infrastructure.Composition.RealmkeeperComposition.CreateSession(dir);
            Realmkeeper.Exploration.Application.ExplorationModule.Register(s);
            var made = s.Execute(new CreateWorldCommand("Phase24land", 123456789, WorldSession.NewGameSettings()));
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

        private void Ok<T>(T c) where T : ICommand
        {
            var r = _s.Dispatch(c);
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors ?? Array.Empty<string>()));
        }

        private void SaveAndLoad(string slot)
        {
            Assert.That(_s.Execute(new SaveWorldCommand(slot)).IsSuccess, Is.True);
            Assert.That(_s.Execute(new LoadWorldCommand(slot)).IsSuccess, Is.True);
        }

        private void Create() => Ok(new CreateCharacterCommand(Realmkeeper.Tests.EditMode.Character.ProgressionTests.Choices("fighter")));

        /// <summary>Stands the player at a point of their region (walkable ground nearby).</summary>
        private void StandAt(float x, float z)
        {
            var t = _s.GetOverlandTerrain(P.RealmId, P.RegionId);
            if (!t.Walkable(x, z)) (x, z) = OverlandQueries.Approach(t, OverlandScale.CellAt(t.Map, x, z), 3);
            Ok(new WalkOverlandCommand(new LocalPose(x, t.HeightAt(x, z), z, 0f)));
        }

        private static float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

        // ----- Landmarks ---------------------------------------------------------------------------------------

        [Test]
        public void Landmarks_AreGeneratedFromTheSeed_PlacedOnWalkableGround_StableAcrossSessions_AndFoundByWalking()
        {
            var w = W;
            var all = new List<OverlandLandmark>();
            foreach (var r in w.Regions)
            {
                var list = Landmarks.InRegion(_s, w, RealmIds.Surface, r.RegionId);
                var t = _s.GetOverlandTerrain(RealmIds.Surface, r.RegionId);
                TestContext.Out.WriteLine($"{r.Name}: " + string.Join(", ", list.GroupBy(l => l.Kind).Select(g => g.Count() + " " + g.Key)));
                Assert.That(list.Count, Is.InRange(3, 30), r.Name + ": some landmarks, not a crowd.");
                Assert.That(list.Count(l => l.Kind == LandmarkKind.Waystone), Is.LessThanOrEqualTo(LandmarkGenesis.MaxWaystones));
                foreach (var l in list)
                {
                    Assert.That(t.Walkable(l.X, l.Z), Is.True, l.Id + " stands on walkable ground.");
                    if (l.Kind == LandmarkKind.Waystone) Assert.That(l.Text, Does.Contain("on foot"), "A waystone names where its road goes, and how long.");
                }

                all.AddRange(list);
            }

            Assert.That(all.Select(l => l.Id).Distinct().Count(), Is.EqualTo(all.Count), "Ids are unique.");
            Assert.That(all.Select(l => l.Kind).Distinct().Count(), Is.GreaterThanOrEqualTo(4), "A mix of kinds across the world.");
            Assert.That(Landmarks.InRegion(_s, w, RealmIds.Ancient, w.Realms[1].Regions[0].RegionId), Is.Empty, "Nothing is invented in the ancient realm.");

            var dir = Path.Combine(_dir, "again");
            Directory.CreateDirectory(dir);
            var other = NewGame(dir);
            var w2 = other.CurrentWorldSnapshotForTests();
            var again = w2.Regions.SelectMany(r => Landmarks.InRegion(other, w2, RealmIds.Surface, r.RegionId)).ToList();
            Assert.That(again.Select(l => l.Id + "@" + l.X + "," + l.Z), Is.EqualTo(all.Select(l => l.Id + "@" + l.X + "," + l.Z)), "The same seed, the same landmarks.");

            // Found by walking up to it; kept through saving and loading; not found twice.
            Ok(new EnterOverlandCommand());
            var near = Landmarks.InRegion(_s, W, RealmIds.Surface, P.RegionId).First();
            Assert.That(Landmarks.IsFound(W, near.Id), Is.False);
            StandAt(near.X + 3f, near.Z);
            Assert.That(Landmarks.IsFound(W, near.Id), Is.True, "Walking up to it finds it.");
            SaveAndLoad("landmarks");
            Assert.That(Landmarks.IsFound(W, near.Id), Is.True, "Kept.");
            Assert.That(W.Overland.Landmarks.Count(x => x.LandmarkId == near.Id), Is.EqualTo(1));
        }

        [Test]
        public void ALandmark_IsUsed_AWaystoneRead_ACampSearchedOnce_ACairnLooksOut()
        {
            Create();
            Ok(new EnterOverlandCommand());
            var here = Landmarks.InRegion(_s, W, RealmIds.Surface, P.RegionId);
            var waystone = here.FirstOrDefault(l => l.Kind == LandmarkKind.Waystone);
            Assume.That(waystone, Is.Not.Null, "The home region has a waystone.");
            Assert.That(_s.Dispatch(new UseLandmarkCommand(waystone.Id)).IsSuccess, Is.False, "Walk there first.");
            StandAt(waystone.X + 2f, waystone.Z);
            var read = _s.Dispatch(new UseLandmarkCommand(waystone.Id));
            Assert.That(read.IsSuccess, Is.True, read.Message);
            Assert.That(read.Message, Does.Contain("Carved on the stone"));

            var search = W.Regions.SelectMany(r => Landmarks.InRegion(_s, W, RealmIds.Surface, r.RegionId)).FirstOrDefault(l => l.Searchable && l.RegionId == P.RegionId);
            if (search != null)
            {
                StandAt(search.X + 2f, search.Z);
                var first = _s.Dispatch(new UseLandmarkCommand(search.Id));
                Assert.That(first.IsSuccess, Is.True, first.Message);
                var containers = W.Containers.Count;
                var second = _s.Dispatch(new UseLandmarkCommand(search.Id));
                Assert.That(second.Message, Does.Contain("searched here"));
                Assert.That(W.Containers.Count, Is.EqualTo(containers), "Searched once: nothing new is made.");
                Assert.That(W.Overland.FindLandmark(search.Id).Searched, Is.True);
            }

            var cairn = here.FirstOrDefault(l => l.Kind == LandmarkKind.Cairn);
            if (cairn != null)
            {
                StandAt(cairn.X + 2f, cairn.Z);
                var view = _s.Dispatch(new UseLandmarkCommand(cairn.Id));
                Assert.That(view.Message, Does.StartWith("From the cairn"));
            }
        }

        // ----- Travellers ---------------------------------------------------------------------------------------

        [Test]
        public void Travellers_AreTheWorldsOwnGroups_OnTheirRoads_MovingWithTheClock_AndNobodyIsInvented()
        {
            Assert.That(_s.Execute(new AdvanceDaysCommand(6)).IsSuccess, Is.True); // let the roads fill
            var w = W;
            var npcs = w.Npcs.Count;
            var seen = new List<(RegionId region, TravellerView v)>();
            foreach (var r in w.Regions)
                foreach (var v in Travellers.InRegion(_s, w, r.RegionId))
                    seen.Add((r.RegionId, v));
            TestContext.Out.WriteLine($"{seen.Count} travelling groups placed in the country; {w.Groups.Count(g => g.IsActive)} active groups in the world.");
            Assume.That(seen.Count, Is.GreaterThan(0), "Some group is on the roads after six days.");
            foreach (var (region, v) in seen)
            {
                Assert.That(w.FindGroup(v.GroupId), Is.Not.Null, "Every traveller is a group of the simulation.");
                var t = _s.GetOverlandTerrain(RealmIds.Surface, region);
                Assert.That(OverlandScale.Inside(t.Map, v.X, v.Z), Is.True);
            }

            Assert.That(W.Npcs.Count, Is.EqualTo(npcs), "Showing them creates no one.");
            Assert.That(seen.Select(s => s.v.GroupId).Distinct().Count(), Is.EqualTo(seen.Count), "Each group in one place.");

            var moving = seen.FirstOrDefault(s => s.v.State == TravelGroupState.Travelling);
            if (moving.v != null)
            {
                Assert.That(_s.Execute(new AdvanceTimeCommand(90)).IsSuccess, Is.True);
                var later = Travellers.InRegion(_s, W, moving.region).FirstOrDefault(v => v.GroupId == moving.v.GroupId);
                if (later != null) Assert.That(Dist(later.X, later.Z, moving.v.X, moving.v.Z), Is.GreaterThan(1f), "It moves along its road as the clock runs.");
            }
        }

        // ----- Navigation ---------------------------------------------------------------------------------------

        [Test]
        public void TheMap_ShowsThePlayersActualPosition_AndWaypointsMarkOnlyWhatIsKnown()
        {
            Ok(new EnterOverlandCommand());
            var region = W.FindRegion(P.RegionId);
            var (x, z) = AtlasQueries.PlayerAt(W);
            Assert.That(x, Is.EqualTo(region.AtlasX * AtlasQueries.RegionCells + P.Pose.X / OverlandScale.MetresPerCell).Within(0.01f), "The atlas marker is the pose itself.");
            Assert.That(z, Is.EqualTo(region.AtlasZ * AtlasQueries.RegionCells + P.Pose.Z / OverlandScale.MetresPerCell).Within(0.01f));
            SaveAndLoad("marker");
            Assert.That(AtlasQueries.PlayerAt(W).x, Is.EqualTo(x).Within(0.01f), "The same after loading.");

            var hidden = W.Places.First(p => p.ParentKind == PlaceParentKind.Region && p.Discovery < PlaceDiscovery.Discovered);
            var refused = _s.Dispatch(new SetWaypointCommand("place:" + hidden.PlaceId));
            Assert.That(refused.IsSuccess, Is.False, "An unknown place cannot be marked: nothing is given away.");
            Assert.That(P.Waypoint, Is.Null);

            var town = W.Settlements.First(s => s.RegionId == P.RegionId && s.SettlementId != P.LastSettlementId);
            Ok(new SetWaypointCommand("settlement:" + town.SettlementId));
            var view = _s.GetWaypoint();
            Assert.That(view.InThisRegion, Is.True);
            Assert.That(view.Metres, Is.GreaterThan(0f));
            Assert.That(view.Line, Does.Contain(town.Name));

            var far = W.Settlements.First(s => s.RegionId != P.RegionId && W.FindRegion(s.RegionId).Discovery != RegionDiscovery.Unknown);
            Ok(new SetWaypointCommand("settlement:" + far.SettlementId));
            view = _s.GetWaypoint();
            Assert.That(view.InThisRegion, Is.False);
            Assert.That(view.NextExitId, Is.Not.Null, "Another region: the compass points to the way on toward it.");
            Assert.That(_s.GetOverland().Sites.Any(s => s.Kind == OverlandSiteKind.RegionExit && s.Id == view.NextExitId), Is.True, "That way on is in this region.");
            SaveAndLoad("waypoint");
            Assert.That(P.Waypoint, Is.EqualTo("settlement:" + far.SettlementId), "Kept.");
            Ok(new SetWaypointCommand(null));
            Assert.That(P.Waypoint, Is.Null);
        }

        [Test]
        public void AJourney_IsSteppedOff_WalkedFrom_RejoinedWhereItWasLeft_AndEndedByArrivingOnFoot()
        {
            Create();
            var home = Realmkeeper.Simulation.Atlas.SimulationTiers.PlayersRegion(W);
            var start = W.Settlements.First(s => s.RegionId == home);
            var dest = W.Settlements.First(s => s.RegionId == home && s.SettlementId != start.SettlementId && W.FindRoute(start.SettlementId, s.SettlementId) != null);
            Ok(new Realmkeeper.Exploration.Application.EnterSettlementCommand(start.SettlementId));
            Ok(new StartJourneyCommand(dest.SettlementId));
            Assert.That(_s.Travel(1).IsSuccess, Is.True);
            var before = TravelCommandHandlers.Position(W);
            var minutes = QuestClockNow();

            Ok(new EnterOverlandCommand()); // off the road, on foot
            Assert.That(P.IsOverland, Is.True);
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Halted), "The journey waits where it was left.");
            Assert.That(QuestClockNow(), Is.EqualTo(minutes), "Stepping off takes no hidden time.");
            Assert.That(OverlandRoads.PointOf(_s, W, before.segment, before.from, before.metres, out var region, out var rx, out var rz), Is.True);
            Assert.That(Dist(P.Pose.X, P.Pose.Z, rx, rz), Is.LessThan(60f), "Standing on the road where the journey is.");
            Assert.That(P.Waypoint, Is.EqualTo("journey"), "The compass remembers where the road was left.");
            Assert.That(AtlasQueries.PlayerAt(W).x, Is.EqualTo(W.FindRegion(region).AtlasX * AtlasQueries.RegionCells + P.Pose.X / OverlandScale.MetresPerCell).Within(0.01f));

            var t = _s.GetOverlandTerrain(RealmIds.Surface, P.RegionId);
            var away = Enumerable.Range(1, 40).Select(i => (x: rx + 12f * i, z: rz)).FirstOrDefault(c => t.Walkable(c.x, c.z) && Dist(c.x, c.z, rx, rz) > 200f);
            Assume.That(away.x, Is.Not.EqualTo(0f), "Open ground beside the road.");
            StandAt(away.x, away.z);
            Assert.That(_s.Dispatch(new RejoinJourneyCommand()).IsSuccess, Is.False, "Too far from where the road was left.");
            SaveAndLoad("off-the-road");
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Halted), "The waiting journey is kept.");
            StandAt(rx, rz);
            Ok(new RejoinJourneyCommand());
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));
            Assert.That(P.LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));
            var after = TravelCommandHandlers.Position(W);
            Assert.That(after.metres, Is.EqualTo(before.metres), "Taken up again exactly where it was: no double movement.");

            // Off again, then walk to the destination and go in: the journey is done.
            Ok(new EnterOverlandCommand());
            var site = _s.GetOverland().Sites.FirstOrDefault(s => s.Kind == OverlandSiteKind.Settlement && s.Id == dest.SettlementId.ToString());
            Assume.That(site, Is.Not.Null);
            StandAt(site.X, site.Z);
            Ok(new EnterSettlementFromOverlandCommand(dest.SettlementId));
            Assert.That(W.Journey.Status, Is.EqualTo(JourneyStatus.Arrived), "Arriving on foot ends the journey.");
        }

        [Test]
        public void TurningToTheMap_FromTheCountry_PutsThePlayerOnTheNearestRoad_AndBackOutThere()
        {
            Ok(new EnterOverlandCommand());
            var t = _s.GetOverlandTerrain(RealmIds.Surface, P.RegionId);
            // Somewhere well away from the towns.
            var spot = Enumerable.Range(0, 32 * 32).Select(i => OverlandScale.CellCentre(new MapCoord(2 + (i % 32) * 4, 2 + (i / 32) * 4)))
                .Where(c => t.Walkable(c.x, c.z) && W.Settlements.Where(s => s.RegionId == P.RegionId).All(s => Dist(OverlandQueries.Gate(t, s.Position).x, OverlandQueries.Gate(t, s.Position).z, c.x, c.z) > 900f))
                .Where(c => OverlandRoads.NearestRoad(W, t, c.x, c.z, out _, out _, out _, out var rx, out var rz) && Dist(rx, rz, c.x, c.z) < 300f)
                .FirstOrDefault();
            Assume.That(spot.x, Is.Not.EqualTo(0f), "A spot near a road, far from towns.");
            StandAt(spot.x, spot.z);
            var point = _s.GetMapPoint();
            Assert.That(point.Value.label, Is.EqualTo("the nearest road"));
            Ok(new LeaveOverlandCommand());
            Assert.That(P.LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));
            Assert.That(TravelCommandHandlers.OnTheRoad(W), Is.True, "On the road, as a road position the planner understands.");
            var (seg, from, metres) = TravelCommandHandlers.Position(W);
            Assert.That(seg, Is.Not.Null);
            Assert.That(OverlandRoads.PointOf(_s, W, seg, from, metres, out _, out var bx, out var bz), Is.True);
            Assert.That(Dist(bx, bz, point.Value.x, point.Value.z), Is.LessThan(80f), "The map position is the road point walked to.");
            Ok(new EnterOverlandCommand());
            Assert.That(Dist(P.Pose.X, P.Pose.Z, point.Value.x, point.Value.z), Is.LessThan(80f), "Back out at the same place.");
        }

        // ----- Encounters ---------------------------------------------------------------------------------------

        /// <summary>Walks the player within a population's range, half an hour at a time, until an encounter comes (or 40 tries).</summary>
        private OverlandEncounterData WalkUntilMet(WorldSession s, Func<WorldData> world)
        {
            var w = world();
            var p = w.Player;
            var t = s.GetOverlandTerrain(RealmIds.Surface, p.RegionId);
            var pop = w.CreaturePopulations.Where(x => x.RegionId == p.RegionId && x.Count > 0 && !w.Groups.Any(g => g.PopulationId == x.PopulationId)).OrderBy(x => x.PopulationId).FirstOrDefault();
            if (pop == null) return null;
            var (cx, cz) = OverlandQueries.Approach(t, pop.Center, 4);
            for (var i = 0; i < 40; i++)
            {
                Assert.That(s.Execute(new AdvanceTimeCommand(OverlandEncounters.BucketMinutes)).IsSuccess, Is.True);
                var x = cx + (i % 2 == 0 ? 6f : -6f);
                if (!t.Walkable(x, cz)) x = cx;
                var r = s.Dispatch(new WalkOverlandCommand(new LocalPose(x, t.HeightAt(x, cz), cz, 0f)));
                if (s.GetPendingOverlandEncounter() is OverlandEncounterData e) return e;
                if (!r.IsSuccess) return null;
            }

            return null;
        }

        [Test]
        public void AnEncounter_ComesFromARealSource_IsNotRerolledByLoading_RestsAfterward_AndItsOutcomeIsKept()
        {
            Create();
            Ok(new EnterOverlandCommand());
            var met = WalkUntilMet(_s, () => W);
            Assume.That(met, Is.Not.Null, "Within a population's range, something comes in 20 hours of walking.");
            TestContext.Out.WriteLine($"{met.Id}: {met.Kind} — {met.Text}");
            Assert.That(met.Kind == OverlandEncounterKind.Creature ? W.CreaturePopulations.Any(p => p.PopulationId == met.PopulationId) : W.FindGroup(met.GroupId) != null, Is.True, "Only a real source.");

            SaveAndLoad("met");
            var again = _s.GetPendingOverlandEncounter();
            Assert.That(again?.Id, Is.EqualTo(met.Id), "Loading keeps it: nothing is rolled again.");
            Assert.That(W.Overland.Encounters.Count(e => e.Id == met.Id), Is.EqualTo(1));

            Ok(new ResolveOverlandEncounterCommand(met.Id, OverlandChoice.BackOff));
            Assert.That(W.Overland.FindEncounter(met.Id).State, Is.EqualTo(OverlandEncounterState.Avoided));
            Assert.That(W.Overland.FindEncounter(met.Id).Outcome, Is.Not.Empty);
            Assert.That(_s.Dispatch(new ResolveOverlandEncounterCommand(met.Id, OverlandChoice.Fight)).IsSuccess, Is.False, "Answered once.");

            // The quiet after: no encounter within three hours, however the walking goes.
            var t = _s.GetOverlandTerrain(RealmIds.Surface, P.RegionId);
            for (var i = 0; i < 5; i++)
            {
                Assert.That(_s.Execute(new AdvanceTimeCommand(OverlandEncounters.BucketMinutes)).IsSuccess, Is.True);
                Ok(new WalkOverlandCommand(new LocalPose(P.Pose.X, t.HeightAt(P.Pose.X, P.Pose.Z), P.Pose.Z, 0f)));
            }

            Assert.That(_s.GetPendingOverlandEncounter(), Is.Null, "A quiet spell follows an encounter.");
        }

        [Test]
        public void Encounters_AreDeterministic_TheSameWorldAndWalkMeetTheSameThing()
        {
            Create();
            Ok(new EnterOverlandCommand());
            var met = WalkUntilMet(_s, () => W);
            Assume.That(met, Is.Not.Null);
            var dir = Path.Combine(_dir, "twin");
            Directory.CreateDirectory(dir);
            var twin = NewGame(dir);
            Assert.That(twin.Dispatch(new CreateCharacterCommand(Realmkeeper.Tests.EditMode.Character.ProgressionTests.Choices("fighter"))).IsSuccess, Is.True);
            Assert.That(twin.Dispatch(new EnterOverlandCommand()).IsSuccess, Is.True);
            var twinMet = WalkUntilMet(twin, twin.CurrentWorldSnapshotForTests);
            Assert.That(twinMet?.Id, Is.EqualTo(met.Id));
            Assert.That(twinMet?.Text, Is.EqualTo(met.Text));
        }

        // ----- Ground weather -------------------------------------------------------------------------------------

        private static RegionWeatherData Weather(int wet, int snow, WeatherKind kind, int intensity, int temperature)
        {
            var r = new RegionWeatherData { GroundWetness = wet, SnowCover = snow };
            for (var i = 0; i < RegionWeatherData.PeriodsPerDay; i++) r.Periods.Add(new WeatherPeriodData { Kind = kind, Intensity = intensity, TemperatureC = temperature });
            return r;
        }

        [Test]
        public void GroundConditions_FollowTheWeather_SlowWalkingAsExplained_AndFloodFordsOnlyInHighWater()
        {
            var dry = GroundConditions.Of(Weather(10, 0, WeatherKind.Clear, 0, 15), 600);
            Assert.That(dry.State, Is.EqualTo(GroundState.Dry));
            Assert.That(dry.OffRoadFactor, Is.EqualTo(1f));
            Assert.That(dry.Line, Is.Empty, "Nothing to say on a fine day.");

            var mud = GroundConditions.Of(Weather(75, 0, WeatherKind.Overcast, 30, 10), 600);
            Assert.That(mud.State, Is.EqualTo(GroundState.Muddy));
            Assert.That(mud.OffRoadFactor, Is.GreaterThan(mud.RoadFactor), "Mud costs most off the road.");
            Assert.That(mud.FordsFlooded, Is.False, "Wet ground alone does not flood a ford.");
            Assert.That(mud.Line, Does.Contain("Mud"));

            var flood = GroundConditions.Of(Weather(75, 0, WeatherKind.HeavyRain, 80, 10), 600);
            Assert.That(flood.FordsFlooded, Is.True, "Hard rain on wet ground floods the fords.");
            Assert.That(flood.Line, Does.Contain("bridge"), "And says how to cross instead.");
            Assert.That(flood.SightFactor, Is.LessThan(1f));

            var snow = GroundConditions.Of(Weather(30, 70, WeatherKind.HeavySnow, 70, -5), 600);
            Assert.That(snow.State, Is.EqualTo(GroundState.DeepSnow));
            Assert.That(snow.FordsFlooded, Is.False);

            var fog = GroundConditions.Of(Weather(20, 0, WeatherKind.Fog, 70, 8), 600);
            Assert.That(fog.SightFactor, Is.LessThan(0.5f), "Thick fog hides what is near.");

            // The walking time charged follows the same factors.
            var t = _s.GetOverlandTerrain(RealmIds.Surface, W.Regions[0].RegionId);
            var (gx, gz) = Enumerable.Range(0, 128 * 128).Select(i => OverlandScale.CellCentre(new MapCoord(i % 128, i / 128)))
                .First(c => t.Walkable(c.x, c.z) && t.Walkable(c.x + 10, c.z) && !t.CellAt(c.x, c.z).Road && !t.CellAt(c.x + 10, c.z).Road && !t.CellAt(c.x, c.z).Crossing);
            var plain = t.WalkMinutes(gx, gz, gx + 10, gz, false);
            Assert.That(t.WalkMinutes(gx, gz, gx + 10, gz, false, mud), Is.EqualTo(plain * mud.OffRoadFactor).Within(plain * 0.02), "Mud's factor, exactly.");
            Assert.That(t.WalkMinutes(gx, gz, gx + 10, gz, false, dry), Is.EqualTo(plain).Within(1e-9), "Dry ground changes nothing.");

            // The same weather, the same ground: the rule reads the world's saved weather and nothing else.
            var w = W;
            var region = w.Regions[0].RegionId;
            var a = GroundConditions.Of(w, region, w.MinuteOfDay);
            var b = GroundConditions.Of(w, region, w.MinuteOfDay);
            Assert.That(a.State == b.State && a.FordsFlooded == b.FordsFlooded && a.Line == b.Line, Is.True);
        }

        // ----- Saves -----------------------------------------------------------------------------------------------

        [Test]
        public void AVersion25Save_LoadsAtVersion26_WithNothingFoundOrMet_AndTheNewStateRoundTrips()
        {
            Create();
            Ok(new EnterOverlandCommand());
            var l = Landmarks.InRegion(_s, W, RealmIds.Surface, P.RegionId).First();
            StandAt(l.X + 2f, l.Z);
            var town = W.Settlements.First(s => s.RegionId == P.RegionId);
            Ok(new SetWaypointCommand("settlement:" + town.SettlementId));
            var before = W;
            SaveAndLoad("v26");
            Assert.That(W.Overland.Landmarks.Select(x => x.LandmarkId + x.Found), Is.EqualTo(before.Overland.Landmarks.Select(x => x.LandmarkId + x.Found)));
            Assert.That(W.Overland.LastCheckBucket, Is.EqualTo(before.Overland.LastCheckBucket));
            Assert.That(P.Waypoint, Is.EqualTo(before.Player.Waypoint));

            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["schemaVersion"] = 25;
            ((JObject)doc["world"]).Remove("overland");
            ((JObject)doc["world"]["player"]).Remove("waypoint");
            File.WriteAllText(Path.Combine(_dir, "old" + JsonWorldStore.Extension), doc.ToString());
            Assert.That(_s.Execute(new LoadWorldCommand("old")).IsSuccess, Is.True);
            Assert.That(W.SchemaVersion, Is.EqualTo(26));
            Assert.That(W.Overland.Landmarks, Is.Empty, "A v25 world has found nothing yet.");
            Assert.That(W.Overland.Encounters, Is.Empty);
            Assert.That(P.Waypoint, Is.Null);
            Assert.That(WorldValidator.Validate(W).IsValid, Is.True);
        }

        private long QuestClockNow() => Realmkeeper.Simulation.Quests.QuestClock.Now(W);
    }
}
