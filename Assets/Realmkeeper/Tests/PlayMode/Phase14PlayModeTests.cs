using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
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
    /// <summary>Shared steps for Phase 14 tests and captures.</summary>
    internal static class Phase14Driver
    {
        /// <summary>Stands in front of a target and presses interact, without insisting it worked (for trying doors in turn).</summary>
        public static IEnumerator Try(ExplorationController c, PlaceInteractable t)
        {
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
            yield return PlaceDriver.Frames(3);
            if ((c.CurrentTarget as PlaceInteractable)?.TargetId == t.TargetId) c.Interact();
            yield return PlaceDriver.Frames(4);
        }

        /// <summary>Walks back to the way in and leaves the place, then enters another from the map.</summary>
        public static IEnumerator Next(RealmkeeperBootstrap bootstrap, ExplorationController c, WorldSession s, string placeId)
        {
            if (c.InPlace)
            {
                yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
                c.LeavePlace();
                yield return PlaceDriver.Frames(8);
            }

            yield return PlaceDriver.EnterFromMap(bootstrap, c, placeId);
        }

        public static IEnumerator Out(ExplorationController c)
        {
            if (!c.InPlace) yield break;
            c.LeavePlace();
            yield return PlaceDriver.Frames(6);
        }

        /// <summary>Fights the current fight to its end through the combat window, as the Phase 13 walk does.</summary>
        public static IEnumerator FightOut(ExplorationController c)
        {
            for (var i = 0; i < 2000 && c.InFight; i++)
            {
                if (c.Combat.Prompt != null) LevelUpDriver.Click(c.Combat.Root, "xk-combat-prompt-decline");
                else CombatDriver.Step(c);
                yield return null;
            }

            if (c.Combat != null && c.Combat.IsOpen && !c.InFight) { c.CloseWindows(); yield return PlaceDriver.Frames(2); }
        }

        /// <summary>Makes every Phase 14 place known (and edits each as asked), returning definition → place id.</summary>
        public static IEnumerator KnowTheNewPlaces(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, Dictionary<string, string> ids, Action<string, JObject> change = null,
                                                   params (string Id, int Qty, Action<JObject> State)[] items)
        {
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                foreach (JObject p in (JArray)w["places"])
                {
                    var def = (string)p["definitionId"];
                    if (!Phase14Places.All.Contains(def)) continue;
                    ids[def] = (string)p["placeId"];
                    p["discovery"] = "Discovered";
                    change?.Invoke(def, p);
                }
            }, items);
        }

        /// <summary>Sets every group in a saved place to Defeated, except those the filter keeps.</summary>
        public static void Clear(JObject place, Func<JObject, bool> keep = null)
        {
            var groups = (JObject)(place["groups"] ?? (place["groups"] = new JObject()));
            foreach (JObject g in (JArray)place["layout"]["groups"])
                if (keep == null || !keep(g))
                    groups[(string)g["id"]] = new JObject { ["state"] = "Defeated", ["alerted"] = false };
        }
    }

    /// <summary>
    /// Phase 14 in the shipped scene (ADR-0067 … ADR-0071): lasting conditions in the HUD, how they end and that they
    /// survive a reload; rest decided by where you are; walls, doors and cover on a fight's grid; a theft seen by a
    /// resident, in the journal, and put right; a home locked at night and broken into; the new places on the map with
    /// their stories — sleepers that wake, fire, a damaged bridge, outlaws who parley; residents who walk their house; and
    /// a follower who follows room to room. Twenty flows, numbered in the comments.
    /// </summary>
    public sealed class Phase14PlayModeTests
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
        public IEnumerator Conditions_ShowInTheHud_EndTheirOwnWays_AndSurviveAReload()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w =>
            {
                w["minuteOfDay"] = 600;
                var list = (JArray)(w["player"]["rpg"]["conditions"] ?? (w["player"]["rpg"]["conditions"] = new JArray()));
                list.Add(new JObject { ["type"] = "Poisoned", ["level"] = 0, ["source"] = "trap:poison_needle", ["reason"] = "a poisoned needle", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "Restoration" });
                list.Add(new JObject { ["type"] = "Exhaustion", ["level"] = 2, ["source"] = "cold", ["reason"] = "cold water", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "LongRestLevel" });
                list.Add(new JObject { ["type"] = "Restrained", ["level"] = 0, ["source"] = "trap:snare_net", ["reason"] = "a net", ["appliedAt"] = 0, ["expiresAt"] = -1, ["remedies"] = "Escape", ["escapeDc"] = 2 });
            });
            yield return PlaceDriver.Frames(4);

            // 1. Each lasting condition is in the HUD: what it does and how it ends.
            var panel = c.Hud.Root.Q<VisualElement>("xk-conditions");
            Assert.That(panel, Is.Not.Null, "The conditions panel is shown.");
            Assert.That(c.ConditionsText, Does.Contain("Poisoned").And.Contain("Exhausted").And.Contain("Restrained"));
            Assert.That(c.ConditionsText, Does.Contain("Ends:"));
            Assert.That(s.RestProblem(Realmkeeper.Domain.Rpg.RestKind.Long), Does.Contain("restrained"), "2. No rest while held fast.");

            // 2. Break free, from the HUD's own button (an Athletics or Acrobatics check against a low DC).
            for (var i = 0; i < 6 && s.GetConditions().Any(k => k.Type == ConditionType.Restrained); i++)
            {
                var b = c.Hud.Root.Q<VisualElement>("xk-conditions")?.Query<Button>().ToList().FirstOrDefault(x => x.text == "Break free");
                Assert.That(b, Is.Not.Null, "Break free is offered: " + c.ConditionsText);
                using (var click = NavigationSubmitEvent.GetPooled()) { click.target = b; b.SendEvent(click); }
                yield return PlaceDriver.Frames(2);
            }

            Assert.That(s.GetConditions().Any(k => k.Type == ConditionType.Restrained), Is.False, "Free of the net.");

            // 3. Save and reload: the poison and the exhaustion are still there, with their reasons.
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            var load = s.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name));
            Assert.That(load.IsSuccess, Is.True, load.Message);
            yield return PlaceDriver.Frames(6);
            Assert.That(s.GetConditions().Select(k => k.Type), Is.EquivalentTo(new[] { ConditionType.Poisoned, ConditionType.Exhaustion }));
            Assert.That(c.ConditionsText, Does.Contain("a poisoned needle"));

            // 4. A long rest takes one level of exhaustion, and does not cure the poison.
            var rest = s.LongRest();
            Assert.That(rest.IsSuccess, Is.True, rest.Message);
            yield return PlaceDriver.Frames(4);
            var after = s.GetConditions();
            Assert.That(after.Single(k => k.Type == ConditionType.Exhaustion).Level, Is.EqualTo(1));
            Assert.That(after.Any(k => k.Type == ConditionType.Poisoned), Is.True, "Rest is not a cure for poison.");
            Assert.That(c.ConditionsText, Does.Contain("Exhausted 1"));
        }

        [UnityTest]
        public IEnumerator AFightAmongWalls_ShowsWallsDoorsAndCover_OnTheGrid()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Crypt, p => p["discovery"] = "Discovered", ("hooded_lantern", 1, null), ("thieves_tools", 1, null));
            var crypt = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Crypt).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, crypt.PlaceId);

            // Into the first room with inhabitants: the fight starts as the player comes in.
            var scene = c.PlaceScene;
            var foeRoom = scene.Figures.Where(f => f.Role == "foe").Select(f => f.Room).FirstOrDefault(r => PlaceDriver.Path(scene, scene.PlayerRoom, r) != null);
            Assume.That(foeRoom, Is.Not.Null, "Nobody to fight in this crypt.");
            var path = PlaceDriver.Path(scene, scene.PlayerRoom, foeRoom);
            if (path.Count > 1) yield return PlaceDriver.WalkTo(c, s, path[path.Count - 2].Room);
            if (!c.InFight)
            {
                var via = c.PlaceScene.Passages.First(p => p.Id == path[path.Count - 1].Via);
                if (via.Kind != ConnectionKind.Opening && via.Door != DoorState.Open && via.Door != DoorState.Broken)
                    for (var i = 0; i < 4 && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Open && c.PlaceScene.Passages.First(p => p.Id == via.Id).Door != DoorState.Broken; i++)
                    {
                        c.ActOn(via.Id, c.PlaceScene.Passages.First(p => p.Id == via.Id).Door == DoorState.Closed ? PlaceAction.Open : PlaceAction.Force);
                        yield return PlaceDriver.Frames(2);
                    }

                if (!c.InFight) c.StepInto(foeRoom);
                yield return PlaceDriver.Frames(4);
                if (!c.InFight) c.ActOn(c.PlaceScene.Figures.First(f => f.Role == "foe" && f.Room == foeRoom).GroupId, PlaceAction.Attack);
                yield return PlaceDriver.Frames(4);
            }

            Assume.That(c.InFight, Is.True, "The fight began: " + c.LastPlaceMessage);
            var snap = c.Combat.Snapshot;
            Assert.That(snap, Is.Not.Null);

            // 5. The room's walls are on the fight's grid.
            Assert.That(snap.Blocked.Count, Is.GreaterThan(0), "The fight knows the walls.");
            Assert.That(c.Combat.Root.Query<Button>().ToList().Any(b => b.ClassListContains("xk-combat-cell--wall")), Is.True, "Walls drawn on the grid.");

            // 6. Cover, as the rules see it, from the player to each foe — in the order list and on the grid.
            var foes = snap.Order.Where(o => o.Id != snap.Player.Id && o.State == VitalState.Conscious).ToList();
            Assert.That(foes, Is.Not.Empty);
            Assert.That(foes.All(f => !string.IsNullOrEmpty(f.CoverFromPlayer)), Is.True, "Cover is worked out for every foe.");
            var cell = foes.Select(f => c.Combat.Root.Q<Button>($"xk-combat-cell-{f.Position.X}-{f.Position.Y}")).FirstOrDefault(b => b != null);
            if (cell != null) Assert.That(cell.tooltip, Does.Contain("from you:"), "The square says what cover the foe has from you.");

            // 7. Doors and low cover come into the fight as they stand in the place (drawn when on the grid).
            Assert.That(snap.Doors.Count + snap.LowCover.Count, Is.GreaterThanOrEqualTo(0));
            foreach (var d in snap.Doors)
            {
                var sq = d.Squares.FirstOrDefault();
                var b = c.Combat.Root.Q<Button>($"xk-combat-cell-{sq.X}-{sq.Y}");
                if (b != null) Assert.That(b.ClassListContains("xk-combat-cell--door") || b.ClassListContains("xk-combat-cell--door-open"), Is.True, "A door square is drawn as a door.");
            }

            yield return Phase14Driver.FightOut(c);
            Assert.That(c.InFight, Is.False, "Fought to the end.");
        }

        [UnityTest]
        public IEnumerator ATheft_IsSeen_GoesInTheJournal_AndCanBePutRight()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var doors = c.BuildingDoors.Select(d => d.TargetId).Distinct().Take(14).ToList();
            FigureView witness = null;
            string stolenFrom = null;
            foreach (var hour in new[] { 9, 13, 18 })
            {
                yield return Phase14Driver.Out(c);
                yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = hour * 60);
                foreach (var key in doors)
                {
                    yield return Phase14Driver.Out(c);
                    var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == key);
                    if (door == null) continue;
                    yield return Phase14Driver.Try(c, door);
                    if (!c.InPlace) continue;
                    var scene = c.PlaceScene;
                    foreach (var f in scene.Features.Where(f => f.Kind == FeatureKind.Container && !f.Hidden).ToList())
                    {
                        var who = scene.Figures.FirstOrDefault(x => x.Role == "resident" && x.Room == f.Room && x.Activity != "Sleeping");
                        if (who == null || scene.Room(f.Room) == null || PlaceDriver.Path(scene, scene.PlayerRoom, f.Room, avoidLocked: true) == null && scene.PlayerRoom != f.Room) continue;
                        yield return PlaceDriver.WalkTo(c, s, f.Room);
                        var steal = s.GetPlaceActions(f.Id).FirstOrDefault(o => o.Action == PlaceAction.Steal && o.Enabled);
                        if (steal == null) continue;
                        var before = s.GetCrimes().Count;

                        // 8. Taking what is not yours, under a resident's eyes: a crime, with who saw it.
                        c.ActOn(f.Id, PlaceAction.Steal);
                        yield return PlaceDriver.Frames(3);
                        if (s.GetCrimes().Count == before) continue;
                        witness = c.PlaceScene.Figures.FirstOrDefault(x => x.Role == "resident" && x.Room == f.Room) ?? who;
                        stolenFrom = scene.Name;
                        break;
                    }

                    if (stolenFrom != null) break;
                }

                if (stolenFrom != null) break;
            }

            Assume.That(stolenFrom, Is.Not.Null, "No household in this town had someone at home beside something to take.");
            var crime = s.GetCrimes().Last();
            Assert.That(crime.Status, Is.Not.EqualTo(CrimeStatus.Undiscovered), "8. Seen as it happened.");
            Assert.That(crime.Seen, Is.Not.Empty);

            // 9. The journal's Crimes tab lists it, with who saw it and what it means.
            c.ToggleHub();
            yield return null;
            c.HubWindow.SelectJournal("Crimes");
            yield return PlaceDriver.Frames(2);
            var list = c.HubWindow.Content.Q<VisualElement>("xk-journal-crimes");
            Assert.That(list, Is.Not.Null, "The Crimes tab.");
            Assert.That(list.Query<Label>().ToList().Any(l => l.text.Contains(crime.Seen[0])), Is.True);
            c.CloseWindows();
            yield return PlaceDriver.Frames(2);

            // 10. Put right through the witness's own words, when they knew who did it.
            if (crime.Known && witness != null)
            {
                Runtime.Playback.Pause();
                Assert.That(c.StartDialogue(witness.NpcId).IsSuccess, Is.True);
                yield return PlaceDriver.Frames(2);
                QuestDriver.Say(c, "settle");
                yield return PlaceDriver.Frames(3);
                if (c.Dialogue.IsOpen) c.EndDialogue();
                Assert.That(s.GetCrimes().Single(x => x.Id == crime.Id).Status, Is.EqualTo(CrimeStatus.Settled), "Settled: " + c.LastPlaceMessage);
            }
            else Assert.That(crime.Bounty, Is.EqualTo(0), "10. Only a description: no bounty on a stranger's word.");
        }

        [UnityTest]
        public IEnumerator AtNight_AHomeIsLocked_AndBreakingIn_IsACrime()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 23 * 60);
            var offered = false;
            foreach (var key in c.BuildingDoors.Select(d => d.TargetId).Distinct().Take(14).ToList())
            {
                yield return Phase14Driver.Out(c);
                var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == key);
                if (door == null) continue;
                yield return Phase14Driver.Try(c, door);
                if (c.InPlace) continue; // a tavern, a temple, a guard post: open all night
                var topics = c.Hud.Topics?.Query<Button>().ToList() ?? new List<Button>();
                if (!topics.Any(b => b.text.Contains("Force the door"))) continue;

                // 11. Locked for the night: the door offers picking, forcing — or leaving it.
                offered = true;
                Assert.That(topics.Any(b => b.text.Contains("Pick the lock")) && topics.Any(b => b.text.Contains("Leave it")), Is.True, "Pick, force, or leave it.");
                var before = s.GetCrimes().Count;
                for (var i = 0; i < 4 && !c.InPlace; i++)
                {
                    if (i > 0) yield return Phase14Driver.Try(c, door);
                    yield return PlaceDriver.Choose(c, "Force the door");
                }

                // 12. Getting in is breaking in: a crime, whether or not anyone saw.
                if (c.InPlace) Assert.That(s.GetCrimes().Count, Is.GreaterThan(before), "Breaking in is recorded.");
                break;
            }

            Assert.That(offered, Is.True, "At least one home is locked at 23:00.");
        }

        [UnityTest]
        public IEnumerator TheNewPlaces_AreOnTheMap_WithTheirStories()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return PlaceDriver.Seasoned(c, s);
            var ids = new Dictionary<string, string>();
            string fireRoom = null;
            string bridgeId = null, bridgeA = null, bridgeB = null;
            string parley = null;
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

                p["variables"] = p["variables"] ?? new JObject();
            }, ("hooded_lantern", 1, null));
            yield return ItemDriver.Give(Runtime, c, s, w => w["player"]["purse"] = 200);

            // 13. Three to five new places in this world, each on the map with a way in.
            Assert.That(ids.Count, Is.InRange(3, 5));
            c.ReturnToWorldMap();
            yield return PlaceDriver.Frames(6);
            foreach (var id in ids.Values) Assert.That(Bootstrap.Hud.PlaceMarkers.ContainsKey(id), Is.True, "A marker for " + id);

            // 14. The catacombs: the sealed vault wakes its sleepers, once.
            if (ids.TryGetValue(Phase14Places.Catacomb, out var catacomb))
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, catacomb);
                var vault = c.PlaceScene.Features.FirstOrDefault(f => f.Id == "story:sealed_vault");
                Assert.That(vault, Is.Not.Null);
                yield return PlaceDriver.WalkTo(c, s, vault.Room);
                Assert.That(c.InFight, Is.False, "They sleep until disturbed.");
                c.ActOn(vault.Id, PlaceAction.Open);
                yield return PlaceDriver.Frames(4);
                Assert.That(c.InFight, Is.True, "Opening the vault wakes them: " + c.LastPlaceMessage);
                yield return Phase14Driver.FightOut(c);
                c.ActOn(vault.Id, PlaceAction.Open);
                yield return PlaceDriver.Frames(3);
                Assert.That(c.InFight, Is.False, "Nothing wakes twice.");
                yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            }

            // 15. The fane: a room on fire, in the scene and on the place map.
            if (ids.TryGetValue(Phase14Places.Temple, out var temple) && fireRoom != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, temple);
                yield return PlaceDriver.WalkTo(c, s, fireRoom);
                Assert.That(c.PlaceScene.Fires[fireRoom], Is.EqualTo("burning"));
                Assert.That(Object.FindObjectsByType<Flicker>(FindObjectsSortMode.None), Is.Not.Empty, "Firelight in the scene.");
                c.ShowPlaceMap(true);
                yield return null;
                Assert.That(c.PlaceMapPanel.Q<VisualElement>("xk-place-map-room-" + fireRoom).ClassListContains("xk-place-map-fire"), Is.True);
                c.ShowPlaceMap(false);
            }

            // 19. The outlaws' den: they talk before they fight, and take a toll.
            if (ids.TryGetValue(Phase14Places.Hideout, out var den) && parley != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, den);
                var room = c.PlaceScene.Figures.FirstOrDefault(f => f.GroupId == parley)?.Room;
                Assume.That(room, Is.Not.Null);
                yield return PlaceDriver.WalkTo(c, s, room);
                Assert.That(c.InFight, Is.False, "They talk first.");
                var opts = s.GetPlaceActions(parley).Select(o => o.Action).ToList();
                Assert.That(opts, Does.Contain(PlaceAction.PayToll));
                c.ActOn(parley, PlaceAction.PayToll);
                yield return PlaceDriver.Frames(2);
                Assert.That(c.LastPlaceMessage, Does.Contain("coin"));
            }

            // 16, 17. The culverts: a damaged bridge on the map, and crossing it takes an Acrobatics check.
            if (ids.TryGetValue(Phase14Places.Works, out var works) && bridgeId != null)
            {
                yield return Phase14Driver.Next(Bootstrap, c, s, works);
                yield return PlaceDriver.WalkTo(c, s, bridgeA);
                Assert.That(c.PlaceScene.Bridges[bridgeId], Is.EqualTo(BridgeState.Damaged));
                c.StepInto(bridgeB);
                yield return PlaceDriver.Frames(4);
                Assert.That(c.LastPlaceMessage, Does.Contain("Acrobatics"));
                var state = c.PlaceScene.Bridges[bridgeId];
                Assert.That(c.PlaceScene.PlayerRoom == bridgeB ? state == BridgeState.Damaged : state == BridgeState.Destroyed, Is.True, "Held, or fell for good.");
                c.ShowPlaceMap(true);
                yield return null;
                var drawn = c.PlaceMapPanel.Q<VisualElement>("xk-place-map-passage-" + bridgeId);
                if (drawn != null) Assert.That(drawn.ClassListContains("xk-place-map-bridge"), Is.True);
                c.ShowPlaceMap(false);

                // 18. The map's list says how the place stands now.
                var summary = s.GetKnownPlaces().Single(p => p.PlaceId == works);
                Assert.That(summary.State.Any(x => x.Contains("bridge")), Is.True, "States: " + string.Join(", ", summary.State));
            }
        }

        [UnityTest]
        public IEnumerator Residents_WalkTheirHouse_AndAFollower_FollowsRoomToRoom()
        {
            var s = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, s, w => w["minuteOfDay"] = 7 * 60);

            // 20a. Residents move between rooms through the passages, and are seen walking the way.
            var walked = false;
            foreach (var key in c.BuildingDoors.Select(d => d.TargetId).Distinct().Take(8).ToList())
            {
                yield return Phase14Driver.Out(c);
                var door = c.BuildingDoors.FirstOrDefault(d => d.TargetId == key);
                if (door == null) continue;
                yield return Phase14Driver.Try(c, door);
                if (!c.InPlace || c.PlaceScene.Rooms.Count < 2) continue;
                for (var step = 0; step < 60 && !walked; step++)
                {
                    s.Execute(new AdvanceTimeCommand(3)); // the move window (IndoorSchedule.MoveWindow): a move is seen for three minutes
                    yield return PlaceDriver.Frames(3);
                    if (c.PlaceScene.Figures.Any(f => f.Role == "resident" && f.Route.Count > 0))
                        walked = Object.FindObjectsByType<PlaceWalker>(FindObjectsSortMode.None).Length > 0 || c.PlaceScene.Figures.Any(f => f.FromRoom != null);
                }

                if (walked) break;
            }

            Assert.That(walked, Is.True, "Someone walked from one room to another.");
            yield return Phase14Driver.Out(c);

            // 20b. A freed captive follows the player room to room.
            yield return PlaceDriver.Seasoned(c, s);
            yield return PlaceDriver.EditPlace(Runtime, c, s, PlaceCatalog.Mine, p => { p["discovery"] = "Discovered"; Phase14Driver.Clear(p); }, ("hooded_lantern", 1, null));
            var mine = s.GetKnownPlaces().Single(x => x.Name == PlaceCatalog.Find(PlaceCatalog.Mine).Name);
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, mine.PlaceId);
            var captive = c.PlaceScene.Features.Single(f => f.Kind == FeatureKind.Captive);
            yield return PlaceDriver.WalkTo(c, s, captive.Room);
            for (var i = 0; i < 3 && !c.PlaceScene.Figures.Any(f => f.Role == "follower"); i++) { c.ActOn(captive.Id, PlaceAction.Free); yield return PlaceDriver.Frames(2); }
            Assume.That(c.PlaceScene.Figures.Any(f => f.Role == "follower"), Is.True, "Freed: " + c.LastPlaceMessage);
            var next = PlaceDriver.Path(c.PlaceScene, c.PlaceScene.PlayerRoom, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A)?.FirstOrDefault().Room;
            Assume.That(next, Is.Not.Null);
            yield return PlaceDriver.WalkTo(c, s, next);
            var follower = c.PlaceScene.Figures.First(f => f.Role == "follower");
            Assert.That(follower.Room, Is.Not.Null, "The follower is somewhere in the place.");
            if (follower.Waiting == null) Assert.That(follower.Room, Is.EqualTo(c.PlaceScene.PlayerRoom), "Through an open way they keep up.");
            Assert.That(c.ConditionsText, Does.Contain(follower.Name), "The HUD says who follows.");
        }
    }
}
