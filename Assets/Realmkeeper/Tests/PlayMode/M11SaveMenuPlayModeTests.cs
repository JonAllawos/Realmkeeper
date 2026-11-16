using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Presentation;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// M1.1 (ADR-0123): the release menu and save screens in the running game. Every test saves into its own temporary
    /// folder (<see cref="RealmkeeperRuntime.SaveRootOverride"/>), never the player's saves.
    /// </summary>
    public sealed class M11SaveMenuPlayModeTests : RealmkeeperPlayModeFixture
    {
        private string _dir;
        private bool _autosaveBefore;

        protected override void AddComponentsBeforeBootstrap(GameObject root)
        {
            _dir = Path.Combine(Path.GetTempPath(), "realmkeeper-m11-play-" + Guid.NewGuid().ToString("N"));
            RealmkeeperRuntime.SaveRootOverride = _dir;
            _autosaveBefore = RkSettings.Autosave;
            RkSettings.Autosave = true;
            root.AddComponent<ExplorationInstaller>();
        }

        [TearDown]
        public void CleanUp()
        {
            RealmkeeperRuntime.SaveRootOverride = null;
            RkSettings.Autosave = _autosaveBefore;
            RkHooks.TransitionInProgress = false;
            if (_dir != null && Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private JsonWorldStore Store => new JsonWorldStore(_dir);
        private TitleScreen Title => Bootstrap.Title;

        private static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        private VisualElement Modal => Title.Root.parent.Q<VisualElement>("rk-modal");

        /// <summary>
        /// Each test starts with the title closed. (When this class runs before any test has loaded the real scene, the test
        /// runner's own scene is in the build list and counts as the launch scene, so the title opens on start: a
        /// pre-existing, order-dependent behaviour recorded in the M1.1 report.)
        /// </summary>
        [UnitySetUp]
        public IEnumerator CloseTheTitle()
        {
            if (Title != null && Title.IsOpen) Title.Hide();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheLaunchMenu_IsTheReleaseMenu_WithDeveloperEntriesOnlyInDevelopmentBuilds()
        {
            Assert.That(Runtime.SaveRoot, Is.EqualTo(Path.GetFullPath(_dir)), "The test's own folder.");
            Title.Open(true);
            yield return null;
            var menu = Title.Root.Q<VisualElement>("title-menu");
            var labels = menu.Query<Button>().ToList().Where(b => b.resolvedStyle.display != DisplayStyle.None).Select(b => b.text).ToList();
            Assert.That(labels.Take(6), Is.EqualTo(new[] { "Continue", "New Game", "Load Game", "Settings", "Credits", "Exit Game" }));
            Assert.That(Title.SaveGameButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "No game to save at launch.");
            Assert.That(Title.ContinueButton.enabledSelf, Is.False, "Nothing saved yet.");
            Assert.That(Title.ContinueButton.tooltip, Does.Contain("No saved game"));
            Assert.That(TitleScreen.DeveloperBuild, Is.True, "The editor counts as a development build.");
            Assert.That(Title.NewWorldButton, Is.Not.Null, "Named, seeded worlds stay reachable for developers …");
            Assert.That(Title.CampaignsButton, Is.Not.Null, "… and campaigns.");
            Assert.That(menu.Q<Label>("title-developer"), Is.Not.Null, "Under a Developer heading.");
        }

        [UnityTest]
        public IEnumerator NewGame_WithThreeFullSlots_ChangesNoFileUntilConfirmed_AndKeepsTheReplacedSaveAsTheBackup()
        {
            foreach (var slot in SaveSlots.Manual)
            {
                Runtime.SelectSlot(slot);
                Assert.That(Runtime.Save(slot).IsSuccess, Is.True);
            }

            var before = Directory.GetFiles(_dir).ToDictionary(f => f, File.ReadAllText);
            var slot2 = before[Store.PathFor("slot-2")];
            Title.Open(true);
            yield return null;
            Click(Title.NewGameButton);
            yield return null;
            Assert.That(Title.Page, Is.EqualTo("newgame"));
            Assert.That(Title.Content.Q<Button>("slot-new-slot-2").text, Is.EqualTo("Replace"));
            Click(Title.Content.Q<Button>("slot-new-slot-2"));
            yield return null;
            Assert.That(Modal, Is.Not.Null, "An occupied slot asks first.");
            Assert.That(Modal.Query<Label>().ToList().Any(l => l.text.Contains("backup")), Is.True, "It says the old save is kept as the backup.");
            Click(Modal.Q<Button>("modal-cancel"));
            yield return null;
            Assert.That(Directory.GetFiles(_dir).ToDictionary(f => f, File.ReadAllText), Is.EqualTo(before), "Cancel: no file changed.");

            Click(Title.Content.Q<Button>("slot-new-slot-2"));
            yield return null;
            Click(Modal.Q<Button>("modal-confirm"));
            yield return Frames(4);
            Assert.That(Title.IsOpen, Is.False, "The new game begins.");
            Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("slot-2"));
            Assert.That(Bootstrap.Session.GetOverview().WorldName, Is.EqualTo(NewGameDefinition.WorldName));
            Assert.That(Bootstrap.Session.GetOverview().Seed, Is.EqualTo(NewGameDefinition.Seed));
            Assert.That(File.ReadAllText(Store.BackupPathFor("slot-2")), Is.EqualTo(slot2), "The replaced save is slot 2's backup.");
            Assert.That(File.ReadAllText(Store.PathFor("slot-1")), Is.EqualTo(before[Store.PathFor("slot-1")]), "Other slots untouched.");
            Assert.That(File.ReadAllText(Store.PathFor("slot-3")), Is.EqualTo(before[Store.PathFor("slot-3")]));
        }

        [UnityTest]
        public IEnumerator SaveGame_WritesTheChosenSlot_ConfirmsAnOverwrite_AndShowsTheDetails()
        {
            Title.Open(false);
            yield return null;
            Assert.That(Title.SaveGameButton.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "In a game, Save Game is offered.");
            Click(Title.SaveGameButton);
            yield return null;
            Assert.That(Title.Page, Is.EqualTo("save"));
            Click(Title.Content.Q<Button>("slot-save-slot-3"));
            yield return Frames(2);
            Assert.That(Store.Exists("slot-3"), Is.True, "An empty slot saves at once.");
            Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("slot-3"));
            var card = Title.Content.Q<VisualElement>("slot-card-slot-3");
            Assert.That(card.Q<Label>("slot-state-slot-3").text, Is.EqualTo("Ready"));
            Assert.That(card.Query<Label>().ToList().Any(l => l.text.StartsWith("Played ", StringComparison.Ordinal)), Is.True);

            var first = File.ReadAllText(Store.PathFor("slot-3"));
            Click(Title.Content.Q<Button>("slot-save-slot-3"));
            yield return null;
            Assert.That(Modal, Is.Not.Null, "A used slot asks first.");
            Click(Modal.Q<Button>("modal-confirm"));
            yield return Frames(2);
            Assert.That(File.ReadAllText(Store.BackupPathFor("slot-3")), Is.EqualTo(first));
        }

        [UnityTest]
        public IEnumerator Continue_LoadsTheMostRecentlyPlayed_AndOffersTheNextWhenThatFails_WithoutChangingFiles()
        {
            Runtime.SelectSlot("slot-2");
            Assert.That(Runtime.Save("slot-2").IsSuccess, Is.True);
            Directory.CreateDirectory(_dir);
            // Newer by its header, readable, but its world is broken: Continue tries it first and must fall back.
            File.WriteAllText(Store.PathFor("slot-1"), "{\n  \"format\": \"realmkeeper.world\",\n  \"schemaVersion\": 27,\n  \"savedAtUtc\": \"2099-01-01T00:00:00.0000000Z\",\n  \"world\": { \"worldName\": \"Broken\", \"seed\": 1, \"currentSimulationDate\": \"0412-03-01\" }\n}");
            var before = Directory.GetFiles(_dir).ToDictionary(f => f, f => File.ReadAllText(f) + File.GetLastWriteTimeUtc(f).Ticks);
            Assert.That(Title.ContinueCandidates().Select(s => s.Slot).Take(2), Is.EqualTo(new[] { "slot-1", "slot-2" }));

            Title.Open(true);
            yield return null;
            Click(Title.ContinueButton);
            yield return Frames(2);
            Assert.That(Modal, Is.Not.Null, "The failure is explained and the next save offered.");
            Assert.That(Title.Message.text, Does.Contain("could not be loaded"));
            Click(Modal.Q<Button>("modal-confirm"));
            yield return Frames(4);
            Assert.That(Title.IsOpen, Is.False);
            Assert.That(Runtime.ActiveSlot.Name, Is.EqualTo("slot-2"));
            Assert.That(Directory.GetFiles(_dir).Where(f => !f.EndsWith(".jpg", StringComparison.Ordinal)).ToDictionary(f => f, f => File.ReadAllText(f) + File.GetLastWriteTimeUtc(f).Ticks),
                Is.EqualTo(before.Where(p => !p.Key.EndsWith(".jpg", StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value)), "Continue never alters a save.");
        }

        [UnityTest]
        public IEnumerator Autosave_FiresAtAnArrival_WaitsSixtySeconds_SkipsTransitions_AndRotatesThroughThree()
        {
            var service = Bootstrap.Saves;
            service.Thumbnails = false;
            Runtime.ResetSaveClock();
            RkHooks.RaiseTransitionArrived("Test town");
            Assert.That(service.LastAutosaveSlot, Is.EqualTo("auto-1"), service.LastSkipReason);
            Assert.That(Store.Inspect("auto-1").Kind, Is.EqualTo(SaveSlotKind.Auto));
            Assert.That(Runtime.ActiveSlot.Name, Is.Not.EqualTo("auto-1"), "An autosave never becomes the slot Save Game writes.");

            RkHooks.RaiseTransitionArrived("Too soon");
            Assert.That(service.Autosaves, Is.EqualTo(1));
            Assert.That(service.LastSkipReason, Does.Contain("60 s"));

            Runtime.ResetSaveClock();
            RkHooks.TransitionInProgress = true;
            Assert.That(service.TryAutosave("mid-transition"), Is.False);
            RkHooks.TransitionInProgress = false;

            for (var i = 0; i < 3; i++)
            {
                yield return null;
                Runtime.ResetSaveClock();
                Assert.That(service.TryAutosave("rotation " + i), Is.True, service.LastSkipReason);
            }

            Assert.That(SaveSlots.Auto.All(Store.Exists), Is.True);
            Assert.That(service.LastAutosaveSlot, Is.EqualTo("auto-1"), "The fourth autosave replaces the oldest.");
            Assert.That(Directory.GetFiles(_dir, "auto-*" + JsonWorldStore.Extension).Length, Is.EqualTo(3), "Three are kept.");

            RkSettings.Autosave = false;
            Runtime.ResetSaveClock();
            Assert.That(service.TryAutosave("off"), Is.False, "The setting turns it off.");
        }

        [UnityTest]
        public IEnumerator PlayTime_Advances_InPlay_NotInMenus_AndSurvivesSaveAndLoad()
        {
            var t0 = Runtime.UnsavedPlaytime;
            yield return Frames(10);
            Assert.That(Runtime.UnsavedPlaytime, Is.GreaterThan(t0), "Counted while playing.");
            Title.Open(false);
            yield return null;
            var t1 = Runtime.UnsavedPlaytime;
            yield return Frames(10);
            Assert.That(Runtime.UnsavedPlaytime, Is.EqualTo(t1), "Not while a menu is open.");
            Title.ShowPage(null);
            Click(Title.ContinueButton);
            yield return Frames(3);

            Runtime.SelectSlot("slot-1");
            Assert.That(Runtime.Save("slot-1").IsSuccess, Is.True);
            var saved = Bootstrap.Session.GetPlaytimeSeconds();
            Assert.That(saved, Is.GreaterThan(0), "Recorded into the world at the save.");
            Assert.That(Runtime.UnsavedPlaytime, Is.LessThan(0.5), "The counter starts again after a save.");
            Assert.That(Store.Inspect("slot-1").PlaytimeSeconds, Is.EqualTo(saved).Within(1e-6));
            yield return Frames(10);
            Assert.That(Runtime.Load("slot-1").IsSuccess, Is.True);
            Assert.That(Bootstrap.Session.GetPlaytimeSeconds(), Is.EqualTo(saved).Within(1e-6), "Loaded back.");
        }
    }
}
