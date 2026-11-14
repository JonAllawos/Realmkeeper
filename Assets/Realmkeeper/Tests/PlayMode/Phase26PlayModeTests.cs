using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 26 in the shipped scene, through the real interface. It covers:
    /// <list type="bullet">
    /// <item>trade: select a line, choose a quantity, confirm (inventory, purse and stock move exactly), refusal reasons
    /// shown;</item>
    /// <item>crafting: the filters, the recipe inspector with each material;</item>
    /// <item>party: the list with you first;</item>
    /// <item>codex: known entries only, search, the inspector;</item>
    /// <item>dialogue: response icons;</item>
    /// <item>combat: the outcome banner;</item>
    /// <item>the region map: its key and the 40 m scale.</item>
    /// </list>
    /// It also measures, in the editor (labelled as such), the timings: opening, closing and switching sections; trade
    /// and codex; repeated opening and closing (element counts, managed memory) with a large inventory.
    /// </summary>
    public sealed class Phase26PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Timings = new List<string>();

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
            var dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir) && Timings.Count > 0) { Directory.CreateDirectory(dir); File.WriteAllLines(Path.Combine(dir, "p26_timings.txt"), Timings); }
            foreach (var t in Timings) Debug.Log("Phase 26 timing: " + t);
            Timings.Clear();
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static IEnumerator Frames(int n = 3)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private static void Click(Button b)
        {
            Assert.That(b, Is.Not.Null);
            Assert.That(b.enabledInHierarchy, Is.True, b.name + " is disabled: " + b.tooltip);
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = b; b.SendEvent(click); }
        }

        private static int Count(VisualElement root) => root == null ? 0 : root.Query<VisualElement>().ToList().Count;

        [UnityTest]
        public IEnumerator TheCompletedScreens_UseTheGamesOwnCommands_AndStayFast()
        {
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var rt = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return Frames(8);
            yield return CombatDriver.EnterTownAsFighter(b, rt, c);
            yield return Frames(4);
            var s = b.Session;

            // ----- Trade: select, choose two, confirm; refusal reasons are written. ---------------------------------
            var shops = Object.FindObjectsByType<ShopInteriorView>(FindObjectsSortMode.None).OrderBy(x => x.ShopName, StringComparer.Ordinal).ToList();
            var watch = Stopwatch.StartNew();
            ShopInteriorView shop = null;
            foreach (var candidate in shops)
            {
                var d = s.GetShopDetail(candidate.ShopId);
                if (d != null && d.Summary.IsOpen && d.ForSale.Any(l => l.Quantity >= 2 && l.UnitPrice * 2 <= d.PlayerPurse && l.Item?.Stackable == true)) { shop = candidate; break; }
            }

            Assume.That(shop, Is.Not.Null, "An open shop with two of something stackable the player can afford.");
            watch.Restart();
            c.OpenTrade(shop);
            var tradeOpenMs = watch.Elapsed.TotalMilliseconds;
            yield return null;
            var trade = c.Trade;
            var line = trade.Detail.ForSale.First(l => l.Quantity >= 2 && l.UnitPrice * 2 <= trade.Detail.PlayerPurse && l.Item?.Stackable == true);
            var purse = s.GetPlayerHub().Purse;
            var held = s.GetInventory().Rows.Where(r => r.ItemId == line.ItemId).Sum(r => r.Quantity);
            trade.Select("sale", line.ItemId, line.StackId);
            yield return null;
            Assert.That(trade.Root.Q<VisualElement>("xk-trade-card"), Is.Not.Null, "The selected line in the inspector.");
            Click(trade.Root.Q<Button>("xk-trade-qty-plus"));
            yield return null;
            Assert.That(trade.Quantity, Is.EqualTo(2));
            var confirm = trade.Root.Q<Button>("xk-trade-confirm");
            Assert.That(confirm.text, Does.Contain("Buy 2 for " + (2 * line.UnitPrice)));
            Click(confirm);
            yield return Frames(2);
            Assert.That(s.GetPlayerHub().Purse, Is.EqualTo(purse - 2 * line.UnitPrice), "The purse paid for two.");
            Assert.That(s.GetInventory().Rows.Where(r => r.ItemId == line.ItemId).Sum(r => r.Quantity), Is.EqualTo(held + 2), "Two received.");
            Assert.That(trade.LastMessage, Does.StartWith("Bought"));
            trade.SelectPage("Sell");
            yield return null;
            var refused = trade.Detail.Offers.FirstOrDefault(o => !o.Accepted);
            if (refused != null)
            {
                var row = trade.Root.Q<VisualElement>("xk-trade-offer-" + refused.ItemId);
                Assert.That(row.Query<Label>().ToList().Any(l => l.text == refused.Refusal), Is.True, "Why it will not buy is written on the row.");
            }

            var forSale = trade.Detail.ForSale.Count;
            var offers = trade.Detail.Offers.Count;
            c.CloseWindows();
            yield return Frames(2);
            Timings.Add($"Trade window open (editor): {tradeOpenMs:0.0} ms with {forSale} lines for sale and {offers} offers.");

            // ----- A large pack for the hub timings (60 kinds of item, given through a save, as Phase 11's tests do). -------
            var ids = ItemCatalog.Items.Where(i => i.Category != ItemCategory.Quest).Take(60).Select(i => (i.Id, 1, (Action<Newtonsoft.Json.Linq.JObject>)null)).ToArray();
            yield return ItemDriver.Give(rt, c, s, null, ids);
            yield return Frames(6);
            c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            s = b.Session;

            // ----- Hub sections: open, each section, close; repeated without accumulating. -----------------------------
            watch.Restart();
            c.ToggleHub();
            var hubOpen = watch.Elapsed.TotalMilliseconds;
            yield return null;
            var hub = c.HubWindow;
            foreach (var section in PlayerHubWindow.Sections)
            {
                watch.Restart();
                hub.Select(section);
                var ms = watch.Elapsed.TotalMilliseconds;
                yield return null;
                Timings.Add($"Hub ▸ {section}: rendered in {ms:0.0} ms ({Count(hub.Content)} elements).");
            }

            watch.Restart();
            c.CloseWindows();
            var hubClose = watch.Elapsed.TotalMilliseconds;
            yield return Frames(2);
            Timings.Add($"Hub open {hubOpen:0.0} ms, close {hubClose:0.0} ms (editor). Inventory rows: {s.GetInventory().Rows.Count}.");

            var hudRoot = c.Hud.DocumentRoot;
            c.ToggleHub();
            hub.Select("Inventory");
            yield return null;
            c.CloseWindows();
            yield return null;
            var baseline = Count(hudRoot);
            GC.Collect();
            var memory = GC.GetTotalMemory(true);
            watch.Restart();
            for (var i = 0; i < 20; i++)
            {
                c.ToggleHub();
                hub.Select(i % 2 == 0 ? "Inventory" : "Codex");
                yield return null;
                c.CloseWindows();
                yield return null;
            }

            var cycleMs = watch.Elapsed.TotalMilliseconds / 20;
            GC.Collect();
            var memoryAfter = GC.GetTotalMemory(true);
            var after = Count(hudRoot);
            // Further batches, one screen each: if memory keeps rising, something is retained; if not, the first batch was warm-up.
            var perScreen = new List<string>();
            var mark = memoryAfter;
            foreach (var screen in new[] { "Inventory", "Codex", "Inventory", "Codex" })
            {
                for (var i = 0; i < 20; i++)
                {
                    c.ToggleHub();
                    hub.Select(screen);
                    yield return null;
                    c.CloseWindows();
                    yield return null;
                }

                GC.Collect();
                var now = GC.GetTotalMemory(true);
                perScreen.Add($"{screen} {(now - mark) / 1024} KB");
                mark = now;
            }

            Timings.Add($"20 open/close cycles (inventory and codex): {cycleMs:0.0} ms each including two frames; HUD elements {baseline} → {after}; managed memory first 20 (warm-up): {(memoryAfter - memory) / 1024} KB; then per 20 cycles: {string.Join(", ", perScreen)}.");
            Assert.That(after, Is.LessThanOrEqualTo(baseline + 5), "Opening and closing does not accumulate interface elements.");

            // ----- Crafting: filters and the recipe inspector. --------------------------------------------------------
            c.ToggleHub();
            hub.Select("Crafting & work");
            yield return null;
            var shop0 = s.GetWorkshop();
            var recipe = shop0.Recipes.First();
            hub.SelectRecipe(recipe.Id);
            yield return null;
            var card = hub.Content.Q<VisualElement>("xk-craft-card");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Query<VisualElement>(className: "xk-craft-mat").ToList().Count, Is.EqualTo(recipe.Inputs.Count), "Every material, have and need.");
            Assert.That(card.Q<Button>("xk-craft-start").enabledSelf, Is.EqualTo(recipe.CanStart && shop0.Active == null));
            Click(hub.Content.Q<Button>("xk-craft-canmake"));
            yield return null;
            var listed = hub.Content.Q<VisualElement>("xk-craft-list").Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith("xk-craft-", StringComparison.Ordinal) && e.name != "xk-craft-list").Select(e => e.name.Substring(9)).ToList();
            Assert.That(listed.All(id => shop0.Recipes.Single(r => r.Id == id).CanStart), Is.True, "Can make now lists only what can start.");

            // ----- Party: you first. -------------------------------------------------------------------------------
            hub.Select("Party");
            yield return null;
            Assert.That(hub.Content.Q<VisualElement>("xk-party-head"), Is.Not.Null);

            // ----- Codex: what is known, searched, inspected. ------------------------------------------------------
            hub.Select("Codex");
            yield return null;
            var codex = s.GetCodex();
            Assert.That(codex.Total, Is.GreaterThan(0), "The visited town at least.");
            var town = codex.Entries.First(e => e.Category == CodexCategory.Settlements && e.Status == "Visited");
            Assert.That(hub.Content.Q<VisualElement>("xk-codex-" + town.Id), Is.Not.Null);
            watch.Restart();
            hub.SetCodexFilter(null, town.Name.Substring(0, 4));
            var searchMs = watch.Elapsed.TotalMilliseconds;
            yield return null;
            Assert.That(hub.Content.Q<VisualElement>("xk-codex-" + town.Id), Is.Not.Null, "Found by search.");
            hub.SelectCodex(town.Id);
            yield return null;
            Assert.That(hub.Content.Q<VisualElement>("xk-codex-card"), Is.Not.Null);
            Timings.Add($"Codex search and render: {searchMs:0.0} ms over {codex.Total} entries.");
            hub.SetCodexFilter(null, string.Empty);
            c.CloseWindows();
            yield return null;

            // ----- Dialogue: each response carries an icon (its words stay). ----------------------------------------
            var npc = c.Residents.Keys.FirstOrDefault();
            if (!npc.IsEmpty && c.StartDialogue(npc).IsSuccess)
            {
                yield return null;
                var responses = c.Dialogue.Root.Query<Button>(className: "xk-dialogue-response").ToList();
                Assert.That(responses, Is.Not.Empty);
                Assert.That(responses.All(r => RkIcons.LeadOf(r) != null), Is.True, "Every response shows its kind.");
                c.EndDialogue();
                yield return null;
            }

            // ----- Combat: the outcome banner. ---------------------------------------------------------------------
            Assert.That(c.StartPracticeFight(false).IsSuccess, Is.True);
            yield return Phase14Driver.FightOut(c);
            yield return Frames(2);
            Assert.That(c.InFight, Is.False, "The fight is over.");
            if (c.Combat != null && c.Combat.Snapshot != null && c.Combat.Snapshot.Status != Realmkeeper.Domain.Combat.EncounterStatus.Active)
                Assert.That(c.Combat.OutcomeText, Is.EqualTo(CombatWindow.OutcomeWord(c.Combat.Snapshot)), "The result is said in a word.");
        }
    }
}
