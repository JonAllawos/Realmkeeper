using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Generation;

namespace Realmkeeper.Tests.EditMode.Application
{
    public sealed class WorldSessionTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "realmkeeper-session-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldSession NewSessionWithWorld(long seed = 9001)
        {
            var session = RealmkeeperComposition.CreateSession(_dir);
            var created = session.Execute(new CreateWorldCommand("Emberfell", seed));
            Assert.That(created.IsSuccess, Is.True, string.Join("; ", created.Errors));
            return session;
        }

        [Test]
        public void CreateWorld_ExposesOverviewSettlementsAndGeography()
        {
            var changes = new List<WorldChange>();
            var session = RealmkeeperComposition.CreateSession(_dir);
            session.WorldChanged += changes.Add;

            session.Execute(new CreateWorldCommand("Emberfell", 9001));

            var overview = session.GetOverview();
            Assert.That(overview.WorldName, Is.EqualTo("Emberfell"));
            Assert.That(overview.Date, Is.EqualTo(new WorldSettings().StartingDate));
            Assert.That(session.GetSettlements().Count, Is.EqualTo(3));
            Assert.That(session.Map, Is.Not.Null);
            Assert.That(changes.Single().GeographyChanged, Is.True);
        }

        [Test]
        public void AdvanceCommands_MoveDateAndNotifyWithNewEvents()
        {
            var session = NewSessionWithWorld();
            var changes = new List<WorldChange>();
            session.WorldChanged += changes.Add;
            var start = session.GetOverview().Date;

            Assert.That(session.Execute(new AdvanceDayCommand()).IsSuccess, Is.True);
            var week = session.Execute(new AdvanceDaysCommand(WorldDate.DaysPerWeek));

            Assert.That(week.DaysAdvanced, Is.EqualTo(7));
            Assert.That(session.GetOverview().Date, Is.EqualTo(start.AddDays(8)));
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes.All(c => c.Reason == WorldChangeReason.Advanced && !c.GeographyChanged), Is.True);
        }

        [Test]
        public void SettlementQuery_UsesIdAndReturnsImmutableSnapshot()
        {
            var session = NewSessionWithWorld();
            var first = session.GetSettlements()[0];

            var found = session.FindSettlement(first.SettlementId);
            Assert.That(found.Name, Is.EqualTo(first.Name));
            Assert.That(found.FactionName, Is.Not.Null.And.Not.Empty);
            Assert.That(found.FoodConsumptionPerDay, Is.EqualTo((found.Population + 9) / 10));
            Assert.That(session.FindSettlement(SettlementId.NewRandom()), Is.Null);

            session.Execute(new AdvanceDaysCommand(30));
            Assert.That(found.FoodReserves, Is.EqualTo(first.FoodReserves), "Snapshots do not change underneath the caller.");
        }

        [Test]
        public void RecentEvents_AreNewestFirstAndCapped()
        {
            var session = NewSessionWithWorld();
            session.Execute(new AdvanceDaysCommand(360));

            var events = session.GetRecentEvents(5);
            Assert.That(events.Count, Is.InRange(2, 5));
            for (var i = 1; i < events.Count; i++) Assert.That(events[i - 1].Date, Is.GreaterThanOrEqualTo(events[i].Date));
            Assert.That(events.All(e => !string.IsNullOrWhiteSpace(e.Summary)), Is.True, "Every event is formatted for the UI.");
            var all = session.GetRecentEvents(10000);
            Assert.That(all.Any(e => e.EventType == HistoricalEventType.TradeShipmentDelivered), Is.True,
                "Within a year the settlements should trade and the chronicle should say so.");
            Assert.That(session.GetRecentEvents(10000, EventImportance.Notable).All(e => e.Importance >= EventImportance.Notable), Is.True);
        }

        [Test]
        public void SaveThenLoad_RestoresWorldAndRegeneratesIdenticalGeography()
        {
            var session = NewSessionWithWorld();
            session.Execute(new AdvanceDaysCommand(45));
            var before = session.GetOverview();
            var mapBefore = RegionGeneratorTests.MapFingerprint(session.Map);
            Assert.That(session.Execute(new SaveWorldCommand("campaign")).IsSuccess, Is.True);

            var fresh = RealmkeeperComposition.CreateSession(_dir);
            var load = fresh.Execute(new LoadWorldCommand("campaign"));

            Assert.That(load.IsSuccess, Is.True, string.Join("; ", load.Errors));
            Assert.That(fresh.GetOverview().Date, Is.EqualTo(before.Date));
            Assert.That(fresh.GetOverview().WorldId, Is.EqualTo(before.WorldId));
            Assert.That(RegionGeneratorTests.MapFingerprint(fresh.Map), Is.EqualTo(mapBefore));
        }

        [Test]
        public void FailedLoad_LeavesCurrentWorldUntouched()
        {
            var session = NewSessionWithWorld();
            var before = session.GetOverview();
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "broken" + JsonWorldStore.Extension), "{ nope");

            var missing = session.Execute(new LoadWorldCommand("nothing-here"));
            var corrupt = session.Execute(new LoadWorldCommand("broken"));

            Assert.That(missing.IsSuccess, Is.False);
            Assert.That(corrupt.IsSuccess, Is.False);
            Assert.That(corrupt.Message, Does.Contain("damaged"));
            Assert.That(session.GetOverview().WorldId, Is.EqualTo(before.WorldId));
            Assert.That(session.GetOverview().Date, Is.EqualTo(before.Date));
        }

        [Test]
        public void Commands_FailGracefullyWithoutAWorld()
        {
            var session = RealmkeeperComposition.CreateSession(_dir);
            Assert.That(session.Execute(new AdvanceDayCommand()).IsSuccess, Is.False);
            Assert.That(session.Execute(new SaveWorldCommand("x")).IsSuccess, Is.False);
            Assert.That(session.GetOverview(), Is.Null);
            Assert.That(session.GetSettlements(), Is.Empty);
        }

        [Test]
        public void Playback_AdvancesWholeDaysByElapsedTime_AndOnlyWhilePlaying()
        {
            var session = NewSessionWithWorld();
            var playback = new SimulationPlayback(session, maxDaysPerTick: 4);
            var start = session.GetOverview().Date;

            Assert.That(playback.Tick(10.0), Is.Zero, "Paused playback never advances.");

            playback.Play();
            Assert.That(playback.Tick(0.6), Is.Zero);  // Normal speed: 1 second per day.
            Assert.That(playback.Tick(0.6), Is.EqualTo(1));
            Assert.That(playback.Tick(2.0), Is.EqualTo(2));
            Assert.That(playback.Tick(100.0), Is.EqualTo(4), "Hitches are clamped, not replayed.");
            Assert.That(session.GetOverview().Date, Is.EqualTo(start.AddDays(7)));

            session.Execute(new SetSimulationSpeedCommand(SimulationSpeed.VeryFast));
            Assert.That(playback.Tick(0.25), Is.EqualTo(2));

            playback.Pause();
            Assert.That(playback.Tick(5), Is.Zero);
        }
    }
}
