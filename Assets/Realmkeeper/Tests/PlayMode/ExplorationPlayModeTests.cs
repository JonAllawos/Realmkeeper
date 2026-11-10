using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// The ADR-0009 vertical slice end to end in Play Mode: enter a settlement from the map,
    /// walk, switch perspective, interact, save and reload inside, and return to the map,
    /// all on the one world session. Input is scripted; nothing here reads devices.
    /// </summary>
    public sealed class ExplorationPlayModeTests : RealmkeeperPlayModeFixture
    {
        private const string TestSlot = "playmode-exploration-test";

        private ExplorationInstaller _installer;
        private ScriptedExplorationInput _input;

        private ExplorationController Controller => _installer.Controller;
        private WorldSession Session => Bootstrap.Session;

        protected override void AddComponentsBeforeBootstrap(GameObject root)
        {
            _installer = root.AddComponent<ExplorationInstaller>();
            _installer.Controller.ManageCursor = false;
            _input = new ScriptedExplorationInput();
            _installer.Controller.SetInput(_input);

            // Fixed frame time: batch-mode frames are otherwise arbitrarily short, which would make
            // "walk for N frames" meaningless.
            Time.captureDeltaTime = 1f / 30f;
        }

        [TearDown]
        public void ResetFrameTime()
        {
            Time.captureDeltaTime = 0f;
        }

        private static bool AnyExplorationSceneLoaded()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name.StartsWith("Exploration")) return true;
            }

            return false;
        }

        private SettlementSummary FirstSettlement => Session.GetSettlements().OrderBy(s => s.SettlementId.ToString()).First();

        private IEnumerator Enter(SettlementSummary settlement)
        {
            Bootstrap.Selection.Select(settlement.SettlementId);
            var result = Bootstrap.Hud.EnterSelectedSettlement();
            Assert.That(result.IsSuccess, Is.True, result.ToString());
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        private IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
                _input.ClearPresses();
            }
        }

        [UnityTest]
        public IEnumerator EnterSettlement_LoadsItsEnvironmentByIdAndHidesTheMap()
        {
            var s = FirstSettlement;
            yield return Enter(s);

            Assert.That(Controller.IsExploring, Is.True);
            Assert.That(Controller.Streamer.Current.SettlementId, Is.EqualTo(s.SettlementId));
            Assert.That(Controller.Streamer.Current.RegionId, Is.EqualTo(s.RegionId));
            Assert.That(Controller.Streamer.Current.RegionId.IsEmpty, Is.False);
            Assert.That(Controller.Streamer.Scene.IsValid() && Controller.Streamer.Scene.isLoaded, Is.True);
            Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Exploration));
            Assert.That(Bootstrap.StrategicVisible, Is.False);
            Assert.That(Bootstrap.CameraController.gameObject.activeInHierarchy, Is.False);
            Assert.That(Bootstrap.WorldView.gameObject.activeInHierarchy, Is.False);
            Assert.That(Bootstrap.Hud.IsVisible, Is.False);
            Assert.That(Controller.CameraRig.Camera.enabled, Is.True);
            Assert.That(Controller.Hud.SettlementLabel.text, Is.EqualTo(s.Name));
            // Region, then who governs the settlement: the same political data the map shows.
            var politics = Session.GetSettlementPolitics(s.SettlementId);
            Assert.That(Controller.Hud.RegionLabel.text, Does.StartWith(Session.FindRegion(s.RegionId).Name));
            Assert.That(Controller.Hud.RegionLabel.text, Does.EndWith(politics.IsIndependent ? "independent" : "governed by " + politics.GoverningFactionName));

            var player = Session.GetPlayer();
            Assert.That(player.SettlementId, Is.EqualTo(s.SettlementId));
            Assert.That(Controller.Player.transform.position.z, Is.EqualTo(player.Pose.Z).Within(0.05f), "Arrives at the gate.");
        }

        [UnityTest]
        public IEnumerator ReturnToMap_RestoresTheMapAndSelectsTheSettlementLeft()
        {
            var s = FirstSettlement;
            var dateBefore = Session.GetOverview().Date;
            yield return Enter(s);
            Bootstrap.Selection.Clear();

            Assert.That(Controller.ReturnToWorldMap().IsSuccess, Is.True);
            yield return null;
            yield return null;

            Assert.That(Controller.IsExploring, Is.False);
            Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Strategic));
            Assert.That(Bootstrap.StrategicVisible, Is.True);
            Assert.That(Bootstrap.CameraController.gameObject.activeInHierarchy, Is.True);
            Assert.That(Bootstrap.Hud.IsVisible, Is.True);
            Assert.That(Bootstrap.Selection.SelectedId, Is.EqualTo(s.SettlementId));
            for (var i = 0; i < 20 && AnyExplorationSceneLoaded(); i++) yield return null;
            Assert.That(AnyExplorationSceneLoaded(), Is.False, "The exploration scene is unloaded.");
            Assert.That(Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None), Is.Empty);

            var player = Session.GetPlayer();
            Assert.That(player.LocationKind, Is.EqualTo(CharacterLocationKind.WorldMap));
            Assert.That(player.LastSettlementId, Is.EqualTo(s.SettlementId));
            Assert.That(Session.GetOverview().Date, Is.EqualTo(dateBefore), "Same world, same day: nothing was regenerated.");
            Assert.That(Session.GetSettlements().Count, Is.EqualTo(Bootstrap.WorldView.Settlements.Count));
        }

        [UnityTest]
        public IEnumerator RepeatedEntry_NeverDuplicatesResidentsOrEnvironments()
        {
            var s = FirstSettlement;
            var residents = Session.GetResidents(s.SettlementId);
            Assert.That(residents.Count, Is.GreaterThan(0));

            for (var i = 0; i < 3; i++)
            {
                yield return Enter(s);
                var views = Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None);
                Assert.That(views.Length, Is.EqualTo(residents.Count), $"Visit {i + 1}");
                Assert.That(views.Select(v => v.NpcId).Distinct().Count(), Is.EqualTo(views.Length));
                Assert.That(views.Select(v => v.NpcId), Is.EquivalentTo(residents.Select(r => r.NpcId)));
                Assert.That(Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<ExplorationCameraRig>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

                Assert.That(Controller.ReturnToWorldMap().IsSuccess, Is.True);
                yield return null;
                yield return null;
            }

            Assert.That(Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None), Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnteringTwice_IsRefusedWithoutReloading()
        {
            var s = FirstSettlement;
            yield return Enter(s);
            var loads = Controller.Streamer.LoadCount;

            var again = Bootstrap.Hud.EnterSelectedSettlement();
            yield return null;

            Assert.That(again.Status, Is.EqualTo(CommandStatus.InvalidSessionState));
            Assert.That(Controller.Streamer.LoadCount, Is.EqualTo(loads));
        }

        [UnityTest]
        public IEnumerator Character_WalksWithScriptedInput_StaysGrounded_AndJumps()
        {
            yield return Enter(FirstSettlement);
            yield return Frames(10); // settle onto the ground
            var start = Controller.Player.transform.position;
            Assert.That(Controller.Player.IsGrounded, Is.True);

            _input.Move = new Vector2(0f, 1f);
            for (var i = 0; i < 60; i++) yield return null;
            _input.Move = Vector2.zero;
            var walked = Controller.Player.transform.position;
            Assert.That(walked.z - start.z, Is.GreaterThan(1f), "Walked north from the gate.");
            Assert.That(Mathf.Abs(walked.y - start.y), Is.LessThan(0.2f), "Stayed on the ground.");

            _input.JumpPressed = true;
            var peak = walked.y;
            for (var i = 0; i < 40; i++)
            {
                yield return null;
                _input.ClearPresses();
                peak = Mathf.Max(peak, Controller.Player.transform.position.y);
            }

            Assert.That(peak - walked.y, Is.GreaterThan(0.4f), "Jumped.");
            Assert.That(Controller.Player.transform.position.y, Is.EqualTo(walked.y).Within(0.15f), "Landed again.");
        }

        [UnityTest]
        public IEnumerator SwitchingPerspective_KeepsTheCharacterWhereItIs()
        {
            yield return Enter(FirstSettlement);
            yield return Frames(5);
            _input.Move = new Vector2(0.4f, 1f);
            for (var i = 0; i < 30; i++) yield return null;
            _input.Move = Vector2.zero;
            yield return null;
            var before = Controller.Player.transform.position;

            Assert.That(Controller.TogglePerspective().IsSuccess, Is.True);
            yield return null;

            Assert.That(Controller.CameraRig.Perspective, Is.EqualTo(CameraPerspective.FirstPerson));
            Assert.That(Session.GetPlayer().Perspective, Is.EqualTo(CameraPerspective.FirstPerson));
            Assert.That(Vector3.Distance(Controller.Player.transform.position, before), Is.LessThan(0.01f));
            Assert.That(Controller.Player.Body.activeSelf, Is.False, "Body hidden in first person.");
            Assert.That(Controller.CameraRig.transform.position.y - before.y, Is.EqualTo(Controller.CameraRig.FirstPerson.PivotHeight).Within(0.05f));
            var pose = Session.GetPlayer().Pose;
            Assert.That(new Vector3(pose.X, pose.Y, pose.Z), Is.EqualTo(before).Using(new Vector3EqualityComparer(0.01f)), "Position recorded with the switch.");

            Assert.That(Controller.TogglePerspective().IsSuccess, Is.True);
            yield return null;
            Assert.That(Controller.CameraRig.Perspective, Is.EqualTo(CameraPerspective.ThirdPerson));
            Assert.That(Controller.Player.Body.activeSelf, Is.True);
            Assert.That(Vector3.Distance(Controller.Player.transform.position, before), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator ThirdPersonCamera_IsPulledInFrontOfWalls()
        {
            yield return Enter(FirstSettlement);
            var layout = Controller.Streamer.Current;
            var hall = layout.Buildings.First(b => b.Kind == BuildingKind.Hall);
            // Inside the hall, just north of the front (south) wall, looking north: the camera's
            // desired spot 4 m behind is outside, beyond the wall.
            var inside = new LocalPose(hall.Center.X - 3f, 0.1f, hall.Center.Z - hall.Depth / 2f + 1.2f, 0f);
            Controller.Player.Teleport(inside);
            Controller.CameraRig.SetView(0f, 0f);
            yield return null;
            Physics.SyncTransforms();
            Controller.CameraRig.Snap();

            Assert.That(Controller.CameraRig.CurrentDistance, Is.LessThan(Controller.CameraRig.ThirdPerson.Distance - 1f));
            Assert.That(Controller.CameraRig.transform.position.z, Is.GreaterThan(hall.Center.Z - hall.Depth / 2f), "Camera stays inside the hall.");
        }

        [UnityTest]
        public IEnumerator TalkingToAResident_IsRecordedThroughTheRegistry()
        {
            yield return Enter(FirstSettlement);
            var resident = Controller.Residents.Values.First();
            var target = resident.transform.position;
            var from = target + new Vector3(0f, 0f, -1.5f);
            Controller.Player.Teleport(new LocalPose(from.x, 0f, from.z, 0f));
            Controller.CameraRig.SetView(0f, 5f);
            yield return null;
            // Residents may wander; face whoever we stand next to.
            var to = resident.transform.position - Controller.Player.transform.position;
            Controller.CameraRig.SetView(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 5f);
            yield return null;

            Assert.That(Controller.CurrentTarget, Is.SameAs(resident));
            Assert.That(Controller.Hud.PromptVisible, Is.True);
            var eventsBefore = Session.GetOverview().EventCount;

            var response = Controller.Interact();

            Assert.That(response.Kind, Is.EqualTo(InteractionKind.StateChanging));
            Assert.That(response.Command.IsSuccess, Is.True, response.Command.ToString());
            Assert.That(Controller.Hud.ResponseVisible, Is.True);
            var npc = Session.FindNpc(resident.NpcId);
            Assert.That(npc.HasMetPlayer, Is.True);
            Assert.That(npc.ConversationCount, Is.EqualTo(1));
            Assert.That(Session.GetOverview().EventCount, Is.EqualTo(eventsBefore + 1));
            Assert.That(Session.GetRecentEvents(5).Any(e => e.EventType == HistoricalEventType.PlayerMetResident), Is.True);
        }

        [UnityTest]
        public IEnumerator GranaryLedger_IsInformational_AndChangesNothing()
        {
            var s = FirstSettlement;
            yield return Enter(s);
            var ledger = Controller.Interactables.First(i => i.InteractableKind == InteractableKind.GranaryLedger);
            var eventsBefore = Session.GetOverview().EventCount;

            var response = ledger.Interact(Session, Controller.Player.transform);

            Assert.That(response.Kind, Is.EqualTo(InteractionKind.Informational));
            Assert.That(response.Command, Is.Null);
            StringAssert.Contains(Session.FindSettlement(s.SettlementId).Food.Stock.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), response.Text);
            Assert.That(Session.GetOverview().EventCount, Is.EqualTo(eventsBefore));
            Assert.That(Session.FindSettlement(s.SettlementId).LabourFocus, Is.EqualTo(s.LabourFocus));
        }

        [UnityTest]
        public IEnumerator NoticeBoard_SetsTheSettlementsLabourFocus()
        {
            var s = FirstSettlement;
            yield return Enter(s);
            var board = Controller.Interactables.First(i => i.InteractableKind == InteractableKind.LabourNoticeBoard);

            var response = board.Interact(Session, Controller.Player.transform);

            Assert.That(response.Kind, Is.EqualTo(InteractionKind.StateChanging));
            Assert.That(response.Command.IsSuccess, Is.True, response.Command.ToString());
            Assert.That(Session.FindSettlement(s.SettlementId).LabourFocus.HasValue, Is.True);
            Assert.That(Session.GetRecentEvents(5).Any(e => e.EventType == HistoricalEventType.LabourNoticePosted), Is.True);
        }

        [UnityTest]
        public IEnumerator SaveAndReload_InsideASettlement_RestoresTheCharacter()
        {
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", TestSlot + ".rkworld.json");
            try
            {
                var s = FirstSettlement;
                yield return Enter(s);
                yield return Frames(5);
                _input.Move = new Vector2(0f, 1f);
                for (var i = 0; i < 40; i++) yield return null;
                _input.Move = Vector2.zero;
                yield return null;
                var saved = Controller.Player.transform.position;

                Assert.That(Runtime.Save(TestSlot).IsSuccess, Is.True);

                _input.Move = new Vector2(1f, 0f);
                for (var i = 0; i < 30; i++) yield return null;
                _input.Move = Vector2.zero;
                Assert.That(Vector3.Distance(Controller.Player.transform.position, saved), Is.GreaterThan(0.5f));
                var loads = Controller.Streamer.LoadCount;

                Assert.That(Session.Execute(new LoadWorldCommand(TestSlot)).IsSuccess, Is.True);
                yield return null;
                yield return null;

                Assert.That(Controller.Streamer.LoadCount, Is.EqualTo(loads + 1), "Environment rebuilt from the loaded world.");
                Assert.That(Controller.Streamer.Current.SettlementId, Is.EqualTo(s.SettlementId));
                Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Exploration));
                Assert.That(Vector3.Distance(Controller.Player.transform.position, saved), Is.LessThan(0.1f));
                Assert.That(Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).Length, Is.EqualTo(Session.GetResidents(s.SettlementId).Count));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator TheClockKeepsRunningWhileExploring()
        {
            yield return Enter(FirstSettlement);
            var before = Session.GetOverview().Date;

            Assert.That(Session.Execute(new AdvanceDayCommand()).IsSuccess, Is.True);
            yield return null;

            Assert.That(Session.GetOverview().Date, Is.Not.EqualTo(before));
            Assert.That(Controller.IsExploring, Is.True, "Advancing time does not leave the settlement.");
            Assert.That(Controller.Hud.DateLabel.text, Is.EqualTo(Session.GetOverview().Date.ToString()));
        }

        [UnityTest]
        public IEnumerator RestartWhileExploring_UnloadsTheTown_AndExplorationWorksOnTheNewSession()
        {
            yield return Enter(FirstSettlement);
            var old = Session;
            var baseline = old.WorldChangedSubscriberCount;

            Assert.That(Runtime.Host.Shutdown().IsSuccess, Is.True);
            for (var i = 0; i < 20 && AnyExplorationSceneLoaded(); i++) yield return null;

            Assert.That(Controller.IsExploring, Is.False);
            Assert.That(AnyExplorationSceneLoaded(), Is.False);
            Assert.That(Runtime.Mode, Is.EqualTo(ViewMode.Strategic));
            Assert.That(Bootstrap.StrategicVisible, Is.True);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                if (Runtime.Host.IsActive) Runtime.Host.Shutdown();
                Assert.That(Runtime.Host.StartNew(new CreateWorldCommand("Again " + cycle, 777 + cycle)).IsSuccess, Is.True);
                yield return null;
                Assert.That(Session, Is.Not.SameAs(old));
                Assert.That(Session.WorldChangedSubscriberCount, Is.EqualTo(baseline), "Map, HUD, selection, runtime and exploration each subscribed once.");
                Assert.That(Session.Commands.IsRegistered<Realmkeeper.Exploration.Application.EnterSettlementCommand>(), Is.True);
            }

            yield return Enter(FirstSettlement);
            Assert.That(Controller.IsExploring, Is.True);
            Assert.That(Controller.Streamer.Current.SettlementId, Is.EqualTo(FirstSettlement.SettlementId));
            Assert.That(Object.FindObjectsByType<ResidentView>(FindObjectsSortMode.None).Length, Is.EqualTo(Session.GetResidents(FirstSettlement.SettlementId).Count));
        }

        [UnityTest]
        public IEnumerator SavingWhileExploring_UsesTheSelectedSlot()
        {
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", "playmode-explore-slot.rkworld.json");
            try
            {
                Assert.That(Runtime.SelectSlot("playmode-explore-slot").IsSuccess, Is.True);
                yield return Enter(FirstSettlement);

                Assert.That(Controller.SaveNow().IsSuccess, Is.True);
                Assert.That(File.Exists(path), Is.True, "The exploration view saved to the slot the map selected.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [UnityTest]
        public IEnumerator ASecondRuntime_IsRefused()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("second runtime"));
            var extra = new GameObject("Extra runtime").AddComponent<RealmkeeperRuntime>();
            yield return null;

            Assert.That(extra == null, Is.True, "The duplicate destroyed itself.");
            Assert.That(Object.FindObjectsByType<RealmkeeperRuntime>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Runtime.Host.IsActive, Is.True);
        }
    }
}
