using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Infrastructure.Persistence;
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
    /// Evidence capture for Phase 7, not a pass/fail test: a Phase 6 warlock (save rewritten to schema v8) earns
    /// experience and goes through levels 2–4 in the real level-up window — the open level-1 invocation, invocations
    /// with prerequisites, the subclass, a feat and its points — then the Player Hub's progression, features and
    /// sheet. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase7CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p7_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CapturePhase7Set()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 10; i++) yield return null;
            runtime.Playback.Pause();

            var choices = LevelUpDriver.Choices("warlock", "tiefling");
            choices.Name = "Corvin Ashe";
            choices.Appearance.BeardId = null;
            Assert.That(session.Dispatch(new CreateCharacterCommand(choices)).IsSuccess, Is.True);
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder, runtime.ActiveSlot.Name + JsonWorldStore.Extension);
            LevelUpDriver.RewriteAsPhaseSix(path);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return Settle(6);
            _log.Add($"Loaded a Phase 6 (schema v8) save: {session.GetCharacter().FullName}, Tiefling Warlock 1, rules version {session.GetCharacter().Player.Rpg.RulesVersion}; open choices: " +
                     string.Join(", ", session.GetCharacter().Outstanding.Select(o => o.Choice.Name)) + ".");
            Assert.That(session.Dispatch(new AwardExperienceCommand(400, ExperienceSource.Quest, "the-drowned-bell", "Rang the drowned bell")).IsSuccess, Is.True);
            Assert.That(session.Dispatch(new AwardExperienceCommand(2400, ExperienceSource.Discovery, "barrow-of-reeds", "Found the Barrow of Reeds")).IsSuccess, Is.True);
            controller.Player.Teleport(new LocalPose(1.5f, 0.1f, -controller.Streamer.Current.HalfExtent + 9f, 180f));
            controller.CameraRig.SetView(0f, 8f);
            controller.CameraRig.Snap();
            yield return Settle(20);
            var camera = controller.CameraRig.Camera; // after the reload rebuilt the scene
            yield return Shoot(camera, "p7_01_hud_level_up", "2,800 experience awarded (a quest and a discovery): the HUD offers 'Level up (L)'. Experience alone did not level the character.");

            controller.ToggleHub();
            controller.Preview?.RenderNow();
            yield return Settle(4);
            yield return Shoot(camera, "p7_02_hub_eligible", "The Hub's sheet: experience bar and the level-up banner (three levels available, taken one at a time).");

            LevelUpDriver.Click(controller.HubWindow.Content, "xk-hub-levelup");
            yield return Settle(2);
            var shots = new Dictionary<string, (string file, string note)>
            {
                ["2:" + AdvancementDraft.OverviewTitle] = ("p7_03_level2_overview", "Level 2 overview: hit points, new features, resources, and the open level-1 choice from Phase 6."),
                ["2:class:invocations"] = ("p7_04_level2_open_invocation", "The level-1 invocation Phase 6 left open, made now (only invocations without prerequisites)."),
                ["2:lv2:invocations"] = ("p7_05_level2_invocations", "Level 2 invocations: unavailable ones say why (level, or a pact they need)."),
                ["2:" + AdvancementDraft.ReviewTitle] = ("p7_06_level2_review", "Level 2 review before confirming."),
                ["3:lv3:subclass"] = ("p7_07_level3_subclass", "Level 3: the Otherworldly Patron (the SRD's Fiend Patron; others deferred) with its features by level."),
                ["4:lv4:feat"] = ("p7_08_level4_feat", "Level 4: Ability Score Improvement or a feat, with prerequisites and categories."),
                ["4:lv4:feat:abilities"] = ("p7_09_level4_ability_points", "The feat's own page: two points, each capped at 20."),
                ["4:" + AdvancementDraft.ReviewTitle] = ("p7_10_level4_review", "Level 4 review: Charisma 17 → 19, the new features, resources."),
            };
            for (var level = 2; level <= 4; level++)
            {
                var at = level;
                yield return LevelUpDriver.ThroughOneLevel(controller, session, page =>
                {
                    var key = at + ":" + (page.Choice != null ? page.Choice.Value.Choice.Id : page.Title);
                    return shots.TryGetValue(key, out var shot) ? Shoot(camera, shot.file, shot.note) : Settle(0);
                });
                yield return Settle(2);
            }

            var c = session.GetCharacter();
            _log.Add($"Advanced: {c.FullName}, Warlock {c.Level} ({c.Rules.Subclass?.Name}); HP {c.Rules.MaxHp}; invocations " +
                     string.Join(", ", c.Rules.ChoiceSlots.Where(s => s.Choice.Pool == "invocation").SelectMany(s => c.Player.Rpg.Chosen(s.Choice.Id))) + ".");
            controller.Preview?.RenderNow();
            yield return Settle(4);
            yield return Shoot(camera, "p7_11_hub_progression", "Hub, Character › Progression: levels gained with their summaries, the experience ledger, and the levels ahead.");
            controller.HubWindow.SelectCharacterPanel("Features");
            yield return Settle(2);
            yield return Shoot(camera, "p7_12_hub_features", "Features (with what each waits for), the class table and resources (pact slots, Magical Cunning, hit dice).");
            controller.HubWindow.SelectCharacterPanel("Sheet");
            yield return Settle(2);
            yield return Shoot(camera, "p7_13_hub_sheet", "The sheet at level 4: hit points, hit dice, initiative and speed breakdowns, scores with their improvements.");
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
