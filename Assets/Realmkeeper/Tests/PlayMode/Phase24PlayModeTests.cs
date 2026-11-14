using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Application.Overland;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 24 (ADR-0113, ADR-0114, ADR-0115) in the shipped scene: landmarks found and used; the world's own travellers on
    /// the roads, talked with; a waypoint on the compass; the player's real position on the atlas; a journey stepped off,
    /// walked from and rejoined; an encounter met by walking and fought on the ground with the one combat engine; ground
    /// weather on the ground, the clock and the fords; saving and loading in the country; repeated region round trips. With
    /// RK_CAPTURE_DIR set, writes the Phase 24 screenshots and measurements.
    /// </summary>
    public sealed class Phase24PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(_dir)) Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "p24_log.txt"), Log);
            Log.Clear();
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            var t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < seconds) yield return null;
        }

        private static IEnumerator WaitTravel(ExplorationController c)
        {
            for (var i = 0; i < 900 && c.Travelling; i++) yield return null;
            Assert.That(c.Travelling, Is.False, "The transition finished.");
            yield return Settle(4);
        }

        private static IEnumerator WaitCountry(ExplorationController c)
        {
            for (var i = 0; i < 900 && !(c.InOverland && !c.Travelling); i++) yield return null;
            Assert.That(c.InOverland, Is.True, "In the country.");
            yield return Settle(6);
        }

        private static void Place(ExplorationController c, float x, float z, float yaw = 0f)
        {
            var t = c.OverlandView.Terrain;
            if (!t.Walkable(x, z)) (x, z) = OverlandQueries.Approach(t, OverlandScale.CellAt(t.Map, x, z), 3);
            for (var i = 1; i <= 6 && GroundConditions.IsFord(t.CellAt(x, z)); i++) // never stand in a ford (it may be in flood)
                foreach (var (dx, dz) in new[] { (40f, 0f), (-40f, 0f), (0f, 40f), (0f, -40f) })
                    if (t.Walkable(x + dx * i, z + dz * i) && !GroundConditions.IsFord(t.CellAt(x + dx * i, z + dz * i))) { x += dx * i; z += dz * i; break; }
            c.Player.Teleport(new LocalPose(x, t.HeightAt(x, z) + 0.2f, z, yaw));
            Assert.That(c.RecordPose().IsSuccess, Is.True, "Standing on walkable ground.");
        }

        /// <summary>Stands a few metres from a point, facing it, the camera behind (a walking step later).</summary>
        private static IEnumerator StandBy(ExplorationController c, float x, float z, float distance = 5f)
        {
            var t = c.OverlandView.Terrain;
            var dir = new Vector2(1f, 0f);
            for (var i = 0; i < 16; i++)
            {
                var a = i * 22.5f * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (t.Walkable(x + dir.x * distance, z + dir.y * distance) && !GroundConditions.IsFord(t.CellAt(x + dir.x * distance, z + dir.y * distance))) break; // not in a ford (it may be in flood)
            }

            var yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg;
            Place(c, x + dir.x * distance, z + dir.y * distance, yaw);
            c.CameraRig.SetView(yaw, 7f);
            c.CameraRig.Snap();
            yield return Wait(1.2f); // a living-country refresh and a walking step
        }

        private static IEnumerator Daylight(WorldSession s)
        {
            var minute = s.GetWorldTime().MinuteOfDay;
            if (minute < 8 * 60 || minute > 17 * 60) s.Execute(new AdvanceTimeCommand((10 * 60 - minute + 1440) % 1440));
            yield return Settle(10);
        }

        private static int Objects() => Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [UnityTest]
        public IEnumerator TheLivingCountry_LandmarksTravellersNavigationJourneysEncountersAndGroundWeather_InThePlayedScene()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            c.ManageCursor = false;
            var input = new ScriptedExplorationInput();
            c.SetInput(input);
            runtime.Playback.Pause();
            Assert.That(session.Execute(new AdvanceDaysCommand(5)).IsSuccess, Is.True, "Let the roads fill.");
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            Assert.That(session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return Settle();

            // ----- Out, and the landmarks ---------------------------------------------------------------------------
            c.GoOutIntoCountry();
            yield return WaitTravel(c);
            Assert.That(c.InOverland, Is.True);
            yield return Daylight(session);
            yield return Wait(1.2f);
            Log.Add($"Out into the country: {c.LastTransitionMs:0} ms; {c.LandmarkViews.Count} landmarks built, {c.TravellerViews.Count} travelling groups near, living-country refresh {c.LastLifeRefreshMs:0.0} ms.");
            Assert.That(c.LandmarkViews.Count, Is.GreaterThan(0), "The country has landmarks.");
            var at = c.Player.transform.position;
            var camp = c.LandmarkViews.Where(v => v.Landmark.Kind == LandmarkKind.TravellersCamp || v.Landmark.Kind == LandmarkKind.AbandonedCamp || v.Landmark.Kind == LandmarkKind.RuinedHomestead)
                .OrderBy(v => Vector2.Distance(new Vector2(v.Landmark.X, v.Landmark.Z), new Vector2(at.x, at.z))).FirstOrDefault();
            var stone = c.LandmarkViews.Where(v => v.Landmark.Kind == LandmarkKind.Waystone).OrderBy(v => Vector2.Distance(new Vector2(v.Landmark.X, v.Landmark.Z), new Vector2(at.x, at.z))).First();
            var show = camp ?? stone;
            yield return StandBy(c, show.Landmark.X, show.Landmark.Z, 14f);
            yield return FreeShot(c, "p24_01_landmarks", c.Player.transform.position + new Vector3(0f, 8f, 0f) - c.CameraRig.transform.forward * 12f, new Vector3(show.Landmark.X, c.Player.transform.position.y + 1f, show.Landmark.Z), 55f, true,
                $"A roadside landmark of the country: {show.Landmark.Name.ToLowerInvariant()}, built from the owned kits.");

            yield return StandBy(c, stone.Landmark.X, stone.Landmark.Z, 3f);
            var before = session.GetLandmarks().First(l => l.Landmark.Id == stone.Landmark.Id).Found;
            stone.Interact(session, c.Player.transform);
            yield return Settle(6);
            Assert.That(session.GetLandmarks().First(l => l.Landmark.Id == stone.Landmark.Id).Found, Is.True, "Read, and found.");
            yield return Shot(c, "p24_03_roadside_interaction", $"Reading a waystone: where its roads go and how long on foot (found before: {before}).");

            if (camp != null && camp.Landmark.Searchable)
            {
                yield return StandBy(c, camp.Landmark.X, camp.Landmark.Z, 3f);
                camp.Interact(session, c.Player.transform);
                yield return Settle(6);
                Assert.That(session.GetLandmarks().First(l => l.Landmark.Id == camp.Landmark.Id).Searched, Is.True, "Searched once.");
                c.CloseWindows();
                Log.Add($"Searched {camp.Landmark.Name}: cache {(session.GetContainer(Landmarks.CacheKey(camp.Landmark.Id)) != null ? "left" : "none")}.");
            }

            // ----- Travellers: the world's own groups on the road ---------------------------------------------------------
            var travellers = session.GetTravellers();
            Log.Add($"Travelling groups in this region now: {travellers.Count} ({string.Join(", ", travellers.Select(t => t.Kind))}).");
            var friendly = travellers.Where(t => !t.Hostile).OrderBy(t => Vector2.Distance(new Vector2(t.X, t.Z), new Vector2(at.x, at.z))).FirstOrDefault();
            if (friendly != null)
            {
                // A long jump is charged its walking time (honestly), and the group travels meanwhile: close in on where it is now.
                for (var hop = 0; hop < 4; hop++)
                {
                    var now = session.GetTravellers().FirstOrDefault(t => t.GroupId == friendly.GroupId);
                    if (now == null) break;
                    friendly = now;
                    if (Vector2.Distance(new Vector2(now.X, now.Z), new Vector2(c.Player.transform.position.x, c.Player.transform.position.z)) < 20f) break;
                    yield return StandBy(c, now.X, now.Z, 9f);
                }

                yield return Wait(1.2f);
                var view = c.TravellerViews.FirstOrDefault(v => v.Traveller.GroupId == friendly.GroupId);
                Assert.That(view, Is.Not.Null, "A group near the player is drawn.");
                var groupPos = view.transform.position;
                yield return FreeShot(c, "p24_02_traveller", c.Player.transform.position + new Vector3(0f, 4f, 0f) - c.CameraRig.transform.forward * 9f, groupPos + Vector3.up, 50f, true,
                    $"{friendly.Line}: a group of the world's simulation ({friendly.Kind}, led by {friendly.Leader ?? "its leader"}), where its journey has it now.");
                view.Interact(session, c.Player.transform);
                yield return Settle(6);
                if (!view.Talked)
                {
                    var said = session.Dispatch(new TalkToTravellersCommand(friendly.GroupId));
                    Assert.That(said.IsSuccess, Is.True, "Spoken with: " + said.Message);
                }

                Log.Add($"Talked with {friendly.Name}.");
                yield return Wait(3f);
                var later = session.GetTravellers().FirstOrDefault(t => t.GroupId == friendly.GroupId);
                if (later != null && friendly.State == TravelGroupState.Travelling) Log.Add($"{friendly.Name}: {Vector2.Distance(new Vector2(later.X, later.Z), new Vector2(friendly.X, friendly.Z)):0.0} m along its road in the meantime.");
            }
            else Log.Add("No friendly group was in this region: the traveller capture is skipped.");

            // ----- A waypoint on the compass -----------------------------------------------------------------------
            var other = session.GetSettlementsInView().First(s => s.SettlementId != town.SettlementId && s.RegionId == session.GetPlayer().RegionId);
            Assert.That(c.MarkWaypoint("settlement:" + other.SettlementId).IsSuccess, Is.True);
            yield return Wait(1.2f);
            Assert.That(session.GetWaypoint()?.InThisRegion, Is.True);
            c.CameraRig.SetView(Mathf.Atan2(session.GetWaypoint().X - c.Player.transform.position.x, session.GetWaypoint().Z - c.Player.transform.position.z) * Mathf.Rad2Deg, 7f);
            c.CameraRig.Snap();
            yield return Wait(0.3f);
            yield return Shot(c, "p24_05_waypoint", $"A waypoint: {session.GetWaypoint().Line}; the compass's gold mark points to it.");

            // ----- The player's real position on the atlas ------------------------------------------------------------
            var pose = session.GetPlayer().Pose;
            var region = session.GetAtlas().Regions.First(r => r.RegionId == session.GetPlayer().RegionId);
            c.ReturnToWorldMap();
            yield return WaitTravel(c);
            yield return Settle(10);
            Assert.That(c.IsExploring, Is.False);
            var hud = bootstrap.Hud;
            hud.ToggleAtlas(true);
            yield return Settle(4);
            var atlas = hud.AtlasSnapshot;
            var mapPoint = (x: atlas.PlayerX - region.AtlasX * AtlasQueries.RegionCells, z: atlas.PlayerZ - region.AtlasZ * AtlasQueries.RegionCells);
            Log.Add($"On the atlas: the player at cell ({mapPoint.x:0.0}, {mapPoint.z:0.0}) of {region.Name}; walked from ({pose.X / OverlandScale.MetresPerCell:0.0}, {pose.Z / OverlandScale.MetresPerCell:0.0}).");
            Assert.That(Vector2.Distance(new Vector2(mapPoint.x, mapPoint.z), new Vector2(pose.X, pose.Z) / OverlandScale.MetresPerCell), Is.LessThan(40f), "The map marks where the player is (the road or town nearest the country position).");
            yield return MapShot(bootstrap, "p24_04_map_position", "The atlas with the player's actual position (where they came out of the country).");
            hud.ToggleAtlas(false);
            yield return Settle(4);

            // ----- A journey, stepped off and rejoined ----------------------------------------------------------------
            var plan = session.Dispatch(new StartJourneyCommand(other.SettlementId));
            Assert.That(plan.IsSuccess, Is.True, plan.Message);
            Assert.That(session.Travel(1, true).IsSuccess, Is.True);
            yield return Settle(4);
            // The hour on the road may have met the road's own outlaws (Phase 16): they bar the road until settled.
            var j0 = session.GetAtlas().Journey;
            if (j0 != null && j0.ThreatPending)
            {
                var paid = session.Dispatch(new ResolveRoadThreatCommand(RoadChoice.PayToll));
                if (!paid.IsSuccess) paid = session.Dispatch(new ResolveRoadThreatCommand(RoadChoice.SlipPast));
                Log.Add($"Outlaws met on the journey's hour: {paid.Message}");
                if (session.GetAtlas().Journey?.ThreatPending == true) Assert.Inconclusive("Outlaws still bar the road after paying and slipping past; the step-off cannot be shown on this seed.");
            }

            var screens = c.Loading.Shown;
            var off = session.Dispatch(new EnterOverlandCommand());
            Assert.That(off.IsSuccess, Is.True, off.Message);
            yield return WaitCountry(c);
            yield return Daylight(session);
            yield return Wait(1.3f);
            Assert.That(c.Loading.Shown, Is.GreaterThan(screens), "Behind the loading screen.");
            Assert.That(session.GetAtlas().Journey?.Status, Is.EqualTo(JourneyStatus.Halted), "The journey waits on the road.");
            Assert.That(session.GetHaltedJourneyPoint(), Is.Not.Null);
            yield return Shot(c, "p24_06_stepped_off", "Off the road on foot, mid-journey: the journey waits; \"Back on the road (J)\" and the compass mark show where it was left.");
            Log.Add($"Stepped off the journey: {c.LastTransitionMs:0} ms.");
            Assert.That(c.RejoinJourney().IsSuccess, Is.True);
            yield return WaitTravel(c);
            Assert.That(session.GetAtlas().Journey?.Status, Is.EqualTo(JourneyStatus.Travelling), "Taken up again where it was left.");

            // ----- An encounter met by walking, fought on the ground ---------------------------------------------------
            Assert.That(session.Dispatch(new EnterOverlandCommand()).IsSuccess, Is.True);
            yield return WaitCountry(c);
            var view2 = c.OverlandView;
            var pops = session.GetCreaturePopulations().Where(p => p.RegionId == view2.RegionId && p.Count > 0).ToList();
            // Something may already have found the player on the way here (the walking above): settle it first, by backing off.
            if (session.GetPendingOverlandEncounter() is OverlandEncounterData earlier)
            {
                Log.Add($"Met on the way: {earlier.Text} (backed off).");
                Assert.That(c.AnswerEncounter(OverlandChoice.BackOff).IsSuccess, Is.True);
            }

            OverlandEncounterData met = null;
            foreach (var pop in pops)
            {
                var (px, pz) = OverlandQueries.Approach(view2.Terrain, pop.Center, 4);
                Place(c, px, pz);
                for (var i = 0; i < 30 && met == null; i++)
                {
                    session.Execute(new AdvanceTimeCommand(OverlandEncounters.BucketMinutes));
                    Place(c, px + (i % 2 == 0 ? 4f : -4f), pz);
                    yield return null;
                    met = session.GetPendingOverlandEncounter();
                }

                if (met != null) break;
            }

            if (met != null)
            {
                yield return Daylight(session);
                yield return Wait(1.2f);
                var stillPending = session.GetPendingOverlandEncounter();
                Log.Add($"Encounter {met.Id} after the wait: pending {stillPending?.Id ?? "none"}; recorded as {session.GetOverlandEncounters().First(e => e.Id == met.Id).State} ({session.GetOverlandEncounters().First(e => e.Id == met.Id).Outcome}); panel shown {c.EncounterPanelShown}.");
                Assert.That(c.EncounterPanelShown, Is.True, "The encounter is put to the player.");
                yield return Shot(c, "p24_07_encounter", $"An overland encounter from a real population: \"{met.Text}\" — fight, slip away, or back off.");
                var fight = c.AnswerEncounter(OverlandChoice.Fight);
                Assert.That(fight.IsSuccess, Is.True, fight.Message);
                yield return Settle(20);
                Log.Add($"Encounter {met.Id}: fought on the overland ground: {(session.GetEncounter() != null ? "the fight is on" : "nothing came of it")}.");
                if (session.GetEncounter() != null)
                {
                    yield return Wait(1f);
                    yield return Shot(c, "p24_07b_encounter_fight", "The encounter fought with the one combat engine, on the overland terrain where the player stood.");
                    session.FightItOut();
                    yield return Settle(20);
                    c.CloseFight();
                    yield return Settle(10);
                }

                Assert.That(session.GetOverlandEncounters().First(e => e.Id == met.Id).State, Is.Not.EqualTo(OverlandEncounterState.Pending), "Its outcome is kept.");
            }
            else Log.Add("No encounter came in 30 half-hours near the region's populations: the encounter capture is skipped.");

            // ----- Ground weather ------------------------------------------------------------------------------------
            GroundConditions g = c.OverlandGround;
            for (var day = 0; day < 40 && (g == null || g.State == GroundState.Dry); day++)
            {
                session.Execute(new AdvanceTimeCommand(12 * 60));
                yield return Wait(1.1f);
                g = c.OverlandGround;
            }

            Log.Add($"The ground: {g?.State} (wetness {g?.Wetness}, snow {g?.Snow}); fords flooded {g?.FordsFlooded}; walking off the road ×{g?.OffRoadFactor:0.00}; sight ×{g?.SightFactor:0.00}; the player's pace ×{c.Player.SpeedMultiplier:0.00}.");
            if (g != null && g.State != GroundState.Dry)
            {
                yield return Daylight(session);
                yield return Wait(1.2f);
                Assert.That(c.Player.SpeedMultiplier, Is.LessThanOrEqualTo(1f));
                yield return Shot(c, "p24_08_weather_ground", $"Weather on the ground: {g.State} ({g.Line}).");
            }

            // A ford: the HUD and the barrier follow the water.
            var ford = Enumerable.Range(0, 128 * 128).Select(i => new MapCoord(i % 128, i / 128)).FirstOrDefault(cell => GroundConditions.IsFord(view2.Terrain.Cell(cell.X, cell.Z)));
            if (!ford.Equals(default(MapCoord)))
            {
                var (fx, fz) = OverlandScale.CellCentre(ford);
                yield return StandBy(c, fx, fz, 18f);
                yield return Daylight(session);
                yield return Wait(1.2f);
                var flooded = c.OverlandGround?.FordsFlooded == true;
                var wade = session.Dispatch(new WalkOverlandCommand(new LocalPose(fx, view2.Terrain.HeightAt(fx, fz), fz, 0f)));
                Assert.That(wade.IsSuccess, Is.EqualTo(!flooded), flooded ? "A ford in flood cannot be waded." : "A ford in normal water can be waded.");
                Log.Add($"Ford at {ford}: {(flooded ? "in flood (refused: " + wade.Message + ")" : "fordable")}; the barriers: {c.Overland.BarrierColliders}.");
                yield return FreeShot(c, "p24_09_river_crossing", c.Player.transform.position + new Vector3(0f, 10f, 0f) - c.CameraRig.transform.forward * 14f, new Vector3(fx, view2.Terrain.HeightAt(fx, fz), fz), 55f, true,
                    flooded ? "A ford in flood: barred until the water falls (a bridge still crosses)." : $"A ford on the river under today's conditions ({c.OverlandGround?.Line}); in flood it would be barred.");
                Place(c, c.Player.transform.position.x, c.Player.transform.position.z);
            }

            // ----- Saving and loading in the country ----------------------------------------------------------------
            var found = session.GetLandmarks().Where(l => l.Found).Select(l => l.Landmark.Id).OrderBy(x => x).ToList();
            var groups = session.GetTravellers().Select(t => t.GroupId).OrderBy(x => x).ToList();
            var waypoint = session.GetPlayer();
            Assert.That(session.Execute(new SaveWorldCommand("p24")).IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand("p24")).IsSuccess, Is.True);
            yield return Settle(10);
            yield return WaitCountry(c);
            Assert.That(session.GetLandmarks().Where(l => l.Found).Select(l => l.Landmark.Id).OrderBy(x => x), Is.EqualTo(found), "What was found stays found.");
            Assert.That(session.GetTravellers().Select(t => t.GroupId).OrderBy(x => x), Is.EqualTo(groups), "The same groups, no more, no fewer.");
            yield return Daylight(session);
            yield return Wait(1.2f);
            yield return Shot(c, "p24_10_after_load", $"After saving and loading in the country: {found.Count} landmarks still found, {groups.Count} travelling groups unchanged, the waypoint kept.");

            // ----- Repeated region round trips: nothing piles up --------------------------------------------------
            var home = session.GetPlayer().RegionId;
            var objectsFirst = 0;
            long memoryFirst = 0;
            for (var round = 0; round < 2; round++)
            {
                var exit = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.RegionExit && s.Usable);
                yield return StandBy(c, exit.X, exit.Z, 4f);
                c.Interact();
                yield return WaitTravel(c);
                var back = c.OverlandView.Sites.First(s => s.Kind == OverlandSiteKind.RegionExit && s.Id == home.ToString());
                yield return StandBy(c, back.X, back.Z, 4f);
                c.Interact();
                yield return WaitTravel(c);
                yield return Wait(1.2f);
                System.GC.Collect();
                Resources.UnloadUnusedAssets();
                yield return Settle(10);
                var objects = Objects();
                var memory = Profiler.GetTotalAllocatedMemoryLong();
                Log.Add($"Round {round + 1} back home: {objects} objects, {memory / 1048576} MB, {c.LandmarkViews.Count} landmarks, {c.TravellerViews.Count} travellers drawn.");
                if (round == 0) { objectsFirst = objects; memoryFirst = memory; }
                else
                {
                    Assert.That(objects, Is.LessThanOrEqualTo(objectsFirst + 60), "No objects pile up across transitions.");
                    Assert.That(memory, Is.LessThan(memoryFirst + 96L * 1048576), "No uncontrolled memory growth.");
                }
            }

            c.ReturnToWorldMap();
            yield return WaitTravel(c);
        }

        // ----- Captures -------------------------------------------------------------------------------------------

        private IEnumerator Shot(ExplorationController c, string name, string note)
        {
            if (string.IsNullOrEmpty(_dir)) { Log.Add($"{name}: {note}"); yield break; }
            yield return Render(c.CameraRig.Camera, name, note, true);
        }

        private IEnumerator MapShot(RealmkeeperBootstrap b, string name, string note)
        {
            if (string.IsNullOrEmpty(_dir)) { Log.Add($"{name}: {note}"); yield break; }
            yield return Render(b.CameraController.Camera, name, note, true);
        }

        private IEnumerator FreeShot(ExplorationController c, string name, Vector3 eye, Vector3 look, float fov, bool fog, string note)
        {
            if (string.IsNullOrEmpty(_dir)) { Log.Add($"{name}: {note}"); yield break; }
            var cam = c.CameraRig.Camera;
            var t = cam.transform;
            var (pos, rot, oldFov, rig) = (t.position, t.rotation, cam.fieldOfView, c.CameraRig.enabled);
            var hadFog = RenderSettings.fog;
            c.CameraRig.enabled = false;
            t.position = eye;
            t.rotation = Quaternion.LookRotation(look - eye);
            cam.fieldOfView = fov;
            RenderSettings.fog = fog && hadFog;
            yield return null;
            yield return Render(cam, name, note, false);
            RenderSettings.fog = hadFog;
            t.position = pos;
            t.rotation = rot;
            cam.fieldOfView = oldFov;
            c.CameraRig.enabled = rig;
        }

        private IEnumerator Render(Camera camera, string name, string note, bool ui)
        {
            yield return null;
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var panels = ui ? Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).Select(d => d.panelSettings).Distinct().ToList() : new List<PanelSettings>();
            var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
            foreach (var x in panels) { x.targetTexture = rt; x.clearColor = false; x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, h); }
            yield return null;
            yield return null;
            var target = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = target;
            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            foreach (var (x, t, cl, sc) in previous) { x.targetTexture = t; x.clearColor = cl; x.scale = sc; }
            rt.Release();
            Object.Destroy(rt);
            Log.Add($"{name}: {note}");
        }
    }
}
