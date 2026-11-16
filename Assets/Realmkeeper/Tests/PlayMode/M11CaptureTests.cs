using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Hosting;
using Realmkeeper.Infrastructure.Persistence;
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
    /// M1.1 evidence capture (not pass/fail): the release menu and the New Game, Load Game and Save Game screens in the
    /// shipped scene, at the Phase 25 sizes (1024×768 to 3440×1440). Saves go to a temporary folder: a manual save, an
    /// autosave, a save from before v28 and a damaged save with its backup, so every slot state is shown. Runs only when
    /// RK_CAPTURE_DIR is set; writes JPEG files and m11_capture_log.txt.
    /// </summary>
    public sealed class M11CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private string _saves;
        private InputSettings.BackgroundBehavior _background;
        private bool _motion;
        private static readonly List<string> Log = new List<string>();
        private static readonly (int, int)[] All = { (1920, 1080), (1366, 768), (1280, 720), (1024, 768), (2560, 1440), (3440, 1440) };

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _saves = Path.Combine(Path.GetTempPath(), "realmkeeper-m11-capture-" + Guid.NewGuid().ToString("N"));
            RealmkeeperRuntime.SaveRootOverride = _saves;
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _motion = RkSettings.ReducedMotion;
            RkSettings.ReducedMotion = true;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "m11_capture_log.txt"), Log);
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
            RealmkeeperRuntime.SaveRootOverride = null;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            yield return null;
            if (Directory.Exists(_saves)) Directory.Delete(_saves, true);
        }

        [UnityTest]
        public IEnumerator Capture_M11_Screens()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var rt = b.Runtime;
            for (var i = 0; i < 10; i++) yield return null;
            var map = b.CameraController.Camera;
            var store = new JsonWorldStore(_saves);

            b.Title.Open(true);
            yield return Sizes("m11_01_main_menu_empty", "Launch menu with no saves: Continue disabled with its reason; Developer entries (editor and development builds).", map, (1920, 1080), (1024, 768));

            rt.SelectSlot("slot-1");
            rt.Save("slot-1");
            rt.Save("slot-1"); // a backup exists
            rt.ResetSaveClock();
            b.Saves.TryAutosave("capture");
            rt.Save("slot-3");
            rt.Save("slot-3");
            var damaged = File.ReadAllText(store.PathFor("slot-3"));
            File.WriteAllText(store.PathFor("slot-3"), damaged.Substring(0, damaged.Length / 2));
            var old = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(store.PathFor("slot-1")));
            old.Remove("summary");
            old.Remove("integrity");
            old["schemaVersion"] = 27;
            ((Newtonsoft.Json.Linq.JObject)old["world"]).Remove("playtimeSeconds");
            File.WriteAllText(store.PathFor("my-old-world"), old.ToString());

            b.Title.Open(true);
            yield return Sizes("m11_02_main_menu", "Launch menu: Continue, New Game, Load Game, Settings, Credits, Exit Game.", map, All);
            b.Title.ShowPage("load");
            yield return Sizes("m11_03_load_game", "Load Game: three slots (one Damaged with its backup offered), the autosaves, and older saves in development builds.", map, All);
            b.Title.ShowPage("newgame");
            yield return Sizes("m11_04_new_game", "New Game: choose a slot; an occupied one says Replace and asks first.", map, (1920, 1080), (1024, 768), (3440, 1440));
            b.Title.Open(false);
            b.Title.ShowPage("save");
            yield return Sizes("m11_05_save_game", "Save Game in a game: the three manual slots.", map, (1920, 1080), (1024, 768), (3440, 1440));
            b.Title.ShowPage(null);
            yield return Sizes("m11_06_in_game_menu", "The menu in a game: Resume, Save Game, Load Game.", map, (1920, 1080));
            b.Title.Hide();
        }

        private IEnumerator Sizes(string name, string note, Camera camera, params (int w, int h)[] sizes)
        {
            foreach (var (w, h) in sizes)
                yield return Shoot(w == 1920 && h == 1080 ? name : $"{name}_{w}x{h}", note, camera, w, h);
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
                x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height);
            }

            for (var i = 0; i < 4; i++) yield return null;
            if (camera != null && camera.isActiveAndEnabled)
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
            var classes = string.Join(", ", docs.Where(d => d.rootVisualElement != null).Select(d => d.name + "=" + RkRoot.WidthClass(d.rootVisualElement)).Distinct());
            foreach (var (x, target, clear, scale) in previous)
            {
                x.targetTexture = target;
                x.clearColor = clear;
                x.scale = scale;
            }

            rt.Release();
            Object.Destroy(rt);
            for (var i = 0; i < 2; i++) yield return null;
            Log.Add($"{name}: {width}x{height}, interface scale {RkSettings.EffectiveFor(RkSettings.UiScaleChoice, height):0.00}, width classes [{classes}]. {note}");
        }
    }
}
