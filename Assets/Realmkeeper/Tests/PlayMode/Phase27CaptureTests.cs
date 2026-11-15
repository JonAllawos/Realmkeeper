using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 27 screenshots from the running game (no mockups), at 1920×1080 through a free camera: streets of a small, a
    /// middle and a large settlement at noon, the market, a residential lane, the owner's banners at a gate, the watch,
    /// a storefront, animals, street details, trade signs, interiors by size and kind, a door, roads up and down hill, a
    /// road end and a junction. Runs only when RK_CAPTURE_DIR is set; writes JPEGs and p27_capture_log.txt.
    /// </summary>
    public sealed class Phase27CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private readonly List<string> _log = new List<string>();

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
            File.WriteAllLines(Path.Combine(_dir, "p27_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static IEnumerator Seconds(float s)
        {
            var t = Time.unscaledTime;
            while (Time.unscaledTime - t < s) yield return null;
        }

        private static float Ground(ExplorationController c, float x, float z) => LocalPaths.StandingHeight(c.Streamer.Current, x, z);

        private static IEnumerator Near(ExplorationController c, float x, float z, float wait = 2.5f)
        {
            c.Player.Teleport(new LocalPose(x, Ground(c, x, z) + 0.3f, z, 0f));
            yield return Seconds(wait); // the townsfolk near here are given figures, two each 0.3 s
        }

        [UnityTest]
        public IEnumerator Capture_Phase27_SettlementLife()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var b = Bootstrap;
            var c = Controller;
            for (var i = 0; i < 8; i++) yield return null;
            yield return CombatDriver.EnterTownAsFighter(b, Runtime, c);
            var s = b.Session;
            var now = s.GetWorldTime().MinuteOfDay;
            c.WaitHour((12 * 60 - now + 24 * 60) % (24 * 60));
            yield return Seconds(1f);
            var layout = c.Streamer.Current;
            var plan = layout.Plan;
            var town = s.GetSettlementsInView().First(x => x.SettlementId == layout.SettlementId);
            _log.Add($"Large settlement: {town.Name} ({town.SettlementType}), population {s.GetStreetLife(layout.SettlementId).Population}, owner {s.GetStreetLife(layout.SettlementId).OwnerName ?? "independent"}.");
            var root = c.EnvironmentRoot.transform;
            Vector3 W(float x, float z, float up) => root.TransformPoint(new Vector3(x, Ground(c, x, z) + up, z));

            // 03, 04: a large settlement's street (the way in) and its market at noon.
            var arrival = layout.ArrivalPose;
            var inward = new Vector2(-arrival.X, -arrival.Z).normalized;
            var streetAt = new Vector2(arrival.X, arrival.Z) + inward * 60f;
            yield return Near(c, streetAt.x, streetAt.y, 6f);
            yield return Shot("p27_03_large_street", W(streetAt.x, streetAt.y, 1.7f), W(streetAt.x + inward.x * 30f, streetAt.y + inward.y * 30f, 1.4f), $"{town.Name}: the way in at noon; {c.TownsfolkFigures} unnamed townsfolk shown of {c.Townsfolk.Count} out.");
            var edge = layout.PlazaRadius + 2f;
            yield return Near(c, 0f, -edge, 5f);
            yield return Shot("p27_04_market", W(0f, -edge - 3f, 2.2f), W(0f, 4f, 1.0f), $"The market square at noon: {c.TownsfolkFigures} townsfolk shown, stalls, benches, named residents.");

            // 05: a residential lane.
            var home = layout.Buildings.Where(x => x.Function == BuildingFunction.Home || x.Function == BuildingFunction.Cottage).OrderBy(x => Math.Abs(Math.Sqrt(x.Center.X * x.Center.X + x.Center.Z * x.Center.Z) - 55)).FirstOrDefault();
            if (home != null)
            {
                var d = LocalPaths.DoorOutside(home, 0f);
                var o = LocalPaths.ToWorld(home, 0f, -home.Depth / 2f - 12f);
                yield return Near(c, d.X, d.Z, 3f);
                yield return Shot("p27_05_residential", W(o.X, o.Z, 2.0f), W(home.Center.X, home.Center.Z, 1.5f), "A residential street: quieter than the market; dressing by the doors (a bench, a barrel or a wood pile at some homes).");
            }

            // 06: the owner's banners at a gate.
            var banner = layout.Dressing.FirstOrDefault(x => x.Kind == DressingKind.Banner && x.For.StartsWith("gate", StringComparison.Ordinal));
            if (banner != null)
            {
                var gate = plan.Gates[int.Parse(banner.For.Substring(5))];
                var inside = new Vector2(gate.X, gate.Z) * 0.82f;
                yield return Near(c, inside.x, inside.y, 3f);
                var bannerObjects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == ModularEnvironmentBuilder.BannerFor(layout.OwnerIndex)).ToList();
                _log.Add($"Banners in this town: {layout.Dressing.Count(x => x.Kind == DressingKind.Banner)} planned, {bannerObjects.Count} objects; nearest to this gate {(bannerObjects.Count > 0 ? bannerObjects.Min(t => Vector3.Distance(t.position, W(gate.X, gate.Z, 0f))) : -1f):0.0} m; planned for it at " + string.Join(" ", layout.Dressing.Where(x => x.For == banner.For).Select(x => $"({x.Position.X:0},{x.Position.Z:0})")) + $"; gate at ({gate.X:0},{gate.Z:0}).");
                yield return Shot("p27_06_faction_gate", W(inside.x, inside.y, 2.0f), W(gate.X, gate.Z, 2.0f), $"A gate with the owner's banners ({ModularEnvironmentBuilder.BannerFor(layout.OwnerIndex)}: the strategic map's colour for {s.GetStreetLife(layout.SettlementId).OwnerName ?? "an independent town"}), and the watch at its post.");
            }

            // 07: the watch on patrol.
            var patrol = c.Townsfolk.FirstOrDefault(x => x.Activity == TownsfolkActivity.Patrolling);
            if (patrol != null)
            {
                var p = TownsfolkPlanner.PositionAt(patrol, 0, out _, out _);
                yield return Near(c, p.X, p.Z, 3f);
                var guard = GameObject.Find("Townsperson " + patrol.Key);
                if (guard != null && guard.activeInHierarchy)
                {
                    var g = guard.transform.position;
                    yield return Shot("p27_07_guard_patrol", g + new Vector3(4f, 2.0f, 4f), g + Vector3.up * 1.2f, "The watch on patrol between a gate and the hall, along the streets.");
                }
                else _log.Add("p27_07_guard_patrol: the patrol's figure was not shown at the moment of capture.");
            }

            // 08, 11, 12: a storefront with its keeper; street details; trade signs.
            foreach (var (name, fit, note) in new[] { ("p27_08_storefront", ShopFit.General, "A storefront: its keeper at the counter, barrels and a crate by the door."), ("p27_11_street_details", ShopFit.Smithy, "A smithy: anvil, workbench and wood pile by its door, its sign above."), ("p27_12_shop_sign", ShopFit.Provisions, "A provisioner's sign (the kit's food sign) by the door.") })
            {
                var shop = layout.Buildings.FirstOrDefault(x => x.Fit == fit) ?? layout.Buildings.FirstOrDefault(x => x.Fit != ShopFit.None);
                if (shop == null) continue;
                var d = LocalPaths.DoorOutside(shop, 0f);
                var o = LocalPaths.ToWorld(shop, LocalPaths.DoorwayX(shop) + 2f, -shop.Depth / 2f - 8f);
                yield return Near(c, d.X, d.Z, 2f);
                yield return Shot(name, W(o.X, o.Z, name == "p27_12_shop_sign" ? 2.6f : 1.9f), W(d.X, d.Z, name == "p27_12_shop_sign" ? 2.4f : 1.2f), note + $" ({shop.Fit}).");
            }

            // 09: animals grazing.
            var animal = Object.FindObjectsByType<AmbientWander>(FindObjectsSortMode.None).FirstOrDefault();
            if (animal != null)
            {
                var a = animal.transform.position;
                c.Player.Teleport(new LocalPose(a.x + 5f, a.y + 0.3f, a.z + 5f, 0f));
                yield return Seconds(3f);
                a = animal.transform.position;
                yield return Shot("p27_09_animals", a + new Vector3(7f, 3f, 7f), a + Vector3.up * 0.5f, "Animals in their pasture (the kit's models where it has the species), grazing about inside the fence.");
            }

            // 18: a door, with the way to it.
            if (home != null)
            {
                var d = LocalPaths.DoorOutside(home, 0f);
                var o = LocalPaths.ToWorld(home, LocalPaths.DoorwayX(home), -home.Depth / 2f - 5f);
                yield return Shot("p27_18_door", W(o.X, o.Z, 1.7f), W(d.X, d.Z, 1.1f) + (W(d.X, d.Z, 0f) - W(o.X, o.Z, 0f)).normalized * 2f, "A house door seen square on: the leaf fills its frame, and the point people walk to is in front of it.");
            }

            // 19–22: roads uphill and downhill, a road's end, a junction.
            Vector2 best = default, dir = default;
            var slope = 0f;
            foreach (var r in plan.Roads)
                for (var i = 0; i + 1 < r.Line.Count; i++)
                {
                    var a = new Vector2(r.Line[i].X, r.Line[i].Z);
                    var k = i + 1;
                    while (k + 1 < r.Line.Count && Vector2.Distance(a, new Vector2(r.Line[k].X, r.Line[k].Z)) < 15f) k++; // a stretch of 15 m or more
                    var e = new Vector2(r.Line[k].X, r.Line[k].Z);
                    var len = Vector2.Distance(a, e);
                    if (len < 10f) continue;
                    var g = Mathf.Abs(Ground(c, e.x, e.y) - Ground(c, a.x, a.y)) / len;
                    if (g > slope && !SettlementTerrain.InWater(plan, a.x, a.y, 3f)) { slope = g; best = a; dir = (e - a).normalized * Mathf.Sign(Ground(c, e.x, e.y) - Ground(c, a.x, a.y)); }
                }

            if (slope > 0f)
            {
                yield return Near(c, best.x, best.y, 1f);
                yield return Shot("p27_19_road_uphill", W(best.x - dir.x * 4f, best.y - dir.y * 4f, 1.7f), W(best.x + dir.x * 20f, best.y + dir.y * 20f, 1.2f), $"A road climbing ({slope * 100f:0} % over its stretch): the paving follows the ground, its corners rounded.");
                var top = best + dir * 18f;
                yield return Shot("p27_20_road_downhill", W(top.x + dir.x * 4f, top.y + dir.y * 4f, 1.7f), W(top.x - dir.x * 20f, top.y - dir.y * 20f, 0.6f), "The same road looking downhill.");
            }

            var ends = new List<(Vector2 end, Vector2 back, bool junction)>();
            foreach (var r in plan.Roads)
            {
                if (r.Line.Count < 2) continue;
                var e = new Vector2(r.Line[r.Line.Count - 1].X, r.Line[r.Line.Count - 1].Z);
                var back = new Vector2(r.Line[r.Line.Count - 2].X, r.Line[r.Line.Count - 2].Z);
                var onOther = plan.Roads.Any(o => o != r && o.Line.Count > 1 && Enumerable.Range(0, o.Line.Count - 1).Any(i => SettlementTerrain.SegmentDistance(o.Line[i].X, o.Line[i].Z, o.Line[i + 1].X, o.Line[i + 1].Z, e.x, e.y, out _, out _) < o.Width / 2f + 1f));
                if (e.magnitude > layout.PlazaRadius + 4f && e.magnitude < layout.HalfExtent - 20f) ends.Add((e, (back - e).normalized, onOther));
            }

            var deadEnd = ends.FirstOrDefault(x => !x.junction);
            if (deadEnd.back != default)
            {
                yield return Near(c, deadEnd.end.x, deadEnd.end.y, 1f);
                var eye = deadEnd.end + deadEnd.back * 8f;
                yield return Shot("p27_21_road_end", W(eye.x, eye.y, 2.2f), W(deadEnd.end.x, deadEnd.end.y, 0f), "A road's end: rounded, draped on the ground.");
            }

            var join = ends.FirstOrDefault(x => x.junction);
            if (join.back != default)
            {
                yield return Near(c, join.end.x, join.end.y, 1f);
                var eye = join.end + join.back * 9f + new Vector2(-join.back.y, join.back.x) * 4f;
                yield return Shot("p27_22_road_junction", W(eye.x, eye.y, 4f), W(join.end.x, join.end.y, 0f), "A junction: a lane meeting a street, both draped, no gap.");
            }

            // 10: children at play.
            var kid = c.Townsfolk.FirstOrDefault(x => x.Child && x.Activity == TownsfolkActivity.Playing);
            if (kid != null)
            {
                var k = kid.Route[0];
                yield return Near(c, k.X + 3f, k.Z + 3f, 4f);
                var fig = GameObject.Find("Townsperson " + kid.Key);
                var at = fig != null ? fig.transform.position : W(k.X, k.Z, 0f);
                yield return Shot("p27_10_children", at + new Vector3(5f, 2.2f, 5f), at + Vector3.up * 0.6f, $"Children at play ({c.Townsfolk.Count(x => x.Child)} children of {c.Townsfolk.Count} out at noon): the town's own bodies at a child's size.");
            }
            else _log.Add("p27_10_children: no child at play in the plan at this hour.");

            // 23: a smithy's forge yard; 24: a shop trading from its stall; 25: a painted sign; 26: the watch in the owner's colour.
            var yard = layout.Dressing.FirstOrDefault(x => x.Kind == DressingKind.ForgeYard);
            if (yard != null)
            {
                yield return Near(c, yard.Position.X, yard.Position.Z, 3f);
                var y0 = W(yard.Position.X, yard.Position.Z, 0f);
                var smithyB = layout.Buildings.First(x => x.Key == yard.For);
                var (yx, yz) = StreetDressingPlanner.Local(smithyB, yard.Position);
                var eye = LocalPaths.ToWorld(smithyB, yx * 1.2f, -smithyB.Depth / 2f - 9f); // from the street in front, looking at the yard
                yield return Shot("p27_23_forge_yard", W(eye.X, eye.Z, 2.6f), y0 + Vector3.up * 1.0f, "A smithy working partly outdoors: its forge yard (the kit's blacksmith stall) beside the building.");
            }

            var stallShop = layout.Buildings.FirstOrDefault(x => x.StallAt.HasValue);
            if (stallShop != null)
            {
                var st = stallShop.StallAt.Value;
                yield return Near(c, st.X, st.Z, 3f);
                var s0 = W(st.X, st.Z, 0f);
                var o = LocalPaths.ToWorld(stallShop, LocalPaths.DoorwayX(stallShop) + 3f, -stallShop.Depth / 2f - 9f);
                yield return Shot("p27_24_stall_shop", W(o.X, o.Z, 2.0f), s0 + Vector3.up * 1.0f, $"An open-air stall shop ({stallShop.Fit}): the keeper serves from behind the stall by day.");
            }

            var signed = layout.Buildings.FirstOrDefault(x => x.Fit == ShopFit.Leatherworks || x.Fit == ShopFit.Tailor || x.Fit == ShopFit.General || x.Fit == ShopFit.Carpentry);
            if (signed != null)
            {
                var d = LocalPaths.DoorOutside(signed, 0f);
                var o = LocalPaths.ToWorld(signed, LocalPaths.DoorwayX(signed) - 4f, -signed.Depth / 2f - 3.5f);
                yield return Near(c, d.X, d.Z, 2f);
                // Square on to the board's face: along the wall, at the board's height (the kit's sign slot, the door's left).
                var face = LocalPaths.ToWorld(signed, LocalPaths.DoorwayX(signed) - 1.15f, -signed.Depth / 2f - 0.78f);
                var eye = LocalPaths.ToWorld(signed, LocalPaths.DoorwayX(signed) - 5.5f, -signed.Depth / 2f - 1.2f);
                var lift = signed.FloorHeight + 2.55f - Ground(c, face.X, face.Z);
                yield return Shot("p27_25_painted_sign", W(eye.X, eye.Z, lift + 0.2f), W(face.X, face.Z, lift), $"A painted signboard ({signed.Fit}: its trade's emblem, no lettering) hung beside the door.", 40f);
            }

            var post = c.Townsfolk.FirstOrDefault(x => x.Activity == TownsfolkActivity.Guarding);
            if (post != null)
            {
                yield return Near(c, post.Route[0].X, post.Route[0].Z, 3f);
                var g = GameObject.Find("Townsperson " + post.Key);
                if (g != null && g.activeInHierarchy) yield return Shot("p27_26_watch_colours", g.transform.position + new Vector3(3f, 1.8f, 3f), g.transform.position + Vector3.up * 1.1f, $"The watch at a gate, dressed in the owner's colour ({s.GetStreetLife(layout.SettlementId).OwnerName}).");
            }

            var store = layout.Buildings.FirstOrDefault(x => x.Function == BuildingFunction.Warehouse || x.Function == BuildingFunction.Stable || x.Function == BuildingFunction.Barn);
            if (store != null)
            {
                // From the road just in front of the door (further out crosses the street into the buildings opposite).
                var o = LocalPaths.ToWorld(store, store.Width * 0.25f, -store.Depth / 2f - 6.5f);
                yield return Near(c, store.Center.X + 30f, store.Center.Z + 30f, 2f); // the player out of the shot
                yield return Shot("p27_27_building_types", W(o.X, o.Z, 2.2f), W(store.Center.X, store.Center.Z, 3.0f), $"A {store.Function}: the barn form (gable end to the street, stone, no windows), not an ordinary house.", 80f);
            }

            // 13–17: interiors by size and kind (entered homes and stores are the generator's; the tavern and smithy are open to the street).
            var tavern = layout.Buildings.FirstOrDefault(x => x.Kind == BuildingKind.Hall);
            if (tavern != null)
            {
                var rot = Quaternion.Euler(0f, -tavern.Yaw, 0f);
                var ctr = root.TransformPoint(new Vector3(tavern.Center.X, tavern.FloorHeight, tavern.Center.Z));
                yield return Near(c, tavern.Center.X, tavern.Center.Z, 1f);
                yield return Shot("p27_15_tavern", ctr + rot * new Vector3(0f, 1.6f, -tavern.Depth / 2f + 0.8f), ctr + rot * new Vector3(0f, 1.0f, tavern.Depth / 2f - 1f), "Inside the tavern (its hall, tables and hearth).", 70f);
            }

            var smithy = layout.Buildings.FirstOrDefault(x => x.Fit == ShopFit.Smithy);
            if (smithy != null)
            {
                var rot = Quaternion.Euler(0f, -smithy.Yaw, 0f);
                var ctr = root.TransformPoint(new Vector3(smithy.Center.X, smithy.FloorHeight, smithy.Center.Z));
                yield return Shot("p27_16_blacksmith", ctr + rot * new Vector3(0f, 1.6f, -smithy.Depth / 2f + 0.6f), ctr + rot * new Vector3(0f, 0.8f, smithy.Depth / 2f - 1f), "Inside a smithy (its forge station and counter).", 75f);
            }

            foreach (var (name, pick, note) in new (string, Func<LayoutBuilding, bool>, string)[]
                     {
                         ("p27_13_small_house", x => (x.Function == BuildingFunction.Cottage || x.Function == BuildingFunction.Home) && x.Width * x.Depth < 70f, "A small house inside: its rooms fit within its walls."),
                         ("p27_14_large_house", x => x.Function == BuildingFunction.LargeHome, "A large house inside: more rooms, a second bedroom, more furniture."),
                         ("p27_17_warehouse", x => x.Function == BuildingFunction.Warehouse || x.Function == BuildingFunction.Workshop, "A store or workshop inside: shelves, barrels, sacks in proportion to its floor."),
                     })
            {
                var bld = layout.Buildings.Where(x => x.Fit == ShopFit.None && x.Kind != BuildingKind.Hall).Where(pick).OrderByDescending(x => name == "p27_13_small_house" ? -x.Width * x.Depth : x.Width * x.Depth).FirstOrDefault();
                if (bld == null) { _log.Add(name + ": none in this town."); continue; }
                var r = c.EnterBuilding(bld.Key);
                yield return Seconds(1f);
                if (!r.IsSuccess) { _log.Add($"{name}: could not enter: {r.Message}"); continue; }
                var p = c.Player.transform.position;
                var fwd = c.Player.transform.forward;
                var body = c.Player.GetComponentsInChildren<Renderer>(true).Where(x => x.enabled).ToList();
                foreach (var r0 in body) r0.enabled = false;
                yield return Shot(name, p + Vector3.up * 1.75f - fwd * 0.6f, p + fwd * 4f + Vector3.up * 0.9f, note + $" ({bld.Function}, {bld.Width:0}×{bld.Depth:0} m).", 80f);
                foreach (var r0 in body) r0.enabled = true;
                c.LeavePlace();
                yield return Seconds(1f);
                layout = c.Streamer.Current;
                root = c.EnvironmentRoot.transform;
            }

            // 02, 01: a middle and a small settlement's streets at noon.
            foreach (var (name, type) in new[] { ("p27_02_medium_street", SettlementType.Village), ("p27_01_small_street", SettlementType.Hamlet) })
            {
                var other = s.GetSettlementsInView().FirstOrDefault(x => x.SettlementType == type);
                if (other == null) { _log.Add(name + ": no " + type + " in view."); continue; }
                c.ReturnToWorldMap();
                yield return Seconds(1f);
                b.Selection.Select(other.SettlementId);
                var entered = b.Hud.EnterSelectedSettlement();
                yield return Seconds(1f);
                if (!entered.IsSuccess) { _log.Add($"{name}: could not enter {other.Name}: {entered.Message}"); continue; }
                Runtime.Playback.Pause();
                layout = c.Streamer.Current;
                root = c.EnvironmentRoot.transform;
                arrival = layout.ArrivalPose;
                inward = new Vector2(-arrival.X, -arrival.Z).normalized;
                var at = new Vector2(arrival.X, arrival.Z) + inward * 20f;
                yield return Near(c, at.x, at.y, 4f);
                yield return Shot(name, W(at.x, at.y, 1.8f), W(at.x + inward.x * 30f, at.y + inward.y * 30f, 1.3f), $"{other.Name} ({type}, population {s.GetStreetLife(other.SettlementId).Population}): {c.Townsfolk.Count} unnamed out at this hour, {c.TownsfolkFigures} shown.");
            }
        }

        private IEnumerator Shot(string name, Vector3 eye, Vector3 look, string note, float fov = 55f)
        {
            var go = new GameObject("capture camera");
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1500f;
            var main = Camera.main;
            if (main != null)
            {
                cam.clearFlags = main.clearFlags;
                cam.backgroundColor = main.backgroundColor;
                var src = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                var dst = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() ?? go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (src != null) dst.renderPostProcessing = src.renderPostProcessing;
            }

            go.transform.position = eye;
            go.transform.LookAt(look);
            yield return null;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(go);
            _log.Add($"{name}: 1920x1080. {note}");
        }
    }
}
