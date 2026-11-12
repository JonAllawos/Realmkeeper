using System.Collections;
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
    /// Phase 5 in the shipped scene: a real purchase at a shop counter (stock on the shelves, the trade
    /// window, coin, goods, the keeper, saving and loading), the Player Hub and equipment on the body,
    /// the light and the residents following the clock (someone walking to work), and a shut shop
    /// whose keeper is away.
    /// </summary>
    public sealed class Phase5PlayModeTests
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
            Time.timeScale = 1f;
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
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

        private static ExplorationController Controller()
        {
            var controller = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            controller.ManageCursor = false;
            controller.SetInput(new ScriptedExplorationInput());
            return controller;
        }

        private static IEnumerator EnterTown(RealmkeeperBootstrap bootstrap)
        {
            var town = bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            bootstrap.Selection.Select(town.SettlementId);
            Assert.That(bootstrap.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return null;
            yield return null;
        }

        /// <summary>Lets time pass (in steps of at most half an hour) until the town's general merchant trades.</summary>
        private static ShopSummary OpenGeneralShop(WorldSession session, SettlementSummary town)
        {
            for (var i = 0; i < 4 * 48; i++)
            {
                var shop = session.GetShops(town.SettlementId).FirstOrDefault(s => s.Categories.Contains(ShopCategory.General));
                Assert.That(shop, Is.Not.Null, "The town has a general merchant.");
                if (shop.IsOpen) return shop;
                session.Execute(new AdvanceTimeCommand(30));
            }

            Assert.Fail("The general merchant never opened.");
            return null;
        }

        [UnityTest]
        public IEnumerator Purchase_AtTheCounter_MovesCoinAndGoods_EmptiesTheShelf_AndSurvivesSaveAndLoad()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var session = bootstrap.Session;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var controller = Controller();
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            var summary = OpenGeneralShop(session, town);
            yield return EnterTown(bootstrap);
            runtime.Playback.Pause(); // hold the clock: the shop stays open for the test
            Assert.That(controller.Shops.Count, Is.GreaterThanOrEqualTo(2), "The merchant's and the smithy's interiors are built.");
            var shop = controller.Shops.Values.Single(v => v.ShopId == summary.ShopId);
            Assert.That(shop.Building.Fit, Is.EqualTo(ShopFit.General));

            // The shelves hold what the shop holds.
            var detail = session.GetShopDetail(summary.ShopId);
            foreach (var line in detail.ForSale)
                Assert.That(shop.Shown.TryGetValue(line.ItemId, out var n) ? n : 0, Is.EqualTo(Mathf.Min(line.Quantity, ShopInteriorView.MaxShownPerItem)), line.ItemId);

            // The keeper stands behind the counter (they are at work this hour).
            var keeper = controller.Residents[summary.KeeperId];
            Assert.That(keeper.IsHidden, Is.False);
            var behind = LocalPaths.BehindCounter(shop.Building);
            Assert.That(Vector2.Distance(new Vector2(keeper.transform.localPosition.x, keeper.transform.localPosition.z), new Vector2(behind.X, behind.Z)), Is.LessThan(1.2f));

            // Walk up to the counter and open the trade window.
            var counter = shop.InteractionPoint;
            var into = (shop.transform.forward).normalized; // the shop's local +Z points from the door to the back
            var stand = counter - into * 0.8f;
            controller.Player.Teleport(new LocalPose(stand.x, 0.1f, stand.z, Quaternion.LookRotation(into).eulerAngles.y));
            controller.CameraRig.SetView(Quaternion.LookRotation(into).eulerAngles.y, 10f);
            for (var i = 0; i < 5; i++) yield return null;
            Assert.That(controller.CurrentTarget, Is.SameAs(shop), "The counter is the interaction target.");
            controller.Interact();
            yield return null;
            Assert.That(controller.Trade.IsOpen, Is.True);
            Assert.That(controller.Player.MovementEnabled, Is.False, "No walking while trading.");

            var before = session.GetPlayerHub();
            var line0 = controller.Trade.Detail.ForSale.Where(l => l.UnitPrice <= before.Purse).OrderBy(l => l.UnitPrice).First();
            var held = before.Inventory.Where(i => i.ItemId == line0.ItemId).Sum(i => i.Quantity);
            var shownBefore = shop.Shown.TryGetValue(line0.ItemId, out var sb) ? sb : 0;
            var buy = controller.Trade.Root.Q<VisualElement>("xk-trade-sale-" + line0.ItemId).Q<Button>();
            Click(buy);
            yield return null;

            var after = session.GetPlayerHub();
            Assert.That(after.Purse, Is.EqualTo(before.Purse - line0.UnitPrice), "Coin paid.");
            Assert.That(after.Inventory.Where(i => i.ItemId == line0.ItemId).Sum(i => i.Quantity), Is.EqualTo(held + 1), "Goods received.");
            var stockAfter = session.GetShopDetail(summary.ShopId).ForSale.FirstOrDefault(l => l.ItemId == line0.ItemId)?.Quantity ?? 0;
            Assert.That(stockAfter, Is.EqualTo(line0.Quantity - 1), "Stock fell.");
            Assert.That(controller.Trade.LastMessage, Does.StartWith("Bought"));
            Assert.That(shop.Shown.TryGetValue(line0.ItemId, out var sa) ? sa : 0, Is.EqualTo(Mathf.Min(stockAfter, ShopInteriorView.MaxShownPerItem)),
                "One fewer on the shelf.");
            Assert.That(shownBefore, Is.EqualTo(Mathf.Min(line0.Quantity, ShopInteriorView.MaxShownPerItem)));
            Assert.That(session.GetRecentEvents(1)[0].EventType, Is.EqualTo(HistoricalEventType.PlayerPurchase));

            // Save, load: the purchase holds, and the reloaded shop shows the reduced stock.
            controller.CloseWindows();
            Assert.That(controller.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            yield return null;
            yield return null;
            var loaded = session.GetPlayerHub();
            Assert.That(loaded.Purse, Is.EqualTo(after.Purse));
            Assert.That(loaded.Inventory.Where(i => i.ItemId == line0.ItemId).Sum(i => i.Quantity), Is.EqualTo(held + 1));
            Assert.That(session.GetShopDetail(summary.ShopId).ForSale.FirstOrDefault(l => l.ItemId == line0.ItemId)?.Quantity ?? 0, Is.EqualTo(stockAfter));
            var reloaded = controller.Shops.Values.Single(v => v.ShopId == summary.ShopId);
            Assert.That(reloaded.Shown.TryGetValue(line0.ItemId, out var sl) ? sl : 0, Is.EqualTo(Mathf.Min(stockAfter, ShopInteriorView.MaxShownPerItem)));
        }

        [UnityTest]
        public IEnumerator PlayerHub_HasEightSections_AndEquipmentChangesTheBody()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var session = bootstrap.Session;
            var controller = Controller();
            yield return EnterTown(bootstrap);
            Assert.That(controller.Player.BodyReport, Is.Not.Null, "The body is built from the player's look and kit.");
            Assert.That(controller.Player.BodyReport.Shown[EquipmentSlot.Chest], Does.StartWith("Peasant:Body"), "The starting tunic is worn.");

            controller.ToggleHub();
            yield return null;
            var hub = controller.HubWindow;
            Assert.That(hub.IsOpen, Is.True);
            foreach (var section in PlayerHubWindow.Sections)
                Assert.That(hub.Root.Q<Button>("xk-hub-tab-" + section), Is.Not.Null, section);
            Assert.That(controller.Preview, Is.Not.Null);
            Assert.That(controller.Preview.Report.Shown[EquipmentSlot.Chest], Does.StartWith("Peasant:Body"), "The preview shows the same body.");
            Assert.That(controller.Player.MovementEnabled, Is.False);

            // Inventory: equip the knife from the pack.
            hub.Select("Inventory");
            var knifeRow = hub.Content.Q<VisualElement>("xk-hub-item-dagger_iron");
            Assert.That(knifeRow, Is.Not.Null);
            Click(knifeRow.Query<Button>().ToList().First(b => b.text == "Equip"));
            yield return null;
            Assert.That(session.GetPlayerHub().Equipment[EquipmentSlot.MainHand].ItemId, Is.EqualTo("dagger_iron"));
            Assert.That(controller.Player.BodyReport.Props, Has.Some.StartsWith("dagger_iron@hand_r"), "The knife is in the right hand.");
            Assert.That(controller.Player.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Item dagger_iron"), Is.True);
            Assert.That(controller.Preview.Report.Props, Has.Some.StartsWith("dagger_iron@"));

            // Equipment: take off the tunic; underclothes show.
            hub.Select("Equipment");
            Click(hub.Content.Q<VisualElement>("xk-hub-slot-Chest").Q<Button>());
            yield return null;
            Assert.That(session.GetPlayerHub().Equipment.ContainsKey(EquipmentSlot.Chest), Is.False);
            Assert.That(controller.Player.BodyReport.Shown[EquipmentSlot.Chest], Is.EqualTo("underclothes"));

            // Appearance: a female frame, rebuilt at once.
            hub.Select("Appearance");
            Click(hub.Content.Q<Button>("xk-hub-frame-1"));
            yield return null;
            Assert.That(session.GetPlayerHub().Appearance.Frame, Is.EqualTo(NpcSex.Female));
            Assert.That(controller.Player.LookSignature, Does.StartWith("Female"));

            foreach (var section in PlayerHubWindow.Sections)
            {
                hub.Select(section);
                Assert.That(hub.Content.childCount, Is.GreaterThan(0), section + " shows something.");
            }

            controller.CloseWindows();
            Assert.That(hub.IsOpen, Is.False);
            Assert.That(controller.Player.MovementEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator TheLight_AndTheResidents_FollowTheClock_AndSomeoneWalksToWork()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var session = bootstrap.Session;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var controller = Controller();
            yield return EnterTown(bootstrap);
            runtime.Playback.Pause();
            var atmosphere = controller.Atmosphere;
            Assert.That(atmosphere.AppliedMinute, Is.EqualTo(session.GetWorldTime().MinuteOfDay));
            Assert.That(atmosphere.Sun.intensity, Is.GreaterThan(0.5f), "Morning sun.");
            Assert.That(atmosphere.Lamps.Count, Is.GreaterThan(5));
            Assert.That(atmosphere.Lamps.All(l => !l.enabled), Is.True, "No lamps by day.");

            // To 22:30: night.
            var minute = session.GetWorldTime().MinuteOfDay;
            controller.WaitHour(22 * 60 + 30 - minute);
            yield return null;
            Assert.That(atmosphere.AppliedMinute, Is.EqualTo(22 * 60 + 30));
            Assert.That(atmosphere.Sun.intensity, Is.EqualTo(0f));
            Assert.That(atmosphere.SunElevation, Is.LessThan(0f));
            Assert.That(atmosphere.Lamps.All(l => l.enabled && l.intensity > 1f), Is.True, "Lamps lit.");
            var layout = controller.Streamer.Current;
            foreach (var p in session.GetPresenceIn(layout.SettlementId))
            {
                var view = controller.Residents[p.NpcId];
                var inside = p.Activity == ActivityKind.Sleeping || p.LocationKind == LocationKind.Residence;
                Assert.That(view.IsHidden, Is.EqualTo(inside), $"{view.DisplayName}: {p.Activity} at {p.LocationName}");
                Assert.That(view.Activity, Is.EqualTo(p.Activity));
            }

            // Next morning: wait (in steps short enough to walk) until a shopkeeper leaves home for work.
            var keeperId = session.GetShops(layout.SettlementId).First(s => s.Categories.Contains(ShopCategory.General)).KeeperId;
            var keeper = controller.Residents[keeperId];
            var walked = false;
            for (var i = 0; i < 3 * 24 * 4 && !walked; i++) // up to three days (a rest day keeps them away)
            {
                controller.WaitHour(15);
                yield return null;
                walked = keeper.IsWalking && session.GetNpcPresence(keeperId).LocationKind == LocationKind.Shop;
            }

            Assert.That(walked, Is.True, "The keeper set off for the shop.");
            Assert.That(keeper.IsHidden, Is.False, "Out of the house and in the street.");
            Time.timeScale = 20f;
            for (var i = 0; i < 800 && keeper.IsWalking; i++) yield return new WaitForSeconds(0.25f); // up to 200 s of walking (scaled time)
            Time.timeScale = 1f;
            Assert.That(keeper.IsWalking, Is.False, "Arrived.");
            Assert.That(Vector3.Distance(keeper.transform.localPosition, keeper.Destination), Is.LessThan(0.6f));
            var shop = controller.Shops.Values.First(v => v.Building.Fit == ShopFit.General);
            var behind = LocalPaths.BehindCounter(shop.Building);
            Assert.That(Vector2.Distance(new Vector2(keeper.transform.localPosition.x, keeper.transform.localPosition.z), new Vector2(behind.X, behind.Z)), Is.LessThan(1.2f),
                "Behind the counter of the shop the world says they keep.");
        }

        [UnityTest]
        public IEnumerator AShutShop_SaysWhy_AndNoOneStandsInForTheKeeper()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var session = bootstrap.Session;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var controller = Controller();
            var town = session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            var minute = session.GetWorldTime().MinuteOfDay;
            session.Execute(new AdvanceTimeCommand(1440 - minute + 2 * 60)); // 02:00
            yield return EnterTown(bootstrap);
            runtime.Playback.Pause();
            var shop = controller.Shops.Values.First(v => v.Building.Fit == ShopFit.Smithy);
            var summary = session.GetShops(town.SettlementId).Single(s => s.ShopId == shop.ShopId);
            Assert.That(summary.IsOpen, Is.False);
            Assert.That(controller.Residents[summary.KeeperId].IsHidden, Is.True, "The smith is asleep at home.");
            var b = shop.Building;
            foreach (var view in controller.Residents.Values.Where(v => !v.IsHidden))
            {
                var local = shop.transform.InverseTransformPoint(view.transform.position);
                Assert.That(Mathf.Abs(local.x) < b.Width / 2f && Mathf.Abs(local.z) < b.Depth / 2f, Is.False, view.DisplayName + " is not minding the shop.");
            }

            controller.OpenTrade(shop);
            yield return null;
            Assert.That(controller.Trade.Root.Q<Label>("xk-trade-status").text, Does.StartWith("Not trading").And.Contain("Closed"));
            Assert.That(controller.Trade.Root.Q<VisualElement>("xk-trade-forsale").Query<Button>().ToList().All(x => !x.enabledSelf), Is.True, "Nothing can be bought.");
            Assert.That(shop.ShownTotal, Is.GreaterThan(0), "The wares are still on the shelves.");
        }
    }
}
