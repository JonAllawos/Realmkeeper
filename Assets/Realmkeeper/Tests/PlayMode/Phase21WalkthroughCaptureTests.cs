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
    /// Phase 21: a walk through a town with a free camera, recording what a player sees: the town from above and at street
    /// level, every shop's inside, an entered home and workshop, and the same street at dusk and night. Run before the
    /// phase's changes (prefix "before") and after them ("after"), with RK_CAPTURE_DIR set; RK_CAPTURE_PREFIX names the set.
    /// </summary>
    public sealed class Phase21WalkthroughCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private string _prefix;
        private readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            _prefix = Environment.GetEnvironmentVariable("RK_CAPTURE_PREFIX") ?? "walk";
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
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, _prefix + "_walk_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();

        private static IEnumerator Settle(int frames = 6)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Walkthrough_Town_Shops_Interiors_DayAndNight()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture the walkthrough.");
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return Settle(10);
            var layout = c.Streamer.Current;
            var town = layout.Buildings;
            _log.Add($"Town: {layout.Type}, half extent {layout.HalfExtent:F0} m, {town.Count} buildings, {layout.Props.Count} props, {c.Residents.Count} residents shown, {c.Shops.Count} shops.");
            foreach (var g in town.GroupBy(b => b.Function?.ToString() ?? b.Kind.ToString()).OrderBy(g => g.Key)) _log.Add($"  {g.Key}: {g.Count()}");

            var centre = new Vector3(layout.Gate.X * 0f, 0f, 0f);
            yield return FreeShot("01_town_overview_high", new Vector3(0f, 70f, -layout.HalfExtent * 0.55f), Vector3.zero, "The whole town from above, morning.");
            yield return FreeShot("02_town_street_level", new Vector3(layout.ArrivalPose.X, 1.7f, layout.ArrivalPose.Z), new Vector3(0f, 1.5f, 0f), "From the arrival point toward the centre, at eye height.");

            // Inside every shop, from just inside the front, looking at the counter.
            var n = 3;
            foreach (var pair in c.Shops)
            {
                var shop = pair.Value;
                var t = shop.transform;
                var eye = t.TransformPoint(new Vector3(0f, 1.6f, -shop.Building.Depth / 2f + 0.8f));
                var look = t.TransformPoint(new Vector3(0f, 1.0f, shop.Building.Depth / 2f - 1.0f));
                yield return FreeShot($"{n:D2}_shop_{Safe(shop.ShopName)}", eye, look, $"Inside {shop.ShopName} ({shop.Building.Fit}): {shop.ShownTotal} wares shown, {shop.Dressed} pieces of its trade's furniture.", 62f);
                // Phase 21: from behind the counter toward the door, where the trade's bench, forge or oven stands.
                yield return FreeShot($"{n:D2}_shop_{Safe(shop.ShopName)}_front", t.TransformPoint(new Vector3(0f, 1.7f, shop.Building.Depth / 2f - 2.6f)), t.TransformPoint(new Vector3(0f, 0.7f, -shop.Building.Depth / 2f + 0.5f)), "The same shop toward its door.", 70f);
                n++;
            }

            // The tavern (a hall open to the street): from its doorway, looking in.
            var tavern = town.FirstOrDefault(x => x.Function == BuildingFunction.Tavern);
            if (tavern != null)
            {
                var rot = Quaternion.Euler(0f, -tavern.Yaw, 0f);
                var ctr = new Vector3(tavern.Center.X, 0f, tavern.Center.Z);
                yield return FreeShot($"{n:D2}_tavern", ctr + rot * new Vector3(0f, 1.6f, -tavern.Depth / 2f + 0.8f), ctr + rot * new Vector3(0f, 1.0f, tavern.Depth / 2f - 1f), $"Inside the tavern ({tavern.Kind}, fit {tavern.Fit}).", 70f);
                n++;
                yield return FreeShot($"{n:D2}_tavern_outside", ctr + rot * new Vector3(0f, 2.2f, -tavern.Depth / 2f - 9f), ctr + Vector3.up * 2f, "The tavern from the street.", 55f);
                n++;
            }

            // An entered home and an entered workshop (their generated interiors).
            foreach (var fn in new[] { BuildingFunction.Home, BuildingFunction.LargeHome, BuildingFunction.Cottage, BuildingFunction.Workshop, BuildingFunction.Religious, BuildingFunction.GuardPost, BuildingFunction.Warehouse })
            {
                var b = town.FirstOrDefault(x => x.Function == fn && x.Kind != BuildingKind.Hall && x.Fit == ShopFit.None);
                if (b == null) { _log.Add($"No enterable {fn} in this town."); continue; }
                var r = c.EnterBuilding(b.Key);
                yield return Settle(10);
                if (!r.IsSuccess) { _log.Add($"Could not enter the {fn}: {r.Message}"); continue; }
                var p = c.Player.transform.position;
                var fwd = c.Player.transform.forward;
                var body = c.Player.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled).ToList();
                foreach (var r0 in body) r0.enabled = false; // the camera stands where the player does
                yield return FreeShot($"{n:D2}_inside_{fn}", p + Vector3.up * 1.75f - fwd * 0.6f, p + fwd * 4f + Vector3.up * 0.9f, $"Inside an entered {fn}: {c.Player.transform.position}.", 78f);
                yield return FreeShot($"{n:D2}_inside_{fn}_back", p + Vector3.up * 1.75f + fwd * 2.5f, p - fwd * 1f + Vector3.up * 0.9f, $"The same room looking back toward the door.", 78f);
                foreach (var r0 in body) r0.enabled = true;
                n++;
                c.LeavePlace();
                yield return Settle(10);
            }

            // Dusk and night in the same street.
            var now = Bootstrap.Session.GetWorldTime().MinuteOfDay;
            c.WaitHour((19 * 60 + 30 - now + 24 * 60) % (24 * 60));
            yield return Settle(10);
            yield return FreeShot($"{n:D2}_street_dusk", new Vector3(layout.ArrivalPose.X, 1.7f, layout.ArrivalPose.Z), new Vector3(0f, 1.5f, 0f), "The same street at dusk.");
            n++;
            c.WaitHour(3 * 60);
            yield return Settle(10);
            yield return FreeShot($"{n:D2}_street_night", new Vector3(layout.ArrivalPose.X, 1.7f, layout.ArrivalPose.Z), new Vector3(0f, 1.5f, 0f), "The same street at night.");
            n++;
            var smithy = c.Shops.Values.FirstOrDefault(s => s.Building.Fit == ShopFit.Smithy);
            if (smithy != null)
            {
                var t = smithy.transform;
                yield return FreeShot($"{n:D2}_smithy_night", t.TransformPoint(new Vector3(0f, 1.6f, -smithy.Building.Depth / 2f + 0.8f)), t.TransformPoint(new Vector3(-smithy.Building.Width / 4f, 1.0f, 0f)), "The smithy at night.", 62f);
            }
        }

        private static string Safe(string s) => new string((s ?? "shop").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        /// <summary>Renders the scene from a free camera (no interface), at 1920×1080.</summary>
        private IEnumerator FreeShot(string name, Vector3 eye, Vector3 look, string note, float fov = 55f)
        {
            var go = new GameObject("walkthrough camera");
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
            File.WriteAllBytes(Path.Combine(_dir, $"{_prefix}_{name}.jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            Object.Destroy(rt);
            Object.Destroy(go);
            _log.Add($"{_prefix}_{name}: {note}");
        }
    }
}
