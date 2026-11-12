using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// Evidence capture for Phase 8, not a pass/fail test: the settlement inspector's structures on the strategic map,
    /// a structure sign in the street, then a fighter's practice fight played through the encounter panel (start,
    /// reasons for unavailable actions, an attack explained, inspecting a bandit, the AI's turn, the end), and the
    /// Hub's credits. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase8CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;
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
            File.WriteAllLines(Path.Combine(_dir, "p8_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CapturePhase8Set()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

            // 1. The strategic map: the town's inspector lists its structures.
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            yield return Settle(6);
            var box = bootstrap.Hud.SettlementStructures;
            box?.parent?.parent?.schedule.Execute(() => { });
            var scroll = box?.GetFirstAncestorOfType<ScrollView>();
            if (scroll != null) scroll.scrollOffset = new Vector2(0, box.layout.yMin - 40f);
            yield return Settle(4);
            var structures = session.GetStructures(town.SettlementId);
            yield return Shoot(Camera.main, "p8_01_inspector_structures",
                $"{town.Name}: the settlement inspector's Structures section — {structures.Count} structures, functional ones built on existing places and shops, informational and decorative ones marked as such.");

            // 2. In the street: a structure sign above the smithy's door.
            yield return CombatDriver.EnterTownAsFighter(bootstrap, runtime, controller);
            controller.AutoAi = false;
            var layout = controller.Streamer.Current;
            var smithy = structures.First(s => s.Kind == "Smithy");
            var b = LocalPaths.Find(layout, smithy.LayoutKey);
            if (b != null)
            {
                var door = LocalPaths.DoorOutside(b);
                var outYaw = LocalPaths.FacingOut(b);
                var dir = new Vector3(Mathf.Sin(outYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(outYaw * Mathf.Deg2Rad));
                var stand = new Vector3(door.X, 0.1f, door.Z) + dir * 6f;
                controller.Player.Teleport(new LocalPose(stand.x, 0.1f, stand.z, outYaw + 180f));
                controller.CameraRig.SetView(outYaw + 180f, 4f);
                controller.CameraRig.Snap();
            }

            yield return Settle(20);
            var camera = controller.CameraRig.Camera;
            yield return Shoot(camera, "p8_02_structure_sign", $"A placeholder sign above the door of {smithy.Name} (a functional structure: the Smithy shop and the smith's workplace).");

            // 3. The practice fight, from the HUD.
            controller.Player.Teleport(new LocalPose(1.5f, 0.1f, -layout.HalfExtent + 9f, 0f));
            controller.CameraRig.SetView(0f, 28f);
            controller.CameraRig.Snap();
            yield return Settle(10);
            LevelUpDriver.Click(controller.Hud.Root, "xk-open-practice");
            yield return Settle(6);
            var w = controller.Combat;
            _log.Add("Initiative: " + string.Join(", ", w.Snapshot.Order.Select(c => $"{c.Name} {c.Initiative}")));
            yield return Shoot(camera, "p8_03_fight_start",
                "The practice fight begins: initiative order with HP, AC and state; the 5-ft tactical grid; ground markers in the street (placeholders: no bandit models exist); time is paused.");

            for (var guard = 0; guard < 12 && controller.InFight && !w.Snapshot.IsPlayersTurn; guard++)
            {
                LevelUpDriver.Click(w.Root, "xk-combat-continue");
                yield return Settle(2);
                if (guard == 0) yield return Shoot(camera, "p8_04_ai_turn", "A computer-controlled turn resolved by the engine (Continue): the result and the log explain each roll.");
            }

            if (controller.InFight)
            {
                yield return Shoot(camera, "p8_05_player_turn_reasons", "The player's turn: the action economy, every option, and why each unavailable one is unavailable (range, used action, not prone …).");
                var enemyIndex = w.Snapshot.Order.ToList().FindIndex(c => c.HostileToPlayer);
                LevelUpDriver.Click(w.Root, "xk-combat-order-" + enemyIndex);
                yield return Settle(2);
                yield return Shoot(camera, "p8_06_inspect_bandit", "Inspecting a bandit: AC 12 (leather), hit points, attacks with their bonuses and damage, scores (SRD 5.2.1 statistics).");
            }

            // Play until the player has attacked once (result explained), then to the end.
            var attacked = false;
            for (var step = 0; step < 400 && controller.InFight; step++)
            {
                var before = w.Snapshot;
                CombatDriver.Step(controller);
                yield return Settle(1);
                if (!attacked && before.IsPlayersTurn && w.Snapshot.Log.Skip(before.Log.Count).Any(l => l.Kind == CombatLogKind.Attack && l.ActorId == before.Player.Id))
                {
                    attacked = true;
                    yield return Settle(2);
                    yield return Shoot(camera, "p8_07_attack_result", "An attack explained: d20 (and both dice with advantage or disadvantage), bonus, total against AC, hit or miss, damage dice and modifiers, hit points before and after.");
                }
            }

            yield return Settle(4);
            var end = w.Snapshot;
            _log.Add($"Ended after {end.Round} rounds: {end.Outcome} {end.WinningSide}; player {session.GetCharacter().Player.Rpg.CurrentHp} HP" + (session.GetCharacter().VitalText != null ? ", " + session.GetCharacter().VitalText : string.Empty));
            yield return Shoot(camera, "p8_08_fight_over", "The fight is over: the outcome, the full log, and the state of everyone; time resumes when the panel closes.");
            LevelUpDriver.Click(w.Root, "xk-combat-close");
            yield return Settle(4);

            // 4. The Hub: vitals and the credits.
            controller.ToggleHub();
            controller.Preview?.RenderNow();
            yield return Settle(4);
            yield return Shoot(camera, "p8_09_hub_sheet_after_fight", "The sheet after the fight: hit points as the fight left them, armour class from the SRD armour rules.");
            controller.HubWindow.Select("Info");
            yield return Settle(3);
            yield return Shoot(camera, "p8_10_hub_credits", "The Hub's Info section: the SRD 5.2.1 and 5.1 attribution (CC BY 4.0) and the CC0 art credits, as in CREDITS.md.");
            controller.CloseWindows();
        }

        private static IEnumerator Settle(int frames)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        private IEnumerator Shoot(Camera camera, string name, string note)
        {
            _log.Add(name + ": " + note);
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
