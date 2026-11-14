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
    /// Phase 25 evidence capture (not pass/fail): every major screen of the overhauled interface in the running game,
    /// with the game's own data, at several sizes:
    /// <list type="bullet">
    /// <item>16:9: 1920×1080, 1366×768 and 1280×720, plus 2560×1440;</item>
    /// <item>4:3: 1024×768;</item>
    /// <item>21:9: 3440×1440.</item>
    /// </list>
    /// The interface scale follows the capture height, as Auto does on such a screen. It runs only when RK_CAPTURE_DIR is
    /// set, and writes JPEG files and a log (p25_capture_log.txt), including each document root's width class.
    /// </summary>
    public sealed class Phase25CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const string CaptureSlot = "p25-capture";
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
            File.WriteAllLines(Path.Combine(_dir, "p25_capture_log.txt"), Log);
            RkSettings.TextSize = _text;
            RkSettings.HighContrast = _contrast;
            RkSettings.ReducedMotion = _motion;
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
        public IEnumerator Capture_Phase25_Screens()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            RkSettings.ReducedMotion = true; // captures show settled screens, not mid-transition ones
            var b = Bootstrap;
            var rt = Runtime;
            yield return Settle(10);
            var map = b.CameraController.Camera;

            // ----- Main menu, new game, settings and accessibility ----------------------------------------------------
            rt.SelectSlot(CaptureSlot);
            rt.Save();
            b.Title.Open(true);
            yield return Sizes("p25_01_main_menu", "Main menu at launch (Continue loads the newest save; Exit asks first).", map, All);
            b.Title.ShowPage("new");
            yield return Sizes("p25_02_new_game", "New game: name, seed, starting speed; the fixed facts stated.", map, (1920, 1080), (1024, 768));
            b.Title.ShowPage(null);
            var settings = new RkSettingsWindow(b.Title.Root.parent);
            yield return Sizes("p25_23_settings", "Settings: display, graphics, sound (with interface sounds), interface, accessibility, controls, what is not available.", map, (1920, 1080), (1280, 720));
            settings.Root.Q<ScrollView>().ScrollTo(settings.TextSize);
            yield return Sizes("p25_24_accessibility", "Accessibility: text size, high contrast, reduced motion (with interface scale above).", map, (1920, 1080), (1024, 768));
            settings.Close();
            RkSettings.TextSize = 1.3f;
            RkSettings.HighContrast = true;
            yield return Settle(4);
            yield return Sizes("p25_24b_text_largest_high_contrast", "The main menu with text at 130 % and high contrast on (same layout, larger text, stronger lines).", map, (1920, 1080), (1366, 768));
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            b.Title.Hide();
            yield return Settle(4);

            // ----- The strategic map ----------------------------------------------------------------------------------
            var town = b.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);
            b.Selection.Select(town.SettlementId);
            yield return Settle(4);
            yield return Sizes("p25_16_strategic_map", $"Strategic map, '{town.Name}' selected: the settlement inspector (population, faction, prosperity, food, economy).", map, (1920, 1080), (1366, 768), (3440, 1440));
            var politics = b.Hud.Root.Q<Button>("toggle-politics");
            if (politics != null)
            {
                using (var click = NavigationSubmitEvent.GetPooled()) { click.target = politics; politics.SendEvent(click); }
                yield return Settle(4);
                yield return Sizes("p25_21_faction_settlement", "Factions and settlements: the political overlay over the map, with the settlement inspector.", map, (1920, 1080));
                using (var click = NavigationSubmitEvent.GetPooled()) { click.target = politics; politics.SendEvent(click); }
            }

            b.Hud.ToggleAtlas(true);
            yield return Settle(4);
            yield return Sizes("p25_16b_world_atlas", "World atlas: regions, roads, the player's position, journeys.", map, (1920, 1080), (1280, 720));
            b.Hud.ToggleAtlas(false);
            yield return Settle(2);

            // ----- Into a town: character creation ----------------------------------------------------------------
            var c = Controller;
            var session = b.Session;
            c.ManageCursor = false;
            c.SetInput(new ScriptedExplorationInput());
            b.Selection.Select(town.SettlementId);
            Assert.That(b.Hud.EnterSelectedSettlement().IsSuccess, Is.True);
            yield return Settle(10);
            rt.Playback.Pause();
            var cam = c.CameraRig.Camera;
            if (c.OpenCreator())
            {
                yield return Settle(6);
                yield return Sizes("p25_03_character_creation", "Character creation: steps, choices from the game's data, the build so far.", cam, (1920, 1080), (1366, 768), (1024, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            Assert.That(session.Dispatch(new CreateCharacterCommand(LevelUpDriver.Choices("cleric"))).IsSuccess, Is.True);
            yield return Settle(4);
            yield return ItemDriver.Give(rt, c, session, null, ("longsword_plus1", 1, null), ("gold_ring", 1, null), ("potion_healing", 2, null));
            yield return Settle(8);
            c = Controller;
            cam = c.CameraRig.Camera;
            rt.Playback.Pause();

            // ----- The exploration HUD with vitals and a notice (equipping a ring through the real command) ----------
            c.CheckNotices();
            var ring = session.GetInventory(null, null, InventorySort.Category, false).Rows.First(r => r.ItemId == "gold_ring");
            Assert.That(session.Dispatch(new EquipItemCommand(ring.StackId, EquipmentSlot.RingLeft)).IsSuccess, Is.True);
            c.CheckNotices();
            c.RefreshVitals();
            yield return Settle(6);
            Log.Add("Notices raised by equipping the ring: " + string.Join(" | ", c.LastNotices.Select(n => n.ToString())));
            yield return Sizes("p25_06_exploration_hud", "Exploration HUD: place and time, vitals (hit points, armour, hunger, conditions), the action bar, a notice (the ring just equipped).", cam, All);

            c.OpenPauseMenu();
            yield return Settle(4);
            yield return Sizes("p25_25_pause_menu", "Pause menu (Esc): Resume, Save, Settings, Return to the world map, Main menu, Exit; recent notices beside it.", cam, (1920, 1080), (1024, 768));
            c.ClosePauseMenu();
            yield return Settle(2);

            // ----- Character sheet, abilities, level advancement ---------------------------------------------------------
            c.ToggleHub();
            yield return Settle(4);
            yield return Sizes("p25_04_character_sheet", "Character sheet: identity, experience, vitals, abilities and saves, with the figure.", cam, (1920, 1080), (1366, 768), (1024, 768));
            c.HubWindow.SelectCharacterPanel("Features");
            yield return Sizes("p25_15_abilities", "Abilities: class and species features, resources, spellcasting.", cam, (1920, 1080));
            c.HubWindow.SelectCharacterPanel("Sheet");

            // ----- Inventory, inspection, comparison, equipment --------------------------------------------------------
            c.HubWindow.Select("Inventory");
            yield return Settle(3);
            yield return Sizes("p25_08_inventory", "Inventory: icons, search, sort, groups, weight; nothing selected.", cam, (1920, 1080), (1366, 768), (1024, 768));
            var sword = session.GetInventory(null, null, InventorySort.Category, false).Rows.First(r => r.ItemId == "longsword_plus1");
            c.HubWindow.SelectItem(sword.StackId);
            yield return Settle(3);
            yield return Sizes("p25_09_item_inspection", "Item inspection: the shared inspector with the item's rules, description, and every action.", cam, (1920, 1080), (1280, 720));
            c.HubWindow.SelectItem(sword.StackId, EquipmentSlot.MainHand);
            yield return Settle(3);
            yield return Sizes("p25_10_equipment_comparison", "Equipment comparison through the rules engine: what changes if worn, in words as well as colour.", cam, (1920, 1080), (1024, 768));
            c.HubWindow.Select("Equipment");
            yield return Settle(3);
            yield return Sizes("p25_10b_equipment", "Equipment: totals, slots with icons, the inspector.", cam, (1920, 1080));

            c.HubWindow.Select("Spells & rest");
            yield return Settle(3);
            yield return Sizes("p25_14_spellbook", "Spellbook (a cleric): prepared spells, slots, filters; rest.", cam, (1920, 1080), (1366, 768));
            c.HubWindow.Select("Crafting & work");
            yield return Settle(3);
            yield return Sizes("p25_13_crafting", "Crafting and work: recipes with what they need, gathering, mending.", cam, (1920, 1080));
            c.HubWindow.Select("Journal");
            yield return Settle(3);
            yield return Sizes("p25_18_quest_journal", "Quest journal.", cam, (1920, 1080), (1366, 768));
            c.HubWindow.Select("Party");
            yield return Settle(3);
            yield return Sizes("p25_20_companions", "Party and companions.", cam, (1920, 1080));
            c.CloseWindows();
            yield return Settle(2);

            Assert.That(session.Dispatch(new AwardExperienceCommand(300, ExperienceSource.Milestone, reason: "Phase 25 capture", awardKey: "p25-capture-award")).IsSuccess, Is.True);
            yield return Settle(2);
            if (c.OpenAdvancement())
            {
                yield return Settle(4);
                yield return Sizes("p25_05_level_advancement", "Level advancement: what the new level brings and the choices still open (nothing chosen for the player).", cam, (1920, 1080), (1366, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            // ----- A merchant ----------------------------------------------------------------------------------------
            var shops = Object.FindObjectsByType<ShopInteriorView>(FindObjectsSortMode.None).OrderBy(s => s.ShopName, StringComparer.Ordinal).ToList();
            var shop = shops.FirstOrDefault(s => s.ShownTotal > 0) ?? shops.FirstOrDefault();
            if (shop != null)
            {
                c.OpenTrade(shop);
                yield return Settle(4);
                yield return Sizes("p25_12_merchant", $"Merchant: {shop.ShopName}'s stock, prices and the purse.", cam, (1920, 1080), (1366, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            // ----- Dialogue -------------------------------------------------------------------------------------------
            var npc = c.Residents.Keys.FirstOrDefault();
            if (!npc.IsEmpty && c.StartDialogue(npc).IsSuccess)
            {
                yield return Settle(4);
                yield return Sizes("p25_19_dialogue", "Dialogue with a resident: speaker, line, responses, what you know of them.", cam, (1920, 1080), (1366, 768));
                c.EndDialogue();
                yield return Settle(2);
            }

            // ----- Combat, loot, bestiary ---------------------------------------------------------------------------
            if (c.StartPracticeFight().IsSuccess)
            {
                c.AutoAi = false;
                yield return Settle(10);
                yield return Sizes("p25_07_combat_hud", "Combat HUD: turn order with health, the action economy, options, the log.", cam, (1920, 1080), (1366, 768), (2560, 1440));
                yield return Phase14Driver.FightOut(c);
                yield return Settle(6);
                c.CheckNotices();
                Log.Add("Notices after the fight: " + string.Join(" | ", c.LastNotices.Select(n => n.ToString())));
                c.ToggleHub();
                c.HubWindow.Select("Storage");
                yield return Settle(3);
                yield return Sizes("p25_11_loot_container", "Loot and containers: what the fight left, and the chest; take one or all.", cam, (1920, 1080));
                c.HubWindow.Select("Bestiary");
                var known = session.GetBestiary();
                if (known.Entries.Count > 0) c.HubWindow.SelectCreature(known.Entries.OrderByDescending(e => e.Defeated).First().CreatureId);
                yield return Settle(3);
                Log.Add($"Bestiary after the fight: {known.Known} kinds, {known.Studied} studied: " + string.Join(", ", known.Entries.Select(e => $"{e.Name} (met {e.Met}, defeated {e.Defeated})")));
                yield return Sizes("p25_22_bestiary", "Bestiary: only creatures met; a studied entry shows its strength, an unstudied one says what is not known yet.", cam, (1920, 1080), (1366, 768), (1024, 768));
                c.CloseWindows();
                yield return Settle(2);
            }

            // ----- The country and its region map ------------------------------------------------------------------
            if (c.IsInTownForCountry)
            {
                c.GoOutIntoCountry();
                for (var i = 0; i < 600 && c.Travelling; i++) yield return null;
                yield return Settle(10);
                rt.Playback.Pause();
                cam = c.CameraRig.Camera;
                yield return Sizes("p25_17_overland", "In the country: compass, waypoint line, ground conditions, vitals and the action bar.", cam, (1920, 1080), (1366, 768));
                c.ToggleRegionMap();
                yield return Settle(4);
                yield return Sizes("p25_17b_region_map", "The region map (N): found places, landmarks, roads, the player.", cam, (1920, 1080), (1024, 768));
                c.ToggleRegionMap();
            }

            Log.Add($"Notices shown in exploration: {c.NoticesShown}; last check {c.LastNoticeMs:0.00} ms.");
        }

        private static readonly (int, int)[] All = { (1920, 1080), (1366, 768), (1280, 720), (1024, 768), (2560, 1440), (3440, 1440) };

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

            for (var i = 0; i < 4; i++) yield return null; // the width classes follow the new size
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
