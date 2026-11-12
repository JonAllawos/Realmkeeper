using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 19 evidence capture (not pass/fail): every major redesigned screen in the running game, rendered at
    /// 1920×1080 and 1366×768 (the interface scale follows the capture height, as Auto does on such a screen), and a
    /// few at 2560×1440. Runs only when RK_CAPTURE_DIR is set. JPEG files and a log are written.
    /// </summary>
    public sealed class Phase19CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const string CaptureSlot = "p19-capture";
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
            File.WriteAllLines(Path.Combine(_dir, "p19_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var store = new Realmkeeper.Infrastructure.Persistence.JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder));
            if (store.Exists(CaptureSlot)) store.Delete(CaptureSlot);
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
        public IEnumerator Capture_Phase19_Screens()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var b = Bootstrap;
            var rt = Runtime;
            yield return Settle(10);
            var map = b.CameraController.Camera;

            // The entry experience (a save exists so Continue and the world list have something to show).
            rt.SelectSlot(CaptureSlot);
            rt.Save();
            b.Title.Open(true);
            yield return Both("p19_01_title", "Title screen at launch: Continue (newest save), New world, Load world, Campaigns, Settings, Credits, Exit.", map);
            b.Title.ShowPage("worlds");
            yield return Both("p19_02_world_selection", "Saved worlds: world name, in-game date, seed, last saved, size, slot; Load and Delete (confirmed).", map);
            b.Title.ShowPage("new");
            yield return Both("p19_03_world_creation", "Found a new world: name, seed (with New seed), starting speed; the fixed facts stated.", map);
            b.Title.ShowPage("campaigns");
            yield return Shoot("p19_04_campaigns", "Campaigns in this world, with Begin (or why not).", map, 1920, 1080);
            b.Title.ShowPage("credits");
            yield return Shoot("p19_05_credits", "Credits: SRD notices, art, icons and fonts with their licences.", map, 1920, 1080);
            b.Title.ShowPage(null);
            var settings = new RkSettingsWindow(b.Title.Root.parent);
            yield return Both("p19_06_settings", "Settings: display mode, resolution, quality, vertical sync, interface scale, the controls, and what is not available yet.", map);
            settings.Close();
            b.Title.Hide();
            yield return Settle(4);

            // The strategic map with a settlement selected.
            var town = b.Session.GetSettlementsInView().First(s => s.SettlementType == Realmkeeper.Domain.World.SettlementType.Town);
            b.Selection.Select(town.SettlementId);
            yield return Settle(4);
            yield return Both("p19_07_strategic_map", $"Strategic map, '{town.Name}' selected: top bar with icons, Settings and Menu; navigation; the settlement inspector.", map);
            b.Hud.ToggleAtlas(true);
            yield return Settle(4);
            yield return Both("p19_08_world_atlas", "World atlas on parchment: drawn settlements and terrain, layers as chips, uncharted land as blank map.", map);
            yield return Shoot("p19_08_world_atlas_1440", "World atlas at 2560×1440.", map, 2560, 1440);
            b.Hud.ToggleAtlas(false);
            yield return Settle(2);

            // Into the town.
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(b, rt, c);
            yield return Settle(10);
            var cam = c.CameraRig.Camera;
            yield return Both("p19_09_exploration_hud", "Settlement exploration HUD.", cam);

            c.ToggleHub();
            yield return Settle(4);
            yield return Both("p19_10_character", "Character sheet (Sheet panel) with the 3D preview.", cam);
            c.HubWindow.SelectCharacterPanel("Skills");
            yield return Shoot("p19_11_character_skills", "Character: skills and proficiencies.", cam, 1920, 1080);
            foreach (var (section, file, note) in new[]
                     {
                         ("Inventory", "p19_12_inventory", "Inventory: groups, search, weight."),
                         ("Equipment", "p19_13_equipment", "Equipment slots."),
                         ("Crafting & work", "p19_14_crafting", "Crafting and work."),
                         ("Spells & rest", "p19_15_spells", "Spells and rest."),
                         ("Journal", "p19_16_journal", "Quest journal."),
                         ("Party", "p19_17_party", "Party."),
                     })
            {
                c.HubWindow.Select(section);
                yield return Settle(3);
                if (section == "Inventory" || section == "Journal") yield return Both(file, note, cam);
                else yield return Shoot(file, note, cam, 1920, 1080);
            }

            c.CloseWindows();
            yield return Settle(2);
            var npc = c.Residents.Keys.FirstOrDefault();
            if (!npc.IsEmpty && c.StartDialogue(npc).IsSuccess)
            {
                yield return Settle(4);
                yield return Both("p19_18_dialogue", "Dialogue with a resident.", cam);
                c.EndDialogue();
                yield return Settle(2);
            }

            if (c.StartPracticeFight().IsSuccess)
            {
                c.AutoAi = false;
                yield return Settle(10);
                yield return Both("p19_19_combat", "Combat HUD (practice fight).", cam);
                yield return Shoot("p19_19_combat_1440", "Combat HUD at 2560×1440.", cam, 2560, 1440);
            }
        }

        private IEnumerator Both(string name, string note, Camera camera)
        {
            yield return Shoot(name, note, camera, 1920, 1080);
            yield return Shoot(name + "_768", note + " (1366×768)", camera, 1366, 768);
        }

        private IEnumerator Shoot(string name, string note, Camera camera, int width, int height)
        {
            yield return null;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var panels = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
            foreach (var x in panels)
            {
                x.targetTexture = rt;
                x.clearColor = false;
                x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height); // what Auto gives on a screen this tall
            }

            yield return null;
            yield return null;
            if (camera != null)
            {
                var cameraTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = cameraTarget;
            }

            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".jpg"), tex.EncodeToJPG(88));
            Object.Destroy(tex);
            foreach (var (x, target, clear, scale) in previous)
            {
                x.targetTexture = target;
                x.clearColor = clear;
                x.scale = scale;
            }

            rt.Release();
            Object.Destroy(rt);
            Log.Add($"{name}: {width}x{height}, interface scale {RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height):0.00}. {note}");
        }
    }
}
