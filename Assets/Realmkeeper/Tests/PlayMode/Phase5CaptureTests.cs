using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.Simulation.Npcs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Evidence capture for Phase 5, not a pass/fail test: screenshots of the shipped scene with its UI,
    /// driven through the same commands and windows the player uses. Morning and night in the town,
    /// residents walking and working, a shop and its keeper, the trade window, a completed purchase and
    /// the reduced stock, the Player Hub (inventory, equipment) and the equipped character. Runs only when
    /// RK_CAPTURE_DIR is set. A log of what each shot shows is written beside the images.
    /// </summary>
    public sealed class Phase5CaptureTests
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
            Time.timeScale = 1f;
            File.WriteAllLines(Path.Combine(_dir, "p5_capture_log.txt"), _log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static void Click(Button b)
        {
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = b;
                b.SendEvent(click);
            }
        }

        [UnityTest]
        public IEnumerator CapturePhase5Set()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var session = bootstrap.Session;
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);

            // Morning, with the merchant open.
            for (var i = 0; i < 200 && !session.GetShops(town.SettlementId).First(s => s.Categories.Contains(ShopCategory.General)).IsOpen; i++)
                session.Execute(new AdvanceTimeCommand(30));
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            for (var i = 0; i < 10; i++) yield return null;
            runtime.Playback.Pause();
            var layout = controller.Streamer.Current;
            var camera = controller.CameraRig.Camera;
            _log.Add($"Town: {town.Name}. Clock {session.GetWorldTime().Text}.");

            // 1. Morning in the town: from the gate road up the main street.
            Place(controller, new Vector3(1.5f, 0.1f, -layout.HalfExtent + 9f), 0f, 6f);
            yield return Settle(20);
            yield return Shoot(camera, "p5_01_town_morning", $"{town.Name} at {session.GetWorldTime().Clock} ({session.GetWorldTime().DayPart}): sun elevation {controller.Atmosphere.SunElevation:F0}°, lamps off.");

            // 2–3. Residents walking and working: let a quarter hour pass at a time until someone sets off.
            ResidentView walker = null;
            for (var i = 0; i < 4 * 24 * 2 && walker == null; i++)
            {
                controller.WaitHour(15);
                yield return null;
                walker = controller.Residents.Values.FirstOrDefault(v => v.IsWalking && !v.IsHidden);
            }

            if (walker != null)
            {
                Time.timeScale = 4f;
                yield return new WaitForSeconds(8f);
                Time.timeScale = 1f;
                if (walker.IsWalking)
                {
                    var w = walker.transform.position;
                    var dir = walker.transform.forward;
                    Place(controller, w + dir * 6f + walker.transform.right * 1.5f, Quaternion.LookRotation(-dir).eulerAngles.y, 4f);
                    yield return Settle(6);
                    yield return Shoot(camera, "p5_02_resident_walking",
                        $"{walker.DisplayName} walking to {session.GetNpcPresence(walker.NpcId).LocationName} ({walker.ActivityReason}) at {session.GetWorldTime().Clock}.");
                }
            }

            // Someone at work out of doors (people working inside a shop are shown in the shop shots).
            bool Indoors(ResidentView v) => controller.Shops.Values.Any(s =>
            {
                var local = s.transform.InverseTransformPoint(v.transform.position);
                return Mathf.Abs(local.x) < s.Building.Width / 2f && Mathf.Abs(local.z) < s.Building.Depth / 2f;
            });
            var worker = controller.Residents.Values.FirstOrDefault(v => !v.IsHidden && !v.IsWalking && v.Activity == ActivityKind.Working && !Indoors(v))
                ?? controller.Residents.Values.FirstOrDefault(v => !v.IsHidden && !v.IsWalking && (v.Activity == ActivityKind.Working || v.Activity == ActivityKind.Guarding || v.Activity == ActivityKind.Trading) && !Indoors(v));
            if (worker != null)
            {
                var p = worker.transform.position;
                var from = p - worker.transform.forward * 3.4f + worker.transform.right * 2.4f;
                var look = p - worker.transform.right * 1.6f; // keep the worker clear of the player's own body
                Place(controller, from, Quaternion.LookRotation(look - from).eulerAngles.y, 8f);
                yield return Settle(10);
                var at = session.GetNpcPresence(worker.NpcId);
                yield return Shoot(camera, "p5_03_resident_working", $"{worker.DisplayName} ({worker.Role}) working at the {at.LocationName}: {at.Reason} Until {at.Until}.");
            }

            // Back to an hour when the merchant trades.
            for (var i = 0; i < 200 && !session.GetShops(town.SettlementId).First(s => s.Categories.Contains(ShopCategory.General)).IsOpen; i++)
                controller.WaitHour(30);
            var summary = session.GetShops(town.SettlementId).First(s => s.Categories.Contains(ShopCategory.General));
            var shop = controller.Shops.Values.Single(v => v.ShopId == summary.ShopId);
            Time.timeScale = 10f;
            yield return new WaitForSeconds(30f);
            Time.timeScale = 1f;

            // 4. The shop: its stocked shelves and the keeper behind the counter.
            var into = shop.transform.forward;
            var counter = shop.InteractionPoint;
            Place(controller, counter - into * 1.6f + shop.transform.right * 0.6f, Quaternion.LookRotation(into).eulerAngles.y - 10f, 12f);
            yield return Settle(10);
            yield return Shoot(camera, "p5_04_shop_interior",
                $"{summary.Name} ({summary.Hours}, {(summary.IsOpen ? "open" : "closed")}): {shop.ShownTotal} wares on the shelves = stock {session.GetShopDetail(summary.ShopId).ForSale.Sum(l => Math.Min(l.Quantity, ShopInteriorView.MaxShownPerItem))}.");

            // 5. Talking to the shopkeeper (from the side of the counter).
            var keeper = controller.Residents[summary.KeeperId];
            var k = keeper.transform.position;
            Place(controller, k + shop.transform.right * 1.3f, Quaternion.LookRotation(-shop.transform.right).eulerAngles.y, 8f);
            yield return Settle(6);
            if (ReferenceEquals(controller.CurrentTarget, keeper))
            {
                controller.Interact();
                controller.AskResident(ConversationTopic.Goods);
                yield return Settle(4);
                yield return Shoot(camera, "p5_05_shopkeeper_conversation", $"Asking {keeper.DisplayName} what is for sale; the Trade option opens the shop.");
            }

            // 6–8. The trade window, a purchase, the reduced stock.
            controller.OpenTrade(shop);
            var line = controller.Trade.Detail.ForSale.Where(l => l.Item.Category == ItemCategory.Clothing || l.Item.Category == ItemCategory.Headwear)
                .OrderBy(l => l.UnitPrice).FirstOrDefault() ?? controller.Trade.Detail.ForSale.OrderBy(l => l.UnitPrice).First();
            controller.Trade.Explain(line.ItemId);
            yield return Settle(2);
            yield return Shoot(camera, "p5_06_trade_window", $"Before: {line.Name} ×{line.Quantity} at {line.UnitPrice} coin. {controller.Trade.Explanation}");
            var purse = session.GetPlayerHub().Purse;
            Click(controller.Trade.Root.Q<VisualElement>("xk-trade-sale-" + line.ItemId).Q<Button>());
            controller.Trade.Explain(line.ItemId);
            yield return Settle(2);
            var stock = session.GetShopDetail(summary.ShopId).ForSale.FirstOrDefault(l => l.ItemId == line.ItemId)?.Quantity ?? 0;
            yield return Shoot(camera, "p5_07_purchase_complete",
                $"{controller.Trade.LastMessage} Purse {purse} → {session.GetPlayerHub().Purse}; stock {line.Quantity} → {stock}; ledger updated.");
            controller.CloseWindows();
            Place(controller, counter - into * 1.6f + shop.transform.right * 0.6f, Quaternion.LookRotation(into).eulerAngles.y - 10f, 12f);
            yield return Settle(6);
            yield return Shoot(camera, "p5_08_reduced_stock",
                $"After the purchase: {line.Name} on the shelves {shop.Shown.GetValueOrDefault(line.ItemId)} (stock {stock}); {shop.ShownTotal} wares in all.");

            // Arms from the smithy, worn.
            var smithy = controller.Shops.Values.First(v => v.Building.Fit == ShopFit.Smithy);
            controller.OpenTrade(smithy);
            foreach (var want in new[] { "sword_arming", "shield_round", "cap_iron" })
            {
                var l = controller.Trade.Detail?.ForSale.FirstOrDefault(x => x.ItemId == want);
                if (l != null && l.UnitPrice <= session.GetPlayerHub().Purse) controller.Buy(want, l.UnitPrice);
            }

            controller.CloseWindows();
            foreach (var item in session.GetPlayerHub().Inventory.Where(i => i.CanEquip && i.ItemId != "dagger_iron").ToList())
                session.Dispatch(new EquipItemCommand(item.StackId));

            // 9–10. The Player Hub: inventory, equipment (with the live preview).
            controller.ToggleHub();
            controller.HubWindow.Select("Inventory");
            yield return Settle(4);
            controller.Preview.RenderNow();
            yield return Shoot(camera, "p5_09_hub_inventory", $"Player Hub, Inventory: {session.GetPlayerHub().InventoryStacks} stacks, {session.GetPlayerHub().Purse} coin.");
            controller.HubWindow.Select("Equipment");
            controller.Preview.Rotate(-30f);
            controller.Preview.RenderNow();
            yield return Settle(4);
            var hub = session.GetPlayerHub();
            yield return Shoot(camera, "p5_10_hub_equipment",
                $"Player Hub, Equipment: {string.Join(", ", hub.Equipment.Select(e => e.Key + "=" + e.Value.Name))}; preview props {string.Join(", ", controller.Preview.Report.Props)}.");
            controller.HubWindow.Select("Weapons");
            controller.Preview.Zoom(1.4f);
            controller.Preview.RenderNow();
            yield return Settle(4);
            yield return Shoot(camera, "p5_11_hub_weapons_zoomed", "Player Hub, Weapons, preview zoomed.");
            controller.CloseWindows();

            // 12. The equipped character in the street.
            Place(controller, new Vector3(-1.5f, 0.1f, -8f), 200f, 8f);
            controller.CameraRig.SetView(20f, 8f);
            yield return Settle(20);
            yield return Shoot(camera, "p5_12_player_equipped", $"The character as equipped: {string.Join(", ", controller.Player.BodyReport.Shown.Select(s => s.Key + ":" + s.Value))}; {string.Join(", ", controller.Player.BodyReport.Props)}.");

            // 13–14. Evening and night.
            var minute = session.GetWorldTime().MinuteOfDay;
            controller.WaitHour(19 * 60 + 15 - minute > 0 ? 19 * 60 + 15 - minute : 1440 - minute + 19 * 60 + 15);
            Place(controller, new Vector3(1.5f, 0.1f, -layout.HalfExtent + 9f), 0f, 6f);
            yield return Settle(20);
            yield return Shoot(camera, "p5_13_town_golden_hour", $"{town.Name} at {session.GetWorldTime().Clock}: sun elevation {controller.Atmosphere.SunElevation:F0}°, lamps {controller.Atmosphere.LampLevel:P0}.");
            controller.WaitHour(22 * 60 + 40 - session.GetWorldTime().MinuteOfDay);
            yield return Settle(20);
            var awake = controller.Residents.Values.Count(v => !v.IsHidden);
            yield return Shoot(camera, "p5_14_town_night",
                $"{town.Name} at {session.GetWorldTime().Clock}: moonlight, {controller.Atmosphere.Lamps.Count(l => l.enabled)} lamps lit, windows glowing; {awake} residents out, the rest indoors.");
            Place(controller, new Vector3(0f, 0.1f, -layout.PlazaRadius - 6f), 0f, 4f);
            yield return Settle(10);
            yield return Shoot(camera, "p5_15_plaza_night", "The plaza at night.");
        }

        private static void Place(ExplorationController controller, Vector3 at, float yaw, float pitch)
        {
            controller.Player.Teleport(new LocalPose(at.x, at.y, at.z, yaw));
            controller.CameraRig.SetView(yaw, pitch);
            controller.CameraRig.Snap();
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
