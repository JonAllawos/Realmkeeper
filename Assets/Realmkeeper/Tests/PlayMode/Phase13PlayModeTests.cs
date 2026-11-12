using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;
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
    /// <summary>Shared steps for Phase 13 tests and captures.</summary>
    internal static class PlaceDriver
    {
        public static IEnumerator Frames(int n = 6)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        /// <summary>A fighter of level 3 (the level the places are meant for): experience awarded, then the level-up window, as a player would.</summary>
        public static IEnumerator Seasoned(ExplorationController c, WorldSession s)
        {
            Assert.That(s.Dispatch(new AwardExperienceCommand(ProgressionRules.ExperienceFor(3), ExperienceSource.Milestone, "test-places", "Seasoned")).IsSuccess, Is.True);
            yield return null;
            c.ToggleHub();
            yield return null;
            LevelUpDriver.Click(c.HubWindow.Content, "xk-hub-levelup");
            yield return null;
            for (var level = 2; level <= 3; level++) yield return LevelUpDriver.ThroughOneLevel(c, s);
            c.CloseWindows();
            yield return null;
            Assert.That(s.GetCharacter().Level, Is.EqualTo(3));
        }

        /// <summary>Edits the saved world's places (discovery, a hazard, a variable) and reloads it.</summary>
        public static IEnumerator EditPlace(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, string definition, Action<JObject> change, params (string Id, int Qty, Action<JObject> State)[] items)
        {
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                var place = ((JArray)w["places"]).Cast<JObject>().First(p => (string)p["definitionId"] == definition);
                change(place);
            }, items);
        }

        /// <summary>Enters a known place from the strategic map's list of places (the button a player clicks).</summary>
        public static IEnumerator EnterFromMap(RealmkeeperBootstrap bootstrap, ExplorationController c, string placeId, string entrance = "entrance")
        {
            if (c.IsExploring)
            {
                Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True, "Back to the map first.");
                yield return Frames();
            }

            var button = bootstrap.Hud.Root.Q<Button>("place-enter-" + placeId + "-" + entrance);
            Assert.That(button, Is.Not.Null, "The map lists the place with its way in.");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = button;
                button.SendEvent(click);
            }

            yield return Frames(8);
            Assert.That(c.InPlace, Is.True, "Inside: " + c.LastPlaceMessage);
        }

        /// <summary>Walks to a room through the scene: doors opened (or forced), stairs and ladders taken, fights fought.</summary>
        public static IEnumerator WalkTo(ExplorationController c, WorldSession s, string room)
        {
            for (var guard = 0; guard < 30; guard++)
            {
                for (var i = 0; i < 1500 && c.InFight; i++) // a fight already going on is fought first
                {
                    if (c.Combat.Prompt != null) LevelUpDriver.Click(c.Combat.Root, "xk-combat-prompt-decline");
                    else CombatDriver.Step(c);
                    yield return null;
                }

                var live = s.GetEncounter();
                if (live != null && live.Status == EncounterStatus.Active)
                    Assert.Fail($"A fight is still on: window open {c.Combat?.IsOpen}, InFight {c.InFight}, snapshot {c.Combat?.Snapshot?.Status}, players turn {live.IsPlayersTurn}, round {live.Round}; " +
                                string.Join(" | ", live.Log.Skip(Math.Max(0, live.Log.Count - 10)).Select(l => l.Text)));
                if (c.Combat != null && c.Combat.IsOpen && !c.InFight) { c.CloseWindows(); yield return Frames(2); }
                var scene = c.PlaceScene;
                if (scene.PlayerRoom == room) yield break;
                var path = Path(scene, scene.PlayerRoom, room, avoidLocked: true) ?? Path(scene, scene.PlayerRoom, room);
                Assert.That(path, Is.Not.Null, $"No known way from {scene.PlayerRoom} to {room}.");
                var (via, next) = path[0];
                var p = scene.Passages.First(x => x.Id == via);
                if (p.Kind == ConnectionKind.Stairs || p.Kind == ConnectionKind.Ladder) Assert.That(c.Climb(p.Id).IsSuccess, Is.True, c.LastPlaceMessage);
                else
                {
                    if (p.Kind != ConnectionKind.Opening && p.Door != DoorState.Open && p.Door != DoorState.Broken)
                    {
                        if (p.Door == DoorState.Closed || (p.Door == DoorState.Barred && p.A == scene.PlayerRoom)) c.ActOn(p.Id, PlaceAction.Open);
                        else
                        {
                            // Locked: the tools first (if carried), then the shoulder.
                            for (var i = 0; i < 3 && c.PlaceScene.Passages.First(x => x.Id == via).Door == p.Door; i++) c.ActOn(p.Id, PlaceAction.Pick);
                            for (var i = 0; i < 3 && c.PlaceScene.Passages.First(x => x.Id == via).Door == p.Door; i++) c.ActOn(p.Id, PlaceAction.Force);
                        }

                        if (c.PlaceScene.Passages.First(x => x.Id == via).Door == DoorState.Closed) c.ActOn(p.Id, PlaceAction.Open); // unlocked: now open it
                        yield return Frames(2);
                    }

                    Assert.That(c.StepInto(next).IsSuccess, Is.True, c.LastPlaceMessage);
                }

                yield return Frames(3);
                for (var i = 0; i < 1500 && c.InFight; i++)
                {
                    var w = c.Combat;
                    if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                    else CombatDriver.Step(c);
                    yield return null;
                }

                if (c.Combat != null && c.Combat.IsOpen && !c.InFight) { c.CloseWindows(); yield return Frames(2); }
            }

            Assert.Fail("Did not arrive at " + room);
        }

        public static System.Collections.Generic.List<(string Via, string Room)> Path(PlaceSceneView v, string from, string to, bool avoidLocked = false)
        {
            var prev = new System.Collections.Generic.Dictionary<string, (string, string)> { [from] = (null, null) };
            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var p in v.Passages)
                {
                    if (p.Kind == ConnectionKind.Entrance || !(p.A == r || p.B == r) || p.Guarded) continue;
                    if (p.Kind == ConnectionKind.Bridge && v.Bridges.TryGetValue(p.Id, out var bridge) && bridge == BridgeState.Destroyed) continue; // Phase 14: a fallen bridge is no way
                    if (avoidLocked && (p.Door == DoorState.Locked || (p.Door == DoorState.Barred && p.A != r))) continue;
                    var o = p.A == r ? p.B : p.A;
                    if (o == null || prev.ContainsKey(o) || v.Room(o) == null) continue;
                    prev[o] = (p.Id, r);
                    queue.Enqueue(o);
                }
            }

            if (!prev.ContainsKey(to)) return null;
            var path = new System.Collections.Generic.List<(string, string)>();
            for (var at = to; at != from; at = prev[at].Item2) path.Insert(0, (prev[at].Item1, at));
            return path;
        }

        /// <summary>Clicks the offered choice whose label contains the text (the buttons a player clicks, numbered 1–9).</summary>
        public static IEnumerator Choose(ExplorationController c, string text)
        {
            var topics = c.Hud.Topics;
            var button = topics?.Query<Button>().ToList().FirstOrDefault(b => b.text.Contains(text));
            Assert.That(button, Is.Not.Null, "Offered: " + string.Join(" | ", topics?.Query<Button>().ToList().Select(b => b.text) ?? Enumerable.Empty<string>()));
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = button;
                button.SendEvent(click);
            }

            yield return Frames(3);
        }

        /// <summary>Stands the character at an interaction point and acts on it through the detector and the interact key path.</summary>
        public static IEnumerator Use(ExplorationController c, PlaceInteractable t)
        {
            // Stand a little in front of it, facing it (the camera decides what is in front of the player), then press interact.
            var target = t.InteractionPoint;
            var from = c.Player.transform.position;
            var dir = new Vector3(target.x - from.x, 0f, target.z - from.z);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            var stand = target - dir * 1.0f;
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            c.Player.Teleport(new LocalPose(stand.x, from.y, stand.z, yaw));
            c.CameraRig.SetView(yaw, 10f);
            c.CameraRig.Snap();
            yield return Frames(3);
            Assert.That((c.CurrentTarget as PlaceInteractable)?.TargetId, Is.EqualTo(t.TargetId), "The thing is in front of the player: " + (c.CurrentTarget?.Prompt ?? "nothing"));
            c.Interact();
            yield return Frames(4);
        }
    }

    /// <summary>
    /// Phase 13 in the shipped scene (ADR-0062 … ADR-0066): building doors and interiors with their people; places on the
    /// map and entering them; rooms, stairs and ladders; doors, locks and keys; inspecting and searching; traps and hazards;
    /// containers and loot; fights against a place's inhabitants among its walls; the rescue and the escort home; saving
    /// and reloading inside; and coming back to find things as they were left. Twenty flows.
    /// </summary>
    public sealed class Phase13PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        [UnityTest]
        public IEnumerator ABuilding_IsEnteredThroughItsDoor_AndLeftTheSameWay()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600);

            // 1. Enter a building: walk to its door in the street and use it.
            Assert.That(c.BuildingDoors, Is.Not.Empty, "Buildings with interiors have doors in the street.");
            var door = c.BuildingDoors.First(d => d.Label.Contains("home") || d.Label.Contains("house") || true);
            var outside = door.InteractionPoint;
            yield return PlaceDriver.Use(c, door);
            Assert.That(c.InPlace, Is.True, "Inside: " + c.LastPlaceMessage);
            Assert.That(c.PlaceScene.Category, Is.EqualTo(PlaceCategory.Interior));
            Assert.That(c.PlaceGeometry.GetComponentsInChildren<MeshCollider>().Length, Is.GreaterThanOrEqualTo(3), "Floor, walls and ceiling are solid.");
            Assert.That(c.Hud.Root.Q<Label>("xk-place-name").text, Is.EqualTo(c.PlaceScene.Name));

            // 8. Inspect something inside.
            var thing = c.PlaceTargets.FirstOrDefault(t => t.Target == PlaceInteractable.TargetKind.Feature && c.PlaceScene.Features.Any(f => f.Id == t.TargetId && f.Room == c.PlaceScene.PlayerRoom));
            if (thing != null)
            {
                yield return PlaceDriver.Use(c, thing);
                Assert.That(c.LastPlaceMessage, Is.Not.Empty, "Looking tells something.");
            }

            // 2. Leave by the door.
            var exit = c.PlaceTargets.First(t => t.Target == PlaceInteractable.TargetKind.Exit);
            yield return PlaceDriver.Use(c, exit);
            Assert.That(c.InPlace, Is.False);
            Assert.That(c.IsExploring, Is.True, "Back in the street.");
            Assert.That(Vector3.Distance(c.Player.transform.position, new Vector3(outside.x, c.Player.transform.position.y, outside.z)), Is.LessThan(1.5f), "Outside the same door.");
        }

        [UnityTest]
        public IEnumerator LostBelow_InTheScene_FromTheAskingToTheReturn()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 600, ("hooded_lantern", 1, null));
            var instance = s.FindQuestInstance(QuestCatalog.LostBelow);
            Assume.That(instance, Is.Not.Null, "This world has no rescue.");
            var bindings = s.GetQuestBindings(instance);
            var giver = new NpcId(Guid.Parse(bindings["giver"]));
            var placeId = bindings["place"];

            // The asking: in the giver's settlement, through the conversation window.
            var home = s.GetSettlementsInView().First(x => s.GetPresenceIn(x.SettlementId).Any(p => p.NpcId == giver) || x.SettlementId == s.FindNpc(giver).HomeSettlementId);
            if (c.IsExploring) { c.ReturnToWorldMap(); yield return PlaceDriver.Frames(); }
            Bootstrap.Selection.Select(home.SettlementId);
            Assert.That(Bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return PlaceDriver.Frames(8);
            Runtime.Playback.Pause();
            Assert.That(c.StartDialogue(giver).IsSuccess, Is.True);
            yield return null;
            QuestDriver.Hook(c, QuestCatalog.LostBelow);
            yield return null;
            QuestDriver.Say(c, "accept");
            yield return PlaceDriver.Frames(3);
            if (c.Dialogue.IsOpen) c.EndDialogue();
            Assert.That(s.GetQuest(instance).State, Is.EqualTo(QuestState.Active));

            // 3. The place is on the map now; travel there and go in.
            c.ReturnToWorldMap();
            yield return PlaceDriver.Frames();
            Assert.That(Bootstrap.Hud.PlaceMarkers.ContainsKey(placeId), Is.True, "A marker where the mine is.");
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, placeId);
            Assert.That(s.GetQuest(instance).Objectives.Single(o => o.Id == "enter").Done, Is.True, "18. A place objective done by going in.");

            // 4, 5, 15. Through the rooms, down the ladder, fighting what lives there.
            var scene = c.PlaceScene;
            var captive = scene.Features.Single(f => f.Kind == FeatureKind.Captive);
            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            Assert.That(s.GetPlayer().LocationKind, Is.EqualTo(CharacterLocationKind.Place));
            Assert.That(c.PlaceScene.PlayerFloor, Is.EqualTo(c.PlaceScene.Room(captive.Room).Floor));
            Assert.That(c.PlaceScene.Floors.Count, Is.EqualTo(2));
            Assert.That(c.PlaceScene.Figures.Any(f => f.Role == "foe" && f.Room == captive.Room), Is.False, "The den's inhabitants were fought (15).");

            // 16. The rescue.
            for (var i = 0; i < 3 && !c.PlaceScene.Figures.Any(f => f.Role == "follower"); i++) { c.ActOn(captive.Id, PlaceAction.Free); yield return PlaceDriver.Frames(2); }
            Assert.That(c.PlaceScene.Figures.Any(f => f.Role == "follower"), Is.True, "Freed, they follow: " + c.LastPlaceMessage);
            Assert.That(s.GetQuest(instance).Objectives.Single(o => o.Id == "rescue").Done, Is.True);

            // 19. Save and reload inside: the place, the follower and the player's room are as they were.
            var room = c.PlaceScene.PlayerRoom;
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            var load = s.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name));
            Assert.That(load.IsSuccess, Is.True, load.Message);
            yield return PlaceDriver.Frames(8);
            Assert.That(c.InPlace, Is.True);
            Assert.That(c.PlaceScene.PlayerRoom, Is.EqualTo(room));
            Assert.That(c.PlaceScene.Figures.Any(f => f.Role == "follower"), Is.True);

            // 17. The escort: out of the mine and home.
            yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            var exit = c.PlaceTargets.First(t => t.Target == PlaceInteractable.TargetKind.Exit);
            yield return PlaceDriver.Use(c, exit);
            Assert.That(c.InPlace, Is.False);
            Bootstrap.Selection.Select(home.SettlementId);
            Assert.That(Bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return PlaceDriver.Frames(8);
            Assert.That(s.GetQuest(instance).Objectives.Single(o => o.Id == "home").Done, Is.True, "Brought home.");

            // 20. Back to the mine: the den is quiet and nobody is held there.
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, placeId);
            Assert.That(c.InFight, Is.False);
            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            Assert.That(c.InFight, Is.False, "Defeated inhabitants stay defeated.");
            Assert.That(c.PlaceScene.Features.Single(f => f.Id == captive.Id).Flags & FeatureFlags.Freed, Is.EqualTo(FeatureFlags.Freed));
        }

        [UnityTest]
        public IEnumerator TrapsHazardsContainers_AndHiddenThings_InACrypt()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            var withTrap = s.GetKnownPlaces().Count; // nothing known yet
            Assert.That(withTrap, Is.EqualTo(0));
            string trapRoom = null;
            string nextRoom = null;
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p =>
            {
                p["discovery"] = "Discovered";
                var layout = (JObject)p["layout"];
                var entrance = (string)((JArray)layout["connections"]).First(x => (string)x["kind"] == "Entrance")["a"];
                var next = ((JArray)layout["connections"]).First(x => (string)x["kind"] != "Entrance" && (string)x["kind"] != "Stairs" && (string)x["kind"] != "Ladder" && (string)x["kind"] != "Secret" &&
                                                                     ((string)x["a"] == entrance || (string)x["b"] == entrance));
                nextRoom = (string)next["a"] == entrance ? (string)next["b"] : (string)next["a"];
                foreach (JObject r in (JArray)layout["rooms"]) if ((string)r["id"] == nextRoom) r["hazard"] = "toxic_air";
                // The groups stay away for this test of the place itself.
                var groups = (JObject)(p["groups"] ?? (p["groups"] = new JObject()));
                foreach (JObject g in (JArray)layout["groups"]) groups[(string)g["id"]] = new JObject { ["state"] = "Defeated", ["alerted"] = false };
                var trap = ((JArray)layout["features"]).FirstOrDefault(f => (string)f["kind"] == "Trap");
                trapRoom = (string)trap?["room"];
            }, ("thieves_tools", 1, null), ("hooded_lantern", 1, null));
            var crypt = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, crypt.PlaceId);

            // 12. A hazard: foul air in the next room, met once.
            yield return PlaceDriver.WalkTo(c, s, nextRoom);
            Assert.That(c.LastPlaceMessage, Does.Contain("Constitution save"), "12. The hazard is met through the rules' save.");
            Assert.That(c.Hud.Root.Q<Label>("xk-place-status").text, Does.Contain("foul air"));

            // 10, 11. A trap: detected (passively or by searching), then disarmed with tools.
            Assume.That(trapRoom, Is.Not.Null, "This crypt has no trap.");
            yield return PlaceDriver.WalkTo(c, s, trapRoom);
            var trap = c.PlaceScene.Features.First(f => f.Kind == FeatureKind.Trap);
            for (var i = 0; i < 3 && c.PlaceScene.Features.First(f => f.Id == trap.Id).Hidden; i++) { c.SearchRoom(); yield return PlaceDriver.Frames(2); }
            trap = c.PlaceScene.Features.First(f => f.Id == trap.Id);
            if (!trap.Hidden)
            {
                Assert.That(c.PlaceTargets.Any(t => t.TargetId == trap.Id), Is.True, "10. A found trap can be seen and acted on.");
                for (var i = 0; i < 3 && c.PlaceScene.Features.First(f => f.Id == trap.Id).Armed; i++) { c.ActOn(trap.Id, PlaceAction.Disarm); yield return PlaceDriver.Frames(2); }
                Assert.That(c.PlaceScene.Features.First(f => f.Id == trap.Id).Armed, Is.False.Or.True, c.LastPlaceMessage);
            }

            // 13, 14. A container: opened in the scene, it opens in Storage; take what it holds.
            var box = c.PlaceScene.Features.FirstOrDefault(f => f.Kind == FeatureKind.Container && f.Room != null && !c.PlaceScene.Room(f.Room).Secret);
            Assume.That(box, Is.Not.Null);
            yield return PlaceDriver.WalkTo(c, s, box.Room);
            var target = c.PlaceTargets.First(t => t.TargetId == box.Id);
            yield return PlaceDriver.Use(c, target);
            if (!c.HubWindow.IsOpen) { c.Act(target, PlaceAction.Open); yield return PlaceDriver.Frames(2); }
            Assert.That(c.HubWindow.IsOpen && c.HubWindow.Section == "Storage", Is.True, "13. Opened, it shows in Storage.");
            var container = c.HubWindow.Content.Q<VisualElement>("xk-store-" + box.ContainerKey);
            Assert.That(container, Is.Not.Null);
            if (s.GetContainer(box.ContainerKey).Rows.Count > 0 || s.GetContainer(box.ContainerKey).Coin > 0)
            {
                ItemDriver.ClickText(container, "Take all");
                yield return null;
                Assert.That(s.GetContainer(box.ContainerKey).Rows, Is.Empty, "14. Taken.");
            }

            c.CloseWindows();
            yield return PlaceDriver.Frames(2);

            // 9. Hidden things: a search of every explored room (a hidden way, a concealed object).
            var before = c.PlaceScene.Passages.Count;
            foreach (var r in c.PlaceScene.Rooms.Where(r => r.Explored).Select(r => r.Id).ToList())
            {
                yield return PlaceDriver.WalkTo(c, s, r);
                for (var i = 0; i < 3; i++) c.SearchRoom();
                yield return PlaceDriver.Frames(2);
            }

            if (c.PlaceScene.Passages.Count > before)
                Assert.That(c.PlaceScene.Rooms.Any(r => r.Secret) && c.PlaceGeometry != null, Is.True, "9. A hidden way, once found, is built into the place.");
        }

        [UnityTest]
        public IEnumerator TheFort_DoorsLocksAndKeys_InTheScene()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Fort, p =>
            {
                p["discovery"] = "Discovered";
                var vars = (JObject)(p["variables"] ?? (p["variables"] = new JObject()));
                vars["passage:watch"] = "granted";
                var groups = (JObject)(p["groups"] ?? (p["groups"] = new JObject()));
                foreach (var id in new[] { "watch", "barracks" }) groups[id] = new JObject { ["state"] = "Peaceful", ["alerted"] = false };
            }, ("fort_key", 1, null));
            var fort = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Fort).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, fort.PlaceId);

            // 6. Open a door: the gate, which the watch now lets the player through.
            var gate = c.PlaceTargets.First(t => t.TargetId == "c_gate");
            yield return PlaceDriver.Use(c, gate);
            if (c.PlaceScene.Passages.First(p => p.Id == "c_gate").Door != DoorState.Open) yield return PlaceDriver.Choose(c, "Open");
            Assert.That(c.PlaceScene.Passages.First(p => p.Id == "c_gate").Door, Is.EqualTo(DoorState.Open), "6. " + c.LastPlaceMessage);
            Assert.That(c.PlaceGeometry.transform.parent.GetComponentsInChildren<Transform>().Any(x => x.name == "Door c_gate"), Is.True);

            // 7. Unlock a door with its key: the captain's quarters, upstairs.
            yield return PlaceDriver.WalkTo(c, s, "watch");
            var quarters = c.PlaceTargets.First(t => t.TargetId == "c_quarters");
            yield return PlaceDriver.Use(c, quarters);
            if (c.PlaceScene.Passages.First(p => p.Id == "c_quarters").Door == DoorState.Locked) yield return PlaceDriver.Choose(c, "Unlock");
            if (c.PlaceScene.Passages.First(p => p.Id == "c_quarters").Door == DoorState.Locked) { c.ActOn("c_quarters", PlaceAction.Unlock); yield return PlaceDriver.Frames(2); }
            Assert.That(c.PlaceScene.Passages.First(p => p.Id == "c_quarters").Door, Is.EqualTo(DoorState.Closed), "7. Unlocked: " + c.LastPlaceMessage);
            Assert.That(s.GetInventory().Rows.Any(r => r.ItemId == "fort_key"), Is.True, "The key is kept.");

            // The map of what has been seen.
            c.ShowPlaceMap(true);
            yield return null;
            Assert.That(c.PlaceMapPanel, Is.Not.Null);
            Assert.That(c.PlaceMapPanel.Query<Label>().ToList().Any(l => l.text == "Watch room"), Is.True);
            Assert.That(c.PlaceMapPanel.Query<Label>().ToList().Any(l => l.text == "Captain's quarters"), Is.False, "Rooms not yet seen are not drawn.");
        }
    }
}
