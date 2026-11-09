using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>Production, consumption, storage, labour and specialization.</summary>
    public sealed class EconomyTests
    {
        private static readonly string[] ProductionOnly = { LabourSystem.Id, ProductionSystem.Id };

        [Test]
        public void Production_OutputIsWorkersTimesRateTimesEfficiency()
        {
            var world = TestWorlds.SingleSettlement(1000, 0, 0, ProductionOnly);
            var e = world.Settlements[0].Economy;
            e.Production.Clear();
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Logging, AllocationWeight = 1, WorkerCapacity = 200, EfficiencyPercent = 125 });
            world.Settlements[0].ResourceInventory.Set(ResourceTypes.Timber, 0);
            var rules = new EconomyRules();

            var day = TestWorlds.Scheduler(rules).AdvanceDay(world).World.Settlements[0];

            // 200 workers × 40 per 100 workers × 125% = 100 timber.
            Assert.That(day.Economy.Production[0].Workers, Is.EqualTo(200));
            Assert.That(day.ResourceInventory.Get(ResourceTypes.Timber), Is.EqualTo(200 * rules.LoggingRate / 100 * 125 / 100));
            Assert.That(day.Economy.FindFlow(ResourceTypes.Timber).Produced, Is.EqualTo(100));
            Assert.That(day.Economy.LifetimeProduced.Get(ResourceTypes.Timber), Is.EqualTo(100));
            Assert.That(day.Economy.LastDaySources.Single(s => s.SourceType == ProductionSourceType.Logging).Amount, Is.EqualTo(100));
        }

        [Test]
        public void Production_BeyondStorageIsWasted_StockNeverExceedsCapacity()
        {
            // Production only (fixed 80 farmers), 960 food in a 1000-food granary.
            var world = TestWorlds.SingleSettlement(100, 960, 80, ProductionSystem.Id);
            var s = world.Settlements[0];
            s.Economy.StorageCapacity.Set(ResourceTypes.Food, 1000);
            s.Economy.Production[0].Workers = 80;

            var day = TestWorlds.Scheduler().AdvanceDay(world).World.Settlements[0];

            Assert.That(day.FoodReserves, Is.EqualTo(1000));
            var flow = day.Economy.FindFlow(ResourceTypes.Food);
            Assert.That(flow.Produced, Is.EqualTo(40), "Only what fits is stored...");
            Assert.That(flow.Wasted, Is.EqualTo(40), "...and the rest is recorded as wasted, never stored.");
            Assert.That(day.Economy.LifetimeProduced.Get(ResourceTypes.Food), Is.EqualTo(40));
        }

        [Test]
        public void Consumption_DemandScalesWithPopulation_AndShortfallNeverMakesStockNegative()
        {
            var world = TestWorlds.SingleSettlement(1000, 30, 0, TestWorlds.FoodSystems);
            var s = world.Settlements[0];
            s.ResourceInventory.Set(ResourceTypes.Timber, 5);
            var rules = TestWorlds.ExactRules();

            var day = TestWorlds.Scheduler(rules).AdvanceDay(world).World.Settlements[0];

            var food = day.Economy.FindFlow(ResourceTypes.Food);
            Assert.That(food.Demand, Is.EqualTo(100), "1000 people eat 100 food a day.");
            Assert.That(food.Consumed, Is.EqualTo(30));
            Assert.That(food.Shortfall, Is.EqualTo(70));
            var timber = day.Economy.FindFlow(ResourceTypes.Timber);
            Assert.That(timber.Demand, Is.EqualTo(rules.DailyDemand(ResourceTypes.Timber, 1000, 0)));
            Assert.That(timber.Consumed, Is.EqualTo(5));
            foreach (var stack in day.ResourceInventory.Stacks) Assert.That(stack.Quantity, Is.GreaterThanOrEqualTo(0));
            Assert.That(day.FoodReserves, Is.Zero);
        }

        [Test]
        public void FoodBalance_LedgerNetChangeEqualsChangeInStock()
        {
            var world = TestWorlds.SingleSettlement(1000, 5000, 70, TestWorlds.FoodSystems);
            var before = world.Settlements[0].FoodReserves;

            var day = TestWorlds.Scheduler().AdvanceDay(world).World.Settlements[0];
            var flow = day.Economy.FindFlow(ResourceTypes.Food);

            Assert.That(flow.Produced, Is.EqualTo(70));
            Assert.That(flow.Consumed, Is.EqualTo(100));
            Assert.That(flow.NetChange, Is.EqualTo(-30));
            Assert.That(day.FoodReserves - before, Is.EqualTo(flow.NetChange));
        }

        [Test]
        public void FoodExhaustionEstimate_MatchesWhenTheSimulationRunsOut()
        {
            // 1000 people need 100/day, farms grow 70: net -30. 300 food lasts exactly 10 days.
            var world = TestWorlds.SingleSettlement(1000, 300, 70, TestWorlds.FoodSystems);
            var scheduler = TestWorlds.Scheduler();
            var first = scheduler.AdvanceDay(world).World;
            var flow = first.Settlements[0].Economy.FindFlow(ResourceTypes.Food);
            var estimate = first.Settlements[0].FoodReserves / -(flow.Produced - flow.Demand);
            Assert.That(estimate, Is.EqualTo(9), "270 left after day 1, at -30/day.");

            var lastFedDay = scheduler.AdvanceDays(first, estimate).World;
            Assert.That(lastFedDay.Settlements[0].Economy.FindFlow(ResourceTypes.Food).Shortfall, Is.Zero);
            var hungryDay = scheduler.AdvanceDay(lastFedDay).World;
            Assert.That(hungryDay.Settlements[0].Economy.FindFlow(ResourceTypes.Food).Shortfall, Is.GreaterThan(0));
        }

        [Test]
        public void Labour_FeedsTheSettlementFirst_ThenStaffsOtherTradesByWeight()
        {
            var world = TestWorlds.SingleSettlement(1000, 0, 0, LabourSystem.Id);
            var e = world.Settlements[0].Economy;
            e.Specialization = SettlementSpecialization.None;
            e.Production.Clear();
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Farming, AllocationWeight = 1, WorkerCapacity = 1000, EfficiencyPercent = 100 });
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Logging, AllocationWeight = 3, WorkerCapacity = 1000, EfficiencyPercent = 100 });
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Quarrying, AllocationWeight = 1, WorkerCapacity = 1000, EfficiencyPercent = 100 });
            var rules = new EconomyRules();

            var day = TestWorlds.Scheduler(rules).AdvanceDay(world).World.Settlements[0].Economy;

            // Workforce 500. Food: 100/day × 115% = 115 food at 50 per 100 workers = 230 farmers.
            Assert.That(day.FindProduction(ProductionSourceType.Farming).Workers, Is.EqualTo(230));
            // The other 270 split 3:1 between logging and quarrying.
            Assert.That(day.FindProduction(ProductionSourceType.Logging).Workers, Is.EqualTo(202).Or.EqualTo(203));
            Assert.That(day.TotalWorkers, Is.EqualTo(500));
        }

        [Test]
        public void Labour_NeverExceedsCapacityOrWorkforce()
        {
            var world = TestWorlds.SingleSettlement(1000, 0, 0, LabourSystem.Id);
            var e = world.Settlements[0].Economy;
            e.Production.Clear();
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Farming, AllocationWeight = 5, WorkerCapacity = 40, EfficiencyPercent = 100 });
            e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Mining, AllocationWeight = 1, WorkerCapacity = 25, EfficiencyPercent = 100 });

            var day = TestWorlds.Scheduler(new EconomyRules()).AdvanceDay(world).World.Settlements[0].Economy;

            Assert.That(day.FindProduction(ProductionSourceType.Farming).Workers, Is.EqualTo(40));
            Assert.That(day.FindProduction(ProductionSourceType.Mining).Workers, Is.EqualTo(25));
            Assert.That(day.TotalWorkers, Is.EqualTo(65), "The rest of the workforce stays idle rather than overfilling.");
        }

        [Test]
        public void Specialization_AgriculturalSettlementsFarmForExport_OthersForThemselves()
        {
            WorldData Build(SettlementSpecialization spec)
            {
                var w = TestWorlds.SingleSettlement(1000, 0, 0, LabourSystem.Id);
                var e = w.Settlements[0].Economy;
                e.Specialization = spec;
                e.Production.Clear();
                e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Farming, AllocationWeight = 1, WorkerCapacity = 1000, EfficiencyPercent = 100 });
                e.Production.Add(new ProductionAssignment { SourceType = ProductionSourceType.Trade, AllocationWeight = 1, WorkerCapacity = 1000, EfficiencyPercent = 100 });
                return w;
            }

            var rules = new EconomyRules();
            var farmers = TestWorlds.Scheduler(rules).AdvanceDay(Build(SettlementSpecialization.Agricultural)).World.Settlements[0].Economy.FindProduction(ProductionSourceType.Farming).Workers;
            var traders = TestWorlds.Scheduler(rules).AdvanceDay(Build(SettlementSpecialization.Trade)).World.Settlements[0].Economy.FindProduction(ProductionSourceType.Farming).Workers;

            Assert.That(farmers, Is.EqualTo(100 * rules.AgriculturalFoodTargetPercent / 100 * 2), "170 food at 0.5 per farmer.");
            Assert.That(traders, Is.EqualTo(100 * rules.DefaultFoodTargetPercent / 100 * 2), "115 food at 0.5 per farmer.");
        }

        [Test]
        public void SeasonalModifierHook_ScalesOutputWithoutChangingSystems()
        {
            var rules = TestWorlds.ExactRules();
            rules.SeasonalModifierPercent = (date, source) => date.Month == 3 && source == ProductionSourceType.Farming ? 50 : 100;
            var world = TestWorlds.SingleSettlement(1000, 0, 100, ProductionOnly);

            var day = TestWorlds.Scheduler(rules).AdvanceDay(world).World.Settlements[0];

            Assert.That(day.Economy.FindFlow(ResourceTypes.Food).Produced, Is.EqualTo(50));
        }

        [Test]
        public void ResourceShortage_WarnsAfterConfirmationDays_AndEndsOnRecovery()
        {
            var world = TestWorlds.SingleSettlement(800, 100000, 0, ConsumptionSystem.Id);
            var s = world.Settlements[0];
            s.ResourceInventory.Set(ResourceTypes.Timber, 0); // 10 timber a day needed, none in store
            var rules = TestWorlds.ExactRules();
            var scheduler = TestWorlds.Scheduler(rules);

            var two = scheduler.AdvanceDays(world, rules.ShortageConfirmDays - 1);
            Assert.That(two.Events.Any(e => e.EventType == HistoricalEventType.ResourceShortageBegan), Is.False, "A brief dip is not reported.");

            var confirmed = scheduler.AdvanceDay(two.World);
            var warning = confirmed.Events.Single(e => e.EventType == HistoricalEventType.ResourceShortageBegan);
            Assert.That(warning.Get("resource"), Is.EqualTo("timber"));
            Assert.That(warning.Severity, Is.EqualTo(EventSeverity.Warning));
            Assert.That(confirmed.World.Settlements[0].Economy.FindStatus(ResourceTypes.Timber).InShortage, Is.True);

            var restocked = confirmed.World.DeepClone();
            restocked.Settlements[0].ResourceInventory.Set(ResourceTypes.Timber, 1000);
            var recovered = scheduler.AdvanceDay(restocked);
            Assert.That(recovered.Events.Single(e => e.EventType == HistoricalEventType.ResourceShortageEnded).Get("resource"), Is.EqualTo("timber"));
            Assert.That(recovered.World.Settlements[0].Economy.FindStatus(ResourceTypes.Timber).InShortage, Is.False);
        }
    }
}
