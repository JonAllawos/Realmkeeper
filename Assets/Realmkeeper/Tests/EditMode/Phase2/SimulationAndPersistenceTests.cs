using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>Advancement commands, determinism, save/load and the chronicle across saves.</summary>
    public sealed class SimulationAndPersistenceTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Phase2Worlds.TempDirectory();

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void AdvanceCommands_MoveExactlyOneSevenAndThirtyDays_AndTheLedgerDescribesTheLastDay()
        {
            var session = Phase2Worlds.Session(_dir);
            var start = session.GetOverview().Date;

            Assert.That(session.Execute(new AdvanceDayCommand()).DaysAdvanced, Is.EqualTo(1));
            Assert.That(session.GetOverview().Date, Is.EqualTo(start.AddDays(1)));

            Assert.That(session.Execute(new AdvanceDaysCommand(WorldDate.DaysPerWeek)).DaysAdvanced, Is.EqualTo(7));
            Assert.That(session.GetOverview().Date, Is.EqualTo(start.AddDays(8)));

            Assert.That(session.Execute(new AdvanceDaysCommand(WorldDate.DaysPerMonth)).DaysAdvanced, Is.EqualTo(30));
            var date = session.GetOverview().Date;
            Assert.That(date, Is.EqualTo(start.AddDays(38)));
            Assert.That(date.Month, Is.EqualTo(start.Month + 1), "A month on the 12×30 calendar is exactly 30 days.");

            foreach (var s in session.GetSettlements())
            {
                Assert.That(s.Food.Demand, Is.EqualTo(new FoodEconomyRules().DailyConsumption(s.Population)), "Ledger is today's, for today's population.");
                Assert.That(s.Economy.Sum(r => r.Produced), Is.GreaterThan(0));
            }
        }

        [Test]
        public void SevenSingleDays_ProduceTheSameWorldAsOneSevenDayCommand()
        {
            var a = Phase2Worlds.Generated(1337);
            var b = Phase2Worlds.Generated(1337);
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());

            var batched = scheduler.AdvanceDays(a, 7).World;
            var stepwise = b;
            for (var i = 0; i < 7; i++) stepwise = scheduler.AdvanceDay(stepwise).World;

            Assert.That(WorldFingerprint.Of(stepwise), Is.EqualTo(WorldFingerprint.Of(batched)));
            Assert.That(batched.Shipments.Count + batched.TradeRoutes.Sum(r => r.ShipmentsDelivered), Is.GreaterThan(0), "Trade happened within the week.");
        }

        [Test]
        public void IdenticalStartingStates_StayIdenticalOverAYear()
        {
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            var a = scheduler.AdvanceDays(Phase2Worlds.Generated(2), 360).World;
            var b = scheduler.AdvanceDays(Phase2Worlds.Generated(2), 360).World;

            Assert.That(WorldFingerprint.Of(a), Is.EqualTo(WorldFingerprint.Of(b)));
            Assert.That(a.HistoricalEvents.Count, Is.GreaterThan(10));
        }

        [Test]
        public void PlaybackSpeed_ChangesTimingOnly_NotTheEconomy()
        {
            var slow = Phase2Worlds.Session(_dir);
            var fast = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
            slow.Execute(new SetSimulationSpeedCommand(SimulationSpeed.Slow));
            fast.Execute(new SetSimulationSpeedCommand(SimulationSpeed.VeryFast));
            var slowPlayback = new SimulationPlayback(slow);
            var fastPlayback = new SimulationPlayback(fast);
            slowPlayback.Play();
            fastPlayback.Play();

            // Bounded loops: if a day ever fails, playback pauses and the test must fail, not hang.
            var slowDays = 0;
            for (var tick = 0; tick < 200 && slowDays < 12; tick++) slowDays += slowPlayback.Tick(0.5);
            var fastDays = 0;
            for (var tick = 0; tick < 200 && fastDays < 12; tick++) fastDays += fastPlayback.Tick(0.05);

            Assert.That(slowDays, Is.EqualTo(12));
            Assert.That(fastDays, Is.EqualTo(12));
            var s = slow.GetSettlements();
            var f = fast.GetSettlements();
            for (var i = 0; i < s.Count; i++)
            {
                Assert.That(f[i].Population, Is.EqualTo(s[i].Population));
                Assert.That(f[i].Prosperity, Is.EqualTo(s[i].Prosperity));
                Assert.That(f[i].Economy.Select(r => r.Stock), Is.EqualTo(s[i].Economy.Select(r => r.Stock)));
            }
        }

        [Test]
        public void SaveLoad_MidShipment_RestoresEconomyTradeAndChronicle_AndContinuesIdentically()
        {
            var scheduler = StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory());
            var world = scheduler.AdvanceDays(Phase2Worlds.Generated(123456789), 45).World;
            Assert.That(world.Shipments, Is.Not.Empty, "Fixture saves while goods are on the road.");
            Assert.That(world.HistoricalEvents.Any(e => e.Data.Count > 0), Is.True);
            var serializer = new JsonWorldSerializer();

            var loaded = serializer.Deserialize(serializer.Serialize(world));

            Assert.That(loaded.Status, Is.EqualTo(LoadStatus.Success), string.Join("; ", loaded.Errors));
            Assert.That(WorldFingerprint.Of(loaded.World), Is.EqualTo(WorldFingerprint.Of(world)));
            var continued = scheduler.AdvanceDays(loaded.World, 30).World;
            var uninterrupted = scheduler.AdvanceDays(world, 30).World;
            Assert.That(WorldFingerprint.Of(continued), Is.EqualTo(WorldFingerprint.Of(uninterrupted)));
        }

        [Test]
        public void ChronicleHistory_SurvivesSaveAndLoadThroughTheSession_WithTheSameWording()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(120));
            var before = session.GetRecentEvents(1000);
            Assert.That(before.Count, Is.GreaterThan(5));

            Assert.That(session.Execute(new SaveWorldCommand("chronicle")).IsSuccess, Is.True);
            session.Execute(new AdvanceDaysCommand(30)); // diverge, then load back
            var load = session.Execute(new LoadWorldCommand("chronicle"));
            Assert.That(load.IsSuccess, Is.True, load.Message);

            var after = session.GetRecentEvents(1000);
            Assert.That(after.Select(e => e.EventId), Is.EqualTo(before.Select(e => e.EventId)));
            Assert.That(after.Select(e => e.Summary), Is.EqualTo(before.Select(e => e.Summary)));
            Assert.That(after.Select(e => e.Importance), Is.EqualTo(before.Select(e => e.Importance)));
            Assert.That(after.Select(e => string.Join(",", e.SettlementIds)), Is.EqualTo(before.Select(e => string.Join(",", e.SettlementIds))));
        }

        [Test]
        public void LoadingAPhase1Save_MigratesOreToIron_AndInitializesTheEconomyFromTerrain()
        {
            // Build a genuine v1 document: no economy or trade, "ore", foodProductionPerDay, Phase 1 systems, text events.
            var session = Phase2Worlds.Session(_dir);
            var fresh = Phase2Worlds.Session(Phase2Worlds.TempDirectory());
            session.Execute(new SaveWorldCommand("v2"));
            var doc = JObject.Parse(File.ReadAllText(Path.Combine(_dir, "v2.rkworld.json")));
            doc["schemaVersion"] = 1;
            var w = (JObject)doc["world"];
            w.Remove("tradeRoutes");
            w.Remove("shipments");
            w["settings"]["enabledSimulationSystems"] = new JArray("realmkeeper.food", "realmkeeper.wellbeing");
            foreach (JObject s in (JArray)w["settlements"])
            {
                s.Remove("economy");
                s["foodProductionPerDay"] = 42;
                foreach (JObject r in (JArray)s["resources"])
                {
                    if ((string)r["resourceTypeId"] == "iron") r["resourceTypeId"] = "ore";
                }
            }

            foreach (JObject e in (JArray)w["historicalEvents"])
            {
                e.Remove("settlementIds");
                e.Remove("data");
                e.Remove("importance");
            }

            File.WriteAllText(Path.Combine(_dir, "v1.rkworld.json"), doc.ToString());

            var result = session.Execute(new LoadWorldCommand("v1"));

            Assert.That(result.IsSuccess, Is.True, result.Message + " " + string.Join("; ", result.Errors));
            Assert.That(result.Message, Does.Contain("economy added"));
            var settlements = session.GetSettlements();
            var freshSettlements = fresh.GetSettlements();
            for (var i = 0; i < settlements.Count; i++)
            {
                Assert.That(settlements[i].Specialization, Is.EqualTo(freshSettlements[i].Specialization), "Same terrain, same economy.");
                Assert.That(settlements[i].Production.Count, Is.GreaterThan(0));
                Assert.That(settlements[i].Resources.Any(r => r.ResourceTypeId.Value == "ore"), Is.False);
            }

            Assert.That(session.GetTradeRoutes().Count, Is.EqualTo(fresh.GetTradeRoutes().Count));
            Assert.That(session.GetRecentEvents(10).Last().Summary, Does.Contain("chronicle"), "Legacy text events still read correctly.");
            Assert.That(session.Execute(new AdvanceDaysCommand(10)).IsSuccess, Is.True, "The upgraded world simulates.");
        }

        [Test]
        public void EventFormatter_BuildsSentencesFromFacts_AndKeepsLegacyText()
        {
            var a = new SettlementId(new System.Guid("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1001"));
            var b = new SettlementId(new System.Guid("8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a1002"));
            string Names(SettlementId id) => id == a ? "Ashford" : id == b ? "Brookmere" : null;

            var delivery = new HistoricalEvent(EventId.NewRandom(), TestWorlds.Start, HistoricalEventType.TradeShipmentDelivered, EventSeverity.Info,
                EventImportance.Minor, new[] { a, b },
                new System.Collections.Generic.Dictionary<string, string> { ["resource"] = "food", ["quantity"] = "1200", ["payment"] = "2400", ["lost"] = "40" });
            Assert.That(EventFormatter.Format(delivery, Names), Is.EqualTo("1,200 food from Ashford reached Brookmere for 2,400 coin, 40 spoiled on the road."));

            var shortage = new HistoricalEvent(EventId.NewRandom(), TestWorlds.Start, HistoricalEventType.ResourceShortageBegan, EventSeverity.Warning,
                EventImportance.Notable, new[] { a }, new System.Collections.Generic.Dictionary<string, string> { ["resource"] = "iron", ["stock"] = "3", ["demand"] = "2" });
            Assert.That(EventFormatter.Format(shortage, Names), Is.EqualTo("Ashford is short of iron: 3 left against 2 needed a day."));

            var legacy = new HistoricalEvent(EventId.NewRandom(), TestWorlds.Start, HistoricalEventType.FoodShortageBegan, EventSeverity.Warning, a,
                "Food is running short in Ashford: about 9 days of reserves remain.");
            Assert.That(EventFormatter.Format(legacy, Names), Is.EqualTo(legacy.Description));
        }

        [Test]
        public void SettlementSummary_ExposesTheEconomyForTheInspector()
        {
            var session = Phase2Worlds.Session(_dir);
            session.Execute(new AdvanceDaysCommand(20));

            foreach (var s in session.GetSettlements())
            {
                Assert.That(s.Economy.Select(r => r.ResourceTypeId), Is.EqualTo(ResourceTypes.All), "One row per resource, in display order.");
                Assert.That(s.Specialization, Is.Not.EqualTo(SettlementSpecialization.None));
                Assert.That(s.Employed, Is.InRange(1, s.Workforce));
                var food = s.Food;
                Assert.That(s.NetFoodPerDay, Is.EqualTo(food.Produced - food.Demand));
                if (s.NetFoodPerDay < 0) Assert.That(s.DaysOfFoodRemaining, Is.EqualTo(food.Stock / -s.NetFoodPerDay));
                else Assert.That(s.DaysOfFoodRemaining, Is.Null);
                Assert.That(session.GetTradeRoutes(s.SettlementId).All(r => r.OriginId == s.SettlementId || r.DestinationId == s.SettlementId), Is.True);
            }
        }
    }
}
