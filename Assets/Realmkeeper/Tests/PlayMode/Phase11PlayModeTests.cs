using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
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
    /// <summary>Shared steps for Phase 11 tests and captures: giving the character things through a saved file, and clicking by label.</summary>
    internal static class ItemDriver
    {
        /// <summary>
        /// Saves, adds instances to the saved player's pack (and applies any other change to the saved world), and loads
        /// the file again — the same path a player's own save takes, through the loader's validation. No backdoor.
        /// </summary>
        public static IEnumerator Give(RealmkeeperRuntime runtime, ExplorationController c, WorldSession session, Action<JObject> change, params (string Id, int Qty, Action<JObject> State)[] items)
        {
            Assert.That(c.SaveNow().IsSuccess, Is.True, "Saved before the edit.");
            var path = new JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder)).PathFor(runtime.ActiveSlot.Name);
            var doc = JObject.Parse(File.ReadAllText(path));
            var world = (JObject)doc["world"];
            var pack = (JArray)world["player"]["inventory"];
            foreach (var (id, qty, state) in items)
            {
                var def = ItemCatalog.Find(id);
                Assert.That(def, Is.Not.Null, id);
                var units = def.Stackable ? 1 : qty;
                for (var i = 0; i < units; i++)
                {
                    var stack = new JObject { ["stackId"] = Guid.NewGuid().ToString(), ["itemId"] = id, ["quantity"] = def.Stackable ? qty : 1, ["provenance"] = "test" };
                    if (def.Magic != null && def.Magic.MaxCharges > 0) stack["charges"] = def.Magic.MaxCharges;
                    state?.Invoke(stack);
                    pack.Add(stack);
                }
            }

            change?.Invoke(world);
            File.WriteAllText(path, doc.ToString());
            var load = session.Execute(new LoadWorldCommand(runtime.ActiveSlot.Name));
            Assert.That(load.IsSuccess, Is.True, load.Message + " " + string.Join("; ", load.Errors));
            for (var i = 0; i < 6; i++) yield return null;
        }

        /// <summary>Clicks the first enabled button with this label under the element.</summary>
        public static void ClickText(VisualElement root, string text)
        {
            var b = root.Query<Button>().ToList().FirstOrDefault(x => x.text == text);
            Assert.That(b, Is.Not.Null, "No button '" + text + "'");
            Assert.That(b.enabledSelf, Is.True, "'" + text + "' is disabled");
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = b;
                b.SendEvent(click);
            }
        }

        public static VisualElement ItemRow(PlayerHubWindow hub, string itemId) => hub.Content.Q<VisualElement>("xk-hub-item-" + itemId);

        /// <summary>
        /// Phase 25: an action the inventory keeps in the item inspector (the row holds only Inspect and the main use): select
        /// the item from its row, then press the action in the inspector.
        /// </summary>
        public static void InspectorAction(PlayerHubWindow hub, string itemId, string text)
        {
            ClickText(ItemRow(hub, itemId), "Inspect");
            ClickText(hub.Content.Q<VisualElement>("xk-inv-details"), text);
        }

        public static string Texts(VisualElement root) => string.Join(" | ", root.Query<Label>().ToList().Select(l => l.text));
    }

    /// <summary>
    /// Phase 11 in the shipped scene, through the real UI (ADR-0055): the inventory's search, filters, sorting,
    /// inspection, comparison and equipping (rings and the off hand); using a potion; attunement and a cursed item;
    /// the chest in the street and storing and taking; crafting and gathering as work that passes time; a shop's
    /// particular goods and an armourer's mending; and the loot a fight leaves. Sixteen flows in all.
    /// </summary>
    public sealed class Phase11PlayModeTests
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

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static PlayerHubWindow OpenHub(ExplorationController c, string section)
        {
            if (!c.HubWindow.IsOpen) c.ToggleHub();
            c.HubWindow.Select(section);
            return c.HubWindow;
        }

        [UnityTest]
        public IEnumerator TheInventory_SearchesFiltersSortsInspectsComparesAndEquips()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, null, ("potion_healing", 2, null), ("iron_ore", 4, null), ("gold_ring", 2, null), ("dagger_iron", 2, null), ("longsword_plus1", 1, null));

            // 1. The inventory shows its load from the carrying rules and every row.
            var hub = OpenHub(c, "Inventory");
            yield return null;
            Assert.That(hub.Content.Q<VisualElement>("xk-inv-load"), Is.Not.Null);
            Assert.That(ItemDriver.Texts(hub.Content), Does.Contain("capacity").And.Contain("AC "));
            Assert.That(ItemDriver.ItemRow(hub, "iron_ore"), Is.Not.Null);

            // 2. Search.
            hub.Content.Q<TextField>("xk-inv-search").value = "ore";
            ItemDriver.ClickText(hub.Content, "Search");
            yield return null;
            Assert.That(ItemDriver.ItemRow(hub, "iron_ore"), Is.Not.Null);
            Assert.That(ItemDriver.ItemRow(hub, "potion_healing"), Is.Null, "Search narrows the list.");
            ItemDriver.ClickText(hub.Content, "Clear");
            yield return null;

            // 3. Filter by group.
            LevelUpDriver.Click(hub.Content, "xk-inv-group-Consumables");
            yield return null;
            Assert.That(ItemDriver.ItemRow(hub, "potion_healing"), Is.Not.Null);
            Assert.That(ItemDriver.ItemRow(hub, "iron_ore"), Is.Null, "Only consumables.");
            LevelUpDriver.Click(hub.Content, "xk-inv-group-all");
            yield return null;

            // 4. Sort by weight (heaviest first).
            LevelUpDriver.Click(hub.Content, "xk-inv-sort-Weight");
            yield return null;
            Assert.That(hub.Sort, Is.EqualTo(InventorySort.Weight));
            var page = session.GetInventory(null, null, InventorySort.Weight, true);
            var first = hub.Content.Query<VisualElement>().ToList().First(e => e.name != null && e.name.StartsWith("xk-hub-item-"));
            Assert.That(first.name, Is.EqualTo("xk-hub-item-" + page.Rows[0].ItemId), "The heaviest row first.");

            // 5–6. Inspect the magic sword: its rules and the comparison with what is in hand.
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "longsword_plus1"), "Inspect");
            yield return null;
            var details = hub.Content.Q<VisualElement>("xk-inv-details");
            Assert.That(details, Is.Not.Null);
            Assert.That(ItemDriver.Texts(details), Does.Contain("+1 to attack and damage rolls").And.Contain("SRD 5.2.1"));
            Assert.That(hub.Content.Q<VisualElement>("xk-inv-compare"), Is.Not.Null, "A comparison through the rules engine.");
            Assert.That(ItemDriver.Texts(hub.Content.Q<VisualElement>("xk-inv-compare")), Does.Contain("Main attack"));

            // 7. Equip from its row.
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "longsword_plus1"), "Equip");
            yield return null;
            Assert.That(session.GetInventory().Equipped[EquipmentSlot.MainHand].ItemId, Is.EqualTo("longsword_plus1"));

            // 8. Rings on both hands; a dagger in the off hand gives an off-hand attack.
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "gold_ring"), "Equip");
            yield return null;
            ItemDriver.InspectorAction(hub, "gold_ring", "Right hand"); // Phase 25: in the inspector
            yield return null;
            var eq = session.GetInventory().Equipped;
            Assert.That(eq.ContainsKey(EquipmentSlot.RingLeft) && eq.ContainsKey(EquipmentSlot.RingRight), Is.True, "A ring on each hand.");
            Assert.That(ItemDriver.ItemRow(hub, "dagger_iron"), Is.Not.Null);
            ItemDriver.InspectorAction(hub, "dagger_iron", "Off hand"); // Phase 25: in the inspector
            yield return null;
            Assert.That(session.GetInventory().Equipped[EquipmentSlot.OffHand].ItemId, Is.EqualTo("dagger_iron"));
            hub.Select("Equipment");
            yield return null;
            Assert.That(hub.Content.Q<VisualElement>("xk-hub-slot-RingRight"), Is.Not.Null);
            Assert.That(ItemDriver.Texts(hub.Content), Does.Contain("Armour class"));
        }

        [UnityTest]
        public IEnumerator Potions_Attunement_AndACurse_WorkFromTheHub()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, w => w["player"]["rpg"]["currentHp"] = 1, ("potion_healing", 1, null), ("cloak_of_protection", 1, null), ("berserker_axe", 1, null));

            // 9. Drink a potion from its row.
            var hub = OpenHub(c, "Inventory");
            yield return null;
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "potion_healing"), "Drink");
            yield return null;
            Assert.That(session.GetCharacter().Player.Rpg.CurrentHp, Is.GreaterThan(1), hub.LastMessage);
            Assert.That(hub.LastMessage, Does.Contain("Hit points"));

            // 10. Attune to a cloak: an hour of work, waited out from the panel.
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "cloak_of_protection"), "Equip");
            yield return null;
            hub.Select("Equipment");
            yield return null;
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-hub-slot-Back"), "Attune");
            yield return null;
            Assert.That(session.GetWork(), Is.Not.Null, hub.LastMessage);
            hub.Select("Crafting & work");
            yield return null;
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-work-active"), "Wait until done");
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(session.GetInventory().Equipped[EquipmentSlot.Back].Attuned, Is.True, hub.LastMessage);

            // 11. A cursed axe binds when attuned; taking it off is refused, with the reason.
            hub.Select("Inventory");
            yield return null;
            ItemDriver.ClickText(ItemDriver.ItemRow(hub, "berserker_axe"), "Equip");
            yield return null;
            hub.Select("Equipment");
            yield return null;
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-hub-slot-MainHand"), "Attune");
            yield return null;
            Assert.That(c.FinishWork(session.GetWork().Key).IsSuccess, Is.True);
            yield return null;
            hub.Select("Equipment");
            yield return null;
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-hub-slot-MainHand"), "Unequip");
            yield return null;
            Assert.That(hub.LastMessage, Does.Contain("curse"));
            Assert.That(session.GetInventory().Equipped[EquipmentSlot.MainHand].Bound, Is.True);
        }

        [UnityTest]
        public IEnumerator TheChestInTheStreet_OpensInStorage_AndKeepsWhatIsStored()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, null, ("potion_healing", 3, null));
            Assert.That(c.Containers.Any(x => x.ContainerKind == ContainerKind.Personal), Is.True, "The chest stands in the street.");
            Assert.That(c.Containers.Any(x => x.ContainerKind == ContainerKind.Cache), Is.True, "The cache's flagstone lies on the square.");

            // 12. Walk up to the chest and open it: the hub opens on Storage.
            var chest = c.Containers.First(x => x.ContainerKind == ContainerKind.Personal);
            var p = chest.InteractionPoint;
            c.Player.Teleport(new LocalPose(p.x + 0.2f, p.y, p.z, 0f));
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(c.CurrentTarget, Is.SameAs(chest), "The chest is the thing in front of the player.");
            c.Interact();
            yield return null;
            Assert.That(c.HubWindow.IsOpen && c.HubWindow.Section == "Storage", Is.True);
            Assert.That(c.HubWindow.Content.Q<VisualElement>("xk-store-" + chest.Key), Is.Not.Null);

            // 13. Store potions from the inventory, then take them back from Storage.
            var hub = OpenHub(c, "Inventory");
            yield return null;
            ItemDriver.InspectorAction(hub, "potion_healing", "Store"); // Phase 25: in the inspector
            yield return null;
            Assert.That(session.GetContainer(chest.Key).Rows.Single(r => r.ItemId == "potion_healing").Quantity, Is.EqualTo(3), hub.LastMessage);
            hub.Select("Storage");
            yield return null;
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-store-" + chest.Key).Q<VisualElement>("xk-store-item-potion_healing"), "Take");
            yield return null;
            Assert.That(session.GetInventory().Rows.Where(r => r.ItemId == "potion_healing").Sum(r => r.Quantity), Is.EqualTo(3), hub.LastMessage);

            // Saved and loaded, the chest still holds what is left in it.
            ItemDriver.ClickText(hub.Content, "Leave 10 coin");
            yield return null;
            Assert.That(c.SaveNow().IsSuccess, Is.True);
            Assert.That(session.Execute(new LoadWorldCommand(Runtime.ActiveSlot.Name)).IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            Assert.That(session.GetContainer(chest.Key).Coin, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator Crafting_AndGathering_AreWorkThatPassesTime()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, null, ("leatherworkers_tools", 1, null), ("cured_leather", 3, null), ("herbalism_kit", 1, null));

            // 14. Start a recipe from its row; it holds the leather; wait it out; the result (or what is left) comes back.
            var hub = OpenHub(c, "Crafting & work");
            yield return null;
            var row = hub.Content.Q<VisualElement>("xk-craft-stitch_jerkin");
            Assert.That(row, Is.Not.Null);
            var clock = session.GetWorldTime().Text;
            ItemDriver.ClickText(row, "Start");
            yield return null;
            Assert.That(session.GetWork(), Is.Not.Null, hub.LastMessage);
            Assert.That(session.GetInventory().Rows.Any(r => r.ItemId == "cured_leather"), Is.False, "The work holds the leather.");
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-work-active"), "Wait until done");
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(session.GetWork(), Is.Null, hub.LastMessage);
            Assert.That(session.GetWorldTime().Text, Is.Not.EqualTo(clock), "Time passed.");
            var rows = session.GetInventory().Rows;
            Assert.That(rows.Any(r => r.ItemId == "jerkin_leather") || rows.Any(r => r.ItemId == "cured_leather"), Is.True, hub.LastMessage);

            // 15. Gather at the hedgerows: the site's stock falls, the herbs arrive.
            hub.Select("Crafting & work");
            yield return null;
            var site = session.GetWorkshop().Sites.First(s => s.Problem == null);
            ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-gather-" + site.Key), $"Gather {site.Yield} ({ItemCommandHandlers.Duration(site.Minutes)})");
            yield return null;
            Assert.That(session.GetWorkshop().Sites.First(s => s.Key == site.Key).Available, Is.EqualTo(site.Available - site.Yield));
            Assert.That(c.FinishWork(session.GetWork().Key).IsSuccess, Is.True);
            Assert.That(session.GetInventory().Rows.Any(r => r.Name == site.Item), Is.True);
        }

        [UnityTest]
        public IEnumerator AnArmourer_BuysAWornBlade_SellsItBack_AndMends()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var settlement = session.GetCharacter().Player.SettlementId;
            var smith = session.GetShops(settlement).FirstOrDefault(s => s.Categories.Contains(ShopCategory.WeaponsAndArmour));
            Assume.That(smith, Is.Not.Null, "This town has no armourer.");
            yield return ItemDriver.Give(Runtime, c, session, w => w["minuteOfDay"] = 600,
                ("shortsword", 1, s => s["condition"] = "Damaged"), ("mace_flanged", 1, s => s["condition"] = "Worn"));
            foreach (var m in new[] { 900, 1020, 540 })
            {
                if (session.GetShopDetail(smith.ShopId).Summary.IsOpen) break;
                var minute = m;
                yield return ItemDriver.Give(Runtime, c, session, w => w["minuteOfDay"] = minute);
            }

            c.OpenTrade(smith.ShopId);
            yield return null;
            var trade = c.Trade;
            Assume.That(trade.Detail.Summary.IsOpen, Is.True, "The armourer is away: " + trade.Detail.Summary.Status);

            // 16. Sell the damaged blade (half price, its state shown), see it among the particular goods, buy it back; mend the mace.
            var offer = trade.Root.Q<VisualElement>("xk-trade-offer-shortsword");
            Assert.That(offer, Is.Not.Null);
            Assert.That(ItemDriver.Texts(offer), Does.Contain("(damaged)"));
            ItemDriver.ClickText(offer, "Sell");
            yield return null;
            Assert.That(trade.LastMessage, Does.StartWith("Sold"), trade.LastMessage);
            var goods = trade.Root.Q<VisualElement>("xk-trade-goods-shortsword");
            Assert.That(goods, Is.Not.Null, "It keeps its state in the shop.");
            ItemDriver.ClickText(goods, "Buy");
            yield return null;
            Assert.That(session.GetInventory().Rows.Single(r => r.ItemId == "shortsword").Condition, Is.EqualTo(ItemCondition.Damaged), trade.LastMessage);
            ItemDriver.ClickText(trade.Root.Q<VisualElement>("xk-trade-repair-mace_flanged"), "Mend");
            yield return null;
            Assert.That(session.GetInventory().Rows.Single(r => r.ItemId == "mace_flanged").Condition, Is.EqualTo(ItemCondition.Good), trade.LastMessage);
        }

        [UnityTest]
        public IEnumerator AFight_LeavesLoot_ThatOpensFromTheCombatPanel()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.AutoAi = false;
            var purse = session.GetCharacter().Player.Purse;
            LevelUpDriver.Click(c.Hud.Root, "xk-open-outing");
            for (var i = 0; i < 3; i++) yield return null;
            Assert.That(c.InFight, Is.True);
            var w = c.Combat;
            for (var i = 0; i < 240 && c.InFight; i++)
            {
                if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                else CombatDriver.Step(c);
                yield return null;
            }

            Assert.That(c.InFight, Is.False);
            var won = !c.PlayerIsDead && w.Snapshot.Outcome == EncounterOutcome.Victory && w.Snapshot.Order.Any(x => x.Kind == CombatantKind.Creature && x.State == VitalState.Dead);
            Assume.That(won, Is.True, "The fight was not won with a foe slain this time.");
            Assert.That(session.GetCharacter().Player.Purse, Is.EqualTo(purse), "No coin is dropped straight into the purse.");

            // 17. The panel offers the loot; it opens in Storage; take all.
            if (w.Snapshot.LootKey == null) Assert.Pass("The slain carried nothing.");
            var key = w.Snapshot.LootKey;
            LevelUpDriver.Click(w.Root, "xk-combat-loot");
            yield return null;
            Assert.That(c.HubWindow.IsOpen && c.HubWindow.Section == "Storage", Is.True);
            var box = c.HubWindow.Content.Q<VisualElement>("xk-store-" + key);
            Assert.That(box, Is.Not.Null);
            ItemDriver.ClickText(box, "Take all");
            yield return null;
            Assert.That(session.GetContainer(key).Rows, Is.Empty, c.HubWindow.LastMessage);
        }
    }
}
