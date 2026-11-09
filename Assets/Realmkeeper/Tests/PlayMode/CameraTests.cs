using System.Collections;
using NUnit.Framework;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Camera motion with synthetic input (device reading is switched off).</summary>
    public sealed class CameraTests : RealmkeeperPlayModeFixture
    {
        private IsometricCameraController Controller => Bootstrap.CameraController;

        private IsometricCameraMath Motion => Controller.Motion;

        [UnityTest]
        public IEnumerator Pan_MovesPivot_AndStaysClampedAtRegionEdge()
        {
            var start = Motion.Pivot;
            Motion.Step(new CameraInput { Pan = Vector2.right }, 0.25f, 0f);
            Controller.Apply();
            Assert.That(Vector3.Distance(Motion.Pivot, start), Is.GreaterThan(1f), "Panning moves the pivot.");
            yield return null;
            Assert.That(Vector3.Distance(Controller.transform.position, Motion.Pivot), Is.LessThan(1e-3f), "The rig follows the pivot.");

            // Pan far beyond the region in every direction.
            var bounds = Bootstrap.WorldView.Bounds;
            foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down })
            {
                for (var i = 0; i < 60; i++) Motion.Step(new CameraInput { Pan = direction }, 0.5f, 0f);
                Assert.That(Motion.Pivot.x, Is.InRange(bounds.xMin - 1e-3f, bounds.xMax + 1e-3f));
                Assert.That(Motion.Pivot.z, Is.InRange(bounds.yMin - 1e-3f, bounds.yMax + 1e-3f));
            }

            // Drag pans too, and is clamped by the same bounds.
            var beforeDrag = Motion.Pivot;
            Motion.Step(new CameraInput { DragPixels = new Vector2(-200f, 0f) }, 0.016f, 0.1f);
            Assert.That(Motion.Pivot, Is.Not.EqualTo(beforeDrag));
        }

        [UnityTest]
        public IEnumerator Zoom_ChangesOrthoSizeSmoothly_WithinLimits()
        {
            var start = Motion.OrthoSize;
            Motion.Step(new CameraInput { Zoom = 1f }, 0.016f, 0f);
            Assert.That(Motion.TargetOrthoSize, Is.LessThan(start), "Scrolling in lowers the target size.");
            Assert.That(Motion.OrthoSize, Is.LessThan(start).And.GreaterThan(Motion.TargetOrthoSize), "Size eases rather than jumping.");

            for (var i = 0; i < 200; i++) Motion.Step(new CameraInput { Zoom = 1f }, 0.05f, 0f);
            Assert.That(Motion.OrthoSize, Is.EqualTo(Motion.Settings.MinOrthoSize).Within(1e-3f));

            for (var i = 0; i < 200; i++) Motion.Step(new CameraInput { Zoom = -1f }, 0.05f, 0f);
            Assert.That(Motion.OrthoSize, Is.EqualTo(Motion.Settings.MaxOrthoSize).Within(1e-3f));

            Controller.Apply();
            yield return null;
            Assert.That(Controller.Camera.orthographicSize, Is.EqualTo(Motion.OrthoSize).Within(1e-3f));
            Assert.That(Controller.Camera.orthographic, Is.True);
        }

        [UnityTest]
        public IEnumerator Rotate_SnapsToQuarterTurn_AfterEasing()
        {
            var startYaw = Motion.Yaw;
            Motion.Step(new CameraInput { RotateSteps = 1 }, 0.05f, 0f);
            Assert.That(Motion.IsRotating, Is.True);
            Assert.That(Motion.Yaw, Is.GreaterThan(startYaw).And.LessThan(startYaw + 90f), "Rotation eases in.");

            for (var i = 0; i < 40; i++) Motion.Step(CameraInput.None, 0.05f, 0f);
            Assert.That(Motion.IsRotating, Is.False);
            Assert.That(Motion.Yaw, Is.EqualTo(Mathf.Repeat(startYaw + 90f, 360f)).Within(1e-4f));
            Assert.That(Mathf.Repeat(Motion.Yaw - startYaw, 90f), Is.EqualTo(0f).Within(1e-4f), "Ends on a 90 degree step.");

            Motion.Step(new CameraInput { RotateSteps = -1 }, 0.05f, 0f);
            for (var i = 0; i < 40; i++) Motion.Step(CameraInput.None, 0.05f, 0f);
            Assert.That(Motion.Yaw, Is.EqualTo(Mathf.Repeat(startYaw, 360f)).Within(1e-4f));

            Controller.Apply();
            yield return null;
            Assert.That(Mathf.DeltaAngle(Controller.transform.eulerAngles.y, Motion.Yaw), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Controller.Camera.transform.localEulerAngles.x, Is.EqualTo(Motion.Settings.Pitch).Within(0.01f));
        }
    }
}
