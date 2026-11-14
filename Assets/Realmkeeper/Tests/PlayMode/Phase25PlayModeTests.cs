using System.Collections;
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
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Phase 25 in the shipped scene, through the real interface. It covers:
    /// <list type="bullet">
    /// <item>the pause menu: it stops the clock and movement, Save writes the slot, Exit asks first, Resume restores;</item>
    /// <item>notices from real commands, shown once and not repeated;</item>
    /// <item>the vitals panel against the character;</item>
    /// <item>the HUD stepping aside for a window;</item>
    /// <item>the inventory's inspector: Drop changes the real inventory, once;</item>
    /// <item>the bestiary after a fight;</item>
    /// <item>the accessibility and width classes;</item>
    /// <item>interface sounds;</item>
    /// <item>the strategic map's notices and the main-menu hook.</item>
    /// </list>
    /// </summary>
    public sealed class Phase25PlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private const string Slot = "p25-test";
        private InputSettings.BackgroundBehavior _background;
        private float _text;
        private bool _contrast;
        private bool _motion;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
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
            RkSettings.TextSize = _text;
            RkSettings.HighContrast = _contrast;
            RkSettings.ReducedMotion = _motion;
            InputSystem.settings.backgroundBehavior = _background;
            var store = new Realmkeeper.Infrastructure.Persistence.JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder));
            if (store.Exists(Slot)) store.Delete(Slot);
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static IEnumerator Frames(int n = 4)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private static void Click(Button b)
        {
            Assert.That(b, Is.Not.Null);
            Assert.That(b.enabledInHierarchy, Is.True, b.name + " is disabled: " + b.tooltip);
            using (var click = NavigationSubmitEvent.GetPooled()) { click.target = b; b.SendEvent(click); }
        }

        [UnityTest]
        public IEnumerator TheOverhauledInterface_RunsTheGamesOwnActions_AndShowsOnlyRealData()
        {
            var b = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var rt = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return Frames(10);
            Assert.That(RkHooks.OpenMainMenu, Is.Not.Null, "The bootstrap offers its main menu to exploration.");
            Assert.That(b.Hud.Toasts, Is.Not.Null, "The strategic map has a notice stack.");
            rt.SelectSlot(Slot);
            yield return CombatDriver.EnterTownAsFighter(b, rt, c);
            yield return Frames(6);
            var s = b.Session;
            var hud = c.Hud;

            // Vitals: the numbers are the character's own.
            c.RefreshVitals();
            var ch = s.GetCharacter();
            Assert.That(hud.Vitals.ClassListContains("xk-hidden"), Is.False);
            Assert.That(hud.VitalsLine.text, Does.StartWith($"{ch.Player.Rpg.CurrentHp} / {ch.Rules.MaxHp} HP · AC {ch.ArmorClass.Total}"));
            Assert.That(hud.VitalsName.text, Is.EqualTo(ch.FullName));

            // Notices: a real command raises one; the same change is not announced twice.
            c.CheckNotices();
            var shown = c.NoticesShown;
            var chest = s.GetInventory().Equipped[EquipmentSlot.Chest];
            Assert.That(s.Dispatch(new UnequipItemCommand(EquipmentSlot.Chest)).IsSuccess, Is.True);
            c.CheckNotices();
            Assert.That(c.NoticesShown, Is.EqualTo(shown + 1));
            Assert.That(c.LastNotices.Last().Title, Does.StartWith("Taken off: " + chest.Name));
            yield return Frames(2);
            Assert.That(hud.Toasts.Visible, Is.GreaterThanOrEqualTo(1));
            c.CheckNotices();
            Assert.That(c.NoticesShown, Is.EqualTo(shown + 1), "Nothing changed, nothing new.");
            Assert.That(s.Dispatch(new EquipItemCommand(chest.StackId)).IsSuccess, Is.True);
            c.CheckNotices();

            // The pause menu: real actions, the clock and the body held while it is open.
            rt.Playback.Play();
            c.OpenPauseMenu();
            yield return Frames(2);
            Assert.That(c.PauseOpen, Is.True);
            Assert.That(rt.Playback.IsPlaying, Is.False, "The clock stops.");
            Assert.That(c.Player.MovementEnabled, Is.False);
            Assert.That(hud.WindowOpen, Is.True, "The HUD steps aside.");
            var root = c.Pause.Root;
            foreach (var name in new[] { "resume", "save", "settings", "map", "main", "exit" }) Assert.That(root.Q<Button>("pause-" + name), Is.Not.Null, name);
            Click(root.Q<Button>("pause-exit"));
            yield return null;
            Assert.That(root.Q<VisualElement>("rk-modal"), Is.Not.Null, "Exit asks first.");
            Click(root.Q<Button>("modal-cancel"));
            yield return null;
            Assert.That(c.PauseOpen, Is.True, "Cancelling keeps the menu.");
            Click(root.Q<Button>("pause-settings"));
            yield return null;
            Assert.That(c.PauseSettings.IsOpen, Is.True);
            Assert.That(c.PauseSettings.TextSize, Is.Not.Null);
            c.PauseSettings.Close();
            Click(root.Q<Button>("pause-save"));
            yield return Frames(2);
            Assert.That(c.PauseOpen, Is.False, "Saving closes the menu.");
            Assert.That(rt.Playback.IsPlaying, Is.True, "The clock runs again.");
            Assert.That(new Realmkeeper.Infrastructure.Persistence.JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder)).Exists(Slot), Is.True, "Save wrote the slot.");
            Assert.That(hud.Toasts.History.First().Title, Is.EqualTo("Saved"));
            rt.Playback.Pause();
            c.OpenPauseMenu();
            yield return null;
            Click(c.Pause.Root.Q<Button>("pause-resume"));
            yield return null;
            Assert.That(c.PauseOpen, Is.False);
            Assert.That(c.Player.MovementEnabled, Is.True, "Movement is given back.");
            Assert.That(rt.Playback.IsPlaying, Is.False, "Resume restores the clock as it was (paused).");

            // A window: the HUD steps aside and returns.
            c.ToggleHub();
            yield return Frames(2);
            Assert.That(hud.WindowOpen, Is.True);
            Assert.That(c.HubWindow.Root.Q<Button>("xk-hub-tab-Bestiary"), Is.Not.Null);

            // The inventory inspector: Drop takes exactly one from the real inventory.
            c.HubWindow.Select("Inventory");
            yield return null;
            var bread = s.GetInventory().Rows.First(r => r.ItemId == "bread" || r.Quantity > 1);
            var before = bread.Quantity;
            Click(c.HubWindow.Content.Q<VisualElement>("xk-hub-item-" + bread.ItemId).Query<Button>().ToList().First(x => x.text == "Inspect"));
            yield return null;
            var card = c.HubWindow.Content.Q<VisualElement>("xk-inv-details");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Q<Label>("xk-inv-details-title").text, Is.EqualTo(bread.Name));
            Click(card.Q<Button>("xk-inv-act-drop"));
            yield return Frames(2);
            Assert.That(s.GetInventory().Rows.Where(r => r.ItemId == bread.ItemId).Sum(r => r.Quantity), Is.EqualTo(before - 1), "One dropped, no more, no less.");
            c.CloseWindows();
            yield return Frames(2);
            Assert.That(hud.WindowOpen, Is.False, "The HUD is back.");

            // The bestiary after a fight: only what was met.
            Assert.That(c.StartPracticeFight(false).IsSuccess, Is.True);
            yield return Phase14Driver.FightOut(c);
            yield return Frames(4);
            var beasts = s.GetBestiary();
            Assert.That(beasts.Entries, Is.Not.Empty);
            c.ToggleHub();
            c.HubWindow.Select("Bestiary");
            yield return null;
            foreach (var e in beasts.Entries) Assert.That(c.HubWindow.Content.Q<VisualElement>("xk-beast-" + e.CreatureId), Is.Not.Null, e.Name);
            c.HubWindow.SelectCreature(beasts.Entries[0].CreatureId);
            yield return null;
            Assert.That(c.HubWindow.Content.Q<VisualElement>("xk-beast-card"), Is.Not.Null);
            c.CloseWindows();

            // Accessibility and width classes reach the documents.
            RkSettings.TextSize = 1.3f;
            RkSettings.HighContrast = true;
            RkSettings.ReducedMotion = true;
            yield return null;
            var docRoot = hud.DocumentRoot;
            Assert.That(docRoot.ClassListContains("rk-text-130"), Is.True);
            Assert.That(docRoot.ClassListContains("rk-contrast"), Is.True);
            Assert.That(docRoot.ClassListContains("rk-reduced-motion"), Is.True);
            RkSettings.TextSize = 1f;
            RkSettings.HighContrast = false;
            yield return null;
            Assert.That(docRoot.ClassListContains("rk-text-130"), Is.False);
            var probe = new VisualElement();
            probe.style.width = 1000;
            probe.style.height = 700;
            probe.style.position = Position.Absolute;
            docRoot.Add(probe);
            RkRoot.Attach(probe);
            yield return Frames(2);
            Assert.That(RkRoot.WidthClass(probe), Is.EqualTo("narrow"));
            Assert.That(probe.ClassListContains("rk-h-short"), Is.True);
            probe.RemoveFromHierarchy();

            // Interface sounds: a click is heard (counted even in batch mode, where nothing plays).
            RkUiSound.ResetCounters();
            c.ToggleHub();
            yield return null;
            using (var e = ClickEvent.GetPooled()) { var tab = c.HubWindow.Root.Q<Button>("xk-hub-tab-Inventory"); e.target = tab; tab.SendEvent(e); }
            yield return null;
            Assert.That(RkUiSound.Requested, Is.GreaterThanOrEqualTo(1));
            Assert.That(RkUiSound.Last, Is.EqualTo("select"));
            c.CloseWindows();
        }
    }
}
