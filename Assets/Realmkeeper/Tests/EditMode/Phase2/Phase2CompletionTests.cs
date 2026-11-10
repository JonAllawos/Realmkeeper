using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>
    /// Phase 2 completion: route suspension, the v3 → v4 save migration, explained prosperity and
    /// stability, food outlook, map indicators, pausing, chronicle integrity, the daily order and
    /// a measured 30-day run.
    /// </summary>
    public sealed class Phase2CompletionTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Phase2Worlds.TempDirectory();

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static long Total(WorldData w, ResourceTypeId r) =>
            w.Settlements.Sum(s => (long)s.ResourceInventory.Get(r)) + w.Shipments.Where(s => s.ResourceTypeId == r).Sum(s => (long)s.Quantity);

        // ----- Daily order --------------------------------------------------------------------

        [Test]
        public void DailyOrder_IsLabourProductionTradeConsumptionWellbeingPopulationChronicle()
        {
            var ids = TestWorlds.Scheduler().Systems.Select(s => s.SystemId).ToArray();
            Assert.That(ids, Is.EqualTo(new[]
            {
                LabourSystem.Id, ProductionSystem.Id, TradeSystem.Id, ConsumptionSystem.Id,
                SettlementWellbeingSystem.Id, PopulationSystem.Id, PoliticalSystem.Id, ChronicleSystem.Id,
            }), "The order documented in Documentation/Systems/Economy.md (politics added in Phase 3).");
            var orders = TestWorlds.Scheduler().Systems.Select(s => s.ExecutionOrder).ToArray();
            Assert.That(orders, Is.Ordered.Ascending);
            Assert.That(orders.Distinct().Count(), Is.EqualTo(orders.Length), "No two systems share a slot.");
        }

        // ----- Route suspension ---------------------------------------------------------------

        [Test]
        public void SuspendedRoute_SendsNothing_ButTheShipmentOnTheRoadStillArrives_AndResumingTradesAgain()
        {
            var world = TestWorlds.TwoSettlements(out var a, out var b, out var route, TradeSystem.Id);
            a.ResourceInventory.Set(ResourceTypes.Food, 5000);
            b.ResourceInventory.Set(ResourceTypes.Food, 0);
            var scheduler = TestWorlds.Scheduler();

            var day1 = scheduler.AdvanceDay(world).World;
            Assert.That(day1.Shipments.Count, Is.EqualTo(1), "Fixture: a shipment left on day 1.");
            var foodBefore = Total(day1, ResourceTypes.Food);
            day1.TradeRoutes[0].IsSuspended = true;

            var day6 = scheduler.AdvanceDays(day1, 5).World;
            Assert.That(day6.Shipments, Is.Empty, "No new shipment while suspended.");
            Assert.That(day6.TradeRoutes[0].ShipmentsDelivered, Is.EqualTo(1), "The shipment already on the road arrived.");
            Assert.That(day6.TradeRoutes[0].Status, Is.EqualTo(TradeRouteStatus.Suspended));
            Assert.That(Total(day6, ResourceTypes.Food), Is.EqualTo(foodBefore - day6.TradeRoutes[0].Delivered.Get(ResourceTypes.Food) / 9),
                "Only the recorded 10% transit loss is gone.");
            var originAfter = day6.Settlements[0].FoodReserves;

            day6.TradeRoutes[0].IsSuspended = false;
            var day7 = scheduler.AdvanceDay(day6).World;
            Assert.That(day7.Shipments.Count, Is.EqualTo(1), "Resumed: trade starts again at the next check.");
            Assert.That(day7.TradeRoutes[0].Status, Is.EqualTo(TradeRouteStatus.InTransit));
            Assert.That(day7.Settlements[0].FoodReserves, Is.LessThan(originAfter));
        }

        [Test]
        public void RouteSuspendedFromTheStart_MovesNothingInThirtyDays()
        {
            var world = TestWorlds.TwoSettlements(out var a, out var b, out var route, TradeSystem.Id);
            a.ResourceInventory.Set(ResourceTypes.Food, 5000);
            b.ResourceInventory.Set(ResourceTypes.Food, 0);
            route.IsSuspended = true;
            var before = world.Settlements.Select(s => s.ResourceInventory.Stacks.Sum(x => (long)x.Quantity)).ToArray();

            var after = TestWorlds.Scheduler().AdvanceDays(world, 30).World;

            Assert.That(after.Shipments, Is.Empty);
            Assert.That(after.TradeRoutes[0].ShipmentsDelivered, Is.EqualTo(0));
            Assert.That(after.Settlements.Select(s => s.ResourceInventory.Stacks.Sum(x => (long)x.Quantity)), Is.EqualTo(before));
        }

        [Test]
        public void SuspendCommand_IsValidated_Chronicled_AndSaved()
        {
            var session = Phase2Worlds.Session(_dir);
            var route = session.GetTradeRoutes(session.GetSettlements()[0].SettlementId).First();
            var events = session.GetOverview().EventCount;

            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(new TradeRouteId(Guid.NewGuid()), true)).Status, Is.EqualTo(CommandStatus.NotFound));
            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(default, true)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(route.TradeRouteId, false)).Status, Is.EqualTo(CommandStatus.ValidationFailed),
                "Resuming a route that is trading is refused.");
            Assert.That(session.GetOverview().EventCount, Is.EqualTo(events), "Refused commands change nothing.");

            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(route.TradeRouteId, true)).IsSuccess, Is.True);
            var suspended = session.GetTradeRoutes(route.OriginId).Single(r => r.TradeRouteId == route.TradeRouteId);
            Assert.That(suspended.IsSuspended, Is.True);
            var e = session.GetRecentEvents(3).First();
            Assert.That(e.EventType, Is.EqualTo(HistoricalEventType.TradeRouteSuspended));
            Assert.That(e.SettlementIds, Is.EqualTo(new[] { route.OriginId, route.DestinationId }));
            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(route.TradeRouteId, true)).Status, Is.EqualTo(CommandStatus.ValidationFailed));

            session.Execute(new AdvanceDaysCommand(20));
            Assert.That(session.Dispatch(new SaveWorldCommand("suspended")).IsSuccess, Is.True);
            session.Execute(new AdvanceDaysCommand(5));
            Assert.That(session.Execute(new LoadWorldCommand("suspended")).IsSuccess, Is.True);
            Assert.That(session.GetTradeRoutes(route.OriginId).Single(r => r.TradeRouteId == route.TradeRouteId).IsSuspended, Is.True, "Saved and loaded.");

            Assert.That(session.Dispatch(new SetTradeRouteSuspendedCommand(route.TradeRouteId, false)).IsSuccess, Is.True);
            Assert.That(session.GetRecentEvents(1).First().EventType, Is.EqualTo(HistoricalEventType.TradeRouteResumed));
        }

        // ----- Save migration v3 → v4 ---------------------------------------------------------

        [Test]
        public void Version3Save_LoadsWithEveryRouteTrading_AndNothingElseChanges()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(25)); // shipments on the road, events, ledgers
            Assert.That(session.Dispatch(new SaveWorldCommand("current")).IsSuccess, Is.True);
            var serializer = new JsonWorldSerializer();
            var json = File.ReadAllText(Path.Combine(_dir, "current" + JsonWorldStore.Extension));
            var current = serializer.Deserialize(json);
            Assert.That(current.IsSuccess, Is.True);

            var doc = JObject.Parse(json);
            doc["schemaVersion"] = 3;
            foreach (JObject r in (JArray)doc["world"]["tradeRoutes"]) r.Remove("isSuspended");
            var migrated = serializer.Deserialize(doc.ToString());

            Assert.That(migrated.IsSuccess, Is.True, string.Join("; ", migrated.Errors));
            Assert.That(migrated.World.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
            Assert.That(migrated.World.TradeRoutes.All(r => !r.IsSuspended), Is.True, "Documented default: not suspended.");
            Assert.That(WorldFingerprint.Of(migrated.World), Is.EqualTo(WorldFingerprint.Of(current.World)),
                "Stocks, routes, shipments and history are exactly as saved.");
        }

        // ----- Prosperity and stability explained -----------------------------------------------

        [Test]
        public void Contributions_AddUpToTheSimulationsTargets_ForEveryFactorCombination()
        {
            var scores = new[] { 0, 1, 37, 50, 99, 100 };
            var penalties = new[] { 0, 5, 10, 32, 120 };
            var checkedCombos = 0;
            foreach (var food in scores)
            foreach (var goods in scores)
            foreach (var output in scores)
            foreach (var penalty in penalties)
            {
                var f = new WellbeingFactors(food, goods, 100 - goods, output, (food + output) / 2, 100 - food, goods, penalty, penalty > 100 ? 15 : 0);
                Assert.That(EconomyMath.TargetFrom(EconomyMath.ProsperityContributions(f)), Is.EqualTo(EconomyMath.ProsperityTarget(f)));
                foreach (var prosperity in new[] { 0, 64, 100 })
                {
                    Assert.That(EconomyMath.TargetFrom(EconomyMath.StabilityContributions(f, prosperity)), Is.EqualTo(EconomyMath.StabilityTarget(f, prosperity)));
                }

                checkedCombos++;
            }

            Assert.That(checkedCombos, Is.EqualTo(6 * 6 * 6 * 5));
        }

        [Test]
        public void SettlementBreakdown_ExplainsPenaltiesAndMatchesTheFormula()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(30));
            foreach (var s in session.GetSettlements())
            {
                var w = s.Wellbeing;
                Assert.That(w.Prosperity.Select(p => p.Factor), Is.EqualTo(new[]
                {
                    WellbeingFactor.Food, WellbeingFactor.Goods, WellbeingFactor.Trade, WellbeingFactor.Output, WellbeingFactor.Shortages,
                }));
                Assert.That(w.ProsperityTarget, Is.InRange(SettlementData.MinRating, SettlementData.MaxRating));
                Assert.That(w.StabilityTarget, Is.InRange(SettlementData.MinRating, SettlementData.MaxRating));
                var shortagePenalty = w.Prosperity.Single(p => p.Factor == WellbeingFactor.Shortages);
                Assert.That(shortagePenalty.Hundredths, Is.LessThanOrEqualTo(0), "Penalties only ever lower a target.");
                if (s.IsInFoodShortage) Assert.That(shortagePenalty.Points, Is.LessThanOrEqualTo(-10), "A food shortage costs at least 10 points.");
                Assert.That(w.Stability.Single(p => p.Factor == WellbeingFactor.Prosperity).Score, Is.EqualTo(s.Prosperity));
            }
        }

        // ----- Food outlook and map indicators ------------------------------------------------

        [TestCase(false, 0, RatingTrend.Falling, ShortageOutlook.None)]
        [TestCase(true, 0, RatingTrend.Rising, ShortageOutlook.Easing)]
        [TestCase(true, 0, RatingTrend.Falling, ShortageOutlook.Worsening)]
        [TestCase(true, 0, RatingTrend.Steady, ShortageOutlook.Unchanged)]
        [TestCase(false, 2, RatingTrend.Steady, ShortageOutlook.Worsening)]
        [TestCase(true, 1, RatingTrend.Rising, ShortageOutlook.Easing)]
        public void ShortageOutlook_FollowsTheReserves(bool inShortage, int daysWithoutFood, RatingTrend trend, ShortageOutlook expected)
        {
            Assert.That(SettlementSummary.OutlookFor(inShortage, daysWithoutFood, trend), Is.EqualTo(expected));
        }

        [Test]
        public void Indicators_AreDerivedFromSimulationValues()
        {
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 40, false, 60, 60), Is.EqualTo(SettlementIndicators.None));
            Assert.That(SettlementSummary.IndicatorsFor(true, 0, 40, false, 60, 60), Is.EqualTo(SettlementIndicators.FoodShortage));
            Assert.That(SettlementSummary.IndicatorsFor(false, 3, 40, false, 60, 60), Is.EqualTo(SettlementIndicators.FoodShortage));
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 75, false, 60, 60), Is.EqualTo(SettlementIndicators.StrongProduction));
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 74, false, 60, 60), Is.EqualTo(SettlementIndicators.None));
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 0, true, 60, 60), Is.EqualTo(SettlementIndicators.ActiveTrade));
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 0, false, 60, 55), Is.EqualTo(SettlementIndicators.DecliningStability));
            Assert.That(SettlementSummary.IndicatorsFor(false, 0, 0, false, 60, 56), Is.EqualTo(SettlementIndicators.None), "A small dip is not flagged.");
            Assert.That(SettlementSummary.IndicatorsFor(true, 1, 90, true, 70, 40),
                Is.EqualTo(SettlementIndicators.FoodShortage | SettlementIndicators.StrongProduction | SettlementIndicators.ActiveTrade | SettlementIndicators.DecliningStability));
        }

        [Test]
        public void EmptyGranary_ShowsAShortageWithAnOutlook_AndTheMapFlagsIt()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(3));
            Assert.That(session.Dispatch(new SaveWorldCommand("granary")).IsSuccess, Is.True);

            // Empty the town's granary in the save file, as a famine would leave it.
            var path = Path.Combine(_dir, "granary" + JsonWorldStore.Extension);
            var doc = JObject.Parse(File.ReadAllText(path));
            var town = (JObject)((JArray)doc["world"]["settlements"]).First(t => (string)t["settlementType"] == "Town");
            foreach (JObject stack in (JArray)town["resources"])
            {
                if ((string)stack["resourceTypeId"] == "food") stack["quantity"] = 0;
            }

            File.WriteAllText(path, doc.ToString());
            Assert.That(session.Execute(new LoadWorldCommand("granary")).IsSuccess, Is.True);
            session.Execute(new AdvanceDayCommand());

            var s = session.GetSettlements().Single(x => x.SettlementType == SettlementType.Town);
            Assert.That(s.IsInFoodShortage || s.DaysWithoutFood > 0, Is.True, "Fixture: the town is short of food.");
            Assert.That(s.Indicators.HasFlag(SettlementIndicators.FoodShortage), Is.True);
            Assert.That(s.FoodShortageOutlook, Is.Not.EqualTo(ShortageOutlook.None));
            Assert.That(s.FoodShortageOutlook, Is.EqualTo(SettlementSummary.OutlookFor(s.IsInFoodShortage, s.DaysWithoutFood, s.FoodReserveTrend)));
            Assert.That(s.FoodReserveTrend, Is.EqualTo(s.Food.NetChange > 0 ? RatingTrend.Rising : s.Food.NetChange < 0 ? RatingTrend.Falling : RatingTrend.Steady));
            Assert.That(s.FoodProductionMeetsDemand, Is.EqualTo(s.Food.Produced >= s.Food.Demand));
        }

        [Test]
        public void ActiveTradeIndicator_MatchesShipmentsOnTheRoad()
        {
            var session = Phase2Worlds.Session(_dir);
            for (var day = 0; day < 20; day++)
            {
                session.Execute(new AdvanceDayCommand());
                foreach (var s in session.GetSettlements())
                {
                    var onRoad = session.GetTradeRoutes(s.SettlementId).Any(r => r.HasShipmentOnRoad && !r.InTransitReturning);
                    Assert.That(s.Indicators.HasFlag(SettlementIndicators.ActiveTrade), Is.EqualTo(onRoad), $"Day {day + 1}, {s.Name}");
                }
            }
        }

        // ----- Pausing ------------------------------------------------------------------------

        [Test]
        public void PausedPlayback_ChangesNothing_AndPlayingAdvancesWholeDays()
        {
            var session = Phase2Worlds.Session(_dir);
            var playback = new SimulationPlayback(session);
            var before = session.GetOverview().Date;
            var stockBefore = session.GetSettlements().Select(s => s.FoodReserves).ToArray();

            Assert.That(playback.Tick(500), Is.EqualTo(0), "Paused: no day passes however much time does.");
            Assert.That(session.GetOverview().Date, Is.EqualTo(before));
            Assert.That(session.GetSettlements().Select(s => s.FoodReserves), Is.EqualTo(stockBefore));

            playback.Play();
            var seconds = SimulationPlayback.SecondsPerDay(session.GetOverview().Speed);
            var days = playback.Tick(seconds * 3 + seconds / 2);
            Assert.That(days, Is.EqualTo(3));
            Assert.That(session.GetOverview().Date, Is.EqualTo(before.AddDays(3)));

            playback.Pause();
            var paused = session.GetOverview().Date;
            playback.Tick(seconds * 10);
            Assert.That(session.GetOverview().Date, Is.EqualTo(paused), "Pausing stops the economy; partial time does not leak.");
        }

        // ----- Chronicle integrity -------------------------------------------------------------

        [Test]
        public void ChronicleEvents_ReferenceRealSettlementsAndResources_AndAreNotDailySpam()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(180));
            Assert.That(session.Dispatch(new SaveWorldCommand("chronicle")).IsSuccess, Is.True);
            var world = new JsonWorldSerializer().Deserialize(File.ReadAllText(Path.Combine(_dir, "chronicle" + JsonWorldStore.Extension))).World;

            var ids = new HashSet<SettlementId>(world.Settlements.Select(s => s.SettlementId));
            var routeIds = new HashSet<string>(world.TradeRoutes.Select(r => r.TradeRouteId.ToString()));
            foreach (var e in world.HistoricalEvents)
            {
                Assert.That(e.SettlementIds.All(ids.Contains), Is.True, $"{e.EventType} references an unknown settlement.");
                Assert.That(e.Date, Is.LessThanOrEqualTo(world.CurrentSimulationDate));
                var resource = e.Get("resource");
                if (resource != null) Assert.That(ResourceTypes.IsKnown(new ResourceTypeId(resource)), Is.True, $"{e.EventType} names unknown resource '{resource}'.");
                var route = e.Get("route");
                if (route != null) Assert.That(routeIds.Contains(route), Is.True, $"{e.EventType} names an unknown route.");
            }

            // No event type is reported for the same settlement more than once a week on average.
            foreach (var group in world.HistoricalEvents.Where(e => e.SettlementIds.Count > 0).GroupBy(e => (e.EventType, e.SettlementIds[0])))
            {
                Assert.That(group.Count(), Is.LessThanOrEqualTo(180 / 7), $"{group.Key.EventType} is spamming the chronicle.");
            }
        }

        // ----- Performance ----------------------------------------------------------------------

        [Test]
        public void ThirtyDayRun_IsMeasured()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(5)); // warm up (JIT)
            var timer = Stopwatch.StartNew();
            var result = session.Execute(new AdvanceDaysCommand(30));
            timer.Stop();

            Assert.That(result.DaysAdvanced, Is.EqualTo(30));
            var settlements = session.GetSettlements().Count;
            TestContext.WriteLine($"30-day run: {timer.Elapsed.TotalMilliseconds:0.0} ms total, {timer.Elapsed.TotalMilliseconds / 30:0.00} ms per day, " +
                                  $"{settlements} settlements, {session.GetSettlements().Sum(s => session.GetResidents(s.SettlementId).Count)} named residents, " +
                                  $"{session.GetSettlements().SelectMany(s => session.GetTradeRoutes(s.SettlementId)).Select(r => r.TradeRouteId).Distinct().Count()} trade routes.");
            Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(10), "Generous bound: catches a pathological regression, not a benchmark.");
        }
    }
}
