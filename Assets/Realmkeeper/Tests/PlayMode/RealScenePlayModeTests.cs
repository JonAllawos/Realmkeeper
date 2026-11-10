using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>
    /// The shipped scene, not a test-built object graph: what a player sees when pressing Play.
    /// Selecting a city by screen point (as a mouse click does), entering it, and walking.
    /// </summary>
    public sealed class RealScenePlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";

        private Mouse _mouse;
        private InputSettings.BackgroundBehavior _background;
        private InputSettings.EditorInputBehaviorInPlayMode _editorInput;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Assert.That(Object.FindAnyObjectByType<RealmkeeperRuntime>() == null, Is.True, "No runtime left from an earlier test.");
            // Headless runs have no focused window; let device events through anyway (restored in teardown).
            _background = InputSystem.settings.backgroundBehavior;
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            InputSystem.settings.backgroundBehavior = _background;
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            _mouse = null;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingACity_SelectsIt_AndEnterLetsYouWalkIt()
        {
            var bootstrap = Object.FindAnyObjectByType<RealmkeeperBootstrap>();
            var installer = Object.FindAnyObjectByType<ExplorationInstaller>();
            Assert.That(bootstrap, Is.Not.Null, "The scene has the strategic bootstrap.");
            Assert.That(installer, Is.Not.Null, "The scene has the exploration installer.");
            Assert.That(bootstrap.Session.Commands.IsRegistered<Realmkeeper.Exploration.Application.EnterSettlementCommand>(), Is.True,
                "Exploration commands are registered on the scene's session.");
            installer.Controller.ManageCursor = false;
            var input = new ScriptedExplorationInput();
            installer.Controller.SetInput(input);

            // Where a mouse would click: the town's centre on screen.
            var town = bootstrap.WorldView.Settlements.Values.Single(v => v.SettlementType == SettlementType.Town);
            bootstrap.CameraController.FocusOn(town.Center);
            for (var i = 0; i < 30; i++) yield return null; // camera glides to the town
            var screen = (Vector2)bootstrap.CameraController.Camera.WorldToScreenPoint(town.Center + Vector3.up);
            Assert.That(bootstrap.Hud.IsPointerOverUi(screen), Is.False, $"The HUD does not cover the town at {screen}.");
            Assert.That(bootstrap.Selection.PickSettlement(screen), Is.SameAs(town), "A ray through that point hits the town.");

            // A real click, through the Input System, as a physical mouse delivers it.
            _mouse = InputSystem.AddDevice<Mouse>();
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen });
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = screen });
            yield return null;
            Assert.That(bootstrap.Selection.SelectedId, Is.EqualTo(town.SettlementId), "Clicking the town on the map selects it.");
            Assert.That(bootstrap.Hud.InspectorVisible, Is.True);

            // With the political overlay on, the same click still selects.
            bootstrap.Selection.Clear();
            bootstrap.Hud.SetPoliticalOverlay(true);
            yield return null;
            Assert.That(bootstrap.Selection.SelectAtScreenPoint(screen), Is.True, "The overlay does not swallow clicks.");
            yield return null;

            var result = bootstrap.Hud.EnterSelectedSettlement();
            Assert.That(result.IsSuccess, Is.True, result.ToString());
            yield return null;
            yield return null;
            Assert.That(installer.Controller.IsExploring, Is.True);
            Assert.That(bootstrap.Runtime.Mode, Is.EqualTo(ViewMode.Exploration));

            var player = installer.Controller.Player;
            for (var i = 0; i < 10; i++) yield return null;
            var start = player.transform.position;
            input.Move = new Vector2(0f, 1f);
            var timer = 0f;
            while (timer < 1.5f)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            input.Move = Vector2.zero;
            Assert.That(player.transform.position.z - start.z, Is.GreaterThan(1f),
                $"The character walks, however high the frame rate ({Time.frameCount} frames so far).");
            Assert.That(installer.Controller.ReturnToWorldMap().IsSuccess, Is.True);
        }
    }
}
