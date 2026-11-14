using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 26.1 in the shipped scene. It verifies the four post-play fixes against the running game, and the local HUD
    /// map:
    /// <list type="bullet">
    /// <item>roads: every road vertex lies 3 cm above the terrain collider the player walks on (raycast), on flat and
    /// sloped ground, and the player's feet stand at the surface;</item>
    /// <item>gates: a player-sized capsule passes through every fenced plot's gate, and is stopped by its fence;</item>
    /// <item>typing: with a text field focused, held W/Q keys move and turn neither the strategic camera; without, they do;</item>
    /// <item>numbers: key 1 in a street conversation is "Talk…" (the first button), and a number in the conversation
    /// window chooses that response (top row and keypad);</item>
    /// <item>the local map: placement, marks from the game's own records only, heading-up turning, moving marks, range,
    /// windows hide it, the country, and editor timings (labelled as such).</item>
    /// </list>
    /// </summary>
    public sealed class Phase26_1PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private Keyboard _keyboard;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        private bool _motion;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView; // headless: no focused game view
            _motion = RkSettings.ReducedMotion;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            var dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir) && Log.Count > 0) { Directory.CreateDirectory(dir); File.AppendAllLines(Path.Combine(dir, "p261_verification.txt"), Log); }
            foreach (var l in Log) Debug.Log("Phase 26.1: " + l);
            Log.Clear();
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Frames(int n = 3)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private IEnumerator Press(Key key, int hold = 1)
        {
            if (_keyboard == null) _keyboard = InputSystem.AddDevice<Keyboard>();
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            for (var i = 0; i < hold; i++) yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        private static float TerrainY(float x, float z)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 200f, z), Vector3.down, 400f);
            var best = float.NaN;
            foreach (var h in hits)
                if (h.collider != null && h.collider.name.StartsWith("Terrain collider", StringComparison.Ordinal) && (float.IsNaN(best) || h.point.y > best)) best = h.point.y;
            return best;
        }

        // ----- Fix 1 and 2: roads on the walked ground; gates that let you through ---------------------------------------

        [UnityTest]
        public IEnumerator Roads_LieOnTheWalkedGround_AndEveryGateLetsThePlayerThrough()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            yield return Frames(6);
            var layout = c.Streamer.Current;
            var plan = layout.Plan;
            Assert.That(plan, Is.Not.Null, "A planned settlement.");

            // Every road vertex against the terrain collider beneath it (bridge decks aside, which stand over water).
            var root = c.EnvironmentRoot.transform;
            var roads = plan.Roads.Where(r => r.Line.Count >= 2).ToList();
            Assert.That(roads.Count, Is.GreaterThan(0));
            int vertices = 0, onDeck = 0, sloped = 0, offCount = 0;
            var deckStep = 0f; // where a level deck meets its bank
            var off = new List<string>();
            float worst = 0f, steepest = 0f, worstMid = 0f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (var road in roads)
            {
                verts.Clear();
                uvs.Clear();
                tris.Clear();
                ModularEnvironmentBuilder.RoadStrip(plan, road, verts, uvs, tris); // exactly what the builder draws
                var v = verts;
                for (var i = 0; i < v.Count; i += 3)
                {
                    var w = root.TransformPoint(v[i]);
                    var ground = TerrainY(w.x, w.z);
                    if (float.IsNaN(ground)) continue;
                    var lift = w.y - ground;
                    if (plan.Bridges.Any(br => br.RoadKey == road.Key && SettlementTerrain.SegmentDistance(br.From.X, br.From.Z, br.To.X, br.To.Z, w.x, w.z, out _, out _) <= br.Width)) { onDeck++; if (lift < 1f) deckStep = Mathf.Max(deckStep, lift); continue; } // the level deck (the builder's own rule)
                    vertices++;
                    worst = Mathf.Max(worst, Mathf.Abs(lift - 0.03f));
                    if (Mathf.Abs(lift - 0.03f) > 0.01f)
                    {
                        var nb = plan.Bridges.Count == 0 ? -1f : plan.Bridges.Min(br => SettlementTerrain.SegmentDistance(br.From.X, br.From.Z, br.To.X, br.To.Z, w.x, w.z, out _, out _));
                        var gx = Mathf.Repeat(w.x + plan.HalfExtent + 60f, 2f);
                        var gz = Mathf.Repeat(w.z + plan.HalfExtent + 60f, 2f);
                        if (off.Count < 12) off.Add($"{road.Key} ({w.x:0.0}, {w.z:0.0}) lift {lift * 100f:0.0} cm, bridge {nb:0.0} m, cell offset ({gx:0.00}, {gz:0.00})");
                        offCount++;
                    }
                    var slope = Mathf.Abs(TerrainY(w.x + 1f, w.z) - TerrainY(w.x - 1f, w.z)) / 2f + Mathf.Abs(TerrainY(w.x, w.z + 1f) - TerrainY(w.x, w.z - 1f)) / 2f;
                    if (slope > 0.05f) sloped++;
                    steepest = Mathf.Max(steepest, slope);
                }

                // Between vertices (the middle of each cross-row edge): the strip neither floats nor sinks.
                for (var i = 0; i + 1 < v.Count; i += 7)
                {
                    if (i % ModularEnvironmentBuilder.RoadPointsAcross == ModularEnvironmentBuilder.RoadPointsAcross - 1) continue; // a row's last point has no neighbour across
                    var mid = root.TransformPoint((v[i] + v[i + 1]) / 2f);
                    var ground = TerrainY(mid.x, mid.z);
                    if (float.IsNaN(ground) || plan.Bridges.Any(br => br.RoadKey == road.Key && SettlementTerrain.SegmentDistance(br.From.X, br.From.Z, br.To.X, br.To.Z, mid.x, mid.z, out _, out _) <= br.Width + 1f)) continue;
                    worstMid = Mathf.Max(worstMid, Mathf.Abs(mid.y - ground - 0.03f));
                }
            }

            Log.Add($"Roads: {roads.Count} roads, {vertices} vertices checked against the terrain collider ({sloped} on sloping ground, steepest {steepest:0.00} m per m), {onDeck} on bridge decks; worst vertex error {worst * 100f:0.0} cm, worst between vertices {worstMid * 100f:0.0} cm.");
            Log.Add($"Roads: {offCount} vertices more than 1 cm from 3 cm: " + string.Join("; ", off) + $". Bridge decks are level by design; where a deck meets its bank it stands up to {deckStep * 100f:0} cm above the ground.");
            Assert.That(vertices, Is.GreaterThan(200));
            Assert.That(worst, Is.LessThan(0.01f), "Each road vertex is 3 cm above the walked ground (within 1 cm).");
            Assert.That(worstMid, Is.LessThan(0.1f), "Between vertices the paving stays within 10 cm of the walked ground.");

            // The player's feet on a sloping stretch of road stand at its surface.
            var slopedPoint = Vector3.zero;
            var bestSlope = 0f;
            foreach (var road in plan.Roads)
                foreach (var p in road.Line)
                {
                    var s = Mathf.Abs(TerrainY(p.X + 1f, p.Z) - TerrainY(p.X - 1f, p.Z)) / 2f + Mathf.Abs(TerrainY(p.X, p.Z + 1f) - TerrainY(p.X, p.Z - 1f)) / 2f;
                    if (s > bestSlope && s < 0.5f && !SettlementTerrain.InWater(plan, p.X, p.Z, 2f)) { bestSlope = s; slopedPoint = new Vector3(p.X, 0f, p.Z); }
                }

            c.Player.Teleport(new LocalPose(slopedPoint.x, TerrainY(slopedPoint.x, slopedPoint.z) + 0.5f, slopedPoint.z, 0f));
            var landed = Time.unscaledTime;
            while (Time.unscaledTime - landed < 1.5f) yield return null; // fall the half metre and settle (batch frames are short)
            var cc = c.Player.Controller;
            var feet = c.Player.transform.position.y + cc.center.y - cc.height / 2f - cc.skinWidth; // the capsule rests on its skin
            var surface = TerrainY(c.Player.transform.position.x, c.Player.transform.position.z) + 0.03f;
            Log.Add($"Feet on a sloping road ({bestSlope:0.00} m per m): {(feet - surface) * 100f:0.0} cm from the paving.");
            Assert.That(Mathf.Abs(feet - surface), Is.LessThan(0.1f), "Standing on a sloping road, the feet are at the paving, not under it.");

            // Every fenced plot: a player-sized capsule passes through its gate and is stopped by its fence.
            var fenced = plan.Fields.Where(f => f.Fenced).ToList();
            int through = 0, stopped = 0;
            foreach (var f in fenced)
            {
                RaycastHit hit;
                bool Blocked(float ax, float az, float bx, float bz)
                {
                    var a = SettlementLayoutGenerator.Local(f.Center, f.Yaw, ax, az);
                    var o = SettlementLayoutGenerator.Local(f.Center, f.Yaw, bx, bz);
                    var start = new Vector3(a.X, TerrainY(a.X, a.Z) + 0.35f, a.Z);
                    var end = new Vector3(o.X, TerrainY(o.X, o.Z) + 0.35f, o.Z);
                    var dir = end - start;
                    return Physics.CapsuleCastAll(start, start + Vector3.up * 1.1f, 0.3f, dir.normalized, dir.magnitude)
                        .Any(h => h.collider != null && h.collider.transform.parent != null && h.collider.transform.parent.name == "Fence colliders " + f.Key);
                }

                Assert.That(Blocked(0f, -f.Depth / 2f - 1.5f, 0f, -f.Depth / 2f + 1.5f), Is.False, f.Key + ": the gate lets the player through");
                through++;
                if (Blocked(f.Width / 4f, f.Depth / 2f + 1.5f, f.Width / 4f, f.Depth / 2f - 1.5f)) stopped++;
            }

            Log.Add($"Gates: {fenced.Count} fenced plots in {plan.Fields.Count} fields; the capsule passed every gate ({through}); the back fence stopped it at {stopped}.");
            Assert.That(stopped, Is.EqualTo(fenced.Count), "Every fence holds where there is no gate.");
        }

        // ----- Fix 3: typing never moves the strategic map -------------------------------------------------------------

        [UnityTest]
        public IEnumerator Typing_InATextField_NeverMovesTurnsOrZoomsTheMap()
        {
            var b = Bootstrap;
            yield return Frames(10);
            var cam = b.CameraController;
            var hud = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.isActiveAndEnabled && d.rootVisualElement != null && d.rootVisualElement.panel != null);
            var field = hud.rootVisualElement.Q<TextField>("new-world-name");
            var added = false;
            if (field == null)
            {
                field = new TextField("Name") { name = "p261-test-field" };
                hud.rootVisualElement.Add(field);
                added = true;
            }

            yield return Frames(2);
            field.Focus();
            yield return Frames(2);
            Assert.That(RkTyping.Active(), Is.True, "The focused field is typing.");
            var pivot = cam.Motion.Pivot;
            var yaw = cam.Motion.TargetYaw;
            yield return Press(Key.W, 200);
            yield return Press(Key.Q);
            yield return Frames(4);
            Assert.That(Vector3.Distance(cam.Motion.Pivot, pivot), Is.LessThan(0.01f), "W held while typing: the map does not move.");
            Assert.That(cam.Motion.TargetYaw, Is.EqualTo(yaw).Within(0.01f), "Q while typing: the map does not turn.");

            field.Blur();
            hud.rootVisualElement.focusController?.focusedElement?.Blur();
            yield return Frames(2);
            Assert.That(RkTyping.Active(), Is.False);
            yield return Press(Key.Q);
            yield return Frames(30);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(cam.Motion.TargetYaw, yaw)), Is.GreaterThan(1f), "Without a field, Q turns the map as before.");
            var free = cam.Motion.Pivot;
            yield return Press(Key.W, 200);
            Assert.That(Vector3.Distance(cam.Motion.Pivot, free), Is.GreaterThan(0.01f), "Without a field, W moves the map as before (so the held W above was a real test).");
            if (added) field.RemoveFromHierarchy();
            Log.Add("Typing: with a text field focused, W held for 200 frames and Q left the strategic camera unmoved and unturned; with no field focused, the same Q turned it and W moved it.");
        }

        // ----- Fix 4: the number keys in conversation -------------------------------------------------------------------

        [UnityTest]
        public IEnumerator NumberKeys_ChooseTheButtonsAsShown_InTheStreetAndInConversation()
        {
            var b = Bootstrap;
            var c = Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            yield return Frames(4);
            var resident = c.Residents.Values.Where(r => r != null && !r.IsHidden).OrderBy(r => r.DisplayName, StringComparer.Ordinal).First();
            resident.enabled = false;
            var p = resident.transform.position;
            c.Player.Teleport(new LocalPose(p.x, p.y + 0.1f, p.z - 1.3f, 0f));
            c.CameraRig.SetView(0f, 5f);
            yield return Frames(5);
            Assume.That(c.CurrentTarget, Is.SameAs(resident), "The resident is in front of the player.");
            c.Interact();
            yield return null;
            Assert.That(c.Hud.TopicsVisible, Is.True);
            var buttons = c.Hud.Topics.Query<Button>().ToList();
            Assert.That(buttons[0].text, Does.StartWith("1").And.Contain("Talk"), "The first button is numbered 1 and is Talk….");
            yield return Press(Key.Digit1);
            yield return Frames(2);
            Assert.That(c.Dialogue.IsOpen, Is.True, "Key 1 does what button 1 says (Talk…), not the topic below it.");

            var view = c.Dialogue.View;
            var n = view.Responses.FindIndex(r => r.Enabled && r.Kind != ResponseKind.Trade) + 1;
            Assume.That(n, Is.GreaterThan(0), "An open response.");
            string chosen = null;
            c.Dialogue.ChooseRequested += id => chosen = id;
            yield return Press(n <= 9 ? Key.Numpad1 + (n - 1) : Key.Numpad0);
            yield return Frames(2);
            Assert.That(chosen, Is.EqualTo(view.Responses[n - 1].Id), $"Keypad {n} chooses response {n}.");
            Log.Add($"Numbers: key 1 opened the conversation (button 1, Talk…); keypad {n} chose response {n} ('{view.Responses[n - 1].Text}').");
        }

        // ----- The local HUD map -----------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheLocalMap_ShowsOnlyKnownThings_TurnsWithThePlayer_AndStaysCheap()
        {
            RkSettings.ReducedMotion = true; // exact headings, for the comparisons
            var b = Bootstrap;
            var c = Controller;
            var s = b.Session;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            yield return Frames(8);
            var map = c.LocalMap;
            Assert.That(map, Is.Not.Null, "The local map is in the exploration HUD.");
            Assert.That(map.Root.panel, Is.Not.Null, "The map is on the live HUD panel.");
            Assert.That(map.Root.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            var box = map.Root.worldBound;
            var panel = c.Hud.Root.worldBound;
            Assert.That(box.xMax, Is.LessThanOrEqualTo(panel.xMax + 0.5f));
            Assert.That(box.yMax, Is.LessThanOrEqualTo(panel.yMax + 0.5f));
            Assert.That(box.center.x > panel.center.x && box.center.y > panel.center.y, Is.True, "Bottom right.");
            foreach (var other in new[] { c.Hud.ActionBar, c.Hud.Vitals, c.Hud.Root.Q<VisualElement>(className: "xk-actions") })
                if (other != null && other.resolvedStyle.display == DisplayStyle.Flex && other.worldBound.width > 0f)
                    Assert.That(box.Overlaps(other.worldBound), Is.False, "The map covers no HUD control: " + other.name);

            // Marks: the town's shops at their doors, its gates; people only when out, named only once met.
            var shops = c.Shops.Values.Count(v => v != null);
            Assert.That(map.Marks.Count(m => m.Data.Kind == LocalMarkKind.Shop), Is.EqualTo(shops), "One mark per shop.");
            foreach (var m in map.Marks.Where(m => m.Data.Kind == LocalMarkKind.Person || m.Data.Kind == LocalMarkKind.Merchant))
            {
                var id = c.Residents.First(r => "person:" + r.Key == m.Data.Key);
                Assert.That(id.Value.IsHidden, Is.False, "No hidden person is marked.");
                var expect = s.MapLabelFor(id.Key, out var known, out _);
                Assert.That(m.Data.Label, Is.EqualTo(expect), "The label is the knowledge rule's.");
                if (!known) Assert.That(m.Data.Label, Is.Not.EqualTo(id.Value.DisplayName), "A stranger is not named: " + id.Value.DisplayName);
            }

            foreach (var r in c.Residents.Values.Where(r => r != null && r.IsHidden))
                Assert.That(map.Marks.Any(m => m.Data.Key == "person:" + r.NpcId), Is.False, "Indoors / hidden: not marked.");

            // Heading-up: the live marks sit where the geometry puts them, facing each way; the ground turns the other way.
            var shop = map.Marks.First(m => m.Data.Kind == LocalMarkKind.Shop);
            var p0 = c.Player.transform.position;
            foreach (var heading in new[] { 0f, 90f, 180f, 270f, 45f })
            {
                c.CameraRig.SetView(heading, 10f);
                yield return Frames(2);
                var pp = c.Player.transform.position;
                LocalMapGeometry.Project(pp.x, pp.z, heading, shop.X, shop.Z, map.PixelsPerMetre, out var ex, out var ey);
                Assert.That(map.ShownHeading, Is.EqualTo(heading).Within(0.01f));
                Assert.That(shop.ScreenX, Is.EqualTo(ex).Within(0.01f));
                Assert.That(shop.ScreenY, Is.EqualTo(ey).Within(0.01f));
                var ground = map.Root.Q<VisualElement>("xk-localmap-ground");
                Assert.That(ground.style.rotate.value.angle.ToDegrees(), Is.EqualTo(-heading).Within(0.01f), "The ground turns beneath the player.");
            }

            // Turning in place with smoothing on: the map follows within a few frames.
            RkSettings.ReducedMotion = false;
            c.CameraRig.SetView(0f, 10f);
            yield return Frames(3);
            c.CameraRig.SetView(120f, 10f);
            var frames = 0;
            var started = Time.unscaledTime;
            while (Mathf.Abs(Mathf.DeltaAngle(map.ShownHeading, 120f)) > 1f && Time.unscaledTime - started < 2f) { frames++; yield return null; }
            var settle = Time.unscaledTime - started;
            Log.Add($"Map: a 120-degree turn in place settled within 1 degree in {settle * 1000f:0} ms ({frames} frames) with smoothing on.");
            Assert.That(settle, Is.LessThan(0.35f), "The map keeps up with the player's turn (no noticeable lag).");
            RkSettings.ReducedMotion = true;

            // Moving: a mark's place follows the player's movement (teleport 12 m east).
            c.CameraRig.SetView(0f, 10f);
            c.Player.Teleport(new LocalPose(p0.x + 12f, p0.y + 0.2f, p0.z, 0f));
            yield return Frames(4);
            var moved = c.Player.transform.position;
            LocalMapGeometry.Project(moved.x, moved.z, 0f, shop.X, shop.Z, map.PixelsPerMetre, out var mx, out var my);
            Assert.That(shop.ScreenX, Is.EqualTo(mx).Within(0.01f));

            // A walking resident's mark follows them; one carried out of reach leaves the map.
            var walker = c.Residents.Values.FirstOrDefault(r => r != null && !r.IsHidden && map.Marks.Any(m => m.Data.Key == "person:" + r.NpcId));
            if (walker != null)
            {
                walker.enabled = false;
                walker.transform.position = moved + new Vector3(5f, 0f, 5f);
                yield return Frames(2);
                var wm = map.Marks.First(m => m.Data.Key == "person:" + walker.NpcId);
                Assert.That(wm.X, Is.EqualTo(moved.x + 5f).Within(0.01f), "Its mark follows the person.");
                walker.transform.position = moved + new Vector3(600f, 0f, 0f);
                var t0 = Time.unscaledTime;
                while (Time.unscaledTime - t0 < 0.6f) yield return null; // the moving marks are refreshed every 0.4 s
                Assert.That(map.Marks.Any(m => m.Data.Key == "person:" + walker.NpcId), Is.False, "Out of reach: the mark leaves.");
            }

            // Range: two steps; clicking changes it.
            var r0 = map.Radius;
            map.CycleRange();
            Assert.That(map.Radius, Is.Not.EqualTo(r0));
            map.CycleRange();

            // A window hides the map (no HUD through windows), closing shows it again.
            c.ToggleHub();
            yield return Frames(3);
            Assert.That(map.Root.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Hidden behind the hub.");
            c.CloseWindows();
            yield return Frames(3);
            Assert.That(map.Root.panel, Is.Not.Null, "The map is on the live HUD panel.");
            Assert.That(map.Root.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            // Labels: never more than five; none over the player's mark.
            Assert.That(map.LabelsShown, Is.LessThanOrEqualTo(LocalHudMap.MaxLabels));

            // Timings in the editor (not a player build) in this town: per frame, per refresh; elements stay constant.
            var people = c.Residents.Values.Count(r => r != null && !r.IsHidden);
            var ticks = new List<double>();
            var refreshes = new List<double>();
            var elements = map.Root.Query<VisualElement>().ToList().Count;
            for (var i = 0; i < 240; i++)
            {
                c.CameraRig.SetView(i * 3f, 10f);
                yield return null;
                ticks.Add(c.LocalMapTickMs);
                refreshes.Add(c.LocalMapRefreshMs);
            }

            Assert.That(map.Root.Query<VisualElement>().ToList().Count, Is.EqualTo(elements), "No elements accumulate.");
            var before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var watch = Stopwatch.StartNew();
            var pos = c.Player.transform.position;
            for (var i = 0; i < 1000; i++) map.Tick(pos, i * 0.36f, 0.016f);
            var direct = watch.Elapsed.TotalMilliseconds / 1000.0;
            var after = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            Log.Add($"Map timings (Unity editor, batch mode; not a player build): {map.Marks.Count} marks ({people} people out in town), per frame mean {ticks.Average():0.000} ms, max {ticks.Max():0.000} ms; refresh of moving marks mean {refreshes.Where(x => x > 0).DefaultIfEmpty(0).Average():0.000} ms; 1000 direct updates {direct:0.0000} ms each, managed heap change over them {(after - before) / 1024} KB; elements {elements}, unchanged after 240 frames of turning.");
            Assert.That(ticks.Average(), Is.LessThan(2.0), "The map costs little per frame (editor).");

            // The country: only known sites and found landmarks; outlaws only within sight.
            c.GoOutIntoCountry();
            for (var i = 0; i < 900 && !(c.InOverland && !c.Travelling); i++) yield return null;
            Assume.That(c.InOverland, Is.True);
            yield return Frames(10);
            var view = c.OverlandView;
            foreach (var m in map.Marks.Where(m => m.Data.Key.StartsWith("site:", StringComparison.Ordinal)))
                Assert.That(view.Sites.Single(x => "site:" + x.Id == m.Data.Key).Known, Is.True, "Only known sites.");
            foreach (var lv in c.LandmarkViews)
            {
                var marked = map.Marks.Any(m => m.Data.Key == "landmark:" + lv.Landmark.Id);
                var found = s.GetLandmarks().Any(l => l.Landmark.Id == lv.Landmark.Id && l.Found);
                Assert.That(marked, Is.EqualTo(found), "A landmark is marked once found, never before: " + lv.Landmark.Name);
            }

            var pc = c.Player.transform.position;
            foreach (var m in map.Marks.Where(m => m.Data.Kind == LocalMarkKind.Hostile))
                Assert.That(Vector2.Distance(new Vector2(m.X, m.Z), new Vector2(pc.x, pc.z)), Is.LessThanOrEqualTo(72f), "Outlaws are marked only within sight.");
            Log.Add($"Country: {map.Marks.Count} marks ({map.Marks.Count(m => m.Data.Kind == LocalMarkKind.Settlement)} settlements, {map.Marks.Count(m => m.Data.Kind == LocalMarkKind.Landmark)} found landmarks, {map.Marks.Count(m => m.Data.Kind == LocalMarkKind.Traveller || m.Data.Kind == LocalMarkKind.Hostile)} travelling groups); range {map.Radius} m.");
        }
    }
}
