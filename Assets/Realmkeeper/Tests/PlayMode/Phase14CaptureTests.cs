using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Evidence capture for Phase 14, not a pass/fail test: lasting conditions in the HUD, a fight among walls with
    /// cover, a household and a theft in the journal, a door locked for the night, the new places on the map and inside —
    /// the catacombs and their sleepers, the fane on fire, the culverts' bridge, the outlaws' den, the hamlet — and a
    /// follower. Runs only when RK_CAPTURE_DIR is set. Every caption says what the picture shows, nothing more.
    /// </summary>
    public sealed class Phase14CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;
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
            File.WriteAllLines(Path.Combine(_dir, "p14_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Settle(int frames = 6)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private static IEnumerator Look(ExplorationController c, Vector3 at, float back = 3.4f, float pitch = 24f)
        {
            if (c.CameraRig.Perspective != CameraPerspective.ThirdPerson) c.TogglePerspective();
            var from = c.Player.transform.position;
            var dir = new Vector3(at.x - from.x, 0f, at.z - from.z);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            var stand = at - dir * back;
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            c.Player.Teleport(new LocalPose(stand.x, from.y, stand.z, yaw));
            c.CameraRig.SetView(yaw, pitch);
            c.CameraRig.Snap();
            yield return Settle();
        }

        private static Camera MapCamera() => Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x => x.isActiveAndEnabled);

        private static Vector3 RoomCentre(ExplorationController c, string room)
        {
            var r = c.PlaceScene.Room(room);
            return PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, r.Floor, r.Rect.CenterX, r.Rect.CenterZ);
        }

        [UnityTest]
        public IEnumerator Capture1_Conditions_Fights_AndCrime()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);

            // A home locked for the night.
            yield return Phase14Driver.Out(c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 23 * 60);
            foreach (var key in c.BuildingDoors.Select(d => d.TargetId).Distinct().Take(14).ToList())
            {
                yield return Phase14Driver.Out(c);
                var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == key);
                if (door == null) continue;
                yield return Phase14Driver.Try(c, door);
                if (c.InPlace) continue;
                var topics = c.Hud.Topics?.Query<Button>().ToList() ?? new List<Button>();
                if (!topics.Any(b => b.text.Contains("Force the door"))) continue;
                yield return Look(c, door.InteractionPoint, 2.6f, 14f);
                yield return Phase14Driver.Try(c, door);
                yield return Shoot(c.CameraRig.Camera, "p14_08_locked_for_the_night", $"23:00 at {door.Label}: \"{c.LastPlaceMessage}\" — offered: {string.Join(" / ", topics.Select(b => b.text))}");
                break;
            }

            // Lasting conditions in the HUD.
            yield return ItemDriver.Give(Runtime, c, s, w =>
            {
                w["minuteOfDay"] = 10 * 60;
                var list = (JArray)(w["player"]["rpg"]["conditions"] ?? (w["player"]["rpg"]["conditions"] = new JArray()));
                list.Add(new JObject { ["type"] = "Poisoned", ["level"] = 0, ["source"] = "trap:poison_needle", ["reason"] = "a poisoned needle", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "Restoration" });
                list.Add(new JObject { ["type"] = "Exhaustion", ["level"] = 1, ["source"] = "cold", ["reason"] = "cold water", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "LongRestLevel" });
                list.Add(new JObject { ["type"] = "Restrained", ["level"] = 0, ["source"] = "trap:snare_net", ["reason"] = "a net", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "Escape", ["escapeDc"] = 12 });
            }, ("hooded_lantern", 1, null));
            yield return Settle(8);
            yield return Shoot(c.CameraRig.Camera, "p14_01_lasting_conditions_hud", "The conditions panel in town: " + c.ConditionsText.Replace("\n", " ").Trim());

            // A fight among a crypt's walls: the grid with walls, doors and cover from the player to each foe.
            yield return ItemDriver.Give(Runtime, c, s, w => ((JArray)w["player"]["rpg"]["conditions"]).Clear());
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p => p["discovery"] = "Discovered", ("thieves_tools", 1, null));
            var crypt = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, crypt.PlaceId);
            var rest = s.RestHere(Realmkeeper.Domain.Rpg.RestKind.Long);
            yield return Look(c, RoomCentre(c, c.PlaceScene.PlayerRoom) + Vector3.forward, 2.0f, 14f);
            yield return Shoot(c.CameraRig.Camera, "p14_02_rest_in_a_place", $"Inside {c.PlaceScene.Name}; the HUD's rest line: \"{rest.Describe}\"");
            var scene = c.PlaceScene;
            var foeRoom = scene.Figures.Where(f => f.Role == "foe").Select(f => f.Room).FirstOrDefault(r => PlaceDriver.Path(scene, scene.PlayerRoom, r) != null);
            if (foeRoom != null)
            {
                var path = PlaceDriver.Path(scene, scene.PlayerRoom, foeRoom);
                if (path.Count > 1) yield return PlaceDriver.WalkTo(c, s, path[path.Count - 2].Room);
                if (!c.InFight)
                {
                    var via = c.PlaceScene.Passages.First(p => p.Id == path[path.Count - 1].Via);
                    for (var i = 0; i < 4 && via.Kind != ConnectionKind.Opening && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Open && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Broken; i++)
                    {
                        c.ActOn(via.Id, c.PlaceScene.Passages.First(p => p.Id == via.Id).Door == DoorState.Closed ? PlaceAction.Open : PlaceAction.Force);
                        yield return Settle(2);
                    }

                    if (!c.InFight) c.StepInto(foeRoom);
                    yield return Settle(4);
                }

                if (c.InFight)
                {
                    var snap = c.Combat.Snapshot;
                    var covers = string.Join("; ", snap.Order.Where(o => o.Id != snap.Player.Id).Select(o => o.Name + ": " + o.CoverFromPlayer));
                    yield return Shoot(c.CameraRig.Camera, "p14_03_combat_grid_walls_cover",
                        $"A fight in {c.PlaceScene.Room(foeRoom).Name.ToLowerInvariant()}: {snap.Blocked.Count} wall squares, {snap.Doors.Count} door(s), {snap.LowCover.Count} low cover square(s) on the grid. Cover from you — {covers}");
                    for (var i = 0; i < 60 && c.InFight && c.Combat.Snapshot.Round < 3; i++) { CombatDriver.Step(c); yield return null; }
                    if (c.InFight)
                    {
                        var log = c.Combat.Snapshot.Log.Where(l => l.Text.Contains("cover")).Select(l => l.Text).LastOrDefault();
                        yield return Shoot(c.CameraRig.Camera, "p14_04_combat_cover_in_the_log", "Round " + c.Combat.Snapshot.Round + (log != null ? "; the log: \"" + log + "\"" : "; the order list shows each foe's cover from you."));
                    }

                    yield return Phase14Driver.FightOut(c);
                }
            }

            yield return Phase14Driver.Out(c);
            if (c.InPlace) { yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A); c.LeavePlace(); yield return Settle(); }

            // A household and a theft under a resident's eyes; the journal's Crimes tab.
            var home = Bootstrap.Session.GetSettlements().FirstOrDefault();
            if (!c.IsExploring && home != null)
            {
                Bootstrap.Selection.Select(home.SettlementId);
                Bootstrap.Hud.EnterSelectedSettlement();
                yield return Settle(8);
            }

            var stolen = false;
            foreach (var hour in new[] { 9, 13, 18 })
            {
                yield return Phase14Driver.Out(c);
                yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = hour * 60);
                foreach (var key in c.BuildingDoors.Select(d => d.TargetId).Distinct().Take(14).ToList())
                {
                    yield return Phase14Driver.Out(c);
                    var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == key);
                    if (door == null) continue;
                    yield return Phase14Driver.Try(c, door);
                    if (!c.InPlace) continue;
                    var inside = c.PlaceScene;
                    foreach (var f in inside.Features.Where(f => f.Kind == FeatureKind.Container && !f.Hidden).ToList())
                    {
                        var who = inside.Figures.FirstOrDefault(x => x.Role == "resident" && x.Room == f.Room && x.Activity != "Sleeping");
                        if (who == null || (inside.PlayerRoom != f.Room && PlaceDriver.Path(inside, inside.PlayerRoom, f.Room, avoidLocked: true) == null)) continue;
                        yield return PlaceDriver.WalkTo(c, s, f.Room);
                        if (!s.GetPlaceActions(f.Id).Any(o => o.Action == PlaceAction.Steal && o.Enabled)) continue;
                        var room = c.PlaceScene.Room(f.Room);
                        yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, room.Floor, f.X, f.Z), 2.2f, 16f);
                        yield return Shoot(c.CameraRig.Camera, "p14_05_household_before_theft", $"{inside.Name}, the {room.Name.ToLowerInvariant()}: {who.Name} is here ({who.Activity}) beside the {f.Name.ToLowerInvariant()}.");
                        var before = s.GetCrimes().Count;
                        c.ActOn(f.Id, PlaceAction.Steal);
                        yield return Settle(3);
                        if (s.GetCrimes().Count == before) continue;
                        yield return Shoot(c.CameraRig.Camera, "p14_06_theft_seen", "Taken from the " + f.Name.ToLowerInvariant() + ": \"" + c.LastPlaceMessage + "\"");
                        stolen = true;
                        break;
                    }

                    if (stolen) break;
                }

                if (stolen) break;
            }

            if (stolen)
            {
                c.ToggleHub();
                yield return null;
                c.HubWindow.SelectJournal("Crimes");
                yield return Settle(3);
                var crime = s.GetCrimes().Last();
                yield return Shoot(c.CameraRig.Camera, "p14_07_journal_crimes", $"The journal's Crimes tab: {crime.What} — {crime.Status}{(crime.Known ? ", known to be you" : string.Empty)}; {string.Join("; ", crime.Seen)}");
                c.CloseWindows();
                yield return Settle(2);
            }
        }

        [UnityTest]
        public IEnumerator Capture2_TheNewPlaces()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            var ids = new Dictionary<string, string>();
            string fireRoom = null, bridgeId = null, bridgeA = null, bridgeB = null, parley = null;
            yield return Phase14Driver.KnowTheNewPlaces(Runtime, c, s, ids, (def, p) =>
            {
                var layout = (JObject)p["layout"];
                switch (def)
                {
                    case Phase14Places.Catacomb: Phase14Driver.Clear(p, g => (string)g["wakesOn"] == "story:sealed_vault"); break;
                    case Phase14Places.Temple:
                        Phase14Driver.Clear(p);
                        fireRoom = (string)((JArray)layout["rooms"]).FirstOrDefault(r => (string)r["hazard"] == "fire")?["id"];
                        break;
                    case Phase14Places.Works:
                        Phase14Driver.Clear(p);
                        var b = ((JArray)layout["connections"]).FirstOrDefault(x => (string)x["kind"] == "Bridge");
                        bridgeId = (string)b?["id"];
                        bridgeA = (string)b?["a"];
                        bridgeB = (string)b?["b"];
                        break;
                    case Phase14Places.Hideout:
                        Phase14Driver.Clear(p, g => (string)g["behaviour"] == "Parley");
                        parley = (string)((JArray)layout["groups"]).FirstOrDefault(g => (string)g["behaviour"] == "Parley")?["id"];
                        break;
                    default: Phase14Driver.Clear(p); break;
                }
            }, ("hooded_lantern", 1, null));
            yield return ItemDriver.Give(Runtime, c, s, w => { w["player"]["purse"] = 200; w["minuteOfDay"] = 11 * 60; });

            // The strategic map with the new places.
            c.ReturnToWorldMap();
            yield return Settle(10);
            var names = s.GetKnownPlaces().Where(p => ids.Values.Contains(p.PlaceId)).Select(p => p.Name + " (" + p.Kind + ")").ToList();
            yield return Shoot(MapCamera(), "p14_09_map_new_places", $"The map, with this world's {ids.Count} new places: {string.Join(", ", names)}.");

            if (ids.TryGetValue(Phase14Places.Catacomb, out var catacomb))
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, catacomb);
                var vault = c.PlaceScene.Features.FirstOrDefault(f => f.Id == "story:sealed_vault");
                yield return Look(c, RoomCentre(c, c.PlaceScene.PlayerRoom) + Vector3.forward, 1.6f, 12f);
                yield return Shoot(c.CameraRig.Camera, "p14_10_catacombs_inside", $"{c.PlaceScene.Name}: the {c.PlaceScene.Room(c.PlaceScene.PlayerRoom).Name.ToLowerInvariant()}, dressed as a catacomb.");
                if (vault != null)
                {
                    yield return PlaceDriver.WalkTo(c, s, vault.Room);
                    var room = c.PlaceScene.Room(vault.Room);
                    yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, room.Floor, vault.X, vault.Z), 2.4f, 16f);
                    c.ActOn(vault.Id, PlaceAction.Open);
                    yield return Settle(4);
                    yield return Shoot(c.CameraRig.Camera, "p14_11_vault_sleepers_wake", $"The {vault.Name.ToLowerInvariant()} opened: \"{c.LastPlaceMessage}\"" + (c.InFight ? " — the sleepers are up and the fight begins." : string.Empty));
                    yield return Phase14Driver.FightOut(c);
                }

                yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            }

            if (ids.TryGetValue(Phase14Places.Temple, out var temple) && fireRoom != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, temple);
                var path = PlaceDriver.Path(c.PlaceScene, c.PlaceScene.PlayerRoom, fireRoom);
                if (path != null && path.Count > 1) yield return PlaceDriver.WalkTo(c, s, path[path.Count - 2].Room);
                yield return PlaceDriver.WalkTo(c, s, fireRoom);
                yield return Look(c, RoomCentre(c, fireRoom), 3.0f, 18f);
                yield return Shoot(c.CameraRig.Camera, "p14_12_fane_on_fire", $"{c.PlaceScene.Name}: the {c.PlaceScene.Room(fireRoom).Name.ToLowerInvariant()} burning; \"{c.LastPlaceMessage}\"");
                c.ShowPlaceMap(true);
                yield return Settle(2);
                yield return Shoot(c.CameraRig.Camera, "p14_13_place_map_fire", "The place map: the burning room marked in red.");
                c.ShowPlaceMap(false);
            }

            if (ids.TryGetValue(Phase14Places.Works, out var works) && bridgeId != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, works);
                yield return PlaceDriver.WalkTo(c, s, bridgeA);
                var bridge = c.PlaceScene.Passages.First(p => p.Id == bridgeId);
                var r = c.PlaceScene.Room(bridgeA);
                yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, r.Floor, bridge.Rect.CenterX, bridge.Rect.CenterZ), 3.0f, 22f);
                yield return Shoot(c.CameraRig.Camera, "p14_14_culverts_bridge", $"{c.PlaceScene.Name}: a {c.PlaceScene.Bridges[bridgeId].ToString().ToLowerInvariant()} bridge over the channel.");
                c.StepInto(bridgeB);
                yield return Settle(4);
                c.ShowPlaceMap(true);
                yield return Settle(2);
                yield return Shoot(c.CameraRig.Camera, "p14_15_crossing_and_place_map", $"After crossing: \"{c.LastPlaceMessage}\" — the bridge is now {c.PlaceScene.Bridges[bridgeId].ToString().ToLowerInvariant()}.");
                c.ShowPlaceMap(false);
            }

            if (ids.TryGetValue(Phase14Places.Hideout, out var den) && parley != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, den);
                var room = c.PlaceScene.Figures.FirstOrDefault(f => f.GroupId == parley)?.Room;
                if (room != null)
                {
                    yield return PlaceDriver.WalkTo(c, s, room);
                    var lookout = c.PlaceTargets.FirstOrDefault(t => t.TargetId == parley);
                    if (lookout != null) yield return Phase14Driver.Try(c, lookout);
                    yield return Shoot(c.CameraRig.Camera, "p14_16_outlaws_parley", "The outlaws talk before they fight: " + string.Join(" / ", s.GetPlaceActions(parley).Select(o => o.Label)));
                    c.ActOn(parley, PlaceAction.PayToll);
                    yield return Settle(2);
                }
            }

            if (ids.TryGetValue(Phase14Places.Hamlet, out var hamlet))
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, hamlet);
                yield return Look(c, RoomCentre(c, c.PlaceScene.PlayerRoom) + Vector3.forward * 2f, 3.5f, 20f);
                yield return Shoot(c.CameraRig.Camera, "p14_17_hamlet", $"{c.PlaceScene.Name}: the {c.PlaceScene.Room(c.PlaceScene.PlayerRoom).Name.ToLowerInvariant()} under the open sky.");
            }

            // The map's list of places, with how each stands now.
            if (c.InPlace) c.ReturnToWorldMap();
            yield return Settle(10);
            var states = s.GetKnownPlaces().Where(p => p.State.Count > 0).Select(p => p.Name + ": " + string.Join(", ", p.State)).ToList();
            yield return Shoot(MapCamera(), "p14_18_map_place_states", states.Count > 0 ? "Places and how they stand: " + string.Join("; ", states) : "The map after the visits.");

            // A follower, room to room (the mine's captive).
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Mine, p => { p["discovery"] = "Discovered"; Phase14Driver.Clear(p); });
            var mine = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Mine).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, mine.PlaceId);
            var captive = c.PlaceScene.Features.Single(f => f.Kind == FeatureKind.Captive);
            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            for (var i = 0; i < 3 && !c.PlaceScene.Figures.Any(f => f.Role == "follower"); i++) { c.ActOn(captive.Id, PlaceAction.Free); yield return Settle(2); }
            var next = PlaceDriver.Path(c.PlaceScene, c.PlaceScene.PlayerRoom, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A)?.FirstOrDefault().Room;
            if (next != null) yield return PlaceDriver.WalkTo(c, s, next);
            var follower = c.PlaceScene.Figures.FirstOrDefault(f => f.Role == "follower");
            if (follower != null)
            {
                var room = c.PlaceScene.Room(follower.Room);
                yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, room.Floor, follower.X, follower.Z), 2.4f, 14f);
                yield return Shoot(c.CameraRig.Camera, "p14_19_follower", $"{follower.Name} follows into the {room.Name.ToLowerInvariant()}; the HUD: {c.ConditionsText.Replace("\n", " ").Trim()}");
            }
        }

        private IEnumerator Shoot(Camera camera, string name, string note)
        {
            Log.Add(name + ": " + note);
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(s => (s, s.targetTexture, s.clearColor)).ToList();
            foreach (var s in settings)
            {
                s.targetTexture = rt;
                s.clearColor = false;
            }

            var cameraTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = cameraTarget;
            yield return null;
            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            foreach (var (s, target, clear) in previous)
            {
                s.targetTexture = target;
                s.clearColor = clear;
            }

            rt.Release();
            Object.Destroy(rt);
        }
    }
}
