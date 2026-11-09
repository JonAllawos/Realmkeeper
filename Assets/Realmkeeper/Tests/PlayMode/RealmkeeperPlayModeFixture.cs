using System.Collections;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Builds the real Realmkeeper object graph (runtime, bootstrap, world visuals, camera,
    /// selection, HUD) in the test scene, with device input switched off so tests drive it
    /// directly. The runtime survives scene changes by design, so teardown destroys it
    /// explicitly: every test starts with no world session.
    /// </summary>
    public abstract class RealmkeeperPlayModeFixture
    {
        protected GameObject Root { get; private set; }

        protected RealmkeeperBootstrap Bootstrap { get; private set; }

        protected RealmkeeperRuntime Runtime => Bootstrap != null ? Bootstrap.Runtime : null;

        [UnitySetUp]
        public IEnumerator SetUpWorld()
        {
            Assert(Object.FindAnyObjectByType<RealmkeeperRuntime>() == null, "A runtime from an earlier test is still alive.");
            Root = new GameObject("Realmkeeper Test Root");
            AddComponentsBeforeBootstrap(Root);
            Bootstrap = Root.AddComponent<RealmkeeperBootstrap>();
            Bootstrap.CameraController.ReadInput = false;
            Bootstrap.Selection.ReadInput = false;
            yield return null; // let Start/Update run once and UI lay out
            yield return null;
        }

        /// <summary>Scene composition that the real scene places before the bootstrap (for example the exploration installer).</summary>
        protected virtual void AddComponentsBeforeBootstrap(GameObject root)
        {
        }

        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            if (Root != null) Object.Destroy(Root);
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            yield return null;
            yield return null; // asynchronous scene unloads finish
        }

        /// <summary>Clicks a UI Toolkit button the way keyboard/gamepad "submit" does.</summary>
        protected static void Click(Button button)
        {
            using (var evt = NavigationSubmitEvent.GetPooled())
            {
                evt.target = button;
                button.SendEvent(evt);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) NUnit.Framework.Assert.Fail(message);
        }
    }
}
