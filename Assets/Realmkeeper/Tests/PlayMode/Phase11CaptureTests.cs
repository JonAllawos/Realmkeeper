using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
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
    /// Evidence capture for Phase 11, not a pass/fail test: the inventory (all, filtered and sorted), an inspected magic
    /// item, a comparison through the rules engine, the equipment with rings, the off hand and attunement, a cursed
    /// item, the chest in the street and the Storage section, crafting and its outcome, an armourer's particular goods
    /// and mending, and the loot a fight leaves. Every screen is the shipped UI over a real world. Runs only when
    /// RK_CAPTURE_DIR is set.
    /// </summary>
    public sealed class Phase11CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const int Width = 1920;
        private const int Height = 1080;
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
            File.WriteAllLines(Path.Combine(_dir, "p11_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private IEnumerator Hub(ExplorationController c, string section, Camera camera, string name, string note)
        {
            if (!c.HubWindow.IsOpen) c.ToggleHub();
            c.HubWindow.Select(section);
            c.Preview?.RenderNow();
            for (var i = 0; i < 4; i++) yield return null;
            yield return Shoot(camera, name, note);
        }

        [UnityTest]
        public IEnumerator Capture1_TheItemScreens()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, w => w["player"]["purse"] = 400,
                ("potion_healing", 3, null), ("potion_greater_healing", 1, null), ("arrows", 30, null), ("longbow", 1, null),
                ("longsword_plus1", 1, null), ("chain_shirt_plus1", 1, null), ("ring_of_protection", 1, null), ("gold_ring", 1, null),
                ("cloak_of_protection", 1, null), ("dagger_iron", 1, null), ("wand_of_magic_missiles", 1, s => s["charges"] = 4),
                ("leatherworkers_tools", 1, null), ("cured_leather", 3, null), ("iron_ore", 6, null), ("hunters_stew", 2, null),
                ("shortsword", 1, s => s["condition"] = "Damaged"), ("mace_flanged", 1, s => s["condition"] = "Worn"), ("berserker_axe", 1, null));
            var camera = c.CameraRig.Camera;
            var page = session.GetInventory();
            Log.Add($"Pack: {page.TotalRows} rows, {page.CarriedGrams} g of {page.CapacityGrams} g capacity.");

            yield return Hub(c, "Inventory", camera, "p11_01_inventory", $"The inventory: {page.TotalRows} instances with their condition, make, charges and attunement; the load bar from SRD carrying capacity ({WorldSession.FormatWeight(page.CarriedGrams)} of {WorldSession.FormatWeight(page.CapacityGrams)}), the rules' AC and attack, search, group filters and sorting.");

            c.HubWindow.SetFilter("Weapons & armour", string.Empty, InventorySort.Value, true);
            yield return Hub(c, "Inventory", camera, "p11_02_inventory_filtered_sorted", "Filtered to weapons and armour, sorted by value (highest first); each row's value reflects its condition (the damaged shortsword is worth half).");

            var wand = page.Rows.First(r => r.ItemId == "wand_of_magic_missiles").StackId;
            c.HubWindow.SetFilter(null, string.Empty, InventorySort.Category, false);
            c.HubWindow.SelectItem(wand);
            yield return Hub(c, "Inventory", camera, "p11_03_inspect_wand", "Inspecting the Wand of Magic Missiles: its spell, charges (4 of 7) and recharge, rarity, weight, value and SRD 5.2.1 source.");

            var mail = page.Rows.First(r => r.ItemId == "chain_shirt_plus1").StackId;
            c.HubWindow.SelectItem(mail);
            var cmp = session.CompareItem(mail);
            yield return Hub(c, "Inventory", camera, "p11_04_compare_armour", $"Comparing the +1 chain shirt with what is worn ({cmp?.Current}), computed by the rules engine: " + string.Join("; ", cmp?.Lines.Select(l => $"{l.Label} {l.Before} → {l.After}") ?? new string[0]) + ".");

            // Wear things: the sword, the mail, rings on both hands, the dagger in the off hand, the cloak attuned.
            void Do(CommandResult r) => Log.Add((r.IsSuccess ? "ok: " : "refused: ") + r.Message);
            Do(session.Dispatch(new EquipItemCommand(page.Rows.First(r => r.ItemId == "longsword_plus1").StackId)));
            Do(session.Dispatch(new EquipItemCommand(mail)));
            Do(session.Dispatch(new EquipItemCommand(page.Rows.First(r => r.ItemId == "ring_of_protection").StackId)));
            Do(session.Dispatch(new EquipItemCommand(page.Rows.First(r => r.ItemId == "gold_ring").StackId, EquipmentSlot.RingRight)));
            Do(session.Dispatch(new EquipItemCommand(page.Rows.First(r => r.ItemId == "dagger_iron").StackId, EquipmentSlot.OffHand)));
            var cloak = page.Rows.First(r => r.ItemId == "cloak_of_protection").StackId;
            Do(session.Dispatch(new EquipItemCommand(cloak)));
            Do(session.Dispatch(StartWorkCommand.Attune(cloak)));
            Do(c.FinishWork(session.GetWork().Key));
            Do(session.Dispatch(StartWorkCommand.Attune(page.Rows.First(r => r.ItemId == "ring_of_protection").StackId)));
            Do(c.FinishWork(session.GetWork().Key));
            c.HubWindow.SelectItem(default);
            yield return Hub(c, "Equipment", camera, "p11_05_equipment", $"Equipment: the +1 longsword, a dagger in the off hand (two-weapon fighting), rings on both hands, the cloak and ring of protection attuned; AC {session.GetInventory().ArmorClass} from the rules ({session.GetInventory().ArmorClassText}).");

            var axe = page.Rows.First(r => r.ItemId == "berserker_axe").StackId;
            Do(session.Dispatch(new EquipItemCommand(axe)));
            Do(session.Dispatch(StartWorkCommand.Attune(axe)));
            Do(c.FinishWork(session.GetWork().Key));
            Do(session.Dispatch(new UnequipItemCommand(EquipmentSlot.MainHand)));
            c.HubWindow.SelectItem(axe);
            yield return Hub(c, "Equipment", camera, "p11_06_cursed_axe", "A cursed Berserker Axe, attuned and now bound: unequipping is refused with the reason, and the inspection names the curse (a shrine's priest can lift it).");
            c.CloseWindows();

            // The chest in the street.
            var chest = c.Containers.FirstOrDefault(x => x.ContainerKind == ContainerKind.Personal);
            if (chest != null)
            {
                var p = chest.InteractionPoint;
                var yaw = c.CameraRig.Camera.transform.eulerAngles.y;
                c.Player.Teleport(new LocalPose(p.x - 2.6f, p.y, p.z - 2.6f, 45f));
                c.CameraRig.SetView(45f, 16f);
                c.CameraRig.Snap();
                for (var i = 0; i < 12; i++) yield return null;
                yield return Shoot(camera, "p11_07_chest_in_the_street", $"{chest.DisplayName}: the player's chest stands by the tavern door; walking up to it and pressing E opens it in the hub's Storage section.");
                c.Player.Teleport(new LocalPose(p.x + 0.2f, p.y, p.z, 0f));
                for (var i = 0; i < 4; i++) yield return null;
                if (c.CurrentTarget == (object)chest) c.Interact();
                else c.OpenStorage(chest.Key);
                Do(session.Dispatch(new PutInContainerCommand(chest.Key, session.GetInventory().Rows.First(r => r.ItemId == "iron_ore").StackId, 6)));
                Do(session.Dispatch(new MoveContainerCoinCommand(chest.Key, -50)));
                yield return Hub(c, "Storage", camera, "p11_08_storage", "Storage: the chest (ore and coin left in it, its capacity) and the town's cache under a loose flagstone, filled once from its loot table.");
            }

            // Crafting: start, wait out, collect.
            Do(session.Dispatch(StartWorkCommand.Craft("stitch_jerkin")));
            yield return Hub(c, "Crafting & work", camera, "p11_09_crafting", "Crafting & work: recipes by discipline with materials held and needed, the tool and proficiency, the station, the DC and the chance; the jerkin in progress holds its leather.");
            Do(c.FinishWork(session.GetWork().Key));
            var site = session.GetWorkshop().Sites.FirstOrDefault(s => s.Problem == null);
            if (site != null)
            {
                Do(session.Dispatch(StartWorkCommand.Gather(site.Key)));
                Do(c.FinishWork(session.GetWork().Key));
            }

            yield return Hub(c, "Crafting & work", camera, "p11_10_work_outcomes", "After the work: the check rolled when it began (d20 + bonus against the DC), the outcome, the gathering site's stock fallen, mending and attunement listed.");
            c.CloseWindows();

            // An armourer: particular goods and mending.
            var smith = session.GetShops(session.GetCharacter().Player.SettlementId).FirstOrDefault(s => s.Categories.Contains(ShopCategory.WeaponsAndArmour));
            if (smith != null)
            {
                foreach (var m in new[] { 600, 900, 1020 })
                {
                    if (session.GetShopDetail(smith.ShopId).Summary.IsOpen) break;
                    var minute = m;
                    yield return ItemDriver.Give(Runtime, c, session, w => w["minuteOfDay"] = minute);
                }

                camera = c.CameraRig.Camera; // a reload rebuilds the scene and its camera
                c.OpenTrade(smith.ShopId);
                yield return null;
                var offer = session.GetShopDetail(smith.ShopId).Offers.FirstOrDefault(o => o.ItemId == "shortsword");
                if (offer != null && offer.Accepted) c.Sell(offer.StackId, offer.UnitPrice);
                for (var i = 0; i < 4; i++) yield return null;
                yield return Shoot(camera, "p11_11_armourer_goods_and_mending", "The armourer: the damaged shortsword just sold now sits among the particular goods at its own price; Mending lists the worn mace with the smith's price.");
                c.CloseWindows();
            }
        }

        [UnityTest]
        public IEnumerator Capture2_TheLootAFightLeaves()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var camera = c.CameraRig.Camera;
            c.AutoAi = false;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                LevelUpDriver.Click(c.Hud.Root, "xk-open-outing");
                for (var i = 0; i < 3; i++) yield return null;
                var w = c.Combat;
                for (var i = 0; i < 240 && c.InFight; i++)
                {
                    if (w.Prompt != null) LevelUpDriver.Click(w.Root, "xk-combat-prompt-decline");
                    else CombatDriver.Step(c);
                    yield return null;
                }

                if (w.Snapshot?.LootKey == null)
                {
                    Log.Add($"Outing {attempt + 1}: {w.Snapshot?.Outcome}, nothing left.");
                    c.CloseWindows();
                    if (c.PlayerIsDead) break;
                    continue;
                }

                for (var i = 0; i < 4; i++) yield return null;
                yield return Shoot(camera, "p11_12_fight_leaves_loot", $"The fight is over: the panel offers what the fallen left ({w.Snapshot.LootSummary}) — nothing went straight into the pack.");
                LevelUpDriver.Click(w.Root, "xk-combat-loot");
                for (var i = 0; i < 4; i++) yield return null;
                yield return Shoot(camera, "p11_13_loot_in_storage", "The loot opens in Storage: the weapons the foes carried (worn or damaged, as used) and their purses, kept in a container keyed by the encounter until taken.");
                yield break;
            }

            Log.Add("No outing left loot in three tries.");
        }

        private IEnumerator Shoot(Camera camera, string name, string note)
        {
            Log.Add(name + ": " + note);
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
