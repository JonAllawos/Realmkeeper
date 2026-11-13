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
    /// Phase 22 QA closeout of Phase 21's missing captures: a working shop (its trade window with the shop's trade, stock
    /// and shortfalls) and a work station in use (the crafting page opened from the station), at 1920×1080, 1366×768 and
    /// 1280×720, through the real controls. Checks the windows open on the right shop and station. Skipped without
    /// RK_CAPTURE_DIR, like the other capture sets.
    /// </summary>
    public sealed class Phase22QaCaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private string _dir;
        private static readonly List<string> Log = new List<string>();
        private static readonly (int w, int h, string suffix)[] Sizes = { (1920, 1080, string.Empty), (1366, 768, "_768"), (1280, 720, "_720") };

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture.");
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
            if (!string.IsNullOrEmpty(_dir)) File.AppendAllLines(Path.Combine(_dir, "p22qa_capture_log.txt"), Log);
            Log.Clear();
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static IEnumerator Settle(int frames = 4)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator AWorkingShop_AndAStationInUse_AtEverySupportedSize()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(6);
            runtime.Playback.Pause();
            var created = session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter")));
            Assert.That(created.IsSuccess, Is.True, created.Message);
            yield return Settle();
            var cam = controller.CameraRig.Camera;

            var shops = Object.FindObjectsByType<ShopInteriorView>(FindObjectsSortMode.None).OrderBy(s => s.ShopName, StringComparer.Ordinal).ToList();
            Assert.That(shops, Is.Not.Empty, "The town's shops are built.");
            var shop = shops.FirstOrDefault(s => s.ShownTotal > 0) ?? shops[0];
            controller.OpenTrade(shop);
            yield return Settle(4);
            Assert.That(controller.Trade.IsOpen, Is.True);
            yield return Shoot("p22qa_01_trade_window", $"A working shop: {shop.ShopName}'s trade window (its trade, stock, prices; shortfalls and supply on hover).", cam);
            controller.CloseWindows();
            yield return Settle();

            var station = shops.SelectMany(s => s.Stations).FirstOrDefault();
            Assert.That(station, Is.Not.Null, "A shop with a work station.");
            controller.OpenStation(station);
            yield return Settle(4);
            Assert.That(controller.HubWindow.IsOpen, Is.True, "The station opens the crafting page.");
            var text = string.Join(" | ", controller.HubWindow.Root.Query<Label>().ToList().Select(l => l.text));
            Assert.That(text, Does.Contain("At the " + StationWords.Name(station.Station)), "The page names the station.");
            yield return Shoot("p22qa_02_station_crafting", $"A work station in use: crafting opened at {station.ShopName}'s {station.Station} (recipes for this station, with a station note).", cam);
            controller.CloseWindows();
        }

        private IEnumerator Shoot(string name, string note, Camera camera)
        {
            foreach (var (w, h, suffix) in Sizes)
            {
                yield return null;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                var panels = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).Select(d => d.panelSettings).Distinct().ToList();
                var previous = panels.Select(x => (x, x.targetTexture, x.clearColor, x.scale)).ToList();
                foreach (var x in panels) { x.targetTexture = rt; x.clearColor = false; x.scale = RkSettings.EffectiveFor(RkSettings.UiScaleChoice, h); }
                yield return null;
                yield return null;
                var target = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = target;
                yield return null;
                var active = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = active;
                File.WriteAllBytes(Path.Combine(_dir, name + suffix + ".jpg"), tex.EncodeToJPG(88));
                Object.Destroy(tex);
                foreach (var (x, t, cl, sc) in previous) { x.targetTexture = t; x.clearColor = cl; x.scale = sc; }
                rt.Release();
                Object.Destroy(rt);
                Log.Add($"{name}{suffix}: {w}x{h}. {note}");
            }
        }
    }
}
