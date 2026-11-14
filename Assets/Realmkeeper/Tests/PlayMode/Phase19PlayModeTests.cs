using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Exploration.Presentation;
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
    /// Phase 19: the redesigned interface, worked through its real controls (buttons are submitted, fields and
    /// dropdowns are given values the way the player gives them). Covers the title screen and its world flow, the
    /// confirmation dialog, the settings screen, runtime tooltips, the removed placeholders, and the shared icon
    /// language on the map, the atlas, the hub and the combat bar.
    /// </summary>
    public sealed class Phase19PlayModeTests : RealmkeeperPlayModeFixture
    {
        private const string TestWorld = "Phase Nineteen Test";
        private string _scaleBefore;

        protected override void AddComponentsBeforeBootstrap(GameObject root) => root.AddComponent<ExplorationInstaller>();

        private static JsonWorldStore Store => new JsonWorldStore(Path.Combine(UnityEngine.Application.persistentDataPath, RealmkeeperRuntime.SaveFolder));

        [SetUp]
        public void RememberPrefs() => _scaleBefore = PlayerPrefs.HasKey("rk.ui.scale") ? PlayerPrefs.GetFloat("rk.ui.scale").ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

        [TearDown]
        public void RestorePrefs()
        {
            if (_scaleBefore == null) PlayerPrefs.DeleteKey("rk.ui.scale");
            else PlayerPrefs.SetFloat("rk.ui.scale", float.Parse(_scaleBefore, System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            foreach (var s in Store.ListSaves().Where(s => s.Slot.StartsWith("phase-nineteen-test", System.StringComparison.Ordinal))) Store.Delete(s.Slot);
        }

        [UnityTest]
        public IEnumerator TheTitle_FoundsANamedWorld_ListsIt_AndDeletesIt_OnlyAfterConfirmation()
        {
            var title = Bootstrap.Title;
            Assert.That(title, Is.Not.Null);
            Assert.That(title.IsOpen, Is.False, "A test scene is not the launch scene: no title over the tests.");
            title.Open(true);
            yield return null;
            Assert.That(title.IsOpen, Is.True);
            Assert.That(Bootstrap.Hud.IsVisible, Is.False, "The map's HUD steps aside for the title.");

            Click(title.NewWorldButton);
            yield return null;
            Assert.That(title.Page, Is.EqualTo("new"));
            var name = title.Content.Q<TextField>("new-world-name");
            var seed = title.Content.Q<TextField>("new-world-seed");
            var create = title.Content.Q<Button>("new-world-create");
            name.value = "   ";
            yield return null;
            Assert.That(create.enabledSelf, Is.False, "A world needs a name.");
            name.value = TestWorld;
            seed.value = "not a number";
            Click(create);
            yield return null;
            Assert.That(title.Content.Q<Label>("new-world-problem").text, Does.Contain("whole number"));
            Assert.That(title.IsOpen, Is.True, "Nothing happened on bad input.");

            seed.value = "424242";
            Click(create);
            for (var i = 0; i < 6; i++) yield return null;
            Assert.That(title.IsOpen, Is.False, "Founding closes the title.");
            Assert.That(Bootstrap.Session.GetOverview().WorldName, Is.EqualTo(TestWorld));
            Assert.That(Bootstrap.Session.GetOverview().Seed, Is.EqualTo(424242));
            Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("phase-nineteen-test"), "The world gets its own slot from its name.");
            Assert.That(Store.Exists("phase-nineteen-test"), Is.True, "…and is saved at once, so it appears in the list.");
            Assert.That(Bootstrap.Hud.IsVisible, Is.True);

            title.Open(false);
            Assert.That(title.ContinueButton.text, Is.EqualTo("Resume"), "Opened in a game, the first entry resumes.");
            Click(title.LoadButton);
            yield return null;
            var card = title.Content.Q<VisualElement>("world-card-phase-nineteen-test");
            Assert.That(card, Is.Not.Null, "The new world is listed.");
            Assert.That(card.Q<Label>(className: "rk-h2").text, Is.EqualTo(TestWorld));

            Click(card.Q<Button>("world-delete-phase-nineteen-test"));
            yield return null;
            var modal = title.Root.parent.Q<VisualElement>("rk-modal");
            Assert.That(modal, Is.Not.Null, "Deleting asks first.");
            Click(modal.Q<Button>("modal-cancel"));
            yield return null;
            Assert.That(Store.Exists("phase-nineteen-test"), Is.True, "Cancel keeps the world.");

            Click(title.Content.Q<Button>("world-delete-phase-nineteen-test"));
            yield return null;
            Click(title.Root.parent.Q<VisualElement>("rk-modal").Q<Button>("modal-confirm"));
            yield return null;
            Assert.That(Store.Exists("phase-nineteen-test"), Is.False, "Confirmed: deleted.");
            Assert.That(title.Content.Q<VisualElement>("world-card-phase-nineteen-test"), Is.Null, "The list is refreshed.");

            Click(title.ContinueButton);
            yield return null;
            Assert.That(title.IsOpen, Is.False, "Resume closes the menu; the running world is untouched.");
            Assert.That(Bootstrap.Session.GetOverview().WorldName, Is.EqualTo(TestWorld));
        }

        [UnityTest]
        public IEnumerator TheTopBar_OpensSettings_AndTheScaleChangesTheWholeInterface()
        {
            var hud = Bootstrap.Hud;
            Assert.That(hud.Root.Q<VisualElement>("nav-later"), Is.Null, "The 'Coming later' placeholders are gone.");
            Assert.That(hud.Root.Query<Button>().ToList().Any(b => b.text == "Coming later" || !b.enabledSelf && b.ClassListContains("rk-nav-item")), Is.False);
            Assert.That(RkIcons.LeadOf(hud.PlayPauseButton), Is.EqualTo("play"));

            Click(hud.SettingsButton);
            yield return null;
            var settings = hud.Settings;
            Assert.That(settings != null && settings.IsOpen, Is.True);
            settings.UiScale.index = System.Array.IndexOf(RkSettings.Scales, 1.3f);
            yield return null;
            Assert.That(RkSettings.UiScaleChoice, Is.EqualTo(1.3f));
            Assert.That(RkSettings.Panel.scale, Is.EqualTo(1.3f).Within(0.001f), "One shared panel: every screen scales.");
            Assert.That(settings.Status.text, Does.Contain("130"));
            settings.UiScale.index = 0;
            yield return null;
            Assert.That(RkSettings.Panel.scale, Is.EqualTo(RkSettings.EffectiveFor(0f, Screen.height)).Within(0.001f), "Auto again.");
            Click(settings.CloseButton);
            yield return null;
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(RkSettingsWindow.NotYet.Any(t => t.StartsWith("Game controllers", System.StringComparison.Ordinal)), Is.True, "What does not exist is stated, not faked."); // Phase 25: the stale "no sound yet" line was removed (sound exists since Phase 20A)
            Assert.That(RkSettingsWindow.NotYet.Any(t => t.StartsWith("Audio", System.StringComparison.Ordinal)), Is.False, "Sound exists, so it is not listed as missing.");
        }

        [Test]
        public void EveryIconMeaning_LoadsItsTexture()
        {
            var missing = RkIcons.Files.Keys.Where(k => !RkIcons.Has(k)).ToList();
            Assert.That(missing, Is.Empty, "Icons missing from Resources/RkIcons: " + string.Join(", ", missing));
            Assert.That(RkIcons.Files.Count, Is.GreaterThanOrEqualTo(150));
        }

        [UnityTest]
        public IEnumerator Tooltips_WrittenSincePhase2_AreShownAtRunTime()
        {
            var hud = Bootstrap.Hud;
            var politics = hud.Root.Q<Button>("toggle-politics");
            Assert.That(politics.tooltip, Is.Not.Empty);
            hud.Tooltips.ShowFor(politics, politics.worldBound.center);
            yield return null;
            Assert.That(hud.Tooltips.Shown, Is.EqualTo(politics.tooltip));
            hud.Tooltips.Hide();
            Assert.That(hud.Tooltips.Shown, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheModal_RunsItsActionOnce_AndEscapeCancels()
        {
            var host = Bootstrap.Hud.Root;
            var runs = 0;
            var m = RkModal.Confirm(host, "Test", "Body", "Do it", () => runs++);
            yield return null;
            using (var e = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { e.target = m.Scrim; m.Scrim.SendEvent(e); }
            yield return null;
            Assert.That(m.IsOpen, Is.False);
            Assert.That(runs, Is.EqualTo(0), "Escape cancels.");
            m = RkModal.Confirm(host, "Test", "Body", "Do it", () => runs++);
            Click(m.ConfirmButton);
            m.Confirm();
            Assert.That(runs, Is.EqualTo(1), "Confirmed once, run once.");
        }

        [UnityTest]
        public IEnumerator TheAtlasLayers_AreIconChips_AndTheMapIsDrawnWithSymbols()
        {
            var hud = Bootstrap.Hud;
            hud.ToggleAtlas(true);
            yield return null;
            var terrain = hud.Root.Q<Button>("rk-atlas-layer-Terrain");
            Assert.That(RkIcons.LeadOf(terrain), Is.EqualTo("terrain"));
            var wasOn = hud.AtlasLayersOn.Contains("Terrain");
            Click(terrain);
            Assert.That(hud.AtlasLayersOn.Contains("Terrain"), Is.Not.EqualTo(wasOn), "A layer chip toggles its layer.");
            Click(terrain);
            yield return null;
            Assert.That(hud.Root.Query<VisualElement>(className: "rk-atlas-stamp").ToList().Count, Is.GreaterThan(0), "Settlements and terrain are drawn with map symbols.");
            hud.ToggleAtlas(false);

        }
    }

    /// <summary>Phase 19, in the real scene: the hub's tabs and the combat bar use the same icon language, and still work.</summary>
    public sealed class Phase19ScenePlayModeTests
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
        }

        private static void Click(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator TheHubTabs_AndTheCombatBar_SpeakTheSameIconLanguage()
        {
            var Bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var Runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            Assert.That(Bootstrap.Title.IsOpen, Is.False, "Loaded by a test, not launched: no title.");
            var c = Object.FindAnyObjectByType<ExplorationInstaller>().Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            c.ToggleHub();
            for (var i = 0; i < 4; i++) yield return null;
            Assert.That(c.HubWindow, Is.Not.Null);
            foreach (var section in new[] { "Character", "Inventory", "Journal", "Spells & rest" })
                Assert.That(RkIcons.LeadOf(c.HubWindow.Root.Q<Button>("xk-hub-tab-" + section)), Is.EqualTo(PlayerHubWindow.HubIcon(section)));
            Click(c.HubWindow.Root.Q<Button>("xk-hub-tab-Inventory"));
            yield return null;
            Assert.That(c.HubWindow.Section, Is.EqualTo("Inventory"), "Tabs still work through their buttons.");
            c.CloseWindows();

            Assert.That(c.StartPracticeFight().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            foreach (var (slot, icon) in new[] { ("action", "action"), ("bonus", "bonus"), ("reaction", "reaction") })
            {
                var l = c.Hud.Root.Q<Label>("xk-cbt-slot-" + slot);
                Assert.That(l, Is.Not.Null, slot);
                Assert.That(RkIcons.LeadOf(l), Is.EqualTo(icon), "Action, bonus and reaction differ by icon, not colour alone.");
                Assert.That(l.layout.width, Is.GreaterThan(40f), "The pill keeps room for its word.");
            }
        }
    }
}
