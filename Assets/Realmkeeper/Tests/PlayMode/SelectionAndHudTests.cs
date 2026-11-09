using System.Collections;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Realmkeeper.Tests.PlayMode
{
    public sealed class SelectionAndHudTests : RealmkeeperPlayModeFixture
    {
        private SettlementView Town => Bootstrap.WorldView.Settlements.Values.First(v => v.SettlementType == SettlementType.Town);

        private IEnumerator SelectTownByClicking()
        {
            Bootstrap.CameraController.FocusOn(Town.Center);
            yield return null;
            Physics.SyncTransforms();
            var screenPoint = Bootstrap.CameraController.Camera.WorldToScreenPoint(Town.Center + Vector3.up * 1.5f);
            Assert.That(Bootstrap.Selection.SelectAtScreenPoint(screenPoint), Is.True, "Clicking the town's screen position hits it.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClickingSettlement_SelectsItsId_AndShowsHighlight()
        {
            Assert.That(Bootstrap.Selection.HasSelection, Is.False);
            Assert.That(Bootstrap.Selection.HighlightVisible, Is.False);

            yield return SelectTownByClicking();

            Assert.That(Bootstrap.Selection.SelectedId, Is.EqualTo(Town.SettlementId));
            Assert.That(Bootstrap.Selection.HighlightVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator AfterSelection_InspectorShowsSettlementPopulationAndFood()
        {
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.False, "Inspector is hidden without a selection.");

            yield return SelectTownByClicking();

            var summary = Bootstrap.Session.FindSettlement(Town.SettlementId);
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.True);
            Assert.That(Bootstrap.Hud.InspectorName.text, Is.EqualTo(summary.Name));
            Assert.That(Bootstrap.Hud.InspectorPopulation.text, Is.EqualTo(summary.Population.ToString("N0", CultureInfo.InvariantCulture)));
            Assert.That(Bootstrap.Hud.InspectorFood.text, Is.EqualTo(summary.FoodReserves.ToString("N0", CultureInfo.InvariantCulture)));

            // The inspector follows the simulation: advance a month and it shows the new values.
            Bootstrap.Session.Execute(new Application.AdvanceDaysCommand(30));
            yield return null;
            var later = Bootstrap.Session.FindSettlement(Town.SettlementId);
            Assert.That(Bootstrap.Hud.InspectorFood.text, Is.EqualTo(later.FoodReserves.ToString("N0", CultureInfo.InvariantCulture)));
        }

        [UnityTest]
        public IEnumerator ClearingSelection_HidesInspectorAndHighlight()
        {
            yield return SelectTownByClicking();
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.True);

            Bootstrap.Selection.Clear();
            yield return null;
            Assert.That(Bootstrap.Selection.HasSelection, Is.False);
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.False);
            Assert.That(Bootstrap.Selection.HighlightVisible, Is.False);

            // Clicking empty ground also clears: use the region corner farthest from every settlement.
            yield return SelectTownByClicking();
            var b = Bootstrap.WorldView.Bounds;
            var corners = new[]
            {
                new Vector3(b.xMin + 8f, 0f, b.yMin + 8f), new Vector3(b.xMax - 8f, 0f, b.yMin + 8f),
                new Vector3(b.xMin + 8f, 0f, b.yMax - 8f), new Vector3(b.xMax - 8f, 0f, b.yMax - 8f),
            };
            var ground = corners.OrderByDescending(c => Bootstrap.WorldView.Settlements.Values
                .Min(v => Vector2.Distance(new Vector2(c.x, c.z), new Vector2(v.Center.x, v.Center.z)))).First();
            ground.y = MapSampling.HeightAt(Bootstrap.Session.Map, ground.x, ground.z);
            Bootstrap.CameraController.FocusOn(ground);
            yield return null;
            Physics.SyncTransforms();
            var emptyPoint = Bootstrap.CameraController.Camera.WorldToScreenPoint(ground);
            Assert.That(Bootstrap.Selection.PickSettlement(emptyPoint), Is.Null, "Precondition: no settlement under the point.");
            Assert.That(Bootstrap.Selection.SelectAtScreenPoint(emptyPoint), Is.False);
            yield return null;
            Assert.That(Bootstrap.Selection.HasSelection, Is.False);
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PlusOneWeekButton_AdvancesSevenDays_AndRefreshesDateLabel()
        {
            var start = Bootstrap.Session.GetOverview().Date;
            Assert.That(Bootstrap.Hud.DateLabel.text, Is.EqualTo(start.ToString()));

            Click(Bootstrap.Hud.AdvanceWeekButton);
            yield return null;

            var now = Bootstrap.Session.GetOverview().Date;
            Assert.That(now, Is.EqualTo(start.AddDays(WorldDate.DaysPerWeek)));
            Assert.That(Bootstrap.Hud.DateLabel.text, Is.EqualTo(now.ToString()));
        }

        [UnityTest]
        public IEnumerator PlayPause_TogglesPlayback_AndDateHoldsWhilePaused()
        {
            Assert.That(Bootstrap.Playback.IsPlaying, Is.False, "The scene starts paused.");
            Assert.That(Bootstrap.Hud.PlayPauseButton.text, Is.EqualTo("Play"));

            Click(Bootstrap.Hud.PlayPauseButton);
            yield return null;
            Assert.That(Bootstrap.Playback.IsPlaying, Is.True);
            Assert.That(Bootstrap.Hud.PlayPauseButton.text, Is.EqualTo("Pause"));

            // Playing at the fastest speed advances the date in real time.
            Bootstrap.Session.Execute(new Application.SetSimulationSpeedCommand(SimulationSpeed.VeryFast));
            var beforePlay = Bootstrap.Session.GetOverview().Date;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Bootstrap.Session.GetOverview().Date, Is.GreaterThan(beforePlay));

            Click(Bootstrap.Hud.PlayPauseButton);
            yield return null;
            Assert.That(Bootstrap.Playback.IsPlaying, Is.False);
            Assert.That(Bootstrap.Hud.PlayPauseButton.text, Is.EqualTo("Play"));

            var paused = Bootstrap.Session.GetOverview().Date;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(paused), "No days pass while paused.");
        }
    }
}
