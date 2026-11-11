using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
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
    /// Evidence capture for Phase 13, not a pass/fail test: places in the running game — a town house and the people in it,
    /// the map with a place found, the mine from its mouth to its lower level and the fight in its den, the crypt with its
    /// tombs, a hazard, a trap and its chest, the fort's gate and its watch, and the mine again after the rescue.
    /// Runs only when RK_CAPTURE_DIR is set. Every caption says what the picture shows, nothing more.
    /// </summary>
    public sealed class Phase13CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p13_capture_log.txt"), Log);
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

        /// <summary>Third person, the camera behind the character looking towards a point (the subject of the picture).</summary>
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

        /// <summary>What stands in a room, by name (captions say only what is there).</summary>
        private static string InView(ExplorationController c, string room)
        {
            var things = c.PlaceScene.Features.Where(f => f.Room == room && !f.Hidden).Select(f => f.Name.ToLowerInvariant()).Distinct().ToList();
            var people = c.PlaceScene.Figures.Where(f => f.Room == room).Select(f => f.Name).ToList();
            var parts = new List<string>();
            if (things.Count > 0) parts.Add("in the room: " + string.Join(", ", things));
            if (people.Count > 0) parts.Add("present: " + string.Join(", ", people));
            return parts.Count == 0 ? "nothing else in the room" : string.Join("; ", parts);
        }

        private static Vector3 RoomCentre(ExplorationController c, string room)
        {
            var r = c.PlaceScene.Room(room);
            return PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, r.Floor, r.Rect.CenterX, r.Rect.CenterZ);
        }

        [UnityTest]
        public IEnumerator Capture1_Places_InTheRunningGame()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 19 * 60 + 30, ("hooded_lantern", 1, null), ("thieves_tools", 1, null));

            // 11, 12. A settlement interior, with the people who live there.
            var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == "house-3") ?? c.BuildingDoors.FirstOrDefault(d => d.TargetId.StartsWith("house-")) ?? c.BuildingDoors.First();
            yield return PlaceDriver.Use(c, door);
            Assert.That(c.InPlace, Is.True);
            var resident = c.PlaceScene.Figures.FirstOrDefault(f => f.Role == "resident");
            var main = c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A;
            yield return Look(c, RoomCentre(c, main) + Vector3.forward * 1.5f, 1.2f, 10f);
            yield return Shoot(c.CameraRig.Camera, "p13_11_settlement_interior",
                $"Inside {c.PlaceScene.Name}: the {c.PlaceScene.Room(main).Name.ToLowerInvariant()}, built from the building's footprint and function ({c.PlaceScene.Rooms.Count} rooms on {c.PlaceScene.Floors.Count} floor(s)).");
            if (resident != null)
            {
                var room = c.PlaceScene.Room(resident.Room);
                if (room != null && resident.Room != c.PlaceScene.PlayerRoom) c.StepInto(resident.Room);
                yield return Settle();
                yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, room.Floor, resident.X, resident.Z), 2.0f, 12f);
                var talked = c.StartDialogue(resident.NpcId);
                yield return Settle();
                yield return Shoot(c.CameraRig.Camera, "p13_12_npc_inside_building",
                    talked.IsSuccess ? $"{resident.Name}, at home ({resident.Activity}), spoken to through the conversation window: \"{c.Dialogue.View?.NpcLine}\""
                                     : $"{resident.Name} at home ({resident.Activity}); they cannot talk now: {talked.Message}");
                if (c.Dialogue.IsOpen) c.EndDialogue();
            }

            c.StepInto(main);
            yield return Settle(2);
            yield return PlaceDriver.Use(c, c.PlaceTargets.First(t => t.Target == PlaceInteractable.TargetKind.Exit));

            // The rescue's asking, so the mine is found (the giver says where it is).
            var instance = s.FindQuestInstance(QuestCatalog.LostBelow);
            var bindings = s.GetQuestBindings(instance);
            var giver = new NpcId(Guid.Parse(bindings["giver"]));
            var mineId = bindings["place"];
            var home = s.FindNpc(giver).HomeSettlementId;
            c.ReturnToWorldMap();
            yield return Settle();
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600);
            Bootstrap.Selection.Select(home);
            Bootstrap.Hud.EnterSelectedSettlement();
            yield return Settle(8);
            Runtime.Playback.Pause();
            c.StartDialogue(giver);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.LostBelow);
            yield return null;
            QuestDriver.Say(c, "accept");
            yield return Settle(3);
            if (c.Dialogue.IsOpen) c.EndDialogue();

            // 14. The map: the mine, found, with its marker and its entry in the list of places.
            c.ReturnToWorldMap();
            yield return Settle(8);
            var list = Bootstrap.Hud.PlaceList;
            if (list != null && list.ClassListContains("rk-hidden")) list.RemoveFromClassList("rk-hidden");
            var marker = Bootstrap.Hud.PlaceMarkers[mineId];
            Bootstrap.Hud.FocusPlace(mineId);
            yield return Settle(10);
            var mapCamera = Camera.main ?? Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(x => x.isActiveAndEnabled);
            yield return Settle();
            yield return Shoot(mapCamera, "p13_14_map_discovered_location",
                $"The strategic map after the giver told the way: a marker where {s.GetPlaceSummary(mineId).Name} lies, and the Places list with its way in. Places the player has not heard of are not shown.");

            // 2, 3, 5. The mine: its mouth, a working, its ladder.
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, mineId);
            var entrance = c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance);
            var mouth = PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, 0, entrance.Rect.X + entrance.Rect.W / 2, c.PlaceScene.Room(entrance.A).Rect.Z);
            yield return Look(c, mouth, 3.5f, 8f);
            yield return Shoot(c.CameraRig.Camera, "p13_02_mine_entrance", $"{c.PlaceScene.Name}: the mouth of the mine from inside, daylight beyond the way out; timber shoring, the first room.");
            yield return Look(c, RoomCentre(c, entrance.A) + Vector3.forward * 3f, 3.5f, 10f);
            yield return Shoot(c.CameraRig.Camera, "p13_01_dungeon_entrance", $"The entrance room of {c.PlaceScene.Name}: the HUD names the place, the floor and the room; the passages lead on.");

            var ledger = c.PlaceScene.Features.First(f => f.DefinitionId == "foremans_ledger");
            yield return PlaceDriver.WalkTo(c, s, ledger.Room);
            var at = PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, c.PlaceScene.Room(ledger.Room).Floor, ledger.X, ledger.Z);
            yield return Look(c, at, 2.2f, 16f);
            yield return Shoot(c.CameraRig.Camera, "p13_03_mine_interior", $"Inside the workings: the {c.PlaceScene.Room(ledger.Room).Name.ToLowerInvariant()}, timber shoring and beams ({InView(c, ledger.Room)}).");
            c.ActOn(ledger.Id, PlaceAction.Read);
            yield return Settle();
            yield return Shoot(c.CameraRig.Camera, "p13_07_discovery", "A discovery: the foreman's ledger, read — why the mine was abandoned. It is kept in the journal's Discoveries.");

            var ladder = c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Ladder);
            yield return PlaceDriver.WalkTo(c, s, ladder.A);
            var foot = PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, c.PlaceScene.Room(ladder.A).Floor, ladder.Rect.X, ladder.Rect.Z);
            yield return Look(c, foot + Vector3.up * 0.5f, 2.6f, 12f);
            yield return Shoot(c.CameraRig.Camera, "p13_05_vertical_connection", "The ladder down the shaft to the lower level (taken with the interact key; the floor changes in the HUD).");

            // 4. Several rooms, and the map of what has been seen.
            c.ShowPlaceMap(true);
            yield return Settle();
            yield return Shoot(c.CameraRig.Camera, "p13_04_multi_room", "The place map (N): only the rooms already seen, on each floor, with the passages between them; gold marks where the player stands.");
            c.ShowPlaceMap(false);

            // 9. The den: the fight among the walls.
            var captive = c.PlaceScene.Features.Single(f => f.Kind == FeatureKind.Captive);
            c.Climb(ladder.Id);
            yield return Settle();
            var encounterShot = false;
            if (c.InFight)
            {
                yield return Settle(4);
                yield return Shoot(c.CameraRig.Camera, "p13_09_creature_encounter", "A fight with the mine's inhabitants at the foot of the ladder, on the place's own grid: walls are dark squares nobody can enter; the combat window lists who acts.");
                encounterShot = true;
            }

            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            yield return Settle(4);
            Log.Add("(at the captive: room " + c.PlaceScene.PlayerRoom + " = " + captive.Room + ")");
            if (c.InFight)
            {
                yield return Settle(4);
                if (!encounterShot) yield return Shoot(c.CameraRig.Camera, "p13_09_creature_encounter", "The den: the fight with its inhabitants, on the place's own grid — walls are dark squares nobody can enter.");
                for (var i = 0; i < 1500 && c.InFight; i++)
                {
                    if (c.Combat.Prompt != null) LevelUpDriver.Click(c.Combat.Root, "xk-combat-prompt-decline");
                    else CombatDriver.Step(c);
                    yield return null;
                }

                c.CloseWindows();
                yield return Settle();
            }

            for (var i = 0; i < 3 && !c.PlaceScene.Figures.Any(f => f.Role == "follower"); i++)
            {
                var freed = c.ActOn(captive.Id, PlaceAction.Free);
                Log.Add("(free attempt: " + (freed.IsSuccess ? freed.Message : "refused: " + freed.Message) + ")");
            }

            yield return Settle();
            Log.Add("(after the den: room " + c.PlaceScene.PlayerRoom + ", in fight " + c.InFight + ", follower " + c.PlaceScene.Figures.Any(f => f.Role == "follower") + ")");

            // 10. Loot: what the fight left, or a crate, in Storage.
            var pile = c.PlaceScene.LootPiles.FirstOrDefault();
            if (pile.Key != null) c.OpenStorage(pile.Key);
            else
            {
                var crate = c.PlaceScene.Features.FirstOrDefault(f => f.Kind == FeatureKind.Container && f.Room == c.PlaceScene.PlayerRoom);
                if (crate != null) c.ActOn(crate.Id, PlaceAction.Open);
            }

            yield return Settle();
            if (!c.HubWindow.IsOpen)
            {
                var crate = c.PlaceScene.Features.FirstOrDefault(f => f.Kind == FeatureKind.Container && c.PlaceScene.Room(f.Room) != null && !c.PlaceScene.Room(f.Room).Secret);
                if (crate != null)
                {
                    yield return PlaceDriver.WalkTo(c, s, crate.Room);
                    yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, c.PlaceScene.Room(crate.Room).Floor, crate.X, crate.Z), 1.8f, 20f);
                    c.ActOn(crate.Id, PlaceAction.Open);
                    if (crate.ContainerKey != null) c.OpenStorage(crate.ContainerKey);
                    yield return Settle();
                }
            }

            if (c.HubWindow.IsOpen)
                yield return Shoot(c.CameraRig.Camera, "p13_10_loot_interaction", "Loot in the place: the container opened in the Storage section, with Take and Take all — real items with one holder.");
            c.CloseWindows();

            // Out and home with the follower, then back (15 comes at the end).
            yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            c.LeavePlace();
            yield return Settle();
            Bootstrap.Selection.Select(home);
            Bootstrap.Hud.EnterSelectedSettlement();
            yield return Settle(8);

            // 6, 8. The crypt: its tombs, foul air.
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p =>
            {
                p["discovery"] = "Discovered";
                var layout = (JObject)p["layout"];
                var entranceRoom = (string)((JArray)layout["connections"]).First(x => (string)x["kind"] == "Entrance")["a"];
                var next = ((JArray)layout["connections"]).First(x => (string)x["kind"] != "Entrance" && (string)x["kind"] != "Stairs" && (string)x["kind"] != "Ladder" && (string)x["kind"] != "Secret" &&
                                                                     ((string)x["a"] == entranceRoom || (string)x["b"] == entranceRoom));
                var nextRoom = (string)next["a"] == entranceRoom ? (string)next["b"] : (string)next["a"];
                foreach (JObject r in (JArray)layout["rooms"]) if ((string)r["id"] == nextRoom) r["hazard"] = "toxic_air";
            });
            var cryptId = s.GetKnownPlaces().First(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name).PlaceId;
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, cryptId);
            var toxic = c.PlaceScene.Rooms.First(r => r.Hazard == "toxic_air");
            yield return PlaceDriver.WalkTo(c, s, toxic.Id);
            yield return Look(c, RoomCentre(c, toxic.Id), 2.8f, 18f);
            yield return Shoot(c.CameraRig.Camera, "p13_08_hazard", $"A hazard: foul air in the {toxic.Name.ToLowerInvariant()} (green haze), met through a Constitution save — the HUD's status line says so. {c.LastPlaceMessage}");
            var tomb = c.PlaceScene.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Tomb && !r.Secret && c.PlaceScene.Figures.All(f => f.Room != r.Id || f.Role != "foe"));
            tomb = tomb ?? c.PlaceScene.Rooms.First(r => r.Kind == RoomKind.Tomb && !r.Secret);
            yield return PlaceDriver.WalkTo(c, s, tomb.Id);
            var box = c.PlaceScene.Features.FirstOrDefault(f => f.Room == tomb.Id && f.Kind == FeatureKind.Container);
            yield return Look(c, box != null ? PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, tomb.Floor, box.X, box.Z) : RoomCentre(c, tomb.Id), 2.6f, 16f);
            yield return Shoot(c.CameraRig.Camera, "p13_06_ruin_or_crypt", $"{c.PlaceScene.Name}: the {tomb.Name.ToLowerInvariant()}, dressed-stone walls ({InView(c, tomb.Id)}).");
            var trap = c.PlaceScene.Features.FirstOrDefault(f => f.Kind == FeatureKind.Trap);
            if (trap != null && trap.Hidden)
            {
                yield return PlaceDriver.WalkTo(c, s, trap.Room);
                for (var i = 0; i < 3 && c.PlaceScene.Features.First(f => f.Id == trap.Id).Hidden; i++) c.SearchRoom();
                yield return Settle();
                trap = c.PlaceScene.Features.First(f => f.Id == trap.Id);
                if (trap.Hidden) trap = null;
            }

            if (trap != null)
            {
                yield return PlaceDriver.WalkTo(c, s, trap.Room);
                yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, c.PlaceScene.Room(trap.Room).Floor, trap.X, trap.Z), 2.2f, 30f);
                yield return Shoot(c.CameraRig.Camera, "p13_08b_trap", $"A trap the player noticed: the {trap.Name.ToLowerInvariant()}, drawn once found; Disarm needs the tools and a check.");
            }

            yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            c.LeavePlace();

            // 13. The fort: the gate and its watch.
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Fort, p => p["discovery"] = "Discovered");
            var fortId = s.GetKnownPlaces().First(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Fort).Name).PlaceId;
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, fortId);
            var guard = c.PlaceScene.Figures.FirstOrDefault(f => f.Role == "guard" && !f.NpcId.IsEmpty);
            if (guard != null)
            {
                yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, 0, guard.X, guard.Z), 3.0f, 10f);
                c.StartDialogue(guard.NpcId);
                yield return Settle();
                var gate = c.Dialogue.View?.Responses.FirstOrDefault(r => r.Id.StartsWith("gate:"));
                if (gate != null) QuestDriver.Say(c, gate.Id);
                yield return Settle();
            }

            yield return Shoot(c.CameraRig.Camera, "p13_13_faction_location", $"{c.PlaceScene.Name}: the gatehouse and its watch ({guard?.Name}); the gate is held until they give leave — by standing, Persuasion or coin.");
            if (c.Dialogue.IsOpen) c.EndDialogue();
            yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            c.LeavePlace();
            yield return Settle();

            // 15. The mine again: the den is quiet, the timber thrown aside, nobody held there.
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, mineId);
            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            yield return Look(c, PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, c.PlaceScene.Room(captive.Room).Floor, captive.X, captive.Z), 2.6f, 18f);
            yield return Shoot(c.CameraRig.Camera, "p13_15_after_return",
                (c.PlaceScene.Features.First(f => f.Id == captive.Id).Flags & FeatureFlags.Freed) != 0
                    ? $"Back in {c.PlaceScene.Name}'s den after the rescue: no fight, nobody held, the timber thrown aside — visits {s.GetPlaceSummary(mineId).Visits}, state {s.GetPlaceSummary(mineId).Discovery}."
                    : $"Back in {c.PlaceScene.Name}'s den: no fight (its inhabitants stay defeated); the captive is still held (the rescue was not completed in this run) — visits {s.GetPlaceSummary(mineId).Visits}, state {s.GetPlaceSummary(mineId).Discovery}.");
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
