using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
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
    /// Cleanup and performance pass (2026-11-15): the player hub renders once per action, not once for the world change
    /// and again for the action's message; a world change with the hub open (time passing) still refreshes it.
    /// Logs the measured renders and their time per click (editor, batch mode).
    /// </summary>
    public sealed class CleanupPerformancePlayModeTests
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

        [UnityTest]
        public IEnumerator TheHub_RendersOncePerAction_AndStillFollowsTheWorld()
        {
            var session = Bootstrap.Session;
            var c = Controller;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return ItemDriver.Give(Runtime, c, session, null, ("dagger_iron", 1, null));

            if (!c.HubWindow.IsOpen) c.ToggleHub();
            var hub = c.HubWindow;
            yield return null;
            yield return null;

            // Idle with the hub open: how often does the world alone re-render it?
            var idleFrom = c.HubRenders;
            var idleUntil = Time.realtimeSinceStartup + 2f;
            var idleFrames = 0;
            while (Time.realtimeSinceStartup < idleUntil) { idleFrames++; yield return null; }
            Debug.Log($"Realmkeeper cleanup: idle with the hub open, {c.HubRenders - idleFrom} renders in {idleFrames} frames (2 s).");

            // Ten equip / unequip clicks through the real buttons.
            const int Clicks = 10;
            var renders = c.HubRenders;
            var ms = c.HubRenderMilliseconds;
            var perClick = new List<int>();
            for (var i = 0; i < Clicks; i++)
            {
                var before = c.HubRenders;
                if (i % 2 == 0)
                {
                    hub.Select("Inventory");
                    yield return null;
                    before = c.HubRenders;
                    ItemDriver.ClickText(ItemDriver.ItemRow(hub, "dagger_iron"), "Equip");
                }
                else
                {
                    hub.Select("Equipment");
                    yield return null;
                    before = c.HubRenders;
                    ItemDriver.ClickText(hub.Content.Q<VisualElement>("xk-hub-slot-MainHand"), "Unequip");
                }

                // What the click itself renders (synchronously). Before the fix: 2 (the world change's refresh, then the
                // action's own render with its message). Later frames may add the clock's periodic refresh, which is allowed.
                perClick.Add(c.HubRenders - before);
                yield return null;
                yield return null;
            }

            var total = c.HubRenders - renders;
            var time = c.HubRenderMilliseconds - ms;
            Debug.Log($"Realmkeeper cleanup: hub renders per click {string.Join(",", perClick)}; {total} renders for {Clicks} clicks, {time:F1} ms in all, {time / Mathf.Max(1, total):F2} ms each.");
            Assert.That(session.GetInventory().Equipped.ContainsKey(EquipmentSlot.MainHand) ? session.GetInventory().Equipped[EquipmentSlot.MainHand].ItemId : null, Is.Not.EqualTo("dagger_iron"), "Ten clicks: equipped five times, unequipped five times.");
            Assert.That(perClick.All(n => n == 1), Is.True, "One full render per action: " + string.Join(",", perClick));
            Assert.That(hub.LastMessage, Is.Not.Empty, "The action's message is kept.");

            // A world change with no action behind it (time passing) still refreshes the open hub, once.
            var shown = c.HubRenders;
            Assert.That(session.Execute(new AdvanceTimeCommand(30)).IsSuccess, Is.True);
            Assert.That(c.HubRenders - shown, Is.EqualTo(0), "Deferred to the end of the frame, not rendered inside the command.");
            yield return null;
            Assert.That(c.HubRenders - shown, Is.GreaterThanOrEqualTo(1), "The open hub follows the world.");
        }
    }
}
