using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>
    /// Trade with only the trade system enabled, so every change in stock is caused by trade.
    /// Route A→B: capacity 100, 2 days on the road, a departure check every day, 90% of food arrives.
    /// </summary>
    public sealed class TradeTests
    {
        private static long Total(WorldData w, ResourceTypeId r) =>
            w.Settlements.Sum(s => (long)s.ResourceInventory.Get(r)) + w.Shipments.Where(s => s.ResourceTypeId == r).Sum(s => (long)s.Quantity);

        private static WorldData FoodRoute(int originFood, int destinationFood, out SettlementData a, out SettlementData b, out TradeRouteData route)
        {
            var world = TestWorlds.TwoSettlements(out a, out b, out route, TradeSystem.Id);
            a.ResourceInventory.Set(ResourceTypes.Food, originFood);
            b.ResourceInventory.Set(ResourceTypes.Food, destinationFood);
            return world;
        }

        [Test]
        public void SuccessfulShipment_MovesRealGoods_AndTheDestinationPaysTheOrigin()
        {
            // B (100 people, 10 food/day) wants 60 days = 600 food; A keeps 45 days = 450 back.
            var world = FoodRoute(2000, 0, out var a, out var b, out var route);
            var coinA = a.ResourceInventory.Get(ResourceTypes.Coin);
            var coinB = b.ResourceInventory.Get(ResourceTypes.Coin);
            var rules = TestWorlds.ExactRules();
            var scheduler = TestWorlds.Scheduler(rules);

            var day1 = scheduler.AdvanceDay(world).World;
            Assert.That(day1.Shipments.Single().Quantity, Is.EqualTo(100), "Capped by the route capacity.");
            Assert.That(day1.Settlements[0].FoodReserves, Is.EqualTo(1900), "Goods leave the origin when they depart.");
            Assert.That(day1.Settlements[0].Economy.FindFlow(ResourceTypes.Food).Exported, Is.EqualTo(100));
            Assert.That(day1.TradeRoutes[0].Status, Is.EqualTo(TradeRouteStatus.InTransit));

            var day3 = scheduler.AdvanceDays(day1, 2).World;
            var a3 = day3.Settlements[0];
            var b3 = day3.Settlements[1];
            Assert.That(b3.FoodReserves, Is.EqualTo(90), "10% of the food spoiled on the road.");
            Assert.That(b3.Economy.FindFlow(ResourceTypes.Food).Imported, Is.EqualTo(90));
            Assert.That(a3.ResourceInventory.Get(ResourceTypes.Coin) - coinA, Is.EqualTo(90 * rules.Price(ResourceTypes.Food)));
            Assert.That(coinB - b3.ResourceInventory.Get(ResourceTypes.Coin), Is.EqualTo(90 * rules.Price(ResourceTypes.Food)));
            Assert.That(day3.TradeRoutes[0].ShipmentsDelivered, Is.EqualTo(1));
            Assert.That(day3.TradeRoutes[0].Delivered.Get(ResourceTypes.Food), Is.EqualTo(90));
            Assert.That(day3.TradeRoutes[0].LastDeliveryQuantity, Is.EqualTo(90));
            Assert.That(a3.Economy.TradeActivity, Is.GreaterThan(0));
            Assert.That(Total(day3, ResourceTypes.Food), Is.EqualTo(2000 - 10), "Only the recorded transit loss is gone.");
            Assert.That(Total(day3, ResourceTypes.Coin), Is.EqualTo(Total(world, ResourceTypes.Coin)), "Coin only changes hands.");

            var delivered = day3.HistoricalEvents.Single(e => e.EventType == HistoricalEventType.TradeShipmentDelivered);
            Assert.That(delivered.SettlementIds, Is.EqualTo(new[] { a.SettlementId, b.SettlementId }));
            Assert.That(delivered.GetInt("quantity"), Is.EqualTo(90));
            Assert.That(delivered.GetInt("lost"), Is.EqualTo(10));
        }

        [Test]
        public void InsufficientOriginSurplus_ShipsNothing_AndTheRouteGoesIdle()
        {
            var world = FoodRoute(400, 0, out _, out _, out _); // below A's 450 reserve

            var day = TestWorlds.Scheduler().AdvanceDay(world).World;

            Assert.That(day.Shipments, Is.Empty);
            Assert.That(day.Settlements[0].FoodReserves, Is.EqualTo(400));
            Assert.That(day.TradeRoutes[0].Status, Is.EqualTo(TradeRouteStatus.Idle));
        }

        [Test]
        public void TransportCapacity_LimitsEachShipment_AndOnlyOneShipmentIsOnTheRoad()
        {
            var world = FoodRoute(5000, 0, out _, out _, out var route);
            route.CapacityPerShipment = 30;
            var scheduler = TestWorlds.Scheduler();

            var day1 = scheduler.AdvanceDay(world).World;
            Assert.That(day1.Shipments.Single().Quantity, Is.EqualTo(30));

            var day2 = scheduler.AdvanceDay(day1).World;
            Assert.That(day2.Shipments.Count, Is.EqualTo(1), "A route with goods on the road sends nothing new: no duplicate transfers.");
            Assert.That(day2.Settlements[0].FoodReserves, Is.EqualTo(5000 - 30));
        }

        [Test]
        public void DestinationCapacity_LimitsWhatIsSent()
        {
            var world = FoodRoute(5000, 0, out _, out var b, out _);
            b.Economy.StorageCapacity.Set(ResourceTypes.Food, 40);

            var day = TestWorlds.Scheduler().AdvanceDay(world).World;

            Assert.That(day.Shipments.Single().Quantity, Is.EqualTo(40), "Never more than the destination can store.");
        }

        [Test]
        public void FullDestination_GoodsWaitThenReturnHome_NothingIsLost()
        {
            // Ship non-perishable timber, then fill B's store behind the shipment's back.
            var world = TestWorlds.TwoSettlements(out var a, out var b, out _, TradeSystem.Id);
            a.ResourceInventory.Set(ResourceTypes.Timber, 1000);
            b.ResourceInventory.Set(ResourceTypes.Timber, 0);
            b.Economy.StorageCapacity.Set(ResourceTypes.Timber, 50);
            var rules = TestWorlds.ExactRules();
            var scheduler = TestWorlds.Scheduler(rules);
            var totalBefore = Total(world, ResourceTypes.Timber);

            var departed = scheduler.AdvanceDay(world).World;
            Assert.That(departed.Shipments.Single().Quantity, Is.EqualTo(50));
            departed.Settlements[1].ResourceInventory.Set(ResourceTypes.Timber, 50); // store filled by other means
            totalBefore += 50;

            var waiting = scheduler.AdvanceDays(departed, 2).World;
            Assert.That(waiting.Shipments.Single().DaysWaiting, Is.EqualTo(1));
            Assert.That(waiting.TradeRoutes[0].Status, Is.EqualTo(TradeRouteStatus.Blocked));
            Assert.That(Total(waiting, ResourceTypes.Timber), Is.EqualTo(totalBefore), "Waiting goods are still counted, on the road.");

            var home = scheduler.AdvanceDays(waiting, rules.MaxWaitDays + 2).World;
            Assert.That(home.Shipments.Where(s => s.IsReturning), Is.Empty, "The shipment came back and was unloaded.");
            Assert.That(home.Settlements[1].ResourceInventory.Get(ResourceTypes.Timber), Is.EqualTo(50));
            Assert.That(Total(home, ResourceTypes.Timber), Is.EqualTo(totalBefore), "Not one unit lost or created.");
            Assert.That(home.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.TradeShipmentReturned), Is.True);
        }

        [Test]
        public void BuyerWithLittleCoin_GetsOnlyWhatItCanPayFor_AndNeverGoesIntoDebt()
        {
            var world = FoodRoute(5000, 0, out _, out var b, out _);
            b.ResourceInventory.Set(ResourceTypes.Coin, 24); // 12 food at 2 coin
            var scheduler = TestWorlds.Scheduler();

            var day1 = scheduler.AdvanceDay(world).World;
            Assert.That(day1.Shipments.Single().Quantity, Is.EqualTo(12));

            var poor = FoodRoute(5000, 0, out _, out var b2, out _);
            b2.ResourceInventory.Set(ResourceTypes.Coin, 4); // 2 food: below the minimum shipment
            Assert.That(scheduler.AdvanceDay(poor).World.Shipments, Is.Empty);

            var arrived = scheduler.AdvanceDays(day1, 2).World;
            Assert.That(arrived.Settlements[1].ResourceInventory.Get(ResourceTypes.Coin), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void NoDuplication_LongRunConservesEveryNonPerishableGood()
        {
            // Full simulation on a generated world: each day, the change in total timber, stone
            // and iron (stock + on the road) must equal what was produced minus what was consumed —
            // and, since Phase 14 (ADR-0071), minus what outlaws on the road took.
            var world = Phase2Worlds.Generated(123456789);
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            var shipments = 0;
            for (var day = 0; day < 200; day++)
            {
                var step = scheduler.AdvanceDay(world);
                Assert.That(step.Committed, Is.True, string.Join("; ", step.Errors));
                foreach (var r in new[] { ResourceTypes.Timber, ResourceTypes.Stone, ResourceTypes.Iron })
                {
                    long produced = 0, consumed = 0;
                    foreach (var s in step.World.Settlements)
                    {
                        var f = s.Economy.FindFlow(r);
                        produced += f.Produced;
                        consumed += f.Consumed;
                        consumed += f.LostOnRoad;
                    }

                    Assert.That(Total(step.World, r) - Total(world, r), Is.EqualTo(produced - consumed), $"{r} on {step.World.CurrentSimulationDate}");
                }

                shipments = Math.Max(shipments, step.World.TradeRoutes.Sum(t => t.ShipmentsDelivered));
                world = step.World;
            }

            Assert.That(shipments, Is.GreaterThan(10), "The scenario must actually trade.");
        }

        [Test]
        public void FailedDay_LeavesNoPartialTransfer()
        {
            var world = FoodRoute(2000, 0, out _, out _, out _);
            world.WorldSettings.EnabledSimulationSystems.Add("bomb");
            var scheduler = TestWorlds.Scheduler();
            scheduler.Register(new DelegateSystem("bomb", 175, _ => throw new InvalidOperationException("later system failed")));
            var before = WorldFingerprint.Of(world);

            var result = scheduler.AdvanceDay(world);

            Assert.That(result.Committed, Is.False);
            Assert.That(WorldFingerprint.Of(result.World), Is.EqualTo(before), "The departure that ran before the failure was discarded.");
            Assert.That(result.World.Shipments, Is.Empty);
        }

        [Test]
        public void Validator_RejectsDuplicateShipmentsOnOneRoute()
        {
            var world = TestWorlds.TwoSettlements(out var a, out var b, out var route);
            ShipmentData Make(string id) => new ShipmentData
            {
                ShipmentId = new ShipmentId(new Guid(id)), TradeRouteId = route.TradeRouteId,
                OriginId = a.SettlementId, DestinationId = b.SettlementId, ResourceTypeId = ResourceTypes.Food, Quantity = 10,
                DepartureDay = 1, ArrivalDay = 3,
            };
            world.Shipments.Add(Make("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a3001"));
            Assert.That(WorldValidator.Validate(world).IsValid, Is.True);

            world.Shipments.Add(Make("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a3002"));
            Assert.That(WorldValidator.Validate(world).Errors, Has.Some.Contains("more than one shipment"));
        }

        private sealed class DelegateSystem : ISimulationSystem
        {
            private readonly Action<SimulationDayContext> _action;
            public DelegateSystem(string id, int order, Action<SimulationDayContext> action) { SystemId = id; ExecutionOrder = order; _action = action; }
            public string SystemId { get; }
            public int ExecutionOrder { get; }
            public void Execute(SimulationDayContext context) => _action(context);
        }
    }
}
