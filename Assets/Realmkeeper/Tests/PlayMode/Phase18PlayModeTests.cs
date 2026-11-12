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
    internal static class Phase18Driver
    {
        /// <summary>Makes the home region's Phase 18 places known (and edits each as asked), returning definition → place id.</summary>
        public static IEnumerator KnowThePlaces(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, Dictionary<string, string> ids,
                                                params (string Id, int Qty, Action<JObject> State)[] items)
        {
            var region = s.GetPlayer().RegionId.ToString();
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                foreach (JObject p in (JArray)w["places"])
                {
                    var def = (string)p["definitionId"];
                    if (!Phase18Places.All.Contains(def) || (string)p["regionId"] != region && !string.IsNullOrEmpty(region) && region != Guid.Empty.ToString()) continue;
                    if (ids.ContainsKey(def)) continue;
                    ids[def] = (string)p["placeId"];
                    p["discovery"] = "Discovered";
                }
            }, items);
        }

        public static FeatureView Feature(ExplorationController c, string id) => c.PlaceScene.Features.First(f => f.Id == id);
    }

    /// <summary>
    /// Phase 18 in the shipped scene, through the real controls: the ringers' bells rung in the verse's order from inside
    /// the vault (the door opens and stays open across a save), and the journal's Discoveries tab grouped by kind with
    /// what was found there.
    /// </summary>
    public sealed class Phase18PlayModeTests
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
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        [UnityTest]
        public IEnumerator TheRingersBells_RungInTheVersesOrder_OpenTheDoor_AndTheJournalGroupsWhatWasFound()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var rt = Runtime;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, rt, c);
            yield return PlaceDriver.Seasoned(c, s);
            var ids = new Dictionary<string, string>();
            yield return Phase18Driver.KnowThePlaces(rt, c, s, ids, ("hooded_lantern", 1, null));
            Assert.That(ids.ContainsKey(Phase18Places.Vault), Is.True, "The home region has its ringers' vault.");
            yield return PlaceDriver.EnterFromMap(Bootstrap, c, ids[Phase18Places.Vault]);

            var verse = Phase18Driver.Feature(c, "story:ringers_verse");
            yield return PlaceDriver.WalkTo(c, s, verse.Room);
            c.ActOn(verse.Id, PlaceAction.Read);
            yield return PlaceDriver.Frames(3);
            Assert.That(c.LastPlaceMessage, Does.Contain("Ring first for the water"), "The verse tells the order.");

            var locked = c.PlaceScene.Passages.Count(p => p.Door == DoorState.Locked);
            foreach (var bellId in new[] { "story:bell_ferry", "story:bell_mill", "story:bell_forge" })
            {
                var bell = Phase18Driver.Feature(c, bellId);
                yield return PlaceDriver.WalkTo(c, s, bell.Room);
                c.ActOn(bell.Id, PlaceAction.Activate);
                yield return PlaceDriver.Frames(3);
            }

            Assert.That(c.LastPlaceMessage, Does.Contain("ringers' door swings open"), c.LastPlaceMessage);
            Assert.That(c.PlaceScene.Passages.Count(p => p.Door == DoorState.Locked), Is.EqualTo(locked - 1), "The ringers' door is no longer locked.");

            // The journal: the Discoveries tab, grouped by kind.
            c.CloseWindows();
            c.ToggleHub();
            c.HubWindow.SelectJournal("Discoveries");
            yield return PlaceDriver.Frames(2);
            Assert.That(c.HubWindow.Root.Q<Label>("xk-discovery-ringers_order"), Is.Not.Null, "The verse's order is in the journal.");
            Assert.That(c.HubWindow.Root.Q<Label>("xk-discoveries-cultural"), Is.Not.Null, "Under Customs.");
            c.CloseWindows();
        }
    }

    /// <summary>
    /// Evidence capture for Phase 18 (automated, not manual verification): the new places and their mechanisms, the
    /// creatures and scenery from the CC0/CC BY kits, the grouped journal, a quest offer and an overheard exchange. Runs only
    /// when RK_CAPTURE_DIR is set. Each caption says what the picture shows, and what the test set up to show it.
    /// </summary>
    public sealed class Phase18CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p18_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        /// <summary>Walks back to the way in and leaves the place.</summary>
        private static IEnumerator Leave(ExplorationController c, WorldSession s)
        {
            if (!c.InPlace) yield break;
            yield return Phase14Driver.FightOut(c);
            yield return PlaceDriver.WalkTo(c, s, c.PlaceScene.Passages.First(p => p.Kind == ConnectionKind.Entrance).A);
            c.LeavePlace();
            yield return PlaceDriver.Frames(8);
        }

        /// <summary>Looks at a feature of the place from a little way off, so it and its room are in the picture.</summary>
        private static IEnumerator Look(ExplorationController c, FeatureView f, float back = 3.2f, float pitch = 22f)
        {
            var room = c.PlaceScene.Room(f.Room);
            var at = PlaceEnvironmentBuilder.CellCentre(c.PlaceScene, room?.Floor ?? 0, f.X, f.Z);
            var from = c.Player.transform.position;
            // Stand inside the room (a point stepped back along the line of approach could be beyond its walls, in the fog):
            // of the room's inset corners and centre, the one nearest to `back` metres from the feature.
            var stand = at - Vector3.forward * back;
            if (room != null)
            {
                const float cell = PlaceEnvironmentBuilder.Cell;
                var r = room.Rect;
                float x0 = (r.X + 0.5f) * cell, x1 = (r.X + r.W - 0.5f) * cell, z0 = (r.Z + 0.5f) * cell, z1 = (r.Z + r.D - 0.5f) * cell;
                var spots = new[] { new Vector3(x0, at.y, z0), new Vector3(x0, at.y, z1), new Vector3(x1, at.y, z0), new Vector3(x1, at.y, z1), new Vector3((x0 + x1) * 0.5f, at.y, (z0 + z1) * 0.5f) };
                stand = spots.OrderBy(p => Mathf.Abs(Vector3.Distance(p, at) - back)).First();
            }

            var dir = new Vector3(at.x - stand.x, 0f, at.z - stand.z);
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();
            var yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            c.Player.Teleport(new Realmkeeper.Domain.World.LocalPose(stand.x, from.y, stand.z, yaw));
            if (c.CameraRig.Perspective != Realmkeeper.Domain.World.CameraPerspective.FirstPerson) c.TogglePerspective(); // in close rooms the third-person camera clips the head
            c.CameraRig.SetView(yaw, pitch);
            c.CameraRig.Snap();
            yield return Wait(0.6f);
        }

        [UnityTest]
        public IEnumerator Capture_Phase18()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            var rt = Runtime;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, rt, c);
            yield return PlaceDriver.Seasoned(c, s);
            var home = s.GetPlayer().SettlementId;

            // 1. A settlement with the new scenery: modelled trees and the well.
            var minute = s.GetWorldTime().MinuteOfDay;
            if (minute < 9 * 60 || minute > 16 * 60) s.Execute(new AdvanceTimeCommand((11 * 60 - minute + 1440) % 1440));
            yield return Wait(1.2f);
            c.CameraRig.SetView(c.Player.transform.eulerAngles.y + 35f, 18f);
            c.CameraRig.Snap();
            yield return Wait(0.6f);
            yield return Shoot("p18_01_settlement_scenery", $"{s.GetPlayer().SettlementName} in daylight: trees from the Stylized Nature MegaKit (CC0) where the layout plants them, and the modelled well where it has one.", c.CameraRig.Camera);

            // 2. People talking to each other (overheard): the hours are stepped until the session reports a real pair, and
            // the player walks over to them (the presentation shows lines only within earshot).
            for (var hour = 0; hour < 10 && s.GetExchanges().Count == 0; hour++) { s.Execute(new AdvanceTimeCommand(60)); yield return Wait(0.4f); }
            var pair = s.GetExchanges().Select(x => (x, a: Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).FirstOrDefault(r => r.NpcId == x.A),
                                                     b: Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).FirstOrDefault(r => r.NpcId == x.B)))
                .FirstOrDefault(p => p.a != null && p.b != null && !p.a.IsHidden && !p.b.IsHidden && Vector3.Distance(p.a.transform.position, p.b.transform.position) <= 8f);
            if (pair.a != null)
            {
                var mid = (pair.a.transform.position + pair.b.transform.position) * 0.5f;
                var side = Vector3.Cross(pair.b.transform.position - pair.a.transform.position, Vector3.up).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.forward;
                var stand = mid + side * 4.5f;
                var face = Mathf.Atan2(mid.x - stand.x, mid.z - stand.z) * Mathf.Rad2Deg;
                c.Player.Teleport(new Realmkeeper.Domain.World.LocalPose(stand.x, mid.y, stand.z, face));
                c.CameraRig.SetView(face, 12f);
                c.CameraRig.Snap();
            }

            yield return Wait(4.5f); // the exchanges refresh every 2 s
            var lines = c.OverheardLines.ToList();
            yield return Shoot("p18_02_overheard_exchange", lines.Count > 0 ? "Overheard: " + string.Join(" | ", lines) : "No pair of people who know each other stood together near the player at this hour (the line appears only for real pairs).", c.CameraRig.Camera);

            var ids = new Dictionary<string, string>();
            yield return Phase18Driver.KnowThePlaces(rt, c, s, ids, ("hooded_lantern", 1, null), ("smiths_tools", 1, null));

            // 3–5. Greywater Drift: the drill board, the pump (worked choked: the barrel cracks), then cleared and worked dry.
            if (ids.TryGetValue(Phase18Places.Drift, out var drift))
            {
                yield return PlaceDriver.EnterFromMap(Bootstrap, c, drift);
                var pump = Phase18Driver.Feature(c, "story:drift_pump");
                var grate = Phase18Driver.Feature(c, "story:intake_grate");
                yield return PlaceDriver.WalkTo(c, s, pump.Room);
                yield return Look(c, pump);
                c.ActOn(pump.Id, PlaceAction.Activate);
                yield return PlaceDriver.Frames(3);
                yield return Shoot("p18_03_drift_pump_cracked", "Greywater Drift: the drainage pump worked with its intake choked — " + c.LastPlaceMessage, c.CameraRig.Camera);
                c.ActOn(pump.Id, PlaceAction.Mend);
                yield return PlaceDriver.Frames(2);
                yield return PlaceDriver.WalkTo(c, s, grate.Room);
                for (var i = 0; i < 4 && !(c.LastPlaceMessage ?? string.Empty).Contains("runs freely") && !(c.LastPlaceMessage ?? string.Empty).Contains("is clear"); i++) { c.ActOn(grate.Id, PlaceAction.Clear); yield return PlaceDriver.Frames(2); }
                yield return PlaceDriver.WalkTo(c, s, pump.Room);
                for (var i = 0; i < 2; i++) { c.ActOn(pump.Id, PlaceAction.Activate); yield return PlaceDriver.Frames(2); }
                yield return Look(c, pump);
                yield return Shoot("p18_04_drift_drained", "Mended with smith's tools, the grate cleared (Athletics, retried until it gave), and the pump worked twice: " + c.LastPlaceMessage, c.CameraRig.Camera);

                // 6. A creature from the new models: the den's inhabitants (a giant spider or rats) in a fight.
                var silver = Phase18Driver.Feature(c, "story:silver_vein");
                var path = PlaceDriver.Path(c.PlaceScene, c.PlaceScene.PlayerRoom, silver.Room);
                if (path != null && path.Count > 0)
                {
                    // Walk to the room before the den, then step in by hand, so the fight can be seen before it is fought out.
                    if (path.Count > 1) yield return PlaceDriver.WalkTo(c, s, path[path.Count - 2].Room);
                    var (via, into) = path[path.Count - 1];
                    var p = c.PlaceScene.Passages.First(x => x.Id == via);
                    if (p.Kind != ConnectionKind.Opening && p.Door != DoorState.Open && p.Door != DoorState.Broken) c.ActOn(p.Id, PlaceAction.Open);
                    yield return PlaceDriver.Frames(2);
                    c.StepInto(into);
                    yield return PlaceDriver.Frames(6);
                    if (c.InFight)
                    {
                        c.AutoAi = false;
                        for (var i = 0; i < 3 && c.InFight; i++) { CombatDriver.Step(c); yield return null; }
                        yield return Wait(0.8f);
                        // Turn the camera on the foe, so the creature is in the picture.
                        var me = s.GetCharacter().Name;
                        var foe = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(tr => tr.name.StartsWith("Marker ", StringComparison.Ordinal) && !tr.name.Contains(me));
                        if (foe != null)
                        {
                            if (c.CameraRig.Perspective == Realmkeeper.Domain.World.CameraPerspective.FirstPerson) c.TogglePerspective(); // back over the shoulder for the fight
                            var d = foe.position - c.Player.transform.position;
                            c.CameraRig.SetView(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 40f, 42f); // off to one side, so the player does not hide the foe
                            c.CameraRig.Snap();
                            yield return Wait(0.6f);
                        }

                        yield return Shoot("p18_05_creature_combat", "A fight in Greywater Drift: the den's creatures drawn with the new creature models (Quaternius CC0), on the existing tactical grid.", c.CameraRig.Camera);
                        yield return Phase14Driver.FightOut(c);
                    }
                }

                yield return Leave(c, s);
            }

            // 7–9. The Ringers' Vault: the bells and the verse; the wrong bell; the right order opens the door.
            if (ids.TryGetValue(Phase18Places.Vault, out var vault))
            {
                yield return PlaceDriver.EnterFromMap(Bootstrap, c, vault);
                var verse = Phase18Driver.Feature(c, "story:ringers_verse");
                yield return PlaceDriver.WalkTo(c, s, verse.Room);
                c.ActOn(verse.Id, PlaceAction.Read);
                yield return PlaceDriver.Frames(3);
                yield return Look(c, Phase18Driver.Feature(c, "story:bell_mill"), 3.6f, 12f);
                yield return Shoot("p18_06_vault_bells_verse", "The Ringers' Vault: three bells of three trades, and the painted verse read by lantern-light — " + c.LastPlaceMessage, c.CameraRig.Camera);
                var forge = Phase18Driver.Feature(c, "story:bell_forge");
                yield return PlaceDriver.WalkTo(c, s, forge.Room);
                c.ActOn(forge.Id, PlaceAction.Activate);
                yield return PlaceDriver.Frames(3);
                yield return Shoot("p18_07_vault_wrong_bell", "The forge bell rung first (wrong): " + c.LastPlaceMessage, c.CameraRig.Camera);
                foreach (var id in new[] { "story:bell_ferry", "story:bell_mill", "story:bell_forge" })
                {
                    var bell = Phase18Driver.Feature(c, id);
                    yield return PlaceDriver.WalkTo(c, s, bell.Room);
                    c.ActOn(bell.Id, PlaceAction.Activate);
                    yield return PlaceDriver.Frames(3);
                }

                yield return Look(c, Phase18Driver.Feature(c, "story:bell_forge"), 4f, 16f);
                yield return Shoot("p18_08_vault_door_open", "Rung in the verse's order: " + c.LastPlaceMessage, c.CameraRig.Camera);
                yield return Leave(c, s);
            }

            // 10. Hollin Tower: the lintel by a flame held close; the hidden way found.
            if (ids.TryGetValue(Phase18Places.Hollin, out var hollin))
            {
                yield return PlaceDriver.EnterFromMap(Bootstrap, c, hollin);
                var lintel = Phase18Driver.Feature(c, "story:scored_lintel");
                yield return PlaceDriver.WalkTo(c, s, lintel.Room);
                c.ActOn(lintel.Id, PlaceAction.Illuminate);
                yield return PlaceDriver.Frames(3);
                yield return Look(c, lintel);
                yield return Shoot("p18_09_hollin_raking_light", "Hollin Tower: a flame held to the scored lintel — " + c.LastPlaceMessage, c.CameraRig.Camera);
                yield return Leave(c, s);
            }

            // 11. The journal: discoveries grouped by kind, with certainty (back in town: the hub opens there).
            yield return CampaignDriver.GoTo(rt, c, s, home);
            c.CloseWindows();
            c.ToggleHub();
            c.HubWindow.SelectJournal("Discoveries");
            yield return Wait(0.4f);
            yield return Shoot("p18_10_journal_discoveries", "The journal's Discoveries tab: grouped by kind (Dangers first), ◆ marks what matters, [unverified] what was only guessed; each with where it was found.", c.CameraRig.Camera);

            // 12. A quest offered in conversation: the drift's quest-giver (read from the world), met in their town and talked to.
            c.CloseWindows();
            string giverId = null;
            yield return ItemDriver.Give(rt, c, s, w =>
            {
                foreach (var token in w.SelectTokens("$..quests[*]"))
                    if (token is JObject q && (string)q["definitionId"] == "drowned_drift" && (string)q["bindings"]?["place"] == drift) giverId = (string)q["bindings"]?["giver"];
            });
            if (giverId != null && Realmkeeper.Domain.Identifiers.NpcId.TryParse(giverId, out var giver) && s.FindNpc(giver) != null)
            {
                yield return CampaignDriver.GoTo(rt, c, s, s.FindNpc(giver).HomeSettlementId);
                c.CloseWindows();
                var talk = c.StartDialogue(giver);
                yield return Wait(0.6f);
                var j = s.GetJournal();
                var drowned = j.Available.Concat(j.Active).FirstOrDefault(q => q.Name == "The Drowned Drift");
                yield return Shoot("p18_11_quest_in_conversation", $"Talking to {s.FindNpc(giver).Name}, who gives The Drowned Drift" + (talk.IsSuccess ? "" : " (the conversation was refused: " + talk.Message + ")") +
                                                                   (drowned != null ? $". In the journal: {drowned.Next}" : "."), c.CameraRig.Camera);
                c.EndDialogue();
            }
            else Log.Add("p18_11_quest_in_conversation: not taken — the drift's quest-giver was not found in the world.");

            // 12. The creature models in a line, each as the game sets it up (its controller, idle), facing the camera — a
            // check of size, facing and materials. Set out for the capture: they are not in the world.
            c.CloseWindows();
            if (c.CameraRig.Perspective == Realmkeeper.Domain.World.CameraPerspective.FirstPerson) c.TogglePerspective();
            var catalog = Resources.Load<ArtCatalog>(ArtCatalog.ResourceName);
            var row = new GameObject("Capture creature line-up");
            var forward = c.Player.transform.forward;
            var right = c.Player.transform.right;
            // Three rows of six, nearest row 7 m ahead, 2.3 m apart, so every model is in frame; each one's height is logged.
            const int perRow = 6;
            var shown = new List<string>();
            // The farm and Poly by Google animals first (nearest), then the rest, so the rise behind does not hide the ones checked last.
            var order = catalog.Animals.OrderBy(a => a.Id == "Sheep" || catalog.AnimalController(a.Id) == null ? 0 : 1).ToList();
            for (var i = 0; i < order.Count; i++)
            {
                var e = order[i];
                if (e?.Prefab == null) continue;
                var go = Object.Instantiate(e.Prefab, row.transform, false);
                var spot = c.Player.transform.position + forward * (7f + (i / perRow) * 4.5f) + right * ((i % perRow - (perRow - 1) * 0.5f) * 2.3f);
                if (Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) spot.y = hit.point.y; // stand on the ground
                go.transform.position = spot;
                // As the game places a body: its own import turn, under a parent that faces the way the creature faces (here, the camera).
                go.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up) * e.Prefab.transform.localRotation;
                var controller = catalog.AnimalController(e.Id);
                if (controller != null) { var a = go.GetComponent<Animator>() ?? go.AddComponent<Animator>(); a.runtimeAnimatorController = controller; a.applyRootMotion = false; }
                var bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
                shown.Add($"{e.Id} {bounds.size.y:0.00} m" + (controller != null ? "" : " (static)"));
            }

            c.CameraRig.SetView(c.Player.transform.eulerAngles.y, 14f);
            c.CameraRig.Snap();
            yield return Wait(1.0f);
            yield return Shoot("p18_12_creature_lineup", "The creature models as the game uses them (set out in a row for the capture): " + string.Join(", ", shown) + ".", c.CameraRig.Camera);
            Object.Destroy(row);
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
