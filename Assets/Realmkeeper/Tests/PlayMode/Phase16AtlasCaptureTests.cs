using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Evidence capture for Phase 16 (automated, not manual verification): the world atlas over the connected world, a
    /// region inspected, the roads and crossings, the ways to a settlement compared, a journey under way with the party,
    /// a caravan and a world event inspected, the faction and economy layers, outlaws on the road, a road cut by a fallen
    /// bridge, and the arrival. Runs only when RK_CAPTURE_DIR is set; each caption says what the picture shows, and
    /// where the test changed the world to show something (its licence), the caption says so.
    /// </summary>
    public sealed class Phase16AtlasCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "p16_atlas_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static string Side(RealmkeeperHud hud) => string.Join(" / ", hud.AtlasRoot.Q("rk-atlas-side").Query<Label>().ToList().Select(l => l.text).Take(8));

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static void Layers(RealmkeeperHud hud, params string[] on)
        {
            foreach (var l in RealmkeeperHud.AtlasLayers) hud.SetAtlasLayer(l, on.Length == 0 || on.Contains(l));
        }

        [UnityTest]
        public IEnumerator Capture_Phase16Atlas()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 2, ids);
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);
            yield return ItemDriver.Give(Runtime, c, s, w => w["player"]["purse"] = 2000);
            yield return AtlasDriver.ToAtlas(Bootstrap, c);
            s.Execute(new AdvanceDaysCommand(12)); // the world runs: caravans out, events, news
            hud.ToggleAtlas(true);
            yield return null;
            var v = hud.AtlasSnapshot;

            // 1. The full map, every layer.
            Layers(hud);
            hud.SelectOnAtlas(null);
            yield return Shoot("p16_01_atlas_full", $"World atlas, all layers: {v.Regions.Count} regions, {v.Settlements.Count} settlements, {v.Routes.Count} roads ({v.Routes.Count(r => r.Crossing)} border crossings), {v.Groups.Count} groups on the roads, {v.Events.Count} events the player knows of. Clock {v.Clock}.");

            // 2. A region inspected.
            hud.SelectOnAtlas("region:" + v.PlayerRegion);
            yield return Shoot("p16_02_region_inspection", "The home region inspected: " + Side(hud));

            // 3. The connections: terrain, settlements and roads only; a border crossing selected.
            Layers(hud, "Terrain", "Settlements", "Roads");
            var crossing = v.Routes.First(r => r.Crossing);
            hud.SelectOnAtlas("route:" + crossing.RouteId);
            yield return Shoot("p16_03_connections", $"Roads and border crossings ({v.Routes.Count(r => r.Crossing)} crossings); a {crossing.Kind} crossing selected: " + Side(hud));

            // 4. A route chosen: a settlement selected, its ways from the route data.
            Layers(hud);
            var near = AtlasDriver.Near(hud, s);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + near.SettlementId);
            yield return null;
            yield return Shoot("p16_04_route_selection", $"{near.Name} selected: the ways there from the route data. " + Side(hud));

            // 5. Alternatives compared, to a settlement beyond the border.
            var far = AtlasDriver.Beyond(hud, far: true);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + far.SettlementId);
            yield return null;
            var options = s.GetTravelOptions(far.SettlementId);
            yield return Shoot("p16_05_route_comparison", $"{far.Name} (another region): {options.Count} ways compared — " + string.Join("; ", options.Select(o => $"{o.Km:0} km, {o.Duration}")));

            // 6, 7. On the road with the party.
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            for (var i = 0; i < 3 && hud.AtlasSnapshot.Journey != null && !hud.AtlasSnapshot.Journey.ThreatPending; i++)
            {
                AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                yield return null;
            }

            var j = hud.AtlasSnapshot.Journey;
            Layers(hud, "Terrain", "Settlements", "Roads", "Travel");
            hud.SelectOnAtlas("player");
            yield return Shoot("p16_06_travel_progress", j == null ? "Journey ended early." : $"Travelling to {j.Destination}: {j.PercentDone}% done, {j.KmLeft:0} km left, next stop {j.NextStop}. Strip: travel 1 h, travel on, halt, give up.");
            yield return Shoot("p16_07_party_on_map", $"The party on the map: the player's mark reads \"{hud.AtlasRoot.Q<Button>("rk-atlas-mark-player")?.text}\"; inspector: " + Side(hud));

            // 8. A caravan inspected.
            Layers(hud);
            var caravan = hud.AtlasSnapshot.Groups.FirstOrDefault(g => g.Kind == TravelGroupKind.Caravan) ?? hud.AtlasSnapshot.Groups.FirstOrDefault();
            if (caravan != null)
            {
                hud.SelectOnAtlas("group:" + caravan.GroupId);
                yield return Shoot("p16_08_caravan_inspection", $"{caravan.Name} inspected ({caravan.Kind}, {caravan.State}): " + Side(hud));
            }
            else Log.Add("p16_08_caravan_inspection: no group on the roads at this moment (not captured).");

            // 9. A world event inspected (now if the player knows of one; otherwise after the arrival, when news has reached them).
            var eventShot = false;
            var ev = hud.AtlasSnapshot.Events.FirstOrDefault();
            if (ev != null)
            {
                hud.SelectOnAtlas("event:" + ev.EventId);
                yield return Shoot("p16_09_event_inspection", $"A world event the player knows of ({ev.Known}, from {ev.KnownFrom}): " + Side(hud));
                eventShot = true;
            }

            // 10. Faction influence.
            Layers(hud, "Terrain", "Settlements", "Factions");
            hud.SelectOnAtlas("region:" + hud.AtlasSnapshot.PlayerRegion);
            yield return Shoot("p16_10_faction_layer", "The faction layer: settlements tinted by their faction; region influence in the inspector: " + Side(hud));

            // 11. The economy.
            Layers(hud, "Terrain", "Settlements", "Roads", "Economy");
            yield return Shoot("p16_11_economy_layer", "The economy layer: settlements short of food marked, caravans on the roads; region economy: " + Side(hud));

            // 12. Outlaws on the road (bold ones: the test's licence, their numbers raised through a save edit).
            Layers(hud);

            yield return AtlasDriver.ArmTheRoads(Runtime, c, s, 8);
            hud.ToggleAtlas(true);
            for (var i = 0; i < 200 && hud.AtlasSnapshot.Journey != null && !hud.AtlasSnapshot.Journey.ThreatPending && hud.AtlasSnapshot.Journey.Status != JourneyStatus.Arrived; i++)
            {
                if (AtlasDriver.Has(hud, "rk-atlas-night")) AtlasDriver.Click(hud, "rk-atlas-night");
                else if (AtlasDriver.Has(hud, "rk-atlas-resume")) AtlasDriver.Click(hud, "rk-atlas-resume");
                else if (AtlasDriver.Has(hud, "rk-atlas-travel-hour")) AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                else break;
                yield return null;
            }

            j = hud.AtlasSnapshot.Journey;
            if (j != null && j.ThreatPending)
            {
                hud.SelectOnAtlas("player");
                yield return Shoot("p16_12_travel_encounter", $"Outlaws bar the road to {j.Destination} (road bands made bold for the capture): fight, slip past (Stealth) or pay {j.Toll} coin.");
                AtlasDriver.Click(hud, "rk-atlas-threat-pay");
                yield return null;
            }
            else Log.Add("p16_12_travel_encounter: no outlaws met before the journey ended (not captured).");

            // 13. A road cut: the bridge on the way to the nearest town fallen (the test's licence, through a save edit).
            if (hud.AtlasSnapshot.Journey != null) { AtlasDriver.Click(hud, "rk-atlas-abandon"); yield return null; }
            var from = s.GetPlayer().LastSettlementId;
            var cut = hud.AtlasSnapshot.Routes.FirstOrDefault(r => (r.A == from || r.B == from) && !r.Crossing) ?? hud.AtlasSnapshot.Routes.First(r => !r.Crossing);
            yield return ItemDriver.Give(Runtime, c, s, w =>
            {
                foreach (JObject r in (JArray)w["routes"])
                {
                    if ((string)r["routeId"] != cut.RouteId) continue;
                    r["hasBridge"] = true;
                    r["bridge"] = "Destroyed";
                }
            });
            hud.ToggleAtlas(true);
            Layers(hud, "Terrain", "Settlements", "Roads", "Threats");
            hud.SelectOnAtlas("route:" + cut.RouteId);
            yield return null;
            yield return Shoot("p16_13_disrupted_route", "A road cut by a fallen bridge (set for the capture): " + Side(hud));

            // 14. Arrival: to a reachable town of the region, entered on arrival.
            Layers(hud);
            var dest = hud.AtlasSnapshot.Settlements.Where(x => x.RegionId == hud.AtlasSnapshot.PlayerRegion && !x.IsHere && s.GetTravelOptions(x.SettlementId).Count > 0).OrderBy(x => s.GetTravelOptions(x.SettlementId)[0].Hours).FirstOrDefault();
            if (dest == null) Log.Add("p16_14_arrival: no reachable settlement in the region from where the player stood (not captured).");
            else
            {
                AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + dest.SettlementId);
                yield return null;
                var go = s.Dispatch(new Realmkeeper.Application.Atlas.StartJourneyCommand(dest.SettlementId));
                yield return null;
                yield return AtlasDriver.TravelOn(hud);
                for (var i = 0; i < 120 && !c.IsExploring; i++) yield return null;
                for (var i = 0; i < 30; i++) yield return null;
                if (c.IsExploring)
                {
                    // Arrived in the dark: wait in town for the morning, so the picture shows the place.
                    var minute = s.GetWorldTime().MinuteOfDay;
                    if (minute < 8 * 60 || minute > 18 * 60) s.Execute(new AdvanceTimeCommand((10 * 60 - minute + 1440) % 1440));
                    yield return Wait(1f);
                    c.CameraRig.SetView(c.Player.transform.eulerAngles.y + 20f, 14f); // over the shoulder, into the town
                    c.CameraRig.Snap();
                    yield return Wait(0.5f);
                    yield return Shoot("p16_14_arrival", $"Arrived in {s.GetPlayer().SettlementName}: the town entered on arrival, the party with the player ({c.Companions.Count} companion bodies).", c.CameraRig.Camera);
                    Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True);
                    for (var i = 0; i < 6; i++) yield return null;
                    hud.ToggleAtlas(true);
                }
                else
                {
                    var jj = s.GetAtlas().Journey;
                    Log.Add($"p16_14_arrival: not arrived (start: {go.Message}; journey {(jj == null ? "none" : jj.Status + " " + jj.Reason + " " + jj.PercentDone + "%")}; in settlement {s.GetPlayer().IsInSettlement}) (not captured).");
                }
            }

            // 9 (deferred). A world event inspected, once news has reached the player.
            var known = hud.AtlasSnapshot.Events;
            if (!eventShot && known.Count > 0)
            {
                Layers(hud);
                hud.SelectOnAtlas("event:" + known[0].EventId);
                yield return Shoot("p16_09_event_inspection", $"A world event the player knows of ({known[0].Known}, from {known[0].KnownFrom}): " + Side(hud));
            }
            else if (!eventShot) Log.Add("p16_09_event_inspection: the player knew of no event (not captured).");

            // 15. A world event and the settlement it touches: the settlement's inspector lists it.
            var touched = known.FirstOrDefault(e => e.Touches.Count > 0);
            var shortage = hud.AtlasSnapshot.Settlements.FirstOrDefault(x => x.Shortage);
            if (touched != null)
            {
                var place = hud.AtlasSnapshot.Settlements.FirstOrDefault(x => touched.Touches.Contains(x.SettlementId));
                Layers(hud, "Terrain", "Settlements", "Roads", "Threats", "Events");
                hud.SelectOnAtlas("settlement:" + place?.SettlementId);
                yield return Shoot("p16_15_event_affecting_settlement", $"{place?.Name}: the inspector lists what the player knows is happening there or on its roads: " + Side(hud));
            }
            else if (shortage != null)
            {
                Layers(hud, "Terrain", "Settlements", "Roads", "Economy", "Events");
                hud.SelectOnAtlas("settlement:" + shortage.SettlementId);
                yield return Shoot("p16_15_event_affecting_settlement", $"{shortage.Name}, short of food (economy layer marks it): " + Side(hud));
            }
            else Log.Add("p16_15_event_affecting_settlement: no known event or shortage at this moment (not captured).");
        }

        private IEnumerator Shoot(string name, string note, Camera camera = null, int width = 1920, int height = 1080)
        {
            yield return null;
            Log.Add(name + ": " + note);
            camera = camera ?? Camera.main;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(x => (x, x.targetTexture, x.clearColor)).ToList();
            foreach (var x in settings) { x.targetTexture = rt; x.clearColor = false; }
            yield return null;
            if (camera != null)
            {
                var cameraTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = cameraTarget;
            }

            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            foreach (var (x, target, clear) in previous) { x.targetTexture = target; x.clearColor = clear; }
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
