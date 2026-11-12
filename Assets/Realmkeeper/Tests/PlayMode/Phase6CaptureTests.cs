using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Evidence capture for Phase 6, not a pass/fail test: the character creator's steps and the Player Hub's
    /// character sheet in the shipped scene, driven by clicking the real buttons. Runs only when RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase6CaptureTests
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
            File.WriteAllLines(Path.Combine(_dir, "p6_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static void Click(VisualElement root, string name)
        {
            var b = root.Q<Button>(name);
            Assert.That(b, Is.Not.Null, name);
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = b;
                b.SendEvent(click);
            }
        }

        [UnityTest]
        public IEnumerator CapturePhase6Set()
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
            var camera = controller.CameraRig.Camera;
            controller.Player.Teleport(new LocalPose(1.5f, 0.1f, -controller.Streamer.Current.HalfExtent + 9f, 0f));
            controller.CameraRig.SetView(0f, 6f);
            controller.CameraRig.Snap();
            yield return Settle(10);
            yield return Shoot(camera, "p6_01_hud_create_prompt", "A new world's player: the HUD offers 'Create character (C)'.");

            Click(controller.Hud.Root, "xk-open-creator");
            var root = controller.Creator.Root;
            root.Q<TextField>("xk-cc-name").value = "Ilsa Marrow";
            root.Q<TextField>("xk-cc-title").value = "of the Hollow Shrine";
            root.Q<TextField>("xk-cc-history").value = "Raised at a hill shrine; left when its keeper died and the shrine's books went missing.";
            controller.Creator.Render();
            yield return Settle(4);
            yield return Shoot(camera, "p6_02_creator_identity", "Step 1, Identity.");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-frame-1");
            Click(root, "xk-cc-hair-colour-4");
            yield return Settle(4);
            controller.Preview.RenderNow();
            yield return Shoot(camera, "p6_03_creator_appearance", "Step 2, Appearance (female frame, dark hair) with the live preview.");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-species-dwarf");
            yield return Settle(4);
            controller.Preview.RenderNow();
            yield return Shoot(camera, "p6_04_creator_species_dwarf", "Step 3, Species: Dwarf, traits, and the honest placeholder notice (human body at dwarf height).");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-background-acolyte");
            yield return Settle(2);
            yield return Shoot(camera, "p6_05_creator_background", "Step 4, Background: Acolyte (SRD 5.2.1).");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-class-cleric");
            Click(root, "xk-cc-class-divine_order-protector");
            yield return Settle(4);
            controller.Preview.RenderNow();
            yield return Shoot(camera, "p6_06_creator_class", "Step 5, Class: Cleric (d8, saves, armour, spellcasting, Divine Order: Protector); the preview wears the cleric's kit.");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-method-1");
            foreach (var (ability, n) in new[] { ("Wisdom", 7), ("Constitution", 6), ("Strength", 5), ("Dexterity", 4), ("Charisma", 2) })
                for (var i = 0; i < n; i++) Click(root, "xk-cc-plus-" + ability);
            yield return Settle(2);
            yield return Shoot(camera, "p6_07_creator_abilities_pointbuy", $"Step 6, Abilities by point buy: {controller.Creator.Draft.PointsLeft} points left; species bonuses and modifiers shown.");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-skillpick-Medicine");
            Click(root, "xk-cc-skillpick-History");
            yield return Settle(2);
            yield return Shoot(camera, "p6_08_creator_skills", "Step 7, Skills: Insight and Religion granted by Acolyte; Medicine and History chosen.");

            Click(root, "xk-cc-next");
            Click(root, "xk-cc-kit-armour-leather");
            yield return Settle(4);
            controller.Preview.RenderNow();
            yield return Shoot(camera, "p6_09_creator_equipment", "Step 8, Starting equipment: options per group, and the background's kit and coin.");

            Click(root, "xk-cc-next");
            yield return Settle(2);
            yield return Shoot(camera, "p6_10_creator_review", "Step 9, Review: everything, ready to create.");

            Click(root, "xk-cc-create");
            yield return Settle(10);
            var c = session.GetCharacter();
            _log.Add($"Created: {c.FullName}, {c.Rules.Species.Name} {c.Rules.Background.Name} {c.Rules.Class.Name} 1; HP {c.Rules.MaxHp}; AC {c.ArmorClass.Total}.");
            controller.Player.Teleport(new LocalPose(1.5f, 0.1f, -controller.Streamer.Current.HalfExtent + 9f, 180f));
            controller.CameraRig.SetView(0f, 8f);
            controller.CameraRig.Snap();
            yield return Settle(20);
            yield return Shoot(camera, "p6_11_created_character_in_world", "The created character in the street: placeholder body at dwarf height, leather jerkin, mace and shield.");

            controller.ToggleHub();
            controller.Preview.RenderNow();
            yield return Settle(4);
            yield return Shoot(camera, "p6_12_hub_sheet", "Player Hub, Character: the sheet (HP, AC, speed, proficiency, abilities and saves).");
            controller.HubWindow.SelectCharacterPanel("Skills");
            yield return Settle(2);
            yield return Shoot(camera, "p6_13_hub_skills", "Character sheet, Skills: each bonus explained with its sources.");
            controller.HubWindow.SelectCharacterPanel("Features");
            yield return Settle(2);
            yield return Shoot(camera, "p6_14_hub_features", "Character sheet, Features and resources.");
            controller.HubWindow.SelectCharacterPanel("Proficiencies");
            yield return Settle(2);
            yield return Shoot(camera, "p6_15_hub_proficiencies", "Character sheet, Proficiencies, languages, senses, resistances.");
            controller.HubWindow.Select("Equipment");
            yield return Settle(2);
            yield return Shoot(camera, "p6_16_hub_equipment", "Player Hub, Equipment: the starting kit worn.");
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
