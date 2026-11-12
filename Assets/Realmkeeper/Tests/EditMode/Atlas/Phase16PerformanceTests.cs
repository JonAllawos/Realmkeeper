using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 16: measured costs of the connected world (six regions, eighteen settlements, their people, caravans,
    /// patrols and events): route planning, a day of simulation, an hour of travel, the atlas view, saving and loading.
    /// Generous bounds catch pathological regressions; the numbers are written to the test output for the report.
    /// </summary>
    public sealed class Phase16PerformanceTests : Phase16Fixture
    {
        [Test]
        public void TheConnectedWorld_IsMeasured()
        {
            var w = W;
            var sw = Stopwatch.StartNew();
            var plans = 0;
            foreach (var a in w.Settlements)
                foreach (var b in w.Settlements)
                    if (a != b && RoutePlanner.Alternatives(w, a.SettlementId, b.SettlementId).Count > 0) plans++;
            sw.Stop();
            var planMs = sw.Elapsed.TotalMilliseconds / plans;

            S.Execute(new AdvanceDaysCommand(2)); // warm up
            sw.Restart();
            var run = S.Execute(new AdvanceDaysCommand(20));
            sw.Stop();
            Assert.That(run.IsSuccess, Is.True, run.Message);
            var dayMs = sw.Elapsed.TotalMilliseconds / 20;
            w = W;

            sw.Restart();
            for (var i = 0; i < 50; i++) AtlasQueries.View(w);
            sw.Stop();
            var atlasMs = sw.Elapsed.TotalMilliseconds / 50;

            var json = new JsonWorldSerializer().Serialize(w);
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Serialize(w);
            sw.Stop();
            var saveMs = sw.Elapsed.TotalMilliseconds / 3;
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Deserialize(json);
            sw.Stop();
            var loadMs = sw.Elapsed.TotalMilliseconds / 3;

            CombatTests.Create(S, "fighter");
            var start = W.Settlements.First(s => s.RegionId == W.Regions[0].RegionId);
            Ok(S.Dispatch(new EnterSettlementCommand(start.SettlementId)));
            var far = W.Settlements.First(s => W.FindRegion(s.RegionId) is RegionData r && r.AtlasX == 2 && r.AtlasZ == 1);
            Ok(S.Dispatch(new StartJourneyCommand(far.SettlementId)));
            sw.Restart();
            var r2 = S.Travel(24, true);
            sw.Stop();
            var hourMs = sw.Elapsed.TotalMilliseconds / System.Math.Max(1, W.Journey.HoursTravelled);

            TestContext.WriteLine($"Connected world: {w.Regions.Count} regions, {w.Settlements.Count} settlements, {w.Npcs.Count} people, {w.Routes.Count} route segments, " +
                                  $"{w.Groups.Count(g => g.IsActive)} active groups ({w.Groups.Count(g => g.IsActive && g.Kind == TravelGroupKind.Caravan)} caravans), {w.WorldEvents.Count(e => e.IsOpen)} open events.");
            TestContext.WriteLine($"Route planning (3 alternatives): {planMs:0.000} ms per pair over {plans} pairs. Simulated day: {dayMs:0.0} ms. Atlas view: {atlasMs:0.00} ms. " +
                                  $"Travel: {hourMs:0.0} ms per hour (incl. the clock). Save {saveMs:0} ms, load {loadMs:0} ms, {json.Length / 1024} KB.");
            Assert.That(planMs, Is.LessThan(50));
            Assert.That(dayMs, Is.LessThan(2000));
            Assert.That(atlasMs, Is.LessThan(100));
        }
    }
}
