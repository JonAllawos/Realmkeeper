using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
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
    /// Phase 26 evidence capture (not pass/fail): the completed screens in the running game with its own data, at these
    /// sizes, each with the interface scale Auto gives at that height:
    /// <list type="bullet">
    /// <item>16:9: 1920×1080, 1366×768, 1280×720, 2560×1440;</item>
    /// <item>4:3: 1024×768;</item>
    /// <item>21:9: 3440×1440;</item>
    /// <item>16:10: 1280×800;</item>
    /// <item>the narrow layout: a 700×1000 window, the only kind of window narrow enough after Auto scaling.</item>
    /// </list>
    /// It runs only when RK_CAPTURE_DIR is set; writes JPEGs and p26_capture_log.txt.
    /// </summary>
    public sealed class Phase26CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private float _text;
        private bool _contrast;
        private bool _motion;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _text = RkSettings.TextSize;
            _contrast = RkSettings.HighContrast;
            _motion = RkSettings.ReducedMotion;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "p26_capture_log.txt"), Log);
            RkSettings.TextSize = _text;
            RkSettings.HighContrast = _contrast;
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
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

        private static readonly (int, int)[] All = { (1920, 1080), (1366, 768), (1280, 720), (1024, 768), (1280, 800), (2560, 1440), (3440, 1440), (700, 1000) };
        private static readonly (int, int)[] Main = { (1920, 1080), (1366, 768), (1280, 800) };

        [UnityTest]
        public IEnumerator Capture_Phase26_Screens()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            RkSettings.ReducedMotion = true;
            var b = Bootstrap;
            var rt = Runtime;
            yield return Settle(10);
            var map = b.CameraController.Camera;
            var s = b.Session;
            var town = s.GetSettlementsInView().First(x => x.SettlementType == SettlementType.Town);

            // Strategic map panels (before entering: the map as the player first sees it).
            b.Selection.Select(town.SettlementId);
            yield return Settle(4);
            yield return Sizes("p26_11_strategic_map_panels", $"Strategic map: '{town.Name}' selected; the settlement inspector, factions and events.", map, (1920, 1080), (1366, 768), (1280, 800));

            // Into the town; character creation first (before a character exists).
            var c = Controller;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            Assert.That(b.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(10);
            rt.Playback.Pause();
            var cam = c.CameraRig.Camera;
            if (c.OpenCreator())
            {
                yield return Settle(4);
                yield return Sizes("p26_08_character_creation", "Character creation: steps, the game's own choices, the build so far.", cam, (1920, 1080), (1280, 800), (1024, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            Assert.That(s.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("fighter"))).IsSuccess, Is.True);
            yield return Settle(4);

            // Companions: willing townsfolk asked in conversation (the Phase 15 drivers).
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(rt, c, s, 2, ids);
            c = Controller;
            cam = c.CameraRig.Camera;
            rt.Playback.Pause();
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);

            // Dialogue (with a companion: the conversation's responses and their kinds).
            if (c.StartDialogue(ids[0]).IsSuccess)
            {
                yield return Settle(4);
                yield return Sizes("p26_10_dialogue", "Dialogue: the speaker's emblem and standing; responses with their kind (check, quest, party, trade, leave) as icons beside the words.", cam, Main);
                c.EndDialogue();
                yield return Settle(2);
            }

            // Trade.
            var shops = Object.FindObjectsByType<ShopInteriorView>(FindObjectsSortMode.None).OrderBy(x => x.ShopName, StringComparer.Ordinal).ToList();
            var shop = shops.FirstOrDefault(x => s.GetShopDetail(x.ShopId)?.ForSale.Any(l => ItemCatalog.Find(l.ItemId)?.IsEquippable == true) == true) ?? shops.FirstOrDefault();
            if (shop != null)
            {
                c.OpenTrade(shop);
                yield return Settle(4);
                var d = c.Trade.Detail;
                var first = d.ForSale.FirstOrDefault();
                if (first != null) c.Trade.Select("sale", first.ItemId, first.StackId);
                yield return Settle(2);
                yield return Sizes("p26_01_trade", $"Trade at {shop.ShopName}: pages (Buy, Sell, Particular goods, Mending), the list with prices, the selected line's inspector, the shop.", cam, All);
                var wearable = d.ForSale.FirstOrDefault(l => ItemCatalog.Find(l.ItemId)?.IsEquippable == true);
                if (wearable != null)
                {
                    c.Trade.Select("sale", wearable.ItemId, wearable.StackId);
                    yield return Settle(2);
                    yield return Sizes("p26_03_trade_comparison", $"Trade: {wearable.Name} compared with what is worn (the rules engine on a copy), the price made factor by factor, quantity and the purse after.", cam, (1920, 1080), (1024, 768));
                }

                c.Trade.SelectPage("Sell");
                var offer = c.Trade.Detail.Offers.FirstOrDefault(o => o.Accepted) ?? c.Trade.Detail.Offers.FirstOrDefault();
                if (offer != null) c.Trade.Select("offer", offer.ItemId, offer.StackId);
                yield return Settle(2);
                yield return Sizes("p26_02_merchant_and_player_inventory", "Trade ▸ Sell: the player's goods with the shop's offer, or its reason for refusing written on the row.", cam, (1920, 1080), (1366, 768));
                RkSettings.TextSize = 1.3f;
                RkSettings.HighContrast = true;
                yield return Settle(4);
                yield return Sizes("p26_20b_trade_text130_contrast", "Trade with text at 130 % and high contrast.", cam, (1920, 1080), (1366, 768));
                RkSettings.TextSize = 1f;
                RkSettings.HighContrast = false;
                c.CloseWindows();
                yield return Settle(2);
            }

            // Crafting.
            c.ToggleHub();
            var hub = c.HubWindow;
            hub.Select("Crafting & work");
            yield return Settle(3);
            yield return Sizes("p26_04_crafting_browser", "Crafting: categories with counts, Can make now, search; recipes with their output or the one thing stopping them.", cam, (1920, 1080), (1366, 768), (1024, 768));
            var recipe = s.GetWorkshop().Recipes.OrderByDescending(r => r.Inputs.Count).First();
            hub.SelectRecipe(recipe.Id);
            yield return Settle(3);
            yield return Sizes("p26_05_crafting_detail", $"Crafting: '{recipe.Name}' — what it makes, each material have/need, tool, station, check and chance, time, and Start or why not.", cam, (1920, 1080), (700, 1000));

            // Party and a companion.
            hub.Select("Party");
            yield return Settle(3);
            yield return Sizes("p26_06_party", "Party: you first, then each companion with health and status; the selected member beside the list.", cam, (1920, 1080), (1366, 768));
            if (ids.Count > 1) hub.ShowParty(ids[1]);
            yield return Settle(3);
            yield return Sizes("p26_07_companion_inspection", "A companion inspected: sheet numbers, ability tiles, loyalty in words, what they carry, the actions.", cam, (1920, 1080), (1024, 768));

            // Codex.
            hub.Select("Codex");
            yield return Settle(3);
            yield return Sizes("p26_14_codex_overview", "Codex: categories with counts, search and sort; entries grouped by category.", cam, All);
            var codex = s.GetCodex();
            foreach (var (cat, file, note) in new[]
                     {
                         (CodexCategory.People, "p26_15_codex_people", "People met: what meeting them tells, how each fact is known, what knowing them better would add."),
                         (CodexCategory.Factions, "p26_16_codex_factions", "Factions: those you have a standing with or whose towns you have visited."),
                         (CodexCategory.Settlements, "p26_18_codex_locations", "Settlements and places: visited, found, or only heard of."),
                     })
            {
                var shown = cat;
                var e = codex.Entries.FirstOrDefault(x => x.Category == cat);
                if (e == null && cat == CodexCategory.People) { shown = CodexCategory.Companions; e = codex.Entries.FirstOrDefault(x => x.Category == shown); } // everyone met here joined the party
                if (e == null && cat == CodexCategory.Settlements) { shown = CodexCategory.Places; e = codex.Entries.FirstOrDefault(x => x.Category == shown); }
                hub.SetCodexFilter(shown);
                if (e != null) hub.SelectCodex(e.Id);
                yield return Settle(3);
                yield return Sizes(file, note + (e == null ? " (none known yet)" : $" Selected: {e.Name}."), cam, (1920, 1080));
            }

            hub.SetCodexFilter(null, town.Name.Substring(0, 3));
            yield return Settle(3);
            yield return Sizes("p26_19_codex_search", $"Codex search: '{town.Name.Substring(0, 3)}'.", cam, (1920, 1080), (1366, 768));
            hub.SetCodexFilter(null, string.Empty);
            c.CloseWindows();
            yield return Settle(2);

            // Combat (and then the creatures it taught).
            if (c.StartPracticeFight().IsSuccess)
            {
                c.AutoAi = false;
                yield return Settle(10);
                yield return Sizes("p26_13_combat", "Combat: turn order with health lines, the selected combatant with condition badges, actions, the log.", cam, (1920, 1080), (1366, 768), (2560, 1440));
                yield return Phase14Driver.FightOut(c);
                yield return Settle(4);
                c.ToggleHub();
                c.HubWindow.Select("Codex");
                var beast = s.GetCodex(category: CodexCategory.Creatures).Entries.FirstOrDefault();
                c.HubWindow.SetCodexFilter(CodexCategory.Creatures);
                if (beast != null) c.HubWindow.SelectCodex(beast.Id);
                yield return Settle(3);
                yield return Sizes("p26_17_codex_creatures", "Codex ▸ Creatures: from the fights the player was in (the bestiary has the full entry).", cam, (1920, 1080));
                c.HubWindow.SetCodexFilter(null);
                c.CloseWindows();
                yield return Settle(2);
            }

            // Advancement.
            Assert.That(s.Dispatch(new AwardExperienceCommand(300, ExperienceSource.Milestone, reason: "Phase 26 capture", awardKey: "p26-capture-award")).IsSuccess, Is.True);
            yield return Settle(2);
            if (c.OpenAdvancement())
            {
                yield return Settle(4);
                yield return Sizes("p26_09_character_advancement", "Advancement: the new level's benefits and the choices still open; nothing chosen for the player.", cam, (1920, 1080), (1366, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            // Settings: accessibility.
            var settings = new RkSettingsWindow(c.Hud.Root.panel.visualTree);
            settings.Root.Q<ScrollView>().ScrollTo(settings.TextSize);
            yield return Settle(3);
            yield return Sizes("p26_20_accessibility_settings", "Settings ▸ Accessibility: text size, high contrast, reduced motion; interface sounds above.", cam, (1920, 1080), (1024, 768));
            settings.Close();
            yield return Settle(2);

            // The region map in the country.
            if (c.IsInTownForCountry)
            {
                c.GoOutIntoCountry();
                for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
                yield return Settle(10);
                rt.Playback.Pause();
                cam = c.CameraRig.Camera;
                c.ToggleRegionMap();
                yield return Settle(4);
                yield return Sizes("p26_12_regional_map", "The region map (N) with its key and scale (one square = 40 m, the approved scale).", cam, (1920, 1080), (1280, 800), (1024, 768));
                c.ToggleRegionMap();
            }
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
