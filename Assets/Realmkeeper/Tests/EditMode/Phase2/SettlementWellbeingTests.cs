using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>Prosperity, stability and population follow documented formulas, gradually.</summary>
    public sealed class SettlementWellbeingTests
    {
        [Test]
        public void ProsperityTarget_WeighsFoodGoodsTradeAndOutput_MinusShortagePenalties()
        {
            // 30% food, 15% goods, 20% trade, 35% output.
            var factors = new WellbeingFactors(food: 100, resources: 60, trade: 50, output: 40, employment: 90, housing: 100, coin: 80, shortagePenalty: 0, starvationPenalty: 0);
            Assert.That(EconomyMath.ProsperityTarget(factors), Is.EqualTo((30 * 100 + 15 * 60 + 20 * 50 + 35 * 40) / 100));

            var withShortages = new WellbeingFactors(100, 60, 50, 40, 90, 100, 80, shortagePenalty: 15, starvationPenalty: 0);
            Assert.That(EconomyMath.ProsperityTarget(withShortages), Is.EqualTo(63 - 15));

            var awful = new WellbeingFactors(0, 0, 0, 0, 0, 0, 0, 40, 15);
            Assert.That(EconomyMath.ProsperityTarget(awful), Is.Zero, "Targets are clamped to 0..100.");
        }

        [Test]
        public void StabilityTarget_WeighsFoodSecurityEconomyHousingProsperityAndEmployment()
        {
            // 35% food, 20% economic security ((goods + coin) / 2), 15% housing, 20% prosperity, 10% employment.
            var factors = new WellbeingFactors(food: 80, resources: 60, trade: 0, output: 0, employment: 100, housing: 50, coin: 100, shortagePenalty: 10, starvationPenalty: 15);
            var expected = (35 * 80 + 20 * 80 + 15 * 50 + 20 * 70 + 10 * 100) / 100 - 10 - 15;
            Assert.That(EconomyMath.StabilityTarget(factors, prosperity: 70), Is.EqualTo(expected));
        }

        [Test]
        public void Factors_ReflectFoodCoverShortagesAndHousingPressure()
        {
            var rules = new EconomyRules();
            var world = TestWorlds.SingleSettlement(100, 600, 0);
            var s = world.Settlements[0];

            Assert.That(EconomyMath.Factors(s, rules).Food, Is.EqualTo(100), "60 days of food is fully secure.");
            s.ResourceInventory.Set(ResourceTypes.Food, 300);
            Assert.That(EconomyMath.Factors(s, rules).Food, Is.EqualTo(50), "30 days is half.");
            s.IsInFoodShortage = true;
            Assert.That(EconomyMath.Factors(s, rules).ShortagePenalty, Is.EqualTo(10));

            s.Population = rules.HousingCapacity(SettlementType.Village) * 120 / 100;
            Assert.That(EconomyMath.Factors(s, rules).Housing, Is.Zero, "120% of housing capacity is untenable.");
        }

        [Test]
        public void Ratings_MoveAtMostOnePointPerDay_AndStayInBounds()
        {
            var world = Phase2Worlds.Generated(42);
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            for (var day = 0; day < 240; day++)
            {
                var next = scheduler.AdvanceDay(world).World;
                for (var i = 0; i < world.Settlements.Count; i++)
                {
                    Assert.That(Math.Abs(next.Settlements[i].Prosperity - world.Settlements[i].Prosperity), Is.LessThanOrEqualTo(1));
                    Assert.That(Math.Abs(next.Settlements[i].Stability - world.Settlements[i].Stability), Is.LessThanOrEqualTo(1));
                    Assert.That(next.Settlements[i].Prosperity, Is.InRange(0, 100));
                    Assert.That(next.Settlements[i].Stability, Is.InRange(0, 100));
                }

                world = next;
            }
        }

        [Test]
        public void FoodShortage_LowersProsperityGradually_AndStabilityWhenItPersists()
        {
            // 1000 people, 40 food a day grown, 1400 in store (under 15 days): a shortage starts at once and deepens.
            var world = TestWorlds.SingleSettlement(1000, 1400, 40, TestWorlds.AllSystems);
            var s = world.Settlements[0];
            s.Prosperity = 60;
            s.Stability = 70;
            var scheduler = TestWorlds.Scheduler();

            var day1 = scheduler.AdvanceDay(world).World.Settlements[0];
            Assert.That(day1.IsInFoodShortage, Is.True);
            Assert.That(day1.Prosperity, Is.EqualTo(59), "One point a day, not a collapse.");
            Assert.That(day1.Population, Is.EqualTo(1000));

            var month = scheduler.AdvanceDays(world, 30).World.Settlements[0];
            Assert.That(month.Prosperity, Is.LessThan(60).And.GreaterThanOrEqualTo(30));
            Assert.That(month.Stability, Is.LessThan(70));
            Assert.That(month.Economy.ProsperityTarget, Is.LessThan(month.Prosperity + 1), "Still heading down while the shortage lasts.");
        }

        [Test]
        public void Population_GrowsSlowlyWhenFedAndStable_AndShrinksWhenUnstable()
        {
            var fed = TestWorlds.SingleSettlement(500, 100000, 500, PopulationSystem.Id);
            fed.Settlements[0].Stability = 80;
            var grown = TestWorlds.Scheduler().AdvanceDays(fed, 360).World.Settlements[0];
            Assert.That(grown.Population, Is.InRange(505, 525), "About 2–3% a year.");

            var unrest = TestWorlds.SingleSettlement(500, 100000, 500, PopulationSystem.Id);
            unrest.Settlements[0].Stability = 10;
            var shrunk = TestWorlds.Scheduler().AdvanceDays(unrest, 360).World.Settlements[0];
            Assert.That(shrunk.Population, Is.InRange(480, 495), "Unrest makes people leave, slowly.");
        }

        [Test]
        public void Chronicle_RecordsRatingAndPopulationMilestonesOnce_NotEveryDay()
        {
            var world = TestWorlds.SingleSettlement(1000, 1400, 40, TestWorlds.AllSystems);
            world.Settlements[0].Prosperity = 60;
            ChronicleSystem.Baseline(world.Settlements[0]);
            var run = TestWorlds.Scheduler().AdvanceDays(world, 60);

            var prosperity = run.World.HistoricalEvents.Where(e => e.EventType == HistoricalEventType.ProsperityChanged).ToList();
            Assert.That(prosperity.Count, Is.InRange(1, 6), "A report per 10 points moved, not one per day.");
            Assert.That(prosperity[0].GetInt("from") - prosperity[0].GetInt("to"), Is.EqualTo(10));
            Assert.That(prosperity[0].Severity, Is.EqualTo(EventSeverity.Warning));
        }
    }
}
