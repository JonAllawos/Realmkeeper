using System.Collections;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// Builds the real Realmkeeper object graph (bootstrap, world visuals, camera, selection,
    /// HUD) in the test scene, with device input switched off so tests drive it directly.
    /// </summary>
    public abstract class RealmkeeperPlayModeFixture
    {
        protected GameObject Root { get; private set; }

        protected RealmkeeperBootstrap Bootstrap { get; private set; }

        [UnitySetUp]
        public IEnumerator SetUpWorld()
        {
            Root = new GameObject("Realmkeeper Test Root");
            Bootstrap = Root.AddComponent<RealmkeeperBootstrap>();
            Bootstrap.CameraController.ReadInput = false;
            Bootstrap.Selection.ReadInput = false;
            yield return null; // let Start/Update run once and UI lay out
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownWorld()
        {
            if (Root != null) Object.Destroy(Root);
            yield return null;
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
    }
}
