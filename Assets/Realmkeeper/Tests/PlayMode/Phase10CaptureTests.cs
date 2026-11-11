using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
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
    /// Evidence capture for Phase 10, not a pass/fail test: the planned town (arrival, square, a district street,
    /// water and a bridge, the keep on its hill, farms and herds, first person), a village and a hamlet, the strategic
    /// map drawn from the plans with the settlement's identity, a named person's character sheet, an outing with a
    /// creature's bestiary entry, and a reaction prompt. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase10CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p10_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        /// <summary>Stands the character at a plan point facing another (on the ground), with the camera behind.</summary>
        private static IEnumerator StandAt(PlanPoint at, PlanPoint toward, float pitch = 18f)
        {
            var c = Controller;
            var layout = c.Streamer.Current;
            var yaw = (float)(90.0 - Math.Atan2(toward.Z - at.Z, toward.X - at.X) * 180.0 / Math.PI);
            c.Player.Teleport(new LocalPose(at.X, LocalPaths.StandingHeight(layout, at.X, at.Z) + 0.1f, at.Z, yaw));
            c.CameraRig.SetView(yaw, pitch);
            c.CameraRig.Snap();
            for (var i = 0; i < 12; i++) yield return null;
        }

        private static PlanPoint Toward(PlanPoint from, PlanPoint to, float metres)
        {
            var d = from.DistanceTo(to);
            if (d < 0.01f) return from;
            return new PlanPoint(from.X + (to.X - from.X) / d * metres, from.Z + (to.Z - from.Z) / d * metres);
        }

        [UnityTest]
        public IEnumerator Capture1_ThePlannedTown()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var plan = c.Streamer.Current.Plan;
            Assert.That(plan, Is.Not.Null);
            var camera = c.CameraRig.Camera;
            Log.Add("Town: " + plan.Identity.Summary + $" — {plan.Buildings.Count} buildings, {plan.Roads.Count} roads, {plan.Bridges.Count} bridges, {plan.Fields.Count} fields.");

            for (var i = 0; i < 12; i++) yield return null;
            yield return Shoot(camera, "p10_01_town_arrival", "Arriving on the main road at the plan's way in, the town ahead: terrain-following road, the walls and gatehouse, roofs rising toward the keep's hill.");

            var hall = plan.FindBuilding(SettlementSites.Hall);
            var origin = new PlanPoint(0, 0);
            yield return StandAt(Toward(origin, hall.Center, -6f), hall.Center, 14f);
            yield return Shoot(camera, "p10_02_market_square", "The market square: the well and stalls on the levelled square, the tavern facing it from its quiet side, shops and the market streets leaving it.");

            var lane = plan.Roads.FirstOrDefault(r => r.Key.StartsWith("lane-lanes", StringComparison.Ordinal)) ?? plan.Roads.First(r => r.Kind == RoadKind.Lane);
            var mid = lane.Line[lane.Line.Count / 2];
            var ahead = lane.Line[Math.Min(lane.Line.Count - 1, lane.Line.Count / 2 + 3)];
            yield return StandAt(mid, ahead, 12f);
            yield return Shoot(camera, "p10_03_district_street", $"A district lane ({lane.Key}): houses on their plots facing the street, plinths where the ground falls, gardens behind.");

            if (plan.Bridges.Count > 0)
            {
                var br = plan.Bridges.OrderByDescending(b => b.From.DistanceTo(b.To)).First();
                var approach = Toward(br.From, br.To, -10f);
                yield return StandAt(approach, br.To, 16f);
                yield return Shoot(camera, "p10_04_bridge_and_water", $"A {(br.Stone ? "stone" : "timber")} bridge carrying {br.RoadKey} over the {br.WaterwayKey}: the water in its cut channel, the deck above it, piers to the bed.");
            }

            var keep = plan.FindBuilding(SettlementSites.Landmark);
            yield return StandAt(Toward(keep.Center, origin, 80f), keep.Center, 3f);
            yield return Shoot(camera, "p10_05_keep_on_its_hill", "The landmark on the high ground: the hill's flattened top under it, the lanes climbing to it.");

            var pasture = plan.Fields.FirstOrDefault(f => f.Kind == FieldKind.Pasture) ?? plan.Fields.First(f => f.Kind == FieldKind.Crop);
            var farmhouse = plan.Buildings.FirstOrDefault(b => b.Function == BuildingFunction.Farmhouse);
            yield return StandAt(Toward(pasture.Center, farmhouse?.Center ?? origin, 30f), pasture.Center, 22f);
            yield return Shoot(camera, "p10_06_farms_and_livestock", $"Farmland: a fenced {pasture.Kind.ToString().ToLowerInvariant()} ({pasture.Crop}) with the herd that grazes it, crop fields in rows, the farmstead and its track.");

            yield return StandAt(Toward(origin, hall.Center, -4f), hall.Center, 4f);
            Assert.That(c.TogglePerspective().IsSuccess, Is.True);
            for (var i = 0; i < 10; i++) yield return null;
            yield return Shoot(camera, "p10_07_first_person_square", "First person in the square, looking at the tavern: the same plan at eye height.");
            c.TogglePerspective();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Capture2_VillageHamletAndTheMap()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Bootstrap;
            var session = bootstrap.Session;
            var c = Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            foreach (var type in new[] { SettlementType.Village, SettlementType.Hamlet })
            {
                var s = session.GetSettlements().First(x => x.SettlementType == type);
                bootstrap.Selection.Select(s.SettlementId);
                Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
                for (var i = 0; i < 8; i++) yield return null;
                Runtime.Playback.Pause();
                var plan = c.Streamer.Current.Plan;
                Log.Add(s.Name + ": " + plan.Identity.Summary + $" — {plan.Buildings.Count} buildings.");
                var hall = plan.FindBuilding(SettlementSites.Hall);
                var landmark = plan.FindBuilding(SettlementSites.Landmark);
                yield return StandAt(Toward(new PlanPoint(0, 0), hall.Center, -10f), landmark.Center, 16f);
                yield return Shoot(c.CameraRig.Camera, type == SettlementType.Village ? "p10_08_village_green" : "p10_09_hamlet",
                    $"{s.Name}, {plan.Identity.Summary.ToLowerInvariant()} Looking from the green toward its {(type == SettlementType.Village ? "chapel" : "watchtower")}.");
                Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True);
                for (var i = 0; i < 6; i++) yield return null;
            }

            var town = session.GetSettlements().First(x => x.SettlementType == SettlementType.Town);
            var view = bootstrap.WorldView.FindSettlementView(town.SettlementId);
            bootstrap.Selection.Select(town.SettlementId);
            bootstrap.CameraController.FocusOn(view.Center);
            for (var i = 0; i < 12; i++) yield return null;
            var identity = bootstrap.Hud.Root.Q<Label>("insp-identity");
            identity?.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(identity);
            for (var i = 0; i < 3; i++) yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p10_10_strategic_map_and_identity",
                $"The strategic map's {town.Name}, drawn from the same plan ({view.BuildingCount} buildings, the wall on its circuit, the fields), and the inspector's account of the place: its identity, districts and landmarks.");

            var hud = bootstrap.Hud;
            var guard = session.GetNpcs(new NpcFilter()).First(n => session.GetNpcCharacter(n.NpcId)?.HasSheet == true);
            hud.ShowPeople(true);
            hud.SelectNpc(guard.NpcId);
            for (var i = 0; i < 4; i++) yield return null;
            var section = hud.NpcInspectorBody.Q<Label>("ninsp-section-character");
            if (section != null) (hud.NpcInspectorBody as ScrollView)?.ScrollTo(section);
            for (var i = 0; i < 3; i++) yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p10_11_npc_character_sheet", $"{guard.Name}'s character record in the inspector: archetype, class and level, hit points, armour class, abilities, saves, skills, equipment — the player's own rules.");
        }

        [UnityTest]
        public IEnumerator Capture3_AnOutingAndAReaction()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c, "wizard");
            session.Dispatch(new ChooseSpellCommand("class:spellbook", "shield"));
            session.Dispatch(new PrepareSpellsCommand(new[] { "shield", "magic_missile" }));
            c.AutoAi = false;
            var camera = c.CameraRig.Camera;

            var outing = c.StartOuting();
            for (var i = 0; i < 4; i++) yield return null;
            if (c.InFight)
            {
                var w = c.Combat;
                var creature = w.Snapshot.Order.First(x => x.Kind == CombatantKind.Creature);
                w.Inspect(creature.Id);
                for (var i = 0; i < 3; i++) yield return null;
                var inspector = w.Root.Q<ScrollView>("xk-combat-inspector");
                if (inspector != null) inspector.scrollOffset = new Vector2(0f, 10000f); // down to the bestiary line
                for (var i = 0; i < 3; i++) yield return null;
                Log.Add("Outing: " + outing.Message);
                yield return Shoot(camera, "p10_12_outing_and_bestiary", $"An outing from the HUD: {outing.Message} The creature's bestiary entry in the inspector (category, challenge, behaviour, traits, source).");
                yield return LivingDriver.UntilPrompt(c, 60);
                if (w.Prompt != null)
                {
                    for (var i = 0; i < 3; i++) yield return null;
                    yield return Shoot(camera, "p10_13_reaction_prompt", "A reaction asked of the player: what triggered it, the cost, what it does, with Accept and Decline above every column; nothing else can be done while it waits.");
                    LevelUpDriver.Click(w.Root, "xk-combat-prompt-accept");
                    for (var i = 0; i < 3; i++) yield return null;
                    yield return Shoot(camera, "p10_14_reaction_resolved", "The answer applied: the interrupted attack resolved again with the reaction — the result and the log say how.");
                }

                for (var i = 0; i < 200 && c.InFight; i++)
                {
                    if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                    else CombatDriver.Step(c);
                    yield return null;
                }

                for (var i = 0; i < 3; i++) yield return null;
                yield return Shoot(camera, "p10_15_outing_settled", "The outing over: the result, the objective, and what it left in the world (fates, experience, the chronicle).");
            }
        }

        /// <summary>
        /// Frame time in the editor's play mode (not a player build) at fixed poses in the planned town, and the scene's
        /// size: the environment's build time on entering, renderers, triangles and lights. Written to the capture log.
        /// </summary>
        [UnityTest]
        public IEnumerator Capture4_Performance()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to measure.");
            var bootstrap = Bootstrap;
            var session = bootstrap.Session;
            var c = Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlements().First(x => x.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 30 && c.EnvironmentRoot == null; i++) yield return null;
            watch.Stop();
            Runtime.Playback.Pause();
            for (var i = 0; i < 3; i++) yield return null; // the merged-away pieces are destroyed at the end of the frame
            var root = c.EnvironmentRoot;
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            long triangles = 0;
            foreach (var r in renderers)
            {
                var f = r.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null) triangles += f.sharedMesh.triangles.Length / 3;
            }

            var lights = root.GetComponentsInChildren<Light>(true).Length;
            var colliders = root.GetComponentsInChildren<Collider>().Length;
            Log.Add($"PERF entering {town.Name}: {watch.ElapsedMilliseconds} ms until the environment exists (plan to scene, residents included); {renderers.Length} renderers after merging, {triangles:N0} triangles, {lights} lights, {colliders} colliders.");
            var plan = c.Streamer.Current.Plan;
            var hall = plan.FindBuilding(SettlementSites.Hall);
            var origin = new PlanPoint(0, 0);
            var poses = new List<(string name, PlanPoint at, PlanPoint toward, float pitch)>
            {
                ("arrival", plan.Arrival, origin, 14f),
                ("square", Toward(origin, hall.Center, -6f), hall.Center, 10f),
                ("from the keep", Toward(plan.FindBuilding(SettlementSites.Landmark).Center, origin, 12f), origin, 18f),
            };
            foreach (var (name, at, toward, pitch) in poses)
            {
                yield return StandAt(at, toward, pitch);
                for (var i = 0; i < 30; i++) yield return null;
                var samples = new List<float>();
                for (var i = 0; i < 180; i++)
                {
                    yield return null;
                    samples.Add(Time.unscaledDeltaTime * 1000f);
                }

                samples.Sort();
                Log.Add($"PERF {name}: mean {samples.Average():0.0} ms, 95th percentile {samples[(int)(samples.Count * 0.95f)]:0.0} ms over 180 frames.");
            }

            Log.Add($"PERF machine: {SystemInfo.processorType}, {SystemInfo.processorCount} threads, {SystemInfo.systemMemorySize} MB; {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}), {Screen.width}x{Screen.height}; editor play mode, Unity {UnityEngine.Application.unityVersion}.");
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
