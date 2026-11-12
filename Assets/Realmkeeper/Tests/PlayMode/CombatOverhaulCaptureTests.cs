using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
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
    /// Evidence capture for the combat overhaul, not a pass/fail test: a practice fight in the running game rendered at
    /// 1920×1080, 2560×1440 and 1366×768, with the share of the screen the combat interface covers measured from its
    /// laid-out elements. Runs only when RK_CAPTURE_DIR is set; RK_CAPTURE_PREFIX names the set ("before", "after").
    /// </summary>
    public sealed class CombatOverhaulCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private static readonly (int W, int H)[] Sizes = { (1920, 1080), (2560, 1440), (1366, 768) };
        private string _dir;
        private string _prefix;
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            _prefix = Environment.GetEnvironmentVariable("RK_CAPTURE_PREFIX") ?? "co";
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
            File.WriteAllLines(Path.Combine(_dir, _prefix + "_capture_log.txt"), Log);
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

        [UnityTest]
        public IEnumerator Capture_PracticeFight_AtThreeResolutions()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.AutoAi = false;
            c.MouseDrivesCombat = false; // the scripted hover, not wherever the real cursor happens to rest
            Assert.That(c.StartPracticeFight().IsSuccess, Is.True);
            yield return Settle(8);
            for (var i = 0; i < 10 && !c.Combat.Snapshot.IsPlayersTurn; i++) { c.RunAiTurn(); yield return Settle(2); }
            if (_prefix != "after")
            {
                foreach (var (w, h) in Sizes) yield return Shoot(c, $"{_prefix}_{w}x{h}_fight", w, h);
                yield break;
            }

            // After: hover a square a few steps away (the route, its cost, what is left), at each size.
            var s = Bootstrap.Session;
            var me = c.Combat.Snapshot.Player.Position;
            var forward = Vector3.ProjectOnPlane(c.CameraRig.Camera.transform.forward, Vector3.up).normalized; // spots in front of the camera, where the picture is
            var spot = Ahead(c, s, forward, 4.5f, p => p.Reachable);
            if (spot != null) c.PreviewCombatMove(c.SquareCentre(spot.Value));
            Log.Add("hover: " + c.Combat.HoverText.Replace('\n', '/'));
            foreach (var (w, h) in Sizes) yield return Shoot(c, $"{_prefix}_{w}x{h}_fight", w, h);

            // The attack group open above the bar.
            c.Combat.OpenGroupPopup("attacks");
            yield return Settle(2);
            yield return Shoot(c, $"{_prefix}_1920x1080_attack_group", 1920, 1080);
            c.Combat.CloseGroupPopup();

            // A move that needs Dash: the question before anything is spent.
            var far = Ahead(c, s, forward, 11f, p => p.HasRoute && p.NeedsDash && p.DashReaches);

            if (far != null)
            {
                c.PreviewCombatMove(c.SquareCentre(far.Value));
                c.CommitCombatMove();
                yield return Settle(2);
                Log.Add("dash question: " + c.Combat.ConfirmText);
                yield return Shoot(c, $"{_prefix}_1920x1080_dash_question", 1920, 1080);
                c.Combat.HideConfirm();
            }

            // Moving, then attacking: the body walks; the result shows in the world.
            var enemy = c.Combat.Snapshot.Order.Where(o => o.HostileToPlayer && o.State == VitalState.Conscious).OrderBy(o => o.Position.DistanceFeet(me)).FirstOrDefault();
            if (enemy != null && !enemy.Position.IsAdjacent(me))
            {
                GridPos? beside = null;
                for (var dx = -1; dx <= 1 && beside == null; dx++)
                    for (var dy = -1; dy <= 1 && beside == null; dy++)
                    {
                        var p = s.PlanPlayerMove(new GridPos(enemy.Position.X + dx, enemy.Position.Y + dy, me.Z));
                        if (p.Reachable) beside = p.Destination;
                    }

                if (beside != null)
                {
                    c.CombatMoveToWorld(c.SquareCentre(beside.Value));
                    yield return Settle(10);
                    yield return Shoot(c, $"{_prefix}_1920x1080_walking", 1920, 1080);
                    var until = Time.realtimeSinceStartup + 4f;
                    while (c.PlayerWalking && Time.realtimeSinceStartup < until) yield return null;
                }
            }

            var attack = c.Combat.Snapshot.PlayerOptions.ToList().FindIndex(o => o.Action is AttackAction && o.Unavailable == null);
            if (attack >= 0)
            {
                c.CombatAct(c.Combat.Snapshot.PlayerOptions[attack].Action);
                var wait = Time.realtimeSinceStartup + 0.45f;
                while (Time.realtimeSinceStartup < wait) yield return null;
                Log.Add("feedback: " + string.Join(", ", c.CombatFeedback));
                yield return Shoot(c, $"{_prefix}_1920x1080_attack_feedback", 1920, 1080);
            }

            c.Combat.ToggleGrid();
            LevelUpDriver.Click(c.Combat.Root, "xk-cbt-log-toggle");
            yield return Settle(2);
            yield return Shoot(c, $"{_prefix}_1920x1080_grid_and_log", 1920, 1080);
        }

        /// <summary>The square nearest a point ahead of the player (searched outwards) whose plan meets the condition.</summary>
        private static GridPos? Ahead(ExplorationController c, WorldSession s, Vector3 forward, float metres, Func<MovePlan, bool> ok)
        {
            var centre = c.SquareAt(c.Player.transform.position + forward * metres);
            for (var r = 0; r <= 4; r++)
                for (var dx = -r; dx <= r; dx++)
                    for (var dy = -r; dy <= r; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        var p = new GridPos(centre.X + dx, centre.Y + dy, centre.Z);
                        if (ok(s.PlanPlayerMove(p))) return p;
                    }

            return null;
        }

        private IEnumerator Shoot(ExplorationController c, string name, int width, int height)
        {
            var camera = c.CameraRig.Camera;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(s => (s, s.targetTexture, s.clearColor)).ToList();
            foreach (var s in settings)
            {
                s.targetTexture = rt;
                s.clearColor = false;
            }

            yield return null; // the panels lay out at the new size
            yield return null;
            var cameraTarget = camera.targetTexture;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = cameraTarget;
            yield return null;
            yield return null;

            // What the combat interface covers: the union area of its visible top-level parts, against the whole panel.
            var root = c.Hud.Root;
            var panel = root.worldBound;
            var parts = CombatParts(c).Where(e => e.resolvedStyle.display != DisplayStyle.None && e.worldBound.width > 1 && e.worldBound.height > 1).Select(e => e.worldBound).ToList();
            var covered = Union(parts, panel);
            var share = panel.width * panel.height > 0 ? covered / (panel.width * panel.height) : 0f;
            var biggest = parts.Count == 0 ? default : parts.OrderByDescending(r => r.width * r.height).First();

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
            Log.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}: {1}x{2}; panel {3:0}x{4:0} px; combat interface covers {5:0.0}% of the screen in {6} part(s); largest part {7:0}x{8:0} px ({9:0.0}% of the width, {10:0.0}% of the height)",
                name, width, height, panel.width, panel.height, share * 100f, parts.Count, biggest.width, biggest.height, panel.width > 0 ? biggest.width / panel.width * 100f : 0f, panel.height > 0 ? biggest.height / panel.height * 100f : 0f));
            yield return Settle(2);
        }

        /// <summary>The combat interface's top-level parts: the old panel, or the new HUD's pieces (whatever exists).</summary>
        private static IEnumerable<VisualElement> CombatParts(ExplorationController c)
        {
            var root = c.Hud.Root;
            foreach (var name in new[] { "xk-combat", "xk-cbt-top", "xk-cbt-bar", "xk-cbt-card", "xk-cbt-log", "xk-cbt-pop", "xk-cbt-grid-panel", "xk-cbt-hover", "xk-cbt-confirm" })
            {
                var e = root.Q<VisualElement>(name);
                if (e == null) continue;
                if (name == "xk-combat" && e.childCount > 0 && e.ClassListContains("xk-cbt")) continue; // the new HUD's container: count its pieces instead
                yield return e;
            }
        }

        /// <summary>Area of a union of rectangles (clipped to the panel), on a coarse 4-px grid — plenty for a percentage.</summary>
        private static float Union(List<Rect> rects, Rect within)
        {
            const float step = 4f;
            var count = 0;
            for (var y = within.yMin; y < within.yMax; y += step)
                for (var x = within.xMin; x < within.xMax; x += step)
                    foreach (var r in rects)
                        if (r.Contains(new Vector2(x, y))) { count++; break; }
            return count * step * step;
        }
    }
}
