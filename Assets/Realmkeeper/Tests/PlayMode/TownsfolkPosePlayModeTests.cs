using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
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
    /// Owner report (2026-11-16): in a town, townsfolk "dancing or crouching and standing up repeatedly". The cause was the
    /// standing-about sway's phase (the key's hash × 0.001, up to about two million). At that size a float moves in steps of
    /// about 0.1, so the figure stood frozen and then jumped a few centimetres in one frame. Each jump read as a burst of
    /// speed, which made a kneeling worker stand up and kneel again and made people standing about twitch.
    /// </summary>
    public sealed class TownsfolkPosePlayModeTests
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

        [Test]
        public void TheStandingSway_IsSmooth_AndSlow_ForEveryKey_AtAnyHour()
        {
            const float dt = 1f / 60f;
            var worst = 0f;
            for (var i = 0; i < 400; i++)
            {
                var key = i % 2 == 0 ? "tf-" + i : "watch-post-" + i;
                foreach (var start in new[] { 0f, 600f, 3500f })
                {
                    var (px, pz) = ExplorationController.StandingSway(key, 2.5f, start);
                    for (var f = 1; f <= 120; f++)
                    {
                        var (x, z) = ExplorationController.StandingSway(key, 2.5f, start + f * dt);
                        worst = Mathf.Max(worst, Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)) / dt);
                        (px, pz) = (x, z);
                    }
                }
            }

            Assert.That(worst, Is.LessThan(0.3f), $"A standing figure drifts below the animator's 0.3 m/s pose threshold every frame (worst {worst:0.000} m/s).");
        }

        [UnityTest]
        public IEnumerator InTown_StandingTownsfolk_HoldTheirPoses_WithoutBurstsOfSpeed()
        {
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            for (var i = 0; i < 8; i++) yield return null;
            yield return CombatDriver.EnterTownAsFighter(b, runtime, c);
            var now = b.Session.GetWorldTime().MinuteOfDay;
            var to = (12 * 60 - now + 24 * 60) % (24 * 60);
            if (to > 0) c.WaitHour(to);
            var layout = c.Streamer.Current;
            var z0 = -(layout.PlazaRadius + 4f);
            c.Player.Teleport(new LocalPose(0f, LocalPaths.StandingHeight(layout, 0f, z0) + 0.3f, z0, 0f));
            var t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 7f) yield return null; // the street fills, two figures at a time

            var specs = c.Townsfolk.ToDictionary(s => s.Key);
            var standing = new Dictionary<Transform, (TownsfolkSpec spec, Animator anim, Vector3 last)>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Townsperson ", StringComparison.Ordinal) || !t.gameObject.activeInHierarchy) continue;
                if (!specs.TryGetValue(t.name.Substring("Townsperson ".Length), out var s)) continue;
                if (s.Route.Count != 1 || s.Activity == TownsfolkActivity.Playing) continue; // people standing about, not walking or playing
                var a = t.GetComponentInChildren<Animator>();
                if (a != null) standing[t] = (s, a, t.position);
            }

            Assume.That(standing.Count, Is.GreaterThan(0), "Someone stands about near the square at noon.");
            var flips = new List<string>();
            var fastest = 0f;
            for (var f = 0; f < 240; f++)
            {
                yield return null;
                foreach (var t in standing.Keys.ToList())
                {
                    if (t == null || !t.gameObject.activeInHierarchy) { standing.Remove(t); continue; }
                    var (s, a, last) = standing[t];
                    var step = t.position - last;
                    step.y = 0f;
                    if (Time.deltaTime > 0f) fastest = Mathf.Max(fastest, step.magnitude / Time.deltaTime);
                    standing[t] = (s, a, t.position);
                    var pose = s.Activity == TownsfolkActivity.Working ? 1 : s.Activity == TownsfolkActivity.Gathering ? 3 : 0;
                    if (a.GetInteger("Activity") != pose && !flips.Contains(t.name)) flips.Add($"{t.name} ({s.Activity})");
                }
            }

            Debug.Log($"Townsfolk poses: {standing.Count} standing figures watched for 240 frames; fastest drift {fastest:0.000} m/s; pose changes {flips.Count}.");
            Assert.That(flips, Is.Empty, "Standing townsfolk keep their pose (no standing up and kneeling again).");
            Assert.That(fastest, Is.LessThan(0.3f), "No burst of speed from a standing figure.");
        }
    }
}
