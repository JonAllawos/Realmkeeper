using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Simulation
{
    public sealed class SimulationSchedulerTests
    {
        private sealed class RecordingSystem : ISimulationSystem
        {
            private readonly List<string> _log;
            public RecordingSystem(string id, int order, List<string> log) { SystemId = id; ExecutionOrder = order; _log = log; }
            public string SystemId { get; }
            public int ExecutionOrder { get; }
            public void Execute(SimulationDayContext context) => _log.Add(SystemId);
        }

        private sealed class DelegateSystem : ISimulationSystem
        {
            private readonly Action<SimulationDayContext> _action;
            public DelegateSystem(string id, int order, Action<SimulationDayContext> action) { SystemId = id; ExecutionOrder = order; _action = action; }
            public string SystemId { get; }
            public int ExecutionOrder { get; }
            public void Execute(SimulationDayContext context) => _action(context);
        }

        private static SimulationScheduler Standard() => TestWorlds.Scheduler();

        private static string[] AllSystems => TestWorlds.AllSystems;

        [Test]
        public void AdvanceDay_MovesDateForwardOneDay_WithoutMutatingInput()
        {
            var world = TestWorlds.SingleSettlement(100, 1000, 50, AllSystems);
            var before = WorldFingerprint.Of(world);

            var result = Standard().AdvanceDay(world);

            Assert.That(result.Committed, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.World.CurrentSimulationDate, Is.EqualTo(new WorldDate(412, 3, 2)));
            Assert.That(WorldFingerprint.Of(world), Is.EqualTo(before), "Input world must not be mutated.");
        }

        [Test]
        public void AdvanceDays_CrossesMonthBoundaryExactly()
        {
            var world = TestWorlds.SingleSettlement(100, 1000, 50, AllSystems);
            var result = Standard().AdvanceDays(world, WorldDate.DaysPerMonth);

            Assert.That(result.Completed, Is.True);
            Assert.That(result.DaysCompleted, Is.EqualTo(30));
            Assert.That(result.World.CurrentSimulationDate, Is.EqualTo(new WorldDate(412, 4, 1)));
        }

        [Test]
        public void FoodSystem_SurplusProductionIncreasesReserves()
        {
            // Population 100 consumes 10/day; production 50 -> +40/day.
            var world = TestWorlds.SingleSettlement(100, 1000, 50, TestWorlds.FoodSystems);
            var result = Standard().AdvanceDays(world, 3);
            Assert.That(result.World.Settlements[0].FoodReserves, Is.EqualTo(1000 + 3 * 40));
        }

        [Test]
        public void FoodSystem_ConsumptionAboveProductionDrawsDownReserves()
        {
            // Population 1000 consumes 100/day; production 40 -> -60/day.
            var world = TestWorlds.SingleSettlement(1000, 6000, 40, TestWorlds.FoodSystems);
            var result = Standard().AdvanceDays(world, 10);
            Assert.That(result.World.Settlements[0].FoodReserves, Is.EqualTo(6000 - 10 * 60));
            Assert.That(result.World.Settlements[0].Population, Is.EqualTo(1000));
        }

        [Test]
        public void FoodSystem_RaisesShortageEventOnceWhenThresholdCrossed()
        {
            // Consumption 100/day, threshold = 15 days = 1500. Reserves 2000, net -60/day.
            // Day 9: 2000 - 540 = 1460 < 1500 -> shortage begins on day 9 only.
            var world = TestWorlds.SingleSettlement(1000, 2000, 40, TestWorlds.FoodSystems);
            var scheduler = Standard();

            var first8 = scheduler.AdvanceDays(world, 8);
            Assert.That(first8.Events, Is.Empty);
            Assert.That(first8.World.Settlements[0].IsInFoodShortage, Is.False);

            var day9 = scheduler.AdvanceDay(first8.World);
            Assert.That(day9.Events.Count, Is.EqualTo(1));
            Assert.That(day9.Events[0].EventType, Is.EqualTo(HistoricalEventType.FoodShortageBegan));
            Assert.That(day9.Events[0].SettlementId, Is.EqualTo(world.Settlements[0].SettlementId));
            Assert.That(day9.Events[0].Date, Is.EqualTo(day9.World.CurrentSimulationDate));
            Assert.That(day9.World.Settlements[0].IsInFoodShortage, Is.True);

            var later = scheduler.AdvanceDays(day9.World, 5);
            Assert.That(later.Events.Count(e => e.EventType == HistoricalEventType.FoodShortageBegan), Is.Zero, "No duplicate shortage events.");
        }

        [Test]
        public void FoodSystem_ShortageEndsOnlyAfterRecoveryThreshold()
        {
            // Consumption 10/day: shortage below 150, recovery at 300. Start in shortage with 100, net +20/day.
            var world = TestWorlds.SingleSettlement(100, 100, 30, TestWorlds.FoodSystems);
            world.Settlements[0].IsInFoodShortage = true;
            var scheduler = Standard();

            var nine = scheduler.AdvanceDays(world, 9); // 280: above shortage line but below recovery.
            Assert.That(nine.World.Settlements[0].IsInFoodShortage, Is.True);

            var ten = scheduler.AdvanceDay(nine.World); // 300: recovered.
            Assert.That(ten.World.Settlements[0].IsInFoodShortage, Is.False);
            Assert.That(ten.Events.Single().EventType, Is.EqualTo(HistoricalEventType.FoodShortageEnded));
        }

        [Test]
        public void FoodSystem_StarvationIsGradual_GraceDaysBeforeAnyLoss()
        {
            // Consumption 100, production 40, reserves 50 -> 90 available < 100: day 1 exhausts reserves.
            var world = TestWorlds.SingleSettlement(1000, 50, 40, TestWorlds.FoodSystems);
            var scheduler = Standard();
            var grace = new FoodEconomyRules().StarvationGraceDays;

            var day1 = scheduler.AdvanceDay(world);
            var s = day1.World.Settlements[0];
            Assert.That(s.FoodReserves, Is.Zero);
            Assert.That(s.Population, Is.EqualTo(1000), "One bad day kills no one.");
            Assert.That(day1.Events.Any(e => e.EventType == HistoricalEventType.FoodReservesExhausted && e.Severity == EventSeverity.Critical), Is.True);

            var graceRun = scheduler.AdvanceDays(day1.World, grace - 1);
            Assert.That(graceRun.World.Settlements[0].Population, Is.EqualTo(1000), "No losses during the grace period.");

            var firstLoss = scheduler.AdvanceDay(graceRun.World);
            Assert.That(firstLoss.World.Settlements[0].Population, Is.EqualTo(1000 - 1000 / 200));
            Assert.That(firstLoss.Events.Single(e => e.EventType == HistoricalEventType.StarvationBegan).Importance, Is.EqualTo(EventImportance.Major));

            var nextDay = scheduler.AdvanceDay(firstLoss.World);
            Assert.That(nextDay.World.Settlements[0].Population, Is.EqualTo(995 - 995 / 200), "Losses are a small fraction per day.");
            Assert.That(nextDay.Events.Any(e => e.EventType == HistoricalEventType.StarvationBegan), Is.False, "Starvation is reported once.");
        }

        [Test]
        public void Simulation_IsDeterministicForIdenticalInputs()
        {
            WorldData Build()
            {
                var w = TestWorlds.SingleSettlement(1000, 3000, 70, AllSystems);
                w.Settlements.Add(TestWorlds.MakeSettlement("Brookmere", "8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1002", 300, 500, 60));
                return w;
            }

            var a = Standard().AdvanceDays(Build(), 400);
            var b = Standard().AdvanceDays(Build(), 400);

            Assert.That(a.Completed && b.Completed, Is.True);
            Assert.That(a.World.HistoricalEvents.Count, Is.GreaterThan(0), "Scenario should exercise events.");
            Assert.That(WorldFingerprint.Of(a.World), Is.EqualTo(WorldFingerprint.Of(b.World)));
        }

        [Test]
        public void Simulation_StepwiseAndBatchedAdvancementAreIdentical()
        {
            var world = TestWorlds.SingleSettlement(1000, 3000, 70, AllSystems);
            var scheduler = Standard();

            var batched = scheduler.AdvanceDays(world, 45).World;
            var stepwise = world;
            for (var i = 0; i < 45; i++) stepwise = scheduler.AdvanceDay(stepwise).World;

            Assert.That(WorldFingerprint.Of(stepwise), Is.EqualTo(WorldFingerprint.Of(batched)));
        }

        [Test]
        public void Simulation_EconomyHasNoHiddenRandomness_SeedOnlyAffectsIds()
        {
            // Phase 1 drifted ratings on coin flips. Phase 2 outcomes depend only on world state.
            var a = TestWorlds.SingleSettlement(1000, 3000, 70, AllSystems);
            var b = TestWorlds.SingleSettlement(1000, 3000, 70, AllSystems);
            b.Seed = a.Seed + 1;
            var scheduler = Standard();

            var ra = scheduler.AdvanceDays(a, 120).World;
            var rb = scheduler.AdvanceDays(b, 120).World;

            Assert.That(rb.Settlements[0].Population, Is.EqualTo(ra.Settlements[0].Population));
            Assert.That(rb.Settlements[0].Prosperity, Is.EqualTo(ra.Settlements[0].Prosperity));
            Assert.That(rb.Settlements[0].Stability, Is.EqualTo(ra.Settlements[0].Stability));
            Assert.That(rb.Settlements[0].FoodReserves, Is.EqualTo(ra.Settlements[0].FoodReserves));
            Assert.That(rb.HistoricalEvents.Select(e => e.EventType), Is.EqualTo(ra.HistoricalEvents.Select(e => e.EventType)));
        }

        [Test]
        public void Scheduler_ExecutesSystemsInExplicitOrderRegardlessOfRegistrationOrder()
        {
            var log = new List<string>();
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(new RecordingSystem("c", 300, log));
            scheduler.Register(new RecordingSystem("a", 100, log));
            scheduler.Register(new RecordingSystem("b", 200, log));
            var world = TestWorlds.SingleSettlement(10, 10, 10, "a", "b", "c");

            scheduler.AdvanceDays(world, 2);

            Assert.That(log, Is.EqualTo(new[] { "a", "b", "c", "a", "b", "c" }));
        }

        [Test]
        public void Scheduler_RejectsDuplicateIdsAndAmbiguousOrder()
        {
            var log = new List<string>();
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(new RecordingSystem("a", 100, log));

            Assert.Throws<InvalidOperationException>(() => scheduler.Register(new RecordingSystem("a", 101, log)));
            Assert.Throws<InvalidOperationException>(() => scheduler.Register(new RecordingSystem("b", 100, log)));
            Assert.Throws<ArgumentException>(() => scheduler.Register(new RecordingSystem(" ", 5, log)));
        }

        [Test]
        public void Scheduler_SkipsSystemsNotEnabledInWorldSettings()
        {
            var log = new List<string>();
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(new RecordingSystem("on", 1, log));
            scheduler.Register(new RecordingSystem("off", 2, log));

            scheduler.AdvanceDay(TestWorlds.SingleSettlement(10, 10, 10, "on"));

            Assert.That(log, Is.EqualTo(new[] { "on" }));
        }

        [Test]
        public void Scheduler_DoesNotCommitDayThatProducesInvalidState()
        {
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(new DelegateSystem("corrupt", 1, ctx => ctx.World.Settlements[0].Population = -50));
            var world = TestWorlds.SingleSettlement(10, 10, 10, "corrupt");
            var before = WorldFingerprint.Of(world);

            var result = scheduler.AdvanceDay(world);

            Assert.That(result.Committed, Is.False);
            Assert.That(result.World, Is.SameAs(world));
            Assert.That(result.Errors.Single(), Does.Contain("negative population"));
            Assert.That(WorldFingerprint.Of(world), Is.EqualTo(before));
        }

        [Test]
        public void Scheduler_IsolatesThrowingSystem_AndStopsMultiDayRunAtFailure()
        {
            var scheduler = new SimulationScheduler(new Xoshiro256RandomFactory());
            scheduler.Register(new DelegateSystem("bomb", 1, ctx =>
            {
                if (ctx.Date == new WorldDate(412, 3, 4)) throw new InvalidOperationException("boom");
            }));
            var world = TestWorlds.SingleSettlement(10, 10, 10, "bomb");

            var run = scheduler.AdvanceDays(world, 10);

            Assert.That(run.Completed, Is.False);
            Assert.That(run.DaysCompleted, Is.EqualTo(2));
            Assert.That(run.World.CurrentSimulationDate, Is.EqualTo(new WorldDate(412, 3, 3)));
            Assert.That(run.Errors.Single(), Does.Contain("boom").And.Contain("bomb"));
        }

        [Test]
        public void Scheduler_RejectsInvalidInputWorld()
        {
            var world = TestWorlds.SingleSettlement(10, 10, 10, AllSystems);
            world.Settlements[0].Stability = 400;

            var result = Standard().AdvanceDay(world);

            Assert.That(result.Committed, Is.False);
            Assert.That(result.Errors[0], Does.StartWith("Input world is invalid"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Standard().AdvanceDays(world, 0));
        }
    }
}
