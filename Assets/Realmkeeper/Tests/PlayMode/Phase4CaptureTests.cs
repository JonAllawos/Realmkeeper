using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
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
    /// Evidence capture for Phase 4, not a pass/fail test: screenshots of the shipped scene with its
    /// UI (the HUD panel is rendered into the same texture as the camera). People list and NPC
    /// inspector, its knowledge and DM tools, the settlement's residents, and a conversation in
    /// exploration. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase4CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;
        private string _dir;
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            // (Ignoring is decided in the test body: Assert.Ignore inside a UnitySetUp coroutine can stall the runner.)
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
            if (string.IsNullOrEmpty(_dir)) yield break; // nothing was loaded: never touch the runner's scene
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CapturePhase4Set()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var installer = Object.FindAnyObjectByType<ExplorationInstaller>();
            var hud = bootstrap.Hud;
            var session = bootstrap.Session;
            // Let the world live a while, so people have memories, ties and hearsay.
            Assert.That(session.Execute(new AdvanceDaysCommand(45)).IsSuccess, Is.True);
            yield return null;

            var town = bootstrap.WorldView.Settlements.Values.Single(v => v.SettlementType == SettlementType.Town);
            bootstrap.CameraController.FocusOn(town.Center);
            for (var i = 0; i < 60; i++) yield return null;

            hud.ShowPeople(true);
            var notable = session.GetNpcs().OrderByDescending(n => session.GetNpcDetail(n.NpcId).Knowledge.Count)
                .First(n => n.Tier == NpcTier.Notable);
            hud.SelectNpc(notable.NpcId);
            yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p4_people_and_inspector");

            ScrollTo(hud.NpcInspectorBody as ScrollView, "ninsp-section-relationships");
            yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p4_inspector_relationships_memories");

            ScrollTo(hud.NpcInspectorBody as ScrollView, "ninsp-section-knowledge");
            yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p4_inspector_knowledge");

            hud.SetManagingNpc(true);
            yield return null;
            ScrollTo(hud.NpcInspectorBody as ScrollView, "ninsp-tools");
            yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p4_dm_tools");
            hud.SetManagingNpc(false);

            hud.ShowPeople(false);
            bootstrap.Selection.Select(town.SettlementId);
            yield return null;
            var body = hud.Root.Q<ScrollView>("insp-body");
            var residents = hud.SettlementResidents;
            if (body != null && residents != null) { yield return null; body.ScrollTo(residents); }
            yield return null;
            yield return Shoot(bootstrap.CameraController.Camera, "p4_settlement_residents");

            // Exploration: talk to someone where they stand today.
            var controller = installer.Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            Assert.That(hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 10; i++) yield return null;
            var view = controller.Residents.Values.First(r => r.Role != NpcRole.Innkeeper);
            foreach (var r in controller.Residents.Values) r.enabled = false;
            var p = view.transform.position;
            var dir = view.transform.forward;
            controller.Player.Teleport(new LocalPose(p.x + dir.x * 1.4f, 0.1f, p.z + dir.z * 1.4f, Quaternion.LookRotation(-dir).eulerAngles.y));
            controller.CameraRig.SetView(Quaternion.LookRotation(-dir).eulerAngles.y, 8f);
            for (var i = 0; i < 10; i++) yield return null;
            if (ReferenceEquals(controller.CurrentTarget, view))
            {
                controller.Interact();
                controller.AskResident(ConversationTopic.News);
            }

            for (var i = 0; i < 20; i++) yield return null;
            yield return Shoot(controller.CameraRig.Camera, "p4_exploration_conversation");
            controller.ReturnToWorldMap();
        }

        private static void ScrollTo(ScrollView scroll, string name)
        {
            var target = scroll?.Q(name);
            if (target != null) scroll.ScrollTo(target);
        }

        /// <summary>Renders the camera, then the UI panels on top, into one texture, and saves it.</summary>
        private IEnumerator Shoot(Camera camera, string name)
        {
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
            yield return null; // panels draw into the texture during the frame
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
